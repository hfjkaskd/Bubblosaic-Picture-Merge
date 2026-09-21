using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Port of top_game_bar.gd + top_game_bar.tscn (old-UI branch).
    /// Layout (design px, y down): bar height 390; Panels offset_top 50;
    /// target panel anchors [0,1/3] shifted +62; moves panel [2/3,1] shifted -62;
    /// both v-shifted -10. Cards 260x120 rounded-24 black(0.4).
    /// Center: DolphinDecoration spine at panel center offset (0,-27.5), scale 0.72.
    /// </summary>
    public class TopGameBar : MonoBehaviour
    {
        // Godot top_game_bar.tscn: height 390, panels top 50; the recording device
        // adds a safe-area top inset (_safe_top / _safe_top_grow_bottom groups).
        public static float TOPBAR_HEIGHT => 390f + App.SafeTopDesign;
        static float PANELS_TOP => 50f + App.SafeTopDesign;
        const float TARGET_PANEL_SHIFT = 36f;
        const float MOVES_PANEL_SHIFT = -13f;
        const float SIDE_PANEL_UP = 28f;
        const int LOW_MOVES_THRESHOLD = 5;
        static readonly Color LOW_MOVES_ORANGE = new Color32(0xEE, 0x7F, 0x00, 0xFF);
        static readonly Color BANNER_COLOR = new Color32(0x0A, 0x68, 0xB7, 0xFF);
        static readonly Color COUNT_NORMAL = new Color32(0x0B, 0x2D, 0x69, 0xFF);
        static readonly Color COUNT_HIGHLIGHT = new Color32(0xFE, 0x9D, 0x00, 0xFF);
        static readonly Color REWARD_NUMBER_COLOR = new Color32(0x0B, 0x2D, 0x69, 0xFF);

        // coin reward flight
        const int REWARD_FLY_COINS_MAX = 6;
        const float REWARD_FLY_DURATION = 0.9f;
        const float REWARD_FLY_COIN_DUR = 0.5f;
        const float REWARD_FLY_STAGGER = 0.08f;
        const float REWARD_FLY_COIN_SIZE = 136f;
        const float REWARD_FLY_HOLD_AFTER_LAND = 0.5f;
        const float REWARD_FLY_FADE_OUT_SEC = 0.3f;
        const float REWARD_FLY_Y_OFFSET = 70f;
        const float REWARD_ICON_SIZE = 92f;
        const float REWARD_GAP = 12f;
        const int COIN_TRAIL_AMOUNT = 16;
        const float COIN_TRAIL_LIFETIME = 0.5f;
        static readonly Color COIN_TRAIL_COLOR = new Color(1f, 1f, 1f, 0.85f);

        const float COINS_GAIN_RISE_PX = 40f;
        const float COINS_GAIN_ENTER_DELAY = 0.2f;
        const float COINS_GAIN_IN_SEC = 0.4f;
        const float COINS_GAIN_OUT_SEC = 0.5f;
        static readonly Color COINS_GAIN_TOP = new Color32(0xFF, 0xF6, 0x9E, 0xFF);
        static readonly Color COINS_GAIN_BOTTOM = new Color32(0xFF, 0xE6, 0x67, 0xFF);
        static readonly Color COINS_GAIN_OUTLINE = new Color32(0x9C, 0x52, 0x0A, 0xFF);
        const float COINS_GAIN_OUTLINE_SIZE = 15f;
        const float COINS_GAIN_SHADOW_OFFSET_Y = -3f;

        [SerializeField] public RectTransform Root;
        [SerializeField] TMP_Text _targetBanner;
        [SerializeField] TMP_Text _movesBanner;
        [SerializeField] TMP_Text _targetCount;
        [SerializeField] TMP_Text _movesNumber;
        [SerializeField] RectTransform _targetPanel;
        [SerializeField] RectTransform _movesPanel;
        [SerializeField] RectTransform _targetCard;
        [SerializeField] RectTransform _movesCard;
        [SerializeField] RectTransform _rewardRoot;
        [SerializeField] Image _rewardIcon;
        [SerializeField] TMP_Text _rewardNumber;
        [SerializeField] TMP_Text _coinsGainLabel;
        [SerializeField] DolphinDecoration _dolphin;
        [SerializeField] BonusChestDecoration _bonusChest;
        [SerializeField] Vector2 _portraitOffset = new Vector2(0f, 170f);
        public DolphinDecoration Dolphin => _dolphin;
        public BonusChestDecoration BonusChest => _bonusChest;

        public int SlotCount { get; private set; }
        public int ReservedCount { get; private set; }
        public int FilledCount { get; private set; }
        public bool IsFull => SlotCount > 0 && FilledCount >= SlotCount;
        public int RemainingCount => Mathf.Max(SlotCount - FilledCount, 0);

        int _rewardBase;
        bool _lowMovesActive, _movesUnlimitedActive;
        Coroutine _punchCo, _flashCo, _rewardPunchCo;
        LastLinkShine _lastLinkShine;

        sealed class CoinTrailDot
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Drag;
            public float Age;
        }

        public static Texture2D RoundedRectTex(int w, int h, int r, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(Mathf.Max(r - x, x - (w - 1 - r)), 0);
                    float dy = Mathf.Max(Mathf.Max(r - y, y - (h - 1 - r)), 0);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    var c = color; c.a *= a;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }

        public void Build(Transform parent)
        {
            // Prefab instances already contain the complete fixed hierarchy.
            // Build remains as the authoring/legacy fallback scaffold only.
            if (HasPrefabReferences())
            {
                InitializePrefabRuntime(parent);
                return;
            }

            var ownRoot = transform as RectTransform;
            Root = ownRoot != null && transform != parent
                ? ownRoot
                : UiFactory.Node((RectTransform)parent, "TopGameBar");
            if (Root.parent != parent)
                Root.SetParent(parent, false);
            Root.gameObject.name = "TopGameBar";
            Root.anchorMin = new Vector2(0, 1);
            Root.anchorMax = new Vector2(1, 1);
            Root.pivot = new Vector2(0.5f, 1);
            Root.offsetMin = new Vector2(0, -TOPBAR_HEIGHT);
            Root.offsetMax = new Vector2(0, 0);

            // Panels container (offset_top 50)
            var panels = UiFactory.Node(Root, "Panels");
            panels.anchorMin = Vector2.zero; panels.anchorMax = Vector2.one;
            panels.offsetMin = Vector2.zero; panels.offsetMax = new Vector2(0, -PANELS_TOP);

            // target panel: anchors [0, 1/3], offset +62 h, -10 v
            _targetPanel = UiFactory.Node(panels, "TargetPanel");
            _targetPanel.anchorMin = new Vector2(0f, 0f);
            _targetPanel.anchorMax = new Vector2(1f / 3f, 1f);
            _targetPanel.offsetMin = new Vector2(TARGET_PANEL_SHIFT, SIDE_PANEL_UP);
            _targetPanel.offsetMax = new Vector2(TARGET_PANEL_SHIFT, SIDE_PANEL_UP);

            _movesPanel = UiFactory.Node(panels, "MovesPanel");
            _movesPanel.anchorMin = new Vector2(2f / 3f, 0f);
            _movesPanel.anchorMax = new Vector2(1f, 1f);
            _movesPanel.offsetMin = new Vector2(MOVES_PANEL_SHIFT, SIDE_PANEL_UP);
            _movesPanel.offsetMax = new Vector2(MOVES_PANEL_SHIFT, SIDE_PANEL_UP);

            _targetCard = MakeCard(_targetPanel);
            _movesCard = MakeCard(_movesPanel);

            _targetBanner = MakeBanner(_targetPanel, Localization.Tr("TARGET"));
            _targetCount = MakeNumber(_targetPanel, "0/0", 216f);
            _movesBanner = MakeBanner(_movesPanel, Localization.Tr("str_moves"));
            _movesNumber = MakeNumber(_movesPanel, "62", 208f);

            // reward coin (hidden)
            _rewardRoot = UiFactory.Node(_targetPanel, "RewardCoin");
            _rewardRoot.anchorMin = Vector2.zero; _rewardRoot.anchorMax = Vector2.one;
            _rewardRoot.offsetMin = Vector2.zero; _rewardRoot.offsetMax = Vector2.zero;
            _rewardIcon = UiFactory.Img(_rewardRoot, "Icon", "Art/Sprites/Continue/coin_icon", REWARD_ICON_SIZE, REWARD_ICON_SIZE);
            _rewardNumber = UiFactory.Label(_rewardRoot, "Number", "0", 48, Color.white, AssetLib.NumFont, TextAnchor.MiddleCenter, 92, 58);
            _rewardRoot.gameObject.SetActive(false);

            // coins gain floating label
            _coinsGainLabel = UiFactory.Label(Root, "CoinsGain", "", 56, COINS_GAIN_TOP, AssetLib.NumFont, TextAnchor.MiddleLeft, 300, 70);
            ConfigureCoinsGainLabel();
            _coinsGainLabel.gameObject.SetActive(false);

            // center panel = anchors [1/3,2/3] of panels area; compute in world:
            // done by BubblePage via InitDolphin after canvas layout settles.
            ApplyDeviceLayout();
            BindPrefabRuntime();
        }

        /// <summary>Injects the HUD parent for TopGameBar.prefab.</summary>
        public void InitializePrefabRuntime(Transform parent)
        {
            if (Root == null)
                Root = transform as RectTransform;
            if (Root != null && parent != null && Root.parent != parent)
                Root.SetParent(parent, false);
            ApplyDeviceLayout();
            BindPrefabRuntime();
        }

        public void ApplyDeviceLayout()
        {
            if (Root != null)
            {
                var offsetMin = Root.offsetMin;
                offsetMin.y = -TOPBAR_HEIGHT;
                Root.offsetMin = offsetMin;
            }

            var panels = _targetPanel != null
                ? _targetPanel.parent as RectTransform
                : _movesPanel != null ? _movesPanel.parent as RectTransform : null;
            if (panels != null)
            {
                var offsetMax = panels.offsetMax;
                offsetMax.y = -PANELS_TOP;
                panels.offsetMax = offsetMax;
            }

            if (_targetPanel != null)
                _targetPanel.anchoredPosition = new Vector2(
                    TARGET_PANEL_SHIFT,
                    SIDE_PANEL_UP);
            if (_movesPanel != null)
                _movesPanel.anchoredPosition = new Vector2(
                    MOVES_PANEL_SHIFT,
                    SIDE_PANEL_UP);

            if (_dolphin != null)
            {
                _dolphin.ApplyHudLayout(
                    App.DesignToWorld(DolphinPanelCenterDesign),
                    App.WorldPerDesign);
            }
        }

        // The Godot CenterPanel spans the live middle third of the expanded
        // viewport, so its global centre follows the actual design width.
        // A fixed x=540 only centres the portrait at the reference 1080 width.
        Vector2 DolphinPanelCenterDesign =>
            new Vector2(DeviceLayout.ViewWidth * 0.5f + _portraitOffset.x, PANELS_TOP + _portraitOffset.y);

        /// <summary>Restores runtime-only bindings after prefab instantiation.</summary>
        public void BindPrefabRuntime()
        {
            RefreshLocalizedLabels();
            if (_dolphin != null)
                _dolphin.BindPrefabRuntime();
            ConfigureRewardNumber();
            ConfigureCoinsGainLabel();
        }

        public void RefreshLocalizedLabels()
        {
            if (_targetBanner != null)
                _targetBanner.text = Localization.Tr("TARGET");
            if (_movesBanner != null)
                _movesBanner.text = Localization.Tr("str_moves");
        }

        void ConfigureRewardNumber()
        {
            if (_rewardNumber == null) return;
            TmpTextStyle.ClearEffects(_rewardNumber);
            _rewardNumber.color = REWARD_NUMBER_COLOR;
            _rewardNumber.enableVertexGradient = false;
        }

        void ConfigureCoinsGainLabel()
        {
            if (_coinsGainLabel == null) return;
            TmpTextStyle.ClearEffects(_coinsGainLabel);
            TmpTextStyle.ApplyOutlineAndShadow(
                _coinsGainLabel,
                COINS_GAIN_OUTLINE,
                COINS_GAIN_OUTLINE_SIZE,
                COINS_GAIN_OUTLINE,
                new Vector2(0f, COINS_GAIN_SHADOW_OFFSET_Y));
            _coinsGainLabel.color = Color.white;
            _coinsGainLabel.enableVertexGradient = true;
            _coinsGainLabel.colorGradient = new VertexGradient(
                COINS_GAIN_TOP,
                COINS_GAIN_TOP,
                COINS_GAIN_BOTTOM,
                COINS_GAIN_BOTTOM);
        }

        bool HasPrefabReferences()
        {
            return Root != null
                && _targetBanner != null
                && _movesBanner != null
                && _targetCount != null
                && _movesNumber != null
                && _targetPanel != null
                && _movesPanel != null
                && _targetCard != null
                && _rewardRoot != null
                && _rewardIcon != null
                && _rewardNumber != null
                && _coinsGainLabel != null;
        }

        void EnsureDolphin()
        {
            if (_dolphin != null)
            {
                _dolphin.BindPrefabRuntime();
                return;
            }

            var catalog = PrefabCatalog.Current;
            if (catalog != null && catalog.DolphinDecoration != null)
                _dolphin = PrefabCatalog.InstantiateComponent<DolphinDecoration>(catalog.DolphinDecoration);

            if (_dolphin == null)
            {
                var dol = new GameObject("DolphinDecoration");
                _dolphin = dol.AddComponent<DolphinDecoration>();
            }
        }

        public void InitDolphin()
        {
            EnsureDolphin();
            if (App.I != null && !_dolphin.transform.IsChildOf(App.I.WorldRoot))
                _dolphin.transform.SetParent(App.I.WorldRoot, false);
            Vector3 world = App.DesignToWorld(DolphinPanelCenterDesign);
            _dolphin.InitializePrefabRuntime(world, App.WorldPerDesign);
            if (AppConfig.BonusLevel)
            {
                EnsureBonusChest();
                _bonusChest.InitializePrefabRuntime(world, App.WorldPerDesign);
            }
        }

        void EnsureBonusChest()
        {
            if (_bonusChest != null) return;
            var chest = new GameObject("BonusChest");
            _bonusChest = chest.AddComponent<BonusChestDecoration>();
            if (App.I != null)
                chest.transform.SetParent(App.I.WorldRoot, false);
        }

        public void SetLevelDecoration(bool hard, bool bonus)
        {
            EnsureDolphin();
            _dolphin.SetHardFrame(
                hard && !bonus && AppConfig.HardLevelDiff);
            _dolphin.SetHiddenForBonus(bonus);
            if (AppConfig.BonusLevel || bonus)
            {
                EnsureBonusChest();
                _bonusChest.SetBonusVisible(bonus);
            }
        }

        RectTransform MakeCard(RectTransform parent)
        {
            bool target = parent == _targetPanel;
            var rt = UiFactory.Node(parent, "WhiteCard");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            string path = target
                ? "Art/Sprites/PsdSkin20260807/Gameplay/target_card"
                : "Art/Sprites/PsdSkin20260807/Gameplay/moves_card";
            rt.sizeDelta = target
                ? new Vector2(354f, 207f)
                : new Vector2(350f, 193f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = AssetLib.Sprite(path);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
            return rt;
        }

        TMP_Text MakeBanner(RectTransform parent, string text)
        {
            var t = UiFactory.Label(parent, "Banner", text, 40, BANNER_COLOR, AssetLib.UiFont, TextAnchor.MiddleCenter, 216, 52);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            // godot offsets: top=-48 bottom=-8 (y down) -> center y = -28 design -> +28 unity
            rt.anchoredPosition = new Vector2(0, 40);
            return t;
        }

        TMP_Text MakeNumber(RectTransform parent, string text, float width)
        {
            var t = UiFactory.Label(parent, "Num", text, 58, COUNT_NORMAL, AssetLib.NumFont, TextAnchor.MiddleCenter, width, 72);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            // godot offsets: top=-10 bottom=50 -> center y=+20 design -> -20 unity
            rt.anchoredPosition = new Vector2(0, -27);
            return t;
        }

        public Vector2 TargetPillScreenCenter() => _targetCard.position;
        public Vector2 MovesNumberScreenCenter() => _movesNumber.rectTransform.position;
        public RectTransform TargetCard => _targetCard;

        public void PlayLastLinkShine()
        {
            if (_targetCard == null || _targetCard.rect.width <= 0f) return;
            if (_lastLinkShine != null)
                Destroy(_lastLinkShine.gameObject);
            _lastLinkShine = LastLinkShine.Play(_targetCard);
        }

        // ------------------------------------------------------- target & moves
        public void SetupTarget(int slotCount)
        {
            ResetRewardMode();
            SlotCount = Mathf.Max(slotCount, 0);
            ReservedCount = 0;
            FilledCount = 0;
            RefreshCount();
            if (_flashCo != null) StopCoroutine(_flashCo);
            _targetCount.color = COUNT_NORMAL;
        }

        public int ReserveNextIndex() => ReservedCount++;

        public void FillCollected(int index, bool animate = true)
        {
            FilledCount = Mathf.Max(FilledCount, index + 1);
            RefreshCount();
            if (animate)
            {
                PunchCount();
                FlashCountColor();
            }
        }

        void RefreshCount() { _targetCount.text = $"{FilledCount}/{SlotCount}"; }

        public void SetMovesVisible(bool on)
        {
            _movesPanel.gameObject.SetActive(on);
            if (on) _movesNumber.canvasRenderer.SetAlpha(1f);
        }

        public void SetMoves(int stepsLeft)
        {
            ExitUnlimitedFontIfNeeded();
            _movesNumber.text = stepsLeft.ToString();
            if (stepsLeft <= LOW_MOVES_THRESHOLD) StartLowMovesWarning();
            else StopLowMovesWarning();
        }

        void SetMovesNumber(int n)
        {
            ExitUnlimitedFontIfNeeded();
            _movesNumber.text = n.ToString();
            _movesNumber.color = n <= LOW_MOVES_THRESHOLD
                ? LOW_MOVES_ORANGE
                : COUNT_NORMAL;
        }

        void ExitUnlimitedFontIfNeeded()
        {
            if (!_movesUnlimitedActive) return;
            _movesUnlimitedActive = false;
            _movesNumber.fontSize = 52;
        }

        public void SetMovesUnlimited()
        {
            _movesUnlimitedActive = true;
            _movesNumber.text = "∞";
            _movesNumber.fontSize = 75;
            StopLowMovesWarning();
        }

        void StartLowMovesWarning()
        {
            if (_lowMovesActive) return;
            _lowMovesActive = true;
            _movesNumber.color = LOW_MOVES_ORANGE;
        }

        void StopLowMovesWarning()
        {
            if (!_lowMovesActive) return;
            _lowMovesActive = false;
            _movesNumber.color = COUNT_NORMAL;
        }

        public void HideMovesNumberForIntro() { _movesNumber.canvasRenderer.SetAlpha(0f); }
        public void RevealMovesNumber() { _movesNumber.CrossFadeAlpha(1f, 0.2f, false); }

        void PunchCount()
        {
            if (_punchCo != null) StopCoroutine(_punchCo);
            _punchCo = StartCoroutine(PunchCo(_targetCount.transform, 1.5f, 0.4f));
        }

        IEnumerator PunchCo(Transform t, float peak, float recover)
        {
            t.localScale = Vector3.one * peak;
            yield return Tween.Scale(t, Vector3.one, recover, Ease.OutBack);
        }

        void FlashCountColor()
        {
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashCo());
        }

        IEnumerator FlashCo()
        {
            _targetCount.color = COUNT_HIGHLIGHT;
            yield return new WaitForSeconds(1.0f);
            float t = 0;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                _targetCount.color = Color.Lerp(
                    COUNT_HIGHLIGHT,
                    COUNT_NORMAL,
                    t / 0.2f);
                yield return null;
            }
            _targetCount.color = COUNT_NORMAL;
        }

        // ------------------------------------------------------- reward mode
        public void EnterRewardMode(int currentCoins)
        {
            _rewardBase = currentCoins;
            _targetBanner.text = Localization.Tr("reward");
            _targetCount.gameObject.SetActive(false);
            _rewardRoot.gameObject.SetActive(true);
            _rewardNumber.text = currentCoins.ToString();
            LayoutRewardCoin();
            _targetBanner.gameObject.SetActive(false);
            ConfigureRewardNumber();
            StopLowMovesWarning();
        }

        public void ResetRewardMode()
        {
            _rewardBase = 0;
            _targetBanner.text = Localization.Tr("TARGET");
            _targetBanner.gameObject.SetActive(true);
            _targetCount.gameObject.SetActive(true);
            _rewardRoot.gameObject.SetActive(false);
            StopLowMovesWarning();
        }

        void LayoutRewardCoin(string numText = "")
        {
            string t = string.IsNullOrEmpty(numText) ? _rewardNumber.text : numText;
            float numW = _rewardNumber.GetPreferredValues(t, 400f, 100f).x;
            float total = REWARD_ICON_SIZE + REWARD_GAP + numW;
            float left = -total * 0.5f;
            _rewardIcon.rectTransform.anchorMin = _rewardIcon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rewardIcon.rectTransform.anchoredPosition = new Vector2(left + REWARD_ICON_SIZE * 0.5f, 0);
            float nx = left + REWARD_ICON_SIZE + REWARD_GAP - 8f;
            _rewardNumber.rectTransform.anchorMin = _rewardNumber.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rewardNumber.rectTransform.sizeDelta = new Vector2(numW + 4, 58);
            _rewardNumber.alignment = TextAlignmentOptions.MidlineLeft;
            _rewardNumber.rectTransform.anchoredPosition = new Vector2(nx + (numW + 4) * 0.5f, 0);
        }

        public void SetRewardNumber(int v) { _rewardNumber.text = v.ToString(); }

        void PunchRewardIcon()
        {
            if (_rewardPunchCo != null) StopCoroutine(_rewardPunchCo);
            _rewardPunchCo = StartCoroutine(PunchCo(_rewardIcon.transform, 1.22f, 0.18f));
        }

        /// <summary>Port of play_coin_reward: number roll + staggered coin flights.</summary>
        public IEnumerator PlayCoinReward(int reward, int startMoves)
        {
            if (reward <= 0) yield break;
            SoundManager.I.Play("game_completed");

            Vector2 startCenter = (Vector2)_movesNumber.rectTransform.position
                + new Vector2(0, -REWARD_FLY_Y_OFFSET * App.HudScaleY);
            LayoutRewardCoin((_rewardBase + reward).ToString());
            yield return null;
            Vector2 endCenter = _rewardIcon.rectTransform.position;
            int coinCount = Mathf.Clamp(reward, 1, REWARD_FLY_COINS_MAX);
            int coinsPerStep = startMoves > 0 ? reward / startMoves : 0;

            var gainLabel = PlayCoinsGainEnter(reward);

            // number roll
            StartCoroutine(Tween.Run(REWARD_FLY_DURATION, k =>
            {
                int added = Mathf.RoundToInt(reward * k);
                _rewardNumber.text = (_rewardBase + added).ToString();
                if (coinsPerStep > 0)
                    _movesNumber.text = Mathf.Max(startMoves - added / coinsPerStep, 0).ToString();
            }, Ease.OutQuad));

            var sheet = AssetLib.Texture("Art/Sprites/Common/coin_spin_sheet");
            for (int i = 0; i < coinCount; i++)
            {
                StartCoroutine(SingleCoinCo(sheet, startCenter, endCenter, i, coinCount, i * REWARD_FLY_STAGGER));
            }
            float total = Mathf.Max(REWARD_FLY_DURATION, (coinCount - 1) * REWARD_FLY_STAGGER + REWARD_FLY_COIN_DUR);
            yield return new WaitForSeconds(total + 0.05f);
            _rewardNumber.text = (_rewardBase + reward).ToString();
            if (coinsPerStep > 0) _movesNumber.text = "0";
            DismissCoinsGainLabel();
            // coins hold + fade handled per coin
            yield return new WaitForSeconds(REWARD_FLY_HOLD_AFTER_LAND + REWARD_FLY_FADE_OUT_SEC);
        }

        IEnumerator SingleCoinCo(Texture2D sheet, Vector2 from, Vector2 to, int idx, int total, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            var go = new GameObject("Coin");
            go.transform.SetParent(Root, false);
            var img = go.AddComponent<RawImage>();
            img.texture = sheet;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            float frameW = sheet != null ? sheet.width / 3f : 136f;
            rt.sizeDelta = new Vector2(REWARD_FLY_COIN_SIZE, REWARD_FLY_COIN_SIZE);
            rt.position = from;
            float coinEndScale = REWARD_ICON_SIZE / REWARD_FLY_COIN_SIZE;
            StartCoroutine(CoinTrailCo(rt));

            Vector2 mid = (from + to) * 0.5f
                + new Vector2(((total <= 1 ? 0.5f : (float)idx / (total - 1)) - 0.5f) * 200f * App.HudScaleX,
                              -800f * App.HudScaleY);

            float t = 0;
            while (t < REWARD_FLY_COIN_DUR)
            {
                t += Time.deltaTime;
                float raw = Mathf.Clamp01(t / REWARD_FLY_COIN_DUR);
                float k = Tween.Evaluate(Ease.InOutQuad, raw);
                Vector2 p = Vector2.Lerp(Vector2.Lerp(from, mid, k), Vector2.Lerp(mid, to, k), k);
                rt.position = p;
                float sm = k <= 0.5f ? Mathf.Lerp(1f, 1.8f, k * 2f) : Mathf.Lerp(1.8f, coinEndScale, (k - 0.5f) * 2f);
                rt.localScale = Vector3.one * sm;
                int frame = Mathf.FloorToInt(k * 12f) % 12;
                img.uvRect = new Rect((frame % 3) / 3f, 1f - (frame / 3 + 1) / 4f, 1f / 3f, 1f / 4f);
                yield return null;
            }
            SoundManager.I.Play("coin");
            Fx.Vibrate(0);
            PunchRewardIcon();
            yield return new WaitForSeconds(REWARD_FLY_HOLD_AFTER_LAND);
            float ft = 0;
            while (ft < REWARD_FLY_FADE_OUT_SEC)
            {
                ft += Time.deltaTime;
                if (img != null) img.color = new Color(1, 1, 1, 1f - Mathf.Clamp01(ft / REWARD_FLY_FADE_OUT_SEC));
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        IEnumerator CoinTrailCo(RectTransform coin)
        {
            var sprite = AssetLib.Sprite("Art/Sprites/Bubble/ambient_bubble");
            var dots = new System.Collections.Generic.List<CoinTrailDot>(COIN_TRAIL_AMOUNT);
            float elapsed = 0f;
            int spawned = 0;

            while (elapsed < REWARD_FLY_COIN_DUR + COIN_TRAIL_LIFETIME)
            {
                elapsed += Time.deltaTime;
                if (coin != null && spawned < COIN_TRAIL_AMOUNT)
                {
                    float flightTime = Mathf.Min(elapsed, REWARD_FLY_COIN_DUR);
                    int target = Mathf.Min(
                        COIN_TRAIL_AMOUNT,
                        Mathf.FloorToInt(flightTime / REWARD_FLY_COIN_DUR * COIN_TRAIL_AMOUNT) + 1);
                    while (spawned < target)
                    {
                        dots.Add(SpawnCoinTrailDot(coin, sprite));
                        spawned++;
                    }
                }

                for (int i = dots.Count - 1; i >= 0; i--)
                {
                    var dot = dots[i];
                    dot.Age += Time.deltaTime;
                    if (dot.Age >= COIN_TRAIL_LIFETIME || dot.Image == null)
                    {
                        if (dot.Image != null) Destroy(dot.Image.gameObject);
                        dots.RemoveAt(i);
                        continue;
                    }

                    dot.Rect.position += (Vector3)(dot.Velocity * Time.deltaTime);
                    dot.Velocity *= Mathf.Exp(-dot.Drag * Time.deltaTime);
                    Color color = COIN_TRAIL_COLOR;
                    color.a *= 1f - dot.Age / COIN_TRAIL_LIFETIME;
                    dot.Image.color = color;
                }
                yield return null;
            }

            foreach (var dot in dots)
                if (dot.Image != null)
                    Destroy(dot.Image.gameObject);
        }

        CoinTrailDot SpawnCoinTrailDot(RectTransform coin, Sprite sprite)
        {
            var go = new GameObject("CoinTrailBubble");
            go.transform.SetParent(Root, false);
            go.transform.SetSiblingIndex(Mathf.Max(0, coin.GetSiblingIndex()));
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = COIN_TRAIL_COLOR;
            float scale = Random.Range(0.15f, 0.4f);
            image.rectTransform.sizeDelta = Vector2.one * 128f * scale;
            image.rectTransform.position = coin.position +
                (Vector3)(Random.insideUnitCircle * 6f * App.HudScale);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(10f, 40f) * App.HudScale;
            return new CoinTrailDot
            {
                Rect = image.rectTransform,
                Image = image,
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                Drag = Random.Range(1f, 3f),
                Age = 0f
            };
        }

        TMP_Text PlayCoinsGainEnter(int amount)
        {
            if (amount <= 0) return null;
            _coinsGainLabel.gameObject.SetActive(true);
            _coinsGainLabel.text = "+" + amount;
            var numPos = (Vector2)_rewardNumber.rectTransform.position;
            var rt = _coinsGainLabel.rectTransform;
            // top position: right of number, above it
            Vector2 top = numPos + new Vector2(_rewardNumber.rectTransform.sizeDelta.x * 0.5f * App.HudScaleX,
                                               (_rewardNumber.rectTransform.sizeDelta.y * 0.5f + 20f) * App.HudScaleY);
            rt.position = top + new Vector2(0, -COINS_GAIN_RISE_PX * App.HudScaleY);
            _coinsGainLabel.canvasRenderer.SetAlpha(0f);
            StartCoroutine(CoinsGainInCo(rt, top));
            return _coinsGainLabel;
        }

        IEnumerator CoinsGainInCo(RectTransform rt, Vector2 top)
        {
            yield return new WaitForSeconds(COINS_GAIN_ENTER_DELAY);
            _coinsGainLabel.CrossFadeAlpha(1f, COINS_GAIN_IN_SEC, false);
            Vector2 from = rt.position;
            yield return Tween.Run(COINS_GAIN_IN_SEC, k => rt.position = Vector2.Lerp(from, top, k), Ease.OutQuad);
        }

        public void DismissCoinsGainLabel()
        {
            if (!_coinsGainLabel.gameObject.activeSelf) return;
            StartCoroutine(CoinsGainOutCo());
        }

        IEnumerator CoinsGainOutCo()
        {
            var rt = _coinsGainLabel.rectTransform;
            Vector2 from = rt.position;
            Vector2 to = from + new Vector2(0, COINS_GAIN_RISE_PX * 0.6f * App.HudScaleY);
            _coinsGainLabel.CrossFadeAlpha(0f, COINS_GAIN_OUT_SEC, false);
            yield return Tween.Run(COINS_GAIN_OUT_SEC, k => rt.position = Vector2.Lerp(from, to, k), Ease.InQuad);
            _coinsGainLabel.gameObject.SetActive(false);
        }

    }
}
