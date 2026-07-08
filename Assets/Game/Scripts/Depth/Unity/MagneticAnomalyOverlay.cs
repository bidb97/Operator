using System.Collections.Generic;
using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Drone.Unity;
using Operator.Managers;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Operator.Depth.Unity
{
    [DefaultExecutionOrder(206)]
    public class MagneticAnomalyOverlay : MonoBehaviour
    {
        static readonly int AnchorCellId = Shader.PropertyToID("_AnchorCell");
        static readonly int RadiusXyId = Shader.PropertyToID("_RadiusXY");
        static readonly int ShapeSeedId = Shader.PropertyToID("_ShapeSeed");
        static readonly int FlowDirId = Shader.PropertyToID("_FlowDir");

        [SerializeField] TilemapRenderer sortingReference;
        [SerializeField] Transform overlayRoot;
        [SerializeField] Shader overlayShader;
        [SerializeField] GameObject audioPrefab;
        [SerializeField] float audioHearRadiusMultiplier = 2.5f;
        [SerializeField] int visiblePadding = 2;
        [SerializeField] int sortingOrder = 20;

        readonly Dictionary<long, QuadVisual> _active = new();
        readonly Stack<QuadVisual> _pool = new();
        int _sortingLayerId;
        int _sortingOrder;

        sealed class QuadVisual
        {
            public Transform Root;
            public MeshRenderer Renderer;
            public Material Material;
            public AudioSource AudioSource;
        }

        Transform _occupant;

        void Awake()
        {
            if (sortingReference == null)
            {
                sortingReference = transform.Find("Grid/RockFront")?.GetComponent<TilemapRenderer>()
                    ?? transform.Find("Grid/RockBack")?.GetComponent<TilemapRenderer>()
                    ?? GetComponentInChildren<TilemapRenderer>();
            }

            if (sortingReference == null)
            {
                Debug.LogError($"{nameof(MagneticAnomalyOverlay)}: назначь TilemapRenderer для sorting.", this);
                enabled = false;
                return;
            }

            if (overlayShader == null)
            {
                overlayShader = Shader.Find("Operator/MagneticAnomalyOverlay");
            }

            if (overlayShader == null)
            {
                Debug.LogError($"{nameof(MagneticAnomalyOverlay)}: назначь shader MagneticAnomalyOverlay.", this);
                enabled = false;
                return;
            }

            if (overlayRoot == null)
            {
                var root = new GameObject("MagneticAnomalyRoot").transform;
                root.SetParent(transform, false);
                overlayRoot = root;
            }

            _sortingLayerId = sortingReference.sortingLayerID;
            _sortingOrder = sortingOrder;

            var drone = FindFirstObjectByType<DroneController>();
            if (drone != null)
            {
                _occupant = drone.transform;
            }
        }

        void LateUpdate()
        {
            var gameManager = GameManager.Instance;
            var cam = Camera.main;
            if (gameManager?.World == null || cam == null || overlayShader == null)
            {
                ClearAll();
                return;
            }

            EnsureMagneticWorld(gameManager);
            var magneticWorld = gameManager.MagneticAnomalyWorld;
            if (magneticWorld == null)
            {
                ClearAll();
                return;
            }

            var world = gameManager.World;
            var rect = ComputeVisibleRect(cam, world, visiblePadding);
            magneticWorld.EnsureInRect(rect.XMin, rect.XMax, rect.YMin, rect.YMax);
            magneticWorld.Tick(Time.deltaTime, _occupant != null ? _occupant.position : null);
            SyncVisible(magneticWorld, rect);
        }

        void SyncVisible(MagneticAnomalyWorld magneticWorld, VisibleRect rect)
        {
            var keep = new HashSet<long>();

            foreach (var anomaly in magneticWorld.Anomalies)
            {
                if (!ShouldRender(anomaly, rect))
                {
                    continue;
                }

                keep.Add(anomaly.Id);
                ApplyVisual(GetOrCreateQuad(anomaly.Id), anomaly);
            }

            var toRelease = new List<long>();
            foreach (var pair in _active)
            {
                if (!keep.Contains(pair.Key))
                {
                    toRelease.Add(pair.Key);
                }
            }

            for (var i = 0; i < toRelease.Count; i++)
            {
                ReleaseQuad(toRelease[i]);
            }
        }

        static bool ShouldRender(MagneticAnomaly anomaly, VisibleRect rect)
        {
            anomaly.GetBounds(out var xMin, out var xMax, out var yMin, out var yMax);
            return xMax >= rect.XMin
                && xMin <= rect.XMax
                && yMax >= rect.YMin
                && yMin <= rect.YMax;
        }

        void ApplyVisual(QuadVisual quad, MagneticAnomaly anomaly)
        {
            quad.Root.gameObject.SetActive(true);
            quad.Root.position = new Vector3(anomaly.Center.x, -anomaly.Center.y, -0.5f);
            quad.Root.localScale = new Vector3(
                Mathf.Max(1f, anomaly.RadiusX * 2.1f),
                Mathf.Max(1f, anomaly.RadiusY * 2.1f),
                1f);

            quad.Material.SetVector(AnchorCellId, new Vector4(anomaly.Center.x, anomaly.Center.y, 0f, 0f));
            quad.Material.SetVector(RadiusXyId, new Vector4(anomaly.RadiusX, anomaly.RadiusY, 0f, 0f));
            quad.Material.SetFloat(ShapeSeedId, anomaly.ShapeSeed);
            quad.Material.SetVector(FlowDirId, ToFlowDirCell(anomaly.Direction));
            ApplyAudioRange(quad, anomaly);
        }

        void ApplyAudioRange(QuadVisual quad, MagneticAnomaly anomaly)
        {
            if (quad.AudioSource == null)
            {
                return;
            }

            var hearRadius = Mathf.Max(anomaly.RadiusX, anomaly.RadiusY) * audioHearRadiusMultiplier;
            hearRadius = Mathf.Max(5f, hearRadius);
            quad.AudioSource.maxDistance = hearRadius;
            quad.AudioSource.minDistance = Mathf.Max(1f, hearRadius * 0.15f);
        }

        QuadVisual GetOrCreateQuad(long anomalyId)
        {
            if (_active.TryGetValue(anomalyId, out var existing))
            {
                return existing;
            }

            var quad = _pool.Count > 0 ? _pool.Pop() : CreateQuad();
            _active[anomalyId] = quad;
            return quad;
        }

        QuadVisual CreateQuad()
        {
            var quadObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadObject.name = "MagneticAnomalyQuad";
            Destroy(quadObject.GetComponent<Collider>());
            quadObject.transform.SetParent(overlayRoot, false);

            var renderer = quadObject.GetComponent<MeshRenderer>();
            var material = new Material(overlayShader);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingLayerID = _sortingLayerId;
            renderer.sortingOrder = _sortingOrder;

            AudioSource audioSource = null;
            if (audioPrefab != null)
            {
                var audioObject = Instantiate(audioPrefab, quadObject.transform);
                audioObject.transform.localPosition = Vector3.zero;
                audioObject.transform.localRotation = Quaternion.identity;
                audioObject.transform.localScale = Vector3.one;
                audioSource = audioObject.GetComponent<AudioSource>();
            }

            quadObject.SetActive(false);

            return new QuadVisual
            {
                Root = quadObject.transform,
                Renderer = renderer,
                Material = material,
                AudioSource = audioSource
            };
        }

        void ReleaseQuad(long anomalyId)
        {
            if (!_active.TryGetValue(anomalyId, out var quad))
            {
                return;
            }

            _active.Remove(anomalyId);
            quad.Root.gameObject.SetActive(false);
            _pool.Push(quad);
        }

        void ClearAll()
        {
            foreach (var pair in _active)
            {
                pair.Value.Root.gameObject.SetActive(false);
                _pool.Push(pair.Value);
            }

            _active.Clear();
        }

        static Vector4 ToFlowDirCell(MagneticDirection direction) =>
            direction switch
            {
                MagneticDirection.Down => new Vector4(0f, 1f, 0f, 0f),
                MagneticDirection.Up => new Vector4(0f, -1f, 0f, 0f),
                MagneticDirection.Right => new Vector4(1f, 0f, 0f, 0f),
                MagneticDirection.Left => new Vector4(-1f, 0f, 0f, 0f),
                _ => new Vector4(0f, 1f, 0f, 0f)
            };

        static void EnsureMagneticWorld(GameManager gameManager)
        {
            if (gameManager.MagneticAnomalyWorld != null)
            {
                return;
            }

            var bootstrap = GameBootstrap.Instance;
            var session = gameManager.Session;
            var world = gameManager.World;
            if (bootstrap?.WorldGen == null || session == null || world == null)
            {
                return;
            }

            gameManager.MagneticAnomalyWorld = new MagneticAnomalyWorld(
                session.Seed,
                world.WorldRadius,
                bootstrap.WorldGen);
        }

        static VisibleRect ComputeVisibleRect(Camera cam, World world, int padding)
        {
            var center = cam.transform.position;
            var halfHeight = cam.orthographicSize;
            var halfWidth = halfHeight * cam.aspect;

            var bottomLeft = WorldGrid.WorldToCell(new Vector3(center.x - halfWidth, center.y - halfHeight, 0f));
            var bottomRight = WorldGrid.WorldToCell(new Vector3(center.x + halfWidth, center.y - halfHeight, 0f));
            var topLeft = WorldGrid.WorldToCell(new Vector3(center.x - halfWidth, center.y + halfHeight, 0f));
            var topRight = WorldGrid.WorldToCell(new Vector3(center.x + halfWidth, center.y + halfHeight, 0f));

            var xMin = Mathf.Min(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x) - padding;
            var xMax = Mathf.Max(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x) + padding;
            var yMin = Mathf.Min(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y) - padding;
            var yMax = Mathf.Max(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y) + padding;

            yMin = Mathf.Max(0, yMin);
            yMax = Mathf.Min(world.WorldRadius, yMax);
            xMin = Mathf.Max(-world.WorldRadius, xMin);
            xMax = Mathf.Min(world.WorldRadius, xMax);

            return new VisibleRect
            {
                XMin = xMin,
                XMax = xMax,
                YMin = yMin,
                YMax = yMax
            };
        }

        void OnDestroy()
        {
            foreach (var quad in _active.Values)
            {
                DestroyQuadMaterial(quad);
            }

            while (_pool.Count > 0)
            {
                DestroyQuadMaterial(_pool.Pop());
            }
        }

        static void DestroyQuadMaterial(QuadVisual quad)
        {
            if (quad?.Material != null)
            {
                Destroy(quad.Material);
            }
        }

        struct VisibleRect
        {
            public int XMin;
            public int XMax;
            public int YMin;
            public int YMax;
        }
    }
}
