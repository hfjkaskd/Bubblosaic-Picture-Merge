using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static class PageAspectAuthoring
    {
        const float Width = 2360f * 1080 / 2340;
        const string Output = "../Validation/PageAspect-20260929";
        static readonly string[] Primary = {
            "Assets/BizzaWZ/Final/Real/UI/GetRewardPanel/GetRewardPanel.prefab",
            "Assets/BizzaWZ/Final/Real/UI/FakeWithdrawPanel/FakeWithdrawPanel.prefab"
        };

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Output + "/backup");
            foreach (string path in Primary)
            {
                string backup = Output + "/backup/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Configure(root);
                    ReferencePrefabTools.Validate(root.transform);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var plan = JsonUtility.FromJson<AllUiAuthoring.Plan>(File.ReadAllText("../ArtChanges/AllUI-20260924/plan.json"));
            var backgrounds = new Dictionary<string,string> {
                {"UIWithdrawalPendingPanel","ReferenceBackdrop"}, {"WithdrawHistory","ReferenceBackdrop"},
                {"FAQPanel","ReferenceBackdrop"}, {"WithdrawDanPanel","ReferenceBackdrop"},
                {"LoadingPanel","SplashPage/SplashMount/SplashRoot/ReferenceLoading/Background"},
                {"UIDailyTaskPage","Background"}, {"NewbieGiftPage","Backdrop"},
                {"SlotPanel","SequentialBackground"}, {"SlotFAQPanel","Background"}, {"ServicePanel","BG"}
            };
            foreach (var page in plan.items)
            {
                if (!backgrounds.TryGetValue(page.id, out string backgroundPath)) continue;
                string backup = Output + "/backup/" + Path.GetFileName(page.prefab);
                if (!File.Exists(backup)) File.Copy(page.prefab, backup);
                var root = PrefabUtility.LoadPrefabContents(page.prefab);
                try
                {
                    Transform backdrop = root.transform.Find(backgroundPath);
                    if (backdrop == null) throw new InvalidOperationException("Missing background: " + page.id);
                    ConfigureLayer(backdrop.parent.gameObject, backdrop, "AspectContent", page.id != "UIDailyTaskPage");
                    ReferencePrefabTools.Validate(root.transform);
                    PrefabUtility.SaveAsPrefabAsset(root, page.prefab);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Audit();
        }

        public static void Configure(GameObject root)
        {
            if (root.GetComponent<GetRewardPanel>() != null && root.transform.Find("Content/RewardRow") != null)
            {
                RewardStructureAuthoring.Configure(root);
                return;
            }
            var backdrop = root.transform.Find("Backdrop");
            if (backdrop == null) throw new InvalidOperationException("Missing authored Backdrop: " + root.name);
            ConfigureLayer(root, backdrop, "Content", true);
            if (root.GetComponent<GetRewardPanel>() != null)
            {
                // Transparent reward backdrop still owns the full-screen modal input barrier.
                backdrop.GetComponent<Image>().raycastTarget = true;
                // The legacy root scale animation shrinks the viewport and exposes edge clicks.
                root.GetComponent<Animation>().playAutomatically = false;
                RewardBackdropAuthoring.Configure(root);
            }
        }

        static void ConfigureLayer(GameObject root, Transform backdrop, string contentName, bool containsArtwork)
        {
            var content = root.transform.Find(contentName) as RectTransform;
            if (content == null)
            {
                var children = new List<Transform>();
                foreach (Transform child in root.transform) if (child != backdrop) children.Add(child);
                content = (RectTransform)ReferencePrefabTools.Child(root.transform, contentName);
                content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
                content.sizeDelta = new Vector2(Width, 2360);
                content.anchoredPosition = Vector2.zero;
                foreach (var child in children) child.SetParent(content, false);
            }
            ReferencePrefabTools.Stretch(backdrop);
            if (containsArtwork)
            {
                var underlay = root.transform.Find("BackgroundUnderlay");
                if (underlay == null) underlay = ReferencePrefabTools.Child(root.transform, "BackgroundUnderlay");
                ReferencePrefabTools.Stretch(underlay);
                var underlayImage = ReferencePrefabTools.Ensure<Image>(underlay);
                underlayImage.raycastTarget = false;
                string resource = (root.name.Contains("Withdraw") || root.name.Contains("FAQPanel") && !root.name.Contains("Slot") || root.name.Contains("History"))
                    ? "AllUI20260924/WithdrawBackdrop" : "CoralV3/CoralBackgroundReference";
                var binding = new SerializedObject(ReferencePrefabTools.Ensure<CoralResourceSprite>(underlay));
                binding.FindProperty("_resourcePath").stringValue = resource;
                binding.FindProperty("_spriteName").stringValue = "";
                binding.FindProperty("_image").objectReferenceValue = underlayImage;
                binding.ApplyModifiedPropertiesWithoutUndo();
                underlayImage.sprite = null;
                var aspect = ReferencePrefabTools.Ensure<AspectRatioFitter>(underlay);
                aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                var sprite = CoralResourceSprite.Load(resource);
                aspect.aspectRatio = sprite.rect.width / sprite.rect.height;
                var extension = ReferencePrefabTools.Ensure<BackdropEdgeExtension>(backdrop);
                var settings = new SerializedObject(extension);
                settings.FindProperty("sourceImage").objectReferenceValue = backdrop.GetComponent<Image>();
                settings.FindProperty("contentAspect").floatValue = Width / 2360;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            var fitter = ReferencePrefabTools.Ensure<PageContentFit>(root.transform);
            var data = new SerializedObject(fitter);
            data.FindProperty("content").objectReferenceValue = content;
            data.ApplyModifiedPropertiesWithoutUndo();
            backdrop.SetAsFirstSibling(); content.SetAsLastSibling();
            var background = root.transform.Find("BackgroundUnderlay");
            if (background != null) background.SetAsFirstSibling();
        }

        public static void Audit()
        {
            Directory.CreateDirectory(Output);
            var plan = JsonUtility.FromJson<AllUiAuthoring.Plan>(File.ReadAllText("../ArtChanges/AllUI-20260924/plan.json"));
            var seen = new HashSet<string>(); var log = new StringBuilder();
            foreach (var page in plan.items)
            {
                if (!seen.Add(page.prefab) || !File.Exists(page.prefab)) continue;
                var root = PrefabUtility.LoadPrefabContents(page.prefab);
                try
                {
                    var rt = (RectTransform)root.transform;
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 2360f * 9 / 16);
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2360);
                    foreach (var image in root.GetComponentsInChildren<Image>(true))
                    {
                        string name = image.name.ToLowerInvariant();
                        if (!(name.Contains("backdrop") || name.Contains("background") || name == "bg")) continue;
                        var corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
                        Vector3 min = rt.InverseTransformPoint(corners[0]), max = rt.InverseTransformPoint(corners[2]);
                        if (max.y - min.y < 1900) continue;
                        bool covers = min.x <= rt.rect.xMin + 1 && max.x >= rt.rect.xMax - 1 && min.y <= rt.rect.yMin + 1 && max.y >= rt.rect.yMax - 1;
                        var sprite = image.GetComponent<CoralResourceSprite>();
                        var source = sprite != null ? new SerializedObject(sprite).FindProperty("_resourcePath").stringValue : "direct/other";
                        log.AppendLine(page.id + " | " + AnimationUtility.CalculateTransformPath(image.transform, root.transform) + " | active=" + image.gameObject.activeSelf + " enabled=" + image.enabled + " | covers=" + covers + " | size=" + (max-min) + " | resource=" + source);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            File.WriteAllText(Output + "/background-audit.txt", log.ToString());
        }
    }
}
