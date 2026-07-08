using System.Collections.Generic;
using Operator.Data;
using UnityEngine;

namespace Operator.Depth.Core
{
    public class World
    {
        readonly Dictionary<Vector2Int, Chunk> _chunks = new();
        readonly WorldGenerator _generator;
        readonly int _worldRadius;

        public int WorldRadius => _worldRadius;

        public World(int worldSeed, int worldRadius, WorldGenConfig config)
        {
            _worldRadius = worldRadius;
            _generator = new WorldGenerator(worldSeed, config);
        }

        public CellData GetCell(int x, int y)
        {
            y = Mathf.Clamp(y, 0, _worldRadius);

            var chunkCoord = Chunk.WorldToChunk(x, y);
            var chunk = GetOrCreateChunk(chunkCoord);

            Chunk.WorldToLocal(x, y, out var localX, out var localY);

            if (chunk.TryGetLocal(localX, localY, out var cell))
                return cell;

            cell = _generator.Generate(x, y, _worldRadius);
            chunk.SetLocal(localX, localY, cell);
            return cell;
        }

        public void SetCell(int x, int y, CellData cell)
        {
            var chunkCoord = Chunk.WorldToChunk(x, y);
            var chunk = GetOrCreateChunk(chunkCoord);
            Chunk.WorldToLocal(x, y, out var localX, out var localY);
            chunk.SetLocal(localX, localY, cell);
        }

        public int WrapX(int x)
        {
            if (x > _worldRadius) return -_worldRadius;
            if (x < -_worldRadius) return _worldRadius;
            return x;
        }

        Chunk GetOrCreateChunk(Vector2Int chunkCoord)
        {
            if (!_chunks.TryGetValue(chunkCoord, out var chunk))
            {
                chunk = new Chunk(chunkCoord);
                _chunks[chunkCoord] = chunk;
            }

            return chunk;
        }
    }
}
