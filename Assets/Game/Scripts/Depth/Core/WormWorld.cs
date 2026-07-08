using System.Collections.Generic;
using Operator.Data;
using UnityEngine;

namespace Operator.Depth.Core
{
    public sealed class WormWorld
    {
        readonly int _worldSeed;
        readonly int _worldRadius;
        readonly WorldGenConfig _config;
        readonly Dictionary<long, Worm> _worms = new();
        readonly HashSet<long> _resolvedSectors = new();
        readonly List<Worm> _sectorSpawnBuffer = new();

        public WormWorld(int worldSeed, int worldRadius, WorldGenConfig config)
        {
            _worldSeed = worldSeed;
            _worldRadius = worldRadius;
            _config = config;
        }

        public IEnumerable<Worm> Worms => _worms.Values;

        public int Count => _worms.Count;

        public bool AnyWormChasing => AnyWormInState(WormState.Attack);

        public void EnsureNearCell(Vector2Int cell, int radiusCells)
        {
            EnsureInRect(
                cell.x - radiusCells,
                cell.x + radiusCells,
                cell.y - radiusCells,
                cell.y + radiusCells);
        }

        public static int MinDistanceSqToCell(Worm worm, Vector2Int targetCell, int worldRadius) =>
            MinDistanceSq(worm, targetCell, worldRadius);

        public static Vector3 HeadWorldPosition(Worm worm) =>
            worm.Segments.Count > 0
                ? WorldGrid.DroneCellToWorld(worm.Segments[0])
                : Vector3.zero;

        public void EnsureInRect(int xMin, int xMax, int yMin, int yMax)
        {
            var spacing = _config?.ResourceClusterSpacing ?? 0;
            if (spacing <= 0)
            {
                return;
            }

            ResourceClusterGenerator.SectorBlockRangeForRect(
                xMin,
                xMax,
                yMin,
                yMax,
                spacing,
                out var blockXMin,
                out var blockXMax,
                out var blockYMin,
                out var blockYMax);

            blockXMin -= 1;
            blockXMax += 1;
            blockYMin -= 1;
            blockYMax += 1;

            for (var blockY = blockYMin; blockY <= blockYMax; blockY++)
            {
                for (var blockX = blockXMin; blockX <= blockXMax; blockX++)
                {
                    EnsureSector(blockX, blockY);
                }
            }
        }

        public void Tick(
            float deltaTime,
            int xMin,
            int xMax,
            int yMin,
            int yMax,
            bool hasTarget,
            Vector2Int targetCell)
        {
            if (_config == null || _worms.Count == 0)
            {
                return;
            }

            var sectorSpacing = _config.ResourceClusterSpacing;

            foreach (var worm in _worms.Values)
            {
                TryWake(worm, hasTarget, targetCell);

                if (worm.State == WormState.Attack)
                {
                    worm.AttackTimer -= deltaTime;
                    if (worm.AttackTimer <= 0f)
                    {
                        EndAttack(worm);
                    }
                }

                if (worm.AttackCooldownTimer > 0f)
                {
                    worm.AttackCooldownTimer = Mathf.Max(0f, worm.AttackCooldownTimer - deltaTime);
                }

                if (worm.AttackRetryTimer > 0f)
                {
                    worm.AttackRetryTimer = Mathf.Max(0f, worm.AttackRetryTimer - deltaTime);
                }

                if (hasTarget && TryLoseAggro(worm, targetCell))
                {
                    continue;
                }

                if (!ShouldSimulate(worm))
                {
                    continue;
                }

                var interval = ResolveStepInterval(worm);
                worm.MoveProgress += deltaTime / interval;
                if (worm.MoveProgress < 1f)
                {
                    continue;
                }

                worm.SimStep++;
                var rng = CreateStepRng(worm);
                TryStartAttack(worm, hasTarget, targetCell, xMin, xMax, yMin, yMax, rng);

                SyncPreviousSegments(worm);
                var moved = false;
                if (worm.State == WormState.Attack)
                {
                    moved = TryAttackMove(worm, targetCell, sectorSpacing, rng);
                    worm.NextStepInterval = RollAttackStepInterval(worm, rng);
                }
                else if (worm.State == WormState.Wander)
                {
                    moved = WormSimulator.TryWanderStep(worm, _worldRadius, sectorSpacing, rng);
                }

                if (!moved)
                {
                    moved = WormSimulator.TryEmergencyStep(worm, _worldRadius, rng);
                }

                if (moved)
                {
                    worm.MoveProgress = Mathf.Clamp01(worm.MoveProgress - 1f);
                }
                else
                {
                    worm.MoveProgress = 1f;
                }
            }
        }

        public bool ShouldRender(Worm worm, int xMin, int xMax, int yMin, int yMax)
        {
            if (worm.State == WormState.Attack)
            {
                return true;
            }

            return IntersectsRect(worm, xMin, xMax, yMin, yMax);
        }

        void EnsureSector(int sectorBlockX, int sectorBlockY)
        {
            var key = SectorKey(sectorBlockX, sectorBlockY);
            if (_resolvedSectors.Contains(key))
            {
                return;
            }

            _resolvedSectors.Add(key);

            WormSpawnGenerator.SpawnSector(
                sectorBlockX,
                sectorBlockY,
                _worldSeed,
                _worldRadius,
                _config,
                _sectorSpawnBuffer);

            for (var i = 0; i < _sectorSpawnBuffer.Count; i++)
            {
                var worm = _sectorSpawnBuffer[i];
                _worms[worm.Id] = worm;
            }
        }

        void TryWake(Worm worm, bool hasTarget, Vector2Int targetCell)
        {
            if (worm.Awake || worm.State != WormState.Dormant || !hasTarget)
            {
                return;
            }

            var wakeRadius = _config.WormWakeRadius;
            var wakeRadiusSq = wakeRadius * wakeRadius;
            if (MinDistanceSq(worm, targetCell, _worldRadius) <= wakeRadiusSq)
            {
                worm.Awake = true;
                worm.State = WormState.Wander;
            }
        }

        void TryStartAttack(
            Worm worm,
            bool hasTarget,
            Vector2Int targetCell,
            int xMin,
            int xMax,
            int yMin,
            int yMax,
            System.Random rng)
        {
            if (worm.State != WormState.Wander
                || !hasTarget
                || worm.Segments.Count == 0
                || worm.AttackCooldownTimer > 0f
                || worm.AttackRetryTimer > 0f)
            {
                return;
            }

            if (!IntersectsRect(worm, xMin, xMax, yMin, yMax))
            {
                return;
            }

            var aggroRadius = _config.WormAggroRadius;
            var aggroRadiusSq = aggroRadius * aggroRadius;
            if (MinDistanceSq(worm, targetCell, _worldRadius) > aggroRadiusSq)
            {
                return;
            }

            var chance = worm.OnVein
                ? _config.WormVeinAggressiveChance
                : _config.WormWildAggressiveChance;
            if (rng.NextDouble() > chance)
            {
                worm.AttackRetryTimer = _config.WormAttackRetryCooldown;
                return;
            }

            StartAttack(worm, rng);
        }

        void StartAttack(Worm worm, System.Random rng)
        {
            var min = _config.WormAttackDurationMin;
            var max = _config.WormAttackDurationMax;
            var duration = Mathf.Lerp(min, max, (float)rng.NextDouble());

            worm.State = WormState.Attack;
            worm.AttackTimer = duration;
            worm.AttackBurstStepsLeft = 0;
            worm.NextStepInterval = _config.WormChargeStepInterval;
        }

        void EndAttack(Worm worm)
        {
            worm.State = WormState.Wander;
            worm.AttackTimer = 0f;
            worm.AttackBurstStepsLeft = 0;
            worm.NextStepInterval = 0f;
            worm.AttackCooldownTimer = _config.WormAttackCooldown;
        }

        bool TryAttackMove(
            Worm worm,
            Vector2Int targetCell,
            int sectorSpacing,
            System.Random rng)
        {
            var direction = ResolveTargetDirection(worm, targetCell);

            if (worm.AttackBurstStepsLeft > 0)
            {
                if (WormSimulator.TryDashStep(worm, direction, _worldRadius))
                {
                    worm.AttackBurstStepsLeft--;
                    return true;
                }

                worm.AttackBurstStepsLeft = 0;
            }

            if (rng.NextDouble() < _config.WormAttackBurstChance)
            {
                worm.AttackBurstStepsLeft = Mathf.Max(0, _config.WormDashLength - 1);
                if (WormSimulator.TryDashStep(worm, direction, _worldRadius))
                {
                    return true;
                }

                worm.AttackBurstStepsLeft = 0;
            }

            return WormSimulator.TryAttackStep(
                worm,
                targetCell,
                _worldRadius,
                sectorSpacing,
                rng);
        }

        float RollAttackStepInterval(Worm worm, System.Random rng)
        {
            if (worm.AttackBurstStepsLeft > 0)
            {
                return _config.WormDashStepInterval;
            }

            var roll = rng.NextDouble();
            if (roll < 0.3f)
            {
                return _config.WormDashStepInterval;
            }

            if (roll < 0.65f)
            {
                return _config.WormChargeStepInterval;
            }

            return _config.WormTickInterval;
        }

        float ResolveStepInterval(Worm worm)
        {
            if (worm.State == WormState.Attack && worm.NextStepInterval > 0f)
            {
                return worm.NextStepInterval;
            }

            return worm.State == WormState.Attack
                ? _config.WormChargeStepInterval
                : _config.WormTickInterval;
        }

        Vector2Int ResolveTargetDirection(Worm worm, Vector2Int targetCell)
        {
            var head = worm.Segments[0];
            var dx = targetCell.x - head.x;
            var width = _worldRadius * 2 + 1;

            if (Mathf.Abs(dx) > width / 2)
            {
                dx += dx > 0 ? -width : width;
            }

            var dy = targetCell.y - head.y;
            if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            {
                return dx >= 0 ? Vector2Int.right : Vector2Int.left;
            }

            return dy >= 0 ? Vector2Int.up : Vector2Int.down;
        }

        bool TryLoseAggro(Worm worm, Vector2Int targetCell)
        {
            if (worm.State != WormState.Attack)
            {
                return false;
            }

            var loseRadius = _config.WormLoseAggroRadius;
            var loseRadiusSq = loseRadius * loseRadius;
            if (MinDistanceSq(worm, targetCell, _worldRadius) <= loseRadiusSq)
            {
                return false;
            }

            EndAttack(worm);
            return true;
        }

        static bool ShouldSimulate(Worm worm)
        {
            if (worm.State == WormState.Dormant)
            {
                return false;
            }

            if (worm.State == WormState.Attack)
            {
                return true;
            }

            return worm.Awake;
        }

        static long SectorKey(int blockX, int blockY) => ((long)blockX << 32) ^ (uint)blockY;

        static void SyncPreviousSegments(Worm worm)
        {
            worm.PreviousSegments.Clear();
            worm.PreviousSegments.AddRange(worm.Segments);
        }

        bool AnyWormInState(WormState state)
        {
            foreach (var worm in _worms.Values)
            {
                if (worm.State == state)
                {
                    return true;
                }
            }

            return false;
        }

        static int MinDistanceSq(Worm worm, Vector2Int targetCell, int worldRadius)
        {
            var best = int.MaxValue;
            for (var i = 0; i < worm.Segments.Count; i++)
            {
                var cell = worm.Segments[i];
                var dx = Mathf.Abs(cell.x - targetCell.x);
                var width = worldRadius * 2 + 1;
                dx = Mathf.Min(dx, width - dx);
                var dy = cell.y - targetCell.y;
                var score = dx * dx + dy * dy;
                if (score < best)
                {
                    best = score;
                }
            }

            return best;
        }

        static bool IntersectsRect(Worm worm, int xMin, int xMax, int yMin, int yMax)
        {
            for (var i = 0; i < worm.Segments.Count; i++)
            {
                var cell = worm.Segments[i];
                if (cell.x >= xMin && cell.x <= xMax && cell.y >= yMin && cell.y <= yMax)
                {
                    return true;
                }
            }

            return false;
        }

        static System.Random CreateStepRng(Worm worm)
        {
            unchecked
            {
                var hash = (int)worm.Id;
                hash = hash * 31 + worm.Segments[0].x;
                hash = hash * 31 + worm.Segments[0].y;
                hash = hash * 31 + worm.SimStep;
                return new System.Random(hash);
            }
        }
    }
}
