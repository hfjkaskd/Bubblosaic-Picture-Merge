using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of continue_panel (out-of-moves): banner then dialog card.</summary>
    public class ContinuePanel : MonoBehaviour
    {
        const int CONTINUE_COIN_COST = 1000;
        // Godot's single-layer 21px outline renders heavier on TMP's CJK
        // fallback glyphs than the two-layer title/button treatment nearby.
        const float AD_LABEL_OUTLINE_PX = 12f;

        BubblePage _page;
        public BubblePage Page
        {
            get => _page;
            set => _page = value;
        }
        public bool Visible { get; private set; }

        [SerializeField] RectTransform _root;
        [SerializeField] Image _dim;
        [SerializeField] RectTransform _banner;
        [SerializeField] RectTransform _dialog;
        [SerializeField] RectTransform _coinHud;
        [SerializeField] RectTransform _card;
        [SerializeField] CanvasGroup _bannerCg;
        [SerializeField] CanvasGroup _dialogCg;
        [SerializeField] CanvasGroup _dimCg;
        [SerializeField] CommonButton _adButton;
        [SerializeField] CommonButton _coinButton;
        [SerializeField] PressButton _closeButton;
        [SerializeField] Image _adIcon;
        [SerializeField] TMP_Text _adLabel;
        [SerializeField] TMP_Text _adLabelBack;
        [SerializeField] TMP_Text _subtitle;
        [SerializeField] TMP_Text _coinHudLabel;
        [SerializeField] TMP_Text _coinCostLabel;
        [SerializeField] ContinueBubbleEmitter _bubbleEmitter;
        [SerializeField] TMP_Text _movesGainLabel;

        const float CARD_FULL_HEIGHT = 1468f;
        const float CARD_SHRINK_PX = 236f;
        const float MOVES_GAIN_RISE_PX = 40f;
        const float MOVES_GAIN_IN_SEC = 0.25f;
        const float MOVES_GAIN_HOLD_SEC = 0.6f;
        const float MOVES_GAIN_OUT_SEC = 0.3f;
        const float MOVES_GAIN_ROLL_SEC = 0.45f;
        static readonly Vector2 AdButtonPosition = new Vector2(0f, -978f);
        static readonly Vector2 CoinButtonPosition = new Vector2(0f, -1210f);
        bool _adButtonUsesRewardedAd;

        public void Build(Transform parent)
        {
            if (HasPrefabReferences())
            {
                InitializePrefabRuntime(Page, parent);
                if (_root != null) _root.gameObject.SetActive(false);
                return;
            }

            _root = UiFactory.FullStretch((RectTransform)parent, "ContinuePanel");

            _dim = UiFactory.Rect(_root, "Dim", new Color(0, 0, 0, 0.8f), 0, 0);
            var dimRt = _dim.rectTransform;
            dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero; dimRt.offsetMax = Vector2.zero;
            _dim.raycastTarget = true;
            _dimCg = _dim.gameObject.AddComponent<CanvasGroup>();

            // banner 1016x388 centered
            _banner = UiFactory.Node(_root, "Banner");
            _banner.sizeDelta = new Vector2(1016, 388);
            var bbg = UiFactory.Img(_banner, "Bg", "Art/Sprites/Continue/banner_oom_bg", 1016, 388);
            var blabel = UiFactory.Label(_banner, "Label", Localization.Tr("out_of_moves"), 112, Color.white, AssetLib.UiFont, TextAnchor.MiddleCenter, 900, 200);
            TmpTextStyle.ApplyOutlineAndShadow(
                blabel,
                new Color32(0x00, 0x12, 0x3D, 0xFF), 12f,
                new Color32(0x00, 0x12, 0x3D, 0xFF), new Vector2(0f, -6f));
            _bannerCg = _banner.gameObject.AddComponent<CanvasGroup>();

            // dialog 1080x1468 centered
            _dialog = UiFactory.Node(_root, "Dialog");
            _dialog.sizeDelta = new Vector2(1080, 1468);
            var card = UiFactory.Img(_dialog, "Card", "Art/Sprites/Setting/dialog_card_bg_blue_9", 0, 0);
            _card = card.rectTransform;
            ConfigureCardRect(_card);
            card.sprite = AssetLib.Sprite9Design("Art/Sprites/Setting/dialog_card_bg_blue_9", 270, 332, 270, 188);
            card.type = Image.Type.Sliced;

            var emitterRt = UiFactory.Node(_dialog, "BubbleEmitter");
            emitterRt.anchorMin = emitterRt.anchorMax = new Vector2(0.5f, 0.5f);
            emitterRt.sizeDelta = new Vector2(260, 260);
            emitterRt.anchoredPosition = new Vector2(0, 262);
            var emitterBubble = UiFactory.Img(
                emitterRt, "Bubble", "Art/Sprites/Bubble/ambient_bubble", 128, 128);
            emitterBubble.gameObject.SetActive(false);
            _bubbleEmitter = emitterRt.gameObject.AddComponent<ContinueBubbleEmitter>();
            _bubbleEmitter.ConfigurePrefabAuthoring(emitterBubble);

            var titleBack = UiFactory.Label(
                _dialog, "TitleBack", Localization.Tr("continue"), 96,
                new Color32(0xB6, 0xD7, 0xF7, 0xFF), AssetLib.UiFont,
                TextAnchor.MiddleCenter, 728, 176);
            titleBack.rectTransform.anchorMin = titleBack.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            titleBack.rectTransform.anchoredPosition = new Vector2(0, -131);
            TmpTextStyle.ApplyOutline(
                titleBack, new Color32(0x00, 0x31, 0x5C, 0xFF), 32f);

            var title = UiFactory.Label(_dialog, "Title", Localization.Tr("continue"), 96, Color.white, AssetLib.UiFont, TextAnchor.MiddleCenter, 728, 176);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0, -128);
            TmpTextStyle.ApplyShadow(
                title, new Color32(0xBF, 0xDF, 0xFF, 0xFF),
                new Vector2(0f, -3f));

            // Original dialog_card_slot panel, with the +15 art above the
            // separate moves caption. Keep this as authored prefab content.
            var movesBox = UiFactory.Node(_dialog, "MovesBox");
            movesBox.anchorMin = movesBox.anchorMax = new Vector2(0.5f, 1f);
            movesBox.sizeDelta = new Vector2(796, 368);
            movesBox.anchoredPosition = new Vector2(0, -472);
            var movesBg = UiFactory.Img(
                movesBox, "Bg", "Art/Sprites/Continue/dialog_card_slot", 796, 368);
            movesBg.raycastTarget = false;
            var plusImg = UiFactory.Img(movesBox, "Plus", "Art/Sprites/Continue/moves_plus15", 321, 229);
            plusImg.rectTransform.anchoredPosition = new Vector2(0, 32.5f);
            var movesSub = UiFactory.Label(movesBox, "MovesLabel", Localization.Tr("str_moves"), 60, new Color32(0x00, 0x31, 0x5C, 0xFF), AssetLib.UiFont, TextAnchor.MiddleCenter, 760, 88);
            movesSub.rectTransform.anchoredPosition = new Vector2(0, -126);
            movesSub.enableAutoSizing = false;
            movesSub.enableWordWrapping = false;

            _subtitle = UiFactory.Label(_dialog, "Subtitle", "", 50, new Color32(0x66, 0x4C, 0x43, 0xFF), AssetLib.UiFont, TextAnchor.MiddleCenter, 940, 154);
            _subtitle.rectTransform.anchorMin = _subtitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _subtitle.rectTransform.anchoredPosition = new Vector2(0, -763);
            _subtitle.alignment = TextAlignmentOptions.Top;
            _subtitle.richText = true;
            _subtitle.enableWordWrapping = true;

            // ad button 700x240 at y 858..1098 from dialog top
            _adButton = CommonButton.Create(_dialog, "AdButton", CommonButton.Variant.Orange, Localization.Tr("get_free_moves"));
            _adButton.Root.anchorMin = _adButton.Root.anchorMax = new Vector2(0.5f, 1f);
            _adButton.Root.sizeDelta = new Vector2(700, 240);
            _adButton.Root.anchoredPosition = new Vector2(0, -978);
            _adButton.OnClick = () => Page.OnContinueAd();
            _adIcon = UiFactory.Img(
                _adButton.Root, "AdIcon", "Art/Sprites/Continue/common_ad", 116, 116);
            _adLabel = FindNamed<TMP_Text>(_adButton.Root, "Label");
            _adLabelBack = FindNamed<TMP_Text>(_adButton.Root, "LabelBack");
            ConfigureAdContents(true);

            _coinButton = CommonButton.Create(_dialog, "CoinButton", CommonButton.Variant.Green, "");
            _coinButton.Root.anchorMin = _coinButton.Root.anchorMax = new Vector2(0.5f, 1f);
            _coinButton.Root.sizeDelta = new Vector2(700, 240);
            _coinButton.Root.anchoredPosition = new Vector2(0, -1210);
            var coinIcon = UiFactory.Img(_coinButton.Root, "Coin", "Art/Sprites/Continue/coin_icon", 100, 101);
            coinIcon.rectTransform.anchoredPosition = new Vector2(-112, 11);
            _coinCostLabel = UiFactory.Label(_coinButton.Root, "Cost", CONTINUE_COIN_COST.ToString(), 96, Color.white, AssetLib.NumFont, TextAnchor.MiddleLeft, 300, 140);
            _coinCostLabel.rectTransform.anchoredPosition = new Vector2(102, 11);
            TmpTextStyle.ApplyOutline(
                _coinCostLabel, new Color(0.078f, 0.357f, 0.176f), 21f);
            _coinButton.OnClick = () => Page.OnContinueCoin();

            var close = UiFactory.Img(_dialog, "Close", "Art/Sprites/Continue/close_x", 144, 144);
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = new Vector2(0, 1);
            close.rectTransform.anchoredPosition = new Vector2(888 + 72, -148);
            UiFactory.MakeButton(close.gameObject, () => Page.OnContinueClose());
            _closeButton = close.GetComponent<PressButton>();

            // coin hud top-right
            _coinHud = UiFactory.Node(_root, "CoinHud");
            _coinHud.anchorMin = _coinHud.anchorMax = new Vector2(1, 1);
            _coinHud.sizeDelta = new Vector2(260, 116);
            _coinHud.anchoredPosition = new Vector2(-166, -128);
            var pill = UiFactory.Img(_coinHud, "Pill", "Art/Sprites/Home/coin_pill_bg", 202, 96);
            pill.rectTransform.anchoredPosition = new Vector2(29, 0);
            pill.sprite = AssetLib.Sprite9("Art/Sprites/Home/coin_pill_bg", 50, 20, 50, 20);
            pill.type = Image.Type.Sliced;
            var chIcon = UiFactory.Img(_coinHud, "Icon", "Art/Sprites/Continue/coin_icon", 116, 116);
            chIcon.rectTransform.anchoredPosition = new Vector2(-72, 0);
            _coinHudLabel = UiFactory.Label(_coinHud, "Count", "1000", 56, new Color32(0x34, 0x36, 0x60, 0xFF), AssetLib.NumFont, TextAnchor.MiddleCenter, 144, 96);
            _coinHudLabel.rectTransform.anchoredPosition = new Vector2(47, 0);

            _movesGainLabel = UiFactory.Label(
                _root, "MovesGainLabel", "+15", 48, Color.white,
                AssetLib.NumFont, TextAnchor.MiddleCenter, 180, 72);
            _movesGainLabel.raycastTarget = false;
            _movesGainLabel.gameObject.SetActive(false);

            // Godot BubbleEmitter uses z_index=1 while the dialog controls use
            // the default z-order, so keep it as the final dialog sibling.
            _bubbleEmitter.transform.SetAsLastSibling();
            _dialogCg = _dialog.gameObject.AddComponent<CanvasGroup>();
            BindPrefabRuntime();
            _root.gameObject.SetActive(false);
        }

        bool HasPrefabReferences()
        {
            return _root != null || _dim != null || _dialog != null ||
                   _adButton != null || _coinButton != null;
        }

        /// <summary>Injects the owning page and rebinds a ContinuePanel prefab instance.</summary>
        public void InitializePrefabRuntime(BubblePage page, Transform parent = null)
        {
            Page = page;
            if (_root == null)
                _root = transform as RectTransform;
            if (parent != null && _root != null && _root != parent && !_root.IsChildOf(parent))
                _root.SetParent(parent, false);
            BindPrefabRuntime();
        }

        public void BindPrefabRuntime()
        {
            if (_root == null)
                _root = transform as RectTransform;
            if (_root == null) return;

            _dim = _dim != null ? _dim : FindNamed<Image>(_root, "Dim");
            _banner = _banner != null ? _banner : FindNamed<RectTransform>(_root, "Banner");
            _dialog = _dialog != null ? _dialog : FindNamed<RectTransform>(_root, "Dialog");
            _coinHud = _coinHud != null ? _coinHud : FindNamed<RectTransform>(_root, "CoinHud");
            _card = _card != null ? _card : FindNamed<RectTransform>(_dialog, "Card");
            _subtitle = _subtitle != null ? _subtitle : FindNamed<TMP_Text>(_root, "Subtitle");
            _coinHudLabel = _coinHudLabel != null ? _coinHudLabel : FindNamed<TMP_Text>(_root, "Count");
            _coinCostLabel = _coinCostLabel != null ? _coinCostLabel : FindNamed<TMP_Text>(_root, "Cost");
            _adButton = _adButton != null ? _adButton : FindNamed<CommonButton>(_root, "AdButton");
            _coinButton = _coinButton != null ? _coinButton : FindNamed<CommonButton>(_root, "CoinButton");
            _closeButton = _closeButton != null ? _closeButton : FindNamed<PressButton>(_root, "Close");
            if (_adButton != null)
            {
                _adIcon = _adIcon != null ? _adIcon : FindNamed<Image>(_adButton.Root, "AdIcon");
                _adLabel = _adLabel != null ? _adLabel : FindNamed<TMP_Text>(_adButton.Root, "Label");
                _adLabelBack = _adLabelBack != null ? _adLabelBack : FindNamed<TMP_Text>(_adButton.Root, "LabelBack");
            }
            _bubbleEmitter = _bubbleEmitter != null
                ? _bubbleEmitter
                : FindNamed<ContinueBubbleEmitter>(_dialog, "BubbleEmitter");
            _movesGainLabel = _movesGainLabel != null
                ? _movesGainLabel
                : FindNamed<TMP_Text>(_root, "MovesGainLabel");

            _dimCg = EnsureCanvasGroup(_dim != null ? _dim.gameObject : null, _dimCg);
            _bannerCg = EnsureCanvasGroup(_banner != null ? _banner.gameObject : null, _bannerCg);
            _dialogCg = EnsureCanvasGroup(_dialog != null ? _dialog.gameObject : null, _dialogCg);
            if (_card != null)
                ConfigureCardRect(_card);
            var bannerLabel = FindNamed<TMP_Text>(_banner, "Label");
            if (bannerLabel != null)
            {
                TmpTextStyle.ApplyOutlineAndShadow(
                    bannerLabel,
                    new Color32(0x00, 0x12, 0x3D, 0xFF), 12f,
                    new Color32(0x00, 0x12, 0x3D, 0xFF),
                    new Vector2(0f, -6f));
            }
            var title = FindNamed<TMP_Text>(_dialog, "Title");
            var titleBack = FindNamed<TMP_Text>(_dialog, "TitleBack");
            if (titleBack != null)
            {
                TmpTextStyle.ApplyOutline(
                    titleBack, new Color32(0x00, 0x31, 0x5C, 0xFF), 32f);
            }
            if (title != null)
            {
                TmpTextStyle.ApplyShadow(
                    title, new Color32(0xBF, 0xDF, 0xFF, 0xFF),
                    new Vector2(0f, -3f));
            }
            if (_subtitle != null)
            {
                _subtitle.alignment = TextAlignmentOptions.Top;
                _subtitle.richText = true;
                _subtitle.enableWordWrapping = true;
            }
            if (_coinCostLabel != null)
            {
                TmpTextStyle.ApplyOutline(
                    _coinCostLabel,
                    new Color(0.078f, 0.357f, 0.176f), 21f);
            }
            _bubbleEmitter?.BindPrefabRuntime();

            if (_adButton != null)
            {
                _adButton.BindPrefabRuntime();
                _adButton.OnClick = OnAdPressed;
            }
            if (_coinButton != null)
            {
                _coinButton.BindPrefabRuntime();
                _coinButton.OnClick = OnCoinPressed;
            }
            if (_closeButton != null)
                _closeButton.OnClick = OnClosePressed;
        }

        static CanvasGroup EnsureCanvasGroup(GameObject target, CanvasGroup current)
        {
            if (current != null || target == null) return current;
            current = target.GetComponent<CanvasGroup>();
            return current != null ? current : target.AddComponent<CanvasGroup>();
        }

        static T FindNamed<T>(Transform parent, string objectName) where T : Component
        {
            if (parent == null) return null;
            var components = parent.GetComponentsInChildren<T>(true);
            foreach (var component in components)
                if (component.gameObject.name == objectName)
                    return component;
            return null;
        }

        void OnAdPressed()
        {
            if (_adButtonUsesRewardedAd &&
                !true)
                return;
            Haptics.Play(HapticLevel.Weak);
            if (Page != null) Page.OnContinueAd();
        }

        void OnCoinPressed()
        {
            Haptics.Play(HapticLevel.Weak);
            if (Page != null) Page.OnContinueCoin();
        }

        void OnClosePressed()
        {
            Haptics.Play(HapticLevel.Weak);
            if (Page != null) Page.OnContinueClose();
        }

        public void PlayAppear(int adCount, bool freeRevive, bool getMoves = false)
        {
            if (_root == null) BindPrefabRuntime();
            if (_root == null) return;
            StopAllCoroutines();
            Visible = true;
            _root.gameObject.SetActive(true);
            if (_dim != null) _dim.gameObject.SetActive(true);
            if (_coinHudLabel != null) _coinHudLabel.text = SaveState.Coins.ToString();
            string revivePct = Random.Range(60, 101).ToString();
            string reviveRaw = Localization.Tr("revive_here");
            string pct = $"<color=#EE7F00>{revivePct}%</color>";
            if (reviveRaw.Contains("d%")) reviveRaw = reviveRaw.Replace("d%", pct);
            else if (reviveRaw.Contains("%d")) reviveRaw = reviveRaw.Replace("%d", pct);
            bool compact = freeRevive || getMoves;
            _adButtonUsesRewardedAd = !compact;
            bool showAdButton =
                !_adButtonUsesRewardedAd ||
                true;
            if (_subtitle != null)
                _subtitle.text = freeRevive
                    ? Localization.Tr("first_death_free_revive_subtitle")
                    : getMoves
                        ? Localization.Tr("get_moves_subtitle")
                        : reviveRaw;
            _adButton.SetText(freeRevive
                ? Localization.Tr("get_free_moves")
                : getMoves
                    ? Localization.Tr("get_moves")
                    : Localization.Tr("free"));
            ConfigureAdContents(!compact);
            _adButton.Root.gameObject.SetActive(showAdButton);
            _adButton.Root.anchoredPosition = AdButtonPosition;
            SetCardCompact(compact || !showAdButton);
            bool adAvailable = compact ||
                               (true &&
                                RewardedAds.CanRequest);
            if (showAdButton)
                SetAdAvailable(adAvailable);
            _coinButton.Root.gameObject.SetActive(!compact);
            _coinButton.Root.anchoredPosition =
                !compact && !showAdButton
                    ? AdButtonPosition
                    : CoinButtonPosition;
            bool affordable = SaveState.Coins >= CONTINUE_COIN_COST;
            _coinButton.SetInteractable(affordable);
            // The recovered CommonButton applies a gray material for the
            // disabled state without fading the whole control.
            _coinButton.CanvasGroup.alpha = 1f;
            _bubbleEmitter?.StopEmitting();
            if (_movesGainLabel != null)
                _movesGainLabel.gameObject.SetActive(false);
            StartCoroutine(AppearCo());
        }

        IEnumerator AppearCo()
        {
            _dimCg.alpha = 0;
            _dialog.gameObject.SetActive(false);
            _coinHud.gameObject.SetActive(false);
            _banner.gameObject.SetActive(true);
            var rest = Vector2.zero;
            var off = rest + new Vector2(0, 260);
            _banner.anchoredPosition = off;
            _bannerCg.alpha = 0;
            // slide in 0.5 BACK OUT
            float t = 0;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float k = Tween.Evaluate(Ease.OutBack, Mathf.Clamp01(t / 0.5f));
                _banner.anchoredPosition = Vector2.LerpUnclamped(off, rest, k);
                _bannerCg.alpha = Mathf.Clamp01(t / 0.5f);
                yield return null;
            }
            yield return new WaitForSeconds(1.6f);
            t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                float k = Tween.Evaluate(Ease.InQuad, Mathf.Clamp01(t / 0.3f));
                _banner.anchoredPosition = Vector2.LerpUnclamped(rest, off, k);
                _bannerCg.alpha = 1f - Mathf.Clamp01(t / 0.3f);
                yield return null;
            }
            _banner.gameObject.SetActive(false);
            _dialog.gameObject.SetActive(true);
            _coinHud.gameObject.SetActive(true);
            _bubbleEmitter?.StartEmitting();
            _dialogCg.alpha = 0;
            _dialog.localScale = Vector3.one * 0.8f;
            StartCoroutine(Tween.Run(0.22f, k => { _dialogCg.alpha = k; _dimCg.alpha = k; }));
            yield return Tween.Scale(_dialog, Vector3.one, 0.28f, Ease.OutBack);
        }

        void SetAdAvailable(bool available)
        {
            if (_adButton == null) return;
            _adButton.SetInteractable(available);
            if (_adButton.CanvasGroup != null)
                _adButton.CanvasGroup.alpha = 1f;

            if (available)
            {
                FitAdLabelSingleLine(
                    _adIcon != null && _adIcon.gameObject.activeSelf);
            }
            else if (_adLabel != null)
            {
                // The Godot control uses one outlined label and applies the
                // disabled gray material to its face and outline together.
                _adLabel.color = new Color32(0xD2, 0xD2, 0xD2, 0xFF);
                TmpTextStyle.ApplyOutline(
                    _adLabel,
                    new Color32(0x78, 0x78, 0x78, 0xFF),
                    AD_LABEL_OUTLINE_PX);
            }
        }

        void SetCardCompact(bool compact)
        {
            if (_card == null) return;
            ConfigureCardRect(_card);
            _card.sizeDelta = new Vector2(0f,
                compact ? CARD_FULL_HEIGHT - CARD_SHRINK_PX : CARD_FULL_HEIGHT);
        }

        static void ConfigureCardRect(RectTransform card)
        {
            if (card == null) return;
            card.anchorMin = new Vector2(0f, 1f);
            card.anchorMax = new Vector2(1f, 1f);
            card.pivot = new Vector2(0.5f, 1f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(0f, CARD_FULL_HEIGHT);
        }

        void ConfigureAdContents(bool showIcon)
        {
            if (_adIcon != null)
            {
                _adIcon.gameObject.SetActive(showIcon);
                var iconRt = _adIcon.rectTransform;
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(116f, 116f);
                iconRt.anchoredPosition = new Vector2(-104f, 11f);
                _adIcon.raycastTarget = false;
            }
            ConfigureAdLabel(_adLabel, showIcon, false);
            ConfigureAdLabel(_adLabelBack, showIcon, true);
            FitAdLabelSingleLine(showIcon);
        }

        static void ConfigureAdLabel(TMP_Text label, bool showIcon, bool back)
        {
            if (label == null) return;
            label.gameObject.SetActive(!back);
            if (back) return;

            var rect = label.rectTransform;
            rect.sizeDelta = new Vector2(showIcon ? 500f : 636f, 220f);
            rect.anchoredPosition = new Vector2(showIcon ? 76f : 0f, 11f);
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        void FitAdLabelSingleLine(bool showIcon)
        {
            if (_adLabel == null) return;
            const int maxSize = 96;
            const int minSize = 24;
            float available = showIcon ? 500f : 636f;
            int size = maxSize;
            while (size > minSize)
            {
                _adLabel.fontSize = size;
                if (_adLabel.GetPreferredValues(
                    _adLabel.text, 10000f,
                    _adLabel.rectTransform.rect.height).x <= available)
                    break;
                size -= 4;
            }
            _adLabel.fontSize = size;
            if (_adLabelBack != null)
                _adLabelBack.gameObject.SetActive(false);
            TmpTextStyle.ApplyOutline(
                _adLabel,
                new Color32(0xA6, 0x61, 0x0A, 0xFF),
                AD_LABEL_OUTLINE_PX);
        }

        /// <summary>
        /// Closes the continue dialog and reproduces the original top-bar
        /// "+moves" float plus delayed number roll.
        /// </summary>
        public void PlayReviveResult(int fromValue, int toValue)
        {
            if (_root == null) BindPrefabRuntime();
            if (_root == null) return;

            Visible = false;
            StopAllCoroutines();
            _bubbleEmitter?.StopEmitting();
            if (_dim != null) _dim.gameObject.SetActive(false);
            if (_banner != null) _banner.gameObject.SetActive(false);
            if (_dialog != null) _dialog.gameObject.SetActive(false);
            if (_coinHud != null) _coinHud.gameObject.SetActive(false);

            if (_movesGainLabel == null || Page == null || Page.TopBar == null)
            {
                Page?.TopBar?.SetMoves(toValue);
                _root.gameObject.SetActive(false);
                return;
            }

            _movesGainLabel.gameObject.SetActive(true);
            _movesGainLabel.text = "+" + Mathf.Max(0, toValue - fromValue);
            _movesGainLabel.color = Color.white;
            Page.TopBar.SetMoves(fromValue);
            StartCoroutine(ReviveResultCo(fromValue, toValue));
        }

        IEnumerator ReviveResultCo(int fromValue, int toValue)
        {
            var labelRt = _movesGainLabel.rectTransform;
            Vector2 top = Page.TopBar.MovesNumberScreenCenter()
                + new Vector2(30f * App.HudScaleX, 30f * App.HudScaleY);
            Vector2 enterStart = top - new Vector2(0f, MOVES_GAIN_RISE_PX * App.HudScaleY);
            Vector2 exitEnd = top + new Vector2(
                0f, MOVES_GAIN_RISE_PX * 0.6f * App.HudScaleY);
            labelRt.position = enterStart;

            float total = MOVES_GAIN_IN_SEC + MOVES_GAIN_HOLD_SEC + MOVES_GAIN_OUT_SEC;
            float elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;

                if (elapsed <= MOVES_GAIN_IN_SEC)
                {
                    float raw = Mathf.Clamp01(elapsed / MOVES_GAIN_IN_SEC);
                    float k = Tween.Evaluate(Ease.OutQuad, raw);
                    labelRt.position = Vector2.LerpUnclamped(enterStart, top, k);
                    SetLabelAlpha(k);
                }
                else if (elapsed >= MOVES_GAIN_IN_SEC + MOVES_GAIN_HOLD_SEC)
                {
                    float raw = Mathf.Clamp01(
                        (elapsed - MOVES_GAIN_IN_SEC - MOVES_GAIN_HOLD_SEC)
                        / MOVES_GAIN_OUT_SEC);
                    float k = Tween.Evaluate(Ease.InQuad, raw);
                    labelRt.position = Vector2.LerpUnclamped(top, exitEnd, k);
                    SetLabelAlpha(1f - raw);
                }

                if (elapsed >= MOVES_GAIN_IN_SEC)
                {
                    float raw = Mathf.Clamp01(
                        (elapsed - MOVES_GAIN_IN_SEC) / MOVES_GAIN_ROLL_SEC);
                    float k = Tween.Evaluate(Ease.OutQuad, raw);
                    Page.TopBar.SetMoves(Mathf.RoundToInt(
                        Mathf.Lerp(fromValue, toValue, k)));
                }
                yield return null;
            }

            Page.TopBar.SetMoves(toValue);
            _movesGainLabel.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }

        void SetLabelAlpha(float alpha)
        {
            if (_movesGainLabel == null) return;
            var color = _movesGainLabel.color;
            color.a = Mathf.Clamp01(alpha);
            _movesGainLabel.color = color;
        }

        public void HidePanel()
        {
            Visible = false;
            StopAllCoroutines();
            _bubbleEmitter?.StopEmitting();
            if (_movesGainLabel != null)
                _movesGainLabel.gameObject.SetActive(false);
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }
}
