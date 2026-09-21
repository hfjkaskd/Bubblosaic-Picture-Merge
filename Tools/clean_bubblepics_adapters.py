from pathlib import Path
import re

root=Path('BizzaWZ/Assets/BubblePics/Scripts')
p=root/'App/AppConfig.cs'
s=p.read_text(encoding='utf-8-sig').replace('using System.Reflection;\n','')
s=re.sub(r'\s*ApplyPersistedDebugOverrides\((?:defaults|loaded)\);','',s)
a=s.index('        /// <summary>\n        /// Applies one persistent GM override')
b=s.index('#if UNITY_EDITOR',a)
s=s[:a]+s[b:]
p.write_text(s,encoding='utf-8')
for relative in ['App/BuildFlavor.cs','GM/BubblePicsGmService.cs']:
    for suffix in ['', '.meta']:
        p=root/(relative+suffix)
        if p.exists(): p.unlink()
p=root/'App/ResourceAssetLoader.cs'
p.write_text('''using UnityEngine;

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
''',encoding='utf-8')
