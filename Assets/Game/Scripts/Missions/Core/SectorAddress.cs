using Operator.Depth.Core;
using UnityEngine;

namespace Operator.Missions.Core
{
    /// <summary>
    /// Адрес сектора для HUD, миссий и журнала. См. TECH.md → «Секторы».
    /// Размер сектора = <see cref="ResourceClusterSpacing"/> в WorldGenConfig (100×100).
    /// </summary>
    public readonly struct SectorAddress
    {
        public int BlockX { get; }
        public int BlockY { get; }

        /// <summary>D — номер сектора по глубине (0 у поверхности).</summary>
        public int Depth => BlockY;

        /// <summary>0 под стволом; &lt;0 влево; &gt;0 вправо (в секторах).</summary>
        public int LateralBlock => BlockX;

        public SectorAddress(int blockX, int blockY)
        {
            BlockX = blockX;
            BlockY = blockY;
        }

        public static SectorAddress FromCell(int cellX, int cellY, int sectorSize)
        {
            return new SectorAddress(
                SectorGrid.LateralBlockIndex(cellX, sectorSize),
                SectorGrid.DepthBlockIndex(cellY, sectorSize));
        }

        public static SectorAddress FromCell(Vector2Int cell, int sectorSize) =>
            FromCell(cell.x, cell.y, sectorSize);

        public bool ContainsCell(int cellX, int cellY, int sectorSize)
        {
            GetCellRect(sectorSize, out var xMin, out var xMax, out var yMin, out var yMax);
            return cellX >= xMin && cellX <= xMax && cellY >= yMin && cellY <= yMax;
        }

        public void GetCellRect(int sectorSize, out int xMin, out int xMax, out int yMin, out int yMax)
        {
            SectorGrid.GetLateralRect(BlockX, sectorSize, out xMin, out xMax);
            SectorGrid.GetDepthRect(BlockY, sectorSize, out yMin, out yMax);
        }

        /// <summary>Для игрока: <c>D15 R8</c>, <c>D7 L4</c>, <c>D7</c>.</summary>
        public string ToDisplayString()
        {
            if (BlockX == 0)
            {
                return $"D{Depth}";
            }

            if (BlockX < 0)
            {
                return $"D{Depth} L{-BlockX}";
            }

            return $"D{Depth} R{BlockX}";
        }

        /// <summary>Компактный id для save / журнала: <c>D15R8</c>, <c>D7L4</c>, <c>D7</c>.</summary>
        public string ToCompactId()
        {
            if (BlockX == 0)
            {
                return $"D{Depth}";
            }

            if (BlockX < 0)
            {
                return $"D{Depth}L{-BlockX}";
            }

            return $"D{Depth}R{BlockX}";
        }

        public static bool TryParseCompactId(string id, out SectorAddress address)
        {
            address = default;

            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            id = id.Replace(" ", string.Empty).ToUpperInvariant();

            if (!id.StartsWith('D'))
            {
                return false;
            }

            var lateralIndex = id.IndexOfAny(new[] { 'L', 'R' });
            if (lateralIndex < 0)
            {
                if (!int.TryParse(id[1..], out var depthOnly))
                {
                    return false;
                }

                address = new SectorAddress(0, depthOnly);
                return true;
            }

            if (!int.TryParse(id[1..lateralIndex], out var depth))
            {
                return false;
            }

            var lateralChar = id[lateralIndex];
            if (!int.TryParse(id[(lateralIndex + 1)..], out var lateral))
            {
                return false;
            }

            var blockX = lateralChar == 'L' ? -lateral : lateral;
            address = new SectorAddress(blockX, depth);
            return true;
        }

        public override string ToString() => ToDisplayString();
    }
}
