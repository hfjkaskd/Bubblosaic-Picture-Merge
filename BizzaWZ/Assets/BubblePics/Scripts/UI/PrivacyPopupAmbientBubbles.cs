using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Pooled UGUI port of PopupAmbientBubbles configured by privacy_dialog.gd.
    /// The authored pool is visible in PrivacyDialog.prefab and no objects are
    /// allocated while the popup animation is running.
    /// </summary>
    public sealed class PrivacyPopupAmbientBubbles : MonoBehaviour
    {
        const float SpawnIntervalMin = 0.6f;
        const float SpawnIntervalMax = 1.4f;
        const float RiseSpeedMin = 60f;
        const float RiseSpeedMax = 110f;
        const float PopDelayMin = 1.5f;
        const float PopDelayMax = 4.5f;
        const float WobbleAmplitude = 8f;
        const float WobbleFrequency = 0.7f;
        const float FadeInDuration = 0.35f;
        const float ScaleMin = 0.9f;
        const float ScaleMax = 1.5f;
        const float AlphaMax = 0.65f;
        const float DialogHalfWidth = 540f;
        const float DialogHalfHeight = 662f;
        const float SideBandWidth = 180f;
        const float ForcePopY = DialogHalfHeight + 30f;

        const int RuntimeBubblePoolSize = 8;
        const int RuntimeFragmentPoolSize = 40;
        const int BurstAmount = 10;
        const float BurstLifetime = 0.8f;
        const float BurstVelocityMin = 25f;
        const float BurstVelocityMax = 70f;
        const float BurstScaleMin = 0.08f;
        const float BurstScaleMax = 0.28f;
        const float BurstEmitRadius = 30f;
        const float BurstGravityY = 40f;
        const float BurstAlphaPeak = 0.7f;

        struct BubbleState
        {
            public Image Image;
            public bool Active;
            public float Speed;
            public float Elapsed;
            public float PopAt;
            public float StartX;
            public float StartY;
            public float WobblePhase;
        }

        struct FragmentState
        {
            public Image Image;
            public bool Active;
            public float Elapsed;
            public float Damping;
            public Vector2 Velocity;
        }

        [SerializeField] Image[] _bubblePool = Array.Empty<Image>();
        [SerializeField] Image[] _fragmentPool = Array.Empty<Image>();

        BubbleState[] _bubbles = Array.Empty<BubbleState>();
        FragmentState[] _fragments = Array.Empty<FragmentState>();
        System.Random _random;
        bool _initialized;
        bool _running;
        float _spawnAccum;
        float _nextSpawnIn;

        public int AuthoredBubbleCount => _bubblePool != null ? _bubblePool.Length : 0;
        public int AuthoredFragmentCount => _fragmentPool != null ? _fragmentPool.Length : 0;

        public void InitializeRuntime()
        {
            if (_initialized) return;
            if ((_bubblePool == null || _bubblePool.Length == 0) &&
                (_fragmentPool == null || _fragmentPool.Length == 0))
            {
                CreateRuntimePool(AssetLib.Sprite(
                    "Art/Sprites/Common/common_decorations_bubble"));
            }

            _bubbles = new BubbleState[_bubblePool != null ? _bubblePool.Length : 0];
            for (int i = 0; i < _bubbles.Length; i++)
            {
                _bubbles[i].Image = _bubblePool[i];
                SetInactive(_bubblePool[i]);
            }

            _fragments = new FragmentState[_fragmentPool != null ? _fragmentPool.Length : 0];
            for (int i = 0; i < _fragments.Length; i++)
            {
                _fragments[i].Image = _fragmentPool[i];
                SetInactive(_fragmentPool[i]);
            }

            _random = new System.Random(
                unchecked(Environment.TickCount * 397 ^ GetInstanceID()));
            _spawnAccum = 0f;
            _nextSpawnIn = 0f;
            _running = false;
            _initialized = true;
        }

        public void StartEffect()
        {
            InitializeRuntime();
            if (_running) return;
            _running = true;
            _spawnAccum = 0f;
            _nextSpawnIn = 0f;
        }

        public void StopEffect()
        {
            _running = false;
            for (int i = 0; i < _bubbles.Length; i++)
            {
                _bubbles[i].Active = false;
                SetInactive(_bubbles[i].Image);
            }
            for (int i = 0; i < _fragments.Length; i++)
            {
                _fragments[i].Active = false;
                SetInactive(_fragments[i].Image);
            }
        }

        public void CreateRuntimePool(Sprite sprite)
        {
            if (sprite == null) return;
            var bubblesRoot = FindOrCreatePoolRoot("Bubbles");
            var fragmentsRoot = FindOrCreatePoolRoot("Fragments");
            _bubblePool = CreatePool(
                bubblesRoot, "Bubble", RuntimeBubblePoolSize, sprite);
            _fragmentPool = CreatePool(
                fragmentsRoot, "Fragment", RuntimeFragmentPoolSize, sprite);
            _initialized = false;
        }

        void Update()
        {
            if (!_running) return;
            float delta = Time.unscaledDeltaTime;
            _spawnAccum += delta;
            if (_spawnAccum >= _nextSpawnIn)
            {
                _spawnAccum = 0f;
                _nextSpawnIn = Range(SpawnIntervalMin, SpawnIntervalMax);
                SpawnBubble();
            }

            for (int i = 0; i < _bubbles.Length; i++)
            {
                var state = _bubbles[i];
                if (!state.Active || state.Image == null) continue;
                state.Elapsed += delta;
                float y = state.StartY + state.Speed * state.Elapsed;
                float x = state.StartX + WobbleAmplitude * Mathf.Sin(
                    state.Elapsed * Mathf.PI * 2f * WobbleFrequency +
                    state.WobblePhase);
                state.Image.rectTransform.anchoredPosition = new Vector2(x, y);
                var color = state.Image.color;
                color.a = AlphaMax * Mathf.Clamp01(state.Elapsed / FadeInDuration);
                state.Image.color = color;

                if (state.Elapsed >= state.PopAt || y > ForcePopY)
                {
                    Vector2 position = state.Image.rectTransform.anchoredPosition;
                    state.Active = false;
                    SetInactive(state.Image);
                    _bubbles[i] = state;
                    SpawnBurst(position);
                    continue;
                }
                _bubbles[i] = state;
            }

            for (int i = 0; i < _fragments.Length; i++)
            {
                var state = _fragments[i];
                if (!state.Active || state.Image == null) continue;
                state.Elapsed += delta;
                if (state.Elapsed >= BurstLifetime)
                {
                    state.Active = false;
                    SetInactive(state.Image);
                    _fragments[i] = state;
                    continue;
                }

                state.Velocity.y += BurstGravityY * delta;
                state.Velocity *= Mathf.Exp(-state.Damping * delta);
                state.Image.rectTransform.anchoredPosition +=
                    state.Velocity * delta;
                var color = state.Image.color;
                color.a = BurstAlphaPeak *
                          (1f - state.Elapsed / BurstLifetime);
                state.Image.color = color;
                _fragments[i] = state;
            }
        }

        void SpawnBubble()
        {
            for (int i = 0; i < _bubbles.Length; i++)
            {
                if (_bubbles[i].Active || _bubbles[i].Image == null) continue;
                Image image = _bubbles[i].Image;
                float scale = Range(ScaleMin, ScaleMax);
                float x = NextBool()
                    ? Range(-DialogHalfWidth, -DialogHalfWidth + SideBandWidth)
                    : Range(DialogHalfWidth - SideBandWidth, DialogHalfWidth);
                float y = Range(-DialogHalfHeight, DialogHalfHeight);

                image.gameObject.SetActive(true);
                image.rectTransform.localScale = new Vector3(scale, scale, 1f);
                image.rectTransform.anchoredPosition = new Vector2(x, y);
                image.color = new Color(1f, 1f, 1f, 0f);
                _bubbles[i] = new BubbleState
                {
                    Image = image,
                    Active = true,
                    Speed = Range(RiseSpeedMin, RiseSpeedMax),
                    PopAt = Range(PopDelayMin, PopDelayMax),
                    StartX = x,
                    StartY = y,
                    WobblePhase = Range(0f, Mathf.PI * 2f),
                };
                return;
            }
        }

        void SpawnBurst(Vector2 position)
        {
            for (int emitted = 0; emitted < BurstAmount; emitted++)
            {
                int slot = FindFreeFragment();
                if (slot < 0) return;
                Image image = _fragments[slot].Image;
                float radius = Mathf.Sqrt(Range(0f, 1f)) * BurstEmitRadius;
                float positionAngle = Range(0f, Mathf.PI * 2f);
                float velocityAngle = Range(0f, Mathf.PI * 2f);
                Vector2 velocity = new Vector2(
                    Mathf.Cos(velocityAngle),
                    Mathf.Sin(velocityAngle)) *
                    Range(BurstVelocityMin, BurstVelocityMax);
                float scale = Range(BurstScaleMin, BurstScaleMax);

                image.gameObject.SetActive(true);
                image.rectTransform.anchoredPosition = position +
                    new Vector2(
                        Mathf.Cos(positionAngle),
                        Mathf.Sin(positionAngle)) * radius;
                image.rectTransform.localScale = new Vector3(scale, scale, 1f);
                image.color = new Color(1f, 1f, 1f, BurstAlphaPeak);
                _fragments[slot] = new FragmentState
                {
                    Image = image,
                    Active = true,
                    Velocity = velocity,
                    Damping = Range(1f, 3f),
                };
            }
        }

        int FindFreeFragment()
        {
            for (int i = 0; i < _fragments.Length; i++)
                if (!_fragments[i].Active && _fragments[i].Image != null)
                    return i;
            return -1;
        }

        RectTransform FindOrCreatePoolRoot(string objectName)
        {
            var existing = transform.Find(objectName) as RectTransform;
            if (existing != null) return existing;
            var go = new GameObject(objectName, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        static Image[] CreatePool(
            RectTransform parent,
            string prefix,
            int count,
            Sprite sprite)
        {
            var result = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(
                    prefix + "_" + i.ToString("00"),
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(60f, 60f);
                var image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.raycastTarget = false;
                result[i] = image;
                go.SetActive(false);
            }
            return result;
        }

        float Range(float min, float max)
        {
            if (_random == null)
                _random = new System.Random(
                    unchecked(Environment.TickCount * 397 ^ GetInstanceID()));
            return min + (float)_random.NextDouble() * (max - min);
        }

        bool NextBool()
        {
            if (_random == null)
                _random = new System.Random(
                    unchecked(Environment.TickCount * 397 ^ GetInstanceID()));
            return _random.NextDouble() < 0.5;
        }

        static void SetInactive(Image image)
        {
            if (image != null) image.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        public void ConfigurePrefabAuthoring(Image[] bubbles, Image[] fragments)
        {
            _bubblePool = bubbles ?? Array.Empty<Image>();
            _fragmentPool = fragments ?? Array.Empty<Image>();
            _initialized = false;
        }
#endif
    }
}

#if UNITY_EDITOR
namespace BubblePics.EditorTools
{
    /// <summary>
    /// Keeps the restored privacy page fully authored and previewable. The
    /// batch entry point is also used by CI to rebuild and validate references.
    /// </summary>
    public static class PrivacyDialogPrefabMigration
    {
        const string PrefabPath =
            "Assets/Resources/Prefabs/Pages/PrivacyDialog.prefab";
        const string BubbleSpritePath =
            "Assets/Resources/Art/Sprites/Common/common_decorations_bubble.png";

        [UnityEditor.MenuItem(
            "Tools/BubblePics/Prefabs/Rebuild Privacy Dialog Effects")]
        public static void Rebuild()
        {
            var prefabRoot =
                UnityEditor.PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var page = prefabRoot.GetComponent<BubblePics.PrivacyDialog>();
                if (page == null)
                    throw new InvalidOperationException(
                        "PrivacyDialog component is missing.");

                var root = FindNested(
                    prefabRoot.transform, "PrivacyDialog",
                    candidate => candidate != prefabRoot.transform);
                var content = FindNested(root, "Content");
                var dialog = FindNested(content, "Dialog");
                var overlay = FindNested(root, "Overlay");
                var title = FindNested(dialog, "Title").GetComponent<TextMeshProUGUI>();
                var innerCard = FindNested(dialog, "InnerCard");
                var body = FindNested(innerCard, "Body").GetComponent<TextMeshProUGUI>();
                var acceptTransform = FindNested(dialog, "Accept");
                var acceptButton =
                    acceptTransform.GetComponent<BubblePics.CommonButton>();
                var acceptPress =
                    acceptTransform.GetComponentInChildren<
                        BubblePics.PressButton>(true);

                RemoveLegacyLinkNode(innerCard, "TermsLink");
                RemoveLegacyLinkNode(innerCard, "LinkConnector");
                RemoveLegacyLinkNode(innerCard, "PrivacyLink");

                var bodyRect = body.rectTransform;
                bodyRect.anchorMin = bodyRect.anchorMax =
                    new Vector2(0.5f, 0.5f);
                bodyRect.anchoredPosition = Vector2.zero;
                bodyRect.sizeDelta = new Vector2(736f, 409f);
                body.fontSize = 64;
                body.enableAutoSizing = false;
                body.enableWordWrapping = true;
                body.overflowMode = TextOverflowModes.Overflow;
                body.horizontalAlignment = HorizontalAlignmentOptions.Center;
                body.verticalAlignment = VerticalAlignmentOptions.Middle;
                body.richText = true;
                body.raycastTarget = true;
                var inline = body.GetComponent<
                    BubblePics.PrivacyInlineLinkText>();
                if (inline == null)
                    inline = body.gameObject.AddComponent<
                        BubblePics.PrivacyInlineLinkText>();
                inline.ConfigurePrefabAuthoring(body);
                inline.SetLocalizedTemplate(
                    BubblePics.Localization.Tr("please_read_accept"),
                    BubblePics.Localization.Tr("terms_service"),
                    BubblePics.Localization.Tr("privacy_policy"));

                title.fontSize = 110;
                title.enableAutoSizing = false;
                title.text = BubblePics.Localization.Tr("welcome");
                if (acceptButton != null)
                {
                    acceptButton.BindPrefabRuntime();
                    acceptButton.SetText(
                        BubblePics.Localization.Tr(
                            "PRIVACY_DIALOG_ACCEPT"));
                }

                var previousAmbient = root.Find("AmbientBubbles");
                if (previousAmbient != null)
                    UnityEngine.Object.DestroyImmediate(
                        previousAmbient.gameObject);
                var ambientGo = new GameObject(
                    "AmbientBubbles", typeof(RectTransform));
                var ambientRect = (RectTransform)ambientGo.transform;
                ambientRect.SetParent(root, false);
                ambientRect.anchorMin = Vector2.zero;
                ambientRect.anchorMax = Vector2.one;
                ambientRect.offsetMin = Vector2.zero;
                ambientRect.offsetMax = Vector2.zero;
                ambientRect.SetAsLastSibling();
                var ambient = ambientGo.AddComponent<
                    BubblePics.PrivacyPopupAmbientBubbles>();
                var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    BubbleSpritePath);
                if (sprite == null)
                    throw new InvalidOperationException(
                        "Privacy ambient bubble sprite is missing.");
                ambient.CreateRuntimePool(sprite);
                ambient.ConfigurePrefabAuthoring(
                    CollectImages(ambientRect.Find("Bubbles")),
                    CollectImages(ambientRect.Find("Fragments")));

                page.ConfigurePrefabAuthoring(
                    (RectTransform)root,
                    (RectTransform)content,
                    (RectTransform)dialog,
                    content.GetComponent<CanvasGroup>(),
                    overlay.GetComponent<CanvasGroup>(),
                    acceptButton,
                    acceptPress,
                    title,
                    body,
                    inline,
                    ambient);

                Validate(
                    root, content, dialog, overlay, title, body,
                    acceptButton, acceptPress, inline, ambient);
                UnityEditor.EditorUtility.SetDirty(page);
                UnityEditor.EditorUtility.SetDirty(inline);
                UnityEditor.EditorUtility.SetDirty(ambient);
                UnityEditor.PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot, PrefabPath);
            }
            finally
            {
                UnityEditor.PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log(
                "PrivacyDialog prefab rebuilt: inline links and pooled " +
                "ambient bubbles validated.");
        }

        public static void RebuildBatch()
        {
            Rebuild();
        }

        public static void ValidateBatch()
        {
            var prefabRoot =
                UnityEditor.PrefabUtility.LoadPrefabContents(PrefabPath);
            string previousLocale = BubblePics.Localization.CurrentLocale;
            try
            {
                var page = prefabRoot.GetComponent<BubblePics.PrivacyDialog>();
                if (page == null)
                    throw new InvalidOperationException(
                        "PrivacyDialog component is missing.");
                var root = FindNested(
                    prefabRoot.transform, "PrivacyDialog",
                    candidate => candidate != prefabRoot.transform);
                var content = FindNested(root, "Content");
                var dialog = FindNested(content, "Dialog");
                var overlay = FindNested(root, "Overlay");
                var title = FindNested(dialog, "Title").GetComponent<TextMeshProUGUI>();
                var body = FindNested(
                    FindNested(dialog, "InnerCard"), "Body").GetComponent<TextMeshProUGUI>();
                var acceptTransform = FindNested(dialog, "Accept");
                var acceptButton =
                    acceptTransform.GetComponent<BubblePics.CommonButton>();
                var acceptPress =
                    acceptTransform.GetComponentInChildren<
                        BubblePics.PressButton>(true);
                var inline =
                    body.GetComponent<BubblePics.PrivacyInlineLinkText>();
                var ambient = root.GetComponentInChildren<
                    BubblePics.PrivacyPopupAmbientBubbles>(true);

                Validate(
                    root, content, dialog, overlay, title, body,
                    acceptButton, acceptPress, inline, ambient);
                ValidateSerializedReferences(page, inline, ambient);
                ValidateInlineLinkLocales(inline);
            }
            finally
            {
                BubblePics.Localization.SetLocale(previousLocale);
                UnityEditor.PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
            Debug.Log(
                "PrivacyDialog validation passed: prefab references, " +
                "48 pooled effects, and localized inline links.");
        }

        static Transform FindNested(
            Transform parent,
            string objectName,
            Func<Transform, bool> predicate = null)
        {
            foreach (var candidate in
                     parent.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == objectName &&
                    (predicate == null || predicate(candidate)))
                    return candidate;
            }
            throw new InvalidOperationException(
                $"PrivacyDialog prefab node '{objectName}' is missing.");
        }

        static void RemoveLegacyLinkNode(
            Transform innerCard,
            string objectName)
        {
            var child = innerCard.Find(objectName);
            if (child != null)
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        static Image[] CollectImages(Transform parent)
        {
            return parent != null
                ? parent.GetComponentsInChildren<Image>(true)
                : Array.Empty<Image>();
        }

        static void Validate(
            Transform root,
            Transform content,
            Transform dialog,
            Transform overlay,
            TMP_Text title,
            TMP_Text body,
            BubblePics.CommonButton acceptButton,
            BubblePics.PressButton acceptPress,
            BubblePics.PrivacyInlineLinkText inline,
            BubblePics.PrivacyPopupAmbientBubbles ambient)
        {
            if (root == null || content == null || dialog == null ||
                overlay == null || title == null || body == null ||
                acceptButton == null || acceptPress == null ||
                inline == null || ambient == null)
                throw new InvalidOperationException(
                    "PrivacyDialog contains an unassigned required reference.");
            if (body.rectTransform.sizeDelta != new Vector2(736f, 409f) ||
                !body.raycastTarget || !body.richText)
                throw new InvalidOperationException(
                    "Privacy body layout is not authored as RichTextLabel.");
            if (ambient.AuthoredBubbleCount != 8 ||
                ambient.AuthoredFragmentCount != 40)
                throw new InvalidOperationException(
                    "Privacy ambient object pool must contain 8 bubbles and " +
                    "40 fragments.");
            if (body.transform.parent.Find("TermsLink") != null ||
                body.transform.parent.Find("LinkConnector") != null ||
                body.transform.parent.Find("PrivacyLink") != null)
                throw new InvalidOperationException(
                    "Fixed-position privacy link nodes still exist.");
            if (body.GetComponent<BubblePics.PressButton>() != null)
                throw new InvalidOperationException(
                    "Privacy inline links must not use button haptics or sound.");
        }

        static void ValidateSerializedReferences(
            BubblePics.PrivacyDialog page,
            BubblePics.PrivacyInlineLinkText inline,
            BubblePics.PrivacyPopupAmbientBubbles ambient)
        {
            var serialized = new UnityEditor.SerializedObject(page);
            if (serialized.FindProperty("_inlineLinks").objectReferenceValue !=
                    inline ||
                serialized.FindProperty("_ambientBubbles").objectReferenceValue !=
                    ambient ||
                serialized.FindProperty("_title").objectReferenceValue == null ||
                serialized.FindProperty("_body").objectReferenceValue == null ||
                serialized.FindProperty("_acceptButton").objectReferenceValue ==
                    null)
                throw new InvalidOperationException(
                    "PrivacyDialog serialized references are incomplete.");
        }

        static void ValidateInlineLinkLocales(
            BubblePics.PrivacyInlineLinkText inline)
        {
            string[] locales =
            {
                "zh_CN", "en", "de", "pl", "ru", "ja", "zh_TW",
            };
            foreach (string locale in locales)
            {
                BubblePics.Localization.SetLocale(locale);
                inline.SetLocalizedTemplate(
                    BubblePics.Localization.Tr("please_read_accept"),
                    BubblePics.Localization.Tr("terms_service"),
                    BubblePics.Localization.Tr("privacy_policy"));
                if (inline.LinkRegionCount < 2)
                    throw new InvalidOperationException(
                        $"Privacy inline link hit regions failed for {locale}.");
            }
        }
    }
}
#endif
