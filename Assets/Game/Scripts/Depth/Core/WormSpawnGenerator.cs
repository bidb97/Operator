using System.Collections.Generic;
using Operator.Data;
using UnityEngine;

namespace Operator.Depth.Core
{
    public static class WormSpawnGenerator
    {
        const int WormSalt = unchecked((int)0x5A827999u);
        const int MaxWormsPerSector = 3;

        static readonly Vector2Int[] Cardinal =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1)
        };

        /// <summary>
        /// Детерминированный спавн всех червей сектора (жила / wild). Сектор = resourceClusterSpacing.
        /// </summary>
        public static void SpawnSector(
            int sectorBlockX,
            int sectorBlockY,
            int worldSeed,
            int worldRadius,
            WorldGenConfig config,
            List<Worm> output)
        {
            output.Clear();

            if (config == null || worldRadius <= 0 || config.ResourceClusterSpacing <= 0)
            {
                return;
            }

            var spacing = config.ResourceClusterSpacing;
            var sectorYMin = sectorBlockY * spacing;
            var depthPercent = sectorYMin / (float)worldRadius * 100f;
            if (depthPercent < config.WormMinDepthPercent)
            {
                return;
            }

            var hasVein = ResourceClusterGenerator.TryGetSectorVein(
                sectorBlockX,
                sectorBlockY,
                worldSeed,
                worldRadius,
                config,
                out var vein);

            var sectorRng = CreateRng(worldSeed, sectorBlockX, sectorBlockY, 0);
            var wormCount = RollWormCount(hasVein, vein, config, sectorRng);
            if (wormCount <= 0)
            {
                return;
            }

            for (var slot = 0; slot < wormCount; slot++)
            {
                var wormRng = CreateRng(worldSeed, sectorBlockX, sectorBlockY, slot + 1);
                if (!TryCreateWorm(
                    sectorBlockX,
                    sectorBlockY,
                    slot,
                    hasVein,
                    vein,
                    worldSeed,
                    worldRadius,
                    config,
                    wormRng,
                    out var worm))
                {
                    continue;
                }

                output.Add(worm);
            }
        }

        static int RollWormCount(
            bool hasVein,
            ResourceSectorVein vein,
            WorldGenConfig config,
            System.Random rng)
        {
            var count = 0;

            if (hasVein)
            {
                count = vein.IsDeVault
                    ? rng.Next(2, MaxWormsPerSector + 1)
                    : rng.Next(1, 3);
            }
            else if (rng.NextDouble() < config.WormWildChance)
            {
                count = 1;
            }

            var min = config?.WormMinPerSector ?? 0;
            return Mathf.Max(count, min);
        }

        static bool TryCreateWorm(
            int sectorBlockX,
            int sectorBlockY,
            int slot,
            bool hasVein,
            ResourceSectorVein vein,
            int worldSeed,
            int worldRadius,
            WorldGenConfig config,
            System.Random rng,
            out Worm worm)
        {
            worm = null;

            var spacing = config.ResourceClusterSpacing;
            var length = rng.Next(config.WormLengthMin, config.WormLengthMax + 1);
            var centerX = hasVein ? vein.AnchorX : sectorBlockX * spacing + spacing / 2;
            var centerY = hasVein ? vein.AnchorY : sectorBlockY * spacing + spacing / 2;
            var roamRadius = hasVein
                ? vein.ZoneRadius + 2
                : Mathf.Max(4, spacing / 4);

            var kind = RollKind(hasVein, vein, rng);

            for (var attempt = 0; attempt < 16; attempt++)
            {
                if (!TryRollHead(
                    sectorBlockX,
                    sectorBlockY,
                    spacing,
                    hasVein,
                    vein,
                    worldRadius,
                    roamRadius,
                    rng,
                    out var headX,
                    out var headY))
                {
                    continue;
                }

                var segments = BuildCompactSegments(
                    headX,
                    headY,
                    length,
                    worldRadius,
                    centerX,
                    centerY,
                    roamRadius,
                    sectorBlockX,
                    sectorBlockY,
                    spacing,
                    rng);

                if (segments == null || segments.Count < config.WormLengthMin)
                {
                    continue;
                }

                worm = new Worm(
                    sectorBlockX,
                    sectorBlockY,
                    slot,
                    segments,
                    WormState.Dormant,
                    segments.Count,
                    hasVein,
                    kind);
                return true;
            }

            return false;
        }

        static ResourceType RollKind(bool hasVein, ResourceSectorVein vein, System.Random rng)
        {
            if (hasVein)
            {
                return vein.Type;
            }

            return (ResourceType)rng.Next((int)ResourceType.Synth, (int)ResourceType.DeVault + 1);
        }

        static bool TryRollHead(
            int sectorBlockX,
            int sectorBlockY,
            int spacing,
            bool hasVein,
            ResourceSectorVein vein,
            int worldRadius,
            int roamRadius,
            System.Random rng,
            out int headX,
            out int headY)
        {
            headX = 0;
            headY = 0;

            if (hasVein)
            {
                for (var attempt = 0; attempt < 8; attempt++)
                {
                    var offsetX = rng.Next(-roamRadius, roamRadius + 1);
                    var offsetY = rng.Next(-roamRadius, roamRadius + 1);
                    headX = WormRules.WrapX(vein.AnchorX + offsetX, worldRadius);
                    headY = vein.AnchorY + offsetY;

                    if (IsValidHead(headX, headY, worldRadius, sectorBlockX, sectorBlockY, spacing))
                    {
                        return true;
                    }
                }

                return false;
            }

            var blockCellXMin = sectorBlockX * spacing;
            var blockCellYMin = sectorBlockY * spacing;

            headX = WormRules.WrapX(blockCellXMin + rng.Next(0, spacing), worldRadius);
            headY = blockCellYMin + rng.Next(0, spacing);
            return IsValidHead(headX, headY, worldRadius, sectorBlockX, sectorBlockY, spacing);
        }

        static bool IsValidHead(
            int headX,
            int headY,
            int worldRadius,
            int sectorBlockX,
            int sectorBlockY,
            int spacing) =>
            WormRules.CanOccupy(headX, headY, worldRadius)
            && WormRules.InSpawnBlock(headX, headY, sectorBlockX, sectorBlockY, spacing);

        static List<Vector2Int> BuildCompactSegments(
            int headX,
            int headY,
            int length,
            int worldRadius,
            int centerX,
            int centerY,
            int maxRadius,
            int sectorBlockX,
            int sectorBlockY,
            int sectorSpacing,
            System.Random rng)
        {
            var head = new Vector2Int(headX, headY);
            var segments = new List<Vector2Int>(length) { head };
            var occupied = new HashSet<Vector2Int> { head };

            for (var i = 1; i < length; i++)
            {
                var prev = segments[i - 1];
                var candidates = new List<Vector2Int>(Cardinal.Length);

                for (var d = 0; d < Cardinal.Length; d++)
                {
                    var cell = StepCell(prev, Cardinal[d], worldRadius);
                    if (!WormRules.CanOccupy(cell.x, cell.y, worldRadius))
                    {
                        continue;
                    }

                    if (!WormRules.InSpawnBlock(cell.x, cell.y, sectorBlockX, sectorBlockY, sectorSpacing))
                    {
                        continue;
                    }

                    if (occupied.Contains(cell))
                    {
                        continue;
                    }

                    if (Mathf.Abs(cell.x - centerX) + Mathf.Abs(cell.y - centerY) > maxRadius)
                    {
                        continue;
                    }

                    candidates.Add(cell);
                }

                if (candidates.Count == 0)
                {
                    break;
                }

                Shuffle(candidates, rng);
                var pick = candidates[0];
                segments.Add(pick);
                occupied.Add(pick);
            }

            return segments;
        }

        static Vector2Int StepCell(Vector2Int cell, Vector2Int dir, int worldRadius) =>
            new(WormRules.WrapX(cell.x + dir.x, worldRadius), cell.y + dir.y);

        static void Shuffle(List<Vector2Int> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static System.Random CreateRng(int worldSeed, int blockX, int blockY, int salt)
        {
            unchecked
            {
                var hash = worldSeed;
                hash = hash * 31 + blockX;
                hash = hash * 31 + blockY;
                hash = hash * 31 + salt;
                hash = hash * 31 + WormSalt;
                return new System.Random(hash);
            }
        }
    }
}
