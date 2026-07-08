using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Depth.Unity;
using Operator.Managers;
using UnityEngine;

namespace Operator.Dev
{
    /// <summary>
    /// Тест жил на WorldGenDebug: по одной клетке на каждый ресурс.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class VeinCrystalDebugPreview : MonoBehaviour
    {
        [SerializeField] ResourcesOverlay resourcesOverlay;
        [SerializeField] int demoDepthY = 5;
        [SerializeField] int demoStartX = 2;

        void Start()
        {
            Draw();
        }

        [ContextMenu("Redraw Veins")]
        public void Draw()
        {
            var bootstrap = GameBootstrap.Instance;
            if (bootstrap == null)
            {
                return;
            }

            if (resourcesOverlay == null)
            {
                resourcesOverlay = FindFirstObjectByType<ResourcesOverlay>();
            }

            if (resourcesOverlay == null)
            {
                Debug.LogError($"{nameof(VeinCrystalDebugPreview)}: ResourcesOverlay not found.", this);
                return;
            }

            var seed = bootstrap.Session?.Seed ?? 42;
            resourcesOverlay.Clear();

            var types = new[]
            {
                ResourceType.Synth,
                ResourceType.Rellit,
                ResourceType.Lumin,
                ResourceType.DeVault
            };

            for (var i = 0; i < types.Length; i++)
            {
                resourcesOverlay.SyncResource(bootstrap.Assets, demoStartX + i, demoDepthY, seed, types[i]);
            }
        }
    }
}
