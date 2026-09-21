using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Rainbow perimeter sweep shown after the final target image lands.
    /// It mirrors BubbleShineOverlay's two opposing heads, five glow layers,
    /// 0.9 perimeter loop and 0.95 second sweep without requiring a texture
    /// baked for a particular HUD card size.
    /// </summary>
    [AddComponentMenu("BubblePics/UI/Last Link Shine")]
    public sealed class LastLinkShine : MaskableGraphic
    {
        const float SweepSeconds = 0.95f;
        const float FadeSeconds = 0.10f;
        const float LoopRatio = 0.90f;
        const float TrailRatio = 0.25f;
        const float Outward = 12f;
        const float CornerRadius = 32f + Outward;
        const int Segments = 48;

        static readonly float[] Widths = { 180f, 126f, 82.8f, 50.4f, 36f };
        static readonly float[] Alphas = { 0.06f, 0.12f, 0.22f, 0.45f, 1f };
        static readonly Color[] Rainbow =
        {
            new Color(0.80f, 0.40f, 1.00f),
            new Color(0.40f, 0.60f, 1.00f),
            new Color(0.40f, 1.00f, 0.40f),
            new Color(1.00f, 0.95f, 0.30f),
            new Color(1.00f, 0.60f, 0.20f),
            new Color(1.00f, 0.30f, 0.30f),
        };

        float _phase;
        float _fade;
        bool _playing;
        Image _headA;
        Image _headB;

        public static LastLinkShine Play(RectTransform parent)
        {
            if (parent == null) return null;
            var go = new GameObject("LastLinkShine", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-Outward, -Outward);
            rt.offsetMax = new Vector2(Outward, Outward);

            var shine = go.AddComponent<LastLinkShine>();
            shine.raycastTarget = false;
            shine.color = Color.white;
            shine.CreateHeads();
            shine.Restart();
            return shine;
        }

        public void Restart()
        {
            _phase = 0f;
            _fade = 0f;
            _playing = true;
            SetVerticesDirty();
            UpdateHeads();
        }

        void CreateHeads()
        {
            Sprite sprite = AssetLib.Sprite("Art/Sprites/Bubble/eff_star5");
            _headA = CreateHead("HeadA", sprite);
            _headB = CreateHead("HeadB", sprite);
        }

        Image CreateHead(string objectName, Sprite sprite)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = Color.white;
            image.rectTransform.sizeDelta = new Vector2(82f, 82f);
            return image;
        }

        void Update()
        {
            if (!_playing) return;

            if (_phase < 1f)
            {
                _phase = Mathf.Min(1f, _phase + Time.deltaTime / SweepSeconds);
            }
            else
            {
                _fade += Time.deltaTime / FadeSeconds;
                if (_fade >= 1f)
                {
                    _playing = false;
                    Destroy(gameObject);
                    return;
                }
            }

            SetVerticesDirty();
            UpdateHeads();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_playing) return;

            float opacity = 1f - Mathf.Clamp01(_fade);
            float head = _phase * LoopRatio;
            for (int layer = 0; layer < Widths.Length; layer++)
            {
                AddTrail(vh, 0f, head, Widths[layer], Alphas[layer] * opacity);
                AddTrail(vh, 0.5f, head, Widths[layer], Alphas[layer] * opacity);
            }
        }

        void AddTrail(VertexHelper vh, float origin, float head, float width, float alpha)
        {
            Vector2 previous = PointOnPerimeter(origin + Mathf.Max(0f, head - TrailRatio));
            for (int i = 1; i <= Segments; i++)
            {
                float frac0 = (i - 1f) / Segments;
                float frac1 = i / (float)Segments;
                float sample = Mathf.Max(0f, head - TrailRatio * (1f - frac1));
                Vector2 current = PointOnPerimeter(origin + sample);

                Vector2 delta = current - previous;
                if (delta.sqrMagnitude > 0.001f)
                {
                    Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
                    Color32 c0 = TrailColor(frac0, alpha);
                    Color32 c1 = TrailColor(frac1, alpha);
                    AddQuad(vh, previous - normal, previous + normal, current + normal, current - normal, c0, c1);
                }
                previous = current;
            }
        }

        static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 from, Color32 to)
        {
            int first = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = from; v.position = a; vh.AddVert(v);
            v.color = from; v.position = b; vh.AddVert(v);
            v.color = to; v.position = c; vh.AddVert(v);
            v.color = to; v.position = d; vh.AddVert(v);
            vh.AddTriangle(first, first + 1, first + 2);
            vh.AddTriangle(first, first + 2, first + 3);
        }

        static Color32 TrailColor(float t, float alpha)
        {
            float palette = Mathf.Clamp01(t) * (Rainbow.Length - 1);
            int a = Mathf.Min(Mathf.FloorToInt(palette), Rainbow.Length - 1);
            int b = Mathf.Min(a + 1, Rainbow.Length - 1);
            Color color = Color.Lerp(Rainbow[a], Rainbow[b], palette - a);
            // The original rainbow trail starts transparent and reaches full
            // strength at the moving head.
            color.a = alpha * Mathf.SmoothStep(0f, 1f, t);
            return color;
        }

        void UpdateHeads()
        {
            if (_headA == null || _headB == null) return;
            float opacity = 1f - Mathf.Clamp01(_fade);
            float head = _phase * LoopRatio;
            _headA.rectTransform.anchoredPosition = PointOnPerimeter(head);
            _headB.rectTransform.anchoredPosition = PointOnPerimeter(0.5f + head);
            Color tint = new Color(0.85f, 0.95f, 1f, opacity);
            _headA.color = tint;
            _headB.color = tint;
        }

        Vector2 PointOnPerimeter(float normalized)
        {
            Rect rect = rectTransform.rect;
            float left = rect.xMin;
            float right = rect.xMax;
            float bottom = rect.yMin;
            float top = rect.yMax;
            float radius = Mathf.Clamp(CornerRadius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            float horizontal = Mathf.Max(0f, rect.width - radius * 2f);
            float vertical = Mathf.Max(0f, rect.height - radius * 2f);
            float arc = Mathf.PI * 0.5f * radius;
            float perimeter = horizontal * 2f + vertical * 2f + arc * 4f;
            float distance = Mathf.Repeat(normalized, 1f) * perimeter;

            if (distance <= horizontal)
                return new Vector2(left + radius + distance, top);
            distance -= horizontal;
            if (distance <= arc && radius > 0f)
            {
                float angle = Mathf.PI * 0.5f - distance / radius;
                return new Vector2(right - radius + Mathf.Cos(angle) * radius,
                    top - radius + Mathf.Sin(angle) * radius);
            }
            distance -= arc;
            if (distance <= vertical)
                return new Vector2(right, top - radius - distance);
            distance -= vertical;
            if (distance <= arc && radius > 0f)
            {
                float angle = -distance / radius;
                return new Vector2(right - radius + Mathf.Cos(angle) * radius,
                    bottom + radius + Mathf.Sin(angle) * radius);
            }
            distance -= arc;
            if (distance <= horizontal)
                return new Vector2(right - radius - distance, bottom);
            distance -= horizontal;
            if (distance <= arc && radius > 0f)
            {
                float angle = -Mathf.PI * 0.5f - distance / radius;
                return new Vector2(left + radius + Mathf.Cos(angle) * radius,
                    bottom + radius + Mathf.Sin(angle) * radius);
            }
            distance -= arc;
            if (distance <= vertical)
                return new Vector2(left, bottom + radius + distance);
            distance -= vertical;
            float finalAngle = Mathf.PI - distance / Mathf.Max(radius, 0.001f);
            return new Vector2(left + radius + Mathf.Cos(finalAngle) * radius,
                top - radius + Mathf.Sin(finalAngle) * radius);
        }
    }
}
