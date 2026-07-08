using Operator.Data;
using Operator.Missions.Core;
using UnityEngine;

namespace Operator.Missions
{
    /// <summary>Автовыдача миссий VIYA: без кнопки «принять».</summary>
    public static class MissionIssuer
    {
        public static bool TryIssueNext(
            WorldSession session,
            MissionCatalog catalog,
            WorldGenConfig worldGen,
            DroneLimitsConfig limits,
            int sectorSize,
            out ActiveMission active)
        {
            active = null;

            if (session == null || catalog == null || sectorSize <= 0)
            {
                return false;
            }

            if (session.ActiveMission != null)
            {
                return false;
            }

            var template = catalog.Get(session.NextMissionIndex);
            if (template == null)
            {
                return false;
            }

            if (!MissionReachability.TryResolve(
                    template,
                    session,
                    worldGen,
                    limits,
                    sectorSize,
                    out var resolved)
                || resolved.Template == null)
            {
                Debug.LogWarning(
                    $"{nameof(MissionIssuer)}: нет достижимой цели для миссии {template.TemplateId}.",
                    catalog);
                return false;
            }

            active = MissionResolver.ToActiveMission(resolved);
            session.ActiveMission = active;

            if (active.Type == MissionType.MineClusters)
            {
                RevealVeinSector(session, active.TargetSector);
            }

            return true;
        }

        static void RevealVeinSector(WorldSession session, SectorAddress sector)
        {
            if (session.RevealedVeinSectors == null)
            {
                session.RevealedVeinSectors = new();
            }

            foreach (var existing in session.RevealedVeinSectors)
            {
                if (existing.BlockX == sector.BlockX && existing.BlockY == sector.BlockY)
                {
                    return;
                }
            }

            session.RevealedVeinSectors.Add(sector);
        }

        public static void CompleteActive(WorldSession session)
        {
            if (session?.ActiveMission == null)
            {
                return;
            }

            session.ActiveMission = null;
            session.NextMissionIndex++;
        }
    }
}
