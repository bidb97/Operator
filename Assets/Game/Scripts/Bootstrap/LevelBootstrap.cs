using System.Collections;
using Operator.Data;
using Operator.Depth.Core;
using Operator.Depth.Unity;
using Operator.Managers;
using Operator.Missions;
using UnityEngine;

namespace Operator.Bootstrap
{
    public class LevelBootstrap : MonoBehaviour
    {
        public static bool SuppressAutoBegin { get; set; }

        public bool IsReady { get; private set; }

        [SerializeField] DepthWorld depthWorld;
        [SerializeField] MissionCatalog missionCatalog;
        [SerializeField] Transform cameraRig;
        [SerializeField] Transform surfaceRoot;
        [SerializeField] Transform drillRoot;

        MissionPreloader _missionPreloader;

        void Awake()
        {
            Debug.Log($"[LevelBootstrap] Awake on {name}, scene={gameObject.scene.name}");
            _missionPreloader = GetComponent<MissionPreloader>();
            if (_missionPreloader == null)
                _missionPreloader = gameObject.AddComponent<MissionPreloader>();
        }

        void Start()
        {
            if (!SuppressAutoBegin)
            {
                BeginLevel();
                return;
            }

            // Камеру/дрон/базу расставляем сразу же в этом кадре — иначе скрипты с
            // Start(), зависящие от позиции камеры (параллакс, оверлеи), успевают
            // выполниться раньше, чем PlaceSceneObjects() из BeginLevelAsync(),
            // который откладывается до сигнала BuildRequested из Main Menu.
            PlaceSceneObjects(GetLevelCamera());

            StartCoroutine(BeginWhenRequested());
        }

        IEnumerator BeginWhenRequested()
        {
            Debug.Log("[LevelBootstrap] ждём сигнал от Main Menu");

            while (!NewGameLoadFlow.BuildRequested)
                yield return null;

            Debug.Log("[LevelBootstrap] сигнал получен, создание мира");

            yield return BeginLevelAsync();

            NewGameLoadFlow.BuildFinished = true;
            NewGameLoadFlow.BuildProgress = 1f;
        }

        public void BeginLevel()
        {
            if (!TryPrepareLevel(out var world, out var assets, out var cam, out var session))
                return;

            TryIssueInstantStartingMission(session);

            PlaceSceneObjects(cam);

            if (depthWorld != null && cam != null)
                depthWorld.SyncVisibleArea(world, assets, cam);

            IsReady = true;
            NewGameLoadFlow.BuildProgress = 1f;
            StartMissionPreloader();
        }

        public IEnumerator BeginLevelAsync()
        {
            IsReady = false;
            NewGameLoadFlow.BuildFinished = false;
            NewGameLoadFlow.BuildProgress = 0f;

            if (!TryPrepareLevel(out var world, out var assets, out var cam, out var session))
                yield break;

            TryIssueInstantStartingMission(session);

            yield return null;

            PlaceSceneObjects(cam);

            if (depthWorld != null && cam != null)
                yield return BuildVisibleAreaAsync(world, assets, cam, session.Seed);
            else
                Debug.Log("[LevelBootstrap] Тайлы пропущены: depthWorld или камера не найдены");

            Debug.Log("[LevelBootstrap] Тайлы готовы");
            IsReady = true;
            StartMissionPreloader();
        }

        void StartMissionPreloader()
        {
            if (missionCatalog == null)
            {
                Debug.LogWarning("[LevelBootstrap] MissionCatalog не назначен — миссии не выдаются");
                return;
            }

            _missionPreloader.Begin(missionCatalog);
        }

        void TryIssueInstantStartingMission(WorldSession session)
        {
            if (missionCatalog == null || session == null)
                return;

            var bootstrap = GameBootstrap.Instance;
            var worldGen = bootstrap?.WorldGen;
            var sectorSize = worldGen != null ? worldGen.ResourceClusterSpacing : 0;
            if (sectorSize <= 0)
                return;

            MissionIssuer.TryIssueInstantNext(session, missionCatalog, sectorSize);
        }

        IEnumerator BuildVisibleAreaAsync(World world, AssetManager assets, Camera cam, int worldSeed)
        {
            var build = depthWorld.SyncVisibleAreaAsync(world, assets, cam, worldSeed);

            while (build.MoveNext())
            {
                NewGameLoadFlow.SetWorldProgress(depthWorld.BuildProgress);
                yield return build.Current;
            }

            NewGameLoadFlow.SetWorldProgress(1f);
        }

        bool TryPrepareLevel(
            out World world,
            out AssetManager assets,
            out Camera cam,
            out WorldSession session)
        {
            world = null;
            assets = null;
            cam = null;
            session = null;

            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null || GameManager.Instance == null)
            {
                Debug.LogError("GameBootstrap or GameManager not found.");
                return false;
            }

            session = gameBootstrap.Session;
            assets = gameBootstrap.Assets;

            world = new World(session.Seed, session.Radius, gameBootstrap.WorldGen);

            GameManager.Instance.World = world;

            GameManager.Instance.WormWorld = new WormWorld(
                session.Seed,
                session.Radius,
                gameBootstrap.WorldGen);

            GameManager.Instance.MagneticAnomalyWorld = new MagneticAnomalyWorld(
                session.Seed,
                session.Radius,
                gameBootstrap.WorldGen);

            cam = GetLevelCamera();
            return true;
        }

        void PlaceSceneObjects(Camera cam)
        {
            if (surfaceRoot != null)
            {
                var baseTransform = surfaceRoot.Find("Yoke");

                surfaceRoot.position = Vector3.zero;

                if (baseTransform != null)
                    baseTransform.localPosition = Vector3.zero;
            }

            if (drillRoot != null)
            {
                drillRoot.position = WorldGrid.DroneCellToWorld(
                    new Vector2Int(GarageBounds.ShaftX, GarageBounds.TopY));
            }

            if (cameraRig != null && drillRoot != null)
            {
                var follow = cameraRig.GetComponent<CameraFollow>();

                if (follow != null)
                {
                    follow.SetTarget(drillRoot);
                    follow.SyncPosition();
                }
            }
        }

        Camera GetLevelCamera()
        {
            if (cameraRig != null)
            {
                var cam = cameraRig.GetComponentInChildren<Camera>(true);
                if (cam != null)
                    return cam;
            }

            return Camera.main;
        }
    }
}
