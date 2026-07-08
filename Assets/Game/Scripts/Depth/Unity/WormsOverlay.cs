using System.Collections.Generic;
using Operator.Audio.Unity;
using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Drone.Unity;
using Operator.Managers;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Operator.Depth.Unity
{
    /// <summary>
    /// Черви: LineRenderer между RockBack и RockFront, полупрозрачная змейка по клеткам.
    /// </summary>
    [DefaultExecutionOrder(205)]
    public class WormsOverlay : MonoBehaviour
    {
        [Tooltip("Обычно RockFront — линия поверх породы (полупрозрачная).")]
        [SerializeField] TilemapRenderer sortingReference;
        [SerializeField] Transform wormsRoot;
        [SerializeField] GameObject prefabSynth;
        [SerializeField] GameObject prefabRellit;
        [SerializeField] GameObject prefabLumin;
        [SerializeField] GameObject prefabDeVault;
        [SerializeField] Transform droneTarget;
        [SerializeField] int visiblePadding = 2;
        [SerializeField] int sortingOrderOffset = 2;
        [SerializeField] Color chargeBlinkColorA = new(0.58f, 0.92f, 0.08f, 1f);
        [SerializeField] Color chargeBlinkColorB = new(0.81f, 1f, 0.16f, 1f);
        [SerializeField] float chargeBlinkSpeed = 10f;

        const string HeadName = "Head";

        readonly Dictionary<long, LineRenderer> _active = new();
        readonly Dictionary<long, ResourceType> _activeKinds = new();
        readonly Dictionary<long, Gradient> _baseGradients = new();
        readonly Dictionary<long, Gradient> _baseHeadGradients = new();
        readonly Dictionary<ResourceType, Stack<LineRenderer>> _pool = new();
        Vector3[] _positionBuffer = System.Array.Empty<Vector3>();

        int _sortingLayerId;
        int _sortingOrder;

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
                Debug.LogError($"{nameof(WormsOverlay)}: назначь Tilemap Renderer (RockBack) для sorting.", this);
                enabled = false;
                return;
            }

            if (wormsRoot == null)
            {
                var root = new GameObject("WormsRoot").transform;
                root.SetParent(transform, false);
                wormsRoot = root;
            }

            if (!HasPrefabs())
            {
                Debug.LogError(
                    $"{nameof(WormsOverlay)}: назначь 4 prefab-а (Synth, Rellit, Lumin, DeVault) с LineRenderer.",
                    this);
                enabled = false;
                return;
            }

            _sortingLayerId = sortingReference.sortingLayerID;
            _sortingOrder = sortingReference.sortingOrder + sortingOrderOffset;
        }

        bool HasPrefabs() =>
            HasWormPrefab(prefabSynth)
            && HasWormPrefab(prefabRellit)
            && HasWormPrefab(prefabLumin)
            && HasWormPrefab(prefabDeVault);

        static bool HasWormPrefab(GameObject prefab) =>
            prefab != null && prefab.GetComponent<LineRenderer>() != null;

        void LateUpdate()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null || Camera.main == null)
            {
                ClearAll();
                return;
            }

            var world = gameManager.World;
            if (world == null)
            {
                ClearAll();
                return;
            }

            EnsureWormWorld(gameManager, world);
            var wormWorld = gameManager.WormWorld;
            var cam = Camera.main;

            if (wormWorld == null)
            {
                ClearAll();
                return;
            }

            var rect = ComputeVisibleRect(cam, world, visiblePadding);
            var hasDroneTarget = TryGetDroneCell(world, out var droneCell);

            var audioRadius = GameBootstrap.Instance?.WorldGen?.WormAudioRadius ?? 50;
            if (hasDroneTarget)
            {
                wormWorld.EnsureNearCell(droneCell, audioRadius);
            }

            wormWorld.EnsureInRect(rect.XMin, rect.XMax, rect.YMin, rect.YMax);

            wormWorld.Tick(
                Time.deltaTime,
                rect.XMin,
                rect.XMax,
                rect.YMin,
                rect.YMax,
                hasDroneTarget,
                droneCell);
            SyncVisible(wormWorld, rect);
        }

        void SyncVisible(WormWorld wormWorld, VisibleRect rect)
        {
            var keep = new HashSet<long>();

            foreach (var worm in wormWorld.Worms)
            {
                if (!wormWorld.ShouldRender(worm, rect.XMin, rect.XMax, rect.YMin, rect.YMax))
                {
                    continue;
                }

                keep.Add(worm.Id);
                ApplyLine(GetOrCreateLine(worm), worm);
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
                ReleaseLine(toRelease[i]);
            }
        }

        void ApplyLine(LineRenderer line, Worm worm)
        {
            var count = worm.Segments.Count;
            if (count == 0)
            {
                line.positionCount = 0;
                return;
            }

            if (_positionBuffer.Length < count + 1)
            {
                _positionBuffer = new Vector3[count + 1];
            }

            var lineCount = BuildLinePositions(worm, _positionBuffer, out var headPosition, out var headDirection);

            line.positionCount = lineCount;
            line.SetPositions(_positionBuffer);
            ApplyHead(line, headPosition, headDirection);
            ApplyStateVisual(line, worm);
        }

        static int BuildLinePositions(
            Worm worm,
            Vector3[] output,
            out Vector3 headPosition,
            out Vector2Int headDirection)
        {
            var count = worm.Segments.Count;
            headDirection = count >= 2 ? worm.Segments[0] - worm.Segments[1] : Vector2Int.right;

            if (worm.MoveProgress >= 1f
                || count <= 1
                || !IsShiftStep(worm.PreviousSegments, worm.Segments))
            {
                for (var i = 0; i < count; i++)
                {
                    output[i] = WorldGrid.DroneCellToWorld(worm.Segments[i]);
                }

                headPosition = output[0];
                return count;
            }

            var t = Mathf.Clamp01(worm.MoveProgress);
            var previousHead = WorldGrid.DroneCellToWorld(worm.PreviousSegments[0]);
            var currentHead = WorldGrid.DroneCellToWorld(worm.Segments[0]);
            output[0] = Vector3.Lerp(previousHead, currentHead, t);
            var outputCount = 1;

            for (var i = 0; i < count - 1; i++)
            {
                output[outputCount++] = WorldGrid.DroneCellToWorld(worm.PreviousSegments[i]);
            }

            var previousTail = WorldGrid.DroneCellToWorld(worm.PreviousSegments[count - 1]);
            var nextTail = WorldGrid.DroneCellToWorld(worm.PreviousSegments[count - 2]);
            output[outputCount++] = Vector3.Lerp(previousTail, nextTail, t);

            headPosition = output[0];
            headDirection = worm.Segments[0] - worm.PreviousSegments[0];
            return outputCount;
        }

        static bool IsShiftStep(IReadOnlyList<Vector2Int> previous, IReadOnlyList<Vector2Int> current)
        {
            if (previous.Count != current.Count || previous.Count <= 1)
            {
                return false;
            }

            var headDelta = current[0] - previous[0];
            if (Mathf.Abs(headDelta.x) + Mathf.Abs(headDelta.y) != 1)
            {
                return false;
            }

            for (var i = 1; i < current.Count; i++)
            {
                if (current[i] != previous[i - 1])
                {
                    return false;
                }
            }

            return true;
        }

        static void ApplyHead(LineRenderer line, Vector3 headPosition, Vector2Int headDirection)
        {
            var head = line.transform.Find(HeadName);
            if (head == null)
            {
                return;
            }

            head.gameObject.SetActive(true);
            head.position = headPosition;
            head.rotation = Quaternion.Euler(0f, 0f, HeadAngle(headDirection) + 90f);
        }

        void ApplyStateVisual(LineRenderer line, Worm worm)
        {
            var head = line.transform.Find(HeadName)?.GetComponent<LineRenderer>();
            if (!_baseGradients.ContainsKey(worm.Id))
            {
                _baseGradients[worm.Id] = line.colorGradient;
            }

            if (head != null && !_baseHeadGradients.ContainsKey(worm.Id))
            {
                _baseHeadGradients[worm.Id] = head.colorGradient;
            }

            if (worm.State != WormState.Attack)
            {
                line.colorGradient = _baseGradients[worm.Id];
                if (head != null)
                {
                    head.colorGradient = _baseHeadGradients[worm.Id];
                }

                return;
            }

            var blink = (Mathf.Sin(Time.time * chargeBlinkSpeed) + 1f) * 0.5f;
            var color = Color.Lerp(chargeBlinkColorA, chargeBlinkColorB, blink);
            var gradient = SolidGradient(color);
            line.colorGradient = gradient;
            if (head != null)
            {
                head.colorGradient = gradient;
            }
        }

        static Gradient SolidGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) });
            return gradient;
        }

        static float HeadAngle(Vector2Int direction)
        {
            if (direction.x > 0)
            {
                return 0f;
            }

            if (direction.x < 0)
            {
                return 180f;
            }

            return direction.y > 0 ? -90f : 90f;
        }

        bool TryGetDroneCell(World world, out Vector2Int cell)
        {
            cell = default;

            if (droneTarget == null)
            {
                var drone = FindFirstObjectByType<DroneController>();
                if (drone != null)
                {
                    droneTarget = drone.transform;
                }
            }

            if (droneTarget == null)
            {
                return false;
            }

            cell = WorldGrid.WorldToCell(droneTarget.position);
            cell.x = world.WrapX(cell.x);
            return cell.y >= 0 && cell.y <= world.WorldRadius;
        }

        LineRenderer GetOrCreateLine(Worm worm)
        {
            if (_active.TryGetValue(worm.Id, out var existing))
            {
                if (_activeKinds.TryGetValue(worm.Id, out var existingKind) && existingKind == worm.Kind)
                {
                    return existing;
                }

                ReleaseLine(worm.Id);
            }

            var line = RentLine(worm.Kind);
            ConfigureLine(line);
            line.gameObject.SetActive(true);
            _active[worm.Id] = line;
            _activeKinds[worm.Id] = worm.Kind;
            return line;
        }

        LineRenderer RentLine(ResourceType kind)
        {
            if (!_pool.TryGetValue(kind, out var pool))
            {
                pool = new Stack<LineRenderer>();
                _pool[kind] = pool;
            }

            if (pool.Count > 0)
            {
                return pool.Pop();
            }

            return CreateLineRenderer(kind);
        }

        GameObject GetPrefab(ResourceType kind)
        {
            return kind switch
            {
                ResourceType.Synth => prefabSynth,
                ResourceType.Rellit => prefabRellit,
                ResourceType.Lumin => prefabLumin,
                ResourceType.DeVault => prefabDeVault,
                _ => null
            };
        }

        void ConfigureLine(LineRenderer line)
        {
            line.alignment = LineAlignment.TransformZ;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.sortingLayerID = _sortingLayerId;
            line.sortingOrder = _sortingOrder;

            var head = line.transform.Find(HeadName)?.GetComponent<LineRenderer>();
            if (head != null)
            {
                head.alignment = LineAlignment.TransformZ;
                head.numCornerVertices = 0;
                head.numCapVertices = 0;
                head.sortingLayerID = _sortingLayerId;
                head.sortingOrder = _sortingOrder + 1;
            }
        }

        LineRenderer CreateLineRenderer(ResourceType kind)
        {
            var prefab = GetPrefab(kind);
            if (prefab == null)
            {
                Debug.LogError($"{nameof(WormsOverlay)}: нет prefab для {kind}.", this);
                prefab = prefabSynth;
            }

            var instance = Instantiate(prefab, wormsRoot);
            instance.name = prefab.name;

            var ambient = instance.GetComponent<WormAmbientAudio>();
            if (ambient != null)
            {
                ambient.enabled = false;
            }

            var line = instance.GetComponent<LineRenderer>();
            ConfigureLine(line);
            return line;
        }

        static void EnsureWormWorld(GameManager gameManager, World world)
        {
            if (gameManager.WormWorld != null)
            {
                return;
            }

            var bootstrap = GameBootstrap.Instance;
            var config = bootstrap?.WorldGen;
            var session = gameManager.Session;
            if (config == null || session == null)
            {
                return;
            }

            gameManager.WormWorld = new WormWorld(session.Seed, world.WorldRadius, config);
        }

        void ReleaseLine(long wormId)
        {
            if (!_active.TryGetValue(wormId, out var line))
            {
                return;
            }

            _activeKinds.TryGetValue(wormId, out var kind);

            _active.Remove(wormId);
            _activeKinds.Remove(wormId);
            _baseGradients.Remove(wormId);
            _baseHeadGradients.Remove(wormId);
            line.gameObject.SetActive(false);
            ReturnToPool(kind, line);
        }

        void ReturnToPool(ResourceType kind, LineRenderer line)
        {
            if (!ResourceTypeIds.HasResource(kind))
            {
                Destroy(line.gameObject);
                return;
            }

            if (!_pool.TryGetValue(kind, out var pool))
            {
                pool = new Stack<LineRenderer>();
                _pool[kind] = pool;
            }

            pool.Push(line);
        }

        void ClearAll()
        {
            foreach (var pair in _active)
            {
                pair.Value.gameObject.SetActive(false);
                ReturnToPool(
                    _activeKinds.TryGetValue(pair.Key, out var kind) ? kind : ResourceType.Synth,
                    pair.Value);
            }

            _active.Clear();
            _activeKinds.Clear();
            _baseGradients.Clear();
            _baseHeadGradients.Clear();
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

        struct VisibleRect
        {
            public int XMin;
            public int XMax;
            public int YMin;
            public int YMax;
        }
    }
}
