using System.Collections.Generic;
using UnityEngine;

namespace Operator.Depth.Core
{
    public class Chunk
    {
        public const int Size = 16;

        public Vector2Int ChunkCoord { get; }

        readonly CellData?[,] _cells = new CellData?[Size, Size];

        public Chunk(Vector2Int chunkCoord)
        {
            ChunkCoord = chunkCoord;
        }

        public bool TryGetLocal(int localX, int localY, out CellData cell)
        {
            cell = default;
            if (!IsInside(localX, localY)) return false;

            var stored = _cells[localX, localY];
            if (stored == null) return false;

            cell = stored.Value;
            return true;
        }

        public void SetLocal(int localX, int localY, CellData cell)
        {
            if (!IsInside(localX, localY)) return;
            cell.Modified = true;
            _cells[localX, localY] = cell;
        }

        public static Vector2Int WorldToChunk(int worldX, int worldY)
        {
            return new Vector2Int(
                FloorDiv(worldX, Size),
                FloorDiv(worldY, Size));
        }

        public static void WorldToLocal(int worldX, int worldY, out int localX, out int localY)
        {
            localX = Mod(worldX, Size);
            localY = Mod(worldY, Size);
        }

        static bool IsInside(int x, int y) =>
            x >= 0 && x < Size && y >= 0 && y < Size;

        static int FloorDiv(int a, int b)
        {
            return a >= 0 ? a / b : (a - b + 1) / b;
        }

        static int Mod(int a, int b)
        {
            var r = a % b;
            return r < 0 ? r + b : r;
        }
    }
}
