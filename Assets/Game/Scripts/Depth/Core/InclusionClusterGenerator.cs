using Operator.Data;
using UnityEngine;

namespace Operator.Depth.Core
{
    /// <summary>
    /// Пачки вкраплений: органичный blob поклеточно, один tier на кластер.
    /// </summary>
    public static class InclusionClusterGenerator
    {
        const int InclusionSalt = unchecked((int)0x1C4A9E2Bu);
        const int ShapeNoiseSalt = unchecked((int)0x6A09E667u);

        static readonly Vector2Int[] CardinalOffsets =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1)
        };

        public static bool TryGetInclusion(
            int x,
            int y,
            int worldSeed,
            int worldRadius,
            RockType dominant,
            WorldGenConfig config,
            out RockType inclusion)
        {
            inclusion = default;

            if (config == null || worldRadius <= 0)
            {
                return false;
            }

            var dominantTier = RockLayers.LaserTier(dominant);
            if (dominantTier >= RockLayers.MaxLaserTier)
            {
                return false;
            }

            var spacing = config.InclusionClusterSpacing;
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
                        dominantTier,
                        config,
                        out inclusion))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static bool TryCellInCluster(
            int clusterBlockX,
            int clusterBlockY,
            int x,
            int y,
            int worldSeed,
            int worldRadius,
            int dominantTier,
            WorldGenConfig config,
            out RockType inclusion)
        {
            inclusion = default;
            var rng = CreateRng(worldSeed, clusterBlockX, clusterBlockY);

            if (rng.NextDouble() > config.InclusionClusterChance)
            {
                return false;
            }

            var spacing = config.InclusionClusterSpacing;
            var anchorX = SectorGrid.LateralBlockOrigin(clusterBlockX, spacing) + rng.Next(0, spacing);
            var anchorY = SectorGrid.DepthBlockOrigin(clusterBlockY, spacing) + rng.Next(0, spacing);

            if (anchorY < 1 || anchorY >= worldRadius || GarageBounds.Contains(anchorX, anchorY))
            {
                return false;
            }

            var targetCells = rng.Next(config.InclusionClusterSizeMin, config.InclusionClusterSizeMax + 1);
            var radiusFromArea = Mathf.RoundToInt(Mathf.Sqrt(targetCells / Mathf.PI));
            var radiusMin = config.InclusionClusterRadiusMin;
            var radiusMax = config.InclusionClusterRadiusMax;
            var zoneRadius = Mathf.Clamp(radiusFromArea, radiusMin, radiusMax);

            var aspect = 0.65f + (float)rng.NextDouble() * 0.7f;
            var radiusX = zoneRadius * aspect;
            var radiusY = Mathf.Max(1f, zoneRadius / aspect);

            var minTier = dominantTier + 1;
            var maxTier = Mathf.Min(
                RockLayers.MaxLaserTier,
                dominantTier + config.InclusionMaxTierDelta);

            if (minTier > maxTier)
            {
                return false;
            }

            var inclusionTier = minTier + rng.Next(0, maxTier - minTier + 1);

            if (!IsCellInBlob(x, y, worldSeed, clusterBlockX, clusterBlockY, anchorX, anchorY, radiusX, radiusY))
            {
                return false;
            }

            if (!HasBlobNeighbor(
                x,
                y,
                worldSeed,
                worldRadius,
                clusterBlockX,
                clusterBlockY,
                anchorX,
                anchorY,
                radiusX,
                radiusY))
            {
                return false;
            }

            inclusion = RockLayers.TierToRock(inclusionTier);
            return y >= 1 && y < worldRadius && !GarageBounds.Contains(x, y);
        }

        static bool HasBlobNeighbor(
            int x,
            int y,
            int worldSeed,
            int worldRadius,
            int clusterBlockX,
            int clusterBlockY,
            int anchorX,
            int anchorY,
            float radiusX,
            float radiusY)
        {
            for (var i = 0; i < CardinalOffsets.Length; i++)
            {
                var nx = x + CardinalOffsets[i].x;
                var ny = y + CardinalOffsets[i].y;

                if (ny < 1 || ny >= worldRadius || GarageBounds.Contains(nx, ny))
                {
                    continue;
                }

                if (IsCellInBlob(
                    nx,
                    ny,
                    worldSeed,
                    clusterBlockX,
                    clusterBlockY,
                    anchorX,
                    anchorY,
                    radiusX,
                    radiusY))
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsCellInBlob(
            int x,
            int y,
            int worldSeed,
            int clusterBlockX,
            int clusterBlockY,
            int anchorX,
            int anchorY,
            float radiusX,
            float radiusY)
        {
            var lx = x - anchorX;
            var ly = y - anchorY;
            var nx = lx / radiusX;
            var ny = ly / radiusY;
            var dist = nx * nx + ny * ny;
            var noise = Hash01(worldSeed, clusterBlockX, clusterBlockY, ShapeNoiseSalt, lx, ly);
            noise = (noise - 0.5f) * 0.55f;
            return dist + noise < 1f;
        }

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
                hash = hash * 31 + InclusionSalt;
                return new System.Random(hash);
            }
        }
    }
}
