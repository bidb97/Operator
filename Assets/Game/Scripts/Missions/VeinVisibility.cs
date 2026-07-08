using Operator.Data;
using Operator.Missions.Core;

namespace Operator.Missions
{
    public static class VeinVisibility
    {
        public static bool IsCellRevealed(int cellX, int cellY, WorldSession session, int sectorSize)
        {
            if (session?.RevealedVeinSectors == null || session.RevealedVeinSectors.Count == 0)
            {
                return false;
            }

            var sector = SectorAddress.FromCell(cellX, cellY, sectorSize);
            foreach (var revealed in session.RevealedVeinSectors)
            {
                if (revealed.BlockX == sector.BlockX && revealed.BlockY == sector.BlockY)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
