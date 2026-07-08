using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Operator.Depth.Unity
{
    /// <summary>
    /// Сетка спрайтов (8×8 по 512 px и т.д.). Клетка (x, y) → свой кусок мозаики, без пропусков.
    /// Мельче камни — уменьши исходник (2048) и slice/PPU, не scale в коде (ломает швы при TileAnchor 0,0.5).
    /// </summary>
    [CreateAssetMenu(fileName = "MosaicRockTile", menuName = "Operator/Mosaic Rock Tile")]
    public class MosaicRockTile : TileBase
    {
        [SerializeField] Sprite[] sprites;
        [SerializeField] int columns = 8;
        [SerializeField] int rows = 8;

#if UNITY_EDITOR
        [SerializeField] Texture2D sourceTexture;
#endif

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData = default;

            if (sprites == null || sprites.Length == 0 || columns <= 0 || rows <= 0)
            {
                return;
            }

            var col = Mod(position.x, columns);
            var row = Mod(-position.y, rows);
            var index = row * columns + col;

            if (index < 0 || index >= sprites.Length || sprites[index] == null)
            {
                return;
            }

            tileData.sprite = sprites[index];
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.None;
        }

        static int Mod(int value, int span)
        {
            if (span <= 0)
            {
                return 0;
            }

            var remainder = value % span;
            return remainder < 0 ? remainder + span : remainder;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (sourceTexture == null)
            {
                return;
            }

            var loaded = LoadSpritesFromTexture(sourceTexture);
            if (loaded.Length > 0)
            {
                sprites = loaded;
                TryInferGridSize(loaded.Length);
            }
        }

        void TryInferGridSize(int spriteCount)
        {
            var side = Mathf.RoundToInt(Mathf.Sqrt(spriteCount));
            if (side * side != spriteCount)
            {
                return;
            }

            columns = side;
            rows = side;
        }

        [ContextMenu("Reload Sliced Sprites")]
        void ReloadSlicedSprites()
        {
            if (sourceTexture == null)
            {
                return;
            }

            sprites = LoadSpritesFromTexture(sourceTexture);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        static Sprite[] LoadSpritesFromTexture(Texture2D texture)
        {
            var path = UnityEditor.AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
            {
                return Array.Empty<Sprite>();
            }

            return UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(SpriteOrderKey)
                .ToArray();
        }

        static int SpriteOrderKey(Sprite sprite)
        {
            var name = sprite.name;
            var separator = name.LastIndexOf('_');
            if (separator >= 0 && int.TryParse(name.AsSpan(separator + 1), out var index))
            {
                return index;
            }

            return 0;
        }
#endif
    }
}
