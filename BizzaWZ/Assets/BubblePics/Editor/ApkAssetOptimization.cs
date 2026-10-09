using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace BubblePics.EditorTools
{
    // Offline asset authoring. Does not replace player initialization or runtime loading.
    public static class ApkAssetOptimization
    {
        [Serializable] public sealed class Plan
        {
            public string backupRoot;
            public FontPlan[] fonts;
            public TexturePlan[] textures;
            public string[] sharedFonts;
        }
        [Serializable] public sealed class FontPlan
        {
            public string path;
            public int atlasSize;
            public int padding;
            public int[] characters;
            public string[] materials;
            public EffectPlan[] effects;
        }
        [Serializable] public sealed class EffectPlan { public string path; public FloatProperty[] properties; }
        [Serializable] public sealed class FloatProperty { public string name; public float value; }
        [Serializable] public sealed class TexturePlan
        {
            public string path;
            public int maxSize;
            public int format;
            public int quality;
            public string reason;
        }
        [Serializable] sealed class SpriteLayout { public SpriteMetaData[] sprites; }
        static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation/ApkOptimize-20260929"));
        static readonly string PlanPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtChanges/ApkOptimize-20260929/plan.json"));
        static readonly string[] DistanceProperties =
        {
            "_FaceDilate", "_OutlineWidth", "_OutlineSoftness", "_UnderlayDilate",
            "_UnderlaySoftness", "_UnderlayOffsetX", "_UnderlayOffsetY", "_WeightNormal", "_WeightBold"
        };

        public static void ApplyFonts()
        {
            RequireStopped();
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
            Directory.CreateDirectory(Folder);
            var log = new StringBuilder();
            foreach (var rule in plan.fonts)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(rule.path);
                if (font == null) throw new InvalidOperationException("Missing font: " + rule.path);
                Backup(plan, rule.path);
                int oldPadding = font.atlasPadding;
                long before = AtlasBytes(font);
                var metrics = new Dictionary<uint, UnityEngine.TextCore.GlyphMetrics>();
                foreach (var character in font.characterTable)
                    if (character.glyph != null) metrics[character.unicode] = character.glyph.metrics;
                var materials = new List<Material> { font.material };
                foreach (string path in rule.materials)
                {
                    Backup(plan, path);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null) throw new InvalidOperationException("Missing font material: " + path);
                    materials.Add(material);
                }
                // Glyph sampling size and face metrics stay unchanged. Only packing changes.
                var atlasSettings = new SerializedObject(font);
                atlasSettings.FindProperty("m_AtlasWidth").intValue = rule.atlasSize;
                atlasSettings.FindProperty("m_AtlasHeight").intValue = rule.atlasSize;
                atlasSettings.FindProperty("m_AtlasPadding").intValue = rule.padding;
                atlasSettings.ApplyModifiedPropertiesWithoutUndo();
                font.isMultiAtlasTexturesEnabled = true;
                font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                font.ClearFontAssetData(true);
                var characters = new uint[rule.characters.Length];
                for (int i = 0; i < characters.Length; i++) characters[i] = (uint)rule.characters[i];
                if (characters.Length > 0) font.TryAddCharacters(characters, out uint[] missing, true);
                foreach (uint code in characters)
                {
                    if (code <= char.MaxValue && char.IsControl((char)code)) continue;
                    if (!font.characterLookupTable.ContainsKey(code))
                        throw new InvalidOperationException("Lost existing character U+" + code.ToString("X4") + " in " + rule.path);
                    if (!metrics.TryGetValue(code, out var beforeMetric)) continue;
                    var afterMetric = font.characterLookupTable[code].glyph.metrics;
                    if (Mathf.Abs(beforeMetric.horizontalAdvance - afterMetric.horizontalAdvance) > .01f
                        || Mathf.Abs(beforeMetric.width - afterMetric.width) > .01f
                        || Mathf.Abs(beforeMetric.height - afterMetric.height) > .01f)
                        throw new InvalidOperationException("Glyph layout metrics changed in " + rule.path);
                }
                float ratio = oldPadding / (float)rule.padding;
                font.normalStyle *= ratio;
                font.boldStyle *= ratio;
                foreach (var material in materials)
                {
                    material.mainTexture = font.atlasTexture;
                    foreach (string property in DistanceProperties)
                        if (material.HasProperty(property)) material.SetFloat(property, material.GetFloat(property) * ratio);
                    material.SetFloat("_GradientScale", rule.padding + 1);
                    material.SetFloat("_TextureWidth", rule.atlasSize);
                    material.SetFloat("_TextureHeight", rule.atlasSize);
                    ShaderUtilities.UpdateShaderRatios(material);
                    EditorUtility.SetDirty(material);
                }
                // Recover physical shadow distances from the backed-up authored material.
                // TMP reserves part of the SDF range for bold weight, so underlay values
                // require a different conversion than outline values when padding changes.
                foreach (var effect in rule.effects)
                {
                    var mat = effect.path == rule.path ? font.material : AssetDatabase.LoadAssetAtPath<Material>(effect.path);
                    foreach (var property in effect.properties)
                        if (mat.HasProperty(property.name)) mat.SetFloat(property.name, property.value);
                    ShaderUtilities.UpdateShaderRatios(mat);
                    float oldGradient = mat.GetFloat("_GradientScale");
                    float oldUnderlayScale = oldGradient * mat.GetFloat("_ScaleRatioC");
                    var underlay = new float[4];
                    string[] underlayNames = { "_UnderlayOffsetX", "_UnderlayOffsetY", "_UnderlayDilate", "_UnderlaySoftness" };
                    for (int i = 0; i < 4; i++) underlay[i] = mat.HasProperty(underlayNames[i]) ? mat.GetFloat(underlayNames[i]) : 0;
                    float authoredRatio = (oldGradient - 1) / rule.padding;
                    foreach (string property in DistanceProperties)
                        if (mat.HasProperty(property)) mat.SetFloat(property, mat.GetFloat(property) * authoredRatio);
                    mat.SetFloat("_GradientScale", rule.padding + 1);
                    mat.SetFloat("_TextureWidth", rule.atlasSize);
                    mat.SetFloat("_TextureHeight", rule.atlasSize);
                    ShaderUtilities.UpdateShaderRatios(mat);
                    for (int iteration = 0; iteration < 4; iteration++)
                    {
                        float newScale = (rule.padding + 1) * mat.GetFloat("_ScaleRatioC");
                        if (newScale <= 0.0001f) break;
                        for (int i = 0; i < 4; i++)
                            if (mat.HasProperty(underlayNames[i])) mat.SetFloat(underlayNames[i], underlay[i] * oldUnderlayScale / newScale);
                        ShaderUtilities.UpdateShaderRatios(mat);
                    }
                    EditorUtility.SetDirty(mat);
                }
                // Keep authored glyphs in the build; unknown input still follows the original
                // dynamic font behavior rather than requiring a new startup generation pass.
                var serialized = new SerializedObject(font);
                serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (var texture in font.atlasTextures) if (texture != null) EditorUtility.SetDirty(texture);
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssets();
                log.AppendLine(rule.path + " | glyphs=" + font.characterTable.Count + " | atlas=" + rule.atlasSize
                    + " | padding=" + rule.padding + " | textures=" + font.atlasTextureCount
                    + " | beforeBytes=" + before + " | afterBytes=" + AtlasBytes(font) + " | preservedMetrics=PASS");
                File.WriteAllText(Path.Combine(Folder, "fonts-result.txt"), log.ToString());
            }
            SynchronizeFontAliases(plan, log);
            ConfigureSharedFonts(plan, log);
            AssetDatabase.SaveAssets();
            log.AppendLine("PASS " + DateTime.UtcNow.ToString("O"));
            File.WriteAllText(Path.Combine(Folder, "fonts-result.txt"), log.ToString());
        }

        static void SynchronizeFontAliases(Plan plan, StringBuilder log)
        {
            var source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/BaggageGo-Bold SDF.asset");
            foreach (string path in new[] { "Assets/BubblePics/Resources/TierReference20260928/TextFont.asset", "Assets/BubblePics/Resources/NewPlayerReference20260928/TextFont.asset" })
            {
                Backup(plan, path);
                var alias = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                var fallback = new List<TMP_FontAsset>(alias.fallbackFontAssetTable);
                var face = alias.faceInfo;
                string name = alias.name;
                EditorUtility.CopySerialized(source, alias);
                alias.name = name;
                alias.faceInfo = face;
                // Known glyphs share the packed pages. Unknown characters are added
                // by the source font, never by two writers sharing one atlas texture.
                alias.atlasPopulationMode = AtlasPopulationMode.Static;
                if (!fallback.Contains(source)) fallback.Insert(0, source);
                alias.fallbackFontAssetTable = fallback;
                alias.ReadFontAssetDefinition();
                foreach (var glyph in alias.glyphTable)
                    if (glyph.atlasIndex >= alias.atlasTextures.Length || alias.atlasTextures[glyph.atlasIndex] == null)
                        throw new InvalidOperationException("Missing alias atlas: " + path);
                EditorUtility.SetDirty(alias);
                log.AppendLine("ALIAS PASS " + path + " | shared atlas references repaired");
            }
        }

        static void ConfigureSharedFonts(Plan plan, StringBuilder log)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("Addressables settings missing.");
            var group = settings.FindGroup("Shared UI Fonts");
            if (group == null)
                group = settings.CreateGroup("Shared UI Fonts", false, false, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            schema.BuildPath.SetVariableByName(settings, "Local.BuildPath");
            schema.LoadPath.SetVariableByName(settings, "Local.LoadPath");
            schema.IncludeInBuild = true;
            foreach (string path in plan.sharedFonts)
            {
                if (path.Contains("/Resources/")) throw new InvalidOperationException("Do not move Resources fonts implicitly: " + path);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (!(asset is TMP_FontAsset) && !(asset is Font)) throw new InvalidOperationException("Not a font: " + path);
                string guid = AssetDatabase.AssetPathToGUID(path);
                var existing = settings.FindAssetEntry(guid);
                string previousAddress = existing == null ? path : existing.address;
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                entry.address = previousAddress;
                log.AppendLine("SHARED " + path);
            }
            EditorUtility.SetDirty(schema);
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(settings);
        }

        public static void ApplyTextures()
        {
            RequireStopped();
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(PlanPath));
            var log = new StringBuilder("Android texture import optimization\n");
            foreach (var rule in plan.textures)
            {
                var importer = AssetImporter.GetAtPath(rule.path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing texture importer: " + rule.path);
                Backup(plan, rule.path + ".meta");
                string spriteLayout = JsonUtility.ToJson(new SpriteLayout { sprites = importer.spritesheet });
                var settings = importer.GetPlatformTextureSettings("Android");
                if (settings.overridden && settings.maxTextureSize == rule.maxSize
                    && (int)settings.format == rule.format && settings.compressionQuality == rule.quality) continue;
                settings.overridden = true;
                settings.maxTextureSize = rule.maxSize;
                settings.format = (TextureImporterFormat)rule.format;
                settings.compressionQuality = rule.quality;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
                if (JsonUtility.ToJson(new SpriteLayout { sprites = importer.spritesheet }) != spriteLayout)
                    throw new InvalidOperationException("Authored sprite rects changed: " + rule.path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(rule.path);
                log.AppendLine(rule.path + " | " + texture.width + "x" + texture.height
                    + " | format=" + (int)settings.format + " | " + rule.reason);
                File.WriteAllText(Path.Combine(Folder, "textures-result.txt"), log.ToString());
            }
            log.AppendLine("PASS " + DateTime.UtcNow.ToString("O"));
            File.WriteAllText(Path.Combine(Folder, "textures-result.txt"), log.ToString());
        }

        static long AtlasBytes(TMP_FontAsset font)
        {
            long size = 0;
            foreach (var texture in font.atlasTextures)
                if (texture != null) size += texture.GetRawTextureData<byte>().Length;
            return size;
        }

        static void Backup(Plan plan, string path)
        {
            string destination = Path.Combine(plan.backupRoot, path);
            if (File.Exists(destination)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(path, destination);
            if (File.Exists(path + ".meta") && !File.Exists(destination + ".meta"))
                File.Copy(path + ".meta", destination + ".meta");
        }

        static void RequireStopped()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before changing imported assets.");
        }
    }
}
