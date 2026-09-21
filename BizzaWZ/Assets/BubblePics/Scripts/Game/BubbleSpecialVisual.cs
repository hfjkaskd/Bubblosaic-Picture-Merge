using TMPro;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Optional, prefab-friendly overlay for the 1.0.9 bubble mechanics. The
    /// fixed BubbleEntity prefab may serialize this component; old prefabs are
    /// upgraded safely at runtime. Every sprite has a Resources fallback, so a
    /// partially imported 1.0.9 asset set remains playable.
    /// </summary>
    public sealed class BubbleSpecialVisual : MonoBehaviour
    {
        static readonly int ColorProperty =
            Shader.PropertyToID("_Color");
        static readonly int BaseColorProperty =
            Shader.PropertyToID("_BaseColor");
        static readonly int TintColorProperty =
            Shader.PropertyToID("_TintColor");
        // MaterialPropertyBlock allocates native Unity state. It cannot be
        // constructed by this MonoBehaviour's static initializer while Unity
        // is deserializing/AddComponent-ing a BubbleEntity, so create it on
        // first renderer use instead.
        static MaterialPropertyBlock _alphaPropertyBlock;

        [SerializeField] Transform _root;
        [SerializeField] SpriteRenderer _back;
        [SerializeField] SpriteRenderer _front;
        [SerializeField] TextMesh _counter;
        [SerializeField] TextMeshPro _counterTmp;
        [SerializeField] SpineLite.SpineSprite _lockSpine;
        [SerializeField] SpineLite.SpineSprite _starfishSpine;

        BubbleView _bubble;
        bool _unlocking;
        string _lockAnimation;

        public void Bind(BubbleView bubble)
        {
            _bubble = bubble;
            EnsureAuthoring();
            SetSortingBase(_bubble != null ? _bubble.ZIndex : 0);
        }

        public void EnsureAuthoring()
        {
            if (_root == null)
            {
                var child = transform.Find("SpecialVisual");
                if (child == null)
                {
                    child = new GameObject("SpecialVisual").transform;
                    child.SetParent(transform, false);
                }
                _root = child;
            }
            _back = EnsureSprite(_back, "Back", 18);
            _front = EnsureSprite(_front, "Front", 20);
            if (_counter == null)
            {
                var child = _root.Find("Counter");
                if (child == null)
                {
                    child = new GameObject("Counter").transform;
                    child.SetParent(_root, false);
                }
                _counter = child.GetComponent<TextMesh>();
                if (_counter == null) _counter = child.gameObject.AddComponent<TextMesh>();
                _counter.anchor = TextAnchor.MiddleCenter;
                _counter.alignment = TextAlignment.Center;
                _counter.fontSize = 64;
                _counter.characterSize = 1f;
                _counter.color = Color.white;
                var renderer = _counter.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sortingOrder = 22;
            }
            // Counter was originally authored with Unity's legacy TextMesh,
            // whose font and glyph metrics cannot reproduce the Godot labels.
            // Keep the serialized component for prefab compatibility, but use
            // a TMP SDF label backed by the recovered BaggageGo font at runtime.
            if (_counter != null)
                _counter.gameObject.SetActive(false);
            if (_counterTmp == null)
            {
                Transform child = _root.Find("CounterTmp");
                if (child == null)
                {
                    child = new GameObject("CounterTmp").transform;
                    child.SetParent(_root, false);
                }
                _counterTmp = child.GetComponent<TextMeshPro>();
                if (_counterTmp == null)
                    _counterTmp = child.gameObject.AddComponent<TextMeshPro>();
            }
            ConfigureCounterBase();
        }

        void ConfigureCounterBase()
        {
            if (_counterTmp == null) return;
            _counterTmp.font = AssetLib.NumFont;
            _counterTmp.fontStyle = FontStyles.Normal;
            _counterTmp.fontWeight = FontWeight.Regular;
            _counterTmp.alignment = TextAlignmentOptions.Center;
            _counterTmp.color = Color.white;
            _counterTmp.enableAutoSizing = false;
            _counterTmp.enableWordWrapping = false;
            _counterTmp.overflowMode = TextOverflowModes.Overflow;
            _counterTmp.margin = Vector4.zero;
            _counterTmp.extraPadding = true;
            _counterTmp.richText = false;
        }

        SpriteRenderer EnsureSprite(SpriteRenderer current, string childName, int order)
        {
            if (current != null) return current;
            var child = _root.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(_root, false);
            }
            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = child.gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return renderer;
        }

        public void Refresh()
        {
            if (_bubble == null) return;
            EnsureAuthoring();

            if (_bubble.IsLocked)
            {
                _unlocking = false;
                SetImageContentVisible(false, 1f);
            }
            else if (!_unlocking)
            {
                SetImageContentVisible(true, 1f);
            }

            string backPath = null;
            string frontPath = null;
            string label = "";
            Color fallback = Color.clear;

            if (_bubble.IsBomb)
            {
                backPath = "Art/Sprites/Bubble/bomb_ring";
                frontPath = "Art/Sprites/Bubble/bomb_icon";
                label = Mathf.Max(0, _bubble.BombCount).ToString();
                fallback = new Color(1f, 0.28f, 0.08f, 0.42f);
            }
            else if (_bubble.IsLocked)
            {
                backPath = "Art/Sprites/Bubble/gp_pic_locked_bubble";
                label = Mathf.Max(0, _bubble.LockCount).ToString();
                fallback = new Color(0.17f, 0.38f, 0.76f, 0.55f);
            }
            else if (_bubble.IsStarfish)
            {
                frontPath = "Art/Sprites/Bubble/starfish_bubble_normal";
                label = Mathf.CeilToInt(_bubble.StarfishSecondsLeft).ToString();
                fallback = new Color(1f, 0.78f, 0.18f, 0.48f);
            }
            else if (_bubble.Fragment != null && _bubble.Fragment.IsRainbow)
            {
                frontPath = "Art/Sprites/Bubble/rainbow_bubble";
                fallback = new Color(0.68f, 0.35f, 1f, 0.5f);
            }
            else if (_bubble.IsMagnetBubble)
            {
                frontPath = "Art/Sprites/Bubble/magnet_bubble_dophin";
                fallback = new Color(0.18f, 0.82f, 0.96f, 0.48f);
            }
            else if (_bubble.Fragment != null && _bubble.Fragment.PendingStickerReturn)
            {
                int shape = Mathf.Max(0, _bubble.Fragment.StickerShapeId);
                string name = BubbleSpecialRules.StickerShapeName(shape);
                // The donor keeps the ivory outlined hole. The detached half
                // is rendered from the source photo through the shape mask by
                // BubbleView, matching Godot's sticker_mask.gdshader.
                if (_bubble.Fragment.StickerHalf == 1)
                {
                    frontPath =
                        "Art/Sprites/Bubble/sticker_puzzle_fill_" + name;
                    fallback = new Color(1f, 0.58f, 0.15f, 0.45f);
                }
            }

            Apply(_back, backPath, fallback);
            Apply(_front, frontPath, fallback);
            RefreshLockSpine();
            RefreshStarfishSpine();
            _counterTmp.text = label;
            _counterTmp.color = Color.white;
            _counterTmp.gameObject.SetActive(!string.IsNullOrEmpty(label));

            SetSortingBase(_bubble.ZIndex);

            float diameter = _bubble.GetRadius() * 2f;
            ScaleToDiameter(_back, diameter * 1.05f);
            ScaleToDiameter(
                _front,
                _bubble.IsBomb
                    ? diameter * (95.6f / 270f)
                    : _bubble.IsStarfish
                    ? diameter * 0.70f
                    : diameter * 0.78f);
            float radius = _bubble.GetRadius();
            if (_bubble.IsBomb)
            {
                Vector3 iconCenter = new Vector3(
                    diameter * (-86f / 270f),
                    diameter * (93f / 270f),
                    0f);
                _front.transform.localPosition = iconCenter;
                _front.transform.localRotation =
                    Quaternion.Euler(0f, 0f, -10f);
                ConfigureCounter(
                    41f,
                    new Vector2(90f, 60f),
                    diameter / 270f,
                    new Color32(0x59, 0x0C, 0x00, 0xFF),
                    5f,
                    Color.clear,
                    Vector2.zero);
                _counterTmp.transform.localPosition =
                    iconCenter + new Vector3(
                        diameter * (-4f / 270f),
                        diameter * (-4f / 270f),
                        0f);
            }
            else if (_bubble.IsLocked)
            {
                ConfigureCounter(
                    76f,
                    new Vector2(80f, 76f),
                    diameter / 320f,
                    new Color32(0x11, 0x35, 0x79, 0xFF),
                    3f,
                    new Color32(0x11, 0x35, 0x79, 0xFF),
                    new Vector2(0f, -3f));
                _counterTmp.transform.localPosition =
                    new Vector3(0f, radius * 0.625f, 0f);
            }
            else
            {
                ConfigureCounter(
                    76f,
                    new Vector2(120f, 84f),
                    diameter / 240f,
                    new Color32(0x11, 0x35, 0x79, 0xFF),
                    18f,
                    new Color32(0x11, 0x35, 0x79, 0xFF),
                    new Vector2(0f, -3f));
                _counterTmp.transform.localPosition =
                    new Vector3(0f, radius * 0.60f, 0f);
            }
            if (_bubble.IsStarfish)
            {
                // Godot design-space centers: icon (0,+37), number (0,-72)
                // in a 240 px overlay. Convert y-down to Unity y-up.
                _front.transform.localPosition =
                    new Vector3(0f, -diameter * (37f / 240f), 0f);
                _counterTmp.transform.localPosition =
                    new Vector3(0f, diameter * (72f / 240f), 0f);
            }
            else if (!_bubble.IsBomb)
            {
                _front.transform.localPosition = Vector3.zero;
                _front.transform.localRotation = Quaternion.identity;
            }
            _root.gameObject.SetActive(
                _back.sprite != null || _front.sprite != null ||
                (_lockSpine != null && _lockSpine.gameObject.activeSelf) ||
                (_starfishSpine != null &&
                 _starfishSpine.gameObject.activeSelf) ||
                fallback.a > 0f || !string.IsNullOrEmpty(label));
        }

        void ConfigureCounter(
            float fontSize,
            Vector2 labelSize,
            float designScale,
            Color outlineColor,
            float outlineSize,
            Color shadowColor,
            Vector2 shadowOffset)
        {
            if (_counterTmp == null) return;
            ConfigureCounterBase();
            _counterTmp.fontSize = fontSize;
            _counterTmp.rectTransform.sizeDelta = labelSize;
            // TMP's world glyph geometry is authored in tenths of a Unity
            // unit. The x10 compensation restores Godot design pixels, while
            // designScale maps each mechanism's native overlay to the bubble.
            _counterTmp.rectTransform.localScale =
                Vector3.one * (10f * Mathf.Max(0.0001f, designScale));
            TmpTextStyle.ApplyOutlineAndShadow(
                _counterTmp,
                outlineColor,
                outlineSize,
                shadowColor,
                shadowOffset);
            TmpTextStyle.ApplyFaceDilation(_counterTmp, 0.025f);
        }

        void RefreshStarfishSpine()
        {
            bool visible = _bubble != null && _bubble.IsStarfish;
            if (!visible)
            {
                if (_starfishSpine != null)
                    _starfishSpine.gameObject.SetActive(false);
                return;
            }

            if (_starfishSpine == null)
            {
                Transform child = _root.Find("StarfishSpine");
                if (child == null)
                {
                    child = new GameObject("StarfishSpine").transform;
                    child.SetParent(_root, false);
                }
                _starfishSpine =
                    child.GetComponent<SpineLite.SpineSprite>();
                if (_starfishSpine == null)
                    _starfishSpine =
                        child.gameObject.AddComponent<SpineLite.SpineSprite>();
            }
            _starfishSpine.gameObject.SetActive(true);
            if (_starfishSpine.Data == null)
                _starfishSpine.Load("starfish");
            if (_starfishSpine.Data != null &&
                (_starfishSpine.Current == null ||
                 _starfishSpine.Current.Animation == null ||
                 _starfishSpine.Current.Animation.Name != "standby"))
                _starfishSpine.SetAnimation("standby", true, 0f);
            float diameter = _bubble.GetRadius() * 2f;
            _starfishSpine.transform.localPosition = Vector3.zero;
            _starfishSpine.transform.localScale =
                Vector3.one * (diameter / 240f);
        }

        public bool PlayStarfishHarvest()
        {
            if (_bubble == null) return false;
            EnsureAuthoring();
            RefreshStarfishSpine();
            if (_starfishSpine == null || _starfishSpine.Data == null ||
                !_starfishSpine.HasAnimation("harvest"))
                return false;
            if (_front != null) _front.gameObject.SetActive(false);
            if (_counterTmp != null) _counterTmp.gameObject.SetActive(false);
            return _starfishSpine.SetAnimation("harvest", false, 0f) != null;
        }

        void RefreshLockSpine()
        {
            bool visible = _bubble != null && _bubble.IsLocked;
            if (!visible)
            {
                if (_lockSpine != null)
                    _lockSpine.gameObject.SetActive(false);
                return;
            }

            if (_lockSpine == null)
            {
                var child = _root.Find("LockSpine");
                if (child == null)
                {
                    child = new GameObject("LockSpine").transform;
                    child.SetParent(_root, false);
                }
                _lockSpine = child.GetComponent<SpineLite.SpineSprite>();
                if (_lockSpine == null)
                    _lockSpine = child.gameObject.AddComponent<SpineLite.SpineSprite>();
            }
            _lockSpine.gameObject.SetActive(true);
            if (_lockSpine.Data == null)
                _lockSpine.Load("bubble_unlock");
            float diameter = _bubble.GetRadius() * 2f;
            _lockSpine.transform.localPosition = Vector3.zero;
            _lockSpine.transform.localScale = Vector3.one * (diameter / 240f);
            RefreshLockMotion();
        }

        void Update()
        {
            if (_bubble != null && _bubble.IsLocked && !_unlocking)
                RefreshLockMotion();
        }

        void RefreshLockMotion()
        {
            if (_lockSpine == null || _lockSpine.Data == null ||
                _bubble == null || !_bubble.IsLocked)
                return;
            string animation = _bubble.HasLanded && !_bubble.Dragging
                ? "idle"
                : "still";
            if (_lockAnimation == animation && _lockSpine.Current != null)
                return;
            if (!_lockSpine.HasAnimation(animation)) return;
            _lockAnimation = animation;
            _lockSpine.SetAnimation(animation, true, 0f);
        }

        public bool PlayUnlock()
        {
            if (_bubble == null) return false;
            EnsureAuthoring();
            _unlocking = true;
            SetImageContentVisible(true, 0f);

            if (_back != null)
                StartCoroutine(Tween.Run(
                    0.4f,
                    value => SetRendererAlpha(_back, 1f - value),
                    Ease.OutSine));
            if (_counterTmp != null)
                StartCoroutine(Tween.Run(
                    0.4f,
                    value => SetTextAlpha(_counterTmp, 1f - value),
                    Ease.OutSine));
            StartCoroutine(Tween.Run(
                0.4f,
                value => SetImageContentAlpha(value),
                Ease.OutSine));

            float finishDelay = 0.4f;
            bool animated = false;
            if (_lockSpine != null && _lockSpine.Data != null &&
                _lockSpine.HasAnimation("die"))
            {
                _lockSpine.gameObject.SetActive(true);
                _lockAnimation = "die";
                SpineLite.TrackEntry entry =
                    _lockSpine.SetAnimation("die", false, 0f);
                if (entry != null)
                {
                    animated = true;
                    finishDelay = Mathf.Max(finishDelay, entry.AnimationEnd);
                }
            }

            TweenRunner.Delay(finishDelay, () =>
            {
                if (this == null) return;
                _unlocking = false;
                _lockAnimation = null;
                Refresh();
            });
            return animated;
        }

        void SetImageContentVisible(bool visible, float alpha)
        {
            if (_bubble == null || _bubble.ImagesRoot == null) return;
            _bubble.ImagesRoot.gameObject.SetActive(visible);
            if (visible) SetImageContentAlpha(alpha);
        }

        void SetImageContentAlpha(float alpha)
        {
            if (_bubble == null || _bubble.ImagesRoot == null) return;
            // Number/word content uses 3D TextMeshPro under ImagesRoot. Its
            // SDF material does not expose the generic _Color property; TMP
            // alpha belongs in vertex color and must be changed through the
            // component without cloning or mutating the shared font material.
            foreach (TMP_Text text in
                     _bubble.ImagesRoot.GetComponentsInChildren<TMP_Text>(true))
                SetTextAlpha(text, alpha);
            foreach (SpriteRenderer renderer in
                     _bubble.ImagesRoot.GetComponentsInChildren<SpriteRenderer>(true))
                SetRendererAlpha(renderer, alpha);
            foreach (MeshRenderer renderer in
                     _bubble.ImagesRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                // Includes TMP_SubMesh renderers as well as the renderer on
                // the TextMeshPro object itself. They have already inherited
                // the TMP vertex alpha above and must not be treated as image
                // materials.
                if (renderer.GetComponentInParent<TMP_Text>() != null)
                    continue;
                SetRendererAlpha(renderer, alpha);
            }
        }

        static void SetRendererAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Color color = renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }

        static void SetRendererAlpha(MeshRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            Material material = renderer.sharedMaterial;
            if (material == null) return;

            int property;
            if (material.HasProperty(ColorProperty))
                property = ColorProperty;
            else if (material.HasProperty(BaseColorProperty))
                property = BaseColorProperty;
            else if (material.HasProperty(TintColorProperty))
                property = TintColorProperty;
            else
                return;

            // A property block keeps the fade local to this bubble. Writing
            // sharedMaterial would fade every renderer using that material
            // and accessing Material.color would log for shaders without
            // _Color (notably TextMeshPro/Mobile/Distance Field).
            if (_alphaPropertyBlock == null)
                _alphaPropertyBlock = new MaterialPropertyBlock();
            _alphaPropertyBlock.Clear();
            renderer.GetPropertyBlock(_alphaPropertyBlock);
            Color color = material.GetColor(property);
            color.a = Mathf.Clamp01(alpha);
            _alphaPropertyBlock.SetColor(property, color);
            renderer.SetPropertyBlock(_alphaPropertyBlock);
        }

        static void SetTextAlpha(TMP_Text text, float alpha)
        {
            if (text == null) return;
            Color color = text.color;
            color.a = Mathf.Clamp01(alpha);
            text.color = color;
        }

        public void SetSortingBase(int bubbleZ)
        {
            if (_back != null)
                _back.sortingOrder = SortOrder.BubbleBand(bubbleZ, 6);
            if (_front != null)
                _front.sortingOrder = SortOrder.BubbleBand(bubbleZ, 7);
            if (_counterTmp != null)
            {
                var renderer = _counterTmp.GetComponent<MeshRenderer>();
                if (renderer != null)
                    renderer.sortingOrder = SortOrder.BubbleBand(bubbleZ, 8);
            }
            if (_lockSpine != null)
                _lockSpine.SortingOrder = SortOrder.BubbleBand(bubbleZ, 7);
            if (_starfishSpine != null)
                _starfishSpine.SortingOrder = SortOrder.BubbleBand(bubbleZ, 7);
        }

        static void Apply(SpriteRenderer renderer, string path, Color fallback)
        {
            if (renderer == null) return;
            renderer.sprite = string.IsNullOrEmpty(path) ? null : AssetLib.Sprite(path);
            renderer.color = renderer.sprite != null ? Color.white : fallback;
            renderer.gameObject.SetActive(renderer.sprite != null || fallback.a > 0f);
        }

        static void ScaleToDiameter(SpriteRenderer renderer, float diameter)
        {
            if (renderer == null || renderer.sprite == null) return;
            float width = renderer.sprite.rect.width /
                Mathf.Max(renderer.sprite.pixelsPerUnit, 0.0001f);
            float scale = diameter / Mathf.Max(width, 0.0001f);
            renderer.transform.localScale = Vector3.one * scale;
        }
    }
}
