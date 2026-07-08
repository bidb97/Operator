using Operator.Data;
using Operator.Missions;
using UnityEngine;
using Operator.Depth.Core;
using Operator.Depth.Unity;
using Operator.Managers;

namespace Operator.Bootstrap
{
    public class LevelBootstrap : MonoBehaviour
    {
        [SerializeField] DepthWorld depthWorld;
        [SerializeField] MissionCatalog missionCatalog;
        [SerializeField] Transform cameraRig;
        [SerializeField] Transform surfaceRoot;
        [SerializeField] Transform drillRoot;

         void Start()
        {
            BeginLevel();
        }
        void BeginLevel()
        {
            var gameBootstrap = GameBootstrap.Instance;

            if (gameBootstrap == null || GameManager.Instance == null) 
            {
                Debug.LogError("GameBootstrap or GameManager not found.");
                return;
            }

            var session = gameBootstrap.Session;

            var world = new World(session.Seed, session.Radius, gameBootstrap.WorldGen);
            
            GameManager.Instance.World = world;
            
            GameManager.Instance.WormWorld = new WormWorld(
                session.Seed,
                session.Radius,
                gameBootstrap.WorldGen);

            GameManager.Instance.MagneticAnomalyWorld = new MagneticAnomalyWorld(
                session.Seed,
                session.Radius,
                gameBootstrap.WorldGen);

            TryIssueStartingMission(gameBootstrap, session);

            var cam = Camera.main;

            if (surfaceRoot != null)
            {
                var baseTransform = surfaceRoot.Find("Yoke");

                surfaceRoot.position = Vector3.zero;
        
                if (baseTransform != null)
                {
                    baseTransform.localPosition = Vector3.zero;
                }
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

            if (depthWorld != null && cam != null)
            {
                depthWorld.SyncVisibleArea(world, gameBootstrap.Assets, cam);
            }
        }

        void TryIssueStartingMission(GameBootstrap bootstrap, WorldSession session)
        {
            if (missionCatalog == null || session?.ActiveMission != null)
            {
                return;
            }

            var worldGen = bootstrap.WorldGen;
            var limits = bootstrap.DroneLimits;
            var sectorSize = worldGen != null ? worldGen.ResourceClusterSpacing : 0;
            if (worldGen == null || limits == null || sectorSize <= 0)
            {
                return;
            }

            MissionIssuer.TryIssueNext(
                session,
                missionCatalog,
                worldGen,
                limits,
                sectorSize,
                out _);
        }
    }
}
