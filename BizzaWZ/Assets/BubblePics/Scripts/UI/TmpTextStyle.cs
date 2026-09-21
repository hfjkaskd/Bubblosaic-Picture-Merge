using TMPro;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Converts the original Godot font effect sizes (1080x2400 design pixels)
    /// to TextMesh Pro SDF material values.  Call this after assigning the font.
    /// </summary>
    public static class TmpTextStyle
    {
        // Godot's font outline / shadow-outline constants are raster effect
        // units, not a 1:1 match for TMP's signed-distance edge range. A
        // direct conversion makes dense CJK glyph outlines merge into blocks.
        // Keep the recovered 0.075 conversion: the apparent weakness in the
        // display titles comes from their glyph face, not from the outline.
        // Increasing this value consumes too much of the visible CJK face.
        const float GodotOutlinePixelScale = 0.075f;

        // Shadow dilation was already visually aligned. Keep it independent
        // so strengthening an outline does not turn soft title shadows into a
        // second heavy stroke.
        const float GodotShadowEffectPixelScale = 0.075f;

        // Godot's title FontVariation uses a heavier face than TMP Regular,
        // while TMP's synthetic Bold is too aggressive for dense CJK glyphs.
        // A small SDF face expansion reproduces that intermediate title weight
        // without changing the outline, point size, or layout metrics.
        public const float StandardTitleFaceDilation = 0.03f;
        public const float DisplayTitleFaceDilation = 0.08f;

        public static void ClearEffects(TMP_Text text)
        {
            Material[] materials = EditableMaterials(text);
            if (materials.Length == 0) return;

            foreach (Material material in materials)
            {
                if (material.HasProperty(ShaderUtilities.ID_OutlineWidth))
                    material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
                material.DisableKeyword(ShaderUtilities.Keyword_Outline);

                if (material.HasProperty(ShaderUtilities.ID_FaceDilate))
                    material.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
                if (material.HasProperty(ShaderUtilities.ID_UnderlayColor))
                    material.SetColor(ShaderUtilities.ID_UnderlayColor, Color.clear);
                if (material.HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
                    material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                if (material.HasProperty(ShaderUtilities.ID_UnderlayOffsetY))
                    material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
                if (material.HasProperty(ShaderUtilities.ID_UnderlayDilate))
                    material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
                if (material.HasProperty(ShaderUtilities.ID_UnderlaySoftness))
                    material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
                material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
                material.DisableKeyword("UNDERLAY_INNER");
            }
            Refresh(text);
        }

        /// <summary>
        /// Adds a small SDF face expansion without TMP's full synthetic-bold
        /// pass. This is useful for Godot FontVariation weights such as 800,
        /// which sit between the recovered base face and TMP's very heavy
        /// Bold simulation for dense CJK glyphs.
        /// </summary>
        public static void ApplyFaceDilation(TMP_Text text, float amount)
        {
            Material[] materials = EditableMaterials(text);
            if (materials.Length == 0) return;
            foreach (Material material in materials)
            {
                if (!material.HasProperty(ShaderUtilities.ID_FaceDilate))
                    continue;
                material.SetFloat(
                    ShaderUtilities.ID_FaceDilate,
                    Mathf.Clamp(amount, -1f, 1f));
            }
            Refresh(text);
        }

        /// <summary>
        /// Expands only the colored/white glyph face of a large display title.
        /// This must be called after ClearEffects because ClearEffects resets
        /// face dilation along with the outline and underlay.
        /// </summary>
        public static void ApplyDisplayTitleFace(TMP_Text text)
        {
            ApplyFaceDilation(text, DisplayTitleFaceDilation);
        }

        /// <summary>
        /// Reproduces the recovered 700-weight title face. Use the stronger
        /// display-title expansion only for the recovered 800-weight variant.
        /// </summary>
        public static void ApplyStandardTitleFace(TMP_Text text)
        {
            ApplyFaceDilation(text, StandardTitleFaceDilation);
        }

        public static void ApplyOutline(TMP_Text text, Color color, float outlinePixels)
        {
            Material[] materials = EditableMaterials(text);
            if (materials.Length == 0) return;
            foreach (Material material in materials)
            {
                if (outlinePixels <= 0f)
                {
                    if (material.HasProperty(ShaderUtilities.ID_OutlineWidth))
                        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
                    material.DisableKeyword(ShaderUtilities.Keyword_Outline);
                }
                else
                {
                    material.EnableKeyword(ShaderUtilities.Keyword_Outline);
                    material.SetColor(ShaderUtilities.ID_OutlineColor, color);
                    material.SetFloat(
                        ShaderUtilities.ID_OutlineWidth,
                        PixelsToSdf(
                            text,
                            material,
                            outlinePixels * GodotOutlinePixelScale,
                            halfRange: true));
                }
            }
            Refresh(text);
        }

        /// <param name="offsetPixels">
        /// Offset in Unity UI design coordinates; negative Y moves the shadow down.
        /// </param>
        public static void ApplyShadow(
            TMP_Text text,
            Color color,
            Vector2 offsetPixels,
            float dilatePixels = 0f,
            float softnessPixels = 0f)
        {
            Material[] materials = EditableMaterials(text);
            if (materials.Length == 0) return;
            foreach (Material material in materials)
            {
                if (color.a <= 0f && offsetPixels.sqrMagnitude <= 0f &&
                    dilatePixels <= 0f && softnessPixels <= 0f)
                {
                    material.DisableKeyword(ShaderUtilities.Keyword_Underlay);
                    continue;
                }

                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.DisableKeyword("UNDERLAY_INNER");
                material.SetColor(ShaderUtilities.ID_UnderlayColor, color);
                material.SetFloat(
                    ShaderUtilities.ID_UnderlayOffsetX,
                    PixelsToSdf(text, material, offsetPixels.x, halfRange: false));
                // TMP samples the atlas in the opposite direction to the visual offset.
                material.SetFloat(
                    ShaderUtilities.ID_UnderlayOffsetY,
                    PixelsToSdf(text, material, -offsetPixels.y, halfRange: false));
                material.SetFloat(
                    ShaderUtilities.ID_UnderlayDilate,
                    PixelsToSdf(
                        text,
                        material,
                        dilatePixels * GodotShadowEffectPixelScale,
                        halfRange: true));
                material.SetFloat(
                    ShaderUtilities.ID_UnderlaySoftness,
                    PixelsToSdf(
                        text,
                        material,
                        softnessPixels * GodotShadowEffectPixelScale,
                        halfRange: false));
            }
            Refresh(text);
        }

        public static void ApplyOutlineAndShadow(
            TMP_Text text,
            Color outlineColor,
            float outlinePixels,
            Color shadowColor,
            Vector2 shadowOffsetPixels,
            float shadowDilatePixels = 0f,
            float softnessPixels = 0f)
        {
            ApplyOutline(text, outlineColor, outlinePixels);
            ApplyShadow(
                text,
                shadowColor,
                shadowOffsetPixels,
                shadowDilatePixels,
                softnessPixels);
        }

        static Material[] EditableMaterials(TMP_Text text)
        {
            if (text == null || text.font == null)
                return System.Array.Empty<Material>();

            // Generate the current character layout first. CJK and several
            // other scripts are rendered through TMP_SubMeshUI fallback
            // materials; changing only text.fontMaterial leaves those glyphs
            // with a stale (and often much thicker) preset.
            text.ForceMeshUpdate(true, true);
            var candidates = new System.Collections.Generic.List<Material>
            {
                text.fontMaterial,
            };
            foreach (TMP_SubMeshUI subMesh in
                     text.GetComponentsInChildren<TMP_SubMeshUI>(true))
                candidates.Add(subMesh.material);
            foreach (TMP_SubMesh subMesh in
                     text.GetComponentsInChildren<TMP_SubMesh>(true))
                candidates.Add(subMesh.material);

            var valid = new System.Collections.Generic.List<Material>(candidates.Count);
            var ids = new System.Collections.Generic.HashSet<int>();
            foreach (Material material in candidates)
            {
                if (material == null ||
                    !material.HasProperty(ShaderUtilities.ID_GradientScale) ||
                    !ids.Add(material.GetInstanceID()))
                    continue;
                valid.Add(material);
            }
            return valid.ToArray();
        }

        static float PixelsToSdf(
            TMP_Text text,
            Material material,
            float pixels,
            bool halfRange)
        {
            if (Mathf.Approximately(pixels, 0f)) return 0f;

            float gradientScale = Mathf.Max(
                1f,
                material.GetFloat(ShaderUtilities.ID_GradientScale));
            float sourcePointSize = text.font != null
                ? Mathf.Max(1f, text.font.faceInfo.pointSize)
                : Mathf.Max(1f, text.fontSize);
            float renderedScale = Mathf.Max(0.01f, text.fontSize / sourcePointSize);
            float sdfPixels = gradientScale * renderedScale;
            float divisor = halfRange ? sdfPixels * 0.5f : sdfPixels;
            return Mathf.Clamp(pixels / Mathf.Max(0.01f, divisor), -1f, 1f);
        }

        static void Refresh(TMP_Text text)
        {
            text.SetMaterialDirty();
            text.SetVerticesDirty();
        }
    }
}
