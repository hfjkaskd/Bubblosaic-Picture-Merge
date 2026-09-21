using TMPro;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Warps TMP's already-shaped glyph mesh onto the same three-point arc used
    /// by the original CurveLabel. Each fallback material is modified in place,
    /// preserving advances, kerning, ligatures, SDF outlines and underlays.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("BubblePics/UI/Curved Text")]
    public sealed class CurvedText : TextMeshProUGUI
    {
        [SerializeField, Min(0f)] float _amplitude = 22f;
        [SerializeField] bool _alignToTangent = true;

        public float Amplitude
        {
            get => _amplitude;
            set
            {
                if (Mathf.Approximately(_amplitude, value)) return;
                _amplitude = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        public bool AlignToTangent
        {
            get => _alignToTangent;
            set
            {
                if (_alignToTangent == value) return;
                _alignToTangent = value;
                SetVerticesDirty();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            OnPreRenderText += WarpText;
        }

        protected override void OnDisable()
        {
            OnPreRenderText -= WarpText;
            base.OnDisable();
        }

        void WarpText(TMP_TextInfo textInfo)
        {
            if (textInfo == null || textInfo.characterCount == 0)
                return;

            Rect rect = rectTransform.rect;
            float width = rect.width;
            if (width <= Mathf.Epsilon)
                return;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!character.isVisible)
                    continue;

                int materialIndex = character.materialReferenceIndex;
                int first = character.vertexIndex;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                Vector2 pivot =
                    ((Vector2)vertices[first] + (Vector2)vertices[first + 2]) * 0.5f;

                float u = Mathf.Clamp01((pivot.x - rect.xMin) / width);
                float arcY = Mathf.Sin(u * Mathf.PI) * _amplitude;
                float slope = _amplitude * Mathf.PI * Mathf.Cos(u * Mathf.PI) / width;
                float angle = _alignToTangent ? Mathf.Atan(slope) : 0f;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);

                for (int j = 0; j < 4; j++)
                {
                    int index = first + j;
                    Vector2 delta = (Vector2)vertices[index] - pivot;
                    Vector2 rotated = new Vector2(
                        delta.x * cos - delta.y * sin,
                        delta.x * sin + delta.y * cos);
                    vertices[index] = pivot + rotated + Vector2.up * arcY;
                }
            }
        }
    }
}
