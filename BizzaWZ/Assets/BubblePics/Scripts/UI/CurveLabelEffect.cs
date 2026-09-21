using TMPro;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// TMP port of curve_label.gd's default arch. Glyphs can remain rigid like
    /// ToolUnlock, or align to the local tangent like the Sea Hero title.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class CurveLabelEffect : MonoBehaviour
    {
        [SerializeField, Min(0f)] float _amplitude = 45f;
        [SerializeField] bool _alignToTangent;
        [SerializeField] bool _fitToVisibleText;

        TextMeshProUGUI _text;

        public float Amplitude
        {
            get => _amplitude;
            set
            {
                _amplitude = Mathf.Max(0f, value);
                ResolveText();
                if (_text != null)
                    _text.SetVerticesDirty();
            }
        }

        public bool AlignToTangent
        {
            get => _alignToTangent;
            set
            {
                if (_alignToTangent == value) return;
                _alignToTangent = value;
                SetTextDirty();
            }
        }

        public bool FitToVisibleText
        {
            get => _fitToVisibleText;
            set
            {
                if (_fitToVisibleText == value) return;
                _fitToVisibleText = value;
                SetTextDirty();
            }
        }

        void OnEnable()
        {
            ResolveText();
            if (_text != null)
                _text.OnPreRenderText += WarpText;
        }

        void OnDisable()
        {
            if (_text != null)
                _text.OnPreRenderText -= WarpText;
        }

        void OnValidate()
        {
            _amplitude = Mathf.Max(0f, _amplitude);
            SetTextDirty();
        }

        void ResolveText()
        {
            if (_text == null)
                _text = GetComponent<TextMeshProUGUI>();
        }

        void WarpText(TMP_TextInfo textInfo)
        {
            if (textInfo == null || textInfo.characterCount == 0 || _text == null)
                return;

            Rect rect = _text.rectTransform.rect;
            if (rect.width <= Mathf.Epsilon)
                return;

            float left = rect.xMin;
            float right = rect.xMax;
            if (_fitToVisibleText &&
                TryGetVisibleExtents(textInfo, out float visibleLeft, out float visibleRight))
            {
                left = visibleLeft;
                right = visibleRight;
            }
            float width = right - left;
            if (width <= Mathf.Epsilon)
                return;

            float center = (left + right) * 0.5f;
            float half = width * 0.5f;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!character.isVisible)
                    continue;

                int materialIndex = character.materialReferenceIndex;
                int first = character.vertexIndex;
                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                float centerX = (vertices[first].x + vertices[first + 2].x) * 0.5f;

                float x = Mathf.Clamp(centerX - center, -half, half);
                float fraction = x / half;
                float yOffset = _amplitude * (1f - fraction * fraction);
                float angle = _alignToTangent
                    ? Mathf.Atan(-2f * _amplitude * x / (half * half))
                    : 0f;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                Vector2 pivot = new Vector2(
                    centerX,
                    (vertices[first].y + vertices[first + 2].y) * 0.5f);
                for (int j = 0; j < 4; j++)
                {
                    int index = first + j;
                    Vector2 delta = (Vector2)vertices[index] - pivot;
                    Vector2 rotated = new Vector2(
                        delta.x * cos - delta.y * sin,
                        delta.x * sin + delta.y * cos);
                    vertices[index] = pivot + rotated + Vector2.up * yOffset;
                }
            }
        }

        static bool TryGetVisibleExtents(
            TMP_TextInfo textInfo,
            out float left,
            out float right)
        {
            left = float.PositiveInfinity;
            right = float.NegativeInfinity;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!character.isVisible) continue;
                Vector3[] vertices =
                    textInfo.meshInfo[character.materialReferenceIndex].vertices;
                int first = character.vertexIndex;
                left = Mathf.Min(left, vertices[first].x, vertices[first + 1].x,
                    vertices[first + 2].x, vertices[first + 3].x);
                right = Mathf.Max(right, vertices[first].x, vertices[first + 1].x,
                    vertices[first + 2].x, vertices[first + 3].x);
            }
            return !float.IsInfinity(left) && right > left;
        }

        void SetTextDirty()
        {
            ResolveText();
            if (_text != null)
                _text.SetVerticesDirty();
        }
    }
}
