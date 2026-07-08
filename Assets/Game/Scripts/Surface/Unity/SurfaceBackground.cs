using System.Collections.Generic;
using System.Reflection;
using Operator.Data;
using Operator.Parallax.Unity;
using UnityEngine;

namespace Operator.Surface.Unity
{
    [DefaultExecutionOrder(50)]
    public class SurfaceBackground : MonoBehaviour
    {
        [SerializeField] AssetPack assetPack;

        readonly ParallaxLayers _parallax = new();

        Camera _camera;

        void Awake()
        {
            _camera = Camera.main;
        }

        void Start()
        {
            BuildForRun();
        }

        void Update()
        {
            _parallax.UpdateParallax(_camera);
        }

        void BuildForRun()
        {
            if (assetPack == null)
            {
                Debug.LogError("SurfaceBackground: assetPack is not assigned.");
                return;
            }

            assetPack.Build();

            var sprites = LoadSprites(assetPack, out var layerCount);
            if (sprites == null)
            {
                return;
            }

            var speeds = BuildSpeeds(layerCount);
            _parallax.Build(sprites, speeds, transform, _camera);
        }

        static List<Sprite> LoadSprites(AssetPack pack, out int layerCount)
        {
            layerCount = 0;
            var sprites = new List<Sprite>();

            var entries = GetEntries(pack);
            if (entries == null || entries.Length == 0)
            {
                Debug.LogError("SurfaceBackground: asset pack has no layers.");
                return null;
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var sprite = ToSprite(entries[i].asset, i);
                if (sprite == null)
                {
                    return null;
                }

                sprites.Add(sprite);
                layerCount++;
            }

            return sprites;
        }

        static AssetPack.Entry[] GetEntries(AssetPack pack)
        {
            var field = typeof(AssetPack).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(pack) as AssetPack.Entry[];
        }

        static float[] BuildSpeeds(int layerCount)
        {
            const float far = 0.1f;
            const float near = 0.5f;

            var speeds = new float[layerCount];

            for (var i = 0; i < layerCount; i++)
            {
                speeds[i] = layerCount <= 1
                    ? far
                    : Mathf.Lerp(far, near, (float)i / (layerCount - 1));
            }

            return speeds;
        }

        static Sprite ToSprite(Object asset, int index)
        {
            if (asset is Sprite sprite)
            {
                return sprite;
            }

            if (asset is Texture2D texture)
            {
                return Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0f),
                    32f);
            }

            Debug.LogError($"SurfaceBackground: layer {index} is {asset.GetType().Name}, expected Sprite or Texture2D.");
            return null;
        }
    }
}
