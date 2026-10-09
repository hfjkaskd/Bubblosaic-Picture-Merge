using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Asset authoring only: visible header icons are also the production collection targets.
    public static class SlotRewardFlightAuthoring
    {
        const string Root = "Assets/BubblePics/Resources/SequentialUI20260928/SlotPanel/";
        const string Resource = "SequentialUI20260928/SlotPanel/ApprovedSource";

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Author reward targets in Edit Mode.");
            var a = new ReferencePrefabTools("SlotPanel", Root + "ApprovedSource.png", 849, 1852);
            a.Slice("CoinIcon", 79, 270, 148, 113).Slice("CashIcon", 469, 270, 130, 120);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "ApprovedSource.png");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.Tight;
            settings.spriteExtrude = 0;
            importer.SetTextureSettings(settings);
            var factory = new SpriteDataProviderFactories(); factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
            var rects = new List<SpriteRect>(provider.GetSpriteRects());
            var outlines = provider.GetDataProvider<ISpriteOutlineDataProvider>();
            foreach (string name in new[] { "CoinIcon", "CashIcon" })
            {
                Rect bounds = a.R(name);
                SpriteRect entry = rects.Find(r => r.name == name);
                if (entry == null) { entry = new SpriteRect { name = name, spriteID = GUID.Generate() }; rects.Add(entry); }
                entry.rect = new Rect(bounds.x, 1852 - bounds.yMax, bounds.width, bounds.height);
                entry.pivot = new Vector2(.5f, .5f); entry.alignment = SpriteAlignment.Center;
            }
            provider.SetSpriteRects(rects.ToArray());
            var pairs = new List<SpriteNameFileIdPair>();
            foreach (var entry in rects) pairs.Add(new SpriteNameFileIdPair(entry.name, entry.spriteID));
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
            foreach (var entry in rects)
            {
                if (entry.name != "CoinIcon" && entry.name != "CashIcon") continue;
                Vector2[] points = SequentialContourAuthoring.Read("Slot" + entry.name);
                for (int i = 0; i < points.Length; i++)
                    points[i] = new Vector2((points[i].x - .5f) * entry.rect.width, (.5f - points[i].y) * entry.rect.height);
                outlines.SetOutlines(entry.spriteID, new List<Vector2[]> { points });
            }
            provider.Apply(); importer.SaveAndReimport();
            var go = PrefabUtility.LoadPrefabContents(SequentialSlotAuthoring.PathName);
            try
            {
                var panel = go.GetComponent<SlotPanel>();
                foreach (string name in new[] { "CoinIcon", "CashIcon" })
                {
                    var t = go.transform.Find("Sequential" + name);
                    if (t == null) t = ReferencePrefabTools.Child(go.transform, "Sequential" + name);
                    a.Place(t, a.R(name));
                    var image = a.Visual(t, name);
                    image.material = null; image.useSpriteMesh = true; image.preserveAspect = true;
                    if (name == "CoinIcon") panel.slotRewardPanel.coinTargetPos = t;
                    else panel.slotRewardPanel.dollarTargetPos = t;
                }
                panel.slotRewardPanel.transform.SetAsLastSibling();
                PrefabUtility.SaveAsPrefabAsset(go, SequentialSlotAuthoring.PathName);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
            AssetDatabase.SaveAssets();
        }
    }
}
