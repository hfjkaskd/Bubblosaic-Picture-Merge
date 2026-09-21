using UnityEngine;

namespace BubblePics
{
    /// <summary>The same authored assets are loaded in the editor and in players.</summary>
    public static class ResourceAssetLoader
    {
        public static T Load<T>(string resourcePath) where T : Object
        {
            var catalog = PrefabCatalog.Current != null ? PrefabCatalog.Current.RuntimeAssets : null;
            var asset = catalog != null ? catalog.Load<T>(resourcePath) : null;
            return asset != null ? asset : Resources.Load<T>(resourcePath);
        }
    }
}
