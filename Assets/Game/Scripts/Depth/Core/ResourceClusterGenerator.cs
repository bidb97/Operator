using Operator.Data;
using UnityEngine;

namespace Operator.Depth.Core
{
    /// <summary>Жила в секторе сетки resourceClusterSpacing (якорь + радиус зоны).</summary>
    public readonly struct ResourceSectorVein
    {
        public int SectorBlockX { get; }
        public int SectorBlockY { get; }
        public int AnchorX { get; }
        public int AnchorY { get; }
        public ResourceType Type { get; }
        public int ZoneRadius { get; }

        public ResourceSectorVein(
            int sectorBlockX,
            int sectorBlockY,
            int anchorX,
            int anchorY,
            ResourceType type,
            int zoneRadius)
        {
            SectorBlockX = sectorBlockX;
            SectorBlockY = sectorBlockY;
            AnchorX = anchorX;
            AnchorY = anchorY;
            Type = type;
            ZoneRadius = zoneRadius;
        }

        public bool IsDeVault => Type == ResourceType.DeVault;
    }

    /// <summary>
    /// Пачки ресурсов: якорь + 3–10 клеток. De-Vault только здесь и только глубоко.
    /// </summary>
    public static class ResourceClusterGenerator
    {
        const int ResourceSalt = unchecked((int)0x7F4A7C15u);
        const int CellRollSalt = unchecked((int)0x510E527Fu);
        const int TypeRollSalt = unchecked((int)0x9B05688Cu);
        const float MinFill = 0.15f;
        const float MaxFill = 0.55f;

        public static bool TryGetResource(
            int x,
            int y,
            int worldSeed,
            int worldRadius,
            WorldGenConfig config,
            out ResourceType resource)
        {
            resource = ResourceType.None;

            if (config == null || worldRadius <= 0)
            {
                return false;
            }

            var spacing = config.ResourceClusterSpacing;
            if (spacing <= 0)
            {
                return false;
            }

            var blockX = SectorGrid.LateralBlockIndex(x, spacing);
            var blockY = SectorGrid.DepthBlockIndex(y, spacing);

            for (var dcy = -1; dcy <= 1; dcy++)
            {
                for (var dcx = -1; dcx <= 1; dcx++)
                {
                    if (TryCellInCluster(
                        blockX + dcx,
                        blockY + dcy,
                        x,
                        y,
                        worldSeed,
                        worldRadius,
                        config,
                        out resource))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Есть ли жила, чей якорь rolled в этом секторе (без учёта соседних блоков).
        /// Те же seed/RNG, что у <see cref="TryGetResource"/> для клеток жилы.
        /// </summary>
        public static bool TryGetSectorVein(
            int sectorBlockX,
            int sectorBlockY,
            int worldSeed,
            int worldRadius,
            WorldGenConfig config,
            out ResourceSectorVein vein)
        {
            vein = default;

            if (config == null || worldRadius <= 0 || config.ResourceClusterSpacing <= 0)
            {
                return false;
            }

            if (!TryRollSectorVein(
                sectorBlockX,
                sectorBlockY,
                worldSeed,
                worldRadius,
                config,
                out var anchorX,
                out var anchorY,
                out var resourceType,
                out var zoneRadius,
                out _))
            {
                return false;
            }

            vein = new ResourceSectorVein(
                sectorBlockX,
                sectorBlockY,
                anchorX,
                anchorY,
                resourceType,
                zoneRadius);
            return true;
        }

        public static void SectorBlockRangeForRect(
            int xMin,
            int xMax,
            int yMin,
            int yMax,
            int spacing,
            out int blockXMin,
            out int blockXMax,
            out int blockYMin,
            out int blockYMax)
        {
            blockXMin = SectorGrid.LateralBlockIndex(xMin, spacing);
            blockXMax = SectorGrid.LateralBlockIndex(xMax, spacing);
            blockYMin = SectorGrid.DepthBlockIndex(yMin, spacing);
            blockYMax = SectorGrid.DepthBlockIndex(yMax, spacing);
        }

        static bool TryCellInCluster(
            int clusterBlockX,
            int clusterBlockY,
            int x,
            int y,
            int worldSeed,
            int worldRadius,
            WorldGenConfig config,
            out ResourceType resource)
        {
            resource = ResourceType.None;

            if (!TryRollSectorVein(
                clusterBlockX,
                clusterBlockY,
                worldSeed,
                worldRadius,
                config,
                out var anchorX,
                out var anchorY,
                out var resourceType,
                out var zoneRadius,
                out var fillChance))
            {
                return false;
            }

            var lx = x - anchorX;
            var ly = y - anchorY;
            if (Mathf.Abs(lx) > zoneRadius || Mathf.Abs(ly) > zoneRadius)
            {
                return false;
            }

            if (!RollResourceCell(worldSeed, clusterBlockX, clusterBlockY, lx, ly, fillChance))
            {
                return false;
            }

            if (!IsResourceHostCell(x, y, worldRadius))
            {
                return false;
            }

            resource = resourceType;
            return true;
        }

        static bool TryRollSectorVein(
            int clusterBlockX,
            int clusterBlockY,
            int worldSeed,
            int worldRadius,
            WorldGenConfig config,
            out int anchorX,
            out int anchorY,
            out ResourceType resourceType,
            out int zoneRadius,
            out float fillChance)
        {
            anchorX = 0;
            anchorY = 0;
            resourceType = ResourceType.None;
            zoneRadius = 0;
            fillChance = 0f;

            var rng = CreateRng(worldSeed, clusterBlockX, clusterBlockY);
            if (rng.NextDouble() > config.ResourceClusterChance)
            {
                return false;
            }

            var spacing = config.ResourceClusterSpacing;
            anchorX = SectorGrid.LateralBlockOrigin(clusterBlockX, spacing) + rng.Next(0, spacing);
            anchorY = SectorGrid.DepthBlockOrigin(clusterBlockY, spacing) + rng.Next(0, spacing);

            if (!IsResourceHostCell(anchorX, anchorY, worldRadius))
            {
                return false;
            }

            resourceType = RollClusterResourceType(
                worldSeed,
                clusterBlockX,
                clusterBlockY,
                anchorY,
                worldRadius,
                config);

            zoneRadius = rng.Next(config.ResourceClusterRadiusMin, config.ResourceClusterRadiusMax + 1);

            var targetCells = rng.Next(config.ResourceClusterSizeMin, config.ResourceClusterSizeMax + 1);
            var zoneSide = 2 * zoneRadius + 1;
            fillChance = Mathf.Clamp((float)targetCells / (zoneSide * zoneSide), MinFill, MaxFill);
            return true;
        }

        static ResourceType RollClusterResourceType(
            int worldSeed,
            int clusterBlockX,
            int clusterBlockY,
            int anchorY,
            int worldRadius,
            WorldGenConfig config)
        {
            var depthPercent = anchorY / (float)worldRadius * 100f;
            var roll = Hash01(worldSeed, clusterBlockX, clusterBlockY, TypeRollSalt, 0, 0);

            if (depthPercent >= config.ResourceDeVaultMinDepthPercent
                && roll < config.ResourceDeVaultClusterWeight)
            {
                return ResourceType.DeVault;
            }

            var commonRoll = Hash01(worldSeed, clusterBlockX, clusterBlockY, TypeRollSalt, 1, 0);
            var pick = (int)(commonRoll * 3f);

            return pick switch
            {
                0 => ResourceType.Synth,
                1 => ResourceType.Rellit,
                _ => ResourceType.Lumin
            };
        }

        static bool RollResourceCell(
            int worldSeed,
            int blockX,
            int blockY,
            int lx,
            int ly,
            float fillChance)
        {
            var u = Hash01(worldSeed, blockX, blockY, CellRollSalt, lx, ly);
            return u < fillChance;
        }

        static bool IsResourceHostCell(int x, int y, int worldRadius) =>
            GarageBounds.CanHostResource(x, y, worldRadius);

        static float Hash01(
            int worldSeed,
            int blockX,
            int blockY,
            int salt,
            int lx,
            int ly)
        {
            unchecked
            {
                uint h = (uint)worldSeed;
                h ^= (uint)blockX * 0x9E3779B9u;
                h ^= (uint)blockY * 0x85EBCA6Bu;
                h ^= (uint)salt;
                h ^= (uint)(lx + 17) * 0xC2B2AE35u;
                h ^= (uint)(ly + 31) * 0x27D4EB2Fu;
                h = ((h >> 16) ^ h) * 0x7FEB352Du;
                h = ((h >> 15) ^ h) * 0x846CA68Bu;
                h = (h >> 16) ^ h;
                return h / (float)uint.MaxValue;
            }
        }

        static System.Random CreateRng(int worldSeed, int clusterBlockX, int clusterBlockY)
        {
            unchecked
            {
                var hash = worldSeed;
                hash = hash * 31 + clusterBlockX;
                hash = hash * 31 + clusterBlockY;
                hash = hash * 31 + ResourceSalt;
                return new System.Random(hash);
            }
        }
    }
}
