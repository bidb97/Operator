using Operator.Bootstrap;

using Operator.Depth.Core;

using Operator.Managers;

using UnityEngine;

using UnityEngine.UI;



namespace Operator.Depth.Unity

{

    /// <summary>

    /// Помехи на экране: шум на весь экран, сила по близости к аномалии.

    /// </summary>

    [DefaultExecutionOrder(210)]

    public class MagneticAnomalyScreenGlitch : MonoBehaviour

    {

        [SerializeField] Transform listener;

        [SerializeField] Texture2D noiseTexture;

        [SerializeField] float hearRadiusMultiplier = 2.5f;

        [SerializeField] float maxAlpha = 0.4f;

        [SerializeField] float fadeSmoothSpeed = 3f;

        [SerializeField] float scrollSpeed = 0.35f;

        [SerializeField] float noisePixelsPerTile = 96f;

        [SerializeField] int canvasSortOrder = -10;



        Canvas _canvas;

        RectTransform _panel;

        RawImage _image;

        float _alpha;

        Vector2 _uvScroll;



        void Awake()

        {

            var canvasObject = new GameObject("MagneticGlitchCanvas");

            _canvas = canvasObject.AddComponent<Canvas>();

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            _canvas.sortingOrder = canvasSortOrder;



            var scaler = canvasObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution = new Vector2(1080, 1920);

            scaler.matchWidthOrHeight = 0.5f;



            var panelObject = new GameObject("GlitchPanel", typeof(RectTransform));

            panelObject.transform.SetParent(canvasObject.transform, false);

            _panel = panelObject.GetComponent<RectTransform>();

            _panel.anchorMin = Vector2.zero;

            _panel.anchorMax = Vector2.one;

            _panel.offsetMin = Vector2.zero;

            _panel.offsetMax = Vector2.zero;



            _image = panelObject.AddComponent<RawImage>();

            _image.raycastTarget = false;

            _image.texture = noiseTexture != null ? noiseTexture : Texture2D.whiteTexture;



            _canvas.enabled = false;

        }



        void LateUpdate()

        {

            UpdateGlitch();

        }



        void UpdateGlitch()

        {

            var targetAlpha = 0f;



            var gameManager = GameManager.Instance;

            if (gameManager != null)

            {

                EnsureMagneticWorld(gameManager);



                var magneticWorld = gameManager.MagneticAnomalyWorld;

                var world = gameManager.World;

                if (magneticWorld != null && world != null && listener != null)

                {

                    var listenerPos = listener.position;

                    var cell = WorldGrid.WorldToCell(listenerPos);

                    cell.x = world.WrapX(cell.x);

                    const int padding = 220;

                    magneticWorld.EnsureInRect(

                        cell.x - padding,

                        cell.x + padding,

                        cell.y - padding,

                        cell.y + padding);



                    var bestProximity = 0f;



                    foreach (var anomaly in magneticWorld.Anomalies)

                    {

                        var hearRadius = Mathf.Max(anomaly.RadiusX, anomaly.RadiusY) * hearRadiusMultiplier;

                        hearRadius = Mathf.Max(5f, hearRadius);

                        var dist = WrappedDistance(listenerPos, anomaly.CenterWorld, world.WorldRadius);

                        if (dist > hearRadius)

                        {

                            continue;

                        }



                        var proximity = 1f - Mathf.Clamp01(dist / hearRadius);

                        if (proximity > bestProximity)

                        {

                            bestProximity = proximity;

                        }

                    }



                    if (bestProximity > 0f)

                    {

                        var t = Mathf.SmoothStep(0f, 1f, bestProximity);

                        targetAlpha = maxAlpha * t;

                    }

                }

            }



            _alpha = Mathf.MoveTowards(_alpha, targetAlpha, fadeSmoothSpeed * Time.deltaTime);



            var active = _alpha > 0.001f;

            _canvas.enabled = active;

            if (!active)

            {

                return;

            }



            Canvas.ForceUpdateCanvases();

            AnimateNoise();

            _image.color = new Color(1f, 1f, 1f, _alpha);

        }



        void AnimateNoise()

        {

            _uvScroll += new Vector2(scrollSpeed, scrollSpeed * 0.61f) * Time.deltaTime;

            _uvScroll.x = Mathf.Repeat(_uvScroll.x, 1f);

            _uvScroll.y = Mathf.Repeat(_uvScroll.y, 1f);



            var panelSize = _panel.rect.size;

            var pixelsPerTile = Mathf.Max(16f, noisePixelsPerTile);

            var tileX = Mathf.Max(1f, panelSize.x / pixelsPerTile);

            var tileY = Mathf.Max(1f, panelSize.y / pixelsPerTile);

            _image.uvRect = new Rect(_uvScroll, new Vector2(tileX, tileY));

        }



        static float WrappedDistance(Vector3 from, Vector3 to, int worldRadius)

        {

            var dx = Mathf.Abs(to.x - from.x);

            if (worldRadius > 0)

            {

                var width = worldRadius * 2 + 1;

                dx = Mathf.Min(dx, width - dx);

            }



            var dy = to.y - from.y;

            return Mathf.Sqrt(dx * dx + dy * dy);

        }



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



        void OnDestroy()

        {

            if (_canvas != null)

            {

                Destroy(_canvas.gameObject);

            }

        }

    }

}


