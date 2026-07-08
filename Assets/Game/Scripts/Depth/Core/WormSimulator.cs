using System.Collections.Generic;
using UnityEngine;

namespace Operator.Depth.Core
{
    public static class WormSimulator
    {
        static readonly Vector2Int[] Cardinal =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1)
        };
        const int AttackPreferredDistanceCells = 2;

        public static bool IsValidChain(IReadOnlyList<Vector2Int> segments)
        {
            if (segments == null || segments.Count <= 1)
            {
                return true;
            }

            for (var i = 0; i < segments.Count - 1; i++)
            {
                var delta = segments[i + 1] - segments[i];
                if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool TryWanderStep(Worm worm, int worldRadius, int blockSpacing, System.Random rng)
        {
            if (worm?.Segments == null || worm.Segments.Count == 0)
            {
                return false;
            }

            var segments = worm.Segments;
            var head = segments[0];
            var candidates = BuildStepCandidates(head, segments, rng);

            for (var i = 0; i < candidates.Count; i++)
            {
                var next = WrapStep(candidates[i], worldRadius);
                if (!CanStepTo(worm, segments, next, worldRadius, blockSpacing, true))
                {
                    continue;
                }

                ShiftHead(segments, next);
                return true;
            }

            return false;
        }

        public static bool TryAttackStep(
            Worm worm,
            Vector2Int targetCell,
            int worldRadius,
            int blockSpacing,
            System.Random rng)
        {
            if (worm?.Segments == null || worm.Segments.Count == 0)
            {
                return false;
            }

            var segments = worm.Segments;
            var head = segments[0];
            var candidates = BuildAttackCandidates(head, targetCell, worldRadius, rng);

            for (var i = 0; i < candidates.Count; i++)
            {
                var next = WrapStep(candidates[i], worldRadius);
                if (!CanStepTo(worm, segments, next, worldRadius, blockSpacing, false))
                {
                    continue;
                }

                ShiftHead(segments, next);
                return true;
            }

            return TryWanderStep(worm, worldRadius, blockSpacing, rng);
        }

        public static bool TryEmergencyStep(Worm worm, int worldRadius, System.Random rng)
        {
            if (worm?.Segments == null || worm.Segments.Count == 0)
            {
                return false;
            }

            var segments = worm.Segments;
            var head = segments[0];
            var candidates = new List<Vector2Int>(Cardinal.Length);

            for (var i = 0; i < Cardinal.Length; i++)
            {
                candidates.Add(WrapStep(StepCell(head, Cardinal[i]), worldRadius));
            }

            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var next = candidates[i];
                if (!WormRules.CanOccupy(next.x, next.y, worldRadius))
                {
                    continue;
                }

                ShiftHead(segments, next);
                return true;
            }

            return false;
        }

        public static bool TryDashStep(Worm worm, Vector2Int direction, int worldRadius)
        {
            if (worm?.Segments == null || worm.Segments.Count == 0)
            {
                return false;
            }

            var next = WrapStep(StepCell(worm.Segments[0], direction), worldRadius);
            if (!WormRules.CanOccupy(next.x, next.y, worldRadius))
            {
                return false;
            }

            ShiftHead(worm.Segments, next);
            return true;
        }

        static List<Vector2Int> BuildStepCandidates(
            Vector2Int head,
            List<Vector2Int> segments,
            System.Random rng)
        {
            var result = new List<Vector2Int>(4);
            var used = new HashSet<Vector2Int>();

            void TryAdd(Vector2Int cell)
            {
                if (used.Add(cell))
                {
                    result.Add(cell);
                }
            }

            var others = new List<Vector2Int>(3);
            for (var i = 0; i < Cardinal.Length; i++)
            {
                var cell = StepCell(head, Cardinal[i]);
                if (segments.Count >= 2 && cell == segments[1])
                {
                    continue;
                }

                others.Add(cell);
            }

            for (var i = others.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (others[i], others[j]) = (others[j], others[i]);
            }

            // ~30% — поворот вместо прямо (не уезжают линией в бесконечность).
            if (segments.Count >= 2 && rng.NextDouble() > 0.3)
            {
                var forward = head - segments[1];
                TryAdd(StepCell(head, forward));
            }

            for (var i = 0; i < others.Count; i++)
            {
                TryAdd(others[i]);
            }

            return result;
        }

        static List<Vector2Int> BuildAttackCandidates(
            Vector2Int head,
            Vector2Int targetCell,
            int worldRadius,
            System.Random rng)
        {
            var result = new List<Vector2Int>(Cardinal.Length);
            for (var i = 0; i < Cardinal.Length; i++)
            {
                result.Add(StepCell(head, Cardinal[i]));
            }

            for (var i = result.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }

            var headDistance = DistanceScore(head, targetCell, worldRadius);
            var preferredDistance = AttackPreferredDistanceCells * AttackPreferredDistanceCells;

            result.Sort((a, b) =>
                AttackCandidateScore(
                    WrapStep(a, worldRadius),
                    targetCell,
                    worldRadius,
                    headDistance,
                    preferredDistance)
                .CompareTo(AttackCandidateScore(
                    WrapStep(b, worldRadius),
                    targetCell,
                    worldRadius,
                    headDistance,
                    preferredDistance)));

            return result;
        }

        static Vector2Int StepCell(Vector2Int head, Vector2Int dir) =>
            new(head.x + dir.x, head.y + dir.y);

        static Vector2Int WrapStep(Vector2Int cell, int worldRadius) =>
            new(WormRules.WrapX(cell.x, worldRadius), cell.y);

        static int DistanceScore(Vector2Int a, Vector2Int b, int worldRadius)
        {
            var dx = Mathf.Abs(a.x - b.x);
            var width = worldRadius * 2 + 1;
            dx = Mathf.Min(dx, width - dx);
            var dy = a.y - b.y;
            return dx * dx + dy * dy;
        }

        static int AttackCandidateScore(
            Vector2Int cell,
            Vector2Int targetCell,
            int worldRadius,
            int headDistance,
            int preferredDistance)
        {
            var distance = DistanceScore(cell, targetCell, worldRadius);
            if (headDistance <= preferredDistance)
            {
                return Mathf.Abs(distance - preferredDistance);
            }

            return distance;
        }

        static bool CanStepTo(
            Worm worm,
            List<Vector2Int> segments,
            Vector2Int next,
            int worldRadius,
            int blockSpacing,
            bool constrainToSpawnBlock)
        {
            if (segments.Count >= 2 && segments[1] == next)
            {
                return false;
            }

            if (constrainToSpawnBlock
                && !WormRules.InSpawnBlock(next.x, next.y, worm.BlockX, worm.BlockY, blockSpacing))
            {
                return false;
            }

            if (segments.Count >= 2 && segments[^1] == next)
            {
                return WormRules.CanOccupy(next.x, next.y, worldRadius);
            }

            return WormRules.CanOccupy(next.x, next.y, worldRadius);
        }

        static void ShiftHead(List<Vector2Int> segments, Vector2Int next)
        {
            for (var i = segments.Count - 1; i >= 1; i--)
            {
                segments[i] = segments[i - 1];
            }

            segments[0] = next;
        }
    }
}
