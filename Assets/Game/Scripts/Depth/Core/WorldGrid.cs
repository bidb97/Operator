using UnityEngine;

namespace Operator.Depth.Core
{
    /// <summary>
    /// Единственный источник правды: logical cell (X, Y) ↔ world. Y вниз = глубина.
    /// После <see cref="Bind"/> — через Unity Grid (Depth/Grid/Tilemap на сцене, transform (0,0,0)).
    /// Контракт (не дублировать offset'ами в сцене и не считать world вручную):
    /// — клетка X занимает world X … X+1; <see cref="CellToWorld"/> = левый край;
    /// — <see cref="CellCenterToWorld"/> = центр клетки;
    /// — Tilemap TileAnchor на сцене = (<see cref="SceneTileAnchorX"/>, <see cref="SceneTileAnchorY"/>);
    /// — дрон: X = левый край, Y = центр (<see cref="DroneCellToWorld"/>).
    /// </summary>
    public static class WorldGrid
    {
        public enum GarageSlot
        {
            Spawn,
            Second
        }

        public const float CellSize = 1f;
        public const float HalfCell = CellSize * 0.5f;

        /// <summary>Tilemap TileAnchor.x на Depth/Grid/Tilemap — должен совпадать.</summary>
        public const float SceneTileAnchorX = 0f;

        /// <summary>Tilemap TileAnchor.y — центр тайла по вертикали при CellSize=1.</summary>
        public const float SceneTileAnchorY = HalfCell;

        /// <summary>Y-сдвиг дрона в стволе (GarageBounds.SpawnCell).</summary>
        public const float GarageSpawnYOffset = HalfCell;

        const float GarageSlotEpsilon = 0.05f;

        static Grid _grid;

        public static void Bind(Grid grid)
        {
            _grid = grid;
        }

        public static Vector3 CellToWorld(Vector2Int cell)
        {
            if (_grid != null)
            {
                return _grid.CellToWorld(ToTilemapCell(cell));
            }

            return new Vector3(cell.x, -cell.y, 0f);
        }

        public static Vector2Int WorldToCell(Vector3 world)
        {
            if (_grid != null)
            {
                var tileCell = _grid.WorldToCell(world);
                return new Vector2Int(tileCell.x, -tileCell.y);
            }

            return new Vector2Int(
                Mathf.FloorToInt(world.x),
                Mathf.FloorToInt(-world.y));
        }

        /// <summary>Дрон: X — левый край клетки (как тайлы); Y — центр; спавн в стволе −<see cref="GarageSpawnYOffset"/>.</summary>
        public static Vector3 DroneCellToWorld(Vector2Int cell)
        {
            var corner = CellToWorld(cell);
            var pos = new Vector3(corner.x, CellCenterToWorld(cell).y, corner.z);

            if (cell.x == GarageBounds.ShaftX && cell.y == GarageBounds.TopY)
            {
                pos.y -= GarageSpawnYOffset;
            }

            return pos;
        }

        public static Vector3 CellCenterToWorld(Vector2Int cell)
        {
            if (_grid != null)
            {
                return _grid.GetCellCenterWorld(ToTilemapCell(cell));
            }

            return new Vector3(cell.x + HalfCell, -cell.y - HalfCell, 0f);
        }

        public static Vector3 GarageSpawnWorld() => DroneCellToWorld(GarageBounds.SpawnCell);

        public static Vector3 GarageSecondWorld() => DroneCellToWorld(GarageBounds.SecondCell);

        public static bool TryGetGarageSlot(Vector3 world, out GarageSlot slot)
        {
            slot = default;

            var spawn = GarageSpawnWorld();
            if (Mathf.Abs(world.x - spawn.x) > GarageSlotEpsilon)
            {
                return false;
            }

            var second = GarageSecondWorld();

            if (Mathf.Abs(world.y - spawn.y) < GarageSlotEpsilon)
            {
                slot = GarageSlot.Spawn;
                return true;
            }

            if (Mathf.Abs(world.y - second.y) < GarageSlotEpsilon)
            {
                slot = GarageSlot.Second;
                return true;
            }

            return false;
        }

        /// <summary>Клетка впереди по facing (probe на 1 cell).</summary>
        public static Vector2Int GetFacingCell(Vector3 world, float facingAngleDeg)
        {
            var dir = FacingAngleToWorldDelta(facingAngleDeg);
            return WorldToCell(world + dir * CellSize);
        }

        public static Vector3Int ToTilemapCell(Vector2Int cell)
        {
            return new Vector3Int(cell.x, -cell.y, 0);
        }

        public static Vector3 FacingAngleToWorldDelta(float angleDeg)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(angleDeg, 0f)) < 0.01f)
            {
                return new Vector3(0f, -CellSize, 0f);
            }

            if (Mathf.Abs(Mathf.DeltaAngle(angleDeg, 180f)) < 0.01f)
            {
                return new Vector3(0f, CellSize, 0f);
            }

            if (Mathf.Abs(Mathf.DeltaAngle(angleDeg, 90f)) < 0.01f)
            {
                return new Vector3(CellSize, 0f, 0f);
            }

            return new Vector3(-CellSize, 0f, 0f);
        }
    }
}
