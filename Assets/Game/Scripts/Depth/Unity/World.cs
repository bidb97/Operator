using System.Collections.Generic;
using Operator.Bootstrap;
using Operator.Depth.Core;
using Operator.Managers;
using Operator.Missions;
using UnityEngine;
using UnityEngine.Tilemaps;
using CoreWorld = Operator.Depth.Core.World;

namespace Operator.Depth.Unity
{
    /// <summary>
    /// Читает World и рисует Tilemap. id тайлов — через AssetManager.
    /// Grid + Tilemap — только из сцены (child Depth).
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class DepthWorld : MonoBehaviour
    {
        const string DepthPackName = "Depth";

        [SerializeField] Tilemap tilemap;
        [SerializeField] Tilemap tilemapBack;
        [SerializeField] ResourcesOverlay resourcesOverlay;
        [SerializeField] int visiblePadding = 1;
        [SerializeField]
        [Tooltip("Editor / Development Build: не снимать тайлы за экраном. Release-билд всегда снимает.")]
        bool debugKeepOffscreenTiles;

        readonly Dictionary<RockType, TileBase> _tileCache = new();
        readonly HashSet<Vector2Int> _paintedCells = new();
        CellRect _visibleRect;
        bool _hasVisibleRect;
        Vector3Int _clipCell = new(int.MinValue, int.MinValue, int.MinValue);
        float _clipProgressStep = -1f;
        float _clipFacingAngle;

        bool ShouldEvictOffscreen =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            !debugKeepOffscreenTiles;
#else
            true;
#endif

        struct CellRect
        {
            public int XMin;
            public int XMax;
            public int YMin;
            public int YMax;

            public bool Equals(CellRect other) =>
                XMin == other.XMin && XMax == other.XMax && YMin == other.YMin && YMax == other.YMax;

            public bool Contains(Vector2Int cell) =>
                cell.x >= XMin && cell.x <= XMax && cell.y >= YMin && cell.y <= YMax;
        }

        void Awake()
        {
            if (tilemap == null)
            {
                Debug.LogError($"{nameof(DepthWorld)}: назначь Tilemap в инспекторе.", this);
                return;
            }

            var grid = tilemap.layoutGrid;
            if (grid == null)
            {
                Debug.LogError($"{nameof(DepthWorld)}: у Tilemap нет Grid (ожидается Depth/Grid/Tilemap).", this);
                return;
            }

            WorldGrid.Bind(grid);

            if (resourcesOverlay == null)
            {
                resourcesOverlay = GetComponentInChildren<ResourcesOverlay>();
            }

            if (tilemapBack != null && tilemapBack.layoutGrid != grid)
            {
                Debug.LogWarning(
                    $"{nameof(DepthWorld)}: tilemapBack должен быть на том же Grid, что и front.",
                    this);
            }
        }

        void LateUpdate()
        {
            if (tilemap == null)
            {
                return;
            }

            var gameManager = GameManager.Instance;
            if (gameManager?.World == null)
            {
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            SyncVisibleArea(gameManager.World, gameManager.Assets, cam, gameManager.Session.Seed);
        }

        public void SyncVisibleArea(CoreWorld world, AssetManager assets, Camera cam, int worldSeed = 0)
        {
            if (tilemap == null || world == null || assets == null || cam == null)
            {
                return;
            }

            if (worldSeed == 0)
            {
                worldSeed = GameManager.Instance?.Session?.Seed ?? 0;
            }

            var rect = ComputeVisibleRect(cam, world, visiblePadding);
            if (_hasVisibleRect && rect.Equals(_visibleRect))
            {
                return;
            }

            if (ShouldEvictOffscreen && _hasVisibleRect)
            {
                foreach (var cell in CollectCellsOutsideRect(rect))
                {
                    UnpaintCell(cell, worldSeed);
                }
            }

            for (var y = rect.YMin; y <= rect.YMax; y++)
            {
                for (var x = rect.XMin; x <= rect.XMax; x++)
                {
                    var logical = new Vector2Int(x, y);
                    if (_paintedCells.Contains(logical))
                    {
                        continue;
                    }

                    PaintCell(world, assets, worldSeed, x, y);
                }
            }

            _visibleRect = rect;
            _hasVisibleRect = true;
        }

        public void RefreshCell(CoreWorld world, AssetManager assets, int x, int y)
        {
            if (tilemap == null || world == null || assets == null)
            {
                return;
            }

            var worldSeed = GameManager.Instance?.Session?.Seed ?? 0;
            PaintCell(world, assets, worldSeed, x, y);
        }

        public void SetDrillClip(int x, int y, float progress, float facingAngleDeg)
        {
            if (tilemap == null)
            {
                return;
            }

            var tileCell = WorldGrid.ToTilemapCell(new Vector2Int(x, y));
            var progressStep = Mathf.Floor(Mathf.Clamp01(progress) * 30f);

            if (tileCell == _clipCell
                && Mathf.Approximately(progressStep, _clipProgressStep)
                && Mathf.Abs(Mathf.DeltaAngle(facingAngleDeg, _clipFacingAngle)) < 0.01f)
            {
                return;
            }

            _clipCell = tileCell;
            _clipProgressStep = progressStep;
            _clipFacingAngle = facingAngleDeg;

            var remaining = 1f - (progressStep / 30f);
            var baseTransform = GetTileBaseTransform(tileCell);
            tilemap.SetTransformMatrix(tileCell, baseTransform * BuildDrillClipMatrix(remaining, facingAngleDeg));
        }

        public void ClearDrillClip(int x, int y)
        {
            if (tilemap == null)
            {
                return;
            }

            _clipCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            _clipProgressStep = -1f;
            ApplyTileBaseTransform(WorldGrid.ToTilemapCell(new Vector2Int(x, y)));
        }

        Matrix4x4 GetTileBaseTransform(Vector3Int tileCell)
        {
            var tileBase = tilemap.GetTile<TileBase>(tileCell);
            if (tileBase == null)
            {
                return Matrix4x4.identity;
            }

            var tileData = default(TileData);
            tileBase.GetTileData(tileCell, tilemap, ref tileData);
            return tileData.transform;
        }

        void ApplyTileBaseTransform(Vector3Int tileCell)
        {
            tilemap.SetTransformMatrix(tileCell, GetTileBaseTransform(tileCell));
        }

        static Matrix4x4 BuildDrillClipMatrix(float remaining, float facingAngleDeg)
        {
            if (remaining <= 0.001f)
            {
                return Matrix4x4.zero;
            }

            var shrink = 1f - remaining;

            if (Mathf.Abs(Mathf.DeltaAngle(facingAngleDeg, 0f)) < 0.01f)
            {
                return Matrix4x4.TRS(
                    new Vector3(0f, -shrink * 0.5f, 0f),
                    Quaternion.identity,
                    new Vector3(1f, remaining, 1f));
            }

            if (Mathf.Abs(Mathf.DeltaAngle(facingAngleDeg, 180f)) < 0.01f)
            {
                return Matrix4x4.TRS(
                    new Vector3(0f, shrink * 0.5f, 0f),
                    Quaternion.identity,
                    new Vector3(1f, remaining, 1f));
            }

            if (Mathf.Abs(Mathf.DeltaAngle(facingAngleDeg, 90f)) < 0.01f)
            {
                return Matrix4x4.TRS(
                    new Vector3(shrink * 0.5f, 0f, 0f),
                    Quaternion.identity,
                    new Vector3(remaining, 1f, 1f));
            }

            return Matrix4x4.TRS(
                new Vector3(-shrink * 0.5f, 0f, 0f),
                Quaternion.identity,
                new Vector3(remaining, 1f, 1f));
        }

        public void RefreshVisibleArea(CoreWorld world, AssetManager assets, Camera cam, int centerX, float surfaceY = 0f)
        {
            SyncVisibleArea(world, assets, cam);
        }

        /// <summary>±radius по X, radius вниз от center (клетки мира).</summary>
        public void RefreshAroundCell(
            CoreWorld world,
            AssetManager assets,
            Vector2Int center,
            int radius)
        {
            if (tilemap == null || world == null || assets == null)
            {
                return;
            }

            radius = Mathf.Max(1, radius);

            ClearAllTilesAndTracking();

            var yStart = Mathf.Max(0, center.y);
            var yEnd = Mathf.Min(center.y + radius, world.WorldRadius);

            for (var y = yStart; y <= yEnd; y++)
            {
                for (var x = center.x - radius; x <= center.x + radius; x++)
                {
                    PaintCell(world, assets, 0, x, y);
                }
            }
        }

        public void RefreshRegion(
            CoreWorld world,
            AssetManager assets,
            int originX,
            int originY,
            int cellsX,
            int cellsY)
        {
            if (tilemap == null || world == null || assets == null)
            {
                return;
            }

            cellsX = Mathf.Max(1, cellsX);
            cellsY = Mathf.Max(1, cellsY);

            ClearAllTilesAndTracking();

            var yStart = Mathf.Max(0, originY);
            var yEnd = Mathf.Min(originY + cellsY - 1, world.WorldRadius);

            for (var y = yStart; y <= yEnd; y++)
            {
                for (var x = originX; x < originX + cellsX; x++)
                {
                    PaintCell(world, assets, 0, x, y);
                }
            }
        }

        public void RefreshArea(CoreWorld world, AssetManager assets, int centerX, int centerY, int radiusX, int radiusY)
        {
            if (tilemap == null || world == null || assets == null)
            {
                return;
            }

            ClearAllTilesAndTracking();

            for (var y = Mathf.Max(0, centerY); y <= centerY + radiusY; y++)
            {
                for (var x = centerX - radiusX; x <= centerX + radiusX; x++)
                {
                    PaintCell(world, assets, 0, x, y);
                }
            }
        }

        void ClearAllTilesAndTracking()
        {
            tilemap.ClearAllTiles();
            tilemapBack?.ClearAllTiles();
            _paintedCells.Clear();
            _hasVisibleRect = false;
            resourcesOverlay?.ClearAll();
        }

        static CellRect ComputeVisibleRect(Camera cam, CoreWorld world, int padding)
        {
            var center = cam.transform.position;
            var halfHeight = cam.orthographicSize;
            var halfWidth = halfHeight * cam.aspect;

            var bottomLeft = WorldGrid.WorldToCell(new Vector3(center.x - halfWidth, center.y - halfHeight, 0f));
            var bottomRight = WorldGrid.WorldToCell(new Vector3(center.x + halfWidth, center.y - halfHeight, 0f));
            var topLeft = WorldGrid.WorldToCell(new Vector3(center.x - halfWidth, center.y + halfHeight, 0f));
            var topRight = WorldGrid.WorldToCell(new Vector3(center.x + halfWidth, center.y + halfHeight, 0f));

            var xMin = Mathf.Min(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x) - padding;
            var xMax = Mathf.Max(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x) + padding;
            var yMin = Mathf.Min(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y) - padding;
            var yMax = Mathf.Max(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y) + padding;

            yMin = Mathf.Max(0, yMin);
            yMax = Mathf.Min(world.WorldRadius, yMax);
            xMin = Mathf.Max(-world.WorldRadius, xMin);
            xMax = Mathf.Min(world.WorldRadius, xMax);

            return new CellRect
            {
                XMin = xMin,
                XMax = xMax,
                YMin = yMin,
                YMax = yMax
            };
        }

        List<Vector2Int> CollectCellsOutsideRect(CellRect rect)
        {
            var stale = new List<Vector2Int>();
            foreach (var cell in _paintedCells)
            {
                if (!rect.Contains(cell))
                {
                    stale.Add(cell);
                }
            }

            return stale;
        }

        void PaintCell(CoreWorld world, AssetManager assets, int worldSeed, int x, int y)
        {
            var logical = new Vector2Int(x, y);
            var tileCell = WorldGrid.ToTilemapCell(logical);
            var cell = world.GetCell(x, y);
            var tile = ResolveTile(assets, cell);

            if (tile != null)
            {
                SetSolidTiles(tileCell, tile);
                _paintedCells.Add(logical);
                ApplyTileBaseTransform(tileCell);
            }
            else
            {
                tilemap.SetTile(tileCell, null);
                tilemap.SetTransformMatrix(tileCell, Matrix4x4.identity);

                var backTile = ResolveTunnelBackTile(world, assets, x, y);
                if (tilemapBack != null && backTile != null)
                {
                    tilemapBack.SetTile(tileCell, backTile);
                    _paintedCells.Add(logical);
                }
                else
                {
                    tilemapBack?.SetTile(tileCell, null);
                    _paintedCells.Remove(logical);
                }
            }

            SyncResourceOverlay(assets, world, worldSeed, x, y, cell);
        }

        void UnpaintCell(Vector2Int logical, int worldSeed)
        {
            var tileCell = WorldGrid.ToTilemapCell(logical);
            tilemap.SetTransformMatrix(tileCell, Matrix4x4.identity);
            ClearTileOnLayers(tileCell);
            _paintedCells.Remove(logical);
            resourcesOverlay?.SyncResource(null, logical.x, logical.y, worldSeed, ResourceType.None);
        }

        void SetSolidTiles(Vector3Int tileCell, TileBase tile)
        {
            tilemap.SetTile(tileCell, tile);
            tilemapBack?.SetTile(tileCell, tile);
        }

        void ClearTileOnLayers(Vector3Int tileCell)
        {
            tilemap.SetTile(tileCell, null);
            tilemapBack?.SetTile(tileCell, null);
        }

        TileBase ResolveTunnelBackTile(CoreWorld world, AssetManager assets, int x, int y)
        {
            if (TryResolveNeighborSolidTile(world, assets, x + 1, y, out var tile)
                || TryResolveNeighborSolidTile(world, assets, x - 1, y, out tile)
                || TryResolveNeighborSolidTile(world, assets, x, y + 1, out tile)
                || TryResolveNeighborSolidTile(world, assets, x, y - 1, out tile))
            {
                return tile;
            }

            var fallbackRock = RockLayers.GetDominantFlat(y, world.WorldRadius);
            return ResolveTile(assets, new CellData { Rock = fallbackRock });
        }

        bool TryResolveNeighborSolidTile(CoreWorld world, AssetManager assets, int x, int y, out TileBase tile)
        {
            var neighbor = world.GetCell(x, y);
            if (!neighbor.IsSolid)
            {
                tile = null;
                return false;
            }

            tile = ResolveTile(assets, neighbor);
            return tile != null;
        }

        void SyncResourceOverlay(AssetManager assets, CoreWorld world, int worldSeed, int x, int y, CellData cell)
        {
            if (resourcesOverlay == null)
            {
                return;
            }

            if (!cell.IsSolid || !cell.HasResource)
            {
                resourcesOverlay.SyncResource(assets, x, y, worldSeed, ResourceType.None);
                return;
            }

            var config = GameBootstrap.Instance?.WorldGen;
            var isCluster = config != null
                && ResourceClusterGenerator.TryGetResource(
                    x, y, worldSeed, world.WorldRadius, config, out _);

            var sectorSize = config?.ResourceClusterSpacing ?? 0;
            var session = GameBootstrap.Instance?.Session;
            var show = !isCluster
                || (sectorSize > 0 && VeinVisibility.IsCellRevealed(x, y, session, sectorSize));

            resourcesOverlay.SyncResource(
                assets,
                x,
                y,
                worldSeed,
                show ? cell.Resource : ResourceType.None);
        }

        TileBase ResolveTile(AssetManager assets, CellData cell)
        {
            if (cell.Rock is RockType.Air or RockType.Core)
            {
                return null;
            }

            if (_tileCache.TryGetValue(cell.Rock, out var cached))
            {
                return cached;
            }

            var assetId = RockLayers.ToAssetId(cell.Rock);
            if (string.IsNullOrEmpty(assetId))
            {
                return null;
            }

            var tile = assets.GetTile(DepthPackName, assetId);
            if (tile != null)
            {
                _tileCache[cell.Rock] = tile;
            }

            return tile;
        }
    }
}
