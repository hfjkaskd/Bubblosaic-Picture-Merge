using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Unified completion page: completion Spine and the next-level button.
    /// The former clear-level banner and scrolling photo wall are intentionally
    /// disabled for every gameplay rule.
    /// </summary>
    public class CompletePanel : MonoBehaviour
    {
        const float INTRO_TIMEOUT = 2.5f;
        const float BACKDROP_FADE_DUR = 0.25f;
        const float NEXT_FADE_DUR = 0.4f;
        const float BANNER_Y = 400f;
        const float BANNER_TEXT_CENTER_ABOVE_BANNER = 68f;

        // Legacy photo-wall authoring data is retained so existing prefabs
        // deserialize cleanly. Runtime entry points always disable this view.
        const float CARD_SIZE = 480f;
        const float CARD_GAP = 36f;
        const float SCROLL_SPEED = 90f;
        const float PHOTO_INSET = 12f;
        const float PHOTO_CORNER = 28f;
        const float FRAME_GOLD_WIDTH = 3f;
        static readonly Color FrameFillColor = new Color32(0xFD, 0xF6, 0xE8, 0xFF);
        static readonly Color FrameGoldColor = new Color32(0xCA, 0xA8, 0x6E, 0xFF);
        static readonly Color FrameShadowColor = new Color(0.095f, 0.13f, 0.187f, 0.16f);
        static Sprite _photoFrameSprite;
        BubblePage _page;
        public BubblePage Page
        {
            get => _page;
            set => _page = value;
        }
        public bool Visible { get; private set; }

        [SerializeField] RectTransform _root;
        [SerializeField] Image _backdrop;
        [SerializeField] SpineLite.SpineSprite _bannerSpine;
        [SerializeField] GameObject _bannerRoot;
        [SerializeField] RectTransform _bannerTextRoot;
        [SerializeField] CurvedText _bannerOutline;
        [SerializeField] CurvedText _bannerFace;
        [SerializeField] CanvasGroup _bannerTextCg;
        [SerializeField] RectTransform _photoWall;
        [SerializeField] CanvasGroup _photoWallCanvasGroup;
        [SerializeField] RectTransform _topRow;
        [SerializeField] RectTransform _bottomRow;
        [SerializeField] CommonButton _nextButton;
        [SerializeField] ParticleSystem[] _confetti;
        Material[] _confettiMaterials;
        Coroutine _confettiFadeCo;
        Coroutine _scrollCo;
        bool _introFinished;

        public void Build(Transform parent)
        {
            if (HasPrefabReferences())
            {
                InitializePrefabRuntime(Page, parent);
                if (_root != null) _root.gameObject.SetActive(false);
                return;
            }

            _root = UiFactory.FullStretch((RectTransform)parent, "CompletePanel");

            _backdrop = UiFactory.Rect(_root, "Backdrop", new Color(0, 0, 0, 0), 0, 0);
            var bd = _backdrop.rectTransform;
            bd.anchorMin = Vector2.zero; bd.anchorMax = Vector2.one;
            bd.offsetMin = Vector2.zero; bd.offsetMax = Vector2.zero;

            // The new static face is authored at 564x266, centred 560px above
            // the 2340px PSD bottom edge. The title/confetti remain animated.
            _nextButton = CommonButton.Create(_root, "NextButton", CommonButton.Variant.Green, Localization.Tr("next"));
            var nb = _nextButton.Root;
            nb.anchorMin = nb.anchorMax = new Vector2(0.5f, 0f);
            nb.sizeDelta = new Vector2(564, 266);
            nb.anchoredPosition = new Vector2(0, 560);
            _nextButton.OnClick = OnNextPressed;

            BuildConfetti();
            BindPrefabRuntime();
            _root.gameObject.SetActive(false);
        }

        bool HasPrefabReferences()
        {
            return _root != null || _backdrop != null || _bannerSpine != null ||
                   _photoWall != null || _nextButton != null;
        }

        /// <summary>Injects page state and restores callbacks on a CompletePanel prefab instance.</summary>
        public void InitializePrefabRuntime(BubblePage page, Transform parent = null)
        {
            Page = page;
            if (_root == null)
                _root = transform as RectTransform;
            if (parent != null && _root != null && _root != parent && !_root.IsChildOf(parent))
                _root.SetParent(parent, false);
            BindPrefabRuntime();
        }

        /// <summary>Rebinds runtime-only delegates and recovers optional references by node name.</summary>
        public void BindPrefabRuntime()
        {
            if (_root == null)
                _root = transform as RectTransform;
            if (_root == null) return;

            _backdrop = _backdrop != null ? _backdrop : FindNamed<Image>(_root, "Backdrop");
            _bannerSpine = _bannerSpine != null ? _bannerSpine : FindNamed<SpineLite.SpineSprite>(_root, "Spine");
            _bannerRoot = _bannerRoot != null ? _bannerRoot : FindNamedObject(_root, "ClearLevelBanner");
            _bannerTextRoot = _bannerTextRoot != null ? _bannerTextRoot : FindNamed<RectTransform>(_root, "ClearText");
            if (_bannerTextRoot != null)
            {
                _bannerOutline = _bannerOutline != null
                    ? _bannerOutline
                    : _bannerTextRoot.GetComponent<CurvedText>();
                _bannerFace = _bannerFace != null
                    ? _bannerFace
                    : FindNamed<CurvedText>(_bannerTextRoot, "TitleFace");
            }
            _photoWall = _photoWall != null ? _photoWall : FindNamed<RectTransform>(_root, "PhotoWall");
            DisablePhotoWall();
            _nextButton = _nextButton != null ? _nextButton : FindNamed<CommonButton>(_root, "NextButton");
            if ((_confetti == null || _confetti.Length == 0))
                _confetti = _root.GetComponentsInChildren<ParticleSystem>(true);
            ConfigureConfetti();

            if (_bannerTextRoot != null)
            {
                _bannerTextCg = _bannerTextCg != null
                    ? _bannerTextCg
                    : _bannerTextRoot.GetComponent<CanvasGroup>();
                if (_bannerTextCg == null)
                    _bannerTextCg = _bannerTextRoot.gameObject.AddComponent<CanvasGroup>();
                BuildBannerChars(Localization.Tr("clear_level"));
            }

            if (_bannerSpine != null)
            {
                if (_bannerSpine.Data == null)
                    _bannerSpine.Load("complete_title");
                _bannerSpine.SortingOrder = 1520;
                _bannerSpine.AnimationCompleted -= OnBannerAnimationCompleted;
                _bannerSpine.AnimationCompleted += OnBannerAnimationCompleted;
            }
            if (_nextButton != null)
            {
                _nextButton.BindPrefabRuntime();
                _nextButton.OnClick = OnNextPressed;
            }
        }

        void OnBannerAnimationCompleted(SpineLite.TrackEntry entry)
        {
            if (entry.Animation?.Name != "in") return;
            _bannerSpine.SetAnimation("idle", true, 0f);
            _introFinished = true;
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

        static GameObject FindNamedObject(Transform parent, string objectName)
        {
            if (parent == null) return null;
            var transforms = parent.GetComponentsInChildren<Transform>(true);
            foreach (var child in transforms)
                if (child.gameObject.name == objectName)
                    return child.gameObject;
            return null;
        }

        /// <summary>
        /// curve_label.gd port. CurvedText bends TMP's complete shaped mesh,
        /// so glyph advances, kerning, ligatures and best-fit sizing remain
        /// intact for every localized clear-level string.
        /// </summary>
        void BuildBannerChars(string text)
        {
            _bannerTextCg = _bannerTextRoot.GetComponent<CanvasGroup>();
            if (_bannerTextCg == null) _bannerTextCg = _bannerTextRoot.gameObject.AddComponent<CanvasGroup>();
            var textCanvas = _bannerTextRoot.GetComponent<Canvas>();
            if (textCanvas == null)
                textCanvas = _bannerTextRoot.gameObject.AddComponent<Canvas>();
            textCanvas.overrideSorting = true;
            textCanvas.sortingOrder = 1530;

            _bannerTextRoot.sizeDelta = new Vector2(660f, 180f);

            if (_bannerFace == null)
            {
                Debug.LogError(
                    "CompletePanel title requires the prefab-authored TitleFace layer.", this);
                return;
            }

            _bannerFace.font = AssetLib.UiFont;
            _bannerFace.fontStyle = FontStyles.Normal;
            _bannerFace.fontWeight = FontWeight.Regular;
            int fontSize = FitBannerFont(_bannerFace, text, 104, 660f);
            Color outlineColor = new Color32(0x8C, 0x45, 0x0A, 0xFF);

            // Godot's CurveLabel draws a single white glyph pass with a brown
            // 24px outline. A second solid-brown glyph underneath leaks through
            // TMP's antialiased edge and makes the face look beige, so retain the
            // authored component only as an inactive compatibility reference.
            if (_bannerOutline != null)
                _bannerOutline.enabled = false;
            ConfigureBannerLayer(_bannerFace, text, fontSize, Color.white);
            TmpTextStyle.ApplyOutline(_bannerFace, outlineColor, 24f);
            TmpTextStyle.ApplyStandardTitleFace(_bannerFace);
        }

        static void ConfigureBannerLayer(
            CurvedText layer,
            string text,
            int fontSize,
            Color color)
        {
            layer.raycastTarget = false;
            layer.font = AssetLib.UiFont;
            // Godot's weight 700 is between TMP's Regular and its very heavy
            // synthetic Bold. The dedicated clear-title material supplies the
            // intermediate SDF weight while this stays on the normal pass.
            layer.fontStyle = FontStyles.Normal;
            layer.fontWeight = FontWeight.Regular;
            layer.fontSize = fontSize;
            layer.alignment = TextAlignmentOptions.Center;
            layer.enableWordWrapping = false;
            layer.overflowMode = TextOverflowModes.Overflow;
            layer.enableAutoSizing = false;
            layer.richText = false;
            layer.enableKerning = true;
            layer.color = color;
            layer.Amplitude = 22f;
            layer.AlignToTangent = true;
            layer.text = text;
        }

        static int FitBannerFont(
            TMP_Text measurementText,
            string text,
            int maximum,
            float availableWidth)
        {
            if (string.IsNullOrEmpty(text) || AssetLib.UiFont == null)
                return maximum;

            int size = maximum;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                measurementText.fontSize = size;
                float width = measurementText.GetPreferredValues(
                    text, 10000f, 180f).x;
                if (width <= availableWidth + 0.5f)
                    break;
                size = Mathf.Max(1, Mathf.FloorToInt(size * availableWidth / width));
            }
            return size;
        }

        void BuildConfetti()
        {
            _confetti = new ParticleSystem[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Confetti" + i);
                go.transform.SetParent(App.I.WorldRoot, false);
                go.transform.position = App.DesignToWorld(new Vector2(540, -200));
                var ps = go.AddComponent<ParticleSystem>();
                go.SetActive(false);
                _confetti[i] = ps;
            }
            ConfigureConfetti();
        }

        void ConfigureConfetti()
        {
            if (_confetti == null || _confetti.Length == 0) return;
            var texture = AssetLib.Texture("Art/Sprites/Common/et_ribbon_007");
            var shader = Shader.Find("BubblePics/ParticleTintUv");
            _confettiMaterials = new Material[_confetti.Length];

            for (int i = 0; i < _confetti.Length; i++)
            {
                var ps = _confetti[i];
                if (ps == null) continue;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;

                Material material = renderer.sharedMaterial;
                if (shader != null && (material == null || material.shader != shader))
                {
                    material = new Material(shader) { mainTexture = texture };
                    renderer.sharedMaterial = material;
                }
                if (material != null)
                {
                    material.mainTexture = texture;
                    // Godot's atlas regions are indexed top-left to bottom-right;
                    // Unity texture UVs start at the bottom-left.
                    float x = (i & 1) * 0.5f;
                    float y = i < 2 ? 0.5f : 0f;
                    material.SetVector("_UvRect", new Vector4(x, y, 0.5f, 0.5f));
                    material.SetColor("_TintColor", new Color(1f, 1f, 1f, 0f));
                }
                _confettiMaterials[i] = material;
                renderer.sortingOrder = 1530;

                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;
                main.loop = true;
                main.startLifetime = 2.5f;
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(32f, 128f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                main.gravityModifier = 190f / 9.81f;
                main.maxParticles = 8;

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 8f / 2.5f;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                float viewWidth = DeviceLayout.ViewWidth > 0f
                    ? DeviceLayout.ViewWidth
                    : App.DesignW;
                shape.scale = new Vector3(viewWidth, 60f, 0.01f);

                // Direction (0, 1), spread 30 degrees in Godot's y-down
                // coordinates becomes negative world-y with a proportional
                // horizontal cone in Unity.
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = false;
                velocity.space = ParticleSystemSimulationSpace.World;
                float spreadX = Mathf.Tan(30f * Mathf.Deg2Rad) * 22.5f;
                velocity.x = new ParticleSystem.MinMaxCurve(-spreadX, spreadX);
                velocity.y = new ParticleSystem.MinMaxCurve(-55f, -22.5f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.enabled = true;

                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(
                    -240f * Mathf.Deg2Rad,
                    240f * Mathf.Deg2Rad);

                var damping = ps.limitVelocityOverLifetime;
                damping.enabled = true;
                damping.space = ParticleSystemSimulationSpace.World;
                damping.limit = 10000f;
                damping.dampen = 0f;
                damping.drag = new ParticleSystem.MinMaxCurve(0.4f, 1.4f);

                var atlas = ps.textureSheetAnimation;
                atlas.enabled = false;

                var color = ps.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(Color.white, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.08f),
                        new GradientAlphaKey(1f, 0.8f),
                        new GradientAlphaKey(0f, 1f)
                    });
                color.color = gradient;
            }
        }

        public void PlayAppear(Texture2D[] textures, int levelNumber, bool isHard = false)
        {
            if (_root == null) BindPrefabRuntime();
            if (_root == null) return;
            Visible = true;
            _root.gameObject.SetActive(true);
            _introFinished = false;

            AlignWorldFxToViewport();

            // backdrop fade (transparent black base — opacity ramp only)
            _backdrop.color = new Color(0, 0, 0, 0);

            // The yellow complete-title ribbon has been removed. Retain the
            // optional references only so older prefabs deserialize safely.
            if (_bannerRoot != null)
                _bannerRoot.SetActive(false);
            if (_bannerTextRoot != null)
                _bannerTextRoot.gameObject.SetActive(false);
            if (_bannerTextCg != null)
            {
                _bannerTextCg.alpha = 0f;
            }
            _introFinished = true;

            // The parameter remains for call-site compatibility; settlement
            // no longer displays the collected images.
            _ = textures;
            DisablePhotoWall();

            // next button
            int nextLevel = levelNumber + 1;
            bool endReached = !LevelRepo.HasLevel(nextLevel);
            _nextButton.SetText(endReached
                ? Localization.Tr("home")
                : Localization.Tr("level_d").Replace("%d", nextLevel.ToString()));
            _nextButton.SetVariant(endReached
                ? CommonButton.Variant.Green
                : LevelRepo.Get(nextLevel)?.difficulty_type == "Hard"
                    ? CommonButton.Variant.Red
                    : CommonButton.Variant.Green);
            _nextButton.SetInteractable(false);
            _nextButton.CanvasGroup.alpha = 0f;

            StartCoroutine(PhaseCo());
            if (_root.gameObject.activeInHierarchy &&
                InAppReviewService.NotifyLevelCompleted(levelNumber))
            {
                FunSmithTelemetry.TrackRatePromptOpen(levelNumber);
            }
        }

        void DisablePhotoWall()
        {
            if (_scrollCo != null)
            {
                StopCoroutine(_scrollCo);
                _scrollCo = null;
            }
            if (_photoWallCanvasGroup != null)
                _photoWallCanvasGroup.alpha = 0f;
            if (_photoWall != null)
                _photoWall.gameObject.SetActive(false);
        }

        // Retained only as a prefab-migration helper. Runtime settlement no
        // longer calls this legacy photo-wall builder.
        void BuildWall(Texture2D[] textures)
        {
            if (_scrollCo != null)
            {
                StopCoroutine(_scrollCo);
                _scrollCo = null;
            }
            foreach (Transform c in _topRow) Destroy(c.gameObject);
            foreach (Transform c in _bottomRow) Destroy(c.gameObject);

            _topRow.anchoredPosition = new Vector2(0f, -60f);
            _bottomRow.anchoredPosition = new Vector2(0f, -620f);

            var imgs = new List<Texture2D>();
            if (textures != null)
                foreach (var texture in textures)
                    if (texture != null)
                        imgs.Add(texture);
            if (imgs.Count == 0)
            {
                _photoWall.gameObject.SetActive(false);
                return;
            }
            _photoWall.gameObject.SetActive(true);

            int half = Mathf.CeilToInt(imgs.Count / 2f);
            var top = imgs.GetRange(0, Mathf.Min(half, imgs.Count));
            var bottom = imgs.Count > half ? imgs.GetRange(half, imgs.Count - half) : new List<Texture2D>(top);

            FillRow(_topRow, top);
            FillRow(_bottomRow, bottom);

            float stride = CARD_SIZE + CARD_GAP;
            _topRow.anchoredPosition = new Vector2(-top.Count * stride, -60f);
            _bottomRow.anchoredPosition = new Vector2(0f, -620f);

            if (_photoWallCanvasGroup == null)
            {
                _photoWallCanvasGroup =
                    _photoWall.GetComponent<CanvasGroup>();
                if (_photoWallCanvasGroup == null)
                {
                    _photoWallCanvasGroup =
                        _photoWall.gameObject.AddComponent<CanvasGroup>();
                }
            }
            if (_photoWallCanvasGroup != null)
            {
                _photoWallCanvasGroup.alpha = 0f;
                _scrollCo = StartCoroutine(RevealAndScrollWallCo(
                    top.Count,
                    bottom.Count,
                    _photoWallCanvasGroup));
            }
        }

        void FillRow(RectTransform row, List<Texture2D> texs)
        {
            float stride = CARD_SIZE + CARD_GAP;
            float setW = texs.Count * stride;
            int copies = Mathf.CeilToInt((1080f + setW) / Mathf.Max(setW, 1)) + 1;
            int idx = 0;
            for (int c = 0; c < copies; c++)
            {
                for (int i = 0; i < texs.Count; i++)
                {
                    var card = MakeCard(row, texs[i]);
                    card.anchoredPosition = new Vector2(idx * stride, 0);
                    idx++;
                }
            }
        }

        RectTransform MakeCard(RectTransform row, Texture2D tex)
        {
            var rt = UiFactory.Node(row, "Card");
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(CARD_SIZE, CARD_SIZE);

            // StyleBoxFlat parity: 40px corners, #FDF6E8 fill, 3px
            // #CAA86E border and a soft 5px shadow offset 5px downward.
            // Five concentric low-alpha layers approximate the original
            // StyleBox shadow blur while keeping the photo cards prefab-safe.
            for (int spread = 5; spread >= 1; spread--)
            {
                float alpha = FrameShadowColor.a * (6 - spread) / 15f;
                AddRoundedLayer(rt, "Shadow" + spread,
                    CARD_SIZE + spread * 2f,
                    new Vector2(0f, -5f),
                    new Color(FrameShadowColor.r, FrameShadowColor.g, FrameShadowColor.b, alpha));
            }
            AddRoundedLayer(rt, "GoldBorder", CARD_SIZE, Vector2.zero, FrameGoldColor);
            AddRoundedLayer(rt, "FrameFill",
                CARD_SIZE - FRAME_GOLD_WIDTH * 2f,
                Vector2.zero,
                FrameFillColor);

            var photoGo = new GameObject("Photo");
            photoGo.transform.SetParent(rt, false);
            var photo = photoGo.AddComponent<RawImage>();
            photo.texture = tex;
            photo.uvRect = KeepAspectCoveredUv(tex, Vector2.one);
            photo.raycastTarget = false;
            var prt = photo.rectTransform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(PHOTO_INSET, PHOTO_INSET);
            prt.offsetMax = new Vector2(-PHOTO_INSET, -PHOTO_INSET);
            var mat = new Material(Shader.Find("BubblePics/FragmentImage"));
            mat.SetFloat("_CornerRadius", PHOTO_CORNER / (CARD_SIZE - PHOTO_INSET * 2f));
            mat.SetFloat("_CornerMask", 15);
            mat.SetFloat("_FillCornerMask", 0);
            mat.SetFloat("_EdgeMask", 15);
            Rect uv = photo.uvRect;
            mat.SetVector("_UvRegion", new Vector4(uv.x, uv.y, uv.width, uv.height));
            photo.material = mat;
            return rt;
        }

        static Image AddRoundedLayer(RectTransform parent, string objectName, float size, Vector2 offset, Color color)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = PhotoFrameSprite();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = offset;
            return image;
        }

        static Sprite PhotoFrameSprite()
        {
            if (_photoFrameSprite != null) return _photoFrameSprite;
            var texture = TopGameBar.RoundedRectTex(480, 480, 40, Color.white);
            texture.name = "PhotoWallRoundedFrame";
            _photoFrameSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect);
            _photoFrameSprite.name = "PhotoWallRoundedFrame";
            return _photoFrameSprite;
        }

        static Rect KeepAspectCoveredUv(Texture texture, Vector2 viewport)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0 ||
                viewport.x <= 0f || viewport.y <= 0f)
                return new Rect(0f, 0f, 1f, 1f);

            float sourceAspect = texture.width / (float)texture.height;
            float viewportAspect = viewport.x / viewport.y;
            if (sourceAspect > viewportAspect)
            {
                float width = viewportAspect / sourceAspect;
                return new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }

            float height = sourceAspect / viewportAspect;
            return new Rect(0f, (1f - height) * 0.5f, 1f, height);
        }

        IEnumerator RevealAndScrollWallCo(
            int topCount,
            int bottomCount,
            CanvasGroup wallGroup)
        {
            float strideTop = topCount * (CARD_SIZE + CARD_GAP);
            float strideBottom = bottomCount * (CARD_SIZE + CARD_GAP);
            float xTop = -strideTop, xBottom = 0;

            // photo_wall.gd starts scrolling only after its 0.5s fade-in.
            float reveal = 0f;
            while (reveal < 0.5f)
            {
                reveal += Time.deltaTime;
                if (wallGroup != null)
                    wallGroup.alpha = Mathf.Clamp01(reveal / 0.5f);
                _topRow.anchoredPosition = new Vector2(xTop, -60f);
                _bottomRow.anchoredPosition = new Vector2(xBottom, -620f);
                yield return null;
            }
            if (wallGroup != null)
                wallGroup.alpha = 1f;

            while (true)
            {
                xTop += SCROLL_SPEED * Time.deltaTime;
                xBottom -= SCROLL_SPEED * Time.deltaTime;
                if (strideTop > 0 && xTop > 0) xTop -= strideTop;
                if (strideBottom > 0 && xBottom < -strideBottom) xBottom += strideBottom;
                _topRow.anchoredPosition = new Vector2(xTop, -60);
                _bottomRow.anchoredPosition = new Vector2(xBottom, -620);
                yield return null;
            }
        }

        void AlignWorldFxToViewport()
        {
            float viewWidth = DeviceLayout.ViewWidth > 0f
                ? DeviceLayout.ViewWidth
                : App.DesignW;
            Vector2 horizontalCenter = new Vector2(viewWidth * 0.5f, BANNER_Y);
            if (_bannerRoot != null)
                _bannerRoot.transform.position = App.DesignToWorld(horizontalCenter);

            if (_confetti == null) return;
            Vector3 emitterPosition = App.DesignToWorld(
                new Vector2(viewWidth * 0.5f, -200f));
            foreach (var ps in _confetti)
            {
                if (ps == null) continue;
                ps.transform.position = emitterPosition;
                var shape = ps.shape;
                shape.scale = new Vector3(viewWidth, 60f, 0.01f);
            }
        }

        IEnumerator PhaseCo()
        {
            float t = 0;
            while (!_introFinished && t < INTRO_TIMEOUT)
            {
                t += Time.deltaTime;
                yield return null;
            }
            // phase 2: confetti + next fade
            foreach (var ps in _confetti ?? System.Array.Empty<ParticleSystem>())
            {
                if (ps == null) continue;
                ps.gameObject.SetActive(true);
                ps.Clear(true);
                ps.Play();
            }
            SetConfettiAlpha(0f);
            if (_confettiFadeCo != null) StopCoroutine(_confettiFadeCo);
            _confettiFadeCo = StartCoroutine(FadeConfettiInCo());
            float f = 0;
            while (f < NEXT_FADE_DUR)
            {
                f += Time.deltaTime;
                _nextButton.CanvasGroup.alpha = Mathf.Clamp01(f / NEXT_FADE_DUR);
                yield return null;
            }
            _nextButton.SetInteractable(true);
        }

        IEnumerator FadeConfettiInCo()
        {
            float elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.deltaTime;
                SetConfettiAlpha(Mathf.Clamp01(elapsed / 0.4f));
                yield return null;
            }
            SetConfettiAlpha(1f);
            _confettiFadeCo = null;
        }

        void SetConfettiAlpha(float alpha)
        {
            if (_confettiMaterials == null) return;
            for (int i = 0; i < _confettiMaterials.Length; i++)
            {
                var material = _confettiMaterials[i];
                if (material != null && material.HasProperty("_TintColor"))
                    material.SetColor("_TintColor", new Color(1f, 1f, 1f, alpha));
            }
        }

        void OnNextPressed()
        {
            if (Page != null) Page.OnCompleteNext();
        }

        public void HidePanel()
        {
            Visible = false;
            DisablePhotoWall();
            if (_confettiFadeCo != null)
            {
                StopCoroutine(_confettiFadeCo);
                _confettiFadeCo = null;
            }
            if (_bannerRoot != null) _bannerRoot.SetActive(false);
            if (_confetti != null)
                foreach (var ps in _confetti)
                    if (ps != null) { ps.Stop(); ps.gameObject.SetActive(false); }
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_bannerSpine != null)
                _bannerSpine.AnimationCompleted -= OnBannerAnimationCompleted;
            if (_confettiMaterials != null)
            {
                foreach (var material in _confettiMaterials)
                    if (material != null && material.shader != null &&
                        material.shader.name == "BubblePics/ParticleTintUv")
                        Destroy(material);
            }
        }
    }

    /// <summary>Port of common_button prefab: 706x230 ninepatch face + layered label.</summary>
    public class CommonButton : MonoBehaviour
    {
        public enum Variant { Green, Blue, Orange, Red }

        [SerializeField] RectTransform _root;
        [SerializeField] CanvasGroup _canvasGroup;
        [SerializeField] Image _face;
        [SerializeField] TMP_Text _label;
        [SerializeField] TMP_Text _labelBack;
        [SerializeField] PressButton _press;
        [SerializeField] bool _interactable = true;
        readonly Dictionary<Graphic, Material> _enabledMaterials =
            new Dictionary<Graphic, Material>();
        readonly Dictionary<TMP_Text, Color> _enabledTextColors =
            new Dictionary<TMP_Text, Color>();
        static Material _disabledGrayMaterial;

        public RectTransform Root => _root;
        public CanvasGroup CanvasGroup => _canvasGroup;
        public System.Action OnClick;

        static readonly Color[] BackColors =
        {
            new Color(0.694f, 0.929f, 0.69f),
            new Color(0.714f, 0.843f, 0.969f),
            new Color(0.973f, 0.859f, 0.671f),
            new Color(0.714f, 0.843f, 0.969f),
        };

        static readonly Color[] OutlineColors =
        {
            new Color(0f, 0.204f, 0.067f, 0.6f),
            new Color(0f, 0.192f, 0.361f, 0.6f),
            new Color(0.396f, 0.173f, 0f, 0.6f),
            new Color(0.6f, 0.13f, 0.13f, 0.6f),
        };

        static readonly string[] FaceTex =
        {
            "Art/Sprites/Common/btn_green_front",
            "Art/Sprites/Common/btn_blue_front",
            "Art/Sprites/Common/btn_orange_front",
            "Art/Sprites/Common/btn_red_front",
        };

        public static CommonButton Create(Transform parent, string name, Variant variant, string text)
        {
            var catalog = PrefabCatalog.Current;
            if (Application.isPlaying && catalog != null && catalog.CommonButton != null)
            {
                var prefabButton = PrefabCatalog.InstantiateComponent<CommonButton>(catalog.CommonButton, parent);
                if (prefabButton != null)
                {
                    prefabButton.gameObject.name = name;
                    prefabButton.InitializePrefabRuntime(parent, variant, text);
                    return prefabButton;
                }
            }

            var rt = UiFactory.Node(parent, name);
            rt.sizeDelta = new Vector2(706, 230);
            var btn = rt.gameObject.AddComponent<CommonButtonPrefab>();
            btn._root = rt;
            btn._canvasGroup = rt.gameObject.AddComponent<CanvasGroup>();

            var face = UiFactory.Img(rt, "Face", FaceTex[(int)variant], 0, 0);
            btn._face = face;
            var frt = face.rectTransform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
            // godot common_button.tscn: 240x240 texture, patch margins 96/90/96/106
            face.sprite = AssetLib.Sprite9Design(
                FaceTex[(int)variant], 96, 90, 96, 106);
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = 1f;
            face.raycastTarget = true;

            btn._labelBack = UiFactory.Label(rt, "LabelBack", text, 102, BackColors[(int)variant], AssetLib.UiFont, TextAnchor.MiddleCenter, 700, 220);
            // Godot offsets the coloured outline layer three design pixels below
            // the white face: LabelBack y=5 and Label y=8 in Unity coordinates.
            btn._labelBack.rectTransform.anchoredPosition = new Vector2(0, 5);
            TmpTextStyle.ApplyOutline(
                btn._labelBack, OutlineColors[(int)variant], 32f);
            btn._label = UiFactory.Label(rt, "Label", text, 102, Color.white, AssetLib.UiFont, TextAnchor.MiddleCenter, 700, 220);
            btn._label.rectTransform.anchoredPosition = new Vector2(0, 8);
            TmpTextStyle.ClearEffects(btn._label);

            UiFactory.MakeButton(face.gameObject, () => btn.OnClick?.Invoke());
            btn._press = face.GetComponent<PressButton>();
            return btn;
        }

        /// <summary>
        /// Applies the recovered common-button face and two-layer label to a
        /// regular UGUI Button. Restored 1.0.9 pages were originally authored
        /// before the reusable Unity prefab existed, so they keep their Button
        /// component while sharing the exact Godot visual treatment here.
        /// </summary>
        public static TMP_Text StyleExisting(
            Button button,
            Variant variant,
            TMP_Text label)
        {
            if (button == null || button.image == null) return null;
            int index = Mathf.Clamp((int)variant, 0, FaceTex.Length - 1);
            button.image.sprite = AssetLib.Sprite9Design(
                FaceTex[index],
                96f,
                90f,
                96f,
                106f);
            button.image.type = Image.Type.Sliced;
            button.image.preserveAspect = false;
            button.image.pixelsPerUnitMultiplier = 1f;
            button.image.color = Color.white;

            if (label == null)
                label = FindNamed<TMP_Text>(button.transform, "Label");
            if (label == null) return null;

            TMP_Text labelBack = FindNamed<TMP_Text>(button.transform, "LabelBack");
            if (labelBack == null)
            {
                GameObject duplicate = UnityEngine.Object.Instantiate(
                    label.gameObject,
                    button.transform,
                    false);
                duplicate.name = "LabelBack";
                labelBack = duplicate.GetComponent<TMP_Text>();
            }

            ConfigureLayer(labelBack, new Vector2(0f, 5f));
            labelBack.color = BackColors[index];
            TmpTextStyle.ApplyOutline(
                labelBack,
                OutlineColors[index],
                32f);
            labelBack.transform.SetAsFirstSibling();

            ConfigureLayer(label, new Vector2(0f, 8f));
            label.color = Color.white;
            TmpTextStyle.ClearEffects(label);
            label.transform.SetAsLastSibling();
            return labelBack;
        }

        static void ConfigureLayer(TMP_Text layer, Vector2 position)
        {
            if (layer == null) return;
            RectTransform rect = layer.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(700f, 220f);
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
            layer.font = AssetLib.UiFont;
            layer.fontSize = 102f;
            layer.enableAutoSizing = true;
            layer.fontSizeMin = 40f;
            layer.fontSizeMax = 102f;
            layer.alignment = TextAlignmentOptions.Center;
            layer.enableWordWrapping = false;
            layer.raycastTarget = false;
        }

        /// <summary>Initializes a persistent CommonButton prefab without rebuilding its hierarchy.</summary>
        public void InitializePrefabRuntime(Transform parent, Variant variant, string text)
        {
            // Authoring wraps reusable controls so the component can live one
            // level below the prefab root. Preserve that wrapper when it is
            // already mounted below the requested parent.
            if (parent != null && transform != parent && !transform.IsChildOf(parent))
                transform.SetParent(parent, false);
            BindPrefabRuntime();
            ApplyVariant(variant);
            SetText(text);
        }

        public void BindPrefabRuntime()
        {
            if (_root == null)
                _root = transform as RectTransform;
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (_face == null)
                _face = FindNamed<Image>(transform, "Face");
            if (_label == null)
                _label = FindNamed<TMP_Text>(transform, "Label");
            if (_labelBack == null)
                _labelBack = FindNamed<TMP_Text>(transform, "LabelBack");
            if (_press == null && _face != null)
                _press = _face.GetComponent<PressButton>();
            if (_press == null && _face != null)
                _press = _face.gameObject.AddComponent<PressButtonPrefab>();
            if (_face != null)
                _face.raycastTarget = true;
            if (_press != null)
            {
                _press.OnClick = InvokeClick;
                _interactable = _press.Interactable;
            }
            ApplyDisabledGray(!_interactable);
        }

        void ApplyVariant(Variant variant)
        {
            int index = Mathf.Clamp((int)variant, 0, FaceTex.Length - 1);
            if (_face != null)
            {
                _face.sprite = AssetLib.Sprite9Design(
                    FaceTex[index], 96, 90, 96, 106);
                _face.type = Image.Type.Sliced;
                _face.pixelsPerUnitMultiplier = 1f;
            }
            if (_labelBack != null)
            {
                _labelBack.color = BackColors[index];
                TmpTextStyle.ApplyOutline(
                    _labelBack, OutlineColors[index], 32f);
            }
        }

        void InvokeClick()
        {
            OnClick?.Invoke();
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

        public void SetText(string t)
        {
            if (_label != null) _label.text = t;
            if (_labelBack != null) _labelBack.text = t;
        }

        public void SetVariant(Variant variant)
        {
            ApplyVariant(variant);
        }

        public void SetInteractable(bool on)
        {
            _interactable = on;
            if (_press != null) _press.Interactable = on;
            ApplyDisabledGray(!on);
        }

        public PressButton Press => _press;

        void ApplyDisabledGray(bool disabled)
        {
            var graphics = GetComponentsInChildren<Graphic>(true);
            if (disabled)
            {
                Material gray = DisabledGrayMaterial();
                if (gray == null) return;
                foreach (var graphic in graphics)
                {
                    if (graphic == null) continue;
                    if (graphic is TMP_Text tmpText)
                    {
                        if (!_enabledTextColors.ContainsKey(tmpText))
                            _enabledTextColors.Add(tmpText, tmpText.color);
                        Color color = tmpText.color;
                        tmpText.color = new Color(0.72f, 0.72f, 0.72f, color.a);
                        continue;
                    }
                    if (!_enabledMaterials.ContainsKey(graphic))
                        _enabledMaterials.Add(graphic, graphic.material);
                    graphic.material = gray;
                    graphic.SetMaterialDirty();
                }
                return;
            }

            foreach (var pair in _enabledMaterials)
            {
                if (pair.Key == null) continue;
                pair.Key.material = pair.Value;
                pair.Key.SetMaterialDirty();
            }
            _enabledMaterials.Clear();
            foreach (var pair in _enabledTextColors)
            {
                if (pair.Key != null)
                    pair.Key.color = pair.Value;
            }
            _enabledTextColors.Clear();
        }

        static Material DisabledGrayMaterial()
        {
            if (_disabledGrayMaterial != null) return _disabledGrayMaterial;
            var shader = Shader.Find("BubblePics/DisabledGray");
            if (shader == null)
            {
                Debug.LogWarning("DisabledGray shader is missing; CommonButton cannot apply its disabled visual.");
                return null;
            }
            _disabledGrayMaterial = new Material(shader)
            {
                name = "CommonButton Disabled Gray",
                hideFlags = HideFlags.HideAndDontSave
            };
            _disabledGrayMaterial.SetFloat("_Saturation", 0f);
            _disabledGrayMaterial.SetFloat("_Lift", 0.6f);
            _disabledGrayMaterial.SetFloat("_TargetGray", 0.72f);
            return _disabledGrayMaterial;
        }
    }

    /// <summary>Simple Godot-style toast.</summary>
    public class Toast : MonoBehaviour
    {
        const float MIN_WIDTH = 320f;
        const float MIN_HEIGHT = 216f;
        const float HORIZONTAL_PADDING = 56f;
        const float VERTICAL_PADDING = 40f;
        const float SAFE_MARGIN = 60f;
        const float BASE_Y = -720f;
        const float FLOAT_DISTANCE = 80f;

        static Toast _current;

        [SerializeField] RectTransform _root;
        [SerializeField] Image _background;
        [SerializeField] TMP_Text _label;
        [SerializeField] CanvasGroup _canvasGroup;
        GameObject _lifetimeRoot;

        public static void Show(string msg)
        {
            if (_current != null)
                _current.CancelAndDestroy();

            var parent = App.I.DialogRoot;
            Toast toast = null;
            var catalog = PrefabCatalog.Current;
            if (catalog != null && catalog.Toast != null)
                toast = PrefabCatalog.InstantiateComponent<Toast>(catalog.Toast, parent);

            if (toast == null)
            {
                var fallbackRoot = UiFactory.Node(parent, "Toast");
                toast = fallbackRoot.gameObject.AddComponent<ToastPrefab>();
                toast.BuildFallback(parent);
            }
            else
            {
                toast.gameObject.name = "Toast";
                toast.InitializePrefabRuntime(parent);
            }
            _current = toast;
            toast.Present(msg);
        }

        /// <summary>Authoring/fallback entry retained for the prefab generator.</summary>
        public void Build(Transform parent)
        {
            if (_root != null || _background != null || _label != null || _canvasGroup != null)
            {
                InitializePrefabRuntime(parent);
                return;
            }
            if (parent != null && transform != parent && !transform.IsChildOf(parent))
                transform.SetParent(parent, false);
            _root = transform as RectTransform;
            if (_root == null && parent != null)
                _root = UiFactory.Node(parent, "ToastView");
            BuildFallback(parent);
            if (_root != null) _root.gameObject.SetActive(false);
        }

        public void InitializePrefabRuntime(Transform parent)
        {
            if (parent != null && transform != parent && !transform.IsChildOf(parent))
                transform.SetParent(parent, false);
            var instanceRoot = transform;
            if (parent != null)
                while (instanceRoot.parent != null && instanceRoot.parent != parent)
                    instanceRoot = instanceRoot.parent;
            _lifetimeRoot = instanceRoot.gameObject;
            BindPrefabRuntime();
        }

        public void BindPrefabRuntime()
        {
            if (_root == null)
                _root = transform as RectTransform;
            if (_root == null) return;
            if (_background == null)
                _background = _root.GetComponent<Image>();
            if (_label == null)
                _label = FindNamed<TMP_Text>(_root, "Msg");
            if (_canvasGroup == null)
                _canvasGroup = _root.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _root.gameObject.AddComponent<CanvasGroup>();
        }

        void BuildFallback(Transform parent)
        {
            if (_root == null)
                _root = transform as RectTransform;
            if (_root == null) return;
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0, -720);
            if (_background == null)
                _background = _root.GetComponent<Image>();
            if (_background == null)
                _background = _root.gameObject.AddComponent<Image>();
            // godot toast.tscn patch margins 70/87/70/70
            _background.sprite = AssetLib.Sprite9("Art/Sprites/Common/common_toast_bg_9s", 70, 87, 70, 70);
            _background.type = Image.Type.Sliced;
            _background.raycastTarget = false;
            if (_label == null)
                _label = UiFactory.Label(_root, "Msg", "", 50, new Color(0.21f, 0.29f, 0.37f), AssetLib.UiFont, TextAnchor.MiddleCenter, 600, 120);
            if (_canvasGroup == null)
                _canvasGroup = _root.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _root.gameObject.AddComponent<CanvasGroup>();
            _lifetimeRoot = gameObject;
        }

        void Present(string msg)
        {
            BindPrefabRuntime();
            if (_root == null || _label == null || _canvasGroup == null) return;
            gameObject.SetActive(true);
            _root.anchoredPosition = new Vector2(0, BASE_Y);
            _label.text = msg;

            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Overflow;
            float viewportWidth = ResolveViewportWidth();
            float maxWidth = Mathf.Max(1f, viewportWidth - SAFE_MARGIN * 2f);
            float minWidth = Mathf.Min(MIN_WIDTH, maxWidth);
            float wantedWidth = _label.preferredWidth + HORIZONTAL_PADDING * 2f;
            float width = Mathf.Clamp(wantedWidth, minWidth, maxWidth);
            bool needsWrap = wantedWidth > maxWidth;

            _root.sizeDelta = new Vector2(width, MIN_HEIGHT);
            var labelRect = _label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(HORIZONTAL_PADDING, VERTICAL_PADDING);
            labelRect.offsetMax = new Vector2(-HORIZONTAL_PADDING, -VERTICAL_PADDING);
            _label.enableWordWrapping = needsWrap;

            Canvas.ForceUpdateCanvases();
            float height = Mathf.Max(
                MIN_HEIGHT,
                _label.preferredHeight + VERTICAL_PADDING * 2f);
            _root.sizeDelta = new Vector2(width, height);
            _canvasGroup.alpha = 0;
            TweenRunner.Go(ToastCo());
        }

        System.Collections.IEnumerator ToastCo()
        {
            float t = 0;
            while (t < 2.0f)
            {
                t += Time.deltaTime;
                if (_root == null) yield break;
                float k = Tween.Evaluate(Ease.OutQuad, Mathf.Clamp01(t / 2f));
                _root.anchoredPosition = new Vector2(0, BASE_Y + FLOAT_DISTANCE * k);
                if (t < 0.2f) _canvasGroup.alpha = t / 0.2f;
                else if (t > 1.8f) _canvasGroup.alpha = 1f - (t - 1.8f) / 0.2f;
                else _canvasGroup.alpha = 1;
                yield return null;
            }
            Object.Destroy(_lifetimeRoot != null ? _lifetimeRoot : gameObject);
        }

        float ResolveViewportWidth()
        {
            var canvas = _root != null ? _root.GetComponentInParent<Canvas>() : null;
            var canvasRect = canvas != null && canvas.rootCanvas != null
                ? canvas.rootCanvas.transform as RectTransform
                : null;
            if (canvasRect != null && canvasRect.rect.width > 0f)
                return canvasRect.rect.width;

            var parentRect = _root != null ? _root.parent as RectTransform : null;
            if (parentRect != null && parentRect.rect.width > 0f)
                return parentRect.rect.width;
            return App.DesignW;
        }

        void CancelAndDestroy()
        {
            var target = _lifetimeRoot != null ? _lifetimeRoot : gameObject;
            if (target != null)
            {
                target.SetActive(false);
                Object.Destroy(target);
            }
        }

        void OnDestroy()
        {
            if (_current == this)
                _current = null;
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
    }
}
