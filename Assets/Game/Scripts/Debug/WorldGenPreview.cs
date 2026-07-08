using System.Diagnostics;
using Operator.Bootstrap;
using Operator.Data;
using Operator.Depth.Core;
using Operator.Depth.Unity;
using Operator.Managers;
using UnityEngine;
using CoreWorld = Operator.Depth.Core.World;
using Debug = UnityEngine.Debug;

namespace Operator.Dev
{
    /// <summary>
    /// Превью: R из конфига. Заливает ±R по X и R вниз от Main Camera.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class WorldGenPreview : MonoBehaviour
    {
        [SerializeField] DepthWorld depthWorld;
        [SerializeField] WorldGenConfig worldGenConfig;
        [SerializeField] bool drawOnPlay = true;

        void Start()
        {
            if (drawOnPlay)
            {
                Draw();
            }
        }

        [ContextMenu("Redraw")]
        public void Draw()
        {
            var bootstrap = GameBootstrap.Instance;
            if (bootstrap == null)
            {
                Debug.LogError($"{nameof(WorldGenPreview)}: GameBootstrap not found.", this);
                return;
            }

            if (depthWorld == null)
            {
                depthWorld = FindFirstObjectByType<DepthWorld>();
            }

            if (depthWorld == null)
            {
                Debug.LogError($"{nameof(WorldGenPreview)}: DepthWorld not found.", this);
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError($"{nameof(WorldGenPreview)}: Main Camera not found.", this);
                return;
            }

            var config = worldGenConfig != null ? worldGenConfig : bootstrap.WorldGen;
            if (config == null)
            {
                Debug.LogError($"{nameof(WorldGenPreview)}: WorldGenConfig not assigned.", this);
                return;
            }

            var worldRadius = config.WorldRadius;
            var worldSeed = config.WorldSeed;
            var center = WorldGrid.WorldToCell(cam.transform.position);

            var world = new CoreWorld(worldSeed, worldRadius, config);

            if (GameManager.Instance == null)
            {
                Debug.LogError($"{nameof(WorldGenPreview)}: GameManager not found — черви не заведутся.", this);
            }
            else
            {
                GameManager.Instance.World = world;
                GameManager.Instance.WormWorld = new WormWorld(worldSeed, worldRadius, config);
            }

            var sw = Stopwatch.StartNew();
            depthWorld.RefreshAroundCell(world, bootstrap.Assets, center, worldRadius);
            sw.Stop();

            var paintedX = worldRadius * 2 + 1;
            var yEnd = Mathf.Min(center.y + worldRadius, worldRadius);
            var yStart = Mathf.Max(0, center.y);
            var painted = paintedX * Mathf.Max(0, yEnd - yStart + 1);

            Debug.Log(
                $"{nameof(WorldGenPreview)}: config={config.name}, seed={worldSeed}, R={worldRadius}, " +
                $"center=({center.x},{center.y}), draw X±{worldRadius} Y+{worldRadius}, painted≈{painted}, " +
                $"inclusions {config.InclusionClusterSizeMin}-{config.InclusionClusterSizeMax}, " +
                $"{sw.ElapsedMilliseconds} ms — Redraw после правок",
                this);
        }
    }
}
