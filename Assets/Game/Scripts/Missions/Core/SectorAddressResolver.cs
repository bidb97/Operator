using UnityEngine;

namespace Operator.Missions.Core
{
    public static class SectorAddressResolver
    {
        public static int LateralBlock(SectorLateralSide side, int distance)
        {
            var d = Mathf.Max(0, distance);
            return side switch
            {
                SectorLateralSide.Left => -d,
                SectorLateralSide.Right => d,
                _ => 0
            };
        }
    }
}
