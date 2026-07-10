using System.Collections;
using Operator.Data;
using Operator.Missions.Core;
using UnityEngine;

namespace Operator.Missions
{
    /// <summary>Автовыдача миссий VIYA: без кнопки «принять».</summary>
    public static class MissionIssuer
    {
        public sealed class ResolveResult
        {
            public bool Success;
            public ActiveMission Mission;
        }

        public static bool TryIssueInstantNext(
            WorldSession session,
            MissionCatalog catalog,
            int sectorSize)
        {
            if (session?.ActiveMission != null)
                return false;

            if (!TryGetTemplate(catalog, session.NextMissionIndex, out var template, out var index))
                return false;

            if (MissionRules.RequiresReachability(template.MissionType))
                return false;

            if (!TryResolveInstant(session, template, sectorSize, index, out var active))
                return false;

            session.ActiveMission = active;
            LogMissionIssued(active);
            return true;
        }

        public static bool TryIssueNext(
            WorldSession session,
            MissionCatalog catalog,
            WorldGenConfig worldGen,
            DroneLimitsConfig limits,
            int sectorSize,
            out ActiveMission active)
        {
            active = null;

            if (!TryGetTemplate(catalog, session?.NextMissionIndex ?? -1, out var template, out var index))
                return false;

            if (session.ActiveMission != null)
                return false;

            if (!MissionRules.RequiresReachability(template.MissionType))
            {
                if (!TryResolveInstant(session, template, sectorSize, index, out active))
                    return false;

                session.ActiveMission = active;
                LogMissionIssued(active);
                return true;
            }

            if (!MissionReachability.TryResolve(
                    template,
                    session,
                    worldGen,
                    limits,
                    sectorSize,
                    out var resolved,
                    index)
                || resolved.Template == null)
            {
                Debug.LogWarning(
                    $"[MissionIssuer] Миссия НЕ выдана: id={template.TemplateId}, type={template.MissionType}, " +
                    $"исчерпано {MissionReachability.MaxAttempts} попыток, seed={session.Seed}, R={session.Radius}",
                    catalog);
                return false;
            }

            if (!ApplyResolved(session, resolved, out active))
                return false;

            LogMissionIssued(active);
            return true;
        }

        public static IEnumerator TryIssueNextAsync(
            WorldSession session,
            MissionCatalog catalog,
            WorldGenConfig worldGen,
            DroneLimitsConfig limits,
            int sectorSize,
            System.Action<float> onAttemptProgress = null)
        {
            var result = new ResolveResult();
            yield return ResolveForIndexAsync(
                session,
                catalog,
                session.NextMissionIndex,
                worldGen,
                limits,
                sectorSize,
                result,
                onAttemptProgress);

            if (!result.Success || result.Mission == null)
                yield break;

            session.ActiveMission = result.Mission;
            LogMissionIssued(result.Mission);
        }

        public static IEnumerator ResolveForIndexAsync(
            WorldSession session,
            MissionCatalog catalog,
            int catalogIndex,
            WorldGenConfig worldGen,
            DroneLimitsConfig limits,
            int sectorSize,
            ResolveResult result,
            System.Action<float> onAttemptProgress = null)
        {
            result.Success = false;
            result.Mission = null;

            if (!TryGetTemplate(catalog, catalogIndex, out var template, out _))
            {
                Debug.Log($"[MissionIssuer] Resolve: нет шаблона index={catalogIndex}");
                yield break;
            }

            if (!MissionRules.RequiresReachability(template.MissionType))
            {
                if (TryResolveInstant(session, template, sectorSize, catalogIndex, out var instant))
                {
                    result.Success = true;
                    result.Mission = instant;
                    if (instant.Type == MissionType.MineClusters)
                        RevealVeinSector(session, instant.TargetSector);
                }

                yield break;
            }

            Debug.Log(
                $"[MissionIssuer] Поиск цели: id={template.TemplateId}, type={template.MissionType}, " +
                $"index={catalogIndex}, до {MissionReachability.MaxAttempts} попыток, " +
                $"seed={session.Seed}, R={session.Radius}");

            var outcome = new MissionReachability.ResolveOutcome();
            yield return MissionReachability.TryResolveAsync(
                template,
                session,
                worldGen,
                limits,
                sectorSize,
                outcome,
                onAttemptProgress,
                catalogIndex);

            if (!outcome.Success || outcome.Resolved.Template == null)
            {
                Debug.LogWarning(
                    $"[MissionIssuer] Миссия НЕ выдана: id={template.TemplateId}, type={template.MissionType}, " +
                    $"index={catalogIndex}, исчерпано {MissionReachability.MaxAttempts} попыток, " +
                    $"seed={session.Seed}, R={session.Radius}",
                    catalog);
                yield break;
            }

            result.Success = true;
            result.Mission = MissionResolver.ToActiveMission(outcome.Resolved);
            if (result.Mission.Type == MissionType.MineClusters)
                RevealVeinSector(session, result.Mission.TargetSector);
        }

        static bool TryResolveInstant(
            WorldSession session,
            MissionTemplate template,
            int sectorSize,
            int catalogIndex,
            out ActiveMission active)
        {
            active = null;
            var resolved = MissionResolver.Resolve(template, session.Seed, sectorSize, catalogIndex);
            if (resolved.Template == null)
                return false;

            active = MissionResolver.ToActiveMission(resolved);
            if (active != null && active.SpeakerAvatar == null && template.Speaker != null)
                active.SpeakerAvatar = template.Speaker.Avatar;

            return active != null;
        }

        static bool TryGetTemplate(
            MissionCatalog catalog,
            int catalogIndex,
            out MissionTemplate template,
            out int index)
        {
            template = null;
            index = catalogIndex;

            if (catalog == null || catalogIndex < 0)
                return false;

            template = catalog.Get(catalogIndex);
            return template != null;
        }

        static void LogMissionIssued(ActiveMission active)
        {
            Debug.Log(
                $"[MissionIssuer] Миссия выдана: id={active.TemplateId}, type={active.Type}, " +
                $"sector={active.TargetSectorDisplay}, cell=({active.TargetCellX},{active.TargetCellY})");
        }

        static bool ApplyResolved(
            WorldSession session,
            ResolvedMission resolved,
            out ActiveMission active)
        {
            active = MissionResolver.ToActiveMission(resolved);
            session.ActiveMission = active;

            if (active.Type == MissionType.MineClusters)
                RevealVeinSector(session, active.TargetSector);

            return true;
        }

        public static void RevealVeinSectorPublic(WorldSession session, SectorAddress sector) =>
            RevealVeinSector(session, sector);

        static void RevealVeinSector(WorldSession session, SectorAddress sector)
        {
            if (session.RevealedVeinSectors == null)
                session.RevealedVeinSectors = new();

            foreach (var existing in session.RevealedVeinSectors)
            {
                if (existing.BlockX == sector.BlockX && existing.BlockY == sector.BlockY)
                    return;
            }

            session.RevealedVeinSectors.Add(sector);
        }

        public static void CompleteActive(WorldSession session)
        {
            if (session?.ActiveMission == null)
                return;

            session.ActiveMission = null;
            session.NextMissionIndex++;
        }
    }
}
