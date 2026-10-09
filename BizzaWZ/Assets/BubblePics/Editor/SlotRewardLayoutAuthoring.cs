using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static class SlotRewardLayoutAuthoring
    {
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Author reward layout in Edit Mode.");
            var root = PrefabUtility.LoadPrefabContents(SequentialSlotAuthoring.PathName);
            try
            {
                Configure(root.GetComponent<SlotPanel>().slotRewardPanel);
                PrefabUtility.SaveAsPrefabAsset(root, SequentialSlotAuthoring.PathName);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        // Static prefab authoring only; the standard layout group ignores hidden rewards.
        public static void Configure(SlotRewardPanel panel)
        {
            if (panel.transform.Find("RewardCards") != null) return;
            var coin = (RectTransform)panel.coinObj.transform;
            var cash = (RectTransform)panel.dollarObj.transform;
            float spacing = cash.anchoredPosition.x - coin.anchoredPosition.x
                - (coin.rect.width + cash.rect.width) * .5f;
            var plate = (RectTransform)panel.transform.Find("Panel");
            var row = new GameObject("RewardCards", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.layer = panel.gameObject.layer;
            var rect = (RectTransform)row.transform;
            rect.SetParent(panel.transform, false);
            rect.SetSiblingIndex(coin.GetSiblingIndex());
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(plate.anchoredPosition.x, coin.anchoredPosition.y);
            rect.sizeDelta = new Vector2(coin.rect.width + cash.rect.width + spacing,
                Mathf.Max(coin.rect.height, cash.rect.height));
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childScaleWidth = layout.childScaleHeight = false;
            coin.SetParent(rect, true);
            cash.SetParent(rect, true);
        }
    }
}
