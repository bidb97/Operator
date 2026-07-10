namespace Operator.Missions.Core
{
    public static class MissionRules
    {
        /// <summary>Миссии без pathfind — выдаются сразу (roll сектора/цели).</summary>
        public static bool RequiresReachability(MissionType type) =>
            type is MissionType.InstallScanner or MissionType.MineClusters;
    }
}
