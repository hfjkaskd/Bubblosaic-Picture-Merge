using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;

namespace BubblePics.EditorTools
{
    // Restore the original reward hierarchy while retaining the current ocean art.
    // All visual structure is authored here and serialized into the production prefab.
    public static class RewardStructureAuthoring
    {
        const string Prefab = "Assets/BizzaWZ/Final/Real/UI/GetRewardPanel/GetRewardPanel.prefab";

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before authoring.");
            var root = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                Configure(root);
                Validate(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../Validation/ClassicReward-20260930");
            File.WriteAllText("../Validation/ClassicReward-20260930/authoring.txt",
                "Original reward/settlement structure; current ocean art and runtime reward bindings retained.");
        }

        public static void Configure(GameObject root)
        {
            var page = root.GetComponent<GetRewardPanel>();
            var content = root.transform.Find("Content");
            Stretch(root.transform);
            Place(content, 0, 0, 1080, 1920);
            Bind(Ensure<PageContentFit>(root.transform), "content", content);
            var animation = root.GetComponent<Animation>();
            if (animation != null) animation.playAutomatically = false;

            var backdrop = root.transform.Find("Backdrop");
            Stretch(backdrop);
            var barrier = backdrop.GetComponent<Image>();
            barrier.sprite = null; barrier.material = null;
            barrier.color = new Color(0, .035f, .085f, .72f);
            barrier.raycastTarget = true; barrier.alphaHitTestMinimumThreshold = 0;
            barrier.enabled = true;
            Remove(root.transform, "BackgroundUnderlay");
            Remove(content, "RewardDragon");
            Remove(content, "Panel");
            Remove(content, "RewardTitle");
            Remove(content, "ReferenceRewardTitle");
            foreach (var caption in root.GetComponentsInChildren<ApprovedHudCaption>(true))
            {
                var data = new SerializedObject(caption);
                var surface = data.FindProperty("_surface").objectReferenceValue as Image;
                var label = data.FindProperty("_label").objectReferenceValue as TMP_Text;
                var group = data.FindProperty("_captionGroup").objectReferenceValue as CanvasGroup;
                if (group != null) group.alpha = 1;
                if (label != null) label.alpha = 1;
                if (surface != null && surface.gameObject != caption.gameObject)
                    UnityEngine.Object.DestroyImmediate(surface.gameObject);
                UnityEngine.Object.DestroyImmediate(caption);
            }
            Remove(content, "ReferenceHeader");
            Remove(page.LevelObj.transform, "ReferenceLevel");
            Remove(page.claimBtn.transform, "ReferenceClaim");
            Remove(page.closeBtn.transform, "ReferenceCollect");
            Place(content.Find("Header"), 0, 650, 800, 210);
            var title = content.Find("HeaderCaption").GetComponent<TMP_Text>();
            Place(title.transform, 0, 654, 700, 110);
            title.margin = Vector4.zero;
            title.fontSize = title.fontSizeMax = 62;
            title.fontSizeMin = 38;
            title.enableAutoSizing = true;
            var localized = title.GetComponent<CoralLocalizedLabel>();
            if (localized != null) UnityEngine.Object.DestroyImmediate(localized);
            // OnOpen owns these stateful captions. In particular, enabling the
            // delayed Collect button must not overwrite its normal-reward amount.
            foreach (var label in new[] { page.rewardText, page.noThanksText })
            {
                var localization = label.GetComponent<CoralLocalizedLabel>();
                if (localization != null) UnityEngine.Object.DestroyImmediate(localization);
            }
            Bind(page, "titleText", title);
            var pageData = new SerializedObject(page);
            pageData.FindProperty("alwaysShowLevel").boolValue = false;
            pageData.FindProperty("normalCollectRevealDelay").floatValue = 3;
            pageData.ApplyModifiedPropertiesWithoutUndo();

            Place(page.LevelObj.transform, 0, 523, 360, 100);
            Place(page.LevelObj.transform.Find("Level"), 0, 0, 360, 98.4f);
            Place(page.levelTxt.transform, 0, 0, 290, 70);
            page.levelTxt.fontSize = page.levelTxt.fontSizeMax = 44;

            var row = content.Find("RewardRow") ?? Child(content, "RewardRow");
            Place(row, 0, 235, 930, 420);
            var layout = Ensure<HorizontalLayoutGroup>(row);
            layout.childAlignment = TextAnchor.MiddleCenter; layout.spacing = 40;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var coins = row.Find("CoinReward") ?? Child(row, "CoinReward");
            var cash = row.Find("CashReward") ?? content.Find("CashReward");
            cash.SetParent(row, false);
            Place(coins, 0, 0, 410, 420); Place(cash, 0, 0, 410, 420);
            var coinImage = coins.Find("Coins") ?? content.Find("Coins");
            coinImage.SetParent(coins, false);
            Place(coinImage, 0, 0, 410, 391);
            Place(cash.Find("Cash"), 0, 0, 410, 391);
            page.itemATxt.transform.SetParent(coins, false);
            Place(page.itemATxt.transform, 0, -83, 340, 95);
            page.itemATxt.fontSize = page.itemATxt.fontSizeMax = 76;
            var unit = coins.Find("CoinsUnitCaption") ?? content.Find("CoinsUnitCaption");
            unit.SetParent(coins, false); Place(unit, 0, -147, 310, 46);
            var unitLabel = unit.GetComponent<TMP_Text>(); unitLabel.fontSize = unitLabel.fontSizeMax = 34;
            Place(page.itemBTxt.transform, 0, -105, 350, 100);
            page.itemBTxt.fontSize = page.itemBTxt.fontSizeMax = 66;
            page.itemAPos = page.itemATxt.transform; page.itemBPos = page.itemBTxt.transform;
            page.MaxDollarTip.transform.SetParent(coins, false);
            Place(page.MaxDollarTip.transform, 122, 145, 104, 52);
            Place(page.MaxDollarTip.transform.Find("MaxBadge"), 0, 0, 104, 52);
            Place(page.MaxDollarTip.transform.Find("MaxLabel"), 0, 0, 96, 45);
            coins.SetAsFirstSibling(); cash.SetAsLastSibling();

            Place(page.claimBtn.transform, 0, -120, 720, 181);
            Place(page.rewardText.transform, 83, 0, 430, 108);
            page.rewardText.fontSize = page.rewardText.fontSizeMax = 62;
            page.claimBtn.GetComponent<Image>().preserveAspect = true;
            Place(page.closeBtn.transform, 0, -290, 505, 117);
            Place(page.noThanksText.transform, 0, 0, 460, 91);
            page.noThanksText.fontSize = page.noThanksText.fontSizeMax = 46;
            page.closeBtn.GetComponent<Image>().preserveAspect = true;
            Place(page.bonusRate.transform, 310, -47, 136, 66);
            Place(page.bonusRate.transform.Find("RateBadge"), 0, 0, 136, 66);
            Place(page.bonusRate.bonusRateTxt.transform, 0, 0, 124, 55);

            var ad = content.Find("AdBonusProgress");
            Place(ad, 0, -496, 880, 218);
            Place(ad.Find("AdBonusCard"), 0, 0, 880, 218);
            Place(ad.Find("AdBonusTitle"), 0, 66, 780, 40);
            Place(ad.Find("CurrentBonus"), -338, 10, 145, 45);
            Place(ad.Find("NextBonus"), 338, 10, 145, 45);
            Place(ad.Find("CurrentBonusCaption"), -338, -34, 160, 34);
            Place(ad.Find("NextBonusCaption"), 338, -34, 160, 34);
            Place(ad.Find("Track"), 0, 10, 500, 48);
            Place(ad.Find("Fill"), 0, 10, 480, 29);
            Place(ad.Find("VideoCount"), 0, 10, 480, 48);
            Place(ad.Find("VideoHint"), 0, -74, 820, 44);
            foreach (var label in ad.GetComponentsInChildren<TMP_Text>(true))
            {
                float size = label.name.Contains("Caption") ? 23 : label.name == "VideoHint" ? 27 : 32;
                label.fontSize = label.fontSizeMax = size;
            }
            Progress(content.Find("WithdrawalProgress"));
            Progress(content.Find("RealWithdrawalProgress"));
            RewardGeneratedArtAuthoring.Configure(root);
            foreach (var image in content.GetComponentsInChildren<Image>(true))
                image.raycastTarget = image.GetComponent<Button>() != null;
            foreach (var label in content.GetComponentsInChildren<TMP_Text>(true)) label.raycastTarget = false;
            backdrop.SetAsFirstSibling(); content.SetAsLastSibling();
        }

        static void Progress(Transform progress)
        {
            Place(progress, 0, -726, 880, 170);
            Place(progress.Find("ProgressCard"), 0, 0, 880, 170);
            Place(progress.Find("ProgressCaption"), -40, 48, 690, 38);
            Place(progress.Find("PaymentMethod"), 369, 48, 58, 38);
            Place(progress.Find("Track"), 0, -4, 784, 48);
            Place(progress.Find("Fill"), 0, -4, 764, 29);
            var percent = progress.Find("Percent"); if (percent != null) Place(percent, 0, -4, 720, 48);
            Place(progress.Find("WithdrawalHint"), 0, -55, 820, 44);
            foreach (var label in progress.GetComponentsInChildren<TMP_Text>(true))
                label.fontSize = label.fontSizeMax = label.name == "ProgressCaption" ? 31 : label.name == "WithdrawalHint" ? 24 : 27;
        }

        static void Place(Transform target, float x, float y, float width, float height)
        {
            if (target == null) throw new InvalidOperationException("Missing original reward binding.");
            var rect = (RectTransform)target;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.localScale = Vector3.one; rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void Remove(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

    }
}
