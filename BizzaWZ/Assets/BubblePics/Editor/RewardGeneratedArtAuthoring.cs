using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;

namespace BubblePics.EditorTools
{
    // Offline authoring: generated transparent assets remain intact. Sprite rects
    // exclude only transparent canvas padding; no screenshot extraction or masking.
    public static class RewardGeneratedArtAuthoring
    {
        const string Root = "Assets/BubblePics/Resources/RewardArt20260930/";
        const string Resource = "RewardArt20260930/";
        static readonly string[] Names = { "RewardTitle", "RewardLevel", "ProgressTrack", "ProgressFill", "ProgressPanel" };

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before authoring generated art.");
            foreach (string name in Names) Import(name);
            RewardStructureAuthoring.Apply();
        }

        static void Import(string name)
        {
            string path = Root + name + ".png";
            if (!File.Exists(path)) throw new FileNotFoundException("Generated sprite required", path);
            // Read alpha solely to configure the sprite's canvas bounds. Do not
            // alter the PNG, recolor its edges, or manufacture a replacement matte.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect bounds;
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Invalid PNG: " + path);
                int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
                var pixels = texture.GetPixels32();
                for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a < 128) continue;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
                if (maxX < minX || maxY < minY) throw new InvalidOperationException("Empty sprite: " + name);
                if (minX == 0 && minY == 0 && maxX == texture.width - 1 && maxY == texture.height - 1)
                    throw new InvalidOperationException("Sprite must have an isolated transparent canvas: " + name);
                bounds = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = name == "RewardLevel" ? 512 : 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var format = importer.GetPlatformTextureSettings(platform);
                format.overridden = true; format.maxTextureSize = importer.maxTextureSize;
                format.format = TextureImporterFormat.ASTC_4x4; format.compressionQuality = 100;
                importer.SetPlatformTextureSettings(format);
            }
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var prior = provider.GetSpriteRects();
            GUID id = prior.Length == 1 && prior[0].name == name ? prior[0].spriteID : GUID.Generate();
            Vector4 border = Vector4.zero;
            if (name == "ProgressTrack" || name == "ProgressFill")
                border = new Vector4(bounds.height * .55f, 0, bounds.height * .55f, 0);
            else if (name == "ProgressPanel")
                border = Vector4.one * bounds.height * .25f;
            provider.SetSpriteRects(new[] { new SpriteRect { name = name, rect = bounds, border = border,
                spriteID = id, alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) } });
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                new[] { new SpriteNameFileIdPair(name, id) });
            provider.Apply(); importer.SaveAndReimport();
        }

        public static void Configure(GameObject root)
        {
            var content = root.transform.Find("Content");
            var header = content.Find("Header");
            // Remove the old polygon approximation. A normal Image now displays
            // the generated asset's own antialiased transparent silhouette.
            var old = header.GetComponent<ApprovedContourImage>();
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            SpriteImage(header, "RewardTitle", false);
            SpriteImage(content.Find("LevelRoot/Level"), "RewardLevel", false);
            var ad = content.Find("AdBonusProgress");
            SpriteImage(ad.Find("AdBonusCard"), "ProgressPanel", true);
            ConfigureBar(ad);
            foreach (string name in new[] { "WithdrawalProgress", "RealWithdrawalProgress" })
            {
                var progress = content.Find(name);
                SpriteImage(progress.Find("ProgressCard"), "ProgressPanel", true);
                ConfigureBar(progress);
            }
        }

        static void ConfigureBar(Transform parent)
        {
            SpriteImage(parent.Find("Track"), "ProgressTrack", true);
            var fill = SpriteImage(parent.Find("Fill"), "ProgressFill", true);
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            var effect = Ensure<WithdrawCloudProgressFill>(fill);
            Bind(effect, "sourceImage", fill);
        }

        static Image SpriteImage(Transform target, string name, bool sliced)
        {
            var image = Ensure<Image>(target);
            image.sprite = null; image.material = null; image.color = Color.white;
            image.raycastTarget = false; image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = !sliced;
            Sprite sprite = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Root + name + ".png"))
                if (asset is Sprite candidate && candidate.name == name) { sprite = candidate; break; }
            if (sprite == null) throw new InvalidOperationException("Import generated sprite first: " + name);
            // Texture max-size import scales both Sprite.rect and Sprite.pixelsPerUnit.
            // Normalize against the production Canvas' 100 reference pixels/unit;
            // otherwise the caps double in size and collapse the panel center.
            if (sliced) image.pixelsPerUnitMultiplier = sprite.rect.height * 100f /
                (sprite.pixelsPerUnit * image.rectTransform.rect.height);
            var data = new SerializedObject(Ensure<CoralResourceSprite>(target));
            data.FindProperty("_resourcePath").stringValue = Resource + name;
            data.FindProperty("_spriteName").stringValue = name;
            data.FindProperty("_image").objectReferenceValue = image;
            data.ApplyModifiedPropertiesWithoutUndo();
            return image;
        }
    }
}
