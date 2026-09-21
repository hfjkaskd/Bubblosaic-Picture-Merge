using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of splash_page — underwater loading page.</summary>
    public class SplashPage : MonoBehaviour
    {
        const string SkinRoot = "Art/Sprites/PsdSkin20260807";
        const float PROGRESS_LEFT = -260.5f;
        const float PROGRESS_WIDTH = 521f;
        const float PROGRESS_HEIGHT = 42f;
        const float DEFAULT_MIN_WAIT_SECONDS = 3f;
        const float FINISH_TWEEN_DURATION = 0.1f;

        const int SLOGAN_COUNT = 67;
        const string QUOTE_KEY_PREFIX = "slogan_content_";
        const string AUTHOR_KEY_PREFIX = "slogan_author_";

        // Progress bubble emitter (splash_page.gd PB_*).
        const int PB_POOL = 32;
        const float PB_EMIT_INTERVAL = 0.15f;
        const float PB_EMIT_Y_FROM_BOTTOM = 495f;
        const float PB_SPEED_MIN = 130f;
        const float PB_SPEED_MAX = 210f;
        const float PB_LIFE_MIN = 0.85f;
        const float PB_LIFE_MAX = 1.3f;
        const float PB_SIZE_MIN = 0.089f;
        const float PB_SIZE_MAX = 0.249f;
        const float PB_SPREAD_X = 26f;
        const float PB_LAG = 28f;
        const float PB_WIGGLE_AMP = 9f;
        const float PB_WIGGLE_FREQ = 1.3f;
        const float PB_ALPHA = 0.8f;
        const int PB_SEED = 91237;

        // Edge decorations and their reusable fragment pool (splash_page.gd DECO_*/FRAG_*).
        const int DECO_COUNT = 15;
        const float DECO_MARGIN = 36f;
        const float DECO_BAND_W = 180f;
        const float DECO_TOP_Y = -130f;
        const float DECO_BOTTOM_Y = 2150f;
        const float DECO_SPEED_MIN = 20f;
        const float DECO_SPEED_MAX = 72f;
        const float DECO_ACCEL_PX = 5f;
        const float DECO_SIZE_MIN = 0.065f;
        const float DECO_SIZE_MAX = 0.358f;
        const float DECO_WIGGLE_FREQ = 0.22f;
        const float DECO_WIGGLE_AMP_MIN = 12f;
        const float DECO_WIGGLE_AMP_MAX = 26f;
        const float DECO_WIGGLE_CHANCE = 0.5f;
        const float DECO_ALPHA = 0.72f;
        const float DECO_FADE_IN = 280f;
        const float DECO_BURST_Y_MIN = 220f;
        const float DECO_BURST_Y_MAX = 1900f;
        const int DECO_SEED = 70234;
        const int FRAG_PER_BURST = 5;
        const int FRAG_POOL = 60;
        const float FRAG_SIZE_MIN = 0.0325f;
        const float FRAG_SIZE_MAX = 0.0845f;
        const float FRAG_LIFE_MIN = 0.6f;
        const float FRAG_LIFE_MAX = 1.1f;
        const float FRAG_SPEED_MIN = 40f;
        const float FRAG_SPEED_MAX = 130f;
        const float FRAG_RISE = 55f;
        const float FRAG_DAMP = 1.8f;

        // Light ray motion (light_rays.gd).
        const float RAY_ALPHA_AMP = 0.08f;
        const float RAY_BREATH_PERIOD = 6f;
        const float RAY_ALPHA_FLICKER = 0.03f;
        const float RAY_FLICKER_PERIOD = 1.6f;
        const float RAY_SWAY_DEG = 0.7f;
        const float RAY_SWAY_PERIOD = 8f;
        const float RAY_WIDTH_PULSE = 0.06f;
        const float RAY_WIDTH_PULSE_PERIOD = 7f;

        [Header("Prefab references")]
        [SerializeField] RectTransform _root;
        [SerializeField] RectTransform _progressFill;
        [SerializeField] RawImage _fillImg;
        [SerializeField] Image _bgGradient;
        [SerializeField] TMP_Text _quoteLabel;
        [SerializeField] TMP_Text _authorLabel;
        [SerializeField] RectTransform _raysRoot;
        [SerializeField] RawImage _waterRipple;
        [SerializeField] List<RectTransform> _deco = new List<RectTransform>();
        [SerializeField] Material _stripesTemplate;

        [Header("Loading artwork")]
        [SerializeField] Image _backgroundImage;
        [SerializeField] string _backgroundResourcePath;
        [SerializeField] bool _animatedDecorations = true;

        sealed class DecoState
        {
            public RectTransform Rect;
            public Image Image;
            public float BaseX;
            public float Speed;
            public float Phase;
            public float BurstY;
            public float Wiggle;
        }

        sealed class ParticleState
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float BaseX;
            public float Life;
            public float MaxLife = 1f;
            public float Phase;
        }

        sealed class RayState
        {
            public RectTransform Rect;
            public Image Image;
            public float BaseAngle;
            public float BaseAlpha;
            public float BaseWidth;
            public float BaseLength;
        }

        readonly List<DecoState> _decoState = new List<DecoState>();
        readonly List<ParticleState> _fragments = new List<ParticleState>();
        readonly List<ParticleState> _progressBubbles = new List<ParticleState>();
        readonly List<RayState> _rays = new List<RayState>();
        readonly Dictionary<Graphic, float> _contentBaseAlpha =
            new Dictionary<Graphic, float>();

        RectTransform _fragmentPoolRoot;
        RectTransform _progressBubblePoolRoot;
        System.Random _decoRng;
        System.Random _progressRng;
        Material _runtimeStripesMat;
        Material _runtimeWaterRippleMat;
        Sprite _runtimeGradientSprite;
        Texture2D _runtimeGradientTexture;
        float _progress;
        float _progressBubbleTime;
        float _progressBubbleEmitTimer;
        float _decoTime;
        float _rayTime;
        float _contentAlpha = 1f;
        int _contentFadeVersion;
        bool _running;
        bool _completed;
        bool _forceRequested;

        public float CarryMaskAlpha => _bgGradient != null ? _bgGradient.color.a : 0.6f;

        /// <summary>
        /// Rebuilds transient materials and effect pools. Fixed UI and the 15
        /// decorative bubbles remain authored in SplashPage.prefab.
        /// </summary>
        public void InitializePrefabRuntime()
        {
            ResolvePrefabReferences();
            if (_backgroundImage != null && !string.IsNullOrEmpty(_backgroundResourcePath))
            {
                var artwork = Resources.Load<Sprite>(_backgroundResourcePath);
                if (artwork == null)
                    Debug.LogError("Loading artwork is missing: " + _backgroundResourcePath, this);
                else
                    _backgroundImage.sprite = artwork;
            }
            Canvas.ForceUpdateCanvases();

            _progress = 0f;
            _progressBubbleTime = 0f;
            _progressBubbleEmitTimer = 0f;
            _decoTime = 0f;
            _rayTime = 0f;
            RestoreContentBaseAlphas();
            _contentAlpha = 1f;
            _contentFadeVersion++;
            _completed = false;
            _forceRequested = false;
            _running = true;

            if (_fillImg != null)
            {
                bool usesPsdFill = _fillImg.texture != null &&
                    _fillImg.texture.name == "progress_fill";
                if (usesPsdFill)
                {
                    if (_runtimeStripesMat != null)
                        DestroyTransient(_runtimeStripesMat);
                    _runtimeStripesMat = null;
                    _fillImg.material = null;
                }

                Material source = _stripesTemplate;
                if (source == null && _runtimeStripesMat != null)
                    source = _runtimeStripesMat;
                if (source == null && _fillImg.material != null &&
                    _fillImg.material.shader != null &&
                    _fillImg.material.shader.name == "BubblePics/SplashStripes")
                    source = _fillImg.material;

                Material replacement = null;
                if (!usesPsdFill && source != null)
                    replacement = new Material(source);
                else if (!usesPsdFill)
                {
                    var shader = Shader.Find("BubblePics/SplashStripes");
                    if (shader != null) replacement = new Material(shader);
                }

                if (_runtimeStripesMat != null)
                    DestroyTransient(_runtimeStripesMat);
                _runtimeStripesMat = replacement;
                if (_runtimeStripesMat != null)
                    _fillImg.material = _runtimeStripesMat;
            }

            // The legacy authoring path creates this texture in memory. It
            // cannot survive a prefab disk round-trip, so recreate it as needed.
            if (_bgGradient != null && _bgGradient.sprite == null)
            {
                _runtimeGradientTexture = MakeGradient(
                    new Color32(0x24, 0x80, 0xE9, 0xFF),
                    new Color32(0x24, 0x58, 0xE9, 0xFF));
                _runtimeGradientSprite = Sprite.Create(
                    _runtimeGradientTexture,
                    new Rect(0, 0, _runtimeGradientTexture.width, _runtimeGradientTexture.height),
                    new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
                _bgGradient.sprite = _runtimeGradientSprite;
            }

            ApplyQuoteStyles();
            if (SaveDataUtils.gameStrategy.Loaded) SetupQuoteForToday();
            else
            {
                _quoteLabel.text = Localization.Tr(QUOTE_KEY_PREFIX + "1");
                _authorLabel.text = "- " + Localization.Tr(AUTHOR_KEY_PREFIX + "1");
            }
            if (_animatedDecorations)
            {
                SetupWaterRipple();
                RebuildRayState();
                RebuildDecoState();
                EnsureEffectPools();
                ResetParticlePools();
            }
            ApplyProgress(0f);
        }

        public void Build(Transform parent)
        {
            // A persistent prefab already owns all fixed nodes. Build remains
            // only as an idempotent legacy fallback.
            if (_root != null && _progressFill != null && _fillImg != null)
            {
                InitializePrefabRuntime();
                return;
            }

            transform.SetParent(parent, false);
            _root = UiFactory.FullStretch((RectTransform)parent, "SplashRoot");
            transform.SetParent(_root, false);

            var bg = UiFactory.Img(_root, "Background", SkinRoot + "/Home/background", 0, 0);
            FitCover(bg.rectTransform, bg.sprite);

            var gradTex = MakeGradient(
                new Color32(0x24, 0x80, 0xE9, 0xFF),
                new Color32(0x24, 0x58, 0xE9, 0xFF));
            var grad = UiFactory.Node(_root, "BgGradient");
            grad.anchorMin = Vector2.zero;
            grad.anchorMax = Vector2.one;
            grad.offsetMin = Vector2.zero;
            grad.offsetMax = Vector2.zero;
            _bgGradient = grad.gameObject.AddComponent<Image>();
            _runtimeGradientTexture = gradTex;
            _runtimeGradientSprite = Sprite.Create(
                gradTex, new Rect(0, 0, gradTex.width, gradTex.height),
                new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            _bgGradient.sprite = _runtimeGradientSprite;
            _bgGradient.color = new Color(1, 1, 1, 0.6f);
            _bgGradient.raycastTarget = false;

            BuildCanvasRays();
            BuildWaterRipple();
            BuildDecoBubbles();

            _quoteLabel = UiFactory.Label(
                _root, "Quote", Localization.Tr("slogan_content_1"), 80,
                Color.white, AssetLib.UiFont, TextAnchor.UpperCenter, 888, 220);
            _quoteLabel.rectTransform.anchorMin = _quoteLabel.rectTransform.anchorMax =
                new Vector2(0.5f, 1f);
            _quoteLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _quoteLabel.rectTransform.anchoredPosition = new Vector2(0, -336);
            _quoteLabel.enableWordWrapping = true;
            _quoteLabel.overflowMode = TextOverflowModes.Overflow;

            _authorLabel = UiFactory.Label(
                _root, "Author", "- " + Localization.Tr("slogan_author_1"), 64,
                Color.white, AssetLib.UiFont, TextAnchor.UpperRight, 888, 90);
            _authorLabel.rectTransform.anchorMin = _authorLabel.rectTransform.anchorMax =
                new Vector2(0.5f, 1f);
            _authorLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _authorLabel.rectTransform.anchoredPosition = new Vector2(0, -624);
            ApplyQuoteStyles();

            var track = UiFactory.Img(
                _root, "ProgressTrack", SkinRoot + "/Home/progress_track", 541, 64);
            track.rectTransform.anchorMin = track.rectTransform.anchorMax =
                new Vector2(0.5f, 0f);
            track.rectTransform.anchoredPosition = new Vector2(0, 651f);
            track.type = Image.Type.Simple;

            var fillGo = new GameObject("ProgressFill");
            fillGo.transform.SetParent(_root, false);
            _fillImg = fillGo.AddComponent<RawImage>();
            Material generatedStripes = null;
            _fillImg.texture = AssetLib.Texture(SkinRoot + "/Home/progress_fill");
            _fillImg.raycastTarget = false;
            _progressFill = _fillImg.rectTransform;
            _progressFill.anchorMin = _progressFill.anchorMax = new Vector2(0.5f, 0f);
            _progressFill.pivot = new Vector2(0, 0.5f);
            _progressFill.anchoredPosition = new Vector2(PROGRESS_LEFT, 654f);
            _progressFill.sizeDelta = new Vector2(0, PROGRESS_HEIGHT);

            var logo = UiFactory.Img(
                _root, "Logo", SkinRoot + "/Home/splash_logo", 894, 882);
            logo.rectTransform.anchorMin = logo.rectTransform.anchorMax =
                new Vector2(0.5f, 0f);
            logo.rectTransform.anchoredPosition = new Vector2(0, 1575f);

            InitializePrefabRuntime();
            DestroyTransient(generatedStripes);
        }

        void ResolvePrefabReferences()
        {
            if (_root == null)
            {
                var ownRect = transform as RectTransform;
                _root = ownRect != null && ownRect.name == "SplashRoot"
                    ? ownRect
                    : FindRect(transform, "SplashRoot");
            }
            if (_progressFill == null) _progressFill = FindRect(_root, "ProgressFill");
            if (_fillImg == null && _progressFill != null)
                _fillImg = _progressFill.GetComponent<RawImage>();
            if (_bgGradient == null)
            {
                var rt = FindRect(_root, "BgGradient");
                if (rt != null) _bgGradient = rt.GetComponent<Image>();
            }
            if (_quoteLabel == null)
            {
                var rt = FindRect(_root, "Quote") ?? FindRect(_root, "QuoteLabel");
                if (rt != null) _quoteLabel = rt.GetComponent<TMP_Text>();
            }
            if (_authorLabel == null)
            {
                var rt = FindRect(_root, "Author") ?? FindRect(_root, "AuthorLabel");
                if (rt != null) _authorLabel = rt.GetComponent<TMP_Text>();
            }
            if (_raysRoot == null) _raysRoot = FindRect(_root, "Rays");
            if (_waterRipple == null)
            {
                var rt = FindRect(_root, "WaterRipple");
                if (rt != null) _waterRipple = rt.GetComponent<RawImage>();
            }

            if (_deco == null) _deco = new List<RectTransform>();
            _deco.RemoveAll(rt => rt == null);
            if (_deco.Count == 0 && _root != null)
            {
                _deco.AddRange(_root.GetComponentsInChildren<RectTransform>(true)
                    .Where(rt => rt.name.StartsWith("Deco", StringComparison.Ordinal))
                    .OrderBy(rt => ParseTrailingNumber(rt.name)));
            }
        }

        public void SetupQuoteForToday()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            int index;
            if (!string.Equals(SaveState.SplashQuoteDate, today, StringComparison.Ordinal))
            {
                // Exact splash_page.gd behaviour: the first launch on a new
                // date always displays slogan #1 and persists only the date.
                index = 1;
                SaveState.SetSplashQuoteDate(today);
            }
            else
            {
                // Further launches on the same date independently choose from
                // #2..#67. The selected index is deliberately not persisted.
                index = UnityEngine.Random.Range(2, SLOGAN_COUNT + 1);
            }

            string quoteKey = QUOTE_KEY_PREFIX + index;
            string authorKey = AUTHOR_KEY_PREFIX + index;
            string quote = Localization.Tr(quoteKey);
            string author = Localization.Tr(authorKey);

            // A partially imported locale must still show readable content.
            if (quote == quoteKey)
                quote = Localization.Tr(QUOTE_KEY_PREFIX + "1");
            if (author == authorKey)
                author = Localization.Tr(AUTHOR_KEY_PREFIX + "1");

            if (_quoteLabel != null) _quoteLabel.text = quote;
            if (_authorLabel != null) _authorLabel.text = "- " + author;
            LayoutQuoteAuthor();
        }

        void ApplyQuoteStyles()
        {
            var shadowColor = new Color(0.149f, 0.216f, 0.322f, 0.55f);
            if (_quoteLabel != null)
            {
                _quoteLabel.enableWordWrapping = true;
                _quoteLabel.overflowMode = TextOverflowModes.Overflow;
                TmpTextStyle.ApplyShadow(
                    _quoteLabel,
                    shadowColor,
                    new Vector2(0f, -6f),
                    10f);
            }
            if (_authorLabel != null)
                TmpTextStyle.ApplyShadow(
                    _authorLabel,
                    shadowColor,
                    new Vector2(0f, -6f),
                    10f);
        }

        void SetupWaterRipple()
        {
            if (_waterRipple == null) return;
            if (_runtimeWaterRippleMat != null)
                DestroyTransient(_runtimeWaterRippleMat);

            var shader = Shader.Find("BubblePics/WaterRipple");
            if (shader == null) return;
            _runtimeWaterRippleMat = new Material(shader)
            {
                name = "SplashWaterRipple (Runtime)",
            };
            _runtimeWaterRippleMat.SetTexture(
                "_CausticsTex", AssetLib.Texture("Art/Sprites/Home/water_caustics"));
            _runtimeWaterRippleMat.SetFloat("_ScaleU", 3.46f);
            _runtimeWaterRippleMat.SetFloat("_Aspect", 0.52f);
            _runtimeWaterRippleMat.SetFloat("_HStretch", 1.6f);
            _runtimeWaterRippleMat.SetVector(
                "_ScrollA", new Vector4(0.05f, 0.03f, 0, 0));
            _runtimeWaterRippleMat.SetVector(
                "_ScrollB", new Vector4(-0.045f, 0.0375f, 0, 0));
            _runtimeWaterRippleMat.SetFloat("_RippleFreq", 2.4f);
            _runtimeWaterRippleMat.SetFloat("_RippleAmp", 0.05f);
            _runtimeWaterRippleMat.SetFloat("_Intensity", 0.25f);
            _runtimeWaterRippleMat.SetColor("_Tint", new Color(0.85f, 0.95f, 1f, 1f));
            _runtimeWaterRippleMat.SetFloat("_BandHeight", 0.167f);
            _runtimeWaterRippleMat.SetFloat("_BandFade", 0.833f);
            _runtimeWaterRippleMat.SetFloat("_Persp", 5f);
            _runtimeWaterRippleMat.SetFloat("_PerspCurve", 3f);
            _runtimeWaterRippleMat.SetFloat("_GlobalAlpha", 1f);
            _waterRipple.texture = Texture2D.whiteTexture;
            _waterRipple.material = _runtimeWaterRippleMat;
            _waterRipple.raycastTarget = false;
        }

        void LayoutQuoteAuthor()
        {
            if (_quoteLabel == null || _authorLabel == null) return;
            Canvas.ForceUpdateCanvases();
            float quoteHeight = Mathf.Max(_quoteLabel.preferredHeight, _quoteLabel.fontSize);
            var size = _quoteLabel.rectTransform.sizeDelta;
            size.y = quoteHeight;
            _quoteLabel.rectTransform.sizeDelta = size;
            _authorLabel.rectTransform.anchoredPosition =
                new Vector2(0, -336f - quoteHeight - 56f);
        }

        void RebuildRayState()
        {
            _rays.Clear();
            if (_raysRoot == null) return;

            var rayRects = _raysRoot.GetComponentsInChildren<RectTransform>(true)
                .Where(rt => rt != _raysRoot &&
                             rt.name.StartsWith("Ray", StringComparison.Ordinal))
                .OrderBy(rt => ParseTrailingNumber(rt.name))
                .Take(16)
                .ToList();
            var rng = new System.Random(20260609);
            for (int i = 0; i < rayRects.Count; i++)
            {
                float width = Range(rng, 0.22f, 2.3f);
                float length = Range(rng, 0.9f, 1.9f);
                float t = i / 15f;
                float angle = Mathf.Lerp(-33f, 33f, t) + Range(rng, -3f, 3f);
                float alpha = Range(rng, 0.28f, 0.56f);
                var rect = rayRects[i];
                var image = rect.GetComponent<Image>();
                rect.localScale = new Vector3(width, length, 1f);
                rect.localRotation = Quaternion.Euler(0, 0, -angle);
                if (image != null)
                    image.color = new Color(0.85f, 0.95f, 1f, alpha);
                _rays.Add(new RayState
                {
                    Rect = rect,
                    Image = image,
                    BaseAngle = angle,
                    BaseAlpha = alpha,
                    BaseWidth = width,
                    BaseLength = length,
                });
            }
        }

        void RebuildDecoState()
        {
            _decoState.Clear();
            _decoRng = new System.Random(DECO_SEED);
            for (int i = 0; i < _deco.Count; i++)
            {
                var state = new DecoState
                {
                    Rect = _deco[i],
                    Image = _deco[i] != null ? _deco[i].GetComponent<Image>() : null,
                };
                _decoState.Add(state);
                ResetDeco(i, true);
            }
        }

        void ResetDeco(int index, bool initial = false)
        {
            if (index < 0 || index >= _decoState.Count) return;
            var state = _decoState[index];
            if (state.Rect == null) return;

            float width = DesignWidth;
            float height = DesignHeight;
            bool left = index % 2 == 0;
            float bandMin = left ? DECO_MARGIN : width - DECO_MARGIN - DECO_BAND_W;
            state.BaseX = bandMin + Range(_decoRng, 0, DECO_BAND_W);
            state.Speed = Range(_decoRng, DECO_SPEED_MIN, DECO_SPEED_MAX);
            state.Phase = Range(_decoRng, 0, Mathf.PI * 2f);
            state.BurstY = Range(_decoRng, DECO_BURST_Y_MIN, DECO_BURST_Y_MAX);
            state.Wiggle = _decoRng.NextDouble() < DECO_WIGGLE_CHANCE
                ? Range(_decoRng, DECO_WIGGLE_AMP_MIN, DECO_WIGGLE_AMP_MAX)
                : 0f;
            float scale = Range(_decoRng, DECO_SIZE_MIN, DECO_SIZE_MAX);
            float godotY = initial
                ? Range(_decoRng, state.BurstY, DECO_BOTTOM_Y)
                : DECO_BOTTOM_Y;

            state.Rect.anchorMin = state.Rect.anchorMax = Vector2.zero;
            state.Rect.pivot = new Vector2(0.5f, 0.5f);
            state.Rect.sizeDelta = new Vector2(200, 200);
            state.Rect.localScale = Vector3.one * scale;
            state.Rect.anchoredPosition = new Vector2(state.BaseX, height - godotY);
            state.Rect.gameObject.SetActive(true);
            SetImageAlpha(state.Image, initial ? 0f : DECO_ALPHA);
        }

        void EnsureEffectPools()
        {
            if (_root == null) return;
            Sprite sprite = _decoState
                .Select(state => state.Image != null ? state.Image.sprite : null)
                .FirstOrDefault(value => value != null);
            if (sprite == null) sprite = AssetLib.Sprite("Art/Sprites/Home/deco_bubble");

            int decoSibling = _deco
                .Where(rt => rt != null && rt.parent == _root)
                .Select(rt => rt.GetSiblingIndex())
                .DefaultIfEmpty(2)
                .Max() + 1;
            _fragmentPoolRoot = EnsurePoolRoot(
                _fragmentPoolRoot, "RisingBubbleFragments", decoSibling);
            BuildParticlePool(_fragmentPoolRoot, "Fragment", FRAG_POOL, sprite, _fragments);

            int progressSibling = _progressFill != null && _progressFill.parent == _root
                ? _progressFill.GetSiblingIndex() + 1
                : _root.childCount;
            _progressBubblePoolRoot = EnsurePoolRoot(
                _progressBubblePoolRoot, "ProgressBubbles", progressSibling);
            BuildParticlePool(
                _progressBubblePoolRoot, "ProgressBubble", PB_POOL, sprite, _progressBubbles);
        }

        RectTransform EnsurePoolRoot(RectTransform current, string nodeName, int siblingIndex)
        {
            if (current == null) current = FindDirectRect(_root, nodeName);
            if (current == null)
            {
                var go = new GameObject(nodeName, typeof(RectTransform));
                go.layer = _root.gameObject.layer;
                current = go.GetComponent<RectTransform>();
                current.SetParent(_root, false);
            }
            current.anchorMin = Vector2.zero;
            current.anchorMax = Vector2.one;
            current.offsetMin = Vector2.zero;
            current.offsetMax = Vector2.zero;
            current.pivot = new Vector2(0.5f, 0.5f);
            current.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, _root.childCount - 1));
            return current;
        }

        static void BuildParticlePool(
            RectTransform parent, string prefix, int count, Sprite sprite,
            List<ParticleState> output)
        {
            output.Clear();
            var existing = parent.Cast<Transform>()
                .Select(child => child as RectTransform)
                .Where(rect => rect != null &&
                               rect.name.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(rect => ParseTrailingNumber(rect.name))
                .ToList();

            for (int i = 0; i < count; i++)
            {
                RectTransform rect;
                Image image;
                if (i < existing.Count)
                {
                    rect = existing[i];
                    image = rect.GetComponent<Image>();
                    if (image == null) image = rect.gameObject.AddComponent<Image>();
                }
                else
                {
                    var go = new GameObject(
                        prefix + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.layer = parent.gameObject.layer;
                    rect = go.GetComponent<RectTransform>();
                    rect.SetParent(parent, false);
                    image = go.GetComponent<Image>();
                }

                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(200, 200);
                image.sprite = sprite;
                image.raycastTarget = false;
                rect.gameObject.SetActive(false);
                output.Add(new ParticleState { Rect = rect, Image = image });
            }
        }

        void ResetParticlePools()
        {
            _progressRng = new System.Random(PB_SEED);
            foreach (var particle in _fragments)
            {
                particle.Life = 0;
                particle.MaxLife = 1;
                if (particle.Rect != null) particle.Rect.gameObject.SetActive(false);
            }
            foreach (var particle in _progressBubbles)
            {
                particle.Life = 0;
                particle.MaxLife = 1;
                if (particle.Rect != null) particle.Rect.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (!_running || _completed || !_animatedDecorations) return;

            float delta = Time.deltaTime;
            UpdateRays(delta);
            UpdateDecoBubbles(delta);
            UpdateFragments(delta);
            UpdateProgressBubbles(delta);

        }

        void UpdateRays(float delta)
        {
            _rayTime += delta;
            for (int i = 0; i < _rays.Count; i++)
            {
                var ray = _rays[i];
                if (ray.Rect == null) continue;
                float fi = i;
                float breathe = RAY_ALPHA_AMP *
                    Mathf.Sin(Mathf.PI * 2f * _rayTime / RAY_BREATH_PERIOD + fi * 0.9f);
                float flicker = RAY_ALPHA_FLICKER *
                    Mathf.Sin(Mathf.PI * 2f * _rayTime / RAY_FLICKER_PERIOD + fi * 1.7f);
                if (ray.Image != null)
                {
                    float alpha = Mathf.Max(0, ray.BaseAlpha + breathe + flicker);
                    ray.Image.color =
                        new Color(0.85f, 0.95f, 1f, alpha * _contentAlpha);
                }
                float sway = RAY_SWAY_DEG *
                    Mathf.Sin(Mathf.PI * 2f * _rayTime / RAY_SWAY_PERIOD + fi * 0.7f);
                ray.Rect.localRotation = Quaternion.Euler(0, 0, -(ray.BaseAngle + sway));
                float widthPulse = 1f + RAY_WIDTH_PULSE *
                    Mathf.Sin(Mathf.PI * 2f * _rayTime / RAY_WIDTH_PULSE_PERIOD + fi * 0.5f);
                ray.Rect.localScale =
                    new Vector3(ray.BaseWidth * widthPulse, ray.BaseLength, 1f);
            }
        }

        void UpdateDecoBubbles(float delta)
        {
            _decoTime += delta;
            float height = DesignHeight;
            for (int i = 0; i < _decoState.Count; i++)
            {
                var state = _decoState[i];
                if (state.Rect == null) continue;

                state.Speed += DECO_ACCEL_PX * delta;
                var position = state.Rect.anchoredPosition;
                position.y += state.Speed * delta;
                float burstUnityY = height - state.BurstY;
                float topUnityY = height - DECO_TOP_Y;
                if (position.y >= burstUnityY || position.y > topUnityY)
                {
                    if (position.y >= burstUnityY)
                        SpawnFragments(position);
                    ResetDeco(i);
                    continue;
                }

                position.x = state.BaseX + state.Wiggle *
                    Mathf.Sin(_decoTime * DECO_WIGGLE_FREQ * Mathf.PI * 2f + state.Phase);
                state.Rect.anchoredPosition = position;

                float godotY = height - position.y;
                float fadeIn = Mathf.Clamp01((DECO_BOTTOM_Y - godotY) / DECO_FADE_IN);
                float fadeOut = Mathf.Clamp01((godotY - state.BurstY) / 200f);
                SetImageAlpha(state.Image, DECO_ALPHA * Mathf.Min(fadeIn, fadeOut));
            }
        }

        void SpawnFragments(Vector2 position)
        {
            int spawned = 0;
            for (int i = 0; i < _fragments.Count && spawned < FRAG_PER_BURST; i++)
            {
                var particle = _fragments[i];
                if (particle.Life > 0 || particle.Rect == null) continue;

                float angle = Range(_decoRng, 0, Mathf.PI * 2f);
                float speed = Range(_decoRng, FRAG_SPEED_MIN, FRAG_SPEED_MAX);
                // Godot's negative Y is upward; Unity UI's positive Y is upward.
                particle.Velocity = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * speed;
                particle.MaxLife = Range(_decoRng, FRAG_LIFE_MIN, FRAG_LIFE_MAX);
                particle.Life = particle.MaxLife;
                float scale = Range(_decoRng, FRAG_SIZE_MIN, FRAG_SIZE_MAX);
                particle.Rect.localScale = Vector3.one * scale;
                particle.Rect.anchoredPosition = position;
                particle.Rect.gameObject.SetActive(true);
                SetImageAlpha(particle.Image, DECO_ALPHA);
                spawned++;
            }
        }

        void UpdateFragments(float delta)
        {
            for (int i = 0; i < _fragments.Count; i++)
            {
                var particle = _fragments[i];
                if (particle.Life <= 0 || particle.Rect == null) continue;
                particle.Life -= delta;
                if (particle.Life <= 0)
                {
                    particle.Life = 0;
                    particle.Rect.gameObject.SetActive(false);
                    continue;
                }

                particle.Velocity *= 1f - Mathf.Clamp01(FRAG_DAMP * delta);
                particle.Velocity += Vector2.up * FRAG_RISE * delta;
                particle.Rect.anchoredPosition += particle.Velocity * delta;
                SetImageAlpha(
                    particle.Image, DECO_ALPHA * (particle.Life / particle.MaxLife));
            }
        }

        void UpdateProgressBubbles(float delta)
        {
            _progressBubbleTime += delta;
            if (_progress < 1f)
            {
                _progressBubbleEmitTimer += delta;
                while (_progressBubbleEmitTimer >= PB_EMIT_INTERVAL)
                {
                    _progressBubbleEmitTimer -= PB_EMIT_INTERVAL;
                    EmitProgressBubble();
                }
            }

            for (int i = 0; i < _progressBubbles.Count; i++)
            {
                var particle = _progressBubbles[i];
                if (particle.Life <= 0 || particle.Rect == null) continue;
                particle.Life -= delta;
                if (particle.Life <= 0)
                {
                    particle.Life = 0;
                    particle.Rect.gameObject.SetActive(false);
                    continue;
                }

                var position = particle.Rect.anchoredPosition;
                position.y += particle.Velocity.y * delta;
                position.x = particle.BaseX + PB_WIGGLE_AMP *
                    Mathf.Sin(_progressBubbleTime * PB_WIGGLE_FREQ * Mathf.PI * 2f +
                              particle.Phase);
                particle.Rect.anchoredPosition = position;
                SetImageAlpha(
                    particle.Image, PB_ALPHA * (particle.Life / particle.MaxLife));
            }
        }

        void EmitProgressBubble()
        {
            for (int i = 0; i < _progressBubbles.Count; i++)
            {
                var particle = _progressBubbles[i];
                if (particle.Life > 0 || particle.Rect == null) continue;

                float emitX = DesignWidth * 0.5f + PROGRESS_LEFT + PROGRESS_WIDTH * _progress;
                particle.BaseX = emitX - PB_LAG +
                                 Range(_progressRng, -PB_SPREAD_X, PB_SPREAD_X);
                particle.Rect.anchoredPosition =
                    new Vector2(particle.BaseX, PB_EMIT_Y_FROM_BOTTOM);
                particle.Velocity = new Vector2(
                    0, Range(_progressRng, PB_SPEED_MIN, PB_SPEED_MAX));
                particle.MaxLife = Range(_progressRng, PB_LIFE_MIN, PB_LIFE_MAX);
                particle.Life = particle.MaxLife;
                particle.Phase = Range(_progressRng, 0, Mathf.PI * 2f);
                float scale = Range(_progressRng, PB_SIZE_MIN, PB_SIZE_MAX);
                particle.Rect.localScale = Vector3.one * scale;
                particle.Rect.gameObject.SetActive(true);
                SetImageAlpha(particle.Image, PB_ALPHA);
                return;
            }
        }

        public void ApplyProgress(float value)
        {
            _progress = Mathf.Clamp01(value);
            if (_progressFill == null) return;
            _progressFill.sizeDelta = new Vector2(
                PROGRESS_WIDTH * _progress,
                PROGRESS_HEIGHT);
            if (_runtimeStripesMat != null)
            {
                _runtimeStripesMat.SetVector(
                    "_BarPx", new Vector4(
                        PROGRESS_WIDTH * _progress,
                        PROGRESS_HEIGHT,
                        0,
                        0));
            }
        }

        public IEnumerator ForceComplete()
        {
            if (_completed) yield break;
            if (_forceRequested)
            {
                while (!_completed) yield return null;
                yield break;
            }

            _forceRequested = true;
            float from = _progress;
            float elapsed = 0;
            while (elapsed < FINISH_TWEEN_DURATION)
            {
                elapsed += Time.deltaTime;
                ApplyProgress(Mathf.Lerp(from, 1f, elapsed / FINISH_TWEEN_DURATION));
                yield return null;
            }
            ApplyProgress(1f);
            _completed = true;
            _running = false;
        }

        /// <summary>
        /// Fades the splash foreground while retaining Background and
        /// BgGradient as the carry mask for the incoming game page.
        /// </summary>
        public IEnumerator FadeOutContent(float duration = 0.2f)
        {
            int version = ++_contentFadeVersion;
            float from = _contentAlpha;
            if (duration <= 0f)
            {
                SetContentAlpha(0f);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (version != _contentFadeVersion) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
                SetContentAlpha(Mathf.Lerp(from, 0f, eased));
                yield return null;
            }
            if (version == _contentFadeVersion)
                SetContentAlpha(0f);
        }

        void SetContentAlpha(float factor)
        {
            _contentAlpha = Mathf.Clamp01(factor);
            if (_root == null) return;

            foreach (var graphic in _root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || IsCarryBackground(graphic.transform)) continue;
                if (!_contentBaseAlpha.TryGetValue(graphic, out float baseAlpha))
                {
                    baseAlpha = graphic.color.a;
                    _contentBaseAlpha.Add(graphic, baseAlpha);
                }
                var color = graphic.color;
                color.a = baseAlpha * _contentAlpha;
                graphic.color = color;
            }
            if (_runtimeWaterRippleMat != null)
                _runtimeWaterRippleMat.SetFloat("_GlobalAlpha", _contentAlpha);
        }

        void RestoreContentBaseAlphas()
        {
            foreach (var pair in _contentBaseAlpha)
            {
                if (pair.Key == null) continue;
                var color = pair.Key.color;
                color.a = pair.Value;
                pair.Key.color = color;
            }
            _contentBaseAlpha.Clear();
            if (_runtimeWaterRippleMat != null)
                _runtimeWaterRippleMat.SetFloat("_GlobalAlpha", 1f);
        }

        bool IsCarryBackground(Transform candidate)
        {
            for (var current = candidate; current != null && current != _root.parent;
                 current = current.parent)
            {
                if (current == _root) return false;
                if (current.name == "Background" || current.name == "BgGradient")
                    return true;
            }
            return false;
        }

        public void Hide()
        {
            _running = false;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_runtimeStripesMat != null) DestroyTransient(_runtimeStripesMat);
            if (_runtimeWaterRippleMat != null) DestroyTransient(_runtimeWaterRippleMat);
            if (_runtimeGradientSprite != null) DestroyTransient(_runtimeGradientSprite);
            if (_runtimeGradientTexture != null) DestroyTransient(_runtimeGradientTexture);
        }

        float DesignWidth => _root != null && _root.rect.width > 1f
            ? _root.rect.width
            : 1080f;

        float DesignHeight => _root != null && _root.rect.height > 1f
            ? _root.rect.height
            : 2400f;

        static float Range(System.Random rng, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        void SetImageAlpha(Image image, float alpha)
        {
            if (image == null) return;
            var color = image.color;
            color.a = alpha * _contentAlpha;
            image.color = color;
        }

        static RectTransform FindRect(Transform parent, string objectName)
        {
            if (parent == null) return null;
            foreach (var rt in parent.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == objectName) return rt;
            return null;
        }

        static RectTransform FindDirectRect(Transform parent, string objectName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i) as RectTransform;
                if (child != null && child.name == objectName) return child;
            }
            return null;
        }

        static int ParseTrailingNumber(string value)
        {
            int start = value.Length;
            while (start > 0 && char.IsDigit(value[start - 1])) start--;
            return start < value.Length &&
                   int.TryParse(value.Substring(start), out int number)
                ? number
                : int.MaxValue;
        }

        static void FitCover(RectTransform rect, Sprite sprite)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            if (sprite == null)
            {
                rect.sizeDelta = new Vector2(1080, 2400);
                return;
            }
            float scale = Mathf.Max(1080f / sprite.rect.width, 2400f / sprite.rect.height);
            rect.sizeDelta = new Vector2(
                sprite.rect.width * scale, sprite.rect.height * scale);
        }

        static Texture2D MakeGradient(Color32 top, Color32 bottom)
        {
            var texture = new Texture2D(1, 256, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int y = 0; y < 256; y++)
                texture.SetPixel(0, y, Color.Lerp(bottom, top, y / 255f));
            texture.Apply();
            return texture;
        }

        void BuildCanvasRays()
        {
            var rng = new System.Random(20260609);
            _raysRoot = UiFactory.Node(_root, "Rays");
            _raysRoot.anchorMin = _raysRoot.anchorMax = new Vector2(0.5f, 1f);
            _raysRoot.anchoredPosition = new Vector2(0, 520);
            for (int i = 0; i < 16; i++)
            {
                var image = UiFactory.Img(
                    _raysRoot, "Ray" + i, "Art/Sprites/Home/home_light", 84, 920);
                image.rectTransform.pivot = new Vector2(0.5f, 1f);
                // Runtime setup reapplies the exact original deterministic layout.
                rng.NextDouble();
                rng.NextDouble();
                rng.NextDouble();
                rng.NextDouble();
            }
        }

        void BuildWaterRipple()
        {
            var rect = UiFactory.Node(_root, "WaterRipple");
            rect.anchorMin = new Vector2(0, 0.766f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _waterRipple = rect.gameObject.AddComponent<RawImage>();
            _waterRipple.raycastTarget = false;
        }

        void BuildDecoBubbles()
        {
            for (int i = 0; i < DECO_COUNT; i++)
            {
                var image = UiFactory.Img(
                    _root, "Deco" + i, "Art/Sprites/Home/deco_bubble", 200, 200);
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
                image.color = new Color(1, 1, 1, DECO_ALPHA);
                _deco.Add(image.rectTransform);
            }
        }

        static void DestroyTransient(UnityEngine.Object value)
        {
            if (value == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Prefab authoring may temporarily persist referenced objects.
                return;
            }
#endif
            Destroy(value);
        }
    }
}
