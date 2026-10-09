using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Offline authoring only: all layout and visuals are saved in the prefab.
    public static class RewardDisplayRepair
    {
        const string Asset = "Assets/BubblePics/Resources/RewardDisplay20260929/Bubbles.png";
        const string Resource = "RewardDisplay20260929/Bubbles";
        const float Width = 2360f * 1080 / 2340;

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before repairing the prefab.");
            ImportBubbles();
            const string prefab = "Assets/BizzaWZ/Final/Real/UI/GetRewardPanel/GetRewardPanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                ConfigurePrefab(root);
                ReferencePrefabTools.Validate(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath("../Validation/RewardDisplayFix-20260929/repair.txt"),
                "PASS responsive anchors; clean reward bubbles; live coin caption; native Button bindings preserved; " + DateTime.UtcNow.ToString("O"));
        }

        static void ImportBubbles()
        {
            AssetDatabase.ImportAsset(Asset, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Asset);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true; settings.maxTextureSize = 1024;
                settings.format = TextureImporterFormat.ASTC_4x4; settings.compressionQuality = 100;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var prior = provider.GetSpriteRects();
            var rects = new List<SpriteRect>(); var names = new List<SpriteNameFileIdPair>();
            for (int i = 0; i < 2; i++)
            {
                string name = i == 0 ? "Coins" : "Cash";
                GUID guid = GUID.Generate(); foreach (var old in prior) if (old.name == name) guid = old.spriteID;
                rects.Add(new SpriteRect { name = name, spriteID = guid,
                    rect = new Rect(i == 0 ? 78 : 906, 66, 790, 754), alignment = SpriteAlignment.Center, pivot = new Vector2(.5f,.5f) });
                names.Add(new SpriteNameFileIdPair(name, guid));
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);
            provider.Apply(); importer.SaveAndReimport();
        }

        public static void ConfigurePrefab(GameObject root)
        {
            PageAspectAuthoring.Configure(root);
            var content = root.transform.Find("Content");
            MakeWidthResponsive((RectTransform)content, Width);
            foreach (string path in new[] { "Coins", "CashReward/Cash" })
            {
                var node = content.Find(path);
                var image = node.GetComponent<Image>(); image.preserveAspect = true; image.material = null;
                var data = new SerializedObject(node.GetComponent<CoralResourceSprite>());
                data.FindProperty("_resourcePath").stringValue = Resource;
                data.FindProperty("_spriteName").stringValue = path == "Coins" ? "Coins" : "Cash";
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var page = root.GetComponent<GetRewardPanel>();
            var coinPosition = page.itemATxt.rectTransform.anchoredPosition;
            coinPosition.y = (1852f / 2 - 968.5f) * 2360 / 1852 - 10;
            page.itemATxt.rectTransform.anchoredPosition = coinPosition;
            var cashPosition = page.itemBTxt.rectTransform.anchoredPosition;
            cashPosition.y = (1852f / 2 - 973) * 2360 / 1852 - 24;
            page.itemBTxt.rectTransform.anchoredPosition = cashPosition;
            // Native text replaces the small caption previously painted into the bitmap.
            var caption = content.Find("CoinsUnitCaption");
            if (caption == null)
            {
                caption = ReferencePrefabTools.Child(content, "CoinsUnitCaption");
                var rect = (RectTransform)caption;
                rect.anchorMin = new Vector2(185f / 849, .5f); rect.anchorMax = new Vector2(352f / 849, .5f);
                rect.pivot = new Vector2(.5f,.5f); rect.sizeDelta = new Vector2(0, 42f * 2360 / 1852);
                rect.anchoredPosition = new Vector2(0, (1852f / 2 - 1026) * 2360 / 1852);
                var source = root.GetComponent<GetRewardPanel>().itemATxt;
                var label = caption.gameObject.AddComponent<TextMeshProUGUI>();
                caption.gameObject.AddComponent<PreserveAuthoredFont>();
                label.font = source.font; label.fontSharedMaterial = source.fontSharedMaterial;
                label.fontSize = label.fontSizeMax = 32f * 2360 / 1852; label.fontSizeMin = 28;
                label.enableAutoSizing = true; label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(.51f,.23f,.015f); label.raycastTarget = false;
                label.enableWordWrapping = false;
                ReferencePrefabTools.Localize(label, "coins");
            }
        }

        static void MakeWidthResponsive(RectTransform parent, float authoredWidth)
        {
            foreach (RectTransform rect in parent)
            {
                float left = rect.anchorMin.x * authoredWidth + rect.offsetMin.x;
                float right = rect.anchorMax.x * authoredWidth + rect.offsetMax.x;
                float width = right - left;
                var min = rect.anchorMin; var max = rect.anchorMax;
                min.x = left / authoredWidth; max.x = right / authoredWidth;
                rect.anchorMin = min; rect.anchorMax = max;
                var size = rect.sizeDelta; size.x = 0; rect.sizeDelta = size;
                var position = rect.anchoredPosition; position.x = 0; rect.anchoredPosition = position;
                if (rect.childCount > 0 && width > .01f) MakeWidthResponsive(rect, width);
            }
        }
    }
}
