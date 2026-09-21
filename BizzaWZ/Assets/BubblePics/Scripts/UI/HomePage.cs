using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Port of home_page.tscn/gd — homepage background, page_logo spine,
    /// settings button (top-left), coin pill (top-right), play button.
    /// </summary>
    public class HomePage : MonoBehaviour
    {
        const int LogoSortingOrder = 950;
        const string SkinRoot = "Art/Sprites/PsdSkin20260807";
        const float CoinHudRightMargin = 42f;
        const float CoinHudTextLeft = 100f;
        const float CoinHudTextRightPadding = 15f;
        const float CoinHudWidth = 243f;

        [Header("Prefab references")]
        [SerializeField] RectTransform _root;
        [SerializeField] CanvasGroup _cg;
        [SerializeField] Image _entryCarryMask;
        [SerializeField] RectTransform _raysRoot;
        [SerializeField] RawImage _waterRipple;
        [SerializeField] RectTransform _risingBubbles;
        [SerializeField] Transform _worldRoot;
        [SerializeField] SpineLite.SpineSprite _logo;
        [SerializeField] TMP_Text _coinLabel;
        [SerializeField] CommonButton _playBtn;
        [SerializeField] TMP_Text _playLabel;
        [SerializeField] TMP_Text _playLabelBack;
        [SerializeField] PressButton _playPress;
        [SerializeField] PressButton _settingsPress;
        [SerializeField] SettingPage _settings;

        const string DecoSpritePath = "Art/Sprites/Home/deco_bubble";
        const int DecoCount = 15;
        const float DecoMargin = 36f;
        const float DecoBandWidth = 180f;
        const float DecoTopY = -130f;
        const float DecoBottomY = 2150f;
        const float DecoSpeedMin = 20f;
        const float DecoSpeedMax = 72f;
        const float DecoAcceleration = 5f;
        const float DecoSizeMin = 0.065f;
        const float DecoSizeMax = 0.358f;
        const float DecoWiggleFrequency = 0.22f;
        const float DecoWiggleAmplitudeMin = 12f;
        const float DecoWiggleAmplitudeMax = 26f;
        const float DecoWiggleChance = 0.5f;
        const float DecoAlpha = 0.72f;
        const float DecoFadeInDistance = 280f;
        const float DecoBurstYMin = 220f;
        const float DecoBurstYMax = 1900f;
        const int DecoSeed = 70234;

        const int FragmentsPerBurst = 5;
        const int FragmentPoolSize = 60;
        const float FragmentSizeMin = 0.0325f;
        const float FragmentSizeMax = 0.0845f;
        const float FragmentLifeMin = 0.6f;
        const float FragmentLifeMax = 1.1f;
        const float FragmentSpeedMin = 40f;
        const float FragmentSpeedMax = 130f;
        const float FragmentRise = -55f;
        const float FragmentDamping = 1.8f;

        const int RayCount = 16;
        const int RaySeed = 20260609;
        const float RayFanDegrees = 33f;
        const float RayAngleJitterDegrees = 3f;
        const float RayWidthMin = 0.22f;
        const float RayWidthMax = 2.3f;
        const float RayLengthMin = 0.9f;
        const float RayLengthMax = 1.9f;
        const float RayAlphaMin = 0.28f;
        const float RayAlphaMax = 0.56f;
        const float RayAlphaAmplitude = 0.08f;
        const float RayBreathPeriod = 6f;
        const float RayAlphaFlicker = 0.03f;
        const float RayFlickerPeriod = 1.6f;
        const float RaySwayDegrees = 0.7f;
        const float RaySwayPeriod = 8f;
        const float RayWidthPulse = 0.06f;
        const float RayWidthPulsePeriod = 7f;
        static readonly Color RayColor = new Color(0.85f, 0.95f, 1f, 1f);

        sealed class DecoBubbleState
        {
            public RectTransform Rect;
            public Image Image;
            public float BaseX;
            public float Speed;
            public float Phase;
            public float BurstY;
            public float Wiggle;
            public float Y;
        }

        sealed class FragmentState
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife = 1f;
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

        readonly List<DecoBubbleState> _decoBubbles = new List<DecoBubbleState>(DecoCount);
        readonly List<FragmentState> _decoFragments = new List<FragmentState>(FragmentPoolSize);
        readonly List<RayState> _rays = new List<RayState>(RayCount);
        readonly List<CanvasGroup> _contentGroups = new List<CanvasGroup>();
        System.Random _decoRandom;
        Material _runtimeWaterRippleMaterial;
        Texture2D _runtimeCarryMaskTexture;
        Sprite _runtimeCarryMaskSprite;
        Coroutine _carryMaskFade;
        Coroutine _playTransition;
        float _decoTime;
        float _rayTime;
        float _contentAlpha = 1f;
        bool _decoPoolBuilt;
        bool _logoIdleInitialized;
        bool _shown;

        public bool IsShown => _shown;

        /// <summary>
        /// Restores runtime-only button delegates and Spine data after a
        /// persistent prefab has been mounted under the shared AppRoot canvas.
        /// </summary>
        public void InitializePrefabRuntime()
        {
            ResolvePrefabReferences();

            if (_settingsPress != null) _settingsPress.OnClick = OnSettingsPressed;
            if (_playPress != null) _playPress.OnClick = OnPlayPressed;

            ApplyDeviceLayout();
            InitializeBackdropVisuals();
            EnsureContentGroups();
            BuildDecoPool();

            // SkeletonData, mesh and animation delegates are runtime data and
            // are intentionally reconstructed instead of serialized in Prefab.
            if (_logo != null)
            {
                if (_logo.Data == null) _logo.Load("page_logo");
                _logo.SortingOrder = LogoSortingOrder;
                if (!_logoIdleInitialized)
                {
                    _logo.SetAnimation("idle", true, 0f);
                    _logoIdleInitialized = true;
                }
            }

            _shown = false;
        }

        public void ApplyDeviceLayout()
        {
            if (_root == null) return;

            var settings = FindTransform(_root, "Settings") as RectTransform;
            if (settings != null)
                settings.anchoredPosition = new Vector2(
                    100.5f,
                    -147.5f);

            var coinHud = FindTransform(_root, "CoinHud") as RectTransform;
            if (coinHud != null)
                LayoutCoinHud(coinHud, _coinLabel != null ? _coinLabel.text : string.Empty);

            if (_logo != null)
                _logo.transform.position =
                    App.DesignToWorld(new Vector2(540f, 601f));
        }

        public void Build()
        {
            // When this component came from HomePage.prefab, all fixed objects
            // already exist. Build is retained only for authoring/legacy use.
            if (_root != null && _worldRoot != null)
            {
                InitializePrefabRuntime();
                Hide();
                return;
            }

            transform.SetParent(App.I.transform, false);
            var go = new GameObject("HomeUiRoot", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _root = (RectTransform)go.transform;
            Stretch(_root);
            _cg = go.AddComponent<CanvasGroup>();

            // background (stretch_mode 6 = keep aspect covered)
            var bg = UiFactory.Img(_root, "Bg", SkinRoot + "/Home/background", 0, 0);
            FitCover(bg.rectTransform, bg.sprite);

            _entryCarryMask = UiFactory.Img(_root, "EntryCarryMask", null);
            Stretch(_entryCarryMask.rectTransform);
            SetGraphicAlpha(_entryCarryMask, 0f);

            // light rays fan from top center (light_rays.gd look)
            BuildCanvasRays();

            var waterGo = new GameObject(
                "WaterRipple", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var waterRect = (RectTransform)waterGo.transform;
            waterRect.SetParent(_root, false);
            waterRect.anchorMin = new Vector2(0f, 0.766f);
            waterRect.anchorMax = Vector2.one;
            waterRect.offsetMin = Vector2.zero;
            waterRect.offsetMax = Vector2.zero;
            _waterRipple = waterGo.GetComponent<RawImage>();
            _waterRipple.raycastTarget = false;

            // Runtime bubble and fragment instances are pooled below this
            // fixed, prefab-authored equivalent of home_page.tscn/RisingBubbles.
            _risingBubbles = UiFactory.FullStretch(_root, "RisingBubbles");

            // page logo spine at (540, 601) design
            _worldRoot = new GameObject("HomeWorld").transform;
            _worldRoot.SetParent(transform, false);
            var logoGo = new GameObject("PageLogo");
            logoGo.transform.SetParent(_worldRoot, false);
            _logo = logoGo.AddComponent<SpineLite.SpineSprite>();
            _logo.Load("page_logo");
            _logo.SortingOrder = LogoSortingOrder;
            logoGo.transform.position = App.DesignToWorld(new Vector2(540, 601));
            logoGo.transform.localScale = Vector3.one * App.WorldPerDesign;

            // settings button, top-left (24,58)-(164,198) + safe top
            var settings = UiFactory.Img(_root, "Settings", SkinRoot + "/Home/settings_button", 143, 143);
            settings.rectTransform.anchorMin = settings.rectTransform.anchorMax = new Vector2(0, 1);
            settings.rectTransform.anchoredPosition = new Vector2(100.5f, -147.5f);
            UiFactory.MakeButton(settings.gameObject, OnSettingsPressed);
            _settingsPress = settings.GetComponent<PressButton>();

            // coin hud, top-right (-296..-36, 70..186) + safe top
            var coinHud = UiFactory.Node(_root, "CoinHud");
            coinHud.anchorMin = coinHud.anchorMax = new Vector2(1, 1);
            coinHud.sizeDelta = new Vector2(CoinHudWidth, 105f);
            coinHud.anchoredPosition = new Vector2(-163.5f, -142.5f);
            var pill = UiFactory.Img(coinHud, "Pill", SkinRoot + "/Home/coin_pill", 214, 90);
            pill.rectTransform.anchoredPosition = new Vector2(14.5f, 0);
            var coinIcon = UiFactory.Img(coinHud, "Icon", SkinRoot + "/Home/coin_icon", 105, 105);
            coinIcon.rectTransform.anchoredPosition = new Vector2(-69f, 0);
            _coinLabel = UiFactory.Label(coinHud, "Count", "1000", 56, new Color32(0x34, 0x36, 0x60, 0xFF),
                AssetLib.NumFont, TextAnchor.MiddleCenter, 160, 80);
            _coinLabel.rectTransform.anchoredPosition = new Vector2(42, 0);
            LayoutCoinHud(coinHud, _coinLabel.text);

            // play button, bottom (-350..350, -500..-270)
            _playBtn = CommonButton.Create(_root, "Play", CommonButton.Variant.Green, "");
            _playBtn.Root.anchorMin = _playBtn.Root.anchorMax = new Vector2(0.5f, 0f);
            _playBtn.Root.sizeDelta = new Vector2(592, 208);
            _playBtn.Root.anchoredPosition = new Vector2(0, 470);
            _playPress = _playBtn.Press;
            _playLabel = FindText(_playBtn.transform, "Label");
            _playLabelBack = FindText(_playBtn.transform, "LabelBack");

            var mounts = GetComponent<PrefabMountSet>();
            if (mounts == null) mounts = gameObject.AddComponent<PrefabMountSet>();
            mounts.SetMounts(new[]
            {
                new PrefabMountSet.Mount
                {
                    Point = PrefabMountSet.MountPoint.Hud,
                    Root = _root,
                },
                new PrefabMountSet.Mount
                {
                    // Home world visuals must stay independent from the game
                    // world: BubblePage.SetVisible(false) disables WorldRoot
                    // while the home page is visible.
                    Point = PrefabMountSet.MountPoint.App,
                    Root = _worldRoot,
                },
            });
            mounts.Attach(App.I);

            InitializePrefabRuntime();
            Hide();
        }

        void ResolvePrefabReferences()
        {
            if (_root == null)
                _root = FindTransform(transform, "HomeUiRoot") as RectTransform ??
                    FindTransform(transform, "HomeCanvas") as RectTransform;
            if (_cg == null && _root != null) _cg = _root.GetComponent<CanvasGroup>();
            if (_entryCarryMask == null)
            {
                var mask = FindTransform(_root, "EntryCarryMask");
                if (mask != null) _entryCarryMask = mask.GetComponent<Image>();
            }
            if (_raysRoot == null) _raysRoot = FindTransform(_root, "Rays") as RectTransform;
            if (_waterRipple == null)
            {
                var ripple = FindTransform(_root, "WaterRipple");
                if (ripple != null) _waterRipple = ripple.GetComponent<RawImage>();
            }
            if (_risingBubbles == null)
                _risingBubbles = FindTransform(_root, "RisingBubbles") as RectTransform;

            if (_worldRoot == null) _worldRoot = FindTransform(transform, "HomeWorld");
            if (_logo == null)
            {
                if (_worldRoot != null) _logo = _worldRoot.GetComponentInChildren<SpineLite.SpineSprite>(true);
                if (_logo == null) _logo = GetComponentInChildren<SpineLite.SpineSprite>(true);
            }

            if (_coinLabel == null)
            {
                var coinHud = FindTransform(_root, "CoinHud");
                _coinLabel = FindText(coinHud, "Count");
            }

            var playRoot = FindTransform(_root, "Play");
            if (_playBtn == null && playRoot != null) _playBtn = playRoot.GetComponent<CommonButton>();
            if (_playPress == null && playRoot != null) _playPress = playRoot.GetComponentInChildren<PressButton>(true);
            if (_playLabel == null) _playLabel = FindText(playRoot, "Label");
            if (_playLabelBack == null) _playLabelBack = FindText(playRoot, "LabelBack");

            if (_settingsPress == null)
            {
                var settings = FindTransform(_root, "Settings");
                if (settings != null) _settingsPress = settings.GetComponent<PressButton>();
            }
        }

        static Transform FindTransform(Transform parent, string objectName)
        {
            if (parent == null) return null;
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == objectName) return child;
            return null;
        }

        static TMP_Text FindText(Transform parent, string objectName)
        {
            var found = FindTransform(parent, objectName);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        void LayoutCoinHud(RectTransform coinHud, string value)
        {
            if (coinHud == null) return;

            value ??= string.Empty;
            float textWidth = 0f;
            if (_coinLabel != null)
            {
                _coinLabel.enableAutoSizing = false;
                _coinLabel.fontSize = 56f;
                _coinLabel.enableWordWrapping = false;
                _coinLabel.overflowMode = TextOverflowModes.Overflow;
                textWidth = _coinLabel.GetPreferredValues(value, 10000f, 116f).x;
                if (float.IsNaN(textWidth) || float.IsInfinity(textWidth))
                    textWidth = 0f;
            }

            float width = CoinHudWidth;
            coinHud.sizeDelta = new Vector2(width, 105f);
            coinHud.anchoredPosition = new Vector2(
                -CoinHudRightMargin - width * 0.5f,
                -142.5f);

            var pill = FindTransform(coinHud, "Pill") as RectTransform;
            if (pill != null)
            {
                pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0.5f);
                pill.sizeDelta = new Vector2(214f, 90f);
                pill.anchoredPosition = new Vector2(14.5f, 0f);
            }

            var icon = FindTransform(coinHud, "Icon") as RectTransform;
            if (icon != null)
            {
                icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
                icon.sizeDelta = new Vector2(105f, 105f);
                icon.anchoredPosition = new Vector2(52.5f, 0f);
            }

            if (_coinLabel != null)
            {
                var labelRect = _coinLabel.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(CoinHudTextLeft, 0f);
                labelRect.offsetMax = new Vector2(-CoinHudTextRightPadding, 0f);
            }
        }

        static void FitCover(RectTransform rt, Sprite sprite)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            if (sprite == null) { rt.sizeDelta = new Vector2(App.DesignW, App.DesignH); return; }
            float s = Mathf.Max(App.DesignW / sprite.rect.width, App.DesignH / sprite.rect.height);
            rt.sizeDelta = new Vector2(sprite.rect.width * s, sprite.rect.height * s);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        void BuildCanvasRays()
        {
            _raysRoot = UiFactory.Node(_root, "Rays");
            _raysRoot.anchorMin = _raysRoot.anchorMax = new Vector2(0.5f, 1f);
            _raysRoot.anchoredPosition = new Vector2(0, 520);
            var rng = new System.Random(RaySeed);
            for (int i = 0; i < RayCount; i++)
            {
                var img = UiFactory.Img(
                    _raysRoot, "Ray" + i, "Art/Sprites/Home/home_light", 84, 920);
                var rt = img.rectTransform;
                rt.pivot = new Vector2(0.5f, 1f);
                float width = Range(rng, RayWidthMin, RayWidthMax);
                float length = Range(rng, RayLengthMin, RayLengthMax);
                float t = i / (float)(RayCount - 1);
                float angle = Mathf.Lerp(-RayFanDegrees, RayFanDegrees, t) +
                    Range(rng, -RayAngleJitterDegrees, RayAngleJitterDegrees);
                float alpha = Range(rng, RayAlphaMin, RayAlphaMax);
                rt.localRotation = Quaternion.Euler(0, 0, -angle);
                rt.localScale = new Vector3(width, length, 1f);
                img.color = new Color(RayColor.r, RayColor.g, RayColor.b, alpha);
            }
        }

        static float Range(System.Random rng, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)rng.NextDouble());
        }

        void InitializeBackdropVisuals()
        {
            SetupCarryMask();
            SetupWaterRipple();
            if (_rays.Count == 0) RebuildRayState();
        }

        void EnsureContentGroups()
        {
            if (_root == null || _contentGroups.Count > 0) return;
            for (int i = 0; i < _root.childCount; i++)
            {
                var child = _root.GetChild(i);
                if (child.name == "Bg" || child.name == "Background" ||
                    child.name == "EntryCarryMask")
                    continue;
                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
                _contentGroups.Add(group);
            }
        }

        void SetupCarryMask()
        {
            if (_entryCarryMask == null) return;
            if (_entryCarryMask.sprite == null)
            {
                _runtimeCarryMaskTexture = MakeGradient(
                    new Color32(0x24, 0x80, 0xE9, 0xFF),
                    new Color32(0x24, 0x58, 0xE9, 0xFF));
                _runtimeCarryMaskSprite = Sprite.Create(
                    _runtimeCarryMaskTexture,
                    new Rect(0, 0, _runtimeCarryMaskTexture.width, _runtimeCarryMaskTexture.height),
                    new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
                _entryCarryMask.sprite = _runtimeCarryMaskSprite;
            }
            _entryCarryMask.raycastTarget = false;
        }

        void SetupWaterRipple()
        {
            if (_waterRipple == null) return;
            if (_runtimeWaterRippleMaterial == null)
            {
                var shader = Shader.Find("BubblePics/WaterRipple");
                if (shader == null) return;
                _runtimeWaterRippleMaterial = new Material(shader)
                {
                    name = "HomeWaterRipple (Runtime)"
                };
            }

            _runtimeWaterRippleMaterial.SetTexture(
                "_CausticsTex", AssetLib.Texture("Art/Sprites/Home/water_caustics"));
            _runtimeWaterRippleMaterial.SetFloat("_ScaleU", 3.46f);
            _runtimeWaterRippleMaterial.SetFloat("_Aspect", 0.52f);
            _runtimeWaterRippleMaterial.SetFloat("_HStretch", 1.6f);
            _runtimeWaterRippleMaterial.SetVector(
                "_ScrollA", new Vector4(0.05f, 0.03f, 0f, 0f));
            _runtimeWaterRippleMaterial.SetVector(
                "_ScrollB", new Vector4(-0.045f, 0.0375f, 0f, 0f));
            _runtimeWaterRippleMaterial.SetFloat("_RippleFreq", 2.4f);
            _runtimeWaterRippleMaterial.SetFloat("_RippleAmp", 0.05f);
            _runtimeWaterRippleMaterial.SetFloat("_Intensity", 0.25f);
            _runtimeWaterRippleMaterial.SetColor("_Tint", RayColor);
            _runtimeWaterRippleMaterial.SetFloat("_BandHeight", 0.167f);
            _runtimeWaterRippleMaterial.SetFloat("_BandFade", 0.833f);
            _runtimeWaterRippleMaterial.SetFloat("_Persp", 5f);
            _runtimeWaterRippleMaterial.SetFloat("_PerspCurve", 3f);
            _runtimeWaterRippleMaterial.SetFloat("_GlobalAlpha", 1f);
            _waterRipple.texture = Texture2D.whiteTexture;
            _waterRipple.material = _runtimeWaterRippleMaterial;
            _waterRipple.raycastTarget = false;
        }

        void RebuildRayState()
        {
            _rays.Clear();
            if (_raysRoot == null) return;

            var rayRects = new List<RectTransform>(RayCount);
            for (int rayIndex = 0; rayIndex < RayCount; rayIndex++)
            {
                var ray = FindTransform(_raysRoot, "Ray" + rayIndex) as RectTransform;
                if (ray != null) rayRects.Add(ray);
            }

            var rng = new System.Random(RaySeed);
            for (int i = 0; i < rayRects.Count; i++)
            {
                float width = Range(rng, RayWidthMin, RayWidthMax);
                float length = Range(rng, RayLengthMin, RayLengthMax);
                float t = i / (float)(RayCount - 1);
                float angle = Mathf.Lerp(-RayFanDegrees, RayFanDegrees, t) +
                    Range(rng, -RayAngleJitterDegrees, RayAngleJitterDegrees);
                float alpha = Range(rng, RayAlphaMin, RayAlphaMax);
                var rect = rayRects[i];
                var image = rect.GetComponent<Image>();

                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(84f, 920f);
                rect.localScale = new Vector3(width, length, 1f);
                rect.localRotation = Quaternion.Euler(0, 0, -angle);
                if (image != null)
                {
                    image.color = new Color(RayColor.r, RayColor.g, RayColor.b, alpha);
                    image.raycastTarget = false;
                }

                _rays.Add(new RayState
                {
                    Rect = rect,
                    Image = image,
                    BaseAngle = angle,
                    BaseAlpha = alpha,
                    BaseWidth = width,
                    BaseLength = length
                });
            }
        }

        static Texture2D MakeGradient(Color32 top, Color32 bottom)
        {
            var texture = new Texture2D(1, 256, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < texture.height; y++)
                texture.SetPixel(0, y, Color.Lerp(bottom, top, y / 255f));
            texture.Apply();
            return texture;
        }

        static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        void BuildDecoPool()
        {
            if (_decoPoolBuilt || _risingBubbles == null) return;

            _decoPoolBuilt = true;
            _decoRandom = new System.Random(DecoSeed);
            _decoTime = 0f;
            var sprite = AssetLib.Sprite(DecoSpritePath);

            for (int i = 0; i < DecoCount; i++)
            {
                var image = CreateDecoImage("Bubble_" + i.ToString("00"), sprite);
                var state = new DecoBubbleState
                {
                    Rect = image.rectTransform,
                    Image = image
                };
                _decoBubbles.Add(state);
                ResetDecoBubble(i, true);
            }

            for (int i = 0; i < FragmentPoolSize; i++)
            {
                var image = CreateDecoImage("Fragment_" + i.ToString("00"), sprite);
                image.gameObject.SetActive(false);
                _decoFragments.Add(new FragmentState
                {
                    Rect = image.rectTransform,
                    Image = image
                });
            }
        }

        Image CreateDecoImage(string objectName, Sprite sprite)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_risingBubbles, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 200f);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, DecoAlpha);
            return image;
        }

        float RandomRange(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)_decoRandom.NextDouble());
        }

        void ResetDecoBubble(int index, bool initial = false)
        {
            var state = _decoBubbles[index];
            bool onLeft = index % 2 == 0;
            float bandMin = onLeft
                ? DecoMargin
                : App.DesignW - DecoMargin - DecoBandWidth;

            state.BaseX = bandMin + RandomRange(0f, DecoBandWidth);
            state.Speed = RandomRange(DecoSpeedMin, DecoSpeedMax);
            state.Phase = RandomRange(0f, Mathf.PI * 2f);
            // home_page.gd reserves a second phase value. It is currently not
            // used by the movement formula, but consuming it keeps the seeded
            // sequence aligned with the original rules.
            RandomRange(0f, Mathf.PI * 2f);
            state.BurstY = RandomRange(DecoBurstYMin, DecoBurstYMax);
            state.Wiggle = _decoRandom.NextDouble() < DecoWiggleChance
                ? RandomRange(DecoWiggleAmplitudeMin, DecoWiggleAmplitudeMax)
                : 0f;

            float scale = RandomRange(DecoSizeMin, DecoSizeMax);
            state.Rect.localScale = Vector3.one * scale;
            state.Y = initial
                ? RandomRange(state.BurstY, DecoBottomY)
                : DecoBottomY;
            state.Rect.anchoredPosition = new Vector2(state.BaseX, -state.Y);
            state.Image.color = new Color(1f, 1f, 1f, 0f);
            state.Image.gameObject.SetActive(true);
        }

        void Update()
        {
            if (!_shown || !_decoPoolBuilt) return;
            float delta = Time.deltaTime;
            UpdateLightRays(delta);
            UpdateDecoBubbles(delta);
        }

        void UpdateLightRays(float delta)
        {
            _rayTime += delta;
            for (int i = 0; i < _rays.Count; i++)
            {
                var ray = _rays[i];
                if (ray.Rect == null) continue;
                float fi = i;
                float breath = RayAlphaAmplitude *
                    Mathf.Sin(Mathf.PI * 2f * _rayTime / RayBreathPeriod + fi * 0.9f);
                float flicker = RayAlphaFlicker *
                    Mathf.Sin(Mathf.PI * 2f * _rayTime / RayFlickerPeriod + fi * 1.7f);
                if (ray.Image != null)
                {
                    float alpha = Mathf.Max(0f, ray.BaseAlpha + breath + flicker);
                    ray.Image.color = new Color(RayColor.r, RayColor.g, RayColor.b, alpha);
                }

                float sway = RaySwayDegrees *
                    Mathf.Sin(Mathf.PI * 2f * _rayTime / RaySwayPeriod + fi * 0.7f);
                ray.Rect.localRotation = Quaternion.Euler(0, 0, -(ray.BaseAngle + sway));
                float widthPulse = 1f + RayWidthPulse *
                    Mathf.Sin(Mathf.PI * 2f * _rayTime / RayWidthPulsePeriod + fi * 0.5f);
                ray.Rect.localScale =
                    new Vector3(ray.BaseWidth * widthPulse, ray.BaseLength, 1f);
            }
        }

        void UpdateDecoBubbles(float delta)
        {
            _decoTime += delta;
            for (int i = 0; i < _decoBubbles.Count; i++)
            {
                var state = _decoBubbles[i];
                state.Speed += DecoAcceleration * delta;
                state.Y -= state.Speed * delta;

                if (state.Y <= state.BurstY || state.Y < DecoTopY)
                {
                    if (state.Y <= state.BurstY)
                        SpawnFragments(new Vector2(state.Rect.anchoredPosition.x, state.Y));
                    ResetDecoBubble(i);
                    continue;
                }

                float x = state.BaseX + state.Wiggle *
                    Mathf.Sin(_decoTime * DecoWiggleFrequency * Mathf.PI * 2f + state.Phase);
                state.Rect.anchoredPosition = new Vector2(x, -state.Y);

                float fadeIn = Mathf.Clamp01((DecoBottomY - state.Y) / DecoFadeInDistance);
                float fadeOut = Mathf.Clamp01((state.Y - state.BurstY) / 200f);
                state.Image.color = new Color(1f, 1f, 1f, DecoAlpha * Mathf.Min(fadeIn, fadeOut));
            }

            UpdateFragments(delta);
        }

        void SpawnFragments(Vector2 designPosition)
        {
            int spawned = 0;
            for (int i = 0; i < _decoFragments.Count && spawned < FragmentsPerBurst; i++)
            {
                var state = _decoFragments[i];
                if (state.Life > 0f) continue;

                float angle = RandomRange(0f, Mathf.PI * 2f);
                float speed = RandomRange(FragmentSpeedMin, FragmentSpeedMax);
                state.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                state.MaxLife = RandomRange(FragmentLifeMin, FragmentLifeMax);
                state.Life = state.MaxLife;
                state.Position = designPosition;

                float scale = RandomRange(FragmentSizeMin, FragmentSizeMax);
                state.Rect.localScale = Vector3.one * scale;
                state.Rect.anchoredPosition = new Vector2(state.Position.x, -state.Position.y);
                state.Image.color = new Color(1f, 1f, 1f, DecoAlpha);
                state.Image.gameObject.SetActive(true);
                spawned++;
            }
        }

        void UpdateFragments(float delta)
        {
            for (int i = 0; i < _decoFragments.Count; i++)
            {
                var state = _decoFragments[i];
                if (state.Life <= 0f) continue;

                state.Life -= delta;
                if (state.Life <= 0f)
                {
                    state.Life = 0f;
                    state.Image.gameObject.SetActive(false);
                    continue;
                }

                state.Velocity *= 1f - Mathf.Clamp01(FragmentDamping * delta);
                state.Velocity = new Vector2(
                    state.Velocity.x,
                    state.Velocity.y + FragmentRise * delta);
                state.Position += state.Velocity * delta;
                state.Rect.anchoredPosition = new Vector2(state.Position.x, -state.Position.y);
                state.Image.color = new Color(
                    1f, 1f, 1f, DecoAlpha * state.Life / state.MaxLife);
            }
        }

        public void Show()
        {
            InitializePrefabRuntime();
            _shown = true;
            if (_carryMaskFade != null)
            {
                StopCoroutine(_carryMaskFade);
                _carryMaskFade = null;
            }
            SetGraphicAlpha(_entryCarryMask, 0f);
            SetContentAlpha(1f);
            if (_cg != null) _cg.alpha = 1f;
            gameObject.SetActive(true);
            if (_root != null) _root.gameObject.SetActive(true);
            if (_worldRoot != null) _worldRoot.gameObject.SetActive(true);
            RefreshEntryUi();
        }

        public void Hide()
        {
            _shown = false;
            if (_root != null) _root.gameObject.SetActive(false);
            if (_worldRoot != null) _worldRoot.gameObject.SetActive(false);
        }

        public float CarryMaskAlpha =>
            _entryCarryMask != null ? _entryCarryMask.color.a : 0f;

        public void PlayEntryMaskFade(float startAlpha, float duration = 0.5f)
        {
            if (_entryCarryMask == null || startAlpha <= 0f) return;
            if (_carryMaskFade != null) StopCoroutine(_carryMaskFade);
            SetGraphicAlpha(_entryCarryMask, startAlpha);
            _carryMaskFade = StartCoroutine(CarryMaskFadeCo(startAlpha, duration));
        }

        public IEnumerator FadeOutContent(float duration)
        {
            float startAlpha = _contentAlpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
                SetContentAlpha(Mathf.Lerp(startAlpha, 0f, 1f - Mathf.Cos(t * Mathf.PI * 0.5f)));
                yield return null;
            }
            SetContentAlpha(0f);
        }

        void SetContentAlpha(float alpha)
        {
            _contentAlpha = Mathf.Clamp01(alpha);
            for (int i = 0; i < _contentGroups.Count; i++)
            {
                if (_contentGroups[i] != null) _contentGroups[i].alpha = _contentAlpha;
            }
            if (_logo != null)
            {
                var tint = _logo.Tint;
                tint.a = _contentAlpha;
                _logo.Tint = tint;
            }
        }

        IEnumerator CarryMaskFadeCo(float startAlpha, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration));
                SetGraphicAlpha(_entryCarryMask, Mathf.Lerp(startAlpha, 0f, Mathf.Sin(t * Mathf.PI * 0.5f)));
                yield return null;
            }
            SetGraphicAlpha(_entryCarryMask, 0f);
            _carryMaskFade = null;
        }

        void RefreshEntryUi()
        {
            if (_coinLabel != null)
            {
                _coinLabel.text = SaveState.Coins.ToString();
                var coinHud = FindTransform(_root, "CoinHud") as RectTransform;
                LayoutCoinHud(coinHud, _coinLabel.text);
            }
            int level = SaveState.CurrentLevel;
            bool endReached = !LevelRepo.HasLevel(level);
            string fmt = endReached ? Localization.Tr("coming_soon") : Localization.Tr("level_d");
            string text = !endReached && fmt.Contains("%d")
                ? fmt.Replace("%d", level.ToString())
                : fmt;
            if (_playLabel != null) _playLabel.text = text;
            if (_playLabelBack != null) _playLabelBack.text = text;
            // Keep compatibility with a CommonButton prefab that exposes its
            // own serialized labels.
            if (_playLabel == null && _playBtn != null) _playBtn.SetText(text);
            if (_playBtn != null)
            {
                _playBtn.SetVariant(endReached
                    ? CommonButton.Variant.Green
                    : LevelRepo.Get(level)?.difficulty_type == "Hard"
                        ? CommonButton.Variant.Red
                        : CommonButton.Variant.Green);
                _playBtn.SetInteractable(!endReached);
            }
        }

        /// <summary>Refreshes level/coin copy after a GM profile mutation.</summary>
        public void RefreshForGm()
        {
            RefreshEntryUi();
        }

        void OnPlayPressed()
        {
            if (!LevelRepo.HasLevel(SaveState.CurrentLevel)) return;
            if (_playTransition != null) return;
            FunSmithTelemetry.TrackHomePlayClick(
                LevelRepo.Get(SaveState.CurrentLevel),
                SaveState.CurrentLevel);
            Haptics.Play(HapticLevel.Weak);
            _playTransition = StartCoroutine(PlayEntryTransitionCo());
        }

        IEnumerator PlayEntryTransitionCo()
        {
            float maskAlpha = CarryMaskAlpha;
            yield return FadeOutContent(0.2f);
            Hide();
            var page = App.I.Page;
            page.SetVisible(true);
            page.OpenLevel(SaveState.CurrentLevel, "fresh", maskAlpha);
            _playTransition = null;
        }

        void OnSettingsPressed()
        {
            if (_settings == null)
            {
                var catalog = PrefabCatalog.Current;
                if (catalog != null && catalog.SettingPage != null)
                {
                    _settings = PrefabCatalog.InstantiateComponent<SettingPage>(
                        catalog.SettingPage, App.I.DialogRoot);
                }

                if (_settings == null)
                {
                    _settings = new GameObject("HomeSettingPage").AddComponent<SettingPage>();
                    _settings.Build(App.I.DialogRoot, App.I.Page);
                }
            }
            SoundManager.I.Play("dialog");
            _settings.Show(gameMode: false);
        }

        void OnDestroy()
        {
            if (_runtimeWaterRippleMaterial != null)
                Destroy(_runtimeWaterRippleMaterial);
            if (_runtimeCarryMaskSprite != null)
                Destroy(_runtimeCarryMaskSprite);
            if (_runtimeCarryMaskTexture != null)
                Destroy(_runtimeCarryMaskTexture);
        }
    }
}
