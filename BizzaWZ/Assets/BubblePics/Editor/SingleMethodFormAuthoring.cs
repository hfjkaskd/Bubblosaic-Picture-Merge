using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Presentation = BubblePics.WithdrawalFormPresentation;

namespace BubblePics.EditorTools
{
    public static partial class FormPresentationAuthoring
    {
        public static void ApplySingleMethodLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before authoring.");

            var root = PrefabUtility.LoadPrefabContents(FormPath);
            try
            {
                var page = root.GetComponent<UIWithdrawalPanel>();
                var presentation = root.GetComponent<Presentation>();
                foreach (var preset in presentation.presets)
                    ConfigureSingleMethodLayout(page, preset);
                presentation.presets[0].Apply();
                PrefabUtility.SaveAsPrefabAsset(root, FormPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        static void ConfigureSingleMethodLayout(UIWithdrawalPanel page, Presentation.Preset preset)
        {
            if (preset.name != "PIX" && preset.name != "PagBank") return;
            ConfigureContinueButton(page, preset);
            float scale = 2360f / 1846f;
            foreach (var rect in preset.rects)
            {
                if (rect.target != page.paymentImage.rectTransform) continue;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.position = Vector2.zero;
                rect.size = new Vector2(302, 102) * scale;
                rect.scale = Vector3.one;
            }
            foreach (var state in preset.images)
            {
                if (state.target != page.paymentImage) continue;
                state.preserveAspect = true;
                state.type = Image.Type.Simple;
                state.raycast = false;
                state.color = Color.white;
            }
        }

        static void ConfigureContinueButton(UIWithdrawalPanel page, Presentation.Preset preset)
        {
            var button = page.transform.Find("Root/FillRoot/pageContent/BtnWithdrawal");
            var image = button.GetComponent<Image>();
            var label = button.Find("Text (TMP)");
            // The controller validates when Continue is pressed. Its enabled native Button
            // must not permanently display the disabled artwork from the empty-form mockup.
            foreach (var state in preset.images)
            {
                if (state.target != image) continue;
                state.path = "PayPalReference20260928/ApprovedSource";
                state.spriteName = "Continue";
                state.resource = image.GetComponent<CoralResourceSprite>();
                state.sprite = null;
                state.material = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/BubblePics/Resources/PayPalReference20260928/Continue.mat");
                state.color = Color.white;
                state.enabled = state.raycast = true;
            }
            // These full-button caption slices contain the old grey plate as well as text.
            // Use the existing live localized label over the green Button target graphic.
            foreach (var state in preset.objects)
                if (state.target != null && state.target.transform.IsChildOf(button)
                    && state.target.GetComponent<ApprovedHudCaption>() != null) state.active = false;
            foreach (var state in preset.behaviours)
                if (state.target is ApprovedHudCaption && state.target.transform.IsChildOf(button)) state.enabled = false;
            foreach (var state in preset.groups)
                if (state.target != null && state.target.transform == label) state.alpha = 1;
        }
    }
}
