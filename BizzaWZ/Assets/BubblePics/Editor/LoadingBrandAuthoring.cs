using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Offline prefab authoring. Runtime uses the existing resource loader and progress events.
    public static class LoadingBrandAuthoring
    {
        const string Artwork = "Assets/BubblePics/Resources/Loading/loading_bubblosaic.png";
        const string Resource = "Loading/loading_bubblosaic";
        const string Prefab = "Assets/BizzaWZ/Common/MenuSystem/Common/LoadingPanel/LoadingPanel.prefab";

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before updating the loading artwork.");

            AssetDatabase.ImportAsset(Artwork, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Artwork);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var format = importer.GetPlatformTextureSettings(platform);
                format.overridden = true;
                format.maxTextureSize = 2048;
                format.format = TextureImporterFormat.ASTC_4x4;
                format.compressionQuality = 100;
                importer.SetPlatformTextureSettings(format);
            }
            importer.SaveAndReimport();

            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                Transform background = null;
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    if (node.name == "ReferenceLoading") background = node.Find("Background");
                if (background == null) throw new InvalidOperationException("Loading artwork node is missing.");

                // The new background contains no baked loading caption. Keep the live,
                // localized TMP caption visible in every language instead of swapping atlases.
                var caption = background.GetComponent<ApprovedHudCaption>();
                if (caption != null)
                {
                    var data = new SerializedObject(caption);
                    var label = (TMP_Text)data.FindProperty("_label").objectReferenceValue;
                    var group = (CanvasGroup)data.FindProperty("_captionGroup").objectReferenceValue;
                    if (label != null) label.alpha = 1f;
                    if (group != null) group.alpha = 1f;
                    UnityEngine.Object.DestroyImmediate(caption);
                }
                var source = new SerializedObject(background.GetComponent<CoralResourceSprite>());
                source.FindProperty("_resourcePath").stringValue = Resource;
                source.FindProperty("_spriteName").stringValue = "";
                source.ApplyModifiedPropertiesWithoutUndo();
                var image = background.GetComponent<Image>();
                image.sprite = null;
                image.material = null;
                image.color = Color.white;
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            PlayerSettings.productName = "Bubblosaic: Picture Merge";
            AssetDatabase.SaveAssets();
        }
    }
}
