using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Drone.Unity;
using Operator.Missions.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Operator.Missions.Unity
{
    /// <summary>
    /// Слева — текущий сектор. Цель в кадре — пин на клетке; за кадром — стрелка от дрона к цели.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class NavigationHud : MonoBehaviour
    {
        [SerializeField] Transform drone;
        [SerializeField] Canvas canvas;
        [SerializeField] GameObject markerPrefab;
        [SerializeField] bool rotateToTarget;
        [SerializeField] float guideMarginHorizontal = 80f;
        [SerializeField] float guideMarginVertical = 130f;
        [SerializeField] float iconRotationOffset = -90f;
        [SerializeField] float sectorPanelInset = 28f;
        [SerializeField] float screenMarginPx = 8f;

        [Header("Optional — если пусто, создаётся в Awake")]
        [SerializeField] GameObject currentSectorRoot;
        [SerializeField] TextMeshProUGUI currentSectorText;

        Transform _marker;

        Vector2Int _targetCell;
        bool _hasTarget;

        void Awake()
        {
            if (drone == null)
            {
                var controller = FindFirstObjectByType<DroneController>();
                if (controller != null)
                {
                    drone = controller.transform;
                }
            }

            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>();
            }

            if (canvas != null && currentSectorRoot == null)
            {
                BuildSectorPanel(canvas.transform);
            }

            SetMarkerVisible(false);
            SetSectorVisible(false);
        }

        void LateUpdate()
        {
            RefreshCurrentSector();
            RefreshTargetMarker();
        }

        public void ShowTarget(Vector2Int targetCell)
        {
            _targetCell = targetCell;
            _hasTarget = true;
        }

        public void HideTarget()
        {
            _hasTarget = false;
            SetMarkerVisible(false);
        }

        void RefreshCurrentSector()
        {
            if (currentSectorRoot == null || currentSectorText == null)
            {
                return;
            }

            if (drone == null || IsAtBase())
            {
                SetSectorVisible(false);
                return;
            }

            var sectorSize = ResolveSectorSize();
            if (sectorSize <= 0)
            {
                SetSectorVisible(false);
                return;
            }

            var sector = SectorAddress.FromCell(WorldGrid.WorldToCell(drone.position), sectorSize);
            currentSectorText.text = sector.ToDisplayString();
            SetSectorVisible(true);
        }

        void RefreshTargetMarker()
        {
            EnsureMarker();

            if (!_hasTarget || drone == null || _marker == null || IsAtBase())
            {
                SetMarkerVisible(false);
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                SetMarkerVisible(false);
                return;
            }

            var droneCell = WorldGrid.WorldToCell(drone.position);
            var worldRadius = GameBootstrap.Instance?.Session?.Radius ?? 0;
            var viewRect = cam.pixelRect;
            var pinWorld = WorldGrid.DroneCellToWorld(_targetCell);
            var pinScreen3 = cam.WorldToScreenPoint(pinWorld);

            if (pinScreen3.z <= 0f)
            {
                SetMarkerVisible(false);
                return;
            }

            SetMarkerVisible(true);

            if (IsVisibleOnScreen(pinScreen3, viewRect, screenMarginPx))
            {
                _marker.position = WithZeroZ(pinWorld);
                if (rotateToTarget)
                {
                    _marker.localRotation = Quaternion.identity;
                }

                return;
            }

            var guideWorld = NeedsWrapGuide(droneCell.x, _targetCell.x, worldRadius)
                ? VirtualTargetWorld(drone.position, droneCell, _targetCell, worldRadius)
                : pinWorld;
            var guideScreen3 = cam.WorldToScreenPoint(guideWorld);
            if (guideScreen3.z <= 0f)
            {
                SetMarkerVisible(false);
                return;
            }

            var droneScreen3 = cam.WorldToScreenPoint(drone.position);
            var originScreen = new Vector2(droneScreen3.x, droneScreen3.y);
            var guideScreen = new Vector2(guideScreen3.x, guideScreen3.y);
            var guideRect = GetGuideRect(viewRect);
            var markerScreen = GetScreenEdgePoint(originScreen, guideScreen - originScreen, 0f, guideRect);
            _marker.position = ScreenToWorld(cam, markerScreen);

            if (rotateToTarget)
            {
                var dir = markerScreen - originScreen;
                var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + iconRotationOffset;
                _marker.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        void EnsureMarker()
        {
            if (_marker != null || markerPrefab == null)
            {
                return;
            }

            _marker = Instantiate(markerPrefab).transform;
            _marker.gameObject.SetActive(false);
        }

        static Vector3 ScreenToWorld(Camera cam, Vector2 screenPoint)
        {
            var z = -cam.transform.position.z;
            var world = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, z));
            world.z = 0f;
            return world;
        }

        static Vector3 WithZeroZ(Vector3 p)
        {
            p.z = 0f;
            return p;
        }

        static bool IsVisibleOnScreen(Vector3 screenPoint, Rect viewRect, float margin)
        {
            return screenPoint.x >= viewRect.xMin + margin
                && screenPoint.x <= viewRect.xMax - margin
                && screenPoint.y >= viewRect.yMin + margin
                && screenPoint.y <= viewRect.yMax - margin;
        }

        Rect GetGuideRect(Rect viewRect)
        {
            return Rect.MinMaxRect(
                viewRect.xMin + guideMarginHorizontal,
                viewRect.yMin + guideMarginVertical,
                viewRect.xMax - guideMarginHorizontal,
                viewRect.yMax - guideMarginVertical);
        }

        static Vector2 GetScreenEdgePoint(Vector2 origin, Vector2 direction, float inset, Rect bounds)
        {
            direction.Normalize();

            var minX = bounds.xMin + inset;
            var minY = bounds.yMin + inset;
            var maxX = bounds.xMax - inset;
            var maxY = bounds.yMax - inset;

            var t = float.PositiveInfinity;

            if (direction.x > 0.0001f)
            {
                t = Mathf.Min(t, (maxX - origin.x) / direction.x);
            }
            else if (direction.x < -0.0001f)
            {
                t = Mathf.Min(t, (minX - origin.x) / direction.x);
            }

            if (direction.y > 0.0001f)
            {
                t = Mathf.Min(t, (maxY - origin.y) / direction.y);
            }
            else if (direction.y < -0.0001f)
            {
                t = Mathf.Min(t, (minY - origin.y) / direction.y);
            }

            if (float.IsInfinity(t) || t <= 0f)
            {
                return origin;
            }

            return origin + direction * t;
        }

        void BuildSectorPanel(Transform canvasRoot)
        {
            currentSectorRoot = CreatePanel(
                canvasRoot,
                "CurrentSectorPanel",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(140f, 56f),
                new Vector2(sectorPanelInset, 0f));

            var bg = currentSectorRoot.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);
            bg.raycastTarget = false;

            currentSectorText = CreateLabel(currentSectorRoot.transform, "CurrentSectorText", 32, TextAlignmentOptions.MidlineLeft);
            var textRect = currentSectorText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);
        }

        static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return go;
        }

        static TextMeshProUGUI CreateLabel(Transform parent, string name, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = alignment;
            label.color = Color.white;
            label.outlineWidth = 0.2f;
            label.outlineColor = Color.black;
            label.raycastTarget = false;
            return label;
        }

        void SetMarkerVisible(bool visible)
        {
            if (_marker != null)
            {
                _marker.gameObject.SetActive(visible);
            }
        }

        void SetSectorVisible(bool visible)
        {
            if (currentSectorRoot != null)
            {
                currentSectorRoot.SetActive(visible);
            }
        }

        bool IsAtBase()
        {
            if (drone == null)
            {
                return true;
            }

            var cell = WorldGrid.WorldToCell(drone.position);
            return cell.x == GarageBounds.ShaftX && cell.y <= GarageBounds.SecondCell.y;
        }

        static int ResolveSectorSize()
        {
            var bootstrap = GameBootstrap.Instance;
            return bootstrap != null ? bootstrap.WorldGen.ResourceClusterSpacing : 0;
        }

        static bool NeedsWrapGuide(int fromX, int toX, int worldRadius)
        {
            if (worldRadius <= 0)
            {
                return false;
            }

            var raw = Mathf.Abs(toX - fromX);
            var wrapped = WrappedAbsDeltaX(fromX, toX, worldRadius);
            return wrapped < raw;
        }

        static int WrappedAbsDeltaX(int fromX, int toX, int worldRadius)
        {
            var dx = Mathf.Abs(toX - fromX);
            if (worldRadius <= 0)
            {
                return dx;
            }

            var width = worldRadius * 2 + 1;
            return Mathf.Min(dx, width - dx);
        }

        static int SignedWrappedDeltaX(int fromX, int toX, int worldRadius)
        {
            var raw = toX - fromX;
            if (worldRadius <= 0)
            {
                return raw;
            }

            var width = worldRadius * 2 + 1;
            if (raw > width / 2)
            {
                raw -= width;
            }
            else if (raw < -width / 2)
            {
                raw += width;
            }

            return raw;
        }

        static Vector3 VirtualTargetWorld(
            Vector3 droneWorld,
            Vector2Int droneCell,
            Vector2Int targetCell,
            int worldRadius)
        {
            var signedDx = SignedWrappedDeltaX(droneCell.x, targetCell.x, worldRadius);
            var signedDy = targetCell.y - droneCell.y;
            return new Vector3(
                droneWorld.x + signedDx * WorldGrid.CellSize,
                droneWorld.y - signedDy * WorldGrid.CellSize,
                droneWorld.z);
        }
    }
}
