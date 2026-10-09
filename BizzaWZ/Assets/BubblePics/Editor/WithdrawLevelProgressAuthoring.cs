using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Author the production prefab; runtime continues to set the same Image.fillAmount.
    public static class WithdrawLevelProgressAuthoring
    {
        const string PathName = "Assets/BizzaWZ/Final/Real/UI/RealWithdrawalPanl/RealWithdrawPanel.prefab";

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before authoring.");
            var root = PrefabUtility.LoadPrefabContents(PathName);
            try
            {
                Configure(root.GetComponent<RealWithdrawPanel>());
                PrefabUtility.SaveAsPrefabAsset(root, PathName);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void Configure(RealWithdrawPanel page)
        {
            var fill = page.progressBar;
            var track = fill.transform.parent.GetComponent<Image>();
            float height = track.rectTransform.rect.height;
            if (height <= 8f) throw new InvalidOperationException("Progress track has no authored height.");
            // Match source-pixel scale to height so horizontal caps are never stretched.
            track.type = Image.Type.Sliced;
            track.pixelsPerUnitMultiplier = 169f / height;
            track.raycastTarget = false;
            var rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
            fill.type = Image.Type.Sliced;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.pixelsPerUnitMultiplier = 124f / (height - 8f);
            fill.raycastTarget = false;
            var effect = fill.GetComponent<WithdrawCloudProgressFill>();
            if (effect == null) effect = fill.gameObject.AddComponent<WithdrawCloudProgressFill>();
            var data = new SerializedObject(effect);
            data.FindProperty("sourceImage").objectReferenceValue = fill;
            data.ApplyModifiedPropertiesWithoutUndo();
            page.progressTxt.transform.SetAsLastSibling();
        }
    }
}
