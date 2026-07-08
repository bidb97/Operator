using System.Collections.Generic;
using Operator.Depth.Core;
using Operator.Managers;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Operator.Depth.Unity
{
    /// <summary>
    /// Ресурсы поверх Tilemap: prefab Core + Glow, в коде только sprite / позиция / rotation.
    /// </summary>
    public class ResourcesOverlay : MonoBehaviour
    {
        const string GlowChildName = "Glow";

        [SerializeField] Tilemap tilemap;
        [SerializeField] TilemapRenderer tilemapRenderer;
        [SerializeField] Transform resourcesRoot;
        [SerializeField] GameObject prefabSynth;
        [SerializeField] GameObject prefabRellit;
        [SerializeField] GameObject prefabLumin;
        [SerializeField] GameObject prefabDeVault;
        [SerializeField] int sortingOrderOffset = 1;

        readonly Dictionary<Vector2Int, ResourceSlot> _active = new();
        readonly Dictionary<ResourceType, List<ResourceSlot>> _pool = new();
        int _sortingLayerId;
        int _sortingOrder;

        sealed class ResourceSlot
        {
            public ResourceType Type;
            public Transform Root;
            public SpriteRenderer Core;
            public SpriteRenderer Glow;
        }

        void Awake()
        {
            if (tilemap == null || tilemapRenderer == null || resourcesRoot == null)
            {
                Debug.LogError(
                    $"{nameof(ResourcesOverlay)}: назначь Tilemap, Tilemap Renderer и Resources Root в инспекторе.",
                    this);
                enabled = false;
                return;
            }

            if (!HasPrefabs())
            {
                Debug.LogError(
                    $"{nameof(ResourcesOverlay)}: назначь 4 prefab-а (Synth, Rellit, Lumin, DeVault).",
                    this);
                enabled = false;
                return;
            }

            _sortingLayerId = tilemapRenderer.sortingLayerID;
            _sortingOrder = tilemapRenderer.sortingOrder + sortingOrderOffset;
        }

        bool HasPrefabs() =>
            prefabSynth != null
            && prefabRellit != null
            && prefabLumin != null
            && prefabDeVault != null;

        public void Clear()
        {
            ClearAll();
        }

        public void ClearAll()
        {
            foreach (var slot in _active.Values)
            {
                ReleaseSlot(slot);
            }

            _active.Clear();
        }

        public void SyncResource(AssetManager assets, int x, int y, int worldSeed, ResourceType resource)
        {
            if (!enabled)
            {
                return;
            }

            var key = new Vector2Int(x, y);

            if (!ResourceTypeIds.HasResource(resource))
            {
                ClearResource(key);
                return;
            }

            if (assets == null || GarageBounds.Contains(x, y))
            {
                ClearResource(key);
                return;
            }

            var sprite = assets.GetSprite(PackName, ResourceTypeIds.AssetId(resource));
            if (sprite == null)
            {
                ClearResource(key);
                return;
            }

            var tmCell = WorldGrid.ToTilemapCell(key);
            var cellCenter = tilemap.GetCellCenterWorld(tmCell);
            var rotationZ = ResourceRotationZ(x, y, worldSeed, (int)resource);

            if (_active.TryGetValue(key, out var existing) && existing.Type != resource)
            {
                ClearResource(key);
            }

            if (!_active.TryGetValue(key, out var slot))
            {
                slot = RentSlot(resource);
                if (slot == null)
                {
                    return;
                }

                _active[key] = slot;
            }

            slot.Core.sprite = sprite;
            slot.Glow.sprite = sprite;
            slot.Root.SetParent(resourcesRoot, true);
            slot.Root.SetPositionAndRotation(cellCenter, Quaternion.Euler(0f, 0f, rotationZ));
            slot.Root.localScale = Vector3.one;
            slot.Root.gameObject.SetActive(true);
        }

        const string PackName = "VeinResources";

        void ClearResource(Vector2Int logical)
        {
            if (!_active.TryGetValue(logical, out var slot))
            {
                return;
            }

            ReleaseSlot(slot);
            _active.Remove(logical);
        }

        void ReleaseSlot(ResourceSlot slot)
        {
            slot.Root.gameObject.SetActive(false);
        }

        static float ResourceRotationZ(int cellX, int cellY, int worldSeed, int resourceIndex)
        {
            unchecked
            {
                var hash = cellX;
                hash = hash * 31 + cellY;
                hash = hash * 31 + worldSeed;
                hash = hash * 31 + resourceIndex;
                var rng = new System.Random(hash);
                return (float)(rng.NextDouble() * 360d);
            }
        }

        ResourceSlot RentSlot(ResourceType resource)
        {
            if (!_pool.TryGetValue(resource, out var pool))
            {
                pool = new List<ResourceSlot>();
                _pool[resource] = pool;
            }

            for (var i = 0; i < pool.Count; i++)
            {
                var slot = pool[i];
                if (!slot.Root.gameObject.activeSelf && !_active.ContainsValue(slot))
                {
                    return slot;
                }
            }

            var prefab = GetPrefab(resource);
            if (prefab == null)
            {
                return null;
            }

            var instance = Instantiate(prefab, resourcesRoot);
            instance.name = prefab.name;

            var slotNew = BindSlot(instance, resource);
            if (slotNew == null)
            {
                Destroy(instance);
                return null;
            }

            ApplySorting(slotNew);
            pool.Add(slotNew);
            return slotNew;
        }

        ResourceSlot BindSlot(GameObject instance, ResourceType resource)
        {
            var core = instance.GetComponent<SpriteRenderer>();
            var glowTransform = instance.transform.Find(GlowChildName);
            var glow = glowTransform != null ? glowTransform.GetComponent<SpriteRenderer>() : null;

            if (core == null || glow == null)
            {
                Debug.LogError(
                    $"{nameof(ResourcesOverlay)}: prefab {instance.name} должен иметь Core (SpriteRenderer на корне) и child {GlowChildName}.",
                    instance);
                return null;
            }

            return new ResourceSlot
            {
                Type = resource,
                Root = instance.transform,
                Core = core,
                Glow = glow
            };
        }

        void ApplySorting(ResourceSlot slot)
        {
            slot.Core.sortingLayerID = _sortingLayerId;
            slot.Core.sortingOrder = _sortingOrder;
            slot.Glow.sortingLayerID = _sortingLayerId;
            slot.Glow.sortingOrder = _sortingOrder - 1;
        }

        GameObject GetPrefab(ResourceType resource)
        {
            return resource switch
            {
                ResourceType.Synth => prefabSynth,
                ResourceType.Rellit => prefabRellit,
                ResourceType.Lumin => prefabLumin,
                ResourceType.DeVault => prefabDeVault,
                _ => null
            };
        }
    }
}
