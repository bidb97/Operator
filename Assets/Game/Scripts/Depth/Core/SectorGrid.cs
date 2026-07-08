using UnityEngine;

namespace Operator.Depth.Core
{
    /// <summary>
    /// Сетка секторов 100×100: X центрирован на стволе (0), Y от поверхности вниз.
    /// См. TECH.md → «Секторы».
    /// </summary>
    public static class SectorGrid
    {
        public static int LateralBlockIndex(int cellX, int sectorSize) =>
            Mathf.FloorToInt((cellX + sectorSize * 0.5f) / sectorSize);

        public static int DepthBlockIndex(int cellY, int sectorSize) =>
            Mathf.FloorToInt((float)cellY / sectorSize);

        public static int LateralBlockOrigin(int blockX, int sectorSize) =>
            blockX * sectorSize - sectorSize / 2;

        public static int DepthBlockOrigin(int blockY, int sectorSize) =>
            blockY * sectorSize;

        public static void GetLateralRect(int blockX, int sectorSize, out int xMin, out int xMax)
        {
            xMin = LateralBlockOrigin(blockX, sectorSize);
            xMax = blockX * sectorSize + sectorSize / 2 - 1;
        }

        public static void GetDepthRect(int blockY, int sectorSize, out int yMin, out int yMax)
        {
            yMin = DepthBlockOrigin(blockY, sectorSize);
            yMax = (blockY + 1) * sectorSize - 1;
        }
    }
}
