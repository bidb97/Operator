using System;
using System.Collections.Generic;
using UnityEngine;

/**
    Создание Asset Pack в Unity, Создаем ассеты в папке Resources/Asset Packs
**/
namespace Operator.Data
{
    
    [CreateAssetMenu(fileName = "Asset Pack", menuName = "Asset Pack (Scriptable Object)")]

    public class AssetPack : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string assetId;
            public UnityEngine.Object asset;
        }

        [SerializeField] Entry[] entries;

        Dictionary<string, UnityEngine.Object> _lookup;

        public void Build()
        {
            _lookup = new Dictionary<string, UnityEngine.Object>(entries.Length);

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.assetId) || entry.asset == null)
                {
                    continue;
                }

                _lookup[entry.assetId] = entry.asset;
            }
        }

        private bool TryGet(string assetId, out UnityEngine.Object asset)
        {
            if (_lookup == null)
            {
                Build();
            }

            return _lookup.TryGetValue(assetId, out asset);
        }

        public T Get<T>(string assetId) where T : UnityEngine.Object
        {
            if (!TryGet(assetId, out var asset))
            {
                Debug.LogError($"AssetPack: asset '{assetId}' not found.");
                return null;
            }

            if (asset is not T typed)
            {
                Debug.LogError($"AssetPack: '{assetId}' is {asset.GetType().Name}, expected {typeof(T).Name}.");
                return null;
            }

            return typed;
        }
    }
}
