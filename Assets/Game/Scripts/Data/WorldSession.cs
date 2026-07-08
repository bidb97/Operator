using System.Collections.Generic;
using Operator.Missions.Core;

namespace Operator.Data
{
    /// <summary>
    /// Сейв текущей игры (мир, прокачка, журнал). Сбрасывается при новой игре.
    /// Глобально: рекорд глубины и мёртвые дроны — отдельно (см. GDD).
    /// </summary>
    public class WorldSession
    {
        public int Seed;
        public int Radius;
        public int RunNumber;

        /// <summary>Текущий тир лазера. 1 — только Edge, растёт прокачкой.</summary>
        public int LaserTier = 1;

        /// <summary>Текущая миссия (null — нет активной).</summary>
        public ActiveMission ActiveMission;

        /// <summary>Индекс следующей миссии в <see cref="MissionCatalog"/>.</summary>
        public int NextMissionIndex;

        /// <summary>Секторы, где жилы видны игроку (миссия на добычу выдана).</summary>
        public List<SectorAddress> RevealedVeinSectors = new();
    }
}
