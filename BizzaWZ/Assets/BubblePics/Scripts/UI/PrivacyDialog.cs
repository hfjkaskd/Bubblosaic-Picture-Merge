using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of privacy_dialog — welcome card with Accept.</summary>
    public class PrivacyDialog : MonoBehaviour
    {
        const int TitleFontSizeMax = 110;
        const int TitleFontSizeMin = 40;
        const float TitleInnerWidth = 728f;
        const float TitlePadding = 16f;

        const int ContentFontSizeMax = 64;
        const int ContentFontSizeMin = 28;
        const float ContentAvailableHeight = 400f;
        const float InnerCardCornerRadius = 48f;
        const float InnerCardShadowSize = 6f;
        static readonly Vector2 InnerCardShadowOffset = new Vector2(0f, -2f);
        static readonly Color InnerCardShadowColor =
            new Color(0.066f, 0.141f, 0.275f, 0.12f);

        public System.Action Accepted;

        [Header("Prefab references")]
        [SerializeField] RectTransform _root;
        [SerializeField] RectTransform _content;
        [SerializeField] RectTransform _dialog;
        [SerializeField] CanvasGroup _contentCg;
        [SerializeField] CanvasGroup _overlayCg;
        [SerializeField] PressButton _acceptPress;
        [SerializeField] CommonButton _acceptButton;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _body;
        [SerializeField] PrivacyInlineLinkText _inlineLinks;
        [SerializeField] PrivacyPopupAmbientBubbles _ambientBubbles;

        bool _closing;

        void OnEnable()
        {
            Localization.LocaleChanged -= HandleLocaleChanged;
            Localization.LocaleChanged += HandleLocaleChanged;
        }

        void OnDisable()
        {
            Localization.LocaleChanged -= HandleLocaleChanged;
            if (_ambientBubbles != null)
                _ambientBubbles.StopEffect();
        }

        /// <summary>Injects the non-serializable accept callback.</summary>
        public void Bind(System.Action accepted)
        {
            Accepted = accepted;
            InitializePrefabRuntime();
        }

        /// <summary>Rebinds delegates after the prefab has been instantiated.</summary>
        public void InitializePrefabRuntime()
        {
            ResolvePrefabReferences();
            if (_acceptPress != null)
                _acceptPress.OnClick = NotifyAccepted;
            if (_inlineLinks != null)
                _inlineLinks.Bind(OpenTerms, OpenPrivacy);
            if (_ambientBubbles != null)
                _ambientBubbles.InitializeRuntime();
            ApplyTextStyles();
            RefreshPrivacyCopy();
        }

        public void Build(Transform parent)
        {
            if (_root != null && _content != null && _dialog != null)
            {
                InitializePrefabRuntime();
                _root.gameObject.SetActive(false);
                return;
            }

            _root = UiFactory.FullStretch((RectTransform)parent, "PrivacyDialog");
            transform.SetParent(_root, false);

            var overlay = UiFactory.Rect(_root, "Overlay", new Color(0, 0, 0, 0.75f), 0, 0);
            var ovRt = overlay.rectTransform;
            ovRt.anchorMin = Vector2.zero; ovRt.anchorMax = Vector2.one;
            ovRt.offsetMin = Vector2.zero; ovRt.offsetMax = Vector2.zero;
            overlay.raycastTarget = true;
            _overlayCg = overlay.gameObject.AddComponent<CanvasGroup>();

            _content = UiFactory.FullStretch(_root, "Content");
            _contentCg = _content.gameObject.AddComponent<CanvasGroup>();

            _dialog = UiFactory.Node(_content, "Dialog");
            _dialog.sizeDelta = new Vector2(1080, 1324);

            var card = UiFactory.Img(_dialog, "CardBg", "Art/Sprites/Common/dialog_card_bg_blue_9s", 1080, 1324);
            // corals: godot rect (180,-116)-(900,176) → center 30px below dialog top
            var topDeco = UiFactory.Img(_dialog, "TopDeco", "Art/Sprites/Common/dialog_deco_top", 720, 292);
            topDeco.rectTransform.anchorMin = topDeco.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            topDeco.rectTransform.anchoredPosition = new Vector2(0, -30);
            var banner = UiFactory.Img(_dialog, "Banner", "Art/Sprites/Common/dialog_title_banner_green", 1040, 280);
            banner.rectTransform.anchoredPosition = new Vector2(0, 662 - 154.5f);
            var starfish = UiFactory.Img(_dialog, "Starfish", "Art/Sprites/Common/dialog_deco_starfish", 173, 177);
            starfish.rectTransform.anchoredPosition = new Vector2(887.5f - 540f, 662 - 126f);
            _title = UiFactory.Label(_dialog, "Title", Localization.Tr("welcome"), 110, Color.white, AssetLib.UiFont, TextAnchor.MiddleCenter, 728, 180);
            _title.rectTransform.anchoredPosition = new Vector2(0, 662 - 154f);

            // inner white card 856x529 at (112,328)
            var innerTexture = TopGameBar.RoundedRectTex(
                140,
                100,
                Mathf.RoundToInt(InnerCardCornerRadius),
                Color.white);
            var innerSprite = Sprite.Create(
                innerTexture,
                new Rect(0, 0, 140, 100),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(50, 50, 50, 50));

            var innerShadow = UiFactory.Node(_dialog, "InnerCardShadow");
            innerShadow.sizeDelta = new Vector2(
                856f + InnerCardShadowSize * 2f,
                529f + InnerCardShadowSize * 2f);
            innerShadow.anchoredPosition =
                new Vector2(0, 662 - 592.5f) + InnerCardShadowOffset;
            var shadowImage = innerShadow.gameObject.AddComponent<Image>();
            shadowImage.sprite = innerSprite;
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = InnerCardShadowColor;
            shadowImage.raycastTarget = false;

            var inner = UiFactory.Node(_dialog, "InnerCard");
            inner.sizeDelta = new Vector2(856, 529);
            inner.anchoredPosition = new Vector2(0, 662 - 592.5f);
            var innerImg = inner.gameObject.AddComponent<Image>();
            innerImg.sprite = innerSprite;
            innerImg.type = Image.Type.Sliced;
            innerImg.raycastTarget = false;

            _body = UiFactory.Label(
                inner, "Body", "", 60, new Color(0.153f, 0.22f, 0.298f),
                AssetLib.UiFont, TextAnchor.MiddleCenter, 736, 409);
            _body.enableWordWrapping = true;
            _body.overflowMode = TextOverflowModes.Overflow;
            _body.richText = true;
            _body.raycastTarget = true;
            _body.rectTransform.anchoredPosition = Vector2.zero;
            _inlineLinks = _body.gameObject.AddComponent<PrivacyInlineLinkText>();

            var accept = CommonButton.Create(_dialog, "Accept", CommonButton.Variant.Green, Localization.Tr("PRIVACY_DIALOG_ACCEPT"));
            accept.Root.sizeDelta = new Vector2(700, 240);
            accept.Root.anchoredPosition = new Vector2(0, 662 - 1033f);
            _acceptButton = accept;
            _acceptPress = accept.Press;

            var bubbleL = UiFactory.Img(_dialog, "BubbleL", "Art/Sprites/Common/dialog_deco_bubble_l", 138, 150);
            bubbleL.rectTransform.anchoredPosition = new Vector2(79 - 540, 662 - 1149);
            var bubbleR = UiFactory.Img(_dialog, "BubbleR", "Art/Sprites/Common/dialog_deco_bubble_r", 153, 216);
            bubbleR.rectTransform.anchoredPosition = new Vector2(971.5f - 540, 662 - 1098);

            var ambientRoot = UiFactory.FullStretch(_root, "AmbientBubbles");
            _ambientBubbles = ambientRoot.gameObject.AddComponent<PrivacyPopupAmbientBubbles>();
            _ambientBubbles.CreateRuntimePool(
                AssetLib.Sprite("Art/Sprites/Common/common_decorations_bubble"));

            InitializePrefabRuntime();
            _root.gameObject.SetActive(false);
        }

        void ResolvePrefabReferences()
        {
            if (_root == null)
                _root = transform as RectTransform ?? FindRect(transform, "PrivacyDialog");
            if (_content == null) _content = FindRect(_root, "Content");
            if (_dialog == null) _dialog = FindRect(_root, "Dialog");
            if (_contentCg == null && _content != null) _contentCg = _content.GetComponent<CanvasGroup>();
            if (_overlayCg == null)
            {
                var overlay = FindRect(_root, "Overlay");
                if (overlay != null) _overlayCg = overlay.GetComponent<CanvasGroup>();
            }
            if (_acceptPress == null)
            {
                var accept = FindRect(_dialog, "Accept");
                if (accept != null)
                {
                    _acceptButton = accept.GetComponent<CommonButton>();
                    if (_acceptButton != null)
                        _acceptButton.BindPrefabRuntime();
                    _acceptPress = accept.GetComponentInChildren<PressButton>(true);
                }
            }
            if (_acceptButton == null && _acceptPress != null)
                _acceptButton = _acceptPress.GetComponentInParent<CommonButton>();
            if (_title == null)
            {
                var title = FindRect(_dialog, "Title");
                if (title != null) _title = title.GetComponent<TMP_Text>();
            }
            if (_body == null)
            {
                var body = FindRect(_dialog, "Body");
                if (body != null) _body = body.GetComponent<TMP_Text>();
            }
            if (_inlineLinks == null && _body != null)
                _inlineLinks = _body.GetComponent<PrivacyInlineLinkText>() ??
                               _body.gameObject.AddComponent<PrivacyInlineLinkText>();
            if (_ambientBubbles == null && _root != null)
                _ambientBubbles = _root.GetComponentInChildren<PrivacyPopupAmbientBubbles>(true);
        }

        static RectTransform FindRect(Transform parent, string objectName)
        {
            if (parent == null) return null;
            foreach (var rt in parent.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == objectName) return rt;
            return null;
        }

        void NotifyAccepted()
        {
            if (_closing) return;
            _closing = true;
            Accepted?.Invoke();
        }

        void OpenTerms()
        {
            PlatformLinks.TryOpenTerms();
        }

        void OpenPrivacy()
        {
            PlatformLinks.TryOpenPrivacyPolicy();
        }

        void RefreshPrivacyCopy()
        {
            if (_title != null)
            {
                _title.text = Localization.Tr("welcome");
                FitTitleFont();
            }

            if (_acceptButton != null)
                _acceptButton.SetText(Localization.Tr("PRIVACY_DIALOG_ACCEPT"));

            string template = Localization.Tr("please_read_accept");
            if (_inlineLinks != null)
            {
                _inlineLinks.SetLocalizedTemplate(
                    template,
                    Localization.Tr("terms_service"),
                    Localization.Tr("privacy_policy"));
                FitContentFont();
                _inlineLinks.InvalidateLayout();
            }
            else if (_body != null)
            {
                _body.text = template
                    .Replace("%1", "").Replace("%2", "")
                    .Replace("%3", "").Replace("%4", "")
                    .Replace("\\n", "\n");
                FitContentFont();
            }
        }

        void HandleLocaleChanged()
        {
            if (this != null)
                RefreshPrivacyCopy();
        }

        void FitTitleFont()
        {
            if (_title == null || _title.font == null) return;
            _title.enableAutoSizing = false;
            int size = TitleFontSizeMax;
            float maxWidth = TitleInnerWidth - TitlePadding * 2f;
            while (size > TitleFontSizeMin &&
                   PreferredWidth(_title, _title.text, size) > maxWidth)
                size -= 2;
            _title.fontSize = size;
        }

        void FitContentFont()
        {
            if (_body == null || _body.font == null) return;
            _body.enableAutoSizing = false;
            int size = ContentFontSizeMax;
            float width = _body.rectTransform.rect.width;
            if (width <= 0f) width = 736f;
            while (size > ContentFontSizeMin &&
                   PreferredWrappedHeight(_body, _body.text, size, width) >
                   ContentAvailableHeight)
                size -= 2;
            _body.fontSize = size;
            _body.SetAllDirty();
        }

        static float PreferredWidth(TMP_Text text, string value, int fontSize)
        {
            float previousSize = text.fontSize;
            bool previousAutoSize = text.enableAutoSizing;
            bool previousWrap = text.enableWordWrapping;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.fontSize = fontSize;
            float width = text.GetPreferredValues(
                value ?? string.Empty,
                10000f,
                1000f).x;
            text.fontSize = previousSize;
            text.enableWordWrapping = previousWrap;
            text.enableAutoSizing = previousAutoSize;
            return width;
        }

        static float PreferredWrappedHeight(
            TMP_Text text,
            string value,
            int fontSize,
            float width)
        {
            float previousSize = text.fontSize;
            bool previousAutoSize = text.enableAutoSizing;
            bool previousWrap = text.enableWordWrapping;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.fontSize = fontSize;
            float height = text.GetPreferredValues(
                value ?? string.Empty,
                width,
                10000f).y;
            text.fontSize = previousSize;
            text.enableWordWrapping = previousWrap;
            text.enableAutoSizing = previousAutoSize;
            return height;
        }

        void ApplyTextStyles()
        {
            if (_title != null)
            {
                TmpTextStyle.ApplyOutline(
                    _title,
                    new Color(0.102f, 0.471f, 0.341f, 1f),
                    21f);
                TmpTextStyle.ApplyDisplayTitleFace(_title);
            }
            if (_body != null)
            {
                _body.richText = true;
                _body.enableWordWrapping = true;
                _body.overflowMode = TextOverflowModes.Overflow;
                TmpTextStyle.ClearEffects(_body);
            }
        }

        public void PlayAppear()
        {
            InitializePrefabRuntime();
            if (_root == null) return;
            _closing = false;
            _root.gameObject.SetActive(true);
            if (_ambientBubbles != null)
                _ambientBubbles.StartEffect();
            if (_content != null && _contentCg != null && _overlayCg != null)
                StartCoroutine(GenericPopup.Open(_content, _contentCg, _overlayCg));
        }

        public IEnumerator PlayClose()
        {
            if (_content != null && _contentCg != null && _overlayCg != null)
                yield return GenericPopup.Close(_content, _contentCg, _overlayCg);
            if (_ambientBubbles != null)
                _ambientBubbles.StopEffect();
            if (_root != null) _root.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        public void ConfigurePrefabAuthoring(
            RectTransform root,
            RectTransform content,
            RectTransform dialog,
            CanvasGroup contentGroup,
            CanvasGroup overlayGroup,
            CommonButton acceptButton,
            PressButton acceptPress,
            TMP_Text title,
            TMP_Text body,
            PrivacyInlineLinkText inlineLinks,
            PrivacyPopupAmbientBubbles ambientBubbles)
        {
            _root = root;
            _content = content;
            _dialog = dialog;
            _contentCg = contentGroup;
            _overlayCg = overlayGroup;
            _acceptButton = acceptButton;
            _acceptPress = acceptPress;
            _title = title;
            _body = body;
            _inlineLinks = inlineLinks;
            _ambientBubbles = ambientBubbles;
        }
#endif
    }

    /// <summary>Port of loading_overlay — dim + centered spinner card.</summary>
    public class LoadingOverlay : MonoBehaviour
    {
        const float SPIN_SPEED = 4.2f; // rad/s

        [Header("Prefab references")]
        [SerializeField] RectTransform _root;
        [SerializeField] RectTransform _spinner;
        [SerializeField] TMP_Text _progressLabel;

        public void InitializePrefabRuntime()
        {
            if (_root == null)
                _root = transform as RectTransform ?? FindRect(transform, "LoadingOverlay");
            if (_spinner == null) _spinner = FindRect(_root, "Spinner");
            if (_progressLabel == null)
            {
                RectTransform progressRect = FindRect(_root, "ProgressLabel");
                if (progressRect != null)
                    _progressLabel = progressRect.GetComponent<TMP_Text>();
            }
            if (_progressLabel != null)
                _progressLabel.gameObject.SetActive(false);
            if (_spinner != null)
            {
                _spinner.anchoredPosition = Vector2.zero;
                _spinner.localRotation = Quaternion.identity;
            }
        }

        public void Show()
        {
            InitializePrefabRuntime();
            SetProgress(0f);
            if (_root != null) _root.gameObject.SetActive(true);
        }

        public void SetProgress(float progress)
        {
            if (_progressLabel == null) return;
            int percent = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f),
                0,
                100);
            _progressLabel.text = percent + "%";
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        public void Build(Transform parent)
        {
            if (_root != null && _spinner != null)
            {
                InitializePrefabRuntime();
                return;
            }

            _root = UiFactory.FullStretch((RectTransform)parent, "LoadingOverlay");
            transform.SetParent(_root, false);
            var dim = UiFactory.Rect(_root, "Dim", new Color(0, 0, 0, 0.55f), 0, 0);
            var dimRt = dim.rectTransform;
            dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero; dimRt.offsetMax = Vector2.zero;
            dim.raycastTarget = true;

            var card = UiFactory.Node(_root, "Card");
            card.sizeDelta = new Vector2(236, 236);
            var cardImg = card.gameObject.AddComponent<Image>();
            var tex = TopGameBar.RoundedRectTex(118, 118, 32, Color.white);
            cardImg.sprite = Sprite.Create(tex, new Rect(0, 0, 118, 118), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect, new Vector4(34, 34, 34, 34));
            cardImg.type = Image.Type.Sliced;

            var spin = UiFactory.Img(card, "Spinner", "Art/Sprites/Common/loading_dots", 160, 160);
            _spinner = spin.rectTransform;
            _spinner.anchoredPosition = Vector2.zero;
            _progressLabel = null;
            InitializePrefabRuntime();
        }

        static RectTransform FindRect(Transform parent, string objectName)
        {
            if (parent == null) return null;
            foreach (var rt in parent.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == objectName) return rt;
            return null;
        }

        void Update()
        {
            if (_spinner != null)
                _spinner.Rotate(0, 0, -SPIN_SPEED * Mathf.Rad2Deg * Time.deltaTime);
        }
    }
}
