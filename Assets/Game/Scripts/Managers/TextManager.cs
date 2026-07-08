using UnityEngine;

namespace Operator.Managers
{
    /// <summary>
    /// Строки UI, AI, журнал — по id из каталога (TextAsset) или таблицы.
    /// </summary>
    public class TextManager
    {
        readonly AssetManager _assets;

        public TextManager(AssetManager assets)
        {
            _assets = assets;
        }

        public string Get(string assetPackName, string assetId)
        {
            var textAsset = _assets.GetText(assetPackName, assetId);

            if (textAsset == null)
            {
                return $"[{assetId}]";
            }
                
            return textAsset.text;
        }

        public string Format(string assetPackName, string assetId, params object[] args)
        {
            return string.Format(Get(assetPackName, assetId), args);
        }
    }
}
