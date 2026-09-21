using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of moves_intro_panel — first step-limited round card intro.</summary>
    public sealed class MovesIntroPanel : MonoBehaviour
    {
        const float HOLD_SEC = 3.0f;
        const float SLIDE_IN_SEC = 0.5f;
        const float SLIDE_OUT_SEC = 0.3f;
        const float SLIDE_PX = 260f;
        const float FLY_SEC = 0.45f;
        const float FADE_SEC = 0.2f;
        const float CARD_FADE_SEC = 0.3f;
        const int NUMBER_FONT_SIZE = 144;
        const int HUD_NUMBER_FONT_SIZE = 112;

        const int BUB_COUNT = 8;
        const float BUB_BAND_INNER = 350f;
        const float BUB_BAND_OUTER = 465f;
        const float BUB_TOP_Y = -220f;
        const float BUB_BOTTOM_Y = 215f;
        const float BUB_SPEED_MIN = 24f;
        const float BUB_SPEED_MAX = 58f;
        const float BUB_SIZE_MIN = 0.1f;
        const float BUB_SIZE_MAX = 0.26f;
        const float BUB_SWAY_AMP = 8f;
        const float BUB_SWAY_FREQ = 0.5f;
        const float BUB_ALPHA = 0.85f;
        const float BUB_FADE_IN = 60f;
        const float BUB_FADE_OUT = 45f;
        const float BUB_BURST_Y_MIN = -215f;
        const float BUB_BURST_Y_MAX = -30f;
        const float BUB_BURST_CHANCE = 0.65f;
        const int BUB_SEED = 7731;

        const int FRAG_PER_BURST = 5;
        const int FRAG_POOL = 40;
        const float FRAG_SIZE_MIN = 0.045f;
        const float FRAG_SIZE_MAX = 0.1f;
        const float FRAG_LIFE_MIN = 0.5f;
        const float FRAG_LIFE_MAX = 0.95f;
        const float FRAG_SPEED_MIN = 30f;
        const float FRAG_SPEED_MAX = 95f;
        const float FRAG_RISE = -45f;
        const float FRAG_DAMP = 1.8f;

        BubblePage _page;
        [SerializeField] RectTransform _root;
        [SerializeField] RectTransform _card;
        [SerializeField] CanvasGroup _cardCg;
        [SerializeField] TMP_Text _movesNumber;
        [SerializeField] TMP_Text _movesNumberBack;
        [SerializeField] RectTransform _risingBubbles;

        TMP_Text _title;
        TMP_Text _movesLabel;
        bool _localeSubscribed;

        Coroutine _showCoroutine;
        Coroutine _cardOutCoroutine;
        bool _playing;
        bool _cardRestCaptured;
        Vector2 _cardRestPosition;

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
        System.Random _bubbleRandom;
        float _bubbleTime;

        public BubblePage Page
        {
            get => _page;
            set => _page = value;
        }

        public void Build(Transform parent)
        {
            if (HasPrefabReferences())
            {
                InitializePrefabRuntime(Page, parent);
                if (_root != null) _root.gameObject.SetActive(false);
                return;
            }

            _root = UiFactory.FullStretch((RectTransform)parent, "MovesIntroPanel");

            _card = UiFactory.Node(_root, "Card");
            _card.sizeDelta = new Vector2(1016, 554);
            var bg = UiFactory.Img(_card, "Bg", "Art/Sprites/Bubble/moves_intro_card", 1016, 554);
            bg.raycastTarget = false;

            // Authored equivalent of moves_intro_panel.tscn/Card/RisingBubbles.
            // Bubble and fragment images are pooled below this fixed container.
            _risingBubbles = UiFactory.Node(_card, "RisingBubbles");
            _risingBubbles.anchorMin = _risingBubbles.anchorMax = new Vector2(0.5f, 0.5f);
            _risingBubbles.anchoredPosition = Vector2.zero;
            _risingBubbles.sizeDelta = Vector2.zero;

            _title = UiFactory.Label(_card, "Title", Localization.Tr("limit_step_hint"), 52, Color.white, AssetLib.UiFont, TextAnchor.MiddleCenter, 760, 150);
            _title.rectTransform.anchoredPosition = new Vector2(0, 118); // godot center y=-118 design
            TmpTextStyle.ApplyOutline(_title, new Color(0, 0, 0, 0.3f), 4f);

            _movesNumberBack = UiFactory.Label(_card, "NumBack", "37", NUMBER_FONT_SIZE, new Color32(0x00, 0x31, 0x5C, 0xFF), AssetLib.NumFont, TextAnchor.MiddleCenter, 500, 200);
            _movesNumberBack.rectTransform.anchoredPosition = new Vector2(0, -63);
            TmpTextStyle.ApplyOutline(
                _movesNumberBack, new Color32(0x00, 0x31, 0x5C, 0xFF), 12f);
            _movesNumber = UiFactory.Label(_card, "Num", "37", NUMBER_FONT_SIZE, Color.white, AssetLib.NumFont, TextAnchor.MiddleCenter, 500, 200);
            _movesNumber.rectTransform.anchoredPosition = new Vector2(0, -51);

            _movesLabel = UiFactory.Label(_card, "Moves", Localization.Tr("str_moves"), 52, Color.white, AssetLib.UiFont, TextAnchor.MiddleCenter, 400, 70);
            _movesLabel.rectTransform.anchoredPosition = new Vector2(0, -153);
            TmpTextStyle.ApplyOutline(
                _movesLabel, new Color(0, 0, 0, 0.3f), 4f);

            _cardCg = _card.gameObject.AddComponent<CanvasGroup>();
            _root.gameObject.SetActive(false);
        }

        bool HasPrefabReferences()
        {
            return _root != null || _card != null || _movesNumber != null || _movesNumberBack != null;
        }

        /// <summary>Injects the owning page and binds fixed references from MovesIntroPanel.prefab.</summary>
        public void InitializePrefabRuntime(BubblePage page, Transform parent = null)
        {
            Page = page;
            if (_root == null)
                _root = transform as RectTransform;
            if (parent != null && _root != null && _root != parent && !_root.IsChildOf(parent))
                _root.SetParent(parent, false);
            BindPrefabRuntime();
            BuildEffectPool();
        }

        public void BindPrefabRuntime()
        {
            if (_root == null)
                _root = transform as RectTransform;
            if (_root == null) return;
            _card = _card != null ? _card : FindNamed<RectTransform>(_root, "Card");
            _movesNumber = _movesNumber != null ? _movesNumber : FindNamed<TMP_Text>(_root, "Num");
            _movesNumberBack = _movesNumberBack != null ? _movesNumberBack : FindNamed<TMP_Text>(_root, "NumBack");
            _risingBubbles = _risingBubbles != null
                ? _risingBubbles
                : FindNamed<RectTransform>(_root, "RisingBubbles");
            if (_cardCg == null && _card != null)
                _cardCg = _card.GetComponent<CanvasGroup>();
            if (_cardCg == null && _card != null)
                _cardCg = _card.gameObject.AddComponent<CanvasGroup>();

            _title = _title != null ? _title : FindNamed<TMP_Text>(_card, "Title");
            if (_title != null)
                TmpTextStyle.ApplyOutline(_title, new Color(0, 0, 0, 0.3f), 4f);
            if (_movesNumberBack != null)
            {
                TmpTextStyle.ApplyOutline(
                    _movesNumberBack,
                    new Color32(0x00, 0x31, 0x5C, 0xFF), 12f);
            }
            _movesLabel = _movesLabel != null
                ? _movesLabel
                : FindNamed<TMP_Text>(_card, "Moves");
            if (_movesLabel != null)
            {
                TmpTextStyle.ApplyOutline(
                    _movesLabel, new Color(0, 0, 0, 0.3f), 4f);
            }

            RefreshLocalizedText();

            if (_card != null && !_cardRestCaptured)
            {
                _cardRestPosition = _card.anchoredPosition;
                _cardRestCaptured = true;
            }
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

        public void MaybeShow()
        {
            if (_root == null) BindPrefabRuntime();
            if (Page == null || _root == null || _card == null) return;
            RefreshLocalizedText();
            if (Page.IsMovesUnlimited()) return;
            if (AppConfig.PointsMode) return;
            if (SaveState.GetFlag("moves_intro_shown")) return;
            SaveState.SetFlag("moves_intro_shown", true);
            Page.TopBar.HideMovesNumberForIntro();
            _playing = true;
            _showCoroutine = StartCoroutine(ShowCo());
        }

        public void Cancel()
        {
            if (!_playing && (_root == null || !_root.gameObject.activeSelf))
                return;

            if (_showCoroutine != null)
                StopCoroutine(_showCoroutine);
            if (_cardOutCoroutine != null)
                StopCoroutine(_cardOutCoroutine);
            _showCoroutine = null;
            _cardOutCoroutine = null;
            _playing = false;

            if (_root != null)
            {
                foreach (var text in _root.GetComponentsInChildren<TMP_Text>(true))
                    if (text != null && text.gameObject.name == "FlyNum")
                        Destroy(text.gameObject);
                _root.gameObject.SetActive(false);
            }

            SetCardNumbersVisible(true);
            if (_card != null)
                _card.anchoredPosition = _cardRestCaptured
                    ? _cardRestPosition
                    : Vector2.zero;
            if (_cardCg != null) _cardCg.alpha = 1f;
            if (Page != null && Page.TopBar != null)
                Page.TopBar.RevealMovesNumber();
        }

        IEnumerator ShowCo()
        {
            _root.gameObject.SetActive(true);
            _movesNumber.text = Page.StepsLeft.ToString();
            _movesNumberBack.text = _movesNumber.text;
            SetCardNumbersVisible(true);
            ResetAllBubbles();

            // Godot waits one process frame after becoming visible so the card
            // and its labels have final layout rectangles before tweening.
            yield return null;
            if (!_playing) yield break;

            if (!_cardRestCaptured)
            {
                _cardRestPosition = _card.anchoredPosition;
                _cardRestCaptured = true;
            }

            Vector2 rest = _cardRestPosition;
            Vector2 off = rest + new Vector2(0, SLIDE_PX);
            _card.anchoredPosition = off;
            _cardCg.alpha = 0;
            float t = 0;
            while (t < SLIDE_IN_SEC)
            {
                t += Time.deltaTime;
                float k = Tween.Evaluate(Ease.OutBack, Mathf.Clamp01(t / SLIDE_IN_SEC));
                _card.anchoredPosition = Vector2.LerpUnclamped(off, rest, k);
                _cardCg.alpha = Mathf.Clamp01(t / SLIDE_IN_SEC);
                yield return null;
            }
            yield return new WaitForSeconds(HOLD_SEC);
            if (!_playing) yield break;

            // fly number to HUD + card slide out
            var flyLabel = UiFactory.Label(_root, "FlyNum", _movesNumber.text, NUMBER_FONT_SIZE, Color.white, AssetLib.NumFont, TextAnchor.MiddleCenter, 500, 200);
            flyLabel.color = _movesNumber.color;
            flyLabel.rectTransform.position = _movesNumber.rectTransform.position;
            flyLabel.rectTransform.localScale = Vector3.one;

            // The moving label replaces both authored number layers. Leaving
            // either one visible produces the duplicate number seen previously.
            SetCardNumbersVisible(false);

            Vector3 flyTo = Page.TopBar.MovesNumberScreenCenter();
            TMP_Text hudNumber = FindHudMovesNumber(flyTo);
            float hudFontSize = hudNumber != null && hudNumber.fontSize > 0
                ? hudNumber.fontSize
                : HUD_NUMBER_FONT_SIZE;
            Color hudColor = hudNumber != null
                ? hudNumber.color
                : new Color(0f, 0f, 0f, 0f);
            float hudFontScale = hudFontSize / (float)NUMBER_FONT_SIZE;

            _cardOutCoroutine = StartCoroutine(CardOutCo());
            t = 0;
            Vector3 flyFrom = flyLabel.rectTransform.position;
            Color flyStartColor = flyLabel.color;
            while (t < FLY_SEC)
            {
                t += Time.deltaTime;
                float k = Tween.Evaluate(Ease.InQuad, Mathf.Clamp01(t / FLY_SEC));
                flyLabel.rectTransform.position = Vector3.Lerp(flyFrom, flyTo, k);
                flyLabel.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, hudFontScale, k);
                if (hudColor.a > 0f)
                    flyLabel.color = Color.Lerp(flyStartColor, hudColor, Mathf.Clamp01(t / FLY_SEC));
                yield return null;
            }

            flyLabel.rectTransform.position = flyTo;
            flyLabel.rectTransform.localScale = Vector3.one * hudFontScale;
            if (hudColor.a > 0f) flyLabel.color = hudColor;

            // Matches MovesIntroPanel.number_arrived -> TopGameBar reveal.
            Page.TopBar.RevealMovesNumber();
            float f = 0;
            while (f < FADE_SEC)
            {
                f += Time.deltaTime;
                flyLabel.canvasRenderer.SetAlpha(1f - f / FADE_SEC);
                yield return null;
            }
            Destroy(flyLabel.gameObject);
            _root.gameObject.SetActive(false);
            _showCoroutine = null;
            _cardOutCoroutine = null;
            _playing = false;
        }

        IEnumerator CardOutCo()
        {
            Vector2 from = _card.anchoredPosition;
            Vector2 to = _cardRestPosition + new Vector2(0, SLIDE_PX);
            float t = 0;
            while (t < Mathf.Max(SLIDE_OUT_SEC, CARD_FADE_SEC))
            {
                t += Time.deltaTime;
                float moveK = Tween.Evaluate(
                    Ease.InQuad, Mathf.Clamp01(t / SLIDE_OUT_SEC));
                float fadeK = Mathf.Clamp01(t / CARD_FADE_SEC);
                _card.anchoredPosition = Vector2.Lerp(from, to, moveK);
                _cardCg.alpha = 1f - fadeK;
                yield return null;
            }

            _card.anchoredPosition = to;
            _cardCg.alpha = 0f;
            _cardOutCoroutine = null;
        }

        void SetCardNumbersVisible(bool visible)
        {
            if (_movesNumber != null) _movesNumber.gameObject.SetActive(visible);
            if (_movesNumberBack != null) _movesNumberBack.gameObject.SetActive(visible);
        }

        TMP_Text FindHudMovesNumber(Vector3 targetPosition)
        {
            if (Page == null || Page.TopBar == null) return null;

            TMP_Text best = null;
            float bestDistance = float.MaxValue;
            var texts = Page.TopBar.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text candidate in texts)
            {
                if (candidate == null || candidate.gameObject.name != "Num")
                    continue;

                float distance =
                    (candidate.rectTransform.position - targetPosition).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = candidate;
                bestDistance = distance;
            }
            return best;
        }

        void Update()
        {
            if (_playing && _root != null && _root.gameObject.activeInHierarchy)
                UpdateBubbles(Time.deltaTime);
        }

        void OnEnable()
        {
            SubscribeLocale();
            RefreshLocalizedText();
        }

        void OnDisable()
        {
            UnsubscribeLocale();
        }

        void OnDestroy()
        {
            UnsubscribeLocale();
        }

        void SubscribeLocale()
        {
            if (_localeSubscribed) return;
            Localization.LocaleChanged += RefreshLocalizedText;
            _localeSubscribed = true;
        }

        void UnsubscribeLocale()
        {
            if (!_localeSubscribed) return;
            Localization.LocaleChanged -= RefreshLocalizedText;
            _localeSubscribed = false;
        }

        void RefreshLocalizedText()
        {
            if (_title == null && _card != null)
                _title = FindNamed<TMP_Text>(_card, "Title");
            if (_movesLabel == null && _card != null)
                _movesLabel = FindNamed<TMP_Text>(_card, "Moves");

            if (_title != null)
                _title.text = Localization.Tr("limit_step_hint");
            if (_movesLabel != null)
                _movesLabel.text = Localization.Tr("str_moves");
        }

        void BuildEffectPool()
        {
            if (_bubbles != null || _risingBubbles == null) return;

            Sprite bubbleSprite = AssetLib.Sprite("Art/Sprites/Home/deco_bubble");
            if (bubbleSprite == null) return;

            _bubbleRandom = new System.Random(BUB_SEED);
            _bubbles = new Image[BUB_COUNT];
            _bubbleX = new float[BUB_COUNT];
            _bubbleY = new float[BUB_COUNT];
            _bubbleSpeed = new float[BUB_COUNT];
            _bubblePhase = new float[BUB_COUNT];
            _bubbleBurstY = new float[BUB_COUNT];
            _bubbleWillBurst = new bool[BUB_COUNT];
            for (int i = 0; i < BUB_COUNT; i++)
                _bubbles[i] = CreateEffectImage("Bubble" + i, bubbleSprite, false);

            _fragments = new Image[FRAG_POOL];
            _fragmentPosition = new Vector2[FRAG_POOL];
            _fragmentVelocity = new Vector2[FRAG_POOL];
            _fragmentLife = new float[FRAG_POOL];
            _fragmentMaxLife = new float[FRAG_POOL];
            for (int i = 0; i < FRAG_POOL; i++)
                _fragments[i] =
                    CreateEffectImage("Fragment" + i, bubbleSprite, true);
        }

        Image CreateEffectImage(string objectName, Sprite sprite, bool inactive)
        {
            var go = new GameObject(
                objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = _risingBubbles.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(_risingBubbles, false);
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
            Image bubble = _bubbles[index];
            bool onLeft = index % 2 == 0;
            float bandMin = onLeft ? -BUB_BAND_OUTER : BUB_BAND_INNER;
            _bubbleX[index] =
                bandMin + NextFloat(0f, BUB_BAND_OUTER - BUB_BAND_INNER);
            _bubbleSpeed[index] = NextFloat(BUB_SPEED_MIN, BUB_SPEED_MAX);
            _bubblePhase[index] = NextFloat(0f, Mathf.PI * 2f);
            _bubbleWillBurst[index] =
                NextFloat(0f, 1f) < BUB_BURST_CHANCE;

            float scale = NextFloat(BUB_SIZE_MIN, BUB_SIZE_MAX);
            bubble.rectTransform.localScale = new Vector3(scale, scale, 1f);
            bubble.gameObject.SetActive(true);

            if (initial)
            {
                int perSide = BUB_COUNT / 2;
                float progress =
                    (index / 2) / (float)Mathf.Max(perSide - 1, 1);
                float initialY = Mathf.Lerp(
                    BUB_BOTTOM_Y - 30f, BUB_BURST_Y_MIN + 30f, progress);
                _bubbleBurstY[index] = Mathf.Clamp(
                    NextFloat(BUB_BURST_Y_MIN, BUB_BURST_Y_MAX),
                    BUB_BURST_Y_MIN,
                    Mathf.Max(initialY - 20f, BUB_BURST_Y_MIN));
                _bubbleY[index] = initialY;
            }
            else
            {
                _bubbleBurstY[index] =
                    NextFloat(BUB_BURST_Y_MIN, BUB_BURST_Y_MAX);
                _bubbleY[index] = BUB_BOTTOM_Y;
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
                float x = _bubbleX[i] + BUB_SWAY_AMP * Mathf.Sin(
                    _bubbleTime * BUB_SWAY_FREQ * Mathf.PI * 2f +
                    _bubblePhase[i]);
                if (y <= _bubbleBurstY[i] || y < BUB_TOP_Y)
                {
                    if (y <= _bubbleBurstY[i] && _bubbleWillBurst[i])
                        SpawnFragments(new Vector2(x, y));
                    ResetBubble(i, false);
                    continue;
                }

                _bubbleY[i] = y;
                float fadeIn =
                    Mathf.Clamp01((BUB_BOTTOM_Y - y) / BUB_FADE_IN);
                float fadeOut =
                    Mathf.Clamp01((y - _bubbleBurstY[i]) / BUB_FADE_OUT);
                SetBubbleVisual(i, BUB_ALPHA * Mathf.Min(fadeIn, fadeOut), x);
            }

            UpdateFragments(delta);
        }

        void SetBubbleVisual(int index, float alpha, float? xOverride = null)
        {
            Image bubble = _bubbles[index];
            bubble.rectTransform.anchoredPosition =
                new Vector2(xOverride ?? _bubbleX[index], -_bubbleY[index]);
            Color color = bubble.color;
            color.a = alpha;
            bubble.color = color;
        }

        void SpawnFragments(Vector2 position)
        {
            int spawned = 0;
            for (int i = 0;
                 i < _fragments.Length && spawned < FRAG_PER_BURST;
                 i++)
            {
                if (_fragmentLife[i] > 0f) continue;

                float angle = NextFloat(0f, Mathf.PI * 2f);
                float speed = NextFloat(FRAG_SPEED_MIN, FRAG_SPEED_MAX);
                _fragmentPosition[i] = position;
                _fragmentVelocity[i] =
                    new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                float life = NextFloat(FRAG_LIFE_MIN, FRAG_LIFE_MAX);
                _fragmentMaxLife[i] = life;
                _fragmentLife[i] = life;

                float scale = NextFloat(FRAG_SIZE_MIN, FRAG_SIZE_MAX);
                Image fragment = _fragments[i];
                fragment.rectTransform.localScale =
                    new Vector3(scale, scale, 1f);
                fragment.rectTransform.anchoredPosition =
                    new Vector2(position.x, -position.y);
                fragment.color = new Color(1f, 1f, 1f, BUB_ALPHA);
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
                    1f - Mathf.Clamp01(FRAG_DAMP * delta);
                _fragmentVelocity[i].y += FRAG_RISE * delta;
                _fragmentPosition[i] += _fragmentVelocity[i] * delta;

                Image fragment = _fragments[i];
                fragment.rectTransform.anchoredPosition = new Vector2(
                    _fragmentPosition[i].x, -_fragmentPosition[i].y);
                Color color = fragment.color;
                color.a =
                    BUB_ALPHA * (_fragmentLife[i] / _fragmentMaxLife[i]);
                fragment.color = color;
            }
        }

        float NextFloat(float min, float max)
        {
            if (_bubbleRandom == null)
                _bubbleRandom = new System.Random(BUB_SEED);
            return min +
                (float)_bubbleRandom.NextDouble() * (max - min);
        }
    }
}
