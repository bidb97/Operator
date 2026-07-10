using System.Collections;
using Operator.Bootstrap;
using Operator.Data;
using Operator.Managers;
using Operator.Missions.Core;
using UnityEngine;

namespace Operator.Missions
{
    /// <summary>
    /// Фоновая выдача и предзагрузка миссий после старта уровня (не блокирует лоадер).
    /// </summary>
    public class MissionPreloader : MonoBehaviour
    {
        MissionCatalog _catalog;
        Coroutine _loop;
        bool _resolving;

        public void Begin(MissionCatalog catalog)
        {
            _catalog = catalog;

            if (_loop != null)
                StopCoroutine(_loop);

            Debug.Log("[MissionPreloader] Старт фоновой выдачи миссий");
            _loop = StartCoroutine(Run());
        }

        public void StopPreload()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }
        }

        IEnumerator Run()
        {
            while (true)
            {
                yield return Tick();
                yield return null;
            }
        }

        IEnumerator Tick()
        {
            if (_resolving || _catalog == null)
                yield break;

            var bootstrap = GameBootstrap.Instance;
            var session = bootstrap?.Session;
            if (session == null)
                yield break;

            var worldGen = bootstrap.WorldGen;
            var limits = bootstrap.DroneLimits;
            var sectorSize = worldGen != null ? worldGen.ResourceClusterSpacing : 0;
            if (worldGen == null || limits == null || sectorSize <= 0)
                yield break;

            if (TryApplyPreloaded(session))
                yield break;

            var targetIndex = session.ActiveMission != null
                ? session.NextMissionIndex + 1
                : session.NextMissionIndex;

            if (targetIndex < 0 || _catalog.Get(targetIndex) == null)
                yield break;

            if (session.PreloadedForIndex == targetIndex && session.PreloadedMission != null)
                yield break;

            if (session.ActiveMission == null
                && targetIndex == session.NextMissionIndex
                && !MissionRules.RequiresReachability(_catalog.Get(targetIndex).MissionType))
            {
                yield break;
            }

            _resolving = true;

            var result = new MissionIssuer.ResolveResult();
            yield return MissionIssuer.ResolveForIndexAsync(
                session,
                _catalog,
                targetIndex,
                worldGen,
                limits,
                sectorSize,
                result);

            _resolving = false;

            if (!result.Success || result.Mission == null)
                yield break;

            if (session.ActiveMission == null && targetIndex == session.NextMissionIndex)
            {
                session.ActiveMission = result.Mission;
                Debug.Log(
                    $"[MissionPreloader] Миссия выдана: id={result.Mission.TemplateId}, index={targetIndex}");
                yield break;
            }

            session.PreloadedForIndex = targetIndex;
            session.PreloadedMission = result.Mission;
            Debug.Log(
                $"[MissionPreloader] Миссия предзагружена: id={result.Mission.TemplateId}, index={targetIndex}");
        }

        static bool TryApplyPreloaded(WorldSession session)
        {
            if (session.ActiveMission != null)
                return false;

            if (session.PreloadedMission == null || session.PreloadedForIndex != session.NextMissionIndex)
                return false;

            session.ActiveMission = session.PreloadedMission;
            session.PreloadedMission = null;
            session.PreloadedForIndex = -1;

            Debug.Log(
                $"[MissionPreloader] Предзагруженная миссия выдана: id={session.ActiveMission.TemplateId}, " +
                $"index={session.NextMissionIndex}");
            return true;
        }
    }
}
