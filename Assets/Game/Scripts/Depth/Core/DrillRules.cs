using UnityEngine;

namespace Operator.Depth.Core
{
    public static class DrillRules
    {
        public const float DefaultDrillDuration = 1f;

        /// <summary>Можно ли бурить соседнюю клетку to из from. Заглушка: tвёрдая порода (не Air, не Core).</summary>
        public static bool CanDrillCell(Vector2Int from, Vector2Int to, World world)
        {
            if (world == null)
            {
                return false;
            }

            if (GarageBounds.IsSurfaceProtected(to.y)
                || !GarageBounds.CanMoveTo(to.x, to.y)
                || !GarageBounds.AllowsMove(from, to))
            {
                return false;
            }

            if (to.y < 0 || to.y > world.WorldRadius)
            {
                return false;
            }

            var x = world.WrapX(to.x);
            return world.GetCell(x, to.y).IsSolid;
        }

        /// <summary>Хватает ли тира лазера, чтобы пробить клетку to (по твёрдости породы).</summary>
        public static bool CanBreakCell(Vector2Int to, World world, int laserTier)
        {
            if (world == null || to.y < 0 || to.y > world.WorldRadius)
            {
                return false;
            }

            var x = world.WrapX(to.x);
            var cell = world.GetCell(x, to.y);
            return cell.IsSolid && RockLayers.LaserTier(cell.Rock) <= laserTier;
        }

        public static bool TryClearCell(Vector2Int cell, World world)
        {
            if (world == null)
            {
                return false;
            }

            if (GarageBounds.IsSurfaceProtected(cell.y)
                || cell.y < 0
                || cell.y > world.WorldRadius)
            {
                return false;
            }

            var x = world.WrapX(cell.x);
            var existing = world.GetCell(x, cell.y);
            if (!existing.IsSolid)
            {
                return false;
            }

            world.SetCell(x, cell.y, new CellData
            {
                Rock = RockType.Air,
                Resource = ResourceType.None,
                Modified = true
            });
            return true;
        }
    }
}
