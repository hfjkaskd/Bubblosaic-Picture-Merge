using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    public enum TutorialSpotlightKind
    {
        Rainbow,
        Starfish,
    }

    /// <summary>
    /// Authored implementation shared by rainbow_tutorial_page and
    /// starfish_tutorial_page. The preview slot is part of each prefab; only
    /// its sprite is exchanged at runtime.
    /// </summary>
    public sealed class TutorialSpotlightView : MetaFlowView
    {
        [SerializeField] TutorialSpotlightKind _kind;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _description;
        [SerializeField] TMP_Text _tapLabel;
        [SerializeField] RectTransform _previewRoot;
        [SerializeField] Image _preview;
        [SerializeField] Image _glow;
        [SerializeField] Button _tapCatcher;
        [SerializeField] string _defaultPreviewPath;

        bool _closing;
        Image _bubbleCarrier;
        static Sprite _circleSprite;

        public override void InitializePrefabRuntime()
        {
            base.InitializePrefabRuntime();
            _title ??= FindNamed<TMP_Text>(panelRoot, "TitleLabel");
            _description ??= FindNamed<TMP_Text>(panelRoot, "DescLabel");
            _tapLabel ??= FindNamed<TMP_Text>(panelRoot, "TapContinueLabel");
            _previewRoot ??= FindNamed<RectTransform>(panelRoot, "BubblePreviewRoot");
            _preview ??= FindNamed<Image>(panelRoot, "BubblePreview");
            _glow ??= FindNamed<Image>(panelRoot, "Glow");
            EnsurePreviewLayers();
            _tapCatcher ??= FindNamed<Button>(panelRoot, "TapCatcher");
            if (_preview != null && _preview.sprite == null && !string.IsNullOrEmpty(_defaultPreviewPath))
                _preview.sprite = AssetLib.Sprite(_defaultPreviewPath);
            if (_tapCatcher != null)
            {
                _tapCatcher.onClick.RemoveListener(Close);
                _tapCatcher.onClick.AddListener(Close);
            }
            ApplyCopy();
        }

        public void Play(Sprite bubblePreview, float bubbleDiameter = 200f)
        {
            _closing = false;
            ApplyCopy();
            if (_preview != null)
            {
                if (bubblePreview != null) _preview.sprite = bubblePreview;
                _preview.rectTransform.sizeDelta =
                    Vector2.one * Mathf.Max(1f, bubbleDiameter) * 0.72f;
                _preview.preserveAspect = true;
                _preview.gameObject.SetActive(_preview.sprite != null);
            }
            if (_bubbleCarrier != null)
            {
                _bubbleCarrier.rectTransform.sizeDelta =
                    Vector2.one * Mathf.Max(1f, bubbleDiameter);
                _bubbleCarrier.gameObject.SetActive(true);
            }
            if (_glow != null)
            {
                bool starfishGlow = _kind == TutorialSpotlightKind.Starfish;
                _glow.gameObject.SetActive(starfishGlow);
                _glow.rectTransform.sizeDelta =
                    Vector2.one * Mathf.Max(1f, bubbleDiameter) * 1.6f;
            }
            Show();
            Run(FadeRoutine(0f, 1f, 0.2f, null));
        }

        public void Close()
        {
            if (_closing) return;
            _closing = true;
            float from = canvasGroup != null ? canvasGroup.alpha : 1f;
            Run(FadeRoutine(from, 0f, 0.2f, Hide));
        }

        public override void HandleBackRequest() => Close();

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            CanvasGroup authoredCanvasGroup,
            TutorialSpotlightKind kind,
            TMP_Text title,
            TMP_Text description,
            TMP_Text tapLabel,
            RectTransform previewRoot,
            Image preview,
            Image glow,
            Button tapCatcher,
            string defaultPreviewPath)
        {
            base.ConfigurePrefabAuthoring(authoredPanelRoot, null, authoredCanvasGroup);
            _kind = kind;
            _title = title;
            _description = description;
            _tapLabel = tapLabel;
            _previewRoot = previewRoot;
            _preview = preview;
            _glow = glow;
            _tapCatcher = tapCatcher;
            _defaultPreviewPath = defaultPreviewPath;
        }

        void ApplyCopy()
        {
            if (_kind == TutorialSpotlightKind.Starfish)
            {
                if (_title != null)
                    _title.text = Text("starfish_guide_title", "Starfish Bubble!");
                if (_description != null)
                    _description.text = Text(
                        "starfish_intro_tip",
                        "Finish its picture to collect the starfish.");
                if (_title != null)
                {
                    _title.enableVertexGradient = true;
                    _title.colorGradient = new VertexGradient(
                        new Color32(0xFF, 0xE0, 0x8A, 0xFF),
                        new Color32(0xFF, 0xE0, 0x8A, 0xFF),
                        new Color32(0xFF, 0x9F, 0x2E, 0xFF),
                        new Color32(0xFF, 0x9F, 0x2E, 0xFF));
                    TmpTextStyle.ClearEffects(_title);
                    TmpTextStyle.ApplyOutlineAndShadow(
                        _title,
                        new Color32(0x6B, 0x34, 0x10, 0xFF),
                        10f,
                        new Color(0f, 0f, 0f, 0.4f),
                        new Vector2(0f, -4f));
                    TmpTextStyle.ApplyDisplayTitleFace(_title);
                }
            }
            else
            {
                if (_title != null)
                    _title.text = Text("rainbow_guide_headline", "Magic bubble!");
                if (_description != null)
                    _description.text = Text(
                        "rainbow_guide_title",
                        "Magic bubble links with any bubble!");
                if (_title != null)
                {
                    _title.enableVertexGradient = true;
                    _title.colorGradient = new VertexGradient(
                        new Color32(0xFC, 0x50, 0x00, 0xFF),
                        new Color32(0xD3, 0xC6, 0xFF, 0xFF),
                        new Color32(0xFF, 0xDE, 0x12, 0xFF),
                        new Color32(0x66, 0xD3, 0xFF, 0xFF));
                    // The rainbow headline is gradient-only in the recovered
                    // scene; it intentionally has no outline or shadow.
                    TmpTextStyle.ClearEffects(_title);
                }
            }
            if (_tapLabel != null)
                _tapLabel.text = Text("tap_to_continue", "Tap to continue");
        }

        void EnsurePreviewLayers()
        {
            if (_previewRoot == null || _preview == null) return;
            Transform existing = _previewRoot.Find("BubbleCarrier");
            if (existing != null)
                _bubbleCarrier = existing.GetComponent<Image>();
            if (_bubbleCarrier == null)
            {
                var carrierObject = new GameObject(
                    "BubbleCarrier",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                carrierObject.transform.SetParent(_previewRoot, false);
                _bubbleCarrier = carrierObject.GetComponent<Image>();
            }
            _bubbleCarrier.sprite = AssetLib.Sprite("Art/Sprites/Bubble/disc");
            _bubbleCarrier.preserveAspect = true;
            _bubbleCarrier.raycastTarget = false;
            _bubbleCarrier.color = Color.white;

            if (_glow != null)
            {
                _glow.sprite = CircleSprite();
                _glow.preserveAspect = true;
                _glow.raycastTarget = false;
                _glow.color = new Color(1f, 0.78f, 0.32f, 0.42f);
                _glow.transform.SetAsFirstSibling();
            }
            _bubbleCarrier.transform.SetSiblingIndex(
                _glow != null ? 1 : 0);
            _preview.transform.SetAsLastSibling();
        }

        static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int size = 64;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "TutorialGlowCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.48f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float edge = Mathf.Clamp01(radius - Vector2.Distance(
                    new Vector2(x, y), center) + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, edge);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _circleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            _circleSprite.name = "TutorialGlowCircle";
            return _circleSprite;
        }

        IEnumerator FadeRoutine(float from, float to, float duration, Action completed)
        {
            if (canvasGroup != null) canvasGroup.alpha = from;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = to;
            completed?.Invoke();
        }
    }

    /// <summary>Typed component for the source's intentionally empty Node2D scene.</summary>
    public class PreviewPiecesViewBase : MetaFlowView
    {
    }
}
