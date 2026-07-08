using Operator.Data;
using UnityEngine;

namespace Operator.Depth.Core
{
    public static class MagneticAnomalySpawnGenerator
    {
        const int AnomalySalt = unchecked((int)0x6C62272Eu);

        public static bool TrySpawnBlock(
            int blockX,
            int blockY,
            int worldSeed,
            int worldRadius,
            WorldGenConfig config,
            out MagneticAnomaly anomaly)
        {
            anomaly = null;

            if (config == null || worldRadius <= 0 || config.MagneticSectorCells <= 0)
            {
                return false;
            }

            var sector = config.MagneticSectorCells;
            var coarse = config.MagneticSpawnBlockSectors;
            var spacing = sector * coarse;

            var rng = CreateRng(worldSeed, blockX, blockY);
            if (rng.NextDouble() > config.MagneticSectorChance)
            {
                return false;
            }

            var anchorX = blockX * spacing + rng.Next(0, spacing);
            var anchorY = blockY * spacing + rng.Next(0, spacing);
            var depthPercent = anchorY / (float)worldRadius * 100f;
            if (depthPercent < config.MagneticMinDepthPercent)
            {
                return false;
            }

            if (anchorY < GarageBounds.ExitCell.y + 1 || anchorY > worldRadius || GarageBounds.Contains(anchorX, anchorY))
            {
                return false;
            }

            var isSmall = rng.NextDouble() < config.MagneticSmallChance;
            float radiusX;
            float radiusY;
            float roamRadius;
            if (isSmall)
            {
                var smallSectors = Mathf.Lerp(
                    config.MagneticSmallRadiusMinSectors,
                    config.MagneticSmallRadiusMaxSectors,
                    (float)rng.NextDouble());
                var small = sector * smallSectors * (0.65f + (float)rng.NextDouble() * 0.35f);
                radiusX = small;
                radiusY = small * (0.75f + (float)rng.NextDouble() * 0.5f);
                roamRadius = rng.Next(config.MagneticSmallRoamMinCells, config.MagneticSmallRoamMaxCells + 1);
            }
            else
            {
                var radiusXSectors = rng.Next(config.MagneticRadiusXMinSectors, config.MagneticRadiusXMaxSectors + 1);
                var radiusYSectors = rng.Next(config.MagneticRadiusYMinSectors, config.MagneticRadiusYMaxSectors + 1);
                var aspect = 0.65f + (float)rng.NextDouble() * 0.7f;
                radiusX = radiusXSectors * sector * aspect;
                radiusY = Mathf.Max(sector, radiusYSectors * sector / aspect);
                roamRadius = rng.Next(config.MagneticLargeRoamMinCells, config.MagneticLargeRoamMaxCells + 1);
            }

            var center = new Vector2(anchorX + WorldGrid.HalfCell, anchorY + WorldGrid.HalfCell);
            var driftSpeed = Mathf.Lerp(
                config.MagneticDriftSpeedMin,
                config.MagneticDriftSpeedMax,
                (float)rng.NextDouble());
            var driftAngle = (float)rng.NextDouble() * Mathf.PI * 2f;
            var driftVelocity = new Vector2(Mathf.Cos(driftAngle), Mathf.Sin(driftAngle)) * driftSpeed;
            var direction = (MagneticDirection)rng.Next(0, 4);
            var directionTimer = Mathf.Lerp(
                config.MagneticDirectionChangeMin,
                config.MagneticDirectionChangeMax,
                (float)rng.NextDouble());

            anomaly = new MagneticAnomaly(
                blockX,
                blockY,
                new Vector2Int(anchorX, anchorY),
                center,
                radiusX,
                radiusY,
                roamRadius,
                direction,
                rng.Next(),
                driftVelocity,
                directionTimer,
                config.MagneticBoostMultiplier,
                config.MagneticSlowMultiplier,
                config.MagneticAgainstMultiplier);

            return true;
        }

        public static int CoarseSpacing(WorldGenConfig config) =>
            config.MagneticSectorCells * Mathf.Max(1, config.MagneticSpawnBlockSectors);

        public static int CoarseBlockIndex(int coord, int spacing) =>
            Mathf.FloorToInt((float)coord / spacing);

        public static void CoarseBlockRangeForRect(
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
            blockXMin = CoarseBlockIndex(xMin, spacing);
            blockXMax = CoarseBlockIndex(xMax, spacing);
            blockYMin = CoarseBlockIndex(yMin, spacing);
            blockYMax = CoarseBlockIndex(yMax, spacing);
        }

        public static MagneticDirection RollDirection(int worldSeed, int blockX, int blockY, int step)
        {
            var rng = CreateRng(worldSeed, blockX, blockY, step + 1000);
            return (MagneticDirection)rng.Next(0, 4);
        }

        static System.Random CreateRng(int worldSeed, int blockX, int blockY, int salt = 0)
        {
            unchecked
            {
                var hash = worldSeed;
                hash = hash * 31 + blockX;
                hash = hash * 31 + blockY;
                hash = hash * 31 + AnomalySalt;
                hash = hash * 31 + salt;
                return new System.Random(hash);
            }
        }
    }
}
