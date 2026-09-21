using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BubblePics.GameModes;
using TMPro;
using UnityEngine;

namespace BubblePics
{
    public enum BubbleState { Alive, Merging, Exploding, Dead }

    /// <summary>
    /// Port of bubble_view.gd. World space uses design pixels with y-down mapped
    /// to Unity y-up via BubbleField.D2W. All radii in design pixels.
    /// </summary>
    public class BubbleView : MonoBehaviour
    {
        public static readonly float[] RADIUS_SCALE = { 1.0f, 1.0f, 1.4142f, 1.7321f, 2.0f };
        public const float FRAME_SAFETY_PX = 2.0f;
        public const float ENLARGE_RATIO_YES2 = 1.06f;
        // Keep ordinary photo pieces a little farther inside the glass. This
        // compensates for jigsaw tabs and the restored 1.06 merge enlargement
        // without changing bubble physics or special-mode content sizing.
        public const float PHOTO_CONTENT_SCALE = 0.96f;
        public const float CORNER_RADIUS_UV = 0.08f;
        public const float YES2_CORNER_PADDING_RATIO = 0.03f;
        public static readonly float[] INSCRIBE_RATIO_BY_COUNT = { 0.92f, 0.92f, 0.94f, 0.96f, 0.98f };
        public const float PICKUP_ENLARGE_UP_DUR = 0.35f;
        public const float PICKUP_ENLARGE_DOWN_DUR = 0.2f;
        public const float GLASS_VISUAL_OVERSCAN = 1.005f;
        public const int DRAG_Z_INDEX = 15;
        public const int ENLARGE_HOVER_Z_INDEX = 3;
        public const float FALL_TRAIL_SPEED_THRESHOLD = 150f;
        public const float COLLIDE_SFX_SPEED_MIN = 500f;
        public const int CLOSURE_PRE_HIDE_FRAMES = 0;
        public const float CLOSURE_SPARKLE_LIFETIME_SEC = 1.8f;
        public const int CLOSURE_RING_TOTAL_FRAMES = 40;
        public const float CLOSURE_RING_SIZE_MULT = 1.0f;

        // glow ratios
        public const float HINT_GLOW_VISIBLE_RATIO = 1.28f;
        public const float HINT_GLOW_FADE_IN = 0.1f;
        public const float HINT_GLOW_FADE_OUT = 0.18f;
        public const float BUBBLE_GLOW_VISIBLE_RATIO = 1.18f;
        public const float AIMING_VISIBLE_RATIO = 1.28f;
        public const float AIMING_OFFSET_RATIO = 0.008f;
        public const float GLOW_DRAG_ALPHA = 0.76f;
        public const float GLOW_TARGET_ALPHA = 1.0f;
        public const float GLOW_FADE_IN = 0.1f;
        public const float GLOW_FADE_OUT = 0.18f;
        public const float REJECT_FLASH_VISIBLE_RATIO = 1.0f;

        // float wander
        public const float FLOAT_RANGE_RATIO = 0.05f;
        public const float FLOAT_MIN_SPEED = 2.5f;
        public const float FLOAT_MAX_SPEED = 7.0f;
        public const float FLOAT_DIR_INTERVAL = 3.5f;
        public const float FLOAT_TURN_LERP = 1.2f;

        // jitter guard
        const float JITTER_WINDOW = 1.0f;
        const float JITTER_SPEED_MIN = 15f;
        const float JITTER_DISP_MAX = 12f;
        const int CALM_FRAMES = 30;
        const float CALM_VEL_FACTOR = 0.5f;
        const float CALM_ANG_FACTOR = 0.5f;
        const float CALM_MAX_SPEED = 120f;
        const float NUDGE_MAX_PX = 6f;
        // The recovered Godot solver permits up to 3 px of contact penetration.
        // Treating that normal contact slop as a stuck overlap makes a resting
        // pile reposition itself once per jitter window and wakes the whole pile.
        const float ESCAPE_NUDGE_MIN_OVERLAP_PX = 3f;
        const float SETTLED_SLEEP_MAX_SPEED = 35f;
        const float SETTLED_SLEEP_MAX_ANGULAR_SPEED = 5f;
        const float SOFT_PUSH_NEIGHBOR_DAMP_FACTOR = 0.88f;
        const float SOFT_PUSH_CAP_TAIL_SEC = 0.25f;

        public int ImageId = -1;
        public BubbleFragment Fragment;
        public Texture2D SourceTexture;
        public BubbleState State = BubbleState.Alive;
        public bool Dragging;
        public bool ReturningHome;
        public Vector3 DragOrigin;
        public float BaseRadius = 72f;
        public bool HasLanded;
        public bool IsLocked;
        public int LockCount { get; private set; }
        public bool IsBomb { get; private set; }
        public int BombCount { get; private set; }
        public int BombInitialCount { get; private set; }
        public float BombSecondsLeft { get; private set; }
        public bool IsStarfish { get; private set; }
        public float StarfishSecondsLeft { get; private set; }
        public bool IsMagnetBubble { get; private set; }

        [Header("Prefab authoring")]
        [SerializeField] public Rigidbody2D Body;
        [SerializeField] public CircleCollider2D Collider;
        [SerializeField] public Transform VisualRoot;
        [SerializeField] public Transform ImagesRoot;
        [SerializeField] public SpriteRenderer BubbleGlow;       // active_bubble
        [SerializeField] public SpriteRenderer OperatedMarkGlow; // active_bubble orange
        [SerializeField] public SpriteRenderer BubbleGlowTarget; // aiming_bubble
        [SerializeField] public SpriteRenderer HintGlow;         // hint_glow
        [SerializeField] public SpriteRenderer BubbleSprite;     // glass
        [SerializeField] public SpriteRenderer RejectFlash;      // reject_flash
        [SerializeField] public ParticleSystem FallTrail;
        [SerializeField] SpriteRenderer[] _imageSlots = new SpriteRenderer[4];

        static Material _fragmentImageMat;
        static Material _stickerImageMat;
        static Material _defaultSpriteMat;
        static Material _tangramLineMaterial;
        static Material _glassBaseMat;
        static PhysicsMaterial2D _bubblePhysicsMat;
        static Texture2D _dissolveNoise;
        static float _collideMuteUntil;
        static bool _fallbackWarningShown;

        bool _boundsSet;
        float _boundLeft, _boundRight, _boundFloor; // design coords
        GameObject _placeholder;
        Vector3 _visualBase;
        Vector2 _floatOrigin, _floatOffset, _floatDir, _floatTargetDir;
        float _floatSpeed, _floatTargetSpeed, _floatTimer;
        float _floatLim = -1f;
        bool _pickupEnlarged, _pickupZRaised;
        Coroutine _pickupCo, _blockSizeCo, _glowCo, _glowTargetCo, _hintCo, _hintGlowCo, _rippleCo, _rejectCo, _shakeCo;
        float _blockSizeTargetRadius;
        Vector2 _prevVelocity;
        Vector2 _jitWinStartPos;
        float _jitWinTime, _jitSpeedAccum;
        int _jitSamples, _calmFrames, _softPushCapFrames;
        readonly ContactPoint2D[] _settledContactBuffer = new ContactPoint2D[8];
        int _savedLayer;
        Material _runtimeGlassMaterial;
        Material _ownedFallTrailMaterial;
        BubbleSpecialVisual _specialVisual;

        public static void MuteCollideFor(float sec)
        {
            float until = Time.time + sec;
            if (until > _collideMuteUntil) _collideMuteUntil = until;
        }

        public static Material FragmentImageMaterial()
        {
            if (_fragmentImageMat == null)
                _fragmentImageMat = new Material(Shader.Find("BubblePics/FragmentImage"));
            return _fragmentImageMat;
        }

        public static Material StickerImageMaterial()
        {
            if (_stickerImageMat == null)
                _stickerImageMat = new Material(Shader.Find("BubblePics/StickerImage"));
            return _stickerImageMat;
        }

        public static Material DefaultSpriteMaterial()
        {
            if (_defaultSpriteMat != null) return _defaultSpriteMat;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("BubbleView: Sprites/Default shader is unavailable.");
                return null;
            }
            _defaultSpriteMat = new Material(shader)
            {
                name = "BubblePics Runtime Sprite",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return _defaultSpriteMat;
        }

        public static Texture2D DissolveNoise()
        {
            if (_dissolveNoise != null) return _dissolveNoise;
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.R8, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            // Perlin approximation of FastNoiseLite freq 0.035, 3 octaves
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = 0f, amp = 1f, freq = 0.035f, tot = 0f;
                    for (int o = 0; o < 3; o++)
                    {
                        n += Mathf.PerlinNoise(x * freq + 13.7f, y * freq + 71.3f) * amp;
                        tot += amp;
                        amp *= 0.5f; freq *= 2f;
                    }
                    n /= tot;
                    tex.SetPixel(x, y, new Color(n, n, n, 1));
                }
            }
            tex.Apply();
            _dissolveNoise = tex;
            return tex;
        }

        public static Texture2D ActiveBubblePicTex() => AssetLib.Texture(
            "Art/Sprites/PsdSkin20260807/Gameplay/bubble_frame");

        // ------------------------------------------------------------ setup
        public static BubbleView Create(Transform parent, BubbleFragment frag, Texture2D texture, float baseRadius)
        {
            BubbleView b = null;
            var catalog = PrefabCatalog.Current;
            if (catalog != null && catalog.BubbleEntity != null)
                b = PrefabCatalog.InstantiateComponent<BubbleView>(catalog.BubbleEntity, parent);

            if (b != null)
            {
                b.ResolveAuthoringReferences();
                if (!b.HasCompleteAuthoringSetup())
                {
                    Debug.LogError(
                        $"Bubble prefab '{catalog.BubbleEntity.name}' is missing its fixed BubbleView references. " +
                        "Rebuild the prefab with BubbleView.SetupPrefabAuthoring(). Falling back to compatibility construction.");
                    Destroy(b.gameObject);
                    b = null;
                }
            }

            if (b == null)
            {
                if (!_fallbackWarningShown)
                {
                    _fallbackWarningShown = true;
                    Debug.LogWarning(
                        "PrefabCatalog.Current.BubbleEntity is unavailable; BubbleView is using the compatibility " +
                        "authoring fallback. Generated/runtime catalogs should assign the BubbleEntity prefab.");
                }
                var go = new GameObject("Bubble");
                go.transform.SetParent(parent, false);
                b = go.AddComponent<BubbleView>();
                b.SetupPrefabAuthoring();
            }

            b.Initialize(frag, texture, baseRadius);
            return b;
        }

        /// <summary>
        /// Assigns runtime data to a prefab-authored bubble. The hierarchy and fixed
        /// physics/render components must already exist on the prefab.
        /// </summary>
        public void Initialize(BubbleFragment frag, Texture2D texture, float baseRadius)
        {
            ResolveAuthoringReferences();
            if (!HasCompleteAuthoringSetup())
            {
                Debug.LogError($"{name}: BubbleView.Initialize requires a complete authored BubbleEntity hierarchy.");
                return;
            }

            SourceTexture = texture;
            BaseRadius = baseRadius;
            Fragment = frag;
            ImageId = frag != null ? frag.ImageId : -1;
            State = BubbleState.Alive;
            Dragging = false;
            ReturningHome = false;
            HasLanded = false;
            IsLocked = false;
            LockCount = 0;
            IsBomb = false;
            BombCount = 0;
            BombInitialCount = 0;
            BombSecondsLeft = 0f;
            IsStarfish = false;
            StarfishSecondsLeft = 0f;
            IsMagnetBubble = false;

            ConfigureBodyDefaults();
            if (_bubblePhysicsMat == null)
                _bubblePhysicsMat = new PhysicsMaterial2D("bubble") { bounciness = 0.15f, friction = 0.1f };
            Body.sharedMaterial = _bubblePhysicsMat;
            Collider.enabled = true;

            EnsureRuntimeSpriteAssets();
            EnsureGlassMaterial();
            VisualRoot.gameObject.SetActive(true);
            VisualRoot.localScale = Vector3.one;
            ImagesRoot.gameObject.SetActive(true);
            SetAlpha(BubbleGlow, 0f);
            OperatedMarkGlow.color = new Color(1f, 0.647f, 0f, 0f);
            SetAlpha(BubbleGlowTarget, 0f);
            SetAlpha(HintGlow, 0f);
            SetAlpha(BubbleSprite, 1f);
            SetAlpha(RejectFlash, 0f);
            StopFallTrail(clear: true);

            _visualBase = VisualRoot.localPosition;
            _floatOrigin = ImagesRoot.localPosition;
            _floatOffset = Vector2.zero;
            _floatTimer = 0f;
            PickNewFloatDirection();
            _floatDir = _floatTargetDir;
            _floatSpeed = _floatTargetSpeed;
            Refresh();
            BubbleSpecialMechanics.NotifyBubbleSpawned(this);
        }

        /// <summary>
        /// Compatibility/editor-authoring helper. Prefab generation can invoke this
        /// once before saving BubbleEntity; normal runtime creation instantiates the
        /// authored prefab and does not add these fixed components.
        /// </summary>
        public void SetupPrefabAuthoring()
        {
            ResolveAuthoringReferences();
            if (Body == null) Body = gameObject.AddComponent<Rigidbody2D>();
            if (Collider == null) Collider = gameObject.AddComponent<CircleCollider2D>();

            if (VisualRoot == null)
            {
                VisualRoot = new GameObject("Visual").transform;
                VisualRoot.SetParent(transform, false);
            }

            BubbleGlow = GetOrCreateSprite(BubbleGlow, VisualRoot, "BubbleGlow", "Art/Sprites/Bubble/active_bubble", 0);
            OperatedMarkGlow = GetOrCreateSprite(OperatedMarkGlow, VisualRoot, "OperatedMarkGlow", "Art/Sprites/Bubble/active_bubble", 1);
            BubbleGlowTarget = GetOrCreateSprite(BubbleGlowTarget, VisualRoot, "BubbleGlowTarget", "Art/Sprites/Bubble/aiming_bubble", 2);
            HintGlow = GetOrCreateSprite(HintGlow, VisualRoot, "HintGlow", "Art/Sprites/Bubble/hint_glow", 3);
            BubbleSprite = GetOrCreateSprite(
                BubbleSprite,
                VisualRoot,
                "BubbleSprite",
                "Art/Sprites/PsdSkin20260807/Gameplay/bubble_frame",
                4);

            if (ImagesRoot == null)
            {
                ImagesRoot = new GameObject("Images").transform;
                ImagesRoot.SetParent(VisualRoot, false);
            }

            if (_imageSlots == null || _imageSlots.Length != 4)
                _imageSlots = new SpriteRenderer[4];
            for (int i = 0; i < _imageSlots.Length; i++)
            {
                _imageSlots[i] = GetOrCreateSprite(_imageSlots[i], ImagesRoot, "P" + i, null, 5);
                _imageSlots[i].gameObject.SetActive(false);
            }

            RejectFlash = GetOrCreateSprite(RejectFlash, VisualRoot, "RejectFlash", "Art/Sprites/Bubble/reject_flash", 6);
            ConfigureFallTrailPrefabAuthoring();
            ConfigureBodyDefaults();
            Collider.radius = BaseRadius;

            SetAlpha(BubbleGlow, 0f);
            OperatedMarkGlow.color = new Color(1f, 0.647f, 0f, 0f);
            SetAlpha(BubbleGlowTarget, 0f);
            SetAlpha(HintGlow, 0f);
            SetAlpha(RejectFlash, 0f);
        }

        /// <summary>
        /// Authors the fixed FallTrail child from bubble_entity.tscn. Passing a
        /// persistent material is used by the focused prefab migration; the
        /// compatibility builder owns a temporary runtime material instead.
        /// </summary>
        public void ConfigureFallTrailPrefabAuthoring(Material sharedMaterial = null)
        {
            if (FallTrail == null)
            {
                var child = transform.Find("FallTrail");
                if (child == null)
                {
                    var go = new GameObject("FallTrail");
                    child = go.transform;
                    child.SetParent(transform, false);
                    child.SetSiblingIndex(0);
                }
                FallTrail = child.GetComponent<ParticleSystem>();
                if (FallTrail == null)
                    FallTrail = child.gameObject.AddComponent<ParticleSystem>();
            }

            var material = sharedMaterial;
            if (material == null)
            {
                var renderer = FallTrail.GetComponent<ParticleSystemRenderer>();
                material = renderer != null ? renderer.sharedMaterial : null;
            }
            if (material == null)
            {
                material = BubbleParticleAuthoring.CreateRuntimeMaterial();
                _ownedFallTrailMaterial = material;
            }
            BubbleParticleAuthoring.ConfigureFallTrail(FallTrail, material);
        }

        void ConfigureBodyDefaults()
        {
            Body.gravityScale = 1.3f * 1.3f; // godot gravity 980*1.3; unity default -9.81*100 => match below
            Body.mass = 1f;
            Body.drag = 0.65f;
            Body.angularDrag = 2.0f;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            // Unity's Box2D solver otherwise keeps every bubble awake under
            // pixel-scaled gravity. Sleeping bodies are still woken by a real
            // collision or when their supporting contact changes.
            Body.sleepMode = RigidbodySleepMode2D.StartAwake;
            // The collider is circular and all visible contents are kept upright,
            // so physical rotation only adds an unnecessary jitter degree of freedom.
            Body.constraints = RigidbodyConstraints2D.FreezeRotation;
            // Godot: gravity 980 px/s^2 * gravity_scale 1.3 = 1274 px/s^2 downward.
            // Unity Physics2D.gravity is set to (0,-980) by App; scale 1.3 here.
            Body.gravityScale = 1.3f;
        }

        void EnsureGlassMaterial()
        {
            if (_glassBaseMat == null)
            {
                _glassBaseMat = new Material(Shader.Find("BubblePics/BubbleGlass"));
                _glassBaseMat.SetTexture("_BaseTex", ActiveBubblePicTex());
            }
            if (_runtimeGlassMaterial != null) Destroy(_runtimeGlassMaterial);
            _runtimeGlassMaterial = new Material(_glassBaseMat);
            BubbleSprite.sharedMaterial = _runtimeGlassMaterial;
        }

        void EnsureRuntimeSpriteAssets()
        {
            AssignSpriteIfMissing(BubbleGlow, "Art/Sprites/Bubble/active_bubble");
            AssignSpriteIfMissing(OperatedMarkGlow, "Art/Sprites/Bubble/active_bubble");
            AssignSpriteIfMissing(BubbleGlowTarget, "Art/Sprites/Bubble/aiming_bubble");
            AssignSpriteIfMissing(HintGlow, "Art/Sprites/Bubble/hint_glow");
            AssignSpriteIfMissing(
                BubbleSprite,
                "Art/Sprites/PsdSkin20260807/Gameplay/bubble_frame");
            AssignSpriteIfMissing(RejectFlash, "Art/Sprites/Bubble/reject_flash");
        }

        static void AssignSpriteIfMissing(SpriteRenderer renderer, string resourcePath)
        {
            if (renderer != null && renderer.sprite == null)
                renderer.sprite = AssetLib.Sprite(resourcePath);
        }

        void ResolveAuthoringReferences()
        {
            if (Body == null) Body = GetComponent<Rigidbody2D>();
            if (Collider == null) Collider = GetComponent<CircleCollider2D>();
            if (VisualRoot == null) VisualRoot = transform.Find("Visual");
            if (VisualRoot == null) return;

            if (BubbleGlow == null) BubbleGlow = FindSprite(VisualRoot, "BubbleGlow");
            if (OperatedMarkGlow == null) OperatedMarkGlow = FindSprite(VisualRoot, "OperatedMarkGlow");
            if (BubbleGlowTarget == null) BubbleGlowTarget = FindSprite(VisualRoot, "BubbleGlowTarget");
            if (HintGlow == null) HintGlow = FindSprite(VisualRoot, "HintGlow");
            if (BubbleSprite == null) BubbleSprite = FindSprite(VisualRoot, "BubbleSprite");
            if (RejectFlash == null) RejectFlash = FindSprite(VisualRoot, "RejectFlash");
            if (ImagesRoot == null) ImagesRoot = VisualRoot.Find("Images");
            if (FallTrail == null)
            {
                var child = transform.Find("FallTrail");
                if (child != null) FallTrail = child.GetComponent<ParticleSystem>();
            }

            if (_imageSlots == null || _imageSlots.Length != 4)
                _imageSlots = new SpriteRenderer[4];
            if (ImagesRoot != null)
            {
                for (int i = 0; i < _imageSlots.Length; i++)
                    if (_imageSlots[i] == null) _imageSlots[i] = FindSprite(ImagesRoot, "P" + i);
            }
        }

        bool HasCompleteAuthoringSetup()
        {
            if (Body == null || Collider == null || VisualRoot == null || ImagesRoot == null ||
                BubbleGlow == null || OperatedMarkGlow == null || BubbleGlowTarget == null ||
                HintGlow == null || BubbleSprite == null || RejectFlash == null ||
                FallTrail == null ||
                _imageSlots == null || _imageSlots.Length != 4)
                return false;
            for (int i = 0; i < _imageSlots.Length; i++)
                if (_imageSlots[i] == null) return false;
            return true;
        }

        static SpriteRenderer FindSprite(Transform parent, string childName)
        {
            var child = parent != null ? parent.Find(childName) : null;
            return child != null ? child.GetComponent<SpriteRenderer>() : null;
        }

        SpriteRenderer GetOrCreateSprite(
            SpriteRenderer current, Transform parent, string childName, string resourcePath, int order)
        {
            var sr = current != null ? current : FindSprite(parent, childName);
            if (sr == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(parent, false);
                sr = go.AddComponent<SpriteRenderer>();
            }
            if (!string.IsNullOrEmpty(resourcePath) && sr.sprite == null)
            {
                // Resources.Load only returns an imported (persistent) Sprite. If
                // the texture is still imported as Default, leave the authoring
                // reference empty rather than embedding AssetLib's transient
                // Sprite; Initialize will safely create/load it at runtime.
                sr.sprite = ResourceAssetLoader.Load<Sprite>(resourcePath);
                if (Application.isPlaying && sr.sprite == null)
                    sr.sprite = AssetLib.Sprite(resourcePath);
            }
            sr.sortingOrder = SortOrder.BubbleBand(0, order);
            return sr;
        }

        /// <summary>Godot z emulation. Bubble container z=1; bubble local z on top.
        /// Effective order = (1 + z) * 20 + intra(0..9).</summary>
        public void SetSortingBase(int z)
        {
            ApplyIntra(BubbleGlow, z, 0);
            ApplyIntra(OperatedMarkGlow, z, 1);
            ApplyIntra(BubbleGlowTarget, z, 2);
            ApplyIntra(HintGlow, z, 3);
            ApplyIntra(BubbleSprite, z, 4);
            ApplyIntra(RejectFlash, z, 6);
            foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
                mr.sortingOrder = SortOrder.BubbleBand(z, 4); // merge spine fx sits under images
            foreach (var p in Pieces)
            {
                if (p.Sr != null)
                    p.Sr.sortingOrder = SortOrder.BubbleBand(z, 5);
                if (p.Mr != null)
                    p.Mr.sortingOrder = SortOrder.BubbleBand(z, 5);
            }
            _specialVisual?.SetSortingBase(z);
        }

        static void ApplyIntra(SpriteRenderer sr, int z, int intra)
        {
            if (sr != null) sr.sortingOrder = SortOrder.BubbleBand(z, intra);
        }

        int _zIndex;
        public int ZIndex
        {
            get => _zIndex;
            set { _zIndex = value; SetSortingBase(value); }
        }

        // ------------------------------------------------------------ static geometry helpers
        public static bool[] NeighborsFor(int q, List<int> held)
        {
            bool hasTop = (q == 2 && held.Contains(0)) || (q == 3 && held.Contains(1));
            bool hasBottom = (q == 0 && held.Contains(2)) || (q == 1 && held.Contains(3));
            bool hasLeft = (q == 1 && held.Contains(0)) || (q == 3 && held.Contains(2));
            bool hasRight = (q == 0 && held.Contains(1)) || (q == 2 && held.Contains(3));
            return new[] { hasTop, hasBottom, hasLeft, hasRight };
        }

        public static int ComputeCornerMask(int q, List<int> held, bool fullImage)
        {
            if (fullImage) return 15;
            var n = NeighborsFor(q, held);
            bool hasTop = n[0], hasBottom = n[1], hasLeft = n[2], hasRight = n[3];
            int mask = 0;
            if (!hasTop && !hasLeft) mask |= 1;
            if (!hasTop && !hasRight) mask |= 2;
            if (!hasBottom && !hasLeft) mask |= 4;
            if (!hasBottom && !hasRight) mask |= 8;
            if (held.Contains(3 - q)) mask &= ~(1 << (3 - q));
            return mask;
        }

        public static int ComputeEdgeMask(int q, List<int> held, bool fullImage)
        {
            if (fullImage) return 15;
            var n = NeighborsFor(q, held);
            int mask = 0;
            if (!n[0]) mask |= 1;
            if (!n[3]) mask |= 2;
            if (!n[1]) mask |= 4;
            if (!n[2]) mask |= 8;
            return mask;
        }

        public static int FillCornerMaskForPiece(int q, List<int> held, bool fullImage)
        {
            if (fullImage || held.Count == 0) return 15;
            int minCol = 2, maxCol = -1, minRow = 2, maxRow = -1;
            foreach (int qq in held)
            {
                int c = (qq == 1 || qq == 3) ? 1 : 0;
                int rw = (qq == 2 || qq == 3) ? 1 : 0;
                minCol = Mathf.Min(minCol, c); maxCol = Mathf.Max(maxCol, c);
                minRow = Mathf.Min(minRow, rw); maxRow = Mathf.Max(maxRow, rw);
            }
            int col = (q == 1 || q == 3) ? 1 : 0;
            int row = (q == 2 || q == 3) ? 1 : 0;
            int mask = 0;
            if (col == minCol && row == minRow) mask |= 1;
            if (col == maxCol && row == minRow) mask |= 2;
            if (col == minCol && row == maxRow) mask |= 4;
            if (col == maxCol && row == maxRow) mask |= 8;
            return mask;
        }

        static float BboxWhSum(List<int> quads, bool fullImage)
        {
            if (fullImage || quads.Count == 0) return 2.0f;
            int minCol = 2, maxCol = -1, minRow = 2, maxRow = -1;
            foreach (int q in quads)
            {
                int col = (q == 1 || q == 3) ? 1 : 0;
                int row = (q == 2 || q == 3) ? 1 : 0;
                minCol = Mathf.Min(minCol, col); maxCol = Mathf.Max(maxCol, col);
                minRow = Mathf.Min(minRow, row); maxRow = Mathf.Max(maxRow, row);
            }
            return (maxCol - minCol + 1) + (maxRow - minRow + 1);
        }

        public static float Yes2FillCornerUv(float r0, float ratio, float enlarge, float cell, float overhangPx, float floorUv, float whSum)
        {
            float s = cell * enlarge;
            if (s <= 0) return floorUv;
            float d = r0 * ratio * enlarge;
            float rEff = r0 * GLASS_VISUAL_OVERSCAN - FRAME_SAFETY_PX - overhangPx - r0 * YES2_CORNER_PADDING_RATIO;
            float k = -whSum * s * 0.5f;
            float bb = k + rEff;
            float disc = bb * bb - (d * d - rEff * rEff);
            float cr;
            if (disc >= 0)
            {
                cr = -bb - Mathf.Sqrt(disc);
                if (cr < 0) cr = -bb + Mathf.Sqrt(disc);
            }
            else
            {
                float disc2 = 2f * d * d - k * k;
                cr = (-k - Mathf.Sqrt(Mathf.Max(0, disc2))) * 0.5f;
            }
            return Mathf.Clamp(cr / s, floorUv, 0.5f);
        }

        public static float QuadrantCell(List<int> quads, float r, int countForRatio = -1)
        {
            int minCol = 2, maxCol = -1, minRow = 2, maxRow = -1;
            foreach (int q in quads)
            {
                int col = (q == 1 || q == 3) ? 1 : 0;
                int row = (q == 2 || q == 3) ? 1 : 0;
                minCol = Mathf.Min(minCol, col); maxCol = Mathf.Max(maxCol, col);
                minRow = Mathf.Min(minRow, row); maxRow = Mathf.Max(maxRow, row);
            }
            float w = maxCol - minCol + 1;
            float h = maxRow - minRow + 1;
            int n = countForRatio >= 0 ? countForRatio : quads.Count;
            float ratio = INSCRIBE_RATIO_BY_COUNT[Mathf.Clamp(n, 0, 4)];
            return 2f * r * ratio / Mathf.Sqrt(w * w + h * h);
        }

        /// <summary>Design-space offset (y down). Convert with V2W when placing in Unity.</summary>
        public static Vector2 QuadrantOffset(int q, List<int> quads, float cell)
        {
            float half = cell * 0.5f;
            var slot = new Vector2(
                (q == 0 || q == 2) ? -half : half,
                (q == 0 || q == 1) ? -half : half);
            float minSx = 1, maxSx = -1, minSy = 1, maxSy = -1;
            foreach (int qq in quads)
            {
                float sx = (qq == 0 || qq == 2) ? -1 : 1;
                float sy = (qq == 0 || qq == 1) ? -1 : 1;
                minSx = Mathf.Min(minSx, sx); maxSx = Mathf.Max(maxSx, sx);
                minSy = Mathf.Min(minSy, sy); maxSy = Mathf.Max(maxSy, sy);
            }
            var bboxCenter = new Vector2((minSx + maxSx) * 0.5f, (minSy + maxSy) * 0.5f) * half;
            return slot - bboxCenter;
        }

        public static bool CanMerge(BubbleFragment fa, BubbleFragment fb)
        {
            return BubbleFragment.CanMerge(fa, fb);
        }

        static bool PathsEqual(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public static List<List<int>> MergeResult(BubbleFragment selfFrag, List<List<int>> incomingHeld)
        {
            var parent = selfFrag.ParentPath();
            var quads = selfFrag.LastQuadrants();
            foreach (var p in incomingHeld)
            {
                if (p.Count > 0)
                {
                    int q = p[p.Count - 1];
                    if (!quads.Contains(q)) quads.Add(q);
                }
            }
            var outList = new List<List<int>>();
            if (quads.Count >= 4)
            {
                outList.Add(new List<int>(parent));
                return outList;
            }
            foreach (int q in quads)
            {
                var np = new List<int>(parent) { q };
                outList.Add(np);
            }
            return outList;
        }

        public bool CanMergeWith(BubbleView other)
        {
            if (other == null || other == this) return false;
            if (State != BubbleState.Alive || other.State != BubbleState.Alive) return false;
            if (IsLocked || other.IsLocked) return false;
            if (IsStarfish || other.IsStarfish) return false;
            if ((Fragment != null && Fragment.IsRainbow) ||
                (other.Fragment != null && other.Fragment.IsRainbow))
                return true;
            return CanMerge(Fragment, other.Fragment);
        }

        public void MergeWithDataOnly(BubbleView other)
        {
            bool stickerPair = BubbleFragment.IsStickerPair(Fragment, other.Fragment);
            if (stickerPair)
            {
                Fragment.PendingStickerReturn = false;
                Fragment.StickerHalf = 0;
            }
            else if (!Fragment.PendingStickerReturn &&
                     other.Fragment.PendingStickerReturn)
            {
                Fragment.PendingStickerReturn = true;
                Fragment.StickerShapeId = other.Fragment.StickerShapeId;
                Fragment.StickerDonorPath =
                    new List<int>(other.Fragment.StickerDonorPath);
                Fragment.StickerHalf = other.Fragment.StickerHalf;
            }
            Fragment.TangramMold =
                Fragment.TangramMold || other.Fragment.TangramMold;
            Fragment.HeldPaths = MergeResult(Fragment, other.Fragment.HeldPaths);
            if (other.IsBomb) other.TransferBombStateTo(this);
            if (other.IsMagnetBubble) IsMagnetBubble = true;
        }

        static readonly int[][] DIAGONAL_QUAD_PAIRS = { new[] { 0, 3 }, new[] { 1, 2 } };

        public static int RadiusBlockCount(List<int> quads, bool isNumber = false)
        {
            if (isNumber) return quads.Count;
            if (quads.Count == 2)
            {
                var s = new List<int>(quads);
                s.Sort();
                foreach (var pair in DIAGONAL_QUAD_PAIRS)
                    if (s[0] == pair[0] && s[1] == pair[1]) return 3;
            }
            return quads.Count;
        }

        public static float RadiusScaleFor(int n) => RADIUS_SCALE[Mathf.Clamp(n, 1, 4)];

        public bool IsFull() => Fragment != null && Fragment.Depth() == 0 &&
            Fragment.HeldPaths.Count == 1 && !Fragment.PendingStickerReturn;

        public float PhotoRadius()
        {
            if (Fragment == null) return BaseRadius;
            if (IsFull()) return BaseRadius * RadiusScaleFor(4);
            if (Fragment.TangramMold)
                return BaseRadius * TangramMatchContent.MoldRadiusScale;
            int n = RadiusBlockCount(Fragment.LastQuadrants(), UsesCompactFormation());
            if (n == 0) n = 1;
            return BaseRadius * RadiusScaleFor(Mathf.Clamp(n, 1, 4));
        }

        public float GetRadius()
        {
            if (Fragment == null) return BaseRadius;
            if (IsStarfish || Fragment.IsRainbow) return BaseRadius;
            var lastQ = Fragment.LastQuadrants();
            bool full = IsFull();
            if (Fragment.TangramMold && !full)
                return BaseRadius * TangramMatchContent.MoldRadiusScale;
            int rbc = RadiusBlockCount(lastQ, UsesCompactFormation());
            int n = full ? 4 : Mathf.Clamp(rbc != 0 ? rbc : 1, 1, 4);
            return BaseRadius * RadiusScaleFor(n);
        }

        // ------------------------------------------------------------ refresh
        public void Refresh()
        {
            float r = GetRadius();
            Collider.radius = r;
            float rVis = r * GLASS_VISUAL_OVERSCAN;

            ScaleToDiameter(BubbleSprite, 2f * rVis);
            ScaleToDiameter(HintGlow, 2f * rVis * HINT_GLOW_VISIBLE_RATIO);
            ScaleToDiameter(BubbleGlow, 2f * rVis * BUBBLE_GLOW_VISIBLE_RATIO);
            ScaleToDiameter(OperatedMarkGlow, 2f * rVis * BUBBLE_GLOW_VISIBLE_RATIO);
            ScaleToDiameter(BubbleGlowTarget, 2f * rVis * AIMING_VISIBLE_RATIO);
            float aimOff = rVis * AIMING_OFFSET_RATIO;
            BubbleGlowTarget.transform.localPosition = new Vector3(aimOff, -aimOff, 0);
            ScaleToDiameter(RejectFlash, 2f * rVis * REJECT_FLASH_VISIBLE_RATIO);

            RebuildImages(r);
            EnsureSpecialVisual().Refresh();
        }

        BubbleSpecialVisual EnsureSpecialVisual()
        {
            if (_specialVisual == null)
                _specialVisual = GetComponent<BubbleSpecialVisual>();
            if (_specialVisual == null)
                _specialVisual = gameObject.AddComponent<BubbleSpecialVisual>();
            _specialVisual.Bind(this);
            return _specialVisual;
        }

        public void SetLock(int count)
        {
            LockCount = Mathf.Max(0, count);
            IsLocked = LockCount > 0;
            EnsureSpecialVisual().Refresh();
        }

        public bool DecrementLock()
        {
            if (!IsLocked) return false;
            LockCount = Mathf.Max(0, LockCount - 1);
            IsLocked = LockCount > 0;
            BubbleSpecialVisual visual = EnsureSpecialVisual();
            if (IsLocked)
            {
                visual.Refresh();
            }
            else
            {
                visual.PlayUnlock();
                SoundManager.I?.Play("bubble_unlock");
            }
            return !IsLocked;
        }

        public void SetBomb(int count)
        {
            IsBomb = count > 0;
            BombCount = Mathf.Max(0, count);
            BombInitialCount = BombCount;
            BombSecondsLeft = BombCount;
            EnsureSpecialVisual().Refresh();
        }

        public bool DecrementBomb()
        {
            if (!IsBomb) return false;
            BombCount = Mathf.Max(0, BombCount - 1);
            if (BombCount == 0) IsBomb = false;
            EnsureSpecialVisual().Refresh();
            return !IsBomb;
        }

        public bool TickBombTime(float delta)
        {
            if (!IsBomb) return false;
            BombSecondsLeft = Mathf.Max(0f, BombSecondsLeft - Mathf.Max(0f, delta));
            BombCount = Mathf.CeilToInt(BombSecondsLeft);
            if (BombSecondsLeft <= 0f) IsBomb = false;
            EnsureSpecialVisual().Refresh();
            return !IsBomb;
        }

        public void AdoptBombState(int count, int initialCount, float secondsLeft)
        {
            IsBomb = count > 0;
            BombCount = Mathf.Max(0, count);
            BombInitialCount = Mathf.Max(BombCount, initialCount);
            BombSecondsLeft = Mathf.Max(0f, secondsLeft);
            EnsureSpecialVisual().Refresh();
        }

        public void TransferBombStateTo(BubbleView target)
        {
            if (!IsBomb || target == null) return;
            target.AdoptBombState(BombCount, BombInitialCount, BombSecondsLeft);
            IsBomb = false;
            BombCount = 0;
            EnsureSpecialVisual().Refresh();
        }

        public void SetStarfish(float seconds)
        {
            IsStarfish = seconds > 0f;
            StarfishSecondsLeft = Mathf.Max(0f, seconds);
            Refresh();
        }

        public void SetMagnetBubble(bool enabled)
        {
            IsMagnetBubble = enabled;
            EnsureSpecialVisual().Refresh();
        }

        public bool TickStarfish(float delta)
        {
            if (!IsStarfish) return false;
            StarfishSecondsLeft = Mathf.Max(
                0f, StarfishSecondsLeft - Mathf.Max(0f, delta));
            EnsureSpecialVisual().Refresh();
            return StarfishSecondsLeft <= 0f;
        }

        public void ClearStarfish(bool rescued)
        {
            if (!IsStarfish) return;
            StarfishSecondsLeft = 0f;
            if (rescued)
            {
                SoundManager.I?.Play("starfish_unlock");
                SoundManager.I?.Play("starfish_bubble_pop");
                BubbleSpecialVisual visual = EnsureSpecialVisual();
                bool harvesting = visual.PlayStarfishHarvest();
                TweenRunner.Delay(harvesting ? 0.2f : 0f, () =>
                {
                    if (this == null) return;
                    IsStarfish = false;
                    Explode();
                });
                return;
            }
            IsStarfish = false;
            EnsureSpecialVisual().Refresh();
            Explode();
        }

        public void PlayComboMergeFeedback(string tier)
        {
            if (IsFull() || string.IsNullOrEmpty(tier)) return;
            string skin = tier == "excellent" ? "excellent_effect" : "perfect_effect";
            if (!PlaySpecialSpine("combo_bubble", "animation", skin, 20, 1.5f))
                PlaySpecialPulse(1.18f, 0.28f);
        }

        public void PlayPerfectFitFeedback()
        {
            if (!PlaySpecialSpine("perfect_fit", "Animation", null, 270, 2.2f))
                PlaySpecialPulse(1.45f, 0.34f);
        }

        bool PlaySpecialSpine(
            string module,
            string animation,
            string skin,
            int sortingOrder,
            float sizeRatio)
        {
            var go = new GameObject(module + "Fx");
            go.transform.SetParent(VisualRoot, false);
            var spine = go.AddComponent<SpineLite.SpineSprite>();
            spine.Load(module);
            if (spine.Data == null || !spine.HasAnimation(animation))
            {
                Destroy(go);
                return false;
            }
            if (!string.IsNullOrEmpty(skin)) spine.SetSkin(skin);
            spine.SortingOrder = sortingOrder;
            go.transform.localScale = Vector3.one *
                (GetRadius() * 2f * sizeRatio / 400f);
            var entry = spine.SetAnimation(animation, false);
            if (entry == null)
            {
                Destroy(go);
                return false;
            }
            entry.Completed = () => { if (go != null) Destroy(go); };
            return true;
        }

        void PlaySpecialPulse(float peak, float duration)
        {
            TweenRunner.Go(SpecialPulseCo(peak, duration));
        }

        IEnumerator SpecialPulseCo(float peak, float duration)
        {
            if (VisualRoot == null) yield break;
            Vector3 start = VisualRoot.localScale;
            float elapsed = 0f;
            while (elapsed < duration && this != null && VisualRoot != null)
            {
                elapsed += Time.deltaTime;
                float value = Mathf.Clamp01(elapsed / Mathf.Max(duration, 0.001f));
                float scale = 1f + (peak - 1f) * Mathf.Sin(value * Mathf.PI);
                VisualRoot.localScale = start * scale;
                yield return null;
            }
            if (this != null && VisualRoot != null)
                VisualRoot.localScale = start;
        }

        static void ScaleToDiameter(SpriteRenderer sr, float diameter)
        {
            if (sr == null || sr.sprite == null) return;
            float w = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
            float s = diameter / w;
            sr.transform.localScale = new Vector3(s, s, 1);
        }

        public void SetCollisionRadius(float r) { Collider.radius = r; }

        public class PieceInfo
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public MeshRenderer Mr;
            public int Quadrant;
            public float RegionWOrig;
            public Sprite RuntimeSprite;
            public Material RuntimeMaterial;
            public Mesh RuntimeMesh;
            public bool RuntimeGameObject;
            public bool Animate = true;
        }

        public List<PieceInfo> Pieces = new List<PieceInfo>();

        void RebuildImages(float r)
        {
            ReleaseImagePieces();
            if (Fragment != null && NumberMatchContent.IsNumberImage(Fragment.ImageId))
            {
                RebuildNumber(r);
                return;
            }
            if (Fragment != null && WordMatchContent.IsWordImage(Fragment.ImageId))
            {
                RebuildWord(r);
                return;
            }
            if (Fragment != null &&
                CategoryMatchContent.IsCategoryImage(Fragment.ImageId))
            {
                RebuildCategory(r);
                return;
            }
            if (Fragment != null &&
                TangramMatchContent.IsTangramImage(Fragment.ImageId))
            {
                RebuildTangram(r);
                return;
            }
            if (SourceTexture == null || Fragment == null) return;
            if (UsesJigsawVisuals())
            {
                RebuildImagesJigsawStable(r);
                return;
            }

            int tw = SourceTexture.width, th = SourceTexture.height;
            var lastQ = Fragment.LastQuadrants();
            bool fullImage = lastQ.Count == 0;
            int ratioN = fullImage ? 4 : Mathf.Clamp(RadiusBlockCount(lastQ), 0, 4);
            float photoR = PhotoRadius() * PHOTO_CONTENT_SCALE;
            float cell = fullImage
                ? 2f * photoR * INSCRIBE_RATIO_BY_COUNT[4] / Mathf.Sqrt(2f)
                : QuadrantCell(lastQ, photoR, ratioN);
            float enlarge = ENLARGE_RATIO_YES2;
            _floatLim = 0f;

            float smallUv = CORNER_RADIUS_UV;
            float ratioUv = INSCRIBE_RATIO_BY_COUNT[Mathf.Clamp(ratioN, 0, 4)];
            float whSum = BboxWhSum(lastQ, fullImage);
            float fillCornerUv = Yes2FillCornerUv(photoR, ratioUv, enlarge, cell, 0f, smallUv, whSum);

            int pieceCount = Mathf.Min(Fragment.HeldPaths.Count, _imageSlots.Length);
            if (Fragment.HeldPaths.Count > _imageSlots.Length)
                Debug.LogError($"{name}: fragment contains {Fragment.HeldPaths.Count} pieces; BubbleEntity supports at most 4.");
            for (int pieceIndex = 0; pieceIndex < pieceCount; pieceIndex++)
            {
                var path = Fragment.HeldPaths[pieceIndex];
                int q = path.Count > 0 ? path[path.Count - 1] : 0;
                Rect region = BubbleFragmentGeometry.RegionForPath(tw, th, path); // design top-left origin
                int em = ComputeEdgeMask(q, lastQ, fullImage);
                const float BLEED = 1.0f;
                float bleedTop = (em & 1) != 0 ? 0 : BLEED;
                float bleedRight = (em & 2) != 0 ? 0 : BLEED;
                float bleedBottom = (em & 4) != 0 ? 0 : BLEED;
                float bleedLeft = (em & 8) != 0 ? 0 : BLEED;
                var bleedRegion = new Rect(
                    region.x - bleedLeft, region.y - bleedTop,
                    region.width + bleedLeft + bleedRight,
                    region.height + bleedTop + bleedBottom);

                // convert top-left origin rect -> Unity bottom-left origin rect
                var uRect = new Rect(bleedRegion.x, th - bleedRegion.y - bleedRegion.height, bleedRegion.width, bleedRegion.height);
                uRect.x = Mathf.Clamp(uRect.x, 0, tw); uRect.y = Mathf.Clamp(uRect.y, 0, th);
                if (uRect.xMax > tw) uRect.width = tw - uRect.x;
                if (uRect.yMax > th) uRect.height = th - uRect.y;

                var sr = _imageSlots[pieceIndex];
                var go = sr.gameObject;
                go.SetActive(true);
                var runtimeSprite = Sprite.Create(SourceTexture, uRect, new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
                sr.sprite = runtimeSprite;
                sr.sortingOrder = SortOrder.BubbleBand(_zIndex, 5);
                Vector4 uvRegion = new Vector4(
                    uRect.x / tw,
                    uRect.y / th,
                    uRect.width / tw,
                    uRect.height / th);
                bool detachedSticker =
                    Fragment.PendingStickerReturn &&
                    Fragment.StickerHalf == 2 &&
                    BubbleFragment.SamePath(path, Fragment.StickerDonorPath);
                Material mat;
                if (detachedSticker)
                {
                    mat = new Material(StickerImageMaterial());
                    string shapeName = BubbleSpecialRules.StickerShapeName(
                        Mathf.Max(0, Fragment.StickerShapeId));
                    Sprite mask = AssetLib.Sprite(
                        "Art/Sprites/Bubble/sticker_puzzle_mask_" + shapeName);
                    if (mask != null)
                        mat.SetTexture("_MaskTex", mask.texture);
                    mat.SetVector("_UvRegion", uvRegion);
                }
                else
                {
                    mat = new Material(FragmentImageMaterial());
                    mat.SetFloat("_CornerRadius", smallUv);
                    mat.SetFloat("_FillCornerRadius", fillCornerUv);
                    int cornerMask = ComputeCornerMask(q, lastQ, fullImage);
                    int fillMask = FillCornerMaskForPiece(q, lastQ, fullImage);
                    mat.SetFloat("_CornerMask", cornerMask);
                    mat.SetFloat("_FillCornerMask", fillMask & cornerMask);
                    mat.SetFloat("_EdgeMask", em);
                    mat.SetVector("_UvRegion", uvRegion);
                }
                sr.material = mat;

                var offD = fullImage ? Vector2.zero : QuadrantOffset(q, lastQ, cell) * enlarge;
                go.transform.localPosition = new Vector3(offD.x, -offD.y, 0); // y-down -> y-up
                float s = cell / bleedRegion.width * enlarge;
                float sy = cell / bleedRegion.height * enlarge;
                go.transform.localScale = new Vector3(s, sy, 1);
                Pieces.Add(new PieceInfo
                {
                    Go = go,
                    Sr = sr,
                    Quadrant = q,
                    RegionWOrig = region.width,
                    RuntimeSprite = runtimeSprite,
                    RuntimeMaterial = mat
                });
            }
        }

        public bool UsesJigsawVisuals()
        {
            if (!JigsawChipRule.IsEnabled || SourceTexture == null || Fragment == null ||
                Fragment.PendingStickerReturn || Fragment.HeldPaths.Count == 0)
                return false;
            if (NumberMatchContent.IsNumberImage(Fragment.ImageId) ||
                WordMatchContent.IsWordImage(Fragment.ImageId) ||
                CategoryMatchContent.IsCategoryImage(Fragment.ImageId) ||
                TangramMatchContent.IsTangramImage(Fragment.ImageId))
                return false;
            int depth = Fragment.HeldPaths[0].Count;
            return Fragment.HeldPaths.All(path => path.Count == depth);
        }

        static int JigsawSeedFor(int imageId, IReadOnlyList<int> parentPath)
        {
            int seed = imageId;
            if (parentPath == null) return seed;
            foreach (int quadrant in parentPath)
                seed = unchecked(seed * 4 + quadrant + 1);
            return seed;
        }

        void RebuildImagesJigsawStable(float radius)
        {
            bool fullImage = Fragment.IsFull();
            List<int> heldQuadrants = fullImage
                ? new List<int> { 0, 1, 2, 3 }
                : Fragment.LastQuadrants();
            List<int> lastQuadrants = Fragment.LastQuadrants();
            int ratioN = fullImage
                ? 4
                : Mathf.Clamp(RadiusBlockCount(lastQuadrants), 0, 4);
            float photoRadius = PhotoRadius() * PHOTO_CONTENT_SCALE;
            float cell = QuadrantCell(
                fullImage ? new List<int> { 0, 1, 2, 3 } : lastQuadrants,
                photoRadius,
                ratioN);
            float fullSize = cell * 2f;
            Vector2 centerOffset = new(-cell, -cell);
            if (!fullImage)
            {
                float minSx = 1f, maxSx = -1f;
                float minSy = 1f, maxSy = -1f;
                foreach (int quadrant in lastQuadrants)
                {
                    float sx = quadrant == 0 || quadrant == 2 ? -1f : 1f;
                    float sy = quadrant == 0 || quadrant == 1 ? -1f : 1f;
                    minSx = Mathf.Min(minSx, sx);
                    maxSx = Mathf.Max(maxSx, sx);
                    minSy = Mathf.Min(minSy, sy);
                    maxSy = Mathf.Max(maxSy, sy);
                }
                float centerX = (minSx + maxSx + 4f) / 8f * fullSize;
                float centerY = (minSy + maxSy + 4f) / 8f * fullSize;
                centerOffset = new Vector2(-centerX, -centerY);
            }

            List<int> parentPath = Fragment.ParentPath();
            var shapes = new List<Vector2[]>();
            if (fullImage || heldQuadrants.Count >= 4)
            {
                shapes.Add(new[]
                {
                    Vector2.zero,
                    new Vector2(fullSize, 0f),
                    new Vector2(fullSize, fullSize),
                    new Vector2(0f, fullSize),
                });
            }
            else
            {
                Vector2[][] allPieces = JigsawPath.BuildFourPieces(
                    fullSize,
                    JigsawSeedFor(Fragment.ImageId, parentPath));
                foreach (int quadrant in heldQuadrants)
                    if (quadrant >= 0 && quadrant < allPieces.Length)
                        shapes.Add(allPieces[quadrant]);
            }

            Rect parentRegion = BubbleFragmentGeometry.RegionForPath(
                SourceTexture.width,
                SourceTexture.height,
                parentPath);
            PieceInfo piece = CreateJigsawMesh(
                "JigsawMerged",
                shapes,
                centerOffset,
                parentRegion,
                fullSize,
                Vector2.zero,
                1f,
                -1,
                fullSize,
                false);
            if (piece != null) Pieces.Add(piece);
            _floatLim = 0f;
        }

        public void RebuildImagesJigsawAnimatable()
        {
            if (!UsesJigsawVisuals() || Fragment.IsFull()) return;
            ReleaseImagePieces();
            List<int> lastQuadrants = Fragment.LastQuadrants();
            int ratioN = Mathf.Clamp(RadiusBlockCount(lastQuadrants), 0, 4);
            float cell = QuadrantCell(
                lastQuadrants,
                PhotoRadius() * PHOTO_CONTENT_SCALE,
                ratioN);
            List<int> parentPath = Fragment.ParentPath();
            const float buildCell = 192f;
            const float buildSize = buildCell * 2f;
            Vector2[][] allPieces = JigsawPath.BuildFourPieces(
                buildSize,
                JigsawSeedFor(Fragment.ImageId, parentPath));
            Rect parentRegion = BubbleFragmentGeometry.RegionForPath(
                SourceTexture.width,
                SourceTexture.height,
                parentPath);
            foreach (int quadrant in lastQuadrants)
            {
                if (quadrant < 0 || quadrant >= allPieces.Length) continue;
                int column = quadrant == 1 || quadrant == 3 ? 1 : 0;
                int row = quadrant == 2 || quadrant == 3 ? 1 : 0;
                Vector2 localCenter = new(
                    (column + 0.5f) * buildCell,
                    (row + 0.5f) * buildCell);
                Vector2 offset = QuadrantOffset(
                    quadrant,
                    lastQuadrants,
                    cell) * ENLARGE_RATIO_YES2;
                PieceInfo piece = CreateJigsawMesh(
                    "JigsawPiece" + quadrant,
                    new List<Vector2[]> { allPieces[quadrant] },
                    -localCenter,
                    parentRegion,
                    buildSize,
                    offset,
                    cell / buildCell * ENLARGE_RATIO_YES2,
                    quadrant,
                    buildCell,
                    true);
                if (piece != null) Pieces.Add(piece);
            }
            _floatLim = 0f;
        }

        PieceInfo CreateJigsawMesh(
            string objectName,
            IReadOnlyList<Vector2[]> shapes,
            Vector2 vertexOffset,
            Rect parentRegion,
            float fullSize,
            Vector2 localOffset,
            float localScale,
            int quadrant,
            float regionWidth,
            bool animate)
        {
            if (shapes == null || shapes.Count == 0 || fullSize <= 0f)
                return null;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            foreach (Vector2[] shape in shapes)
            {
                if (!JigsawPath.TryTriangulate(
                        shape,
                        out Vector2[] clean,
                        out ushort[] shapeTriangles))
                    continue;
                int vertexBase = vertices.Count;
                foreach (Vector2 point in clean)
                {
                    Vector2 shifted = point + vertexOffset;
                    vertices.Add(new Vector3(shifted.x, -shifted.y, 0f));
                    float sourceX = parentRegion.x + point.x / fullSize * parentRegion.width;
                    float sourceY = parentRegion.y + point.y / fullSize * parentRegion.height;
                    uvs.Add(new Vector2(
                        sourceX / SourceTexture.width,
                        1f - sourceY / SourceTexture.height));
                    colors.Add(Color.white);
                }
                foreach (ushort triangle in shapeTriangles)
                    triangles.Add(vertexBase + triangle);
            }
            if (vertices.Count < 3 || triangles.Count < 3) return null;

            var go = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(ImagesRoot, false);
            go.transform.localPosition = new Vector3(localOffset.x, -localOffset.y, 0f);
            go.transform.localScale = new Vector3(localScale, localScale, 1f);
            var mesh = new Mesh { name = objectName + " Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            var material = new Material(DefaultSpriteMaterial())
            {
                name = objectName + " Material",
                mainTexture = SourceTexture,
            };
            renderer.sharedMaterial = material;
            if (BubbleSprite != null)
                renderer.sortingLayerID = BubbleSprite.sortingLayerID;
            renderer.sortingOrder = SortOrder.BubbleBand(_zIndex, 5);
            renderer.allowOcclusionWhenDynamic = false;
            return new PieceInfo
            {
                Go = go,
                Mr = renderer,
                Quadrant = quadrant,
                RegionWOrig = regionWidth,
                RuntimeMaterial = material,
                RuntimeMesh = mesh,
                RuntimeGameObject = true,
                Animate = animate,
            };
        }

        void RebuildCategory(float radius)
        {
            var quadrants = IsFull()
                ? new List<int> { 0, 1, 2, 3 }
                : Fragment.LastQuadrants();
            if (quadrants.Count == 0) quadrants.Add(0);
            int count = quadrants.Count == 4
                ? 4
                : Mathf.Max(1, quadrants.Count);
            float cell = CategoryMatchContent.FormationCell(
                quadrants,
                radius,
                count);
            float iconSize = CategoryMatchContent.IconDisplaySize(cell, count);

            int rendererIndex = 0;
            foreach (int slot in quadrants)
            {
                if (rendererIndex >= _imageSlots.Length) break;
                Texture2D icon = CategoryMatchContent.IconFor(
                    Fragment.ImageId,
                    slot);
                if (icon == null) continue;
                float sizeReference = Mathf.Max(icon.width, icon.height);
                if (sizeReference <= 0f) continue;

                Sprite runtimeSprite = Sprite.Create(
                    icon,
                    new Rect(0f, 0f, icon.width, icon.height),
                    new Vector2(0.5f, 0.5f),
                    1f,
                    0,
                    SpriteMeshType.FullRect);
                runtimeSprite.name =
                    $"Category_{Fragment.ImageId}_{slot}";

                SpriteRenderer renderer = _imageSlots[rendererIndex++];
                renderer.gameObject.SetActive(true);
                renderer.sprite = runtimeSprite;
                renderer.color = Color.white;
                // ReleaseImagePieces deliberately clears owned runtime
                // materials. Category sprites therefore need their standard
                // sprite material restored explicitly before reuse.
                renderer.sharedMaterial = DefaultSpriteMaterial();
                renderer.sortingOrder = SortOrder.BubbleBand(_zIndex, 5);
                Vector2 offset = CategoryMatchContent.FormationOffset(
                    slot,
                    quadrants,
                    cell);
                renderer.transform.localPosition =
                    new Vector3(offset.x, -offset.y, 0f);
                float scale = iconSize / sizeReference;
                renderer.transform.localScale =
                    new Vector3(scale, scale, 1f);

                Pieces.Add(new PieceInfo
                {
                    Go = renderer.gameObject,
                    Sr = renderer,
                    Quadrant = slot,
                    RegionWOrig = sizeReference,
                    RuntimeSprite = runtimeSprite,
                });
            }
            _floatLim = 0f;
        }

        void RebuildNumber(float radius)
        {
            var quadrants = IsFull()
                ? new List<int> { 0, 1, 2, 3 }
                : Fragment.LastQuadrants();
            if (quadrants.Count == 0) quadrants.Add(0);
            int count = IsFull() ? 4 : Mathf.Max(1, quadrants.Count);
            float cell = NumberMatchContent.FormationCell(quadrants, radius, count);
            Sprite disc = AssetLib.Sprite("Art/Sprites/Bubble/disc");
            float sourceWidth = disc != null ? Mathf.Max(1f, disc.rect.width) : 100f;

            int pieceIndex = 0;
            if (IsFull())
            {
                for (int slot = 0; slot < NumberPuzzleValidator.GroupSize; slot++)
                    AddNumberPiece(pieceIndex++, slot, quadrants, cell, sourceWidth, disc);
            }
            else
            {
                foreach (List<int> path in Fragment.HeldPaths)
                {
                    if (pieceIndex >= _imageSlots.Length) break;
                    int slot = path.Count > 0 ? path[path.Count - 1] : pieceIndex;
                    AddNumberPiece(pieceIndex++, slot, quadrants, cell, sourceWidth, disc);
                }
            }
            _floatLim = 0f;
        }

        void RebuildWord(float radius)
        {
            var quadrants = IsFull()
                ? new List<int> { 0, 1, 2, 3 }
                : Fragment.LastQuadrants();
            if (quadrants.Count == 0) quadrants.Add(0);
            int count = IsFull() ? 4 : Mathf.Max(1, quadrants.Count);
            float cell = WordMatchContent.FormationCell(
                quadrants,
                radius,
                count);
            Sprite disc = AssetLib.Sprite("Art/Sprites/Bubble/disc");
            float sourceWidth = disc != null
                ? Mathf.Max(1f, disc.rect.width)
                : 100f;

            int pieceIndex = 0;
            if (IsFull())
            {
                for (int slot = 0; slot < 4; slot++)
                    AddWordPiece(
                        pieceIndex++,
                        slot,
                        quadrants,
                        cell,
                        sourceWidth,
                        disc);
            }
            else
            {
                foreach (List<int> path in Fragment.HeldPaths)
                {
                    if (pieceIndex >= _imageSlots.Length) break;
                    int slot = path.Count > 0
                        ? path[path.Count - 1]
                        : pieceIndex;
                    AddWordPiece(
                        pieceIndex++,
                        slot,
                        quadrants,
                        cell,
                        sourceWidth,
                        disc);
                }
            }
            _floatLim = 0f;
        }

        void RebuildTangram(float radius)
        {
            bool closed = IsFull();
            var held = closed
                ? new List<int> { 0, 1, 2, 3 }
                : Fragment.LastQuadrants();
            bool mold = Fragment.TangramMold;
            float scale = TangramMatchContent.FormationScale(
                Fragment.ImageId,
                held,
                PhotoRadius(),
                mold);

            if (mold)
            {
                for (int slot = 0; slot < 4; slot++)
                    AddTangramPiece(
                        slot,
                        slot,
                        held,
                        scale,
                        held.Contains(slot),
                        true);
            }
            else
            {
                int rendererIndex = 0;
                foreach (int slot in held)
                {
                    if (rendererIndex >= _imageSlots.Length) break;
                    AddTangramPiece(
                        rendererIndex++,
                        slot,
                        held,
                        scale,
                        true,
                        false);
                }
            }
            _floatLim = 0f;
        }

        void AddTangramPiece(
            int rendererIndex,
            int slot,
            List<int> held,
            float scale,
            bool filled,
            bool mold)
        {
            if (rendererIndex < 0 || rendererIndex >= _imageSlots.Length)
                return;
            Vector2[] polygon =
                TangramMatchContent.PieceCenteredPolygon(Fragment.ImageId, slot);
            if (polygon.Length < 3) return;
            // Recovered polygon data uses Godot's y-down canvas convention.
            var unityPolygon = new Vector2[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
                unityPolygon[i] = new Vector2(polygon[i].x, -polygon[i].y);
            if (!TangramMatchContent.TryTriangulate(
                    unityPolygon,
                    out Vector2[] vertices,
                    out ushort[] triangles))
            {
                Debug.LogError(
                    $"Tangram polygon triangulation failed for " +
                    $"image {Fragment.ImageId}, slot {slot}.");
                return;
            }

            Sprite runtimeSprite = Sprite.Create(
                TangramMatchContent.WhiteTexture,
                new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect);
            runtimeSprite.name = $"Tangram_{Fragment.ImageId}_{slot}";
            // Sprite.OverrideGeometry expects coordinates in the source
            // rectangle's bottom-left pixel space, whereas the recovered
            // Godot Polygon2D points are centred around (0,0). Offset them by
            // the 16 px helper sprite's pivot; the renderer subtracts that
            // pivot again when producing local geometry.
            var spriteVertices = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                spriteVertices[i] = vertices[i] + new Vector2(8f, 8f);
            runtimeSprite.OverrideGeometry(spriteVertices, triangles);

            SpriteRenderer renderer = _imageSlots[rendererIndex];
            renderer.gameObject.SetActive(true);
            renderer.sprite = runtimeSprite;
            Color pieceColor = TangramMatchContent.PieceColor(
                Fragment.ImageId, slot);
            renderer.color = filled
                ? pieceColor
                : new Color(pieceColor.r, pieceColor.g, pieceColor.b, 0.04f);
            renderer.sharedMaterial = DefaultSpriteMaterial();
            renderer.sortingOrder = SortOrder.BubbleBand(_zIndex, 5);
            Vector2 offset = TangramMatchContent.FormationOffset(
                Fragment.ImageId,
                slot,
                held,
                scale,
                mold);
            renderer.transform.localPosition =
                new Vector3(offset.x, -offset.y, 0f);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);

            if (mold)
                AddTangramOutline(renderer.transform, vertices, scale);
            Pieces.Add(new PieceInfo
            {
                Go = renderer.gameObject,
                Sr = renderer,
                Quadrant = slot,
                RegionWOrig = 1f,
                RuntimeSprite = runtimeSprite,
                RuntimeMaterial = null,
                Animate = filled,
            });
        }

        void AddTangramOutline(
            Transform parent,
            IReadOnlyList<Vector2> vertices,
            float scale)
        {
            var outline = new GameObject("TangramOutline");
            outline.transform.SetParent(parent, false);
            var line = outline.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = vertices.Count;
            for (int i = 0; i < vertices.Count; i++)
                line.SetPosition(i, new Vector3(vertices[i].x, vertices[i].y, -0.01f));
            // Godot draws both the mold seams and outer contour over the
            // glass. At phone resolution the original 3.5 px half-alpha line
            // becomes nearly invisible after the glass shader, so use the
            // recovered outer-outline weight for a legible mold target.
            // Unity's LineRenderer width is already expressed in world units;
            // unlike Godot Line2D it is not multiplied by this polygon
            // parent's local scale. Dividing here reduced the contour to a
            // sub-pixel stipple on device.
            line.startWidth = line.endWidth = 7f;
            line.startColor = line.endColor = new Color(1f, 1f, 1f, 0.9f);
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            if (_tangramLineMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _tangramLineMaterial = new Material(shader)
                    {
                        name = "Tangram Line",
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    // Sprites/Default samples _MainTex. A runtime-created
                    // material has none, which produced a stippled rather
                    // than continuous mold outline on standalone players.
                    _tangramLineMaterial.mainTexture = Texture2D.whiteTexture;
                }
            }
            line.sharedMaterial = _tangramLineMaterial;
            line.sortingOrder = SortOrder.BubbleBand(_zIndex, 6);
        }

        void AddNumberPiece(
            int pieceIndex,
            int slot,
            List<int> quadrants,
            float cell,
            float sourceWidth,
            Sprite disc)
        {
            SpriteRenderer renderer = _imageSlots[pieceIndex];
            renderer.gameObject.SetActive(true);
            // The renderer is only a transform carrier for the formula label.
            // Leaving it enabled lets the generic bubble-appear tween restore
            // its alpha and draws a second, smaller bubble disc.
            renderer.enabled = false;
            renderer.sprite = disc;
            renderer.color = new Color(1f, 1f, 1f, 0f);
            renderer.sortingOrder = SortOrder.BubbleBand(_zIndex, 5);
            Vector2 offset = NumberMatchContent.FormationOffset(slot, quadrants, cell);
            renderer.transform.localPosition = new Vector3(offset.x, -offset.y, 0f);
            float scale = cell / sourceWidth;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);

            var labelObject = new GameObject("Formula");
            labelObject.transform.SetParent(renderer.transform, false);
            var label = labelObject.AddComponent<TextMeshPro>();
            // TextMeshPro's 3D glyph geometry is authored in tenths of a
            // Unity unit. Godot's Label used the disc pixel coordinate space
            // directly, so compensate before inheriting the disc-to-cell
            // scale. Without this, a 227 px formula renders only a few pixels
            // high on screen.
            label.rectTransform.localScale = Vector3.one * 10f;
            label.font = AssetLib.NumFont;
            label.text = NumberMatchContent.FormulaFor(Fragment.ImageId, slot);
            label.fontSize = sourceWidth * 0.443f;
            label.enableAutoSizing = true;
            label.fontSizeMin = sourceWidth * 0.22f;
            label.fontSizeMax = sourceWidth * 0.443f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = int.TryParse(label.text, out _)
                ? new Color(1f, 0.88235f, 0.00784f, 1f)
                : Color.white;
            // Godot caps a formula to 80% of its formation cell. Keeping the
            // full cell width made five-character expressions render about
            // 20% larger and visually heavier than the reference, while
            // short numeric answers should remain at the authored max size.
            label.rectTransform.sizeDelta =
                new Vector2(sourceWidth * 0.8f, sourceWidth * 0.7f);
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableWordWrapping = false;
            TmpTextStyle.ApplyOutline(
                label,
                new Color(0f, 0.16863f, 0.33725f, 0.4f),
                sourceWidth * 0.053f);
            TmpTextStyle.ApplyFaceDilation(label, 0.025f);
            MeshRenderer mesh = label.GetComponent<MeshRenderer>();
            if (mesh != null)
                mesh.sortingOrder = SortOrder.BubbleBand(_zIndex, 6);

            Pieces.Add(new PieceInfo
            {
                Go = renderer.gameObject,
                Sr = renderer,
                Quadrant = slot,
                RegionWOrig = sourceWidth,
            });
        }

        void AddWordPiece(
            int pieceIndex,
            int slot,
            List<int> quadrants,
            float cell,
            float sourceWidth,
            Sprite disc)
        {
            SpriteRenderer renderer = _imageSlots[pieceIndex];
            renderer.gameObject.SetActive(true);
            // Word Match uses a plain label directly in the parent bubble.
            // Keep the carrier component disabled so generic alpha tweens
            // cannot reveal its helper disc.
            renderer.enabled = false;
            renderer.sprite = disc;
            renderer.color = new Color(1f, 1f, 1f, 0f);
            renderer.sortingOrder = SortOrder.BubbleBand(_zIndex, 5);
            Vector2 offset = WordMatchContent.FormationOffset(
                slot,
                quadrants,
                cell);
            renderer.transform.localPosition =
                new Vector3(offset.x, -offset.y, 0f);
            float scale = cell / sourceWidth;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);

            var labelObject = new GameObject("Formula");
            labelObject.transform.SetParent(renderer.transform, false);
            var label = labelObject.AddComponent<TextMeshPro>();
            label.rectTransform.localScale = Vector3.one * 10f;
            label.font = AssetLib.NumFont;
            label.text = WordMatchContent.WordFor(Fragment.ImageId, slot);
            label.fontSize = sourceWidth * 0.37f;
            label.enableAutoSizing = true;
            label.fontSizeMin = sourceWidth * 0.14f;
            label.fontSizeMax = sourceWidth * 0.37f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.rectTransform.sizeDelta =
                new Vector2(sourceWidth * 0.94f, sourceWidth * 0.7f);
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableWordWrapping = false;
            TmpTextStyle.ApplyOutline(
                label,
                new Color(0f, 0.16863f, 0.33725f, 0.4f),
                sourceWidth * 0.0444f);
            TmpTextStyle.ApplyFaceDilation(label, 0.025f);
            MeshRenderer mesh = label.GetComponent<MeshRenderer>();
            if (mesh != null)
                mesh.sortingOrder = SortOrder.BubbleBand(_zIndex, 6);

            Pieces.Add(new PieceInfo
            {
                Go = renderer.gameObject,
                Sr = renderer,
                Quadrant = slot,
                RegionWOrig = sourceWidth,
            });
        }

        void ReleaseImagePieces()
        {
            foreach (var piece in Pieces)
            {
                if (piece == null) continue;
                if (piece.Sr != null)
                {
                    piece.Sr.sprite = null;
                    piece.Sr.sharedMaterial = null;
                    piece.Sr.transform.localPosition = Vector3.zero;
                    piece.Sr.transform.localRotation = Quaternion.identity;
                    piece.Sr.transform.localScale = Vector3.one;
                    piece.Sr.gameObject.SetActive(false);
                }
                if (piece.RuntimeSprite != null) Destroy(piece.RuntimeSprite);
                if (piece.RuntimeMaterial != null) Destroy(piece.RuntimeMaterial);
                if (piece.RuntimeMesh != null) Destroy(piece.RuntimeMesh);
                if (piece.RuntimeGameObject && piece.Go != null)
                    Destroy(piece.Go);
            }
            Pieces.Clear();

            if (_imageSlots == null) return;
            foreach (var slot in _imageSlots)
            {
                if (slot == null) continue;
                for (int i = slot.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = slot.transform.GetChild(i);
                    if (child != null &&
                        (child.name == "Formula" ||
                         child.name == "TangramOutline"))
                        Destroy(child.gameObject);
                }
                slot.color = Color.white;
                slot.enabled = true;
                slot.gameObject.SetActive(false);
            }
        }

        public bool IsNumber()
        {
            return Fragment != null && NumberMatchContent.IsNumberImage(Fragment.ImageId);
        }

        public bool IsTangram()
        {
            return Fragment != null &&
                   TangramMatchContent.IsTangramImage(Fragment.ImageId);
        }

        public bool IsCategory()
        {
            return Fragment != null &&
                   CategoryMatchContent.IsCategoryImage(Fragment.ImageId);
        }

        public bool IsWord()
        {
            return Fragment != null &&
                   WordMatchContent.IsWordImage(Fragment.ImageId);
        }

        public bool UsesCompactFormation()
        {
            return IsNumber() || IsWord() || IsCategory() || IsTangram();
        }

        // ------------------------------------------------------------ update
        void Update()
        {
            UpdateFallTrail();
            if (State != BubbleState.Alive) return;
            CheckReturnHomeWatchdog();

            if (_floatLim != 0f)
            {
                _floatTimer += Time.deltaTime;
                if (_floatTimer >= FLOAT_DIR_INTERVAL)
                {
                    _floatTimer = 0f;
                    PickNewFloatDirection();
                }
                float t = Mathf.Clamp01(Time.deltaTime * FLOAT_TURN_LERP);
                _floatDir = Vector2.Lerp(_floatDir, _floatTargetDir, t);
                if (_floatDir.magnitude > 0.001f) _floatDir.Normalize();
                _floatSpeed = Mathf.Lerp(_floatSpeed, _floatTargetSpeed, t);
                _floatOffset += _floatDir * (_floatSpeed * Time.deltaTime * 0.5f);
                float lim = _floatLim >= 0 ? _floatLim : BaseRadius * FLOAT_RANGE_RATIO;
                if (_floatOffset.x > lim) { _floatOffset.x = lim; _floatDir.x = -Mathf.Abs(_floatDir.x); _floatTargetDir.x = -Mathf.Abs(_floatTargetDir.x); }
                else if (_floatOffset.x < -lim) { _floatOffset.x = -lim; _floatDir.x = Mathf.Abs(_floatDir.x); _floatTargetDir.x = Mathf.Abs(_floatTargetDir.x); }
                if (_floatOffset.y > lim) { _floatOffset.y = lim; _floatDir.y = -Mathf.Abs(_floatDir.y); _floatTargetDir.y = -Mathf.Abs(_floatTargetDir.y); }
                else if (_floatOffset.y < -lim) { _floatOffset.y = -lim; _floatDir.y = Mathf.Abs(_floatDir.y); _floatTargetDir.y = Mathf.Abs(_floatTargetDir.y); }
                ImagesRoot.localPosition = _floatOrigin + _floatOffset;
            }

            // keep visuals upright
            if (Mathf.Abs(ImagesRoot.eulerAngles.z) > 0.01f) ImagesRoot.rotation = Quaternion.identity;
            if (Mathf.Abs(BubbleSprite.transform.eulerAngles.z) > 0.01f) BubbleSprite.transform.rotation = Quaternion.identity;
        }

        void UpdateFallTrail()
        {
            if (FallTrail == null) return;
            var shouldEmit = State == BubbleState.Alive &&
                             !Dragging &&
                             Body != null &&
                             Body.velocity.magnitude > FALL_TRAIL_SPEED_THRESHOLD;
            if (shouldEmit)
            {
                if (!FallTrail.isEmitting) FallTrail.Play(true);
            }
            else if (FallTrail.isPlaying || FallTrail.isEmitting)
            {
                StopFallTrail(clear: false);
            }
        }

        void StopFallTrail(bool clear)
        {
            if (FallTrail == null) return;
            FallTrail.Stop(
                true,
                clear
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
        }

        void FixedUpdate()
        {
            if (Body == null) return;
            _prevVelocity = Body.velocity;
            if (State != BubbleState.Alive || Dragging || Body.isKinematic)
            {
                JitResetWindow();
                _calmFrames = 0;
                return;
            }

            if (Body.IsSleeping())
            {
                JitResetWindow();
                _calmFrames = 0;
                return;
            }

            // Rigidbody2D owns the physical transform. Moving Transform directly
            // here makes Box2D and the render transform overwrite one another on
            // alternate frames, which is especially visible in a resting stack.
            var dPos = BubbleField.W2D(Body.position);
            if (_boundsSet && (dPos.x < _boundLeft || dPos.x > _boundRight || dPos.y > _boundFloor))
            {
                var clamped = ClampIntoBoundsDesign(dPos);
                var vel = Body.velocity;
                if (clamped.x > dPos.x && vel.x < 0) vel.x = 0;
                else if (clamped.x < dPos.x && vel.x > 0) vel.x = 0;
                if (clamped.y < dPos.y && vel.y < 0) vel.y = 0; // design y down: clamped up means moving down blocked
                Body.velocity = vel;
                Body.position = BubbleField.D2W(clamped.x, clamped.y);
            }

            if (_softPushCapFrames > 0)
            {
                Body.velocity *= SOFT_PUSH_NEIGHBOR_DAMP_FACTOR;
                _softPushCapFrames--;
            }

            if (_calmFrames > 0)
            {
                Body.velocity = Vector2.ClampMagnitude(Body.velocity * CALM_VEL_FACTOR, CALM_MAX_SPEED);
                Body.angularVelocity *= CALM_ANG_FACTOR;
                _calmFrames--;
                if (_calmFrames == 0)
                    TrySleepSettledBody();
                JitResetWindow();
                return;
            }
            _jitSpeedAccum += Body.velocity.magnitude;
            _jitSamples++;
            _jitWinTime += Time.fixedDeltaTime;
            if (_jitWinTime < JITTER_WINDOW) return;
            float avgSpeed = _jitSpeedAccum / Mathf.Max(_jitSamples, 1);
            float netDisp = Vector2.Distance(Body.position, _jitWinStartPos);
            if (avgSpeed > JITTER_SPEED_MIN && netDisp < JITTER_DISP_MAX)
            {
                ApplyEscapeNudge();
                _calmFrames = CALM_FRAMES;
            }
            JitResetWindow();
        }

        void TrySleepSettledBody()
        {
            if (!HasLanded || Body == null || Body.isKinematic ||
                Body.velocity.sqrMagnitude > SETTLED_SLEEP_MAX_SPEED * SETTLED_SLEEP_MAX_SPEED ||
                Mathf.Abs(Body.angularVelocity) > SETTLED_SLEEP_MAX_ANGULAR_SPEED ||
                Body.GetContacts(_settledContactBuffer) == 0)
                return;

            Body.velocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.Sleep();
        }

        Vector2 ClampIntoBoundsDesign(Vector2 pos)
        {
            float r = GetRadius();
            float xLo = _boundLeft + r, xHi = _boundRight - r;
            if (xHi < xLo) { float mid = (_boundLeft + _boundRight) * 0.5f; xLo = mid; xHi = mid; }
            return new Vector2(Mathf.Clamp(pos.x, xLo, xHi), Mathf.Min(pos.y, _boundFloor - r));
        }

        public Vector2 ClampIntoBounds(Vector2 designPos) => _boundsSet ? ClampIntoBoundsDesign(designPos) : designPos;

        void ApplyEscapeNudge()
        {
            Vector2 deepestDir = Vector2.zero;
            float maxOverlap = 0;
            float r = GetRadius();
            Vector2 position = Body.position;
            var hits = Physics2D.OverlapCircleAll(position, r + 4);
            foreach (var h in hits)
            {
                var ob = h.GetComponent<BubbleView>();
                if (ob == null || ob == this) continue;
                Vector2 otherPosition = ob.Body != null
                    ? ob.Body.position
                    : (Vector2)ob.transform.position;
                float d = Vector2.Distance(position, otherPosition);
                if (d <= 0.01f) continue;
                float overlap = (r + ob.GetRadius()) - d;
                if (overlap > maxOverlap)
                {
                    maxOverlap = overlap;
                    deepestDir = (position - otherPosition) / d;
                }
            }
            if (maxOverlap > ESCAPE_NUDGE_MIN_OVERLAP_PX)
                Body.position = position + deepestDir *
                    Mathf.Min((maxOverlap - ESCAPE_NUDGE_MIN_OVERLAP_PX) * 0.5f, NUDGE_MAX_PX);
        }

        void JitResetWindow()
        {
            _jitWinStartPos = Body != null ? Body.position : (Vector2)transform.position;
            _jitWinTime = 0; _jitSpeedAccum = 0; _jitSamples = 0;
        }

        public void ApplySoftPushSpeedCap(float growSec)
        {
            float secs = Mathf.Max(growSec, 0) + SOFT_PUSH_CAP_TAIL_SEC;
            int frames = Mathf.CeilToInt(secs / Time.fixedDeltaTime);
            _softPushCapFrames = Mathf.Max(_softPushCapFrames, frames);
        }

        /// <summary>
        /// Port of BubbleView.apply_block_size_abs_smooth. Block-size boosts
        /// are absolute against the field radius and are intentionally
        /// grow-only, so a later peak recalculation cannot shrink a bubble
        /// that has already been enlarged.
        /// </summary>
        public bool ApplyBlockSizeAbsSmooth(float fieldBaseRadius, float mult, float growDuration)
        {
            if (Fragment == null || State != BubbleState.Alive) return false;
            int held = Fragment.HeldPaths.Count;
            if (held < 2 || held > 3) return false;

            float newRadius = fieldBaseRadius * mult;
            float floorRadius = Mathf.Max(BaseRadius, _blockSizeTargetRadius);
            if (newRadius <= floorRadius) return false;

            _blockSizeTargetRadius = newRadius;
            if (_blockSizeCo != null) StopCoroutine(_blockSizeCo);
            if (growDuration <= 0f || !isActiveAndEnabled)
            {
                BaseRadius = newRadius;
                Refresh();
                _blockSizeCo = null;
                return true;
            }

            _blockSizeCo = StartCoroutine(BlockSizeTweenCo(BaseRadius, newRadius, growDuration));
            return true;
        }

        IEnumerator BlockSizeTweenCo(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t);
                SetBlockSizeRadius(Mathf.LerpUnclamped(from, to, eased));
            }
            SetBlockSizeRadius(to);
            _blockSizeCo = null;
        }

        void SetBlockSizeRadius(float radius)
        {
            if (State != BubbleState.Alive || Dragging ||
                (Body != null && Body.isKinematic))
                return;
            BaseRadius = radius;
            Refresh();
        }

        void OnCollisionEnter2D(Collision2D col)
        {
            if (State != BubbleState.Alive || Dragging || Body.isKinematic) return;
            HasLanded = true;
            if (Time.time < _collideMuteUntil) return;
            float relSpeed;
            var ob = col.collider.GetComponent<BubbleView>();
            if (ob != null)
            {
                if (ob.State != BubbleState.Alive) return;
                if (GetInstanceID() > ob.GetInstanceID()) return;
                Vector2 delta = ob.transform.position - transform.position;
                float dist = delta.magnitude;
                if (dist < 0.001f) return;
                var normal = delta / dist;
                relSpeed = Mathf.Max(Vector2.Dot(_prevVelocity - ob._prevVelocity, normal), 0);
            }
            else
            {
                var wall = col.collider.gameObject.name;
                relSpeed = wall.StartsWith("WallL") || wall.StartsWith("WallR")
                    ? Mathf.Abs(_prevVelocity.x) : Mathf.Abs(_prevVelocity.y);
            }
            if (relSpeed >= COLLIDE_SFX_SPEED_MIN)
            {
                if (AppConfig.MoveVibrationOn)
                    Haptics.Play(HapticLevel.VeryWeak);
            }
        }

        void PickNewFloatDirection()
        {
            float ang = Random.value * Mathf.PI * 2f;
            _floatTargetDir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            _floatTargetSpeed = Random.Range(FLOAT_MIN_SPEED, FLOAT_MAX_SPEED);
        }

        public void ResetImageFloat()
        {
            _floatOffset = Vector2.zero;
            ImagesRoot.localPosition = _floatOrigin;
        }

        public void ResetImageRotation() { ImagesRoot.rotation = Quaternion.identity; }

        public void SetPlayBounds(float left, float right, float floorY)
        {
            _boundLeft = left; _boundRight = right; _boundFloor = floorY;
            _boundsSet = true;
        }

        public void MarkLandedSettled() { HasLanded = true; }

        // ------------------------------------------------------------ selection / glow
        public void SetSelected(bool on)
        {
            BubbleSprite.material.SetFloat("_Selected", on ? 1f : 0f);
        }

        void ShowGlow(bool on)
        {
            if (_glowCo != null) StopCoroutine(_glowCo);
            float targetGlowA = on ? GLOW_DRAG_ALPHA : 0f;
            float dur = on ? GLOW_FADE_IN : GLOW_FADE_OUT;
            _glowCo = StartCoroutine(FadeSprite(BubbleGlow, targetGlowA, dur));
        }

        void ShowGlowTarget(bool on)
        {
            if (_glowTargetCo != null) StopCoroutine(_glowTargetCo);
            float a = on ? GLOW_TARGET_ALPHA : 0f;
            float dur = on ? GLOW_FADE_IN : GLOW_FADE_OUT;
            _glowTargetCo = StartCoroutine(FadeSprite(BubbleGlowTarget, a, dur));
        }

        IEnumerator FadeSprite(SpriteRenderer sr, float targetA, float dur)
        {
            float from = sr.color.a;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                var c = sr.color;
                c.a = Mathf.Lerp(from, targetA, Mathf.Clamp01(t / dur));
                sr.color = c;
                yield return null;
            }
            var cc = sr.color; cc.a = targetA; sr.color = cc;
        }

        public void ClearSelectGlowInstant()
        {
            if (_glowCo != null) StopCoroutine(_glowCo);
            if (_glowTargetCo != null) StopCoroutine(_glowTargetCo);
            SetAlpha(BubbleGlow, 0);
            SetAlpha(BubbleGlowTarget, 0);
            SetAlpha(BubbleSprite, 1);
        }

        static void SetAlpha(SpriteRenderer sr, float a)
        {
            if (sr == null) return;
            var c = sr.color; c.a = a; sr.color = c;
        }

        // ------------------------------------------------------------ pickup enlarge
        public void SetPickupEnlarged(bool on)
        {
            if (on)
            {
                if (State != BubbleState.Alive) return;
                _pickupEnlarged = true;
                if (_pickupCo != null) StopCoroutine(_pickupCo);
                _pickupCo = StartCoroutine(Tween.Scale(
                    VisualRoot,
                    Vector3.one * AppConfig.PickupEnlargePeak,
                    PICKUP_ENLARGE_UP_DUR,
                    Ease.OutCubic));
                if (!Dragging) { ZIndex = ENLARGE_HOVER_Z_INDEX; _pickupZRaised = true; }
            }
            else
            {
                if (!_pickupEnlarged) return;
                _pickupEnlarged = false;
                if (_pickupCo != null) StopCoroutine(_pickupCo);
                _pickupCo = StartCoroutine(Tween.Scale(VisualRoot, Vector3.one, PICKUP_ENLARGE_DOWN_DUR, Ease.OutCubic));
                if (_pickupZRaised) { _pickupZRaised = false; ZIndex = 0; }
            }
        }

        public void CancelPickupEnlargeForMerge()
        {
            _pickupEnlarged = false;
            _pickupZRaised = false;
            if (_pickupCo != null) StopCoroutine(_pickupCo);
        }

        // ------------------------------------------------------------ ripple (neighbor push while drag)
        public void PlayRipple(Vector2 worldDir, float amplitude, float duration, float delay = 0)
        {
            if (State != BubbleState.Alive || amplitude <= 0) return;
            if (_rippleCo != null) StopCoroutine(_rippleCo);
            _rippleCo = StartCoroutine(RippleCo(worldDir.normalized * amplitude, duration, delay));
        }

        IEnumerator RippleCo(Vector2 localPeak, float duration, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            Vector3 peak = _visualBase + (Vector3)localPeak;
            yield return Tween.MoveLocal(VisualRoot, peak, duration * 0.4f, Ease.OutSine);
            yield return Tween.MoveLocal(VisualRoot, _visualBase, duration * 0.6f, Ease.InOutSine);
        }

        public void ResetRipple()
        {
            if (_rippleCo != null) StopCoroutine(_rippleCo);
            VisualRoot.localPosition = _visualBase;
        }

        // ------------------------------------------------------------ drag
        public void StartDrag()
        {
            if (State != BubbleState.Alive) return;
            ResetRipple();
            Dragging = true;
            StopFallTrail(clear: false);
            ReturningHome = false;
            DragOrigin = transform.position;
            _savedLayer = gameObject.layer;
            Collider.enabled = false;
            Body.isKinematic = true;
            Body.velocity = Vector2.zero;
            ZIndex = DRAG_Z_INDEX;
            ShowGlow(true);
            SpawnPlaceholder();
        }

        void SpawnPlaceholder()
        {
            RemovePlaceholder();
            _placeholder = new GameObject("Placeholder");
            _placeholder.transform.SetParent(transform.parent, false);
            _placeholder.transform.position = DragOrigin;
            var col = _placeholder.AddComponent<CircleCollider2D>();
            col.radius = GetRadius();
            var rb = _placeholder.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
        }

        void RemovePlaceholder()
        {
            if (_placeholder != null) Destroy(_placeholder);
            _placeholder = null;
        }

        void OnDestroy()
        {
            RemovePlaceholder();
            ReleaseImagePieces();
            if (_runtimeGlassMaterial != null) Destroy(_runtimeGlassMaterial);
            _runtimeGlassMaterial = null;
            if (_ownedFallTrailMaterial != null) Destroy(_ownedFallTrailMaterial);
            _ownedFallTrailMaterial = null;
        }

        public void UpdateDrag(Vector3 target)
        {
            if (!Dragging) return;
            transform.position = target;
        }

        public void ClearDragFlag()
        {
            Dragging = false;
            SetSelected(false);
            ShowGlow(false);
            SetPickupEnlarged(false);
        }

        float _returnStartTime = -1f;

        public IEnumerator TweenReturnHome()
        {
            ReturningHome = true;
            _returnStartTime = Time.time;
            float d = Vector3.Distance(transform.position, DragOrigin);
            float dur = Mathf.Clamp(d / 1800f, 0.1f, 0.32f);
            yield return Tween.Move(transform, DragOrigin, dur, Ease.OutQuad);
        }

        /// <summary>Bubble-owned return flight that always ends in RestorePhysics,
        /// so an interrupted caller can never leave the bubble frozen.</summary>
        public Coroutine StartReturnHome() => StartCoroutine(ReturnHomeAndRestoreCo());

        IEnumerator ReturnHomeAndRestoreCo()
        {
            ClearDragFlag();
            yield return TweenReturnHome();
            RestorePhysics();
        }

        /// <summary>Self-heal: a bubble stuck in ReturningHome (its host coroutine
        /// died mid-flight) becomes unpickable forever; free it after a timeout.</summary>
        void CheckReturnHomeWatchdog()
        {
            if (!ReturningHome || Dragging || State != BubbleState.Alive) return;
            if (_returnStartTime >= 0 && Time.time - _returnStartTime > 2f)
            {
                Debug.LogWarning($"[BubbleView] return-home watchdog freed {name}");
                RestorePhysics();
            }
        }

        public void RestorePhysics()
        {
            ReturningHome = false;
            _returnStartTime = -1f;
            SetPickupEnlarged(false);
            RemovePlaceholder();
            Collider.enabled = true;
            Body.isKinematic = false;
            ZIndex = 0;
            Body.velocity = Vector2.zero;
            Body.WakeUp();
        }

        public void SetHovered(bool on)
        {
            if (State != BubbleState.Alive) return;
            ShowGlowTarget(on);
            if (!on) SetPickupEnlarged(false);
        }

        // ------------------------------------------------------------ reject
        const int REJECT_FLASH_COUNT = 1;
        const float REJECT_FLASH_ON = 0.16f;
        const float REJECT_FLASH_OFF = 0.16f;
        const float REJECT_FLASH_PEAK_ALPHA = 0.5f;

        public void FlashReject()
        {
            if (State != BubbleState.Alive || RejectFlash == null) return;
            if (_rejectCo != null) StopCoroutine(_rejectCo);
            _rejectCo = StartCoroutine(RejectCo());
        }

        IEnumerator RejectCo()
        {
            for (int i = 0; i < REJECT_FLASH_COUNT; i++)
            {
                yield return FadeSprite(RejectFlash, REJECT_FLASH_PEAK_ALPHA, REJECT_FLASH_ON);
                yield return FadeSprite(RejectFlash, 0, REJECT_FLASH_OFF);
            }
        }

        const float REJECT_SHAKE_DIAMETER_RATIO = 0.06f;
        const float REJECT_SHAKE_DURATION = 0.3f;
        const float REJECT_SHAKE_DECAY = 0.6f;

        public void TweenRejectShake()
        {
            if (State != BubbleState.Alive) return;
            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeCo());
        }

        IEnumerator ShakeCo()
        {
            float amp = GetRadius() * 2f * REJECT_SHAKE_DIAMETER_RATIO;
            float baseX = _visualBase.x;
            float[] offsets =
            {
                amp,
                -amp * REJECT_SHAKE_DECAY,
                amp * REJECT_SHAKE_DECAY * REJECT_SHAKE_DECAY,
                -amp * REJECT_SHAKE_DECAY * REJECT_SHAKE_DECAY * REJECT_SHAKE_DECAY,
                0f,
            };
            float seg = REJECT_SHAKE_DURATION / offsets.Length;
            foreach (float off in offsets)
            {
                Vector3 to = new Vector3(baseX + off, VisualRoot.localPosition.y, 0);
                yield return Tween.MoveLocal(VisualRoot, to, seg, Ease.InOutSine);
            }
            VisualRoot.localPosition = _visualBase;
        }

        // ------------------------------------------------------------ hint
        const int HINT_FLASH_COUNT = 3;
        const float HINT_FLASH_ON = 0.18f;
        const float HINT_FLASH_OFF = 0.14f;

        public void FlashHint()
        {
            if (State != BubbleState.Alive) return;
            ClearHintHighlight();
            if (_hintCo != null) StopCoroutine(_hintCo);
            _hintCo = StartCoroutine(HintCo());
        }

        IEnumerator HintCo()
        {
            SetAlpha(HintGlow, 0);
            for (int i = 0; i < HINT_FLASH_COUNT; i++)
            {
                yield return FadeSprite(HintGlow, 1f, HINT_FLASH_ON);
                yield return FadeSprite(HintGlow, 0f, HINT_FLASH_OFF);
            }
            ShowHintGlow(true);
        }

        void ShowHintGlow(bool on)
        {
            if (_hintGlowCo != null) StopCoroutine(_hintGlowCo);
            _hintGlowCo = StartCoroutine(FadeSprite(HintGlow, on ? 1f : 0f, on ? HINT_GLOW_FADE_IN : HINT_GLOW_FADE_OUT));
        }

        public void ClearHintHighlight()
        {
            if (_hintCo != null) StopCoroutine(_hintCo);
            if (State == BubbleState.Alive) ShowHintGlow(false);
        }

        public void ClearHintHighlightInstant()
        {
            if (_hintCo != null) StopCoroutine(_hintCo);
            if (_hintGlowCo != null) StopCoroutine(_hintGlowCo);
            SetAlpha(HintGlow, 0);
        }

        public void SetHintHighlight(bool on) { ShowHintGlow(on); }

        // ------------------------------------------------------------ merge / explode / closure
        public void MarkMerging()
        {
            State = BubbleState.Merging;
            Collider.enabled = false;
            Dragging = false;
            SetSelected(false);
            CancelPickupEnlargeForMerge();
            Body.isKinematic = true;
            Body.velocity = Vector2.zero;
            RemovePlaceholder();
            ResetImageFloat();
        }

        public void Explode()
        {
            if (State == BubbleState.Exploding || State == BubbleState.Dead) return;
            State = BubbleState.Exploding;
            Collider.enabled = false;
            Body.isKinematic = true;
            MuteCollideFor(0.9f);
            ImagesRoot.gameObject.SetActive(false);
            ZIndex = 110;

            var mat = BubbleSprite.material;
            mat.SetTexture("_DissolveNoise", DissolveNoise());
            mat.SetVector("_DissolveDirection", new Vector4(0, 1, 0, 0));
            mat.SetFloat("_DissolveNoiseStrength", 0.3f);
            mat.SetFloat("_DissolveEdgeWidth", 0.14f);
            mat.SetColor("_DissolveEdgeColor", new Color(0.85f, 0.98f, 1f, 1f));
            mat.SetFloat("_DissolveProgress", 0f);

            Fx.SpawnDissolveBurst(transform.position, GetRadius(), transform.parent);
            Fx.SpawnLightFlashRing(transform.position, GetRadius(), transform.parent);
            Fx.SpawnShockwaveRing(transform.position, GetRadius(), transform.parent);

            SetAlpha(BubbleGlow, 0); SetAlpha(BubbleGlowTarget, 0);
            SetAlpha(HintGlow, 0); SetAlpha(RejectFlash, 0);

            StartCoroutine(ExplodeCo(mat));
        }

        IEnumerator ExplodeCo(Material mat)
        {
            const float EXPLODE_DURATION = 0.55f;
            float t = 0;
            while (t < EXPLODE_DURATION)
            {
                t += Time.deltaTime;
                mat.SetFloat("_DissolveProgress", Mathf.Lerp(0, 1.2f, t / EXPLODE_DURATION));
                yield return null;
            }
            State = BubbleState.Dead;
            Destroy(gameObject);
        }

        public void PrepareRevealHandoff()
        {
            if (State == BubbleState.Exploding || State == BubbleState.Dead) return;
            State = BubbleState.Exploding;
            Collider.enabled = false;
            Body.isKinematic = true;
            MuteCollideFor(0.9f);
            SetAlpha(BubbleGlow, 0); SetAlpha(BubbleGlowTarget, 0);
            SetAlpha(HintGlow, 0); SetAlpha(RejectFlash, 0);
        }

        /// <summary>New closure: droplets + rings + glass fade/expand; leaves photo visible.</summary>
        public void PlayClosureEffectsOnly(bool rainbowVariant = false)
        {
            if (State == BubbleState.Exploding || State == BubbleState.Dead) return;
            State = BubbleState.Exploding;
            Collider.enabled = false;
            Body.isKinematic = true;
            MuteCollideFor(0.9f);

            if (rainbowVariant)
            {
                Fx.SpawnWaterDroplets(transform.position, GetRadius(), transform.parent, 2.0f, true);
                Fx.SpawnClosureRing(transform.position, GetRadius(), 0f);
                Fx.SpawnClosureRing(transform.position, GetRadius(), 6f / 60f);
                Fx.SpawnClosureRing(transform.position, GetRadius(), 12f / 60f);
                Fx.SpawnClosureRing(transform.position, GetRadius(), 18f / 60f);
            }
            else
            {
                Fx.SpawnWaterDroplets(transform.position, GetRadius(), transform.parent, 1.0f, false);
                Fx.SpawnClosureRing(transform.position, GetRadius(), 0f);
                Fx.SpawnClosureRing(transform.position, GetRadius(), 12f / 60f);
            }

            SetAlpha(BubbleGlow, 0); SetAlpha(BubbleGlowTarget, 0);
            SetAlpha(HintGlow, 0); SetAlpha(RejectFlash, 0);

            if (BubbleSprite != null && BubbleSprite.sprite != null)
            {
                float rVis = GetRadius() * GLASS_VISUAL_OVERSCAN;
                ScaleToDiameter(BubbleSprite, 2f * rVis);
                StartCoroutine(ClosureGlassFadeCo());
            }
        }

        IEnumerator ClosureGlassFadeCo()
        {
            float dur = 10f / 60f;
            var start = BubbleSprite.transform.localScale;
            var end = start * 1.6f;
            float t = 0;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Ease.OutQuad.Evaluate(Mathf.Clamp01(t / dur));
                BubbleSprite.transform.localScale = Vector3.Lerp(start, end, k);
                SetAlpha(BubbleSprite, 1f - k);
                yield return null;
            }
        }

        public void PlayClosureShowcase(bool rainbowVariant = false)
        {
            if (State == BubbleState.Dead) return;
            if (State != BubbleState.Exploding) PlayClosureEffectsOnly(rainbowVariant);
            StartCoroutine(ClosureShowcaseEndCo());
        }

        IEnumerator ClosureShowcaseEndCo()
        {
            yield return new WaitForSeconds(CLOSURE_PRE_HIDE_FRAMES / 60f);
            if (this == null) yield break;
            if (BubbleSprite != null) BubbleSprite.gameObject.SetActive(false);
            ImagesRoot.gameObject.SetActive(false);
            State = BubbleState.Dead;
            Destroy(gameObject);
        }
    }
}
