using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of setting_page: sound/vib toggles + restart/home (game mode)
    /// or the compact sound/vibration + version layout (home mode).</summary>
    public class SettingPage : MonoBehaviour
    {
        // Godot computes this at show time from Body's combined minimum height:
        // 264 top + 862 game content + 150 bottom. Keep runtime equal to the
        // authored prefab instead of collapsing it back to the 1200 minimum.
        const string SkinRoot = "Art/Sprites/PsdSkin20260807";
        const float GameCardWidth = 1019f;
        const float GameCardHeight = 1264f;
        const float HomeRowHeight = 144f;
        const float HomeCardPadding = 28f;
        const float HomeRowTextWidth = 628f;
        const float HomeHelpTextWidth = 560f;
        const float PanelSliceLeft = 190f;
        const float PanelSliceTop = 260f;
        const float PanelSliceRight = 190f;
        const float PanelSliceBottom = 360f;
        // Match the recovered Godot scene exactly: Title occupies y=44..216
        // and TitleBack occupies y=47..219 from the card's top edge. The blue
        // outline face therefore sits 3 px below the white foreground face.
        static readonly Vector2 TitleBackPosition = new Vector2(0f, -133f);
        static readonly Vector2 TitlePosition = new Vector2(0f, -130f);
        static readonly Vector2 TitleSize = new Vector2(728f, 172f);
        static readonly Vector2 VersionSize = new Vector2(400f, 44f);
        const float HomeVersionBottomPadding = 96f;

        [SerializeField] RectTransform _root;
        [SerializeField] RectTransform _content;
        [SerializeField] RectTransform _card;
        [SerializeField] CanvasGroup _contentCg;
        [SerializeField] CanvasGroup _overlayCg;
        [SerializeField] Image _soundBtn;
        [SerializeField] Image _vibBtn;
        [SerializeField] RectTransform _actionsRoot;
        [SerializeField] RectTransform _cardsRoot;
        [SerializeField] TMP_Text _version;
        [SerializeField] TMP_Text _titleBack;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _soundLabel;
        [SerializeField] TMP_Text _vibLabel;
        [SerializeField] PressButton _closePress;
        [SerializeField] PressButton _soundPress;
        [SerializeField] PressButton _vibPress;
        [SerializeField] CommonButton _restartButton;
        [SerializeField] CommonButton _homeButton;
        [SerializeField] PressButton[] _homeRowButtons;
        [SerializeField] TMP_Text[] _homeRowLabels;
        [System.NonSerialized] BubblePage _page;
        bool _gameMode = true;
        bool _closing;
        Coroutine _closeRoutine;

        void Awake()
        {
            // HomePage may instantiate the settings prefab and call Show
            // directly, so restore its non-serialized delegates eagerly.
            if (_root != null && App.I != null)
                InitializePrefabRuntime(App.I.DialogRoot, null);
            else if (_root != null)
                BindPrefabRuntime();
        }

        void OnEnable()
        {
            if (!Application.isPlaying || _root == null) return;
            if (_page == null && _gameMode && App.I != null)
                _page = App.I.Page;
            BindPrefabRuntime();
        }

        public void Build(Transform parent, BubblePage page)
        {
            if (HasPrefabReferences())
            {
                InitializePrefabRuntime(parent, page);
                return;
            }

            _page = page;
            var ownRoot = transform as RectTransform;
            _root = ownRoot != null && transform != parent
                ? ownRoot
                : UiFactory.FullStretch((RectTransform)parent, "SettingPage");
            if (_root.parent != parent)
                _root.SetParent(parent, false);
            _root.gameObject.name = "SettingPage";
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            var overlay = UiFactory.Rect(_root, "Overlay", new Color(0, 0, 0, 0.8f), 0, 0);
            var ovRt = overlay.rectTransform;
            ovRt.anchorMin = Vector2.zero; ovRt.anchorMax = Vector2.one;
            ovRt.offsetMin = Vector2.zero; ovRt.offsetMax = Vector2.zero;
            overlay.raycastTarget = true;
            _overlayCg = overlay.gameObject.AddComponent<CanvasGroup>();

            _content = UiFactory.FullStretch(_root, "Content");
            _contentCg = _content.gameObject.AddComponent<CanvasGroup>();

            _card = UiFactory.Node(_content, "CardRoot");
            _card.sizeDelta = new Vector2(1080, 1200);
            var bg = UiFactory.Img(_card, "Bg", SkinRoot + "/Setting/panel", 0, 0);
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bg.sprite = AssetLib.Sprite9Design(
                SkinRoot + "/Setting/panel",
                PanelSliceLeft,
                PanelSliceTop,
                PanelSliceRight,
                PanelSliceBottom);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 1f;

            _titleBack = UiFactory.Label(_card, "TitleBack", Localization.Tr("settings"), 96,
                new Color(0.714f, 0.843f, 0.969f), AssetLib.UiFont, TextAnchor.MiddleCenter, 728, 172);
            _titleBack.rectTransform.anchorMin = _titleBack.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _titleBack.rectTransform.anchoredPosition = TitleBackPosition;
            _title = UiFactory.Label(_card, "Title", Localization.Tr("settings"), 96, Color.white, AssetLib.UiFont, TextAnchor.MiddleCenter, 728, 172);
            _title.rectTransform.anchorMin = _title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _title.rectTransform.anchoredPosition = TitlePosition;
            ApplyTitleStyle();

            var close = UiFactory.Img(_card, "Close", "Art/Sprites/Setting/btn_close_red", 144, 144);
            close.rectTransform.anchorMin = close.rectTransform.anchorMax = new Vector2(1, 1);
            close.rectTransform.anchoredPosition = new Vector2(-120, -148);
            UiFactory.MakeButton(close.gameObject, OnClosePressed);
            _closePress = close.GetComponent<PressButton>();

            // toggles (Body VBox: btn 200x200 at y-388, label 8px below)
            _soundBtn = MakeToggle(
                -210,
                SkinRoot + "/Setting/sound_on",
                Localization.Tr("sound"),
                OnSoundPressed,
                out _soundLabel,
                out _soundPress);
            _vibBtn = MakeToggle(
                210,
                SkinRoot + "/Setting/vibration_on",
                Localization.Tr("vibration"),
                OnVibrationPressed,
                out _vibLabel,
                out _vibPress);

            // game mode actions: restart (blue) above home (orange), stacked
            _actionsRoot = UiFactory.Node(_card, "GameActions");
            _actionsRoot.anchorMin = _actionsRoot.anchorMax = new Vector2(0.5f, 1f);
            _restartButton = CommonButton.Create(_actionsRoot, "Restart", CommonButton.Variant.Blue, Localization.Tr("SETTING_RESTART"));
            _restartButton.Root.anchorMin = _restartButton.Root.anchorMax = new Vector2(0.5f, 1f);
            _restartButton.Root.anchoredPosition = new Vector2(0, -756);
            _restartButton.OnClick = OnRestartPressed;
            _homeButton = CommonButton.Create(_actionsRoot, "Home", CommonButton.Variant.Orange, Localization.Tr("home"));
            _homeButton.Root.anchorMin = _homeButton.Root.anchorMax = new Vector2(0.5f, 1f);
            _homeButton.Root.anchoredPosition = new Vector2(0, -986);
            _homeButton.OnClick = OnHomePressed;

            // Keep the authored rows for prefab compatibility, but the current
            // release intentionally hides the external support/legal section.
            _cardsRoot = UiFactory.Node(_card, "SettingCards");
            _cardsRoot.anchorMin = _cardsRoot.anchorMax = new Vector2(0.5f, 1f);
            var homeRowButtons = new List<PressButton>(5);
            var homeRowLabels = new List<TMP_Text>(5);
            float y = -640f;
            y = BuildRowCard(y, new[]
            {
                ("Art/Sprites/Setting/setting_ic_weak_help", "help_center", (System.Action)OnHelpPressed),
                ("Art/Sprites/Setting/setting_ic_weak_rate", "rate_us", (System.Action)OnRatePressed),
            }, homeRowButtons, homeRowLabels);
            y -= 16f;
            BuildRowCard(y, new[]
            {
                ("Art/Sprites/Setting/setting_ic_weak_privacy", "privacy_policy",
                    (System.Action)OnPrivacyPressed),
                ("Art/Sprites/Setting/setting_ic_weak_privacy_preference", "privacy_preference",
                    (System.Action)OnPrivacyPreferencePressed),
                ("Art/Sprites/Setting/setting_ic_weak_service", "terms_service",
                    (System.Action)OnTermsPressed),
            }, homeRowButtons, homeRowLabels);
            _homeRowButtons = homeRowButtons.ToArray();
            _homeRowLabels = homeRowLabels.ToArray();

            _version = UiFactory.Label(_card, "Version", "V" + Application.version, 36,
                new Color(0.702f, 0.576f, 0.537f), AssetLib.UiFont, TextAnchor.MiddleCenter, 400, 44);
            ConfigureVersionRect(
                _version.rectTransform,
                GameCardHeight - HomeVersionBottomPadding -
                VersionSize.y * 0.5f);

            BindPrefabRuntime();
            ApplyPresentation(gameMode: false);
            ApplyToggleSprites(soundOn: true, vibrateOn: true);
            if (Application.isPlaying)
                _root.gameObject.SetActive(false);
        }

        /// <summary>Slot card (dialog_card_slot 9-slice) holding tappable rows.</summary>
        float BuildRowCard(
            float top,
            (string icon, string key, System.Action act)[] rows,
            List<PressButton> buttons,
            List<TMP_Text> labels)
        {
            float cardH = rows.Length * HomeRowHeight + HomeCardPadding * 2f;
            var card = UiFactory.Img(_cardsRoot, "RowCard", "Art/Sprites/Setting/dialog_card_slot", 872, cardH);
            card.sprite = AssetLib.Sprite9Design("Art/Sprites/Setting/dialog_card_slot", 100, 100, 100, 100);
            card.type = Image.Type.Sliced;
            card.rectTransform.anchorMin = card.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            card.rectTransform.anchoredPosition = new Vector2(0, top - cardH / 2f);
            for (int i = 0; i < rows.Length; i++)
            {
                var row = UiFactory.Node(card.rectTransform, "Row_" + rows[i].key);
                row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                row.sizeDelta = new Vector2(872 - 96, HomeRowHeight);
                row.anchoredPosition = new Vector2(
                    0,
                    -HomeCardPadding - HomeRowHeight * i - HomeRowHeight * 0.5f);
                var icon = UiFactory.Img(row, "Icon", rows[i].icon, 96, 96);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(48, 0);
                bool hasRedDot = rows[i].key == "help_center";
                float labelWidth = hasRedDot ? HomeHelpTextWidth : HomeRowTextWidth;
                var lbl = UiFactory.Label(row, "Label", Localization.Tr(rows[i].key), 52,
                    new Color(0.4f, 0.298f, 0.263f), AssetLib.UiFont,
                    TextAnchor.MiddleLeft, labelWidth, 100);
                lbl.rectTransform.anchorMin = lbl.rectTransform.anchorMax = new Vector2(0, 0.5f);
                lbl.rectTransform.anchoredPosition = new Vector2(132f + labelWidth * 0.5f, 0);
                ConfigureRowLabel(lbl);
                labels.Add(lbl);
                if (hasRedDot)
                {
                    var redDot = UiFactory.Img(
                        row,
                        "Reddot",
                        "Art/Sprites/Setting/common_reddot",
                        48,
                        48);
                    redDot.raycastTarget = false;
                    redDot.rectTransform.anchorMin =
                        redDot.rectTransform.anchorMax = new Vector2(1, 0.5f);
                    redDot.rectTransform.anchoredPosition = new Vector2(-72, 0);
                }
                var hit = UiFactory.Rect(
                    row,
                    "Hit",
                    new Color(0, 0, 0, 0),
                    872 - 96,
                    HomeRowHeight);
                var act = rows[i].act;
                UiFactory.MakeButton(hit.gameObject, () => act());
                buttons.Add(hit.GetComponent<PressButton>());
            }
            return top - cardH;
        }

        Image MakeToggle(
            float x,
            string spritePath,
            string label,
            System.Action onClick,
            out TMP_Text labelText,
            out PressButton pressButton)
        {
            var node = UiFactory.Node(_card, "Toggle_" + label);
            node.anchorMin = node.anchorMax = new Vector2(0.5f, 1f);
            node.anchoredPosition = new Vector2(x, -388);
            var btn = UiFactory.Img(node, "Btn", spritePath, 200, 200);
            labelText = UiFactory.Label(node, "Label", label, 52, new Color32(0x66, 0x4C, 0x43, 0xFF), AssetLib.UiFont, TextAnchor.MiddleCenter, 260, 64);
            labelText.rectTransform.anchoredPosition = new Vector2(0, -140);
            UiFactory.MakeButton(btn.gameObject, onClick);
            pressButton = btn.GetComponent<PressButton>();
            return btn;
        }

        /// <summary>Injects the runtime owner into SettingPage.prefab.</summary>
        public void InitializePrefabRuntime(Transform parent, BubblePage page)
        {
            _page = page;
            if (_root == null)
                _root = transform as RectTransform;
            if (_root != null && parent != null && _root.parent != parent)
                _root.SetParent(parent, false);
            BindPrefabRuntime();
            if (_root != null)
                _root.gameObject.SetActive(false);
            _closing = false;
        }

        /// <summary>Restores System.Action fields after prefab instantiation.</summary>
        public void BindPrefabRuntime()
        {
            if (_closePress != null) _closePress.OnClick = OnClosePressed;
            if (_soundPress != null)
            {
                _soundPress.OnClick = OnSoundPressed;
                _soundPress.PlaySound = true;
            }
            if (_vibPress != null)
            {
                _vibPress.OnClick = OnVibrationPressed;
                _vibPress.PlaySound = true;
            }
            if (_restartButton != null)
            {
                // CommonButton contains its own PressButton. Both delegates are
                // runtime-only and must be restored after prefab
                // deserialization: PressButton -> CommonButton -> page action.
                _restartButton.BindPrefabRuntime();
                _restartButton.OnClick = OnRestartPressed;
            }
            if (_homeButton != null)
            {
                _homeButton.BindPrefabRuntime();
                _homeButton.OnClick = OnHomePressed;
            }

            var rowActions = new System.Action[]
            {
                OnHelpPressed,
                OnRatePressed,
                OnPrivacyPressed,
                OnPrivacyPreferencePressed,
                OnTermsPressed,
            };
            if (_homeRowButtons != null)
            {
                int count = Mathf.Min(_homeRowButtons.Length, rowActions.Length);
                for (int i = 0; i < count; i++)
                    if (_homeRowButtons[i] != null)
                        _homeRowButtons[i].OnClick = rowActions[i];
            }

            string settings = Localization.Tr("settings");
            if (_titleBack != null) _titleBack.text = settings;
            if (_title != null) _title.text = settings;
            if (_soundLabel != null) _soundLabel.text = Localization.Tr("sound");
            if (_vibLabel != null) _vibLabel.text = Localization.Tr("vibration");
            if (_restartButton != null) _restartButton.SetText(Localization.Tr("SETTING_RESTART"));
            if (_homeButton != null) _homeButton.SetText(Localization.Tr("home"));

            string[] rowKeys =
            {
                "help_center",
                "rate_us",
                "privacy_policy",
                "privacy_preference",
                "terms_service",
            };
            if (_homeRowLabels != null)
            {
                int count = Mathf.Min(_homeRowLabels.Length, rowKeys.Length);
                for (int i = 0; i < count; i++)
                {
                    if (_homeRowLabels[i] != null)
                    {
                        _homeRowLabels[i].text = Localization.Tr(rowKeys[i]);
                        ConfigureRowLabel(_homeRowLabels[i]);
                    }
                }
            }
            if (_version != null) _version.text = "V" + Application.version;

            // Some imported prefab text nodes still carry TMP's default
            // LiberationSans material. Rebind the project's existing main
            // font after all localized copy is assigned, then build title
            // effects from that compatible atlas.
            AssetLib.ApplyLocalizedFonts(_card);
            ApplyTitleStyle();
        }

        bool HasPrefabReferences()
        {
            return _root != null
                && _content != null
                && _card != null
                && _contentCg != null
                && _overlayCg != null
                && _soundBtn != null
                && _vibBtn != null
                && _actionsRoot != null
                && _cardsRoot != null
                && _version != null
                && _closePress != null
                && _soundPress != null
                && _vibPress != null
                && _restartButton != null
                && _homeButton != null
                && _homeRowButtons != null
                && _homeRowButtons.Length >= 5
                && _homeRowLabels != null
                && _homeRowLabels.Length >= 5;
        }

        void OnSoundPressed()
        {
            bool oldValue = SaveState.SoundOn;
            SaveState.SoundOn = !oldValue;
            FunSmithTelemetry.TrackSettingsChange(
                "sound",
                oldValue,
                SaveState.SoundOn,
                _page != null ? "game_setting" : "home_setting");
            RefreshToggles();
            if (SoundManager.I != null) SoundManager.I.Play("button");
        }

        void OnVibrationPressed()
        {
            bool oldValue = SaveState.VibrateOn;
            bool nowOn = !oldValue;
            SaveState.VibrateOn = nowOn;
            FunSmithTelemetry.TrackSettingsChange(
                "vibrate",
                oldValue,
                nowOn,
                _page != null ? "game_setting" : "home_setting");
            RefreshToggles();
            if (nowOn)
                Haptics.Play(HapticLevel.Medium);
        }

        void OnClosePressed()
        {
            if (!BeginDismiss()) return;
            Haptics.Play(HapticLevel.Weak);
        }

        void OnRestartPressed()
        {
            if (!BeginDismiss()) return;
            Haptics.Play(HapticLevel.Weak);
            if (_page != null) _page.RestartLevel();
        }

        void OnHomePressed()
        {
            if (!BeginDismiss()) return;
            Haptics.Play(HapticLevel.Weak);
            if (_page != null) _page.RequestGoHome();
        }

        void OnHelpPressed()
        {
            if (!HelpCenterService.TryShow())
                RefreshHomeRows();
        }

        void OnRatePressed()
        {
            if (!InAppReviewService.TryOpenStore())
                RefreshHomeRows();
        }

        void OnPrivacyPressed()
        {
            if (!PlatformLinks.TryOpenPrivacyPolicy())
                RefreshHomeRows();
        }

        void OnPrivacyPreferencePressed()
        {
            if (!ConsentService.TryShowPrivacyPreferences())
                RefreshHomeRows();
        }

        void OnTermsPressed()
        {
            if (!PlatformLinks.TryOpenTerms())
                RefreshHomeRows();
        }

        void RefreshToggles()
        {
            ApplyToggleSprites(SaveState.SoundOn, SaveState.VibrateOn);
        }

        void ApplyToggleSprites(bool soundOn, bool vibrateOn)
        {
            if (_soundBtn != null)
            {
                ApplyToggleSprite(_soundBtn, soundOn
                    ? SkinRoot + "/Setting/sound_on"
                    : SkinRoot + "/Setting/sound_off");
            }
            if (_vibBtn != null)
            {
                ApplyToggleSprite(_vibBtn, vibrateOn
                    ? SkinRoot + "/Setting/vibration_on"
                    : SkinRoot + "/Setting/vibration_off");
            }
        }

        static void ApplyToggleSprite(Image image, string path)
        {
            if (image == null) return;
            image.sprite = AssetLib.Sprite(path);
            image.preserveAspect = true;
            if (image.sprite != null)
                image.rectTransform.sizeDelta = image.sprite.rect.size;
        }

        public bool IsShown => _root != null && _root.gameObject.activeSelf;

        public void Show(bool gameMode = true)
        {
            _gameMode = gameMode;
            if (_closeRoutine != null)
            {
                StopCoroutine(_closeRoutine);
                _closeRoutine = null;
            }
            _closing = false;
            // Rebind on every presentation as well as on instantiation. This
            // keeps persistent prefab delegates valid after an Editor domain
            // reload and also repairs callers that retained the same settings
            // instance between openings.
            if (_page == null && gameMode && App.I != null)
                _page = App.I.Page;
            BindPrefabRuntime();
            ApplyPresentation(gameMode);
            RefreshToggles();
            if (gameMode) _page?.Specials?.PauseCountdowns();
            _root.gameObject.SetActive(true);
            StartCoroutine(GenericPopup.Open(_content, _contentCg, _overlayCg));
        }

        void ApplyPresentation(bool gameMode)
        {
            // Gameplay adds restart/home actions; home mode stays compact and
            // exposes only the common controls plus the version label.
            if (_actionsRoot != null)
                _actionsRoot.gameObject.SetActive(gameMode);
            if (_cardsRoot != null)
                _cardsRoot.gameObject.SetActive(false);
            if (_version != null)
                _version.gameObject.SetActive(!gameMode);
            if (_card == null) return;

            if (gameMode)
            {
                _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
                _card.sizeDelta = new Vector2(GameCardWidth, GameCardHeight);
                _card.anchoredPosition = new Vector2(0f, 63f);
                Image background = _card.Find("Bg")?.GetComponent<Image>();
                if (background != null)
                {
                    background.sprite = AssetLib.Sprite(SkinRoot + "/Setting/panel");
                    background.type = Image.Type.Simple;
                    background.preserveAspect = true;
                    background.pixelsPerUnitMultiplier = 1f;
                }
            }
            else
            {
                _card.anchoredPosition = Vector2.zero;
                Image background = _card.Find("Bg")?.GetComponent<Image>();
                if (background != null)
                {
                    background.sprite = AssetLib.Sprite9Design(
                        SkinRoot + "/Setting/panel",
                        PanelSliceLeft,
                        PanelSliceTop,
                        PanelSliceRight,
                        PanelSliceBottom);
                    background.type = Image.Type.Sliced;
                    background.preserveAspect = false;
                    background.pixelsPerUnitMultiplier = 1f;
                }
                RefreshHomeRows();
            }
        }

        /// <summary>
        /// Retains the authored prefab bindings while hiding the complete
        /// external support/legal section. Both cards close to zero height so
        /// the version copy moves directly below the sound/vibration controls.
        /// </summary>
        public void RefreshHomeRows()
        {
            if (_homeRowButtons == null || _cardsRoot == null || _card == null)
                return;

            bool[] visible = { false, false, false, false, false };

            int count = Mathf.Min(_homeRowButtons.Length, visible.Length);
            for (int i = 0; i < count; i++)
            {
                var row = RowRoot(i);
                if (row != null)
                    row.gameObject.SetActive(visible[i]);
            }

            float top = 640f;
            bool hasPreviousCard = false;
            LayoutRowCard(new[] { 0, 1 }, visible, ref top, ref hasPreviousCard);
            LayoutRowCard(new[] { 2, 3, 4 }, visible, ref top, ref hasPreviousCard);

            // The PSD panel is a fixed 1019x1264 illustration, not a compact
            // nine-slice shell. Keep the home layout in that same coordinate
            // system so the title remains in the blue header and the version
            // remains inside the illustrated bottom edge.
            if (_version != null)
            {
                ConfigureVersionRect(
                    _version.rectTransform,
                    GameCardHeight - HomeVersionBottomPadding -
                    VersionSize.y * 0.5f);
            }
            _card.sizeDelta = new Vector2(GameCardWidth, GameCardHeight);
        }

        void LayoutRowCard(
            int[] rowIndices,
            bool[] visibility,
            ref float top,
            ref bool hasPreviousCard)
        {
            RectTransform card = null;
            int visibleCount = 0;
            for (int i = 0; i < rowIndices.Length; i++)
            {
                int index = rowIndices[i];
                var row = RowRoot(index);
                if (row == null) continue;
                if (card == null) card = row.parent as RectTransform;
                if (index >= visibility.Length || !visibility[index]) continue;

                row.anchoredPosition = new Vector2(
                    0,
                    -(HomeCardPadding + HomeRowHeight * (visibleCount + 0.5f)));
                visibleCount++;
            }

            if (card == null)
                return;
            if (visibleCount == 0)
            {
                card.gameObject.SetActive(false);
                return;
            }

            if (hasPreviousCard)
                top += 16f;
            float height = HomeCardPadding * 2f + HomeRowHeight * visibleCount;
            card.gameObject.SetActive(true);
            card.sizeDelta = new Vector2(872f, height);
            card.anchoredPosition = new Vector2(0, -(top + height * 0.5f));
            top += height;
            hasPreviousCard = true;
        }

        RectTransform RowRoot(int index)
        {
            if (_homeRowButtons == null
                || index < 0
                || index >= _homeRowButtons.Length
                || _homeRowButtons[index] == null)
                return null;
            return _homeRowButtons[index].transform.parent as RectTransform;
        }

        static void ConfigureRowLabel(TMP_Text label)
        {
            if (label == null) return;
            label.enableAutoSizing = true;
            label.fontSizeMin = 28;
            label.fontSizeMax = 52;
        }

        void ApplyTitleStyle()
        {
            if (_titleBack != null)
            {
                ConfigureTitleRect(_titleBack.rectTransform, TitleBackPosition);
                TmpTextStyle.ClearEffects(_titleBack);
                TmpTextStyle.ApplyOutline(
                    _titleBack,
                    new Color32(0x00, 0x31, 0x5C, 0xFF),
                    32f);
            }
            if (_title != null)
            {
                ConfigureTitleRect(_title.rectTransform, TitlePosition);
                TmpTextStyle.ClearEffects(_title);
                TmpTextStyle.ApplyShadow(
                    _title,
                    new Color(0.749f, 0.875f, 1f, 1f),
                    new Vector2(0f, -3f));
            }
        }

        static void ConfigureTitleRect(RectTransform rect, Vector2 position)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = TitleSize;
        }

        static void ConfigureVersionRect(RectTransform rect, float top)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = VersionSize;
        }

        void Dismiss()
        {
            BeginDismiss();
        }

        bool BeginDismiss()
        {
            if (_closing || !IsShown)
                return false;
            _closing = true;
            if (_gameMode) _page?.Specials?.ResumeCountdowns();
            _closeRoutine = StartCoroutine(CloseCo());
            return true;
        }

        IEnumerator CloseCo()
        {
            yield return GenericPopup.Close(_content, _contentCg, _overlayCg);
            _root.gameObject.SetActive(false);
            _closing = false;
            _closeRoutine = null;
        }

        void Update()
        {
            if (!Application.isPlaying || !IsShown || _closing)
                return;
            if (Input.GetKeyDown(KeyCode.Escape))
                BeginDismiss();
        }
    }

    /// <summary>GenericPopup.res open/close animation (Mark @ 0.3s).</summary>
    public static class GenericPopup
    {
        public static IEnumerator Open(RectTransform content, CanvasGroup contentCg, CanvasGroup overlayCg)
        {
            overlayCg.alpha = 0;
            contentCg.alpha = 0;
            content.localScale = Vector3.one * 0.7f;
            float t = 0;
            const float dur = 0.3f;
            while (t < dur)
            {
                t += Time.deltaTime;
                overlayCg.alpha = Mathf.Clamp01(t / 0.003f);
                contentCg.alpha = Mathf.Clamp01(t / 0.055f);
                // scale 0.7 -> 1.05 @0.1 -> 1.0 @0.3
                float s;
                if (t < 0.0996f) s = Mathf.Lerp(0.70f, 1.05f, t / 0.0996f);
                else s = Mathf.Lerp(1.05f, 1.0f, (t - 0.0996f) / (dur - 0.0996f));
                content.localScale = Vector3.one * s;
                yield return null;
            }
            content.localScale = Vector3.one;
            contentCg.alpha = 1;
            overlayCg.alpha = 1;
        }

        public static IEnumerator Close(RectTransform content, CanvasGroup contentCg, CanvasGroup overlayCg)
        {
            float t = 0;
            const float dur = 0.3167f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float s;
                if (t < 0.149f) s = Mathf.Lerp(1.0f, 1.05f, t / 0.149f);
                else s = Mathf.Lerp(1.05f, 0.80f, (t - 0.149f) / (dur - 0.149f));
                content.localScale = Vector3.one * s;
                if (t > dur - 0.05f)
                {
                    float k = 1f - (dur - t) / 0.05f;
                    contentCg.alpha = 1f - k;
                    overlayCg.alpha = 1f - k;
                }
                yield return null;
            }
            contentCg.alpha = 0;
            overlayCg.alpha = 0;
        }
    }
}
