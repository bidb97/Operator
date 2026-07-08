using UnityEngine;

namespace Operator.Depth.Core
{
    public static class WormRules
    {
        public static bool CanOccupy(int x, int y, int worldRadius)
        {
            if (x < -worldRadius || x > worldRadius)
            {
                return false;
            }

            if (y < 1 || y >= worldRadius)
            {
                return false;
            }

            if (GarageBounds.Contains(x, y))
            {
                return false;
            }

            return true;
        }

        public static bool InSpawnBlock(int x, int y, int blockX, int blockY, int spacing)
        {
            if (spacing <= 0)
            {
                return true;
            }

            var minX = blockX * spacing;
            var maxX = minX + spacing - 1;
            var minY = blockY * spacing;
            var maxY = minY + spacing - 1;
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }

        public static int WrapX(int x, int worldRadius)
        {
            if (x > worldRadius)
            {
                return -worldRadius;
            }

            if (x < -worldRadius)
            {
                return worldRadius;
            }

            return x;
        }
    }
}
