using Operator.Data;
using UnityEngine;

namespace Operator.Depth.Core
{
    public class WorldGenerator
    {
        readonly int _worldSeed;
        readonly WorldGenConfig _config;

        public WorldGenerator(int worldSeed, WorldGenConfig config)
        {
            _worldSeed = worldSeed;
            _config = config;
        }

        public CellData Generate(int x, int y, int worldRadius)
        {
            if (y < 0 || y > worldRadius)
            {
                return new CellData { Rock = RockType.Air };
            }

            if (y == 0)
            {
                return new CellData { Rock = RockType.Air };
            }

            if (y == worldRadius)
            {
                return new CellData { Rock = RockType.Core };
            }

            if (GarageBounds.Contains(x, y))
            {
                return new CellData { Rock = RockType.Air };
            }

            var rock = ResolveDominantRock(x, y, worldRadius);

            if (InclusionClusterGenerator.TryGetInclusion(
                x, y, _worldSeed, worldRadius, rock, _config, out var inclusion))
            {
                rock = inclusion;
            }

            var resource = ResourceType.None;
            if (rock is not RockType.Air and not RockType.Core)
            {
                if (!ResourceClusterGenerator.TryGetResource(x, y, _worldSeed, worldRadius, _config, out resource))
                {
                    ResourceScatterGenerator.TryGetResource(x, y, _worldSeed, worldRadius, _config, out resource);
                }
            }

            return new CellData { Rock = rock, Resource = resource };
        }

        RockType ResolveDominantRock(int x, int y, int worldRadius)
        {
            if (_config == null)
            {
                Debug.LogWarning("WorldGenConfig is not assigned — using flat layers.");
                return RockLayers.GetDominantFlat(y, worldRadius);
            }

            if (worldRadius <= 0)
            {
                return RockType.Edge;
            }

            var depthPercent = y / (float)worldRadius * 100f;
            var percents = _config.LayerDepthPercents;

            if (percents == null || percents.Length == 0)
            {
                return RockType.Edge;
            }

            for (var i = 0; i < percents.Length; i++)
            {
                var threshold = percents[i] + _config.GetLayerBoundaryShiftPercent(x, y, i, _worldSeed);
                if (depthPercent < threshold)
                {
                    return (RockType)(i + 1);
                }
            }

            return RockType.Halo;
        }
    }
}
