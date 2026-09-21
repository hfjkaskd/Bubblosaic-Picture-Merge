using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Port of bubble_backdrop_controller.gd. World sprites in design pixels.
    /// Layers (Godot draw order): Background, Scrim, BackgroundUnderwater,
    /// LightRays, EntryCarryMask, MidDepthBubbles, AmbientBubbles, WaterRipple.
    /// </summary>
    public class BackdropController : MonoBehaviour
    {
        // Godot draws the World children in scene-tree order while they share
        // z_index=0. Unity renderers need explicit, distinct orders to preserve
        // that order without letting backdrop effects enter the bubble band.
        const int BACKGROUND_ORDER = 0;
        const int SCRIM_ORDER = 10;
        const int UNDERWATER_ORDER = 20;
        const int LIGHT_RAYS_ORDER = 21;
        const int ENTRY_CARRY_MASK_ORDER = 22;
        // MidDepthBubbles use 25 and AmbientBubbles/particles use 30/31.
        const int WATER_RIPPLE_ORDER = 40;
        // The PSD gameplay background is authored as a complete 1080x2340
        // frame. Applying the legacy 1.244x experiment zoom crops about 459
        // source pixels from its lower composition, so keep this static skin
        // and its ripple overlay at the authored full-frame scale.
        const float PSD_GAMEPLAY_BACKGROUND_ZOOM = 1f;

        public const float BG_END_ZOOM = 1.244f;
        public const float TRANSITION_SEC = 0.6f;
        public const float SCRIM_END_ALPHA = 0.7f;
        public const float BG_EFFECTS_FADE_SEC = 0.5f;
        public const float BG_EFFECTS_INTRO_PROGRESS = 0.5f;
        public const float RIPPLE_TARGET_ALPHA = 0.8f;
        public const float GAMEPLAY_FADE_OUT_SEC = 0.2f;
        public const float COMPLETE_LIGHTS_FADE_IN_SEC = 0.25f;

        public BubblePage Page;

        [Header("Prefab hierarchy")]
        [SerializeField] Transform _contentRoot;
        [SerializeField] SpriteRenderer _background;
        [SerializeField] SpriteRenderer _scrim;
        [SerializeField] SpriteRenderer _underwater;
        [SerializeField] LightRays _lightRays;
        [SerializeField] SpriteRenderer _entryCarryMask;
        [SerializeField] MidDepthBubbles _midDepth;
        [SerializeField] AmbientBubbles _ambient;
        [SerializeField] SpriteRenderer _waterRipple;
        [SerializeField] Material _rippleMaterialTemplate;
        Material _rippleMat;

        public LightRays LightRays => _lightRays;
        public AmbientBubbles Ambient => _ambient;
        public MidDepthBubbles MidDepth => _midDepth;
        float _zoom = 1f;
        float _bgEffects;
        Coroutine _introCo, _effectsCo, _topbarCo, _toolbarCo;
        Vector2 _topBarRest, _toolbarRest;
        bool _barsRestCached;
        readonly Dictionary<SpriteRenderer, float> _gameplayBaseAlpha = new();

        static Texture2D MakeVGradient(Color32[] colors32, float[] stops)
        {
            var colors = System.Array.ConvertAll(colors32, c => (Color)c);
            return MakeVGradientC(colors, stops);
        }

        static Texture2D MakeVGradientC(Color[] colors, float[] stops)
        {
            int h = 256;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                float t = 1f - y / (float)(h - 1); // v=0 bottom => t from top
                Color c = colors[colors.Length - 1];
                for (int i = 1; i < stops.Length; i++)
                {
                    if (t <= stops[i])
                    {
                        float k = Mathf.InverseLerp(stops[i - 1], stops[i], t);
                        c = Color.Lerp(colors[i - 1], colors[i], k);
                        break;
                    }
                }
                tex.SetPixel(0, y, c);
            }
            tex.Apply();
            return tex;
        }

        // gameplay scrim (new 4-stop): #65B0EC #65B0EC #0061B0 #002F57
        static Texture2D _scrimTexNew;
        static Sprite _scrimSpriteNew;
        static Texture2D ScrimTexNew()
        {
            if (_scrimTexNew == null)
                _scrimTexNew = MakeVGradient(new[]
                {
                    new Color32(0x65, 0xB0, 0xEC, 0xFF),
                    new Color32(0x65, 0xB0, 0xEC, 0xFF),
                    new Color32(0x00, 0x61, 0xB0, 0xFF),
                    new Color32(0x00, 0x2F, 0x57, 0xFF),
                }, new[] { 0f, 0.333f, 0.667f, 1f });
            return _scrimTexNew;
        }

        static Sprite ScrimSpriteNew()
        {
            if (_scrimSpriteNew == null)
            {
                var tex = ScrimTexNew();
                _scrimSpriteNew = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0, 1), 1f, 0, SpriteMeshType.FullRect);
            }
            return _scrimSpriteNew;
        }

        // old 2-stop: #65B0EC -> #0070D2
        static Texture2D _scrimTexOld;
        static Sprite _scrimSpriteOld;
        static Texture2D ScrimTexOld()
        {
            if (_scrimTexOld == null)
                _scrimTexOld = MakeVGradient(new[]
                {
                    new Color32(0x65, 0xB0, 0xEC, 0xFF),
                    new Color32(0x00, 0x70, 0xD2, 0xFF),
                }, new[] { 0f, 1f });
            return _scrimTexOld;
        }

        static Sprite ScrimSpriteOld()
        {
            if (_scrimSpriteOld == null)
            {
                var tex = ScrimTexOld();
                _scrimSpriteOld = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0, 1), 1f, 0, SpriteMeshType.FullRect);
            }
            return _scrimSpriteOld;
        }

        static Texture2D _entryMaskTexture;
        static Texture2D EntryMaskTexture()
        {
            if (_entryMaskTexture == null)
            {
                _entryMaskTexture = MakeVGradient(new[]
                {
                    new Color32(0x24, 0x80, 0xE9, 0xFF),
                    new Color32(0x24, 0x58, 0xE9, 0xFF),
                }, new[] { 0f, 1f });
            }
            return _entryMaskTexture;
        }

        /// <summary>
        /// Creates and wires the persistent backdrop hierarchy while authoring a
        /// prefab. Calling this repeatedly is safe; already-authored nodes and
        /// components are reused.
        /// </summary>
        [ContextMenu("Authoring/Setup Prefab Hierarchy")]
        public void SetupPrefabAuthoring()
        {
            SetupPrefabAuthoring(_contentRoot != null ? _contentRoot : transform);
        }

        public void SetupPrefabAuthoring(Transform contentRoot)
        {
            _contentRoot = contentRoot != null ? contentRoot : transform;
            EnsureHierarchy(_contentRoot);
            MarkAuthoringDirty();
        }

        /// <summary>
        /// Binds an instantiated backdrop prefab to its page. Fixed children
        /// already present in the prefab are reused. Missing children remain
        /// missing: prefab edits are authoritative and runtime must not repair
        /// or recreate deleted artwork.
        /// </summary>
        public void InitializePrefabRuntime(BubblePage page, Transform worldRoot = null)
        {
            if (page != null) Page = page;
            if (_contentRoot == null)
            {
                if (HasBoundHierarchy())
                    _contentRoot = ResolveBoundRoot();
                else
                    _contentRoot = worldRoot != null ? worldRoot : transform;
            }
            InitializeAuthoredHierarchy();
        }

        /// <summary>
        /// Legacy API retained for catalog-less pages, but it deliberately
        /// does not author missing visuals at runtime.
        /// </summary>
        public void Build(Transform worldRoot)
        {
            _contentRoot = worldRoot != null ? worldRoot : transform;
            InitializeAuthoredHierarchy();
        }

        void InitializeAuthoredHierarchy()
        {
            if (_background != null)
                _background.sortingOrder = BACKGROUND_ORDER;
            SetAlpha(_scrim, 0f);
            if (_scrim != null)
                _scrim.sortingOrder = SCRIM_ORDER;
            if (_underwater != null)
            {
                _underwater.sortingOrder = UNDERWATER_ORDER;
                _underwater.gameObject.SetActive(false);
            }
            if (_lightRays != null)
            {
                _lightRays.InitializePrefabRuntime(LIGHT_RAYS_ORDER);
                _lightRays.gameObject.SetActive(false);
            }
            SetAlpha(_entryCarryMask, 0f);
            if (_entryCarryMask != null)
                _entryCarryMask.sortingOrder = ENTRY_CARRY_MASK_ORDER;
            if (_waterRipple != null)
            {
                _waterRipple.sortingOrder = WATER_RIPPLE_ORDER;
                EnsureRippleMaterial();
                SetAlpha(_waterRipple, 0f);
            }
            ApplyZoom();
        }

        void EnsureHierarchy(Transform root)
        {
            _background = EnsureCoverSprite(_background, root, "Background",
                AssetLib.Texture("Art/Sprites/PsdSkin20260807/Gameplay/background"),
                BACKGROUND_ORDER);
            _scrim = EnsureStretchSprite(_scrim, root, "Scrim", ScrimTexNew(), SCRIM_ORDER);
            SetAlpha(_scrim, 0);
            _underwater = EnsureCoverSprite(_underwater, root, "BackgroundUnderwater",
                AssetLib.Texture("Art/Sprites/Home/bubble_bg_underwater"), UNDERWATER_ORDER);
            _underwater.gameObject.SetActive(false);

            _lightRays = EnsureComponentNode(_lightRays, root, "LightRays");
            _lightRays.InitializePrefabRuntime(LIGHT_RAYS_ORDER);
            _lightRays.gameObject.SetActive(false);

            _entryCarryMask = EnsureStretchSprite(_entryCarryMask, root, "EntryCarryMask",
                EntryMaskTexture(), ENTRY_CARRY_MASK_ORDER);
            SetAlpha(_entryCarryMask, 0);

            _midDepth = EnsureComponentNode(_midDepth, root, "MidDepthBubbles");
            _ambient = EnsureComponentNode(_ambient, root, "AmbientBubbles");

            _waterRipple = EnsureCoverSprite(_waterRipple, root, "WaterRipple",
                AssetLib.Texture("Art/Sprites/PsdSkin20260807/Gameplay/background"),
                WATER_RIPPLE_ORDER);
            EnsureRippleMaterial();
            _waterRipple.sortingOrder = WATER_RIPPLE_ORDER;
            SetAlpha(_waterRipple, 0);

            ApplyZoom();
        }

        bool HasBoundHierarchy()
        {
            return _background != null || _scrim != null || _underwater != null ||
                   _lightRays != null || _entryCarryMask != null || _midDepth != null ||
                   _ambient != null || _waterRipple != null;
        }

        Transform ResolveBoundRoot()
        {
            if (_background != null) return _background.transform.parent;
            if (_scrim != null) return _scrim.transform.parent;
            if (_underwater != null) return _underwater.transform.parent;
            if (_lightRays != null) return _lightRays.transform.parent;
            if (_entryCarryMask != null) return _entryCarryMask.transform.parent;
            if (_midDepth != null) return _midDepth.transform.parent;
            if (_ambient != null) return _ambient.transform.parent;
            if (_waterRipple != null) return _waterRipple.transform.parent;
            return transform;
        }

        SpriteRenderer EnsureCoverSprite(SpriteRenderer current, Transform parent,
            string nodeName, Texture2D tex, int order)
        {
            var sr = EnsureSpriteNode(current, parent, nodeName);
            EnsureSprite(sr, tex);
            sr.sortingOrder = order;
            return sr;
        }

        SpriteRenderer EnsureStretchSprite(SpriteRenderer current, Transform parent,
            string nodeName, Texture2D tex, int order)
        {
            var sr = EnsureSpriteNode(current, parent, nodeName);
            EnsureSprite(sr, tex);
            sr.sortingOrder = order;
            if (tex == null) return sr;

            // stretch to full view
            float sx = BubbleField.ViewW / tex.width;
            float sy = BubbleField.ViewH / tex.height;
            sr.transform.localScale = new Vector3(sx, sy, 1);
            sr.transform.position = App.DesignToWorld(new Vector2(0, 0));
            return sr;
        }

        static SpriteRenderer EnsureSpriteNode(SpriteRenderer current, Transform parent, string nodeName)
        {
            if (current != null) return current;
            var child = FindDirectChild(parent, nodeName);
            if (child == null)
            {
                var go = new GameObject(nodeName);
                child = go.transform;
                child.SetParent(parent, false);
            }
            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            return renderer;
        }

        static T EnsureComponentNode<T>(T current, Transform parent, string nodeName)
            where T : Component
        {
            if (current != null) return current;
            var child = FindDirectChild(parent, nodeName);
            if (child == null)
            {
                var go = new GameObject(nodeName);
                child = go.transform;
                child.SetParent(parent, false);
            }
            var component = child.GetComponent<T>();
            if (component == null) component = child.gameObject.AddComponent<T>();
            return component;
        }

        static Transform FindDirectChild(Transform parent, string nodeName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == nodeName) return child;
            }
            return null;
        }

        static void EnsureSprite(SpriteRenderer renderer, Texture2D texture)
        {
            if (renderer == null || texture == null) return;
            if (renderer.sprite != null && renderer.sprite.texture == texture)
                return;
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0, 1), 1f, 0, SpriteMeshType.FullRect);
        }

        void EnsureRippleMaterial()
        {
            if (_waterRipple == null) return;
            if (_rippleMat == null &&
                _rippleMaterialTemplate != null &&
                _rippleMaterialTemplate.shader != null &&
                _rippleMaterialTemplate.shader.name == "BubblePics/WaterRipple")
                _rippleMat = _rippleMaterialTemplate;
            if (_rippleMat == null &&
                _waterRipple != null &&
                _waterRipple.sharedMaterial != null &&
                _waterRipple.sharedMaterial.shader != null &&
                _waterRipple.sharedMaterial.shader.name == "BubblePics/WaterRipple")
                _rippleMat = _waterRipple.sharedMaterial;
            if (_rippleMat == null)
            {
                var shader = Shader.Find("BubblePics/WaterRipple");
                if (shader == null) return;
                _rippleMat = new Material(shader) { name = "BackdropWaterRipple (Runtime)" };
            }

            _rippleMat.SetTexture("_CausticsTex", AssetLib.Texture("Art/Effect/water_ripple"));
            _rippleMat.SetFloat("_ScaleU", 3.5f);
            _rippleMat.SetFloat("_Aspect", 2.22f);
            _rippleMat.SetFloat("_HStretch", 1.6f);
            _rippleMat.SetVector("_ScrollA", new Vector4(0.1f, 0.06f, 0, 0));
            _rippleMat.SetVector("_ScrollB", new Vector4(-0.09f, 0.075f, 0, 0));
            _rippleMat.SetFloat("_RippleFreq", 3.0f);
            _rippleMat.SetFloat("_RippleAmp", 0.06f);
            _rippleMat.SetFloat("_Intensity", 0.05f);
            _rippleMat.SetColor("_Tint", new Color(0.85f, 0.95f, 1f, 1f));
            _rippleMat.SetFloat("_BandHeight", 0.167f);
            _rippleMat.SetFloat("_BandFade", 0.067f);
            _rippleMat.SetFloat("_Persp", 5.0f);
            _rippleMat.SetFloat("_PerspCurve", 3.0f);
            _waterRipple.sharedMaterial = _rippleMat;
        }

        void MarkAuthoringDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            if (_lightRays != null) UnityEditor.EditorUtility.SetDirty(_lightRays);
#endif
        }

        static void SetAlpha(SpriteRenderer sr, float a)
        {
            if (sr == null) return;
            var c = sr.color; c.a = a; sr.color = c;
        }

        static void SetActive(Component component, bool active)
        {
            if (component != null)
                component.gameObject.SetActive(active);
        }

        void ApplyZoom()
        {
            ApplyCover(_background, PSD_GAMEPLAY_BACKGROUND_ZOOM);
            ApplyCover(_waterRipple, PSD_GAMEPLAY_BACKGROUND_ZOOM);
            ApplyCover(_underwater, _zoom);
        }

        void ApplyCover(SpriteRenderer sr, float zoom)
        {
            if (sr == null || sr.sprite == null) return;
            float texW = sr.sprite.rect.width, texH = sr.sprite.rect.height;
            float baseS = Mathf.Max(BubbleField.ViewW / texW, BubbleField.ViewH / texH);
            // design-space top-left of unzoomed cover
            var basePos = new Vector2((BubbleField.ViewW - texW * baseS) / 2f,
                (BubbleField.ViewH - texH * baseS) / 2f);
            var pivot = new Vector2(BubbleField.ViewW / 2f, 0f);
            float s = baseS * zoom;
            var topLeft = pivot + (basePos - pivot) * zoom;
            // Sprite.Create() backgrounds use a top-left pivot, while imported PSD
            // sprites use Unity's default centre pivot. Position the actual sprite
            // pivot so either representation covers the same centred screen area.
            var spritePivotOffset = new Vector2(
                sr.sprite.pivot.x * s,
                (texH - sr.sprite.pivot.y) * s);
            var pos = topLeft + spritePivotOffset;
            sr.transform.localScale = new Vector3(s, s, 1);
            sr.transform.position = App.DesignToWorld(pos);
        }

        public void SetBgEffectsProgress(float p)
        {
            _bgEffects = p;
            SetAlpha(_waterRipple, p * RIPPLE_TARGET_ALPHA);
        }

        // ------------------------------------------------------------ transitions
        public void PlayShowTransition(string mode, float entryMaskAlpha)
        {
            ResetGameplayContentAlpha();
            HideCompleteFx();
            // A cleared Sprite field is also an intentional prefab edit. Do
            // not restore a generated gradient into an authored empty slot.
            if (_scrim != null && _scrim.sprite != null)
            {
                _scrim.sprite = ScrimSpriteNew();
                RestretchScrim();
            }

            if (_introCo != null) StopCoroutine(_introCo);
            if (mode == "fresh")
            {
                _introCo = StartCoroutine(BgIntroFreshCo());
                PlayBarsTransition(true);
                if (entryMaskAlpha > 0)
                {
                    SetAlpha(_entryCarryMask, entryMaskAlpha);
                    StartCoroutine(Tween.Run(TRANSITION_SEC, k => SetAlpha(_entryCarryMask, Mathf.Lerp(entryMaskAlpha, 0, k)), Ease.OutSine));
                }
                else SetAlpha(_entryCarryMask, 0);
            }
            else if (mode == "next")
            {
                _introCo = StartCoroutine(BgIntroNextCo());
                PlayBarsTransition(true);
                SetAlpha(_entryCarryMask, 0);
            }
            else
            {
                _zoom = BG_END_ZOOM;
                ApplyZoom();
                SetUnderwaterImmediate();
                ResetBarsToRest();
                SetBgEffectsProgress(1f);
                SetAlpha(_entryCarryMask, 0);
            }
        }

        void RestretchScrim()
        {
            if (_scrim == null || _scrim.sprite == null) return;
            float sx = BubbleField.ViewW / _scrim.sprite.rect.width;
            float sy = BubbleField.ViewH / _scrim.sprite.rect.height;
            _scrim.transform.localScale = new Vector3(sx, sy, 1);
        }

        void SetUnderwaterImmediate()
        {
            SetActive(_background, true);
            SetAlpha(_scrim, SCRIM_END_ALPHA);
            SetActive(_underwater, false);
        }

        IEnumerator BgIntroFreshCo()
        {
            _zoom = 1f;
            SetActive(_underwater, false);
            SetActive(_background, true);
            SetAlpha(_scrim, 0);
            SetBgEffectsProgress(0);
            float t = 0;
            while (t < TRANSITION_SEC)
            {
                t += Time.deltaTime;
                float raw = Mathf.Clamp01(t / TRANSITION_SEC);
                float k = Tween.Evaluate(Ease.OutCubic, raw);
                _zoom = Mathf.Lerp(1f, BG_END_ZOOM, k);
                ApplyZoom();
                SetAlpha(_scrim, Mathf.Lerp(0, SCRIM_END_ALPHA, k));
                SetBgEffectsProgress(Mathf.Lerp(0, BG_EFFECTS_INTRO_PROGRESS, Tween.Evaluate(Ease.OutSine, raw)));
                yield return null;
            }
            // effects 0.5 -> 1.0 after
            if (_effectsCo != null) StopCoroutine(_effectsCo);
            _effectsCo = StartCoroutine(Tween.Run(BG_EFFECTS_FADE_SEC,
                k => SetBgEffectsProgress(Mathf.Lerp(BG_EFFECTS_INTRO_PROGRESS, 1f, k)), Ease.InOutSine));
        }

        IEnumerator BgIntroNextCo()
        {
            _zoom = BG_END_ZOOM;
            ApplyZoom();
            SetBgEffectsProgress(0);
            SetActive(_background, true);
            SetAlpha(_scrim, SCRIM_END_ALPHA);
            SetActive(_underwater, true);
            SetAlpha(_underwater, 1f);
            float t = 0;
            while (t < TRANSITION_SEC)
            {
                t += Time.deltaTime;
                float raw = Mathf.Clamp01(t / TRANSITION_SEC);
                SetAlpha(_underwater, Mathf.Lerp(1, 0, Tween.Evaluate(Ease.OutCubic, raw)));
                SetBgEffectsProgress(Tween.Evaluate(Ease.OutSine, raw));
                yield return null;
            }
            SetActive(_underwater, false);
            SetAlpha(_underwater, 1f);
        }

        public void PlaySettleToCompleteBackdrop()
        {
            FadeOutGameplayContent();
            if (_introCo != null) StopCoroutine(_introCo);

            // Keep the normal full-screen background as an opaque base while
            // the completion background fades in.  Godot's underwater sprite
            // covers the expanded viewport exactly, but Unity can expose the
            // camera clear behind its top edge for a frame/aspect combination
            // when the HUD slides out.  The base layer is visually hidden by
            // the opaque underwater image and prevents that white strip.
            SetActive(_background, true);
            SetAlpha(_background, 1f);
            ApplyCover(_background, PSD_GAMEPLAY_BACKGROUND_ZOOM);

            if (_underwater == null)
            {
                FinishSettleToCompleteBackdrop();
                return;
            }

            SetActive(_underwater, true);
            SetAlpha(_underwater, 0f);
            ApplyCover(_underwater, _zoom);
            StartCoroutine(Tween.Run(
                TRANSITION_SEC,
                k => SetAlpha(_underwater, k),
                Ease.OutCubic,
                FinishSettleToCompleteBackdrop));
        }

        void FinishSettleToCompleteBackdrop()
        {
            // Leave the base active for the complete screen. The next level
            // transition already reuses it. Respect a deleted/cleared scrim.
            SetActive(_background, true);
            if (_scrim != null && _scrim.sprite != null)
            {
                _scrim.sprite = ScrimSpriteOld();
                RestretchScrim();
                SetAlpha(_scrim, 0f);
            }
        }

        public void FadeOutGameplayContent()
        {
            StartCoroutine(FadeGroupCo(0f, GAMEPLAY_FADE_OUT_SEC));
        }

        public void ResetGameplayContentAlpha()
        {
            SetGroupAlpha(Page?.Field?.Container, 1f);
            Ambient?.SetGroupAlpha(1f);
            MidDepth?.SetGroupAlpha(1f);
        }

        IEnumerator FadeGroupCo(float to, float dur)
        {
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Tween.Evaluate(Ease.OutSine, Mathf.Clamp01(t / dur));
                float a = Mathf.Lerp(1f, to, k);
                SetGroupAlpha(Page?.Field?.Container, a);
                Ambient?.SetGroupAlpha(a);
                MidDepth?.SetGroupAlpha(a);
                yield return null;
            }
        }

        void SetGroupAlpha(Transform root, float a)
        {
            if (root == null) return;
            var stale = new List<SpriteRenderer>();
            foreach (var pair in _gameplayBaseAlpha)
                if (pair.Key == null) stale.Add(pair.Key);
            foreach (var key in stale) _gameplayBaseAlpha.Remove(key);

            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!_gameplayBaseAlpha.TryGetValue(sr, out float baseAlpha))
                {
                    baseAlpha = sr.color.a;
                    _gameplayBaseAlpha[sr] = baseAlpha;
                }
                var c = sr.color;
                c.a = baseAlpha * Mathf.Clamp01(a);
                sr.color = c;
            }
        }

        public void OnCompleteReveal()
        {
            if (_effectsCo != null) StopCoroutine(_effectsCo);
            SetBgEffectsProgress(0f);
            if (LightRays != null)
            {
                LightRays.gameObject.SetActive(true);
                LightRays.SetGroupAlpha(0);
                StartCoroutine(Tween.Run(
                    COMPLETE_LIGHTS_FADE_IN_SEC,
                    k => LightRays.SetGroupAlpha(k),
                    Ease.OutSine));
            }
            // Completion presents the large centre idle_1 pose directly.
            // Keep the subtle light rays, but do not add either water-curtain
            // treatment (the legacy waves Spine or the caustic wave band).
            Page?.HideCompletionWaveBand();
        }

        public void HideCompleteFx()
        {
            SetActive(LightRays, false);
            Page?.HideCompletionWaveBand();
        }

        // ------------------------------------------------------------ bars
        void EnsureBarsRestCached()
        {
            if (_barsRestCached) return;
            _topBarRest = Page.TopBar.Root.anchoredPosition;
            _toolbarRest = Page.Toolbar.Root.anchoredPosition;
            _barsRestCached = true;
        }

        public void InvalidateBarsRestCache() { _barsRestCached = false; }

        public void ResetBarsToRest()
        {
            EnsureBarsRestCached();
            ApplyTopBarPosition(Page.TopBar.Root, _topBarRest);
            Page.Toolbar.Root.anchoredPosition = _toolbarRest;
        }

        void ApplyTopBarPosition(RectTransform top, Vector2 position)
        {
            top.anchoredPosition = position;
            Page?.TopBar?.Dolphin?.SetHudBarOffset(
                position.y - _topBarRest.y);
        }

        public Coroutine TopbarSlideCoroutine { get; private set; }

        public void PlayBarsTransition(bool slideIn)
        {
            EnsureBarsRestCached();
            var top = Page.TopBar.Root;
            var tool = Page.Toolbar.Root;
            Vector2 topOff = _topBarRest + new Vector2(0, TopGameBar.TOPBAR_HEIGHT + 20);
            float toolbarExitDistance =
                ToolbarView.GetExitSlideDistance(tool);
            Vector2 toolOff = _toolbarRest -
                new Vector2(0, toolbarExitDistance);
            if (_topbarCo != null) StopCoroutine(_topbarCo);
            if (_toolbarCo != null) StopCoroutine(_toolbarCo);
            if (slideIn)
            {
                ApplyTopBarPosition(top, topOff);
                tool.anchoredPosition = toolOff;
                _topbarCo = StartCoroutine(Tween.Run(
                    TRANSITION_SEC,
                    k => ApplyTopBarPosition(
                        top,
                        Vector2.Lerp(topOff, _topBarRest, k)),
                    Ease.OutCubic));
                _toolbarCo = StartCoroutine(Tween.Run(TRANSITION_SEC, k => tool.anchoredPosition = Vector2.Lerp(toolOff, _toolbarRest, k), Ease.OutCubic));
            }
            else
            {
                var fromT = top.anchoredPosition;
                var fromB = tool.anchoredPosition;
                _topbarCo = StartCoroutine(Tween.Run(
                    TRANSITION_SEC,
                    k => ApplyTopBarPosition(
                        top,
                        Vector2.Lerp(fromT, topOff, k)),
                    Ease.InCubic));
                _toolbarCo = StartCoroutine(Tween.Run(TRANSITION_SEC, k => tool.anchoredPosition = Vector2.Lerp(fromB, toolOff, k), Ease.InCubic));
            }
            TopbarSlideCoroutine = _topbarCo;
        }
    }
}
