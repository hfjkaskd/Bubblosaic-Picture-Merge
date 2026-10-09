using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static class WithdrawalServiceButtonAuthoring
    {
        public const string Shared = "Assets/BubblePics/RuntimePrefabs/UI/WithdrawalServiceButton.prefab";
        public const string Starter = "Assets/BizzaWZ/Final/Real/UI/FakeWithdrawPanel/FakeWithdrawPanel.prefab";
        public const string Coins = "Assets/BizzaWZ/Final/Real/UI/RealWithdrawalPanl/RealWithdrawPanel.prefab";

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Shared) == null)
            {
                var source = PrefabUtility.LoadPrefabContents(Starter);
                GameObject button = null;
                try
                {
                    button = UnityEngine.Object.Instantiate(source.GetComponentInChildren<ServiceBtn>(true).gameObject);
                    button.name = "WithdrawalServiceButton";
                    var rect = (RectTransform)button.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, .46f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = new Vector2(-94f, 0);
                    rect.sizeDelta = new Vector2(140f, 140f);
                    rect.localScale = Vector3.one;
                    Place((RectTransform)button.transform.Find("ChatBubble"), new Vector2(.5f,.5f), new Vector2(0,-2), new Vector2(74,72));
                    Place((RectTransform)button.transform.Find("RedDot"), new Vector2(.82f,.82f), Vector2.zero, new Vector2(34,34));
                    foreach (var image in button.GetComponentsInChildren<Image>(true))
                    {
                        image.preserveAspect = true;
                        image.raycastTarget = image.gameObject == button;
                        image.sprite = null; // Preserve the existing lazy resource bindings.
                    }
                    PrefabUtility.SaveAsPrefabAsset(button, Shared);
                }
                finally
                {
                    if (button != null) UnityEngine.Object.DestroyImmediate(button);
                    PrefabUtility.UnloadPrefabContents(source);
                }
            }
            var shared = PrefabUtility.LoadPrefabContents(Shared);
            try
            {
                Place((RectTransform)shared.transform, new Vector2(1f,.5f), new Vector2(-78f,0), new Vector2(132,132));
                PrefabUtility.SaveAsPrefabAsset(shared, Shared);
            }
            finally { PrefabUtility.UnloadPrefabContents(shared); }
            foreach (var path in new[] { Starter, Coins })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try { Configure(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }

        public static void Configure(GameObject page)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Shared);
            if (prefab == null) throw new InvalidOperationException("Author the shared withdrawal service button first.");
            foreach (var old in page.GetComponentsInChildren<ServiceBtn>(true))
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            // A viewport-level sibling stays visible while the form/tiers scroll or fit uniformly.
            var button = (GameObject)PrefabUtility.InstantiatePrefab(prefab, page.transform);
            button.name = page.GetComponent<FakeWithdrawPanel>() != null ? "Service" : "ServiceBtn";
            button.transform.SetAsLastSibling();
            var native = button.GetComponent<Button>();
            if (native == null || native.targetGraphic.gameObject != button || native.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("The visible service control must own its standard Button.");
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
        }
    }
}
