using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static class WithdrawalGuideAuthoring
    {
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before authoring the withdrawal guide.");
            const string widget = "Assets/BizzaWZ/Final/Real/UI/GamePanel/Resources/Resources/GameUiWidget.prefab";
            Mark(widget, "GameplayHudHeader/CurrencyBar/CurrentGroup/DollarGroup/FakeBtn", "withdraw-cash");
            Mark(widget, "GameplayHudHeader/CurrencyBar/CurrentGroup/GoldGroup/RealBtn", "withdraw-coins");
            Mark("Assets/BizzaWZ/Final/Real/UI/FakeWithdrawPanel/FakeWithdrawPanel.prefab",
                "Content/Withdraw", "withdraw-starter");

            const string tips = "Assets/BizzaWZ/Final/Framework/Runtime/Module/Teach/UITeach/UITeachTipsPage.prefab";
            var root = PrefabUtility.LoadPrefabContents(tips);
            try
            {
                // The authored ModalOverlay is the only backdrop. The legacy root Image
                // dimmed the highlighted button and intercepted all input underneath it.
                var legacyBackdrop = root.GetComponent<Image>();
                if (legacyBackdrop != null) UnityEngine.Object.DestroyImmediate(legacyBackdrop);
                var data = new SerializedObject(root.GetComponent<UITeachTipsPage>());
                data.FindProperty("followHighlight").boolValue = true;
                data.FindProperty("targetGap").floatValue = 24;
                data.FindProperty("viewportMargin").floatValue = 24;
                data.FindProperty("tailOffset").floatValue = .2f;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, tips);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        static void Mark(string prefab, string path, string id)
        {
            var root = PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var target = root.transform.Find(path);
                if (target == null || target.GetComponent<Button>() == null)
                    throw new InvalidOperationException("Guide target must be the visible Button: " + path);
                var marker = target.GetComponent<GuideObjectMarker>();
                if (marker == null) marker = target.gameObject.AddComponent<GuideObjectMarker>();
                marker.GUID = id;
                PrefabUtility.SaveAsPrefabAsset(root, prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
