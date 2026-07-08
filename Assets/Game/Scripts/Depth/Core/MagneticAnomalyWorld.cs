using System.Collections.Generic;
using Operator.Data;
using UnityEngine;

namespace Operator.Depth.Core
{
    public sealed class MagneticAnomalyWorld
    {
        readonly int _worldSeed;
        readonly int _worldRadius;
        readonly WorldGenConfig _config;
        readonly Dictionary<long, MagneticAnomaly> _anomalies = new();
        readonly HashSet<long> _resolvedBlocks = new();

        public MagneticAnomalyWorld(int worldSeed, int worldRadius, WorldGenConfig config)
        {
            _worldSeed = worldSeed;
            _worldRadius = worldRadius;
            _config = config;
        }

        public IEnumerable<MagneticAnomaly> Anomalies => _anomalies.Values;

        public int Count => _anomalies.Count;

        public void EnsureInRect(int xMin, int xMax, int yMin, int yMax)
        {
            if (_config == null || _config.MagneticSectorCells <= 0)
            {
                return;
            }

            var spacing = MagneticAnomalySpawnGenerator.CoarseSpacing(_config);
            MagneticAnomalySpawnGenerator.CoarseBlockRangeForRect(
                xMin,
                xMax,
                yMin,
                yMax,
                spacing,
                out var blockXMin,
                out var blockXMax,
                out var blockYMin,
                out var blockYMax);

            for (var blockY = blockYMin; blockY <= blockYMax; blockY++)
            {
                for (var blockX = blockXMin; blockX <= blockXMax; blockX++)
                {
                    EnsureBlock(blockX, blockY);
                }
            }
        }

        public void Tick(float deltaTime, Vector3? occupantWorld = null)
        {
            if (_config == null || deltaTime <= 0f || _anomalies.Count == 0)
            {
                return;
            }

            foreach (var anomaly in _anomalies.Values)
            {
                TickAnomaly(anomaly, deltaTime, occupantWorld);
            }
        }

        public bool TryGetAtWorld(Vector3 worldPos, out MagneticAnomaly anomaly)
        {
            anomaly = null;

            foreach (var candidate in _anomalies.Values)
            {
                if (!MagneticFieldQuery.IsInsideWorld(candidate, worldPos, _worldRadius))
                {
                    continue;
                }

                anomaly = candidate;
                return true;
            }

            return false;
        }

        void EnsureBlock(int blockX, int blockY)
        {
            var key = MagneticAnomalyIds.Key(blockX, blockY);
            if (_resolvedBlocks.Contains(key))
            {
                return;
            }

            _resolvedBlocks.Add(key);

            if (!MagneticAnomalySpawnGenerator.TrySpawnBlock(
                blockX,
                blockY,
                _worldSeed,
                _worldRadius,
                _config,
                out var anomaly))
            {
                return;
            }

            if (IsTooClose(anomaly))
            {
                return;
            }

            _anomalies[key] = anomaly;
        }

        bool IsTooClose(MagneticAnomaly anomaly)
        {
            var minSep = _config.MagneticMinSeparationCells;
            if (minSep <= 0)
            {
                return false;
            }

            var minSepSq = (float)minSep * minSep;
            foreach (var existing in _anomalies.Values)
            {
                var delta = existing.Center - anomaly.Center;
                if (delta.sqrMagnitude < minSepSq)
                {
                    return true;
                }
            }

            return false;
        }

        void TickAnomaly(MagneticAnomaly anomaly, float deltaTime, Vector3? occupantWorld)
        {
            var anchorCenter = new Vector2(
                anomaly.Anchor.x + WorldGrid.HalfCell,
                anomaly.Anchor.y + WorldGrid.HalfCell);

            anomaly.Center += anomaly.DriftVelocity * deltaTime;

            var offset = anomaly.Center - anchorCenter;
            var roam = anomaly.RoamRadius;
            if (offset.sqrMagnitude > roam * roam)
            {
                anomaly.Center = anchorCenter + offset.normalized * roam;
                if (offset.sqrMagnitude > 0.0001f)
                {
                    anomaly.DriftVelocity = Vector2.Reflect(
                        anomaly.DriftVelocity,
                        offset.normalized);
                }
            }

            if (occupantWorld.HasValue
                && MagneticFieldQuery.IsInsideWorld(anomaly, occupantWorld.Value, _worldRadius))
            {
                return;
            }

            anomaly.DirectionTimer -= deltaTime;
            if (anomaly.DirectionTimer > 0f)
            {
                return;
            }

            anomaly.DirectionStep++;
            anomaly.Direction = MagneticAnomalySpawnGenerator.RollDirection(
                _worldSeed,
                anomaly.BlockX,
                anomaly.BlockY,
                anomaly.DirectionStep);
            anomaly.DirectionTimer = Mathf.Lerp(
                _config.MagneticDirectionChangeMin,
                _config.MagneticDirectionChangeMax,
                Hash01(_worldSeed, anomaly.BlockX, anomaly.BlockY, anomaly.DirectionStep));
        }

        static float Hash01(int worldSeed, int blockX, int blockY, int step)
        {
            unchecked
            {
                var hash = worldSeed;
                hash = hash * 31 + blockX;
                hash = hash * 31 + blockY;
                hash = hash * 31 + step;
                hash = hash * 31 + 0x31415926;
                return (hash & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }
    }
}
