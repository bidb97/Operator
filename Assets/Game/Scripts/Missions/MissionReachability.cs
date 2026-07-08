using System.Collections.Generic;
using Operator.Data;
using Operator.Depth.Core;
using Operator.Missions.Core;
using UnityEngine;

namespace Operator.Missions
{
    /// <summary>
    /// Roll + проверка достижимости: путь от гаража, бюджет топливо/заряд/отсек + запас.
    /// </summary>
    public static class MissionReachability
    {
        const int MaxAttempts = 64;

        static readonly Vector2Int[] Cardinals =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1)
        };

        public static bool TryResolve(
            MissionTemplate template,
            WorldSession session,
            WorldGenConfig worldGen,
            DroneLimitsConfig limits,
            int sectorSize,
            out ResolvedMission resolved)
        {
            resolved = default;

            if (template == null
                || session == null
                || worldGen == null
                || limits == null
                || sectorSize <= 0)
            {
                return false;
            }

            var world = new World(session.Seed, session.Radius, worldGen);
            var fuelBudget = limits.FuelMax * (1f - limits.MissionSafetyMargin);
            var chargeBudget = limits.ChargeMax * (1f - limits.MissionSafetyMargin);
            var cargoBudget = limits.CargoMax * (1f - limits.MissionSafetyMargin);
            var laserTier = session.LaserTier;
            var missionIndex = session.NextMissionIndex;

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var sector = MissionResolver.RollSector(template, session.Seed, missionIndex, attempt);
                Vector2Int target;

                if (template.MissionType == MissionType.MineClusters)
                {
                    if (!TryGetMatchingVein(
                            sector,
                            session.Seed,
                            session.Radius,
                            worldGen,
                            template.MineResource,
                            out var vein))
                    {
                        continue;
                    }

                    target = new Vector2Int(vein.AnchorX, vein.AnchorY);
                }
                else
                {
                    if (!TryRollScannerTarget(
                            template,
                            world,
                            sector,
                            session.Seed,
                            sectorSize,
                            missionIndex,
                            attempt,
                            laserTier,
                            out target))
                    {
                        continue;
                    }
                }

                if (!TryFindStandCell(
                        world,
                        target,
                        laserTier,
                        limits,
                        fuelBudget,
                        chargeBudget,
                        out _,
                        out var outboundFuel,
                        out var outboundCharge,
                        out var pathMoves))
                {
                    continue;
                }

                var returnFuel = pathMoves * limits.FuelPerCell;
                if (outboundFuel + returnFuel > fuelBudget || outboundCharge > chargeBudget)
                {
                    continue;
                }

                if (template.MissionType == MissionType.MineClusters)
                {
                    var rock = world.GetCell(world.WrapX(target.x), target.y);
                    var miningCharge = template.MineClusterCount
                        * limits.GetChargeForRockTier(RockLayers.LaserTier(rock.Rock));
                    var maxVeinCells = template.MineClusterCount * worldGen.ResourceClusterSizeMax;
                    var cargo = maxVeinCells * limits.CargoPerVeinCell;

                    if (outboundCharge + miningCharge > chargeBudget || cargo > cargoBudget)
                    {
                        continue;
                    }
                }

                resolved = new ResolvedMission(template, sector, target);
                return true;
            }

            return false;
        }

        static bool TryGetMatchingVein(
            SectorAddress sector,
            int worldSeed,
            int worldRadius,
            WorldGenConfig config,
            ResourceType resource,
            out ResourceSectorVein vein)
        {
            vein = default;

            if (!ResourceClusterGenerator.TryGetSectorVein(
                    sector.BlockX,
                    sector.BlockY,
                    worldSeed,
                    worldRadius,
                    config,
                    out vein))
            {
                return false;
            }

            return vein.Type == resource;
        }

        static bool TryFindStandCell(
            World world,
            Vector2Int target,
            int laserTier,
            DroneLimitsConfig limits,
            float fuelBudget,
            float chargeBudget,
            out Vector2Int standCell,
            out float outboundFuel,
            out float outboundCharge,
            out int pathMoves)
        {
            standCell = default;
            outboundFuel = 0f;
            outboundCharge = 0f;
            pathMoves = 0;

            var goals = new List<Vector2Int>();
            CollectStandGoals(world, target, laserTier, goals);
            if (goals.Count == 0)
            {
                return false;
            }

            var goalSet = new HashSet<Vector2Int>(goals);
            var starts = GetGarageStartCells(world);
            if (starts.Count == 0)
            {
                return false;
            }

            var best = new Dictionary<Vector2Int, (float fuel, float charge, int moves)>();
            var open = new List<Vector2Int>();

            foreach (var start in starts)
            {
                best[start] = (0f, 0f, 0);
                open.Add(start);
            }

            while (open.Count > 0)
            {
                var pick = SelectNextOpenIndex(open, best, goals);
                var current = open[pick];
                open.RemoveAt(pick);
                var state = best[current];

                if (goalSet.Contains(current))
                {
                    standCell = current;
                    outboundFuel = state.fuel;
                    outboundCharge = state.charge;
                    pathMoves = state.moves;
                    return true;
                }

                for (var d = 0; d < Cardinals.Length; d++)
                {
                    var next = StepCell(current, Cardinals[d], world);
                    if (!TryMoveCost(world, next, laserTier, limits, state, out var nextState))
                    {
                        continue;
                    }

                    var roundTripFuel = nextState.fuel + nextState.moves * limits.FuelPerCell;
                    if (nextState.charge > chargeBudget || roundTripFuel > fuelBudget)
                    {
                        continue;
                    }

                    if (!best.TryGetValue(next, out var existing)
                        || Score((existing.fuel, existing.charge, existing.moves)) > Score(nextState))
                    {
                        best[next] = nextState;
                        if (!open.Contains(next))
                        {
                            open.Add(next);
                        }
                    }
                }
            }

            return false;
        }

        static float Score((float fuel, float charge, int moves) state) => state.fuel + state.charge;

        static int SelectNextOpenIndex(
            List<Vector2Int> open,
            Dictionary<Vector2Int, (float fuel, float charge, int moves)> best,
            List<Vector2Int> goals)
        {
            var pick = 0;
            var bestRank = Rank(open[0], best, goals);
            for (var i = 1; i < open.Count; i++)
            {
                var rank = Rank(open[i], best, goals);
                if (rank < bestRank)
                {
                    bestRank = rank;
                    pick = i;
                }
            }

            return pick;
        }

        static float Rank(
            Vector2Int cell,
            Dictionary<Vector2Int, (float fuel, float charge, int moves)> best,
            List<Vector2Int> goals)
        {
            var state = best[cell];
            return Score(state) + MinManhattanToGoals(cell, goals);
        }

        static int MinManhattanToGoals(Vector2Int cell, List<Vector2Int> goals)
        {
            var min = int.MaxValue;
            for (var i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var dist = Mathf.Abs(cell.x - goal.x) + Mathf.Abs(cell.y - goal.y);
                if (dist < min)
                {
                    min = dist;
                }
            }

            return min;
        }

        static bool TryMoveCost(
            World world,
            Vector2Int next,
            int laserTier,
            DroneLimitsConfig limits,
            (float fuel, float charge, int moves) from,
            out (float fuel, float charge, int moves) result)
        {
            result = default;

            if (next.y < 1 || next.y > world.WorldRadius || !GarageBounds.CanMoveTo(next.x, next.y))
            {
                return false;
            }

            var x = world.WrapX(next.x);
            var cell = world.GetCell(x, next.y);

            if (cell.Rock == RockType.Core)
            {
                return false;
            }

            var fuel = from.fuel;
            var charge = from.charge;

            if (cell.IsSolid)
            {
                if (!DrillRules.CanBreakCell(next, world, laserTier))
                {
                    return false;
                }

                charge += limits.GetChargeForRockTier(RockLayers.LaserTier(cell.Rock));
                fuel += limits.FuelPerCell * limits.DrillFuelMultiplier;
            }
            else
            {
                fuel += limits.FuelPerCell;
            }

            result = (fuel, charge, from.moves + 1);
            return true;
        }

        static bool TryRollScannerTarget(
            MissionTemplate template,
            World world,
            SectorAddress sector,
            int worldSeed,
            int sectorSize,
            int missionIndex,
            int attempt,
            int laserTier,
            out Vector2Int targetSolid)
        {
            targetSolid = default;

            for (var sub = 0; sub < 16; sub++)
            {
                var cell = MissionResolver.RollTargetCell(
                    template,
                    sector,
                    worldSeed,
                    sectorSize,
                    missionIndex,
                    attempt * 16 + sub);

                if (cell.y < 1
                    || cell.y > world.WorldRadius
                    || GarageBounds.Contains(cell.x, cell.y)
                    || GarageBounds.IsSurfaceProtected(cell.y))
                {
                    continue;
                }

                var data = world.GetCell(world.WrapX(cell.x), cell.y);
                if (!data.IsSolid)
                {
                    continue;
                }

                if (!HasStandGoal(world, cell, laserTier))
                {
                    continue;
                }

                targetSolid = cell;
                return true;
            }

            return false;
        }

        static bool HasStandGoal(World world, Vector2Int targetSolid, int laserTier)
        {
            for (var d = 0; d < Cardinals.Length; d++)
            {
                var stand = StepCell(targetSolid, Cardinals[d], world);
                if (IsStandGoal(world, stand, laserTier))
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsStandGoal(World world, Vector2Int stand, int laserTier)
        {
            if (stand.y < 1 || stand.y > world.WorldRadius || !GarageBounds.CanMoveTo(stand.x, stand.y))
            {
                return false;
            }

            var cell = world.GetCell(world.WrapX(stand.x), stand.y);
            if (cell.Rock == RockType.Core)
            {
                return false;
            }

            return !cell.IsSolid || DrillRules.CanBreakCell(stand, world, laserTier);
        }

        static void CollectStandGoals(
            World world,
            Vector2Int target,
            int laserTier,
            List<Vector2Int> goals)
        {
            for (var d = 0; d < Cardinals.Length; d++)
            {
                var stand = StepCell(target, Cardinals[d], world);
                if (IsStandGoal(world, stand, laserTier))
                {
                    goals.Add(stand);
                }
            }
        }

        static List<Vector2Int> GetGarageStartCells(World world)
        {
            var starts = new List<Vector2Int>(3);
            TryAddAirStart(world, GarageBounds.SpawnCell, starts);
            TryAddAirStart(world, GarageBounds.SecondCell, starts);
            TryAddAirStart(world, GarageBounds.ExitCell, starts);
            return starts;
        }

        static void TryAddAirStart(World world, Vector2Int cell, List<Vector2Int> starts)
        {
            if (!GarageBounds.CanMoveTo(cell.x, cell.y))
            {
                return;
            }

            if (!world.GetCell(world.WrapX(cell.x), cell.y).IsSolid)
            {
                starts.Add(cell);
            }
        }

        static Vector2Int StepCell(Vector2Int cell, Vector2Int dir, World world) =>
            new(world.WrapX(cell.x + dir.x), cell.y + dir.y);
    }
}
