using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static class RewardBackdropAuthoring
    {
        const string Prefab = "Assets/BizzaWZ/Final/Real/UI/GetRewardPanel/GetRewardPanel.prefab";
        const string Art = "Assets/BubblePics/Resources/RewardForeground20260930/Dragon.png";

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            AssetDatabase.ImportAsset(Art, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.filterMode = FilterMode.Bilinear;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = 1024; android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void Configure(GameObject root)
        {
            var background = root.transform.Find("Backdrop");
            var image = background.GetComponent<Image>();
            var binding = background.GetComponent<CoralResourceSprite>();
            if (binding != null) UnityEngine.Object.DestroyImmediate(binding);
            var extension = background.GetComponent<BackdropEdgeExtension>();
            if (extension != null) UnityEngine.Object.DestroyImmediate(extension);
            image.sprite = null;
            image.material = null;
            image.color = Color.clear;
            image.raycastTarget = true;
            image.alphaHitTestMinimumThreshold = 0;
            image.enabled = true;
            ReferencePrefabTools.Stretch(background);
            var underlay = root.transform.Find("BackgroundUnderlay");
            if (underlay != null) UnityEngine.Object.DestroyImmediate(underlay.gameObject);

            var content = root.transform.Find("Content");
            var dragon = content.Find("RewardDragon");
            if (dragon == null) dragon = ReferencePrefabTools.Child(content, "RewardDragon");
            var rect = (RectTransform)dragon;
            // Keep the illustration on the panel's upper edge; layout remains authored and aspect-preserving.
            const float scale = 2360f / 1852f;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(281f - 849f / 2f, 1852f / 2f - 466f) * scale;
            rect.sizeDelta = new Vector2(435f, 391f) * scale;
            rect.localScale = Vector3.one;
            var character = ReferencePrefabTools.Ensure<Image>(dragon);
            character.preserveAspect = true; character.raycastTarget = false;
            character.sprite = null; character.color = Color.white;
            var data = new SerializedObject(ReferencePrefabTools.Ensure<CoralResourceSprite>(dragon));
            data.FindProperty("_resourcePath").stringValue = "RewardForeground20260930/Dragon";
            data.FindProperty("_spriteName").stringValue = "";
            data.FindProperty("_image").objectReferenceValue = character;
            data.ApplyModifiedPropertiesWithoutUndo();
            dragon.SetAsFirstSibling();
        }
    }
}
