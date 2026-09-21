using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Port of hard_level_banner.tscn and bubble_hard_banner_plugin.gd.
    /// The fixed artwork and hierarchy live in a prefab; only localized copy,
    /// percentage and the entry/exit timeline are runtime state.
    /// </summary>
    public sealed class HardLevelBanner : MonoBehaviour
    {
        const float InDuration = 0.7f;
        const float HoldDuration = 2.5f;
        const float OutDuration = 0.28f;
        const float SlidePixels = 220f;
        const float BannerOffsetY = 83f;

        const int TitleFontSizeMax = 75;
        const int TitleFontSizeMin = 44;
        const float TitleMaxWidth = 540f;
        const float TitleRowY = 148f;
        const float TitleRowHeight = 100f;
        const float TitleGap = 20f;
        const float FireWidth = 99f;
        const float FireHeight = 115f;

        const int SubtitleFontSizeMax = 53;
        const int SubtitleFontSizeMin = 34;
        const float SubtitleMaxWidth = 792f;
        const float SubtitleMaxHeight = 210f;
        const float SubtitleX = 18f;
        const float SubtitleY = -59f;
        const float SubtitlePercentRatio = 68f / 53f;

        const int BubbleCount = 6;
        const float BubbleBandInner = 225f;
        const float BubbleBandOuter = 375f;
        const float BubbleTopY = -200f;
        const float BubbleBottomY = 255f;
        const float BubbleSpeedMin = 24f;
        const float BubbleSpeedMax = 58f;
        const float BubbleSizeMin = 0.11f;
        const float BubbleSizeMax = 0.32f;
        const float BubbleSwayAmplitude = 8f;
        const float BubbleSwayFrequency = 0.5f;
        const float BubbleAlpha = 0.9f;
        const float BubbleFadeIn = 60f;
        const float BubbleFadeOut = 45f;
        const float BubbleBurstYMin = -200f;
        const float BubbleBurstYMax = 0f;
        const float BubbleBurstChance = 0.65f;
        const int BubbleSeed = 5123;

        const int FragmentsPerBurst = 5;
        const int FragmentPoolSize = 40;
        const float FragmentSizeMin = 0.05f;
        const float FragmentSizeMax = 0.12f;
        const float FragmentLifeMin = 0.5f;
        const float FragmentLifeMax = 0.95f;
        const float FragmentSpeedMin = 30f;
        const float FragmentSpeedMax = 95f;
        const float FragmentRise = -45f;
        const float FragmentDamping = 1.8f;

        static readonly string[] TipKeys =
        {
            "hard_tips1", "hard_tips2", "hard_tips3", "hard_tips4",
        };

        [SerializeField] RectTransform _root;
        [SerializeField] RectTransform _banner;
        [SerializeField] CanvasGroup _bannerGroup;
        [SerializeField] RectTransform _fireIcon;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _subtitle;
        [SerializeField] Image _bubbleDecoration;

        Coroutine _timeline;
        Vector2 _restPosition;
        RectTransform _effectsRoot;
        Image[] _bubbles;
        float[] _bubbleX;
        float[] _bubbleY;
        float[] _bubbleSpeed;
        float[] _bubblePhase;
        float[] _bubbleBurstY;
        bool[] _bubbleWillBurst;
        Image[] _fragments;
        Vector2[] _fragmentPosition;
        Vector2[] _fragmentVelocity;
        float[] _fragmentLife;
        float[] _fragmentMaxLife;
        System.Random _effectRandom;
        float _bubbleTime;
        bool _effectsRunning;

        public void Build(Transform parent)
        {
            if (HasPrefabReferences())
            {
                InitializePrefabRuntime(parent);
                return;
            }

            _root = UiFactory.FullStretch((RectTransform)parent, "HardLevelBanner");
            _root.gameObject.SetActive(false);

            var rootGroup = _root.gameObject.AddComponent<CanvasGroup>();
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;

            var bannerImage = UiFactory.Img(
                _root, "Banner", "Art/Sprites/PsdSkin20260807/HardLevel/card", 979, 700);
            bannerImage.raycastTarget = false;
            _banner = bannerImage.rectTransform;
            _banner.anchorMin = _banner.anchorMax = new Vector2(0.5f, 0.5f);
            // The authored page is 1080x2400. Its 790..1610 offsets place
            // the banner exactly on the page's vertical centre.
            _banner.anchoredPosition = new Vector2(0f, BannerOffsetY);
            _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;

            _bubbleDecoration = UiFactory.Img(
                _banner, "RisingBubbles", "Art/Sprites/Bubble/hard_level_bubbles", 968, 328);
            _bubbleDecoration.raycastTarget = false;
            _bubbleDecoration.rectTransform.anchoredPosition = new Vector2(0, -8);

            var fire = UiFactory.Img(
                _banner, "FireIcon", "Art/Sprites/PsdSkin20260807/HardLevel/fire_icon", 99, 115);
            fire.raycastTarget = false;
            _fireIcon = fire.rectTransform;

            _title = UiFactory.Label(
                _banner, "Title", Localization.Tr("hard_level"), 75, Color.white,
                AssetLib.UiFont, TextAnchor.MiddleCenter, 300, TitleRowHeight);
            _title.raycastTarget = false;
            TmpTextStyle.ApplyOutlineAndShadow(
                _title,
                new Color(0.478f, 0.055f, 0.271f), 15f,
                new Color(0.478f, 0.055f, 0.271f),
                new Vector2(0f, -5f));
            TmpTextStyle.ApplyDisplayTitleFace(_title);

            _subtitle = UiFactory.Label(
                _banner, "Subtitle", "", 53, Color.white,
                AssetLib.UiFont, TextAnchor.UpperCenter, SubtitleMaxWidth, SubtitleMaxHeight);
            _subtitle.raycastTarget = false;
            _subtitle.alignment = TextAlignmentOptions.Top;
            _subtitle.richText = true;
            _subtitle.enableWordWrapping = true;
            _subtitle.overflowMode = TextOverflowModes.Overflow;
            _subtitle.rectTransform.anchoredPosition = new Vector2(SubtitleX, SubtitleY);
            TmpTextStyle.ApplyShadow(
                _subtitle, new Color(0, 0, 0, 0.32f),
                new Vector2(0f, -2f));

            InitializePrefabRuntime(parent);
        }

        public void InitializePrefabRuntime(Transform parent = null)
        {
            if (_root == null) _root = transform as RectTransform;
            if (_root != null && parent != null && _root != parent && !_root.IsChildOf(parent))
                _root.SetParent(parent, false);
            if (_banner != null)
            {
                _banner.anchoredPosition = new Vector2(
                    _banner.anchoredPosition.x,
                    BannerOffsetY);
                _restPosition = _banner.anchoredPosition;
                if (_fireIcon == null)
                    _fireIcon = _banner.Find("FireIcon") as RectTransform;
            }
            if (_title != null)
            {
                TmpTextStyle.ApplyOutlineAndShadow(
                    _title,
                    new Color(0.478f, 0.055f, 0.271f), 15f,
                    new Color(0.478f, 0.055f, 0.271f),
                    new Vector2(0f, -5f));
                TmpTextStyle.ApplyDisplayTitleFace(_title);
            }
            if (_subtitle != null)
            {
                _subtitle.richText = true;
                _subtitle.enableWordWrapping = true;
                _subtitle.overflowMode = TextOverflowModes.Overflow;
                _subtitle.alignment = TextAlignmentOptions.Top;
                _subtitle.rectTransform.sizeDelta = new Vector2(
                    SubtitleMaxWidth,
                    SubtitleMaxHeight);
                _subtitle.rectTransform.anchoredPosition = new Vector2(
                    SubtitleX,
                    SubtitleY);
                TmpTextStyle.ApplyShadow(
                    _subtitle, new Color(0, 0, 0, 0.32f),
                    new Vector2(0f, -2f));
            }
            BuildEffectPool();
            HideImmediate();
        }

        public bool MaybeShow(LevelData level)
        {
            if (level == null ||
                !string.Equals(GameplayDifficulty.ForLevel(level.level).label, "Hard",
                    System.StringComparison.OrdinalIgnoreCase))
                return false;

            string key = HardBannerFlag(level);
            string legacyKey = LegacyHardBannerFlag(level);
            if (SaveState.GetFlag(key) ||
                (key != legacyKey && SaveState.GetFlag(legacyKey)))
                return false;

            // Do not consume the one-shot flag unless the authored page was
            // actually mounted under an active canvas and its timeline began.
            if (!Show()) return false;
            SaveState.SetFlag(key, true);
            if (key != legacyKey)
                SaveState.SetFlag(legacyKey, true);
            return true;
        }

        public bool Show()
        {
            if (!HasPrefabReferences()) return false;
            if (_timeline != null) StopCoroutine(_timeline);
            ApplyLocalizedCopy();
            _root.gameObject.SetActive(true);
            if (!_root.gameObject.activeInHierarchy)
            {
                _root.gameObject.SetActive(false);
                return false;
            }

            ResetAllBubbles();
            _effectsRunning = _bubbles != null && _bubbles.Length > 0;
            _timeline = StartCoroutine(PlayTimeline());
            return _timeline != null;
        }

        public void HideImmediate()
        {
            if (_timeline != null)
            {
                StopCoroutine(_timeline);
                _timeline = null;
            }
            _effectsRunning = false;
            if (_banner != null) _banner.anchoredPosition = _restPosition;
            if (_bannerGroup != null) _bannerGroup.alpha = 0f;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>Handles the platform/keyboard back action while this transient page is visible.</summary>
        public bool HandleBackRequest()
        {
            if (_root == null || !_root.gameObject.activeInHierarchy) return false;
            HideImmediate();
            return true;
        }

        void Update()
        {
            if (_root != null && _root.gameObject.activeInHierarchy &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                HandleBackRequest();
                return;
            }

            if (_effectsRunning)
                UpdateBubbles(Time.unscaledDeltaTime);
        }

        IEnumerator PlayTimeline()
        {
            Vector2 start = _restPosition + Vector2.up * SlidePixels;
            _banner.anchoredPosition = start;
            _bannerGroup.alpha = 0f;

            float t = 0f;
            while (t < InDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / InDuration);
                float eased = Tween.Evaluate(Ease.OutBack, k);
                _banner.anchoredPosition = Vector2.LerpUnclamped(start, _restPosition, eased);
                _bannerGroup.alpha = k;
                yield return null;
            }

            yield return new WaitForSecondsRealtime(HoldDuration);

            t = 0f;
            while (t < OutDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / OutDuration);
                float eased = k * k;
                _banner.anchoredPosition =
                    Vector2.LerpUnclamped(_restPosition, start, eased);
                _bannerGroup.alpha = 1f - k;
                yield return null;
            }

            _timeline = null;
            HideImmediate();
        }

        void ApplyLocalizedCopy()
        {
            _title.text = Localization.Tr("hard_level");
            FitTitleFont();

            int index = Random.Range(0, TipKeys.Length);
            bool passTip = index == 0 || index == 3;
            int percent = passTip ? Random.Range(40, 71) : Random.Range(30, 61);
            string template = Localization.Tr(TipKeys[index]);
            string plain = template.Contains("%d")
                ? template.Replace("%d", percent + "%")
                : template;
            int percentFontSize = FitSubtitleFont(plain);
            string styled =
                $"<color=#FFEF73><size={percentFontSize}>{percent}%</size></color>";
            _subtitle.text = template.Contains("%d")
                ? template.Replace("%d", styled)
                : template;
        }

        void FitTitleFont()
        {
            if (_title == null || _title.font == null) return;
            _title.enableAutoSizing = false;
            int size = TitleFontSizeMax;
            while (size > TitleFontSizeMin &&
                   PreferredWidth(_title, _title.text, size) > TitleMaxWidth)
                size -= 2;
            _title.fontSize = size;
            LayoutTitleRow();
            TmpTextStyle.ApplyOutlineAndShadow(
                _title,
                new Color(0.478f, 0.055f, 0.271f), 15f,
                new Color(0.478f, 0.055f, 0.271f),
                new Vector2(0f, -5f));
            TmpTextStyle.ApplyDisplayTitleFace(_title);
        }

        void LayoutTitleRow()
        {
            if (_fireIcon == null || _title == null) return;

            RectTransform titleRect = _title.rectTransform;
            float titleWidth = Mathf.Min(
                TitleMaxWidth,
                Mathf.Ceil(PreferredWidth(
                    _title, _title.text, Mathf.RoundToInt(_title.fontSize))));
            titleWidth = Mathf.Max(1f, titleWidth);

            // Godot's centred HBox is only as wide as its two children:
            // 68px fire + 8px separation + the title's preferred width.
            float rowWidth = FireWidth + TitleGap + titleWidth;
            float rowLeft = -rowWidth * 0.5f;

            _fireIcon.anchorMin = _fireIcon.anchorMax = new Vector2(0.5f, 0.5f);
            _fireIcon.sizeDelta = new Vector2(FireWidth, FireHeight);
            _fireIcon.anchoredPosition = new Vector2(
                rowLeft + FireWidth * 0.5f,
                TitleRowY);

            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(titleWidth, TitleRowHeight);
            titleRect.anchoredPosition = new Vector2(
                rowLeft + FireWidth + TitleGap + titleWidth * 0.5f,
                TitleRowY);
        }

        int FitSubtitleFont(string plainText)
        {
            if (_subtitle == null || _subtitle.font == null)
                return Mathf.RoundToInt(SubtitleFontSizeMax * SubtitlePercentRatio);

            _subtitle.enableAutoSizing = false;
            int size = SubtitleFontSizeMax;
            while (size > SubtitleFontSizeMin)
            {
                float wrappedHeight = PreferredWrappedHeight(
                    _subtitle, plainText, size, SubtitleMaxWidth);
                int percentSize = Mathf.RoundToInt(size * SubtitlePercentRatio);
                if (wrappedHeight + percentSize - size <= SubtitleMaxHeight)
                    break;
                size -= 2;
            }
            _subtitle.fontSize = size;
            TmpTextStyle.ApplyShadow(
                _subtitle, new Color(0, 0, 0, 0.32f),
                new Vector2(0f, -2f));
            return Mathf.RoundToInt(size * SubtitlePercentRatio);
        }

        static float PreferredWidth(TMP_Text text, string value, int fontSize)
        {
            float previousSize = text.fontSize;
            bool previousWrapping = text.enableWordWrapping;
            bool previousRichText = text.richText;
            text.fontSize = fontSize;
            text.enableWordWrapping = false;
            text.richText = false;
            float width = text.GetPreferredValues(value, 10000f, 10000f).x;
            text.fontSize = previousSize;
            text.enableWordWrapping = previousWrapping;
            text.richText = previousRichText;
            return width;
        }

        static float PreferredWrappedHeight(
            TMP_Text text, string value, int fontSize, float width)
        {
            float previousSize = text.fontSize;
            bool previousWrapping = text.enableWordWrapping;
            bool previousRichText = text.richText;
            text.fontSize = fontSize;
            text.enableWordWrapping = true;
            text.richText = false;
            float height = text.GetPreferredValues(value, width, 10000f).y;
            text.fontSize = previousSize;
            text.enableWordWrapping = previousWrapping;
            text.richText = previousRichText;
            return height;
        }

        void BuildEffectPool()
        {
            if (_bubbles != null || _bubbleDecoration == null) return;

            Sprite bubbleSprite = AssetLib.Sprite("Art/Sprites/Home/deco_bubble");
            if (bubbleSprite == null)
            {
                // Keep the authored static decoration as a graceful fallback
                // when an imported project does not contain the source bubble.
                _bubbleDecoration.enabled = true;
                return;
            }

            _bubbleDecoration.enabled = false;
            _effectsRoot = _bubbleDecoration.rectTransform;
            _effectsRoot.anchoredPosition = Vector2.zero;

            _effectRandom = new System.Random(BubbleSeed);
            _bubbles = new Image[BubbleCount];
            _bubbleX = new float[BubbleCount];
            _bubbleY = new float[BubbleCount];
            _bubbleSpeed = new float[BubbleCount];
            _bubblePhase = new float[BubbleCount];
            _bubbleBurstY = new float[BubbleCount];
            _bubbleWillBurst = new bool[BubbleCount];
            for (int i = 0; i < BubbleCount; i++)
                _bubbles[i] = CreateEffectImage("Bubble" + i, bubbleSprite, false);

            _fragments = new Image[FragmentPoolSize];
            _fragmentPosition = new Vector2[FragmentPoolSize];
            _fragmentVelocity = new Vector2[FragmentPoolSize];
            _fragmentLife = new float[FragmentPoolSize];
            _fragmentMaxLife = new float[FragmentPoolSize];
            for (int i = 0; i < FragmentPoolSize; i++)
                _fragments[i] = CreateEffectImage("Fragment" + i, bubbleSprite, true);
        }

        Image CreateEffectImage(string objectName, Sprite sprite, bool inactive)
        {
            var go = new GameObject(
                objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = _effectsRoot.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(_effectsRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sprite.rect.size;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, 0f);
            if (inactive) go.SetActive(false);
            return image;
        }

        void ResetAllBubbles()
        {
            BuildEffectPool();
            if (_bubbles == null) return;

            _bubbleTime = 0f;
            for (int i = 0; i < _bubbles.Length; i++)
                ResetBubble(i, true);
            for (int i = 0; i < _fragments.Length; i++)
            {
                _fragmentLife[i] = 0f;
                _fragments[i].gameObject.SetActive(false);
            }
        }

        void ResetBubble(int index, bool initial)
        {
            bool onLeft = index % 2 == 0;
            float bandMin = onLeft ? -BubbleBandOuter : BubbleBandInner;
            _bubbleX[index] = bandMin +
                NextFloat(0f, BubbleBandOuter - BubbleBandInner);
            _bubbleSpeed[index] = NextFloat(BubbleSpeedMin, BubbleSpeedMax);
            _bubblePhase[index] = NextFloat(0f, Mathf.PI * 2f);
            _bubbleWillBurst[index] = NextFloat(0f, 1f) < BubbleBurstChance;

            float scale = NextFloat(BubbleSizeMin, BubbleSizeMax);
            _bubbles[index].rectTransform.localScale =
                new Vector3(scale, scale, 1f);
            _bubbles[index].gameObject.SetActive(true);

            if (initial)
            {
                int perSide = BubbleCount / 2;
                float progress = (index / 2) / (float)Mathf.Max(perSide - 1, 1);
                float initialY = Mathf.Lerp(
                    BubbleBottomY - 30f, BubbleBurstYMin + 30f, progress);
                initialY = Mathf.Max(initialY, BubbleBurstYMin + 15f);
                _bubbleBurstY[index] = Mathf.Clamp(
                    NextFloat(BubbleBurstYMin, BubbleBurstYMax),
                    BubbleBurstYMin,
                    Mathf.Max(initialY - 20f, BubbleBurstYMin));
                _bubbleY[index] = initialY;
            }
            else
            {
                _bubbleBurstY[index] =
                    NextFloat(BubbleBurstYMin, BubbleBurstYMax);
                _bubbleY[index] = BubbleBottomY;
            }

            SetBubbleVisual(index, 0f);
        }

        void UpdateBubbles(float delta)
        {
            if (_bubbles == null || delta <= 0f) return;

            _bubbleTime += delta;
            for (int i = 0; i < _bubbles.Length; i++)
            {
                float y = _bubbleY[i] - _bubbleSpeed[i] * delta;
                float x = _bubbleX[i] + BubbleSwayAmplitude * Mathf.Sin(
                    _bubbleTime * BubbleSwayFrequency * Mathf.PI * 2f +
                    _bubblePhase[i]);
                if (y <= _bubbleBurstY[i] || y < BubbleTopY)
                {
                    if (y <= _bubbleBurstY[i] && _bubbleWillBurst[i])
                        SpawnFragments(new Vector2(x, y));
                    ResetBubble(i, false);
                    continue;
                }

                _bubbleY[i] = y;
                float fadeIn = Mathf.Clamp01((BubbleBottomY - y) / BubbleFadeIn);
                float fadeOut =
                    Mathf.Clamp01((y - _bubbleBurstY[i]) / BubbleFadeOut);
                SetBubbleVisual(i, BubbleAlpha * Mathf.Min(fadeIn, fadeOut), x);
            }

            UpdateFragments(delta);
        }

        void SetBubbleVisual(int index, float alpha, float? xOverride = null)
        {
            Image image = _bubbles[index];
            image.rectTransform.anchoredPosition =
                new Vector2(xOverride ?? _bubbleX[index], -_bubbleY[index]);
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        void SpawnFragments(Vector2 position)
        {
            int spawned = 0;
            for (int i = 0; i < _fragments.Length && spawned < FragmentsPerBurst; i++)
            {
                if (_fragmentLife[i] > 0f) continue;

                float angle = NextFloat(0f, Mathf.PI * 2f);
                float speed = NextFloat(FragmentSpeedMin, FragmentSpeedMax);
                _fragmentPosition[i] = position;
                _fragmentVelocity[i] =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                float life = NextFloat(FragmentLifeMin, FragmentLifeMax);
                _fragmentMaxLife[i] = life;
                _fragmentLife[i] = life;

                float scale = NextFloat(FragmentSizeMin, FragmentSizeMax);
                Image fragment = _fragments[i];
                fragment.rectTransform.localScale =
                    new Vector3(scale, scale, 1f);
                fragment.rectTransform.anchoredPosition =
                    new Vector2(position.x, -position.y);
                fragment.color = new Color(1f, 1f, 1f, BubbleAlpha);
                fragment.gameObject.SetActive(true);
                spawned++;
            }
        }

        void UpdateFragments(float delta)
        {
            for (int i = 0; i < _fragments.Length; i++)
            {
                if (_fragmentLife[i] <= 0f) continue;

                _fragmentLife[i] -= delta;
                if (_fragmentLife[i] <= 0f)
                {
                    _fragments[i].gameObject.SetActive(false);
                    continue;
                }

                _fragmentVelocity[i] *=
                    1f - Mathf.Clamp01(FragmentDamping * delta);
                _fragmentVelocity[i].y += FragmentRise * delta;
                _fragmentPosition[i] += _fragmentVelocity[i] * delta;

                Image fragment = _fragments[i];
                fragment.rectTransform.anchoredPosition = new Vector2(
                    _fragmentPosition[i].x, -_fragmentPosition[i].y);
                Color color = fragment.color;
                color.a = BubbleAlpha *
                    (_fragmentLife[i] / _fragmentMaxLife[i]);
                fragment.color = color;
            }
        }

        float NextFloat(float min, float max)
        {
            if (_effectRandom == null)
                _effectRandom = new System.Random(BubbleSeed);
            return min + (float)_effectRandom.NextDouble() * (max - min);
        }

        static string HardBannerFlag(LevelData level)
        {
            string identity = !string.IsNullOrEmpty(level.level_unique_id)
                ? level.level_unique_id
                : $"{level.chapter}:{level.level}";
            return "hard_banner_shown_" + identity;
        }

        static string LegacyHardBannerFlag(LevelData level)
        {
            return $"hard_banner_shown_{level.chapter}:{level.level}";
        }

        bool HasPrefabReferences()
        {
            return _root != null && _banner != null && _bannerGroup != null
                && _fireIcon != null && _title != null && _subtitle != null;
        }

        /// <summary>
        /// Clears both current and legacy one-shot keys so GM and QA tools use
        /// the exact same identity rules as runtime display.
        /// </summary>
        public static void ResetShownFlag(LevelData level)
        {
            if (level == null) return;
            string key = HardBannerFlag(level);
            string legacyKey = LegacyHardBannerFlag(level);
            SaveState.SetFlag(key, false);
            if (key != legacyKey)
                SaveState.SetFlag(legacyKey, false);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/BubblePics/QA/困难关提示/重置当前关卡")]
        static void ResetCurrentHardBannerFlag()
        {
            if (!LevelRepo.TryGet(SaveState.CurrentLevel, out LevelData level))
            {
                Debug.LogWarning(
                    $"无法重置困难关提示：关卡 {SaveState.CurrentLevel} 不存在。");
                return;
            }

            ResetShownFlag(level);
            Debug.Log($"已重置关卡 {level.level} 的困难关提示记录。");
        }

        [UnityEditor.MenuItem("Tools/BubblePics/QA/困难关提示/重置全部困难关")]
        static void ResetAllHardBannerFlags()
        {
            int count = 0;
            for (int i = 1; i <= LevelRepo.Count; i++)
            {
                if (!LevelRepo.TryGet(i, out LevelData level) ||
                    !string.Equals(GameplayDifficulty.ForLevel(level.level).label, "Hard",
                        System.StringComparison.OrdinalIgnoreCase))
                    continue;

                ResetShownFlag(level);
                count++;
            }
            Debug.Log($"已重置 {count} 个困难关的提示记录。");
        }
#endif
    }
}
