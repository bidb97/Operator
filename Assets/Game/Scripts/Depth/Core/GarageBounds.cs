using UnityEngine;

namespace Operator.Depth.Core
{
    public static class GarageBounds
    {
        public const int HalfWidth = 1;
        public const int TopY = 1;
        public const int Depth = 2;
        public const int OffsetX = 0;
        public const int BaseX = 0;

        /// <summary>Колонка ствола у Ярмо — единственная проезжая в гараже.</summary>
        public const int ShaftX = 0;

        public static int CenterX => BaseX + OffsetX;

        public static int SecondY => TopY + 1;

        public static Vector2Int SpawnCell => new(ShaftX, TopY);

        public static Vector2Int SecondCell => new(ShaftX, SecondY);

        public static Vector2Int ExitCell => new(ShaftX, TopY + Depth);

        public static int WidthCells => HalfWidth * 2 + 1;

        /// <summary>Левый нижний угол спрайта гаража (3×2 клетки) + визуальная подгонка.</summary>
        public static Vector3 SpriteBottomLeftWorld()
        {
            var cell = new Vector2Int(CenterX - HalfWidth, TopY + Depth - 1);
            var corner = WorldGrid.CellToWorld(cell);
            var bottomLeft = new Vector3(corner.x, corner.y - WorldGrid.CellSize, corner.z);
            return bottomLeft + new Vector3(-WorldGrid.HalfCell, WorldGrid.CellSize, 0f);
        }

        /// <summary>Поверхность и спавн — не бурить (y 0 и 1).</summary>
        public static bool IsSurfaceProtected(int y) => y <= TopY;

        /// <summary>Клетка для ресурсов — те же ограничения по глубине и зоне, что и для бурения.</summary>
        public static bool CanHostResource(int x, int y, int worldRadius) =>
            !IsSurfaceProtected(y)
            && y < worldRadius
            && !Contains(x, y);

        public static bool Contains(int x, int y)
        {
            if (y < TopY || y >= TopY + Depth)
            {
                return false;
            }

            return x >= CenterX - HalfWidth && x <= CenterX + HalfWidth;
        }

        public static bool IsShaft(int x, int y) => Contains(x, y) && x == ShaftX;

        /// <summary>Воздух сбоку от ствола — только графика, не проезд.</summary>
        public static bool CanOccupy(int x, int y)
        {
            if (Contains(x, y) && x != ShaftX)
            {
                return false;
            }

            return true;
        }

        /// <summary>Потолок по глубине: world Y &lt; TopY на любом X — ехать нельзя (поверхность / база позже).</summary>
        public static bool IsAboveSpawnDepth(int worldY) => worldY < TopY;

        public static bool CanMoveTo(int x, int y)
        {
            if (IsAboveSpawnDepth(y))
            {
                return false;
            }

            if (!CanOccupy(x, y))
            {
                return false;
            }

            return true;
        }

        /// <summary>В гараже — только вверх/вниз по стволу.</summary>
        public static bool AllowsMove(Vector2Int from, Vector2Int to)
        {
            if (!IsShaft(from.x, from.y))
            {
                return true;
            }

            var deltaY = to.y - from.y;
            return to.x == from.x && deltaY is 1 or -1;
        }
    }
}
