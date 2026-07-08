using System.Collections.Generic;
using Operator.Parallax.Core;
using UnityEngine;

namespace Operator.Parallax.Unity
{
    public class ParallaxLayers
    {
        readonly ParallaxMotion _parallaxMotion = new();

        readonly List<ParallaxLayer> _layers = new();

        Transform _backgroundRoot;

        float _lastCameraX = float.NaN;

        public void Build(IReadOnlyList<Sprite> sprites, float[] speeds, Transform root, Camera cam)
        {
            Clear(root);

            if (sprites == null || speeds == null || root == null || sprites.Count == 0)
            {
                return;
            }

            if (speeds.Length != sprites.Count)
            {
                Debug.LogError($"ParallaxLayers: speeds length ({speeds.Length}) != sprite count ({sprites.Count}).");
                return;
            }

            _parallaxMotion.Init(speeds);
            _backgroundRoot = root;

            var layerCount = sprites.Count;

            for (var i = layerCount - 1; i >= 0; i--)
            {
                var sprite = sprites[i];
                if (sprite == null)
                {
                    continue;
                }

                var layerHeight = GetLayerHeight(cam, i);
                var scale = layerHeight > 0f ? layerHeight / sprite.bounds.size.y : 1f;
                var scaledWidth = sprite.bounds.size.x * scale;
                var tileCount = GetTileCount(cam, scaledWidth);
                var layer = new ParallaxLayer();
                var sortingOrder = i < layerCount - 1 ? i : layerCount;

                layer.Build(root, sprite, i, 0f, tileCount, sortingOrder, layerHeight);
                _layers.Insert(0, layer);
            }
        }

        public void UpdateParallax(Camera cam)
        {
            if (cam == null || _backgroundRoot == null)
            {
                return;
            }

            var cameraX = cam.transform.position.x;

            if (Mathf.Approximately(cameraX, _lastCameraX))
            {
                return;
            }

            _lastCameraX = cameraX;

            var rootWorldX = _backgroundRoot.position.x;
            var viewLeft = cameraX - GetViewWidth(cam) * 0.5f;
            var layersCount = _layers.Count;

            for (var i = 0; i < layersCount; i++)
            {
                var scroll = _parallaxMotion.GetLayerOffset(i, cameraX);
                _layers[i].Update(scroll, viewLeft, rootWorldX);
            }
        }

        public void Clear(Transform root)
        {
            _layers.Clear();
            _backgroundRoot = null;
            _lastCameraX = float.NaN;

            if (root == null)
            {
                return;
            }

            for (var i = root.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(root.GetChild(i).gameObject);
            }
        }

        static float GetViewWidth(Camera cam)
        {
            return cam != null ? cam.orthographicSize * 2f * cam.aspect : 0f;
        }

        static float GetVisibleBandHeight(Camera cam)
        {
            if (cam == null)
            {
                return 0f;
            }

            return cam.orthographicSize * 2f / 3f;
        }

        static float GetLayerHeight(Camera cam, int layerIndex)
        {
            var band = GetVisibleBandHeight(cam);

            if (layerIndex == 0)
            {
                const float skyOverhang = 1.5f;
                return band * skyOverhang;
            }

            return band;
        }

        static int GetTileCount(Camera cam, float tileWidth)
        {
            var viewWidth = GetViewWidth(cam);
            if (viewWidth <= 0f || tileWidth <= 0f)
            {
                return 3;
            }

            return Mathf.CeilToInt(viewWidth / tileWidth) + 2;
        }
    }
}
