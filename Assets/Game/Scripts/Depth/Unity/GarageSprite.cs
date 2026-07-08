using Operator.Depth.Core;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Operator.Depth.Unity
{
    /// <summary>
    /// Ставит спрайт гаража на зону <see cref="GarageBounds"/> (3×2 клетки).
    /// </summary>
    [DefaultExecutionOrder(250)]
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public class GarageSprite : MonoBehaviour
    {
        const string SpritePath = "Assets/Game/Sprites/Depth/Garage.png";

        [SerializeField] Sprite garageSprite;
        [SerializeField] int sortingOrder = 10;

        void OnEnable() => ApplyLayout();

        void Awake() => ApplyLayout();

        void ApplyLayout()
        {
            var grid = transform.parent != null ? transform.parent.GetComponent<Grid>() : null;
            if (grid != null)
            {
                WorldGrid.Bind(grid);
            }

            var renderer = GetComponent<SpriteRenderer>();
            var sprite = ResolveSprite(renderer);
            if (sprite != null)
            {
                renderer.sprite = sprite;
            }

            renderer.sortingOrder = sortingOrder;

            var bottomLeft = GarageBounds.SpriteBottomLeftWorld();
            if (sprite == null)
            {
                transform.position = bottomLeft;
                return;
            }

            var pivotWorld = new Vector3(
                sprite.pivot.x / sprite.pixelsPerUnit,
                sprite.pivot.y / sprite.pixelsPerUnit,
                0f);

            transform.position = bottomLeft + pivotWorld;
        }

        Sprite ResolveSprite(SpriteRenderer renderer)
        {
            if (garageSprite != null)
            {
                return garageSprite;
            }

#if UNITY_EDITOR
            garageSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (garageSprite != null)
            {
                return garageSprite;
            }
#endif

            return renderer.sprite;
        }
    }
}
