using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Maps original gameplay paths to authored assets loaded on demand.
    /// Large migrated assets are not loaded merely by opening the catalog.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RuntimeAssetCatalog",
        menuName = "BubblePics/Runtime Asset Catalog")]
    public sealed class RuntimeAssetCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Path;
            public string ResourcePath;
            public UnityEngine.Object Asset;
        }

        [SerializeField] Entry[] _entries = Array.Empty<Entry>();

        Dictionary<string, List<Entry>> _lookup;

        public Entry[] Entries => _entries;

        public T Load<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            EnsureLookup();
            if (!_lookup.TryGetValue(path, out List<Entry> assets))
                return null;
            for (int i = 0; i < assets.Count; i++)
            {
                var entry = assets[i];
                if (entry.Asset is T typed) return typed;
                if (!string.IsNullOrEmpty(entry.ResourcePath))
                {
                    var loaded = Resources.Load<T>(entry.ResourcePath);
                    if (loaded != null) return loaded;
                }
            }
            return null;
        }

        public void Configure(Entry[] entries)
        {
            _entries = entries ?? Array.Empty<Entry>();
            _lookup = null;
        }

        void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, List<Entry>>(
                StringComparer.Ordinal);
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                if (string.IsNullOrWhiteSpace(entry.Path) || (entry.Asset == null && string.IsNullOrEmpty(entry.ResourcePath)))
                    continue;
                if (!_lookup.TryGetValue(
                        entry.Path,
                        out List<Entry> assets))
                {
                    assets = new List<Entry>(2);
                    _lookup[entry.Path] = assets;
                }
                assets.Add(entry);
            }
        }
    }
}
