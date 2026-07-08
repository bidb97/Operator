using System.Collections.Generic;
using UnityEngine;

namespace Operator.Parallax.Unity
{
    public class ParallaxLayer
    {
        Transform _root;

        readonly List<Transform> _tiles = new();

        float _tileWidth;

        float _lastScroll = float.NaN;

        float _lastFirstLocalX = float.NaN;

        float _alignY;

        public void Build(Transform parent, Sprite sprite, int layerIndex, float yTop, int tileCount, int sortingOrder, float backgroundHeight)
        {
            var scale = backgroundHeight > 0f ? backgroundHeight / sprite.bounds.size.y : 1f;

            _tileWidth = sprite.bounds.size.x * scale;
            _lastScroll = float.NaN;
            _lastFirstLocalX = float.NaN;
            _alignY = -sprite.bounds.min.y * scale;

            var layerRoot = new GameObject($"Layer {layerIndex}");

            _root = layerRoot.transform;
            _root.SetParent(parent, false);
            _root.localPosition = new Vector3(0f, yTop, 0f);

            for (var i = 0; i < tileCount; i++)
            {
                var tile = new GameObject($"Tile {i}");
                tile.transform.SetParent(_root, false);
                tile.transform.localScale = new Vector3(scale, scale, 1f);

                var renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = sortingOrder;

                tile.transform.localPosition = new Vector3(0f, _alignY, 0f);
                _tiles.Add(tile.transform);
            }
        }

        public void Update(float scroll, float viewLeft, float rootWorldX)
        {
            if (!Mathf.Approximately(scroll, _lastScroll))
            {
                var rootPos = _root.localPosition;
                rootPos.x = scroll;
                _root.localPosition = rootPos;
                _lastScroll = scroll;
            }

            var firstLocalX = Mathf.Floor((viewLeft - rootWorldX - scroll) / _tileWidth) * _tileWidth;

            if (Mathf.Approximately(firstLocalX, _lastFirstLocalX))
            {
                return;
            }

            _lastFirstLocalX = firstLocalX;

            for (var t = 0; t < _tiles.Count; t++)
            {
                var pos = _tiles[t].localPosition;
                pos.x = firstLocalX + t * _tileWidth;
                pos.y = _alignY;
                _tiles[t].localPosition = pos;
            }
        }
    }
}
