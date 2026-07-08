using Operator.Data;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace Operator.Managers
{
    /// <summary>
    /// Единая точка доступа к ассетам по id. Пути в коде не хардкодим.
    /// </summary>
    public class AssetManager
    {
        readonly Dictionary <string, AssetPack> _assetPacks = new();

        public AssetManager()
        {
            foreach (var assetPack in Resources.LoadAll<AssetPack>("Asset Packs"))
            {
                assetPack.Build();
                _assetPacks.Add(assetPack.name, assetPack);
            }
        }

        private AssetPack GetPackByName(string assetPackName)
        {
            if (_assetPacks.TryGetValue(assetPackName, out var assetPack))
            {
                return assetPack;
            }   

            Debug.LogError($"Asset pack {assetPackName} not found!");
            return null;
        }

        public Sprite GetSprite(string assetPackName, string id) => GetPackByName(assetPackName)?.Get<Sprite>(id);
        public TileBase GetTile(string assetPackName, string id) => GetPackByName(assetPackName)?.Get<TileBase>(id);
        public AudioClip GetAudio(string assetPackName, string id) => GetPackByName(assetPackName)?.Get<AudioClip>(id);
        public TextAsset GetText(string assetPackName, string id) => GetPackByName(assetPackName)?.Get<TextAsset>(id);
        public GameObject GetPrefab(string assetPackName, string id) => GetPackByName(assetPackName)?.Get<GameObject>(id);

    }
}
