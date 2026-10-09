using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Offline prefab authoring. Runtime uses the existing resource loader and Spine callbacks.
    public static class MainOtterAuthoring
    {
        const string Background = "Assets/BubblePics/Resources/CoralV3/MainRuinsBackground.png";
        const string Root = "Assets/BubblePics/RuntimePrefabs/";
        static readonly string BackupRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtChanges/MainOtter-20260929/Backup"));

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring.");
            AssetDatabase.ImportAsset(Background, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Background);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 2048;
                settings.format = TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            var report = new StringBuilder();
            Edit(Root + "Game/BubbleWorld.prefab", root =>
            {
                int count = 0;
                foreach (var binder in root.GetComponentsInChildren<CoralResourceSprite>(true))
                {
                    var data = new SerializedObject(binder);
                    string path = data.FindProperty("_resourcePath").stringValue;
                    if (path != "CoralV3/CoralBackgroundReference" && path != "CoralV3/MainRuinsBackground") continue;
                    data.FindProperty("_resourcePath").stringValue = "CoralV3/MainRuinsBackground";
                    data.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
                if (count != 1) throw new InvalidOperationException("Expected one gameplay background, got " + count);
            }, report);
            Edit(Root + "UI/TopGameBar.prefab", RestoreOtter, report);
            Edit(Root + "Game/DolphinDecoration.prefab", RestoreOtter, report);
            const string appearancePath = "Assets/BubblePics/Resources/CoralV3/ToolAppearance.asset";
            string appearanceBackup = Path.Combine(BackupRoot, appearancePath);
            Directory.CreateDirectory(Path.GetDirectoryName(appearanceBackup));
            if (!File.Exists(appearanceBackup)) File.Copy(appearancePath, appearanceBackup);
            var appearance = AssetDatabase.LoadAssetAtPath<ToolAppearance>(appearancePath);
            appearance.MagnetResource = "BubblePicsDynamic/588febfffe00a554cabdb727c28e81b8/drop_small";
            EditorUtility.SetDirty(appearance);
            report.AppendLine("PASS third tool uses original transparent otter icon; unlock/count/ad logic unchanged.");
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(BackupRoot, "../authoring.txt"), report + "\n" + DateTime.UtcNow.ToString("O"));
        }

        static void RestoreOtter(GameObject root)
        {
            var mascot = root.GetComponentInChildren<DolphinDecoration>(true);
            if (mascot == null) throw new InvalidOperationException("Missing original mascot controller.");
            var spine = mascot.Spine;
            if (spine == null) throw new InvalidOperationException("Missing original otter Spine.");
            var data = new SerializedObject(mascot);
            data.FindProperty("_portrait").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            var dragon = mascot.transform.Find("CoralPortrait");
            if (dragon != null) UnityEngine.Object.DestroyImmediate(dragon.gameObject);
            spine.gameObject.SetActive(true);
            spine.enabled = true;
            spine.GetComponent<MeshRenderer>().enabled = true;
        }

        static void Edit(string path, Action<GameObject> edit, StringBuilder report)
        {
            string backup = Path.Combine(BackupRoot, path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(path, backup);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int buttons = root.GetComponentsInChildren<Button>(true).Length;
                edit(root);
                if (root.GetComponentsInChildren<Button>(true).Length != buttons) throw new InvalidOperationException("Button count changed.");
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                    if (button.targetGraphic == null || button.onClick.GetPersistentEventCount() != 0)
                        throw new InvalidOperationException("Invalid native Button: " + button.name);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.AppendLine("PASS " + path + "; standard Buttons retained=" + buttons);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
