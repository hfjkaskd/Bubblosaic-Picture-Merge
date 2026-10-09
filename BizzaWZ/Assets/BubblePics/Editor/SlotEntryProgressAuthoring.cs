using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Offline authoring of the existing progress Image, not a runtime UI replacement.
    public static class SlotEntryProgressAuthoring
    {
        const string Widget = "Assets/BizzaWZ/Final/Real/UI/GamePanel/Resources/Resources/GameUiWidget.prefab";
        const float ArtworkScale = 1080f / 941f / .9f;

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before authoring.");
            string backup = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../ArtChanges/SlotEntryProgress-20260929/Backup", Widget));
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(Widget, backup);
            var root = PrefabUtility.LoadPrefabContents(Widget);
            try
            {
                Configure(root.transform.Find("FooterMount/EntryRow/SlotEnter"));
                PrefabUtility.SaveAsPrefabAsset(root, Widget);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void Configure(Transform slot)
        {
            var controller = slot.GetComponent<SlotEnter>();
            var fill = controller.progressImag;
            var rt = fill.rectTransform;
            // Cover the blue capsule, including the border/padding in the fill sprite.
            // The former 117 x 28 text-erasure region is smaller than the visible track.
            // Keep the label above the fill and the native SlotEnter data binding.
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(1f, 1.5f) * ArtworkScale;
            rt.sizeDelta = new Vector2(125f, 37f) * ArtworkScale;
            rt.localScale = Vector3.one;
            fill.type = Image.Type.Sliced;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.pixelsPerUnitMultiplier = 124f / rt.sizeDelta.y;
            fill.color = Color.white;
            fill.material = null;
            fill.raycastTarget = false;
            fill.enabled = true;
            fill.gameObject.SetActive(true);
            if (fill.GetComponent<WithdrawCloudProgressFill>() == null)
                throw new InvalidOperationException("Missing sliced progress mesh component.");
            var binder = fill.GetComponent<CoralResourceSprite>();
            if (binder == null) throw new InvalidOperationException("Missing authored sprite loader.");
            var data = new SerializedObject(binder);
            data.FindProperty("_resourcePath").stringValue = "AllUI20260924/SharedControls";
            data.FindProperty("_spriteName").stringValue = "Green";
            data.ApplyModifiedPropertiesWithoutUndo();
            controller.progressTxt.transform.SetAsLastSibling();
            var button = slot.GetComponent<Button>();
            if (button == null || button.targetGraphic == null || button.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("Slot entry must retain its standard code-bound Button.");
        }
    }
}
