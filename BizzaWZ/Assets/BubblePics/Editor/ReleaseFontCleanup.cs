using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BubblePics.EditorTools
{
    public static class ReleaseFontCleanup
    {
        const string Root="Assets/BubblePics/RuntimeFonts/";
        static readonly string Folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/ApkOptimize-20260929/stage3"));
        public static void Audit()
        {
            var required=new HashSet<uint>();var log=new StringBuilder();
            foreach(var file in Directory.GetFiles("Assets/BubblePics/Resources/Localization","*.json"))
            {
                if(Path.GetFileName(file).StartsWith("zh_",StringComparison.Ordinal))continue;
                var map=SpineLite.MiniJson.Parse(File.ReadAllText(file)) as Dictionary<string,object>;
                if(map!=null)foreach(var value in map.Values)if(value is string s)foreach(char c in s)if(c>=0x3000&&!char.IsSurrogate(c))required.Add(c);
            }
            FontEngine.InitializeFontEngine();var uncovered=new HashSet<uint>(required);
            foreach(string file in Directory.GetFiles(Root,"*.ttf"))
            {
                if(file.Contains("SourceHan")||file.Contains("NotoSansSC"))continue;
                var font=AssetDatabase.LoadAssetAtPath<Font>(file);FontEngine.LoadFontFace(font,64);int found=0;
                foreach(uint c in required)if(FontEngine.TryGetGlyphWithUnicodeValue(c,GlyphLoadFlags.LOAD_DEFAULT,out var glyph)){uncovered.Remove(c);found++;}
                log.AppendLine(file+" supported="+found+" / "+required.Count);
            }
            var han=AssetDatabase.LoadAssetAtPath<Font>(Root+"SourceHanSansSC-Bold.ttf");FontEngine.LoadFontFace(han,64);
            var subset=new List<uint>();foreach(uint c in uncovered)if(FontEngine.TryGetGlyphWithUnicodeValue(c,GlyphLoadFlags.LOAD_DEFAULT,out var glyph))subset.Add(c);
            subset.Sort();var chars=new StringBuilder();foreach(uint c in subset)chars.Append(char.ConvertFromUtf32((int)c));
            File.WriteAllText(Path.Combine(Folder,"release-language-subset.txt"),chars.ToString());
            log.AppendLine("Required="+required.Count+" Uncovered="+uncovered.Count+" SuppliedBySourceHan="+subset.Count);
            File.WriteAllText(Path.Combine(Folder,"font-audit.txt"),log.ToString());
        }

        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            Audit();
            string subsetPath=Root+"TMP/ReleaseLanguageSupplement SDF.asset";
            string chars=File.ReadAllText(Path.Combine(Folder,"release-language-subset.txt"));
            TMP_FontAsset supplement=null;
            if(chars.Length>0)
            {
                supplement=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(subsetPath);
                if(supplement==null)
                {
                    supplement=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(Root+"SourceHanSansSC-Bold.ttf"),64,8,GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic,false);
                    supplement.name="ReleaseLanguageSupplement SDF";
                    if(!supplement.TryAddCharacters(chars,out string missing)||missing.Length>0)throw new InvalidOperationException("Missing release characters: "+missing);
                    supplement.atlasPopulationMode=AtlasPopulationMode.Static;
                    var so=new SerializedObject(supplement);
                    so.FindProperty("m_SourceFontFileGUID").stringValue=string.Empty;
                    so.FindProperty("m_SourceFontFile").objectReferenceValue=null;
                    so.FindProperty("m_SourceFontFile_EditorRef").objectReferenceValue=null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(supplement,subsetPath);
                    AssetDatabase.AddObjectToAsset(supplement.material,supplement);
                    foreach(var texture in supplement.atlasTextures)AssetDatabase.AddObjectToAsset(texture,supplement);
                    EditorUtility.SetDirty(supplement);
                }
            }
            var removed=new HashSet<string>{Root+"TMP/NotoSansSC-subset SDF.asset",Root+"TMP/SourceHanSansSC-Bold SDF.asset"};
            foreach(string guid in AssetDatabase.FindAssets("t:TMP_FontAsset",new[]{"Assets"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);if(path.Contains("/ArtArchive/"))continue;
                var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(font==null||font.fallbackFontAssetTable==null)continue;
                bool changed=font.fallbackFontAssetTable.Exists(f=>f!=null&&removed.Contains(AssetDatabase.GetAssetPath(f)));
                if(!changed)continue;Backup(path);
                font.fallbackFontAssetTable.RemoveAll(f=>f!=null&&removed.Contains(AssetDatabase.GetAssetPath(f)));
                if(supplement!=null&&!font.fallbackFontAssetTable.Contains(supplement))font.fallbackFontAssetTable.Add(supplement);
                EditorUtility.SetDirty(font);
            }
            var settings=AddressableAssetSettingsDefaultObject.Settings;
            if(supplement!=null)
            {
                var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(subsetPath),settings.FindGroup("Shared UI Fonts"));entry.address="ReleaseLanguageSupplement";
            }
            AssetDatabase.SaveAssets();
            foreach(string path in new[]{Root+"TMP/NotoSansSC-subset SDF.asset",Root+"TMP/SourceHanSansSC-Bold SDF.asset",Root+"NotoSansSC-subset.ttf",Root+"SourceHanSansSC-Bold.ttf","Assets/BubblePics/Resources/Localization/zh_CN.json","Assets/BubblePics/Resources/Localization/zh_TW.json"})
            {
                if(!File.Exists(path))continue;Backup(path);settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(path));
                string to="Assets/BubblePics/ArtArchive/ReleaseCleanup20260929/"+path.Substring("Assets/BubblePics/".Length).Replace("Resources/","SourceData/");
                Directory.CreateDirectory(Path.GetDirectoryName(to));AssetDatabase.Refresh();
                string error=AssetDatabase.MoveAsset(path,to);if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
            }
            EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
            foreach(string alias in new[]{"zh","zh-CN","zh_TW","zh-Hant-HK","ChineseSimplified","ChineseTraditional"})if(Localization.NormalizeLocale(alias)!="en")throw new InvalidOperationException("Locale removal failed: "+alias);
            File.WriteAllText(Path.Combine(Folder,"font-cleanup.txt"),"PASS Removed Chinese locale resources and both CJK fallback font families. Static supplementary characters="+chars.Length+"; no source TTF reference. "+DateTime.UtcNow.ToString("O"));
        }

        static void Backup(string path)
        {
            string target="F:/CodexArtifacts/pingguoshu-optimize-20260929/stage3-before/"+path;
            if(File.Exists(target))return;Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(path,target);
            if(File.Exists(path+".meta"))File.Copy(path+".meta",target+".meta");
        }
    }
}

