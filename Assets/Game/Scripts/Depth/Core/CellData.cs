using UnityEngine;

namespace Operator.Depth.Core
{
    public enum RockType
    {
        Air = 0,
        Edge,
        Sediment,
        Scale,
        Arc,
        Frame,
        Seam,
        Mantle,
        Halo,
        Core
    }

    public struct CellData
    {
        public RockType Rock;
        public ResourceType Resource;
        public bool Modified;

        public bool IsSolid => Rock != RockType.Air && Rock != RockType.Core;
        public bool HasResource => ResourceTypeIds.HasResource(Resource);
    }

    public static class RockLayers
    {
        static readonly float[] LayerMaxPercent =
        {
            8f, 15f, 25f, 55f, 68f, 80f, 92f, 100f
        };

        /// <summary>Запасной вариант без WorldGenConfig.</summary>
        public static RockType GetDominantFlat(int y, int worldRadius)
        {
            if (worldRadius <= 0)
            {
                return RockType.Edge;
            }

            var depthPercent = y / (float)worldRadius * 100f;

            for (var i = 0; i < LayerMaxPercent.Length; i++)
            {
                if (depthPercent < LayerMaxPercent[i])
                {
                    return (RockType)(i + 1);
                }
            }

            return RockType.Halo;
        }

        public static string ToAssetId(RockType rock)
        {
            return rock switch
            {
                RockType.Edge => "edge",
                RockType.Sediment => "sediment",
                RockType.Scale => "scale",
                RockType.Arc => "arc",
                RockType.Frame => "frame",
                RockType.Seam => "seam",
                RockType.Mantle => "mantle",
                RockType.Halo => "halo",
                _ => null
            };
        }

        public const int MaxLaserTier = 8;

        public static int LaserTier(RockType rock) => (int)rock;

        public static RockType TierToRock(int tier)
        {
            return (RockType)Mathf.Clamp(tier, 1, MaxLaserTier);
        }
    }
}
