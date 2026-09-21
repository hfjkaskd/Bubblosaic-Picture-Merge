using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Shared port of bonus_level_banner.gd, merge_intro_banner.gd and
    /// sea_hero_banner.gd. Visual and copy differences remain serialized in
    /// their individual prefab assets.
    /// </summary>
    public sealed class MetaIntroBannerView : MetaFlowView
    {
        [SerializeField] RectTransform _banner;
        [SerializeField] Image _bannerImage;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _subtitle;
        [SerializeField] SpineLite.SpineSprite _spine;
        [SerializeField] string _spriteResourcePath;
        [SerializeField] string _spineResourceName;
        [SerializeField] string _titleKey;
        [SerializeField] string _titleFallback;
        [SerializeField] string _subtitleKey;
        [SerializeField] string _subtitleFallback;
        [SerializeField] string _soundKind;
        [SerializeField] bool _usesPercent;
        [SerializeField] int _percentMin;
        [SerializeField] int _percentMax;
        [SerializeField] float _slideDistance = 220f;
        [SerializeField] float _inDuration = 0.7f;
        [SerializeField] float _holdDuration = 1.8f;
        [SerializeField] float _outDuration = 0.28f;

        Vector2 _restPosition;
        Canvas _overlayCanvas;
        Vector3 _spineRestLocalPosition;
        Vector3 _spineRestLocalScale;
        ParticleSystem[] _seaHeroConfetti;
        Material[] _seaHeroConfettiMaterials;
        Image _numberMatchTitleIcon;

        public override void InitializePrefabRuntime()
        {
            base.InitializePrefabRuntime();
            if (_banner == null) _banner = FindNamed<RectTransform>(panelRoot, "Banner");
            if (_bannerImage == null && _banner != null)
                _bannerImage = _banner.GetComponent<Image>();
            if (_title == null) _title = FindNamed<TMP_Text>(panelRoot, "Title");
            if (_subtitle == null) _subtitle = FindNamed<TMP_Text>(panelRoot, "Subtitle");
            if (_spine == null && worldRoot != null)
                _spine = worldRoot.GetComponentInChildren<SpineLite.SpineSprite>(true);
            if (_bannerImage != null && _bannerImage.sprite == null &&
                !string.IsNullOrEmpty(_spriteResourcePath))
                _bannerImage.sprite = AssetLib.Sprite(_spriteResourcePath);
            if (_spine != null && _spine.Data == null && !string.IsNullOrEmpty(_spineResourceName))
                _spine.Load(_spineResourceName);
            ConfigureNumberMatchComposition();
            ConfigureSeaHeroComposition();
            EnsureOverlaySorting();
            ConfigureRecoveredTextPresentation();
            if (_banner != null) _restPosition = _banner.anchoredPosition;
            if (_spine != null)
            {
                _spineRestLocalPosition = _spine.transform.localPosition;
                _spineRestLocalScale = _spine.transform.localScale;
            }
        }

        void EnsureOverlaySorting()
        {
            if (panelRoot == null) return;
            _overlayCanvas = panelRoot.GetComponent<Canvas>();
            if (_overlayCanvas == null)
                _overlayCanvas = panelRoot.gameObject.AddComponent<Canvas>();
            _overlayCanvas.overrideSorting = true;
            int dialogOrder = App.I != null && App.I.DialogCanvas != null
                ? App.I.DialogCanvas.sortingOrder
                : 2600;
            // Godot's sea-hero Spine node has z_index=-1, so its title and
            // subtitle must render above the skeleton. Other intro banners do
            // not have a world-space layer between their panel and copy.
            _overlayCanvas.sortingOrder = dialogOrder + (IsSeaHeroBanner ? 30 : 10);
            if (IsSeaHeroBanner && _spine != null)
                _spine.SortingOrder = dialogOrder - 10;
        }

        public void Play(int? forcedPercent = null)
        {
            Show();
            int percent = forcedPercent ?? Random.Range(
                Mathf.Min(_percentMin, _percentMax),
                Mathf.Max(_percentMin, _percentMax) + 1);
            if (_title != null)
                _title.text = Text(_titleKey, _titleFallback);
            if (_subtitle != null)
            {
                string copy = Text(_subtitleKey, _subtitleFallback);
                _subtitle.text = IsBonusBanner
                    ? FormatBonusPercent(copy, percent)
                    : _usesPercent ? FormatPercent(copy, percent) : copy;
            }
            FitLocalizedText();
            ConfigureRecoveredTextPresentation();

            if (_spine != null)
            {
                if (_spine.Data == null && !string.IsNullOrEmpty(_spineResourceName))
                    _spine.Load(_spineResourceName);
                if (_spine.Data != null)
                {
                    _spine.SetDefaultMix(0.25f);
                    _spine.SetAnimation("in", false, 0f);
                    _spine.AddAnimation("idle", 0f, true, 0.25f);
                }
            }
            EmitSeaHeroConfetti();
            if (!string.IsNullOrEmpty(_soundKind))
                SoundManager.I?.Play(_soundKind);
            Run(PlayRoutine());
        }

        bool IsBonusBanner => _titleKey == "bonus_congrats_title";
        bool IsSeaHeroBanner => _titleKey == "sea_hero_title";

        void ConfigureRecoveredTextPresentation()
        {
            if (_title == null || _subtitle == null) return;

            _title.fontStyle = FontStyles.Normal;
            _title.fontWeight = FontWeight.Regular;
            _title.richText = true;
            _subtitle.fontStyle = FontStyles.Normal;
            _subtitle.fontWeight = FontWeight.Regular;
            _subtitle.richText = true;
            _subtitle.enableWordWrapping = true;
            _subtitle.overflowMode = TextOverflowModes.Overflow;
            TmpTextStyle.ClearEffects(_title);
            TmpTextStyle.ClearEffects(_subtitle);

            if (IsBonusBanner)
            {
                Color magenta = new Color(0.478f, 0.055f, 0.271f, 1f);
                TmpTextStyle.ApplyOutlineAndShadow(
                    _title, magenta, 15f, magenta, new Vector2(0f, -5f));
                TmpTextStyle.ApplyDisplayTitleFace(_title);
                TmpTextStyle.ApplyShadow(
                    _subtitle,
                    new Color(0f, 0f, 0f, 0.32f),
                    new Vector2(0f, -2f));
                return;
            }

            if (IsSeaHeroBanner)
            {
                Color blue = new Color(0.05f, 0.2f, 0.45f, 1f);
                TmpTextStyle.ApplyOutlineAndShadow(
                    _title, blue, 15f, blue, new Vector2(0f, -5.5f));
                TmpTextStyle.ApplyDisplayTitleFace(_title);
                TmpTextStyle.ApplyOutlineAndShadow(
                    _subtitle, blue, 9f, blue, new Vector2(0f, -4.5f));
                CurveLabelEffect curve =
                    _title.GetComponent<CurveLabelEffect>() ??
                    _title.gameObject.AddComponent<CurveLabelEffect>();
                curve.Amplitude = 13.5f;
                curve.AlignToTangent = true;
                curve.FitToVisibleText = true;
                return;
            }

            Color outline;
            Color titleShadow;
            switch (_titleKey)
            {
                case "number_match_title":
                    outline = new Color(0f, 0.239f, 0.212f, 1f);
                    titleShadow = new Color(0f, 0.361f, 0.231f, 0.55f);
                    break;
                case "shape_puzzle_title":
                    outline = new Color(0.25f, 0.12f, 0.34f, 1f);
                    titleShadow = new Color(0.25f, 0.12f, 0.34f, 0.55f);
                    break;
                case "word_merge_title":
                    outline = new Color(0.5f, 0.22f, 0.02f, 1f);
                    titleShadow = new Color(0.5f, 0.22f, 0.02f, 0.55f);
                    break;
                default:
                    outline = new Color(0.28f, 0.1f, 0.42f, 1f);
                    titleShadow = new Color(0.28f, 0.1f, 0.42f, 0.55f);
                    break;
            }

            TmpTextStyle.ApplyOutlineAndShadow(
                _title,
                outline,
                15f,
                titleShadow,
                new Vector2(0f, -9f),
                10f);
            TmpTextStyle.ApplyOutlineAndShadow(
                _subtitle,
                outline,
                10f,
                new Color(0f, 0f, 0f, 0.4f),
                new Vector2(0f, -6f),
                6f);
            TmpTextStyle.ApplyDisplayTitleFace(_title);
            TmpTextStyle.ApplyFaceDilation(_subtitle, 0.02f);
        }

        void FitLocalizedText()
        {
            if (_title == null || _subtitle == null) return;
            if (IsBonusBanner)
            {
                _title.fontSize = 70f;
                _subtitle.fontSize = 46f;
                return;
            }
            if (IsSeaHeroBanner)
            {
                // Banner.scale=1.125 in the recovered scene also scales both
                // text nodes. These TMP sizes are the measured equivalents at
                // the shared 1080x2400 design resolution.
                FitWidth(_title, 92, 52, 765f);
                FitWidth(_subtitle, 50, 27, 900f);
                return;
            }

            FitWidth(
                _title,
                80,
                40,
                _titleKey == "number_match_title" ? 560f : 700f);
            if (_titleKey == "number_match_title")
            {
                _subtitle.fontSize = 56f;
                return;
            }
            FitWrappedHeight(_subtitle, 56, 36, 700f, 186f);
        }

        static void FitWidth(TMP_Text text, int maximum, int minimum, float width)
        {
            if (text == null || text.font == null) return;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            int size = maximum;
            while (size > minimum &&
                   Preferred(text, text.text, size, 10000f).x > width)
                size -= 2;
            text.fontSize = size;
        }

        static void FitWrappedHeight(
            TMP_Text text,
            int maximum,
            int minimum,
            float width,
            float height)
        {
            if (text == null || text.font == null) return;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            int size = maximum;
            while (size > minimum &&
                   Preferred(text, text.text, size, width).y > height)
                size -= 2;
            text.fontSize = size;
        }

        static Vector2 Preferred(
            TMP_Text text,
            string value,
            float size,
            float width)
        {
            float previous = text.fontSize;
            text.fontSize = size;
            Vector2 preferred = text.GetPreferredValues(value, width, 10000f);
            text.fontSize = previous;
            return preferred;
        }

        static string FormatBonusPercent(string template, int percent)
        {
            template ??= string.Empty;
            string styled =
                $"<color=#FFEF73><size=68>{percent}%</size></color>";
            if (template.Contains("%d"))
                return template.Replace("%d", styled).Replace("%%", "%");
            if (template.Contains("{0}"))
                return string.Format(template, styled);
            return template;
        }

        void ConfigureNumberMatchComposition()
        {
            if (_titleKey != "number_match_title" || _banner == null ||
                _title == null)
                return;

            const string iconName = "NumberMatchTitleIcon";
            Transform existing = _banner.Find(iconName);
            Image icon;
            if (existing != null)
            {
                icon = existing.GetComponent<Image>();
            }
            else
            {
                var iconObject = new GameObject(
                    iconName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                iconObject.transform.SetParent(_banner, false);
                icon = iconObject.GetComponent<Image>();
            }

            if (icon != null)
            {
                _numberMatchTitleIcon = icon;
                icon.sprite = AssetLib.Sprite("Art/Sprites/Bubble/number_match_icon");
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(100f, 100f);
                iconRect.anchoredPosition = new Vector2(-145f, -405f);
                iconRect.SetSiblingIndex(Mathf.Max(0, _title.transform.GetSiblingIndex()));
            }

            RectTransform titleRect = _title.rectTransform;
            titleRect.anchoredPosition = new Vector2(50f, -405f);
            titleRect.sizeDelta = new Vector2(600f, 110f);
        }

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            Transform authoredWorldRoot,
            CanvasGroup authoredCanvasGroup,
            RectTransform banner,
            Image bannerImage,
            TMP_Text title,
            TMP_Text subtitle,
            SpineLite.SpineSprite spine,
            string spriteResourcePath,
            string spineResourceName,
            string titleKey,
            string titleFallback,
            string subtitleKey,
            string subtitleFallback,
            bool usesPercent,
            int percentMin,
            int percentMax,
            float holdDuration,
            string soundKind)
        {
            base.ConfigurePrefabAuthoring(authoredPanelRoot, authoredWorldRoot, authoredCanvasGroup);
            _banner = banner;
            _bannerImage = bannerImage;
            _title = title;
            _subtitle = subtitle;
            _spine = spine;
            _spriteResourcePath = spriteResourcePath;
            _spineResourceName = spineResourceName;
            _titleKey = titleKey;
            _titleFallback = titleFallback;
            _subtitleKey = subtitleKey;
            _subtitleFallback = subtitleFallback;
            _usesPercent = usesPercent;
            _percentMin = percentMin;
            _percentMax = percentMax;
            _holdDuration = holdDuration;
            _soundKind = soundKind;
            _restPosition = banner != null ? banner.anchoredPosition : Vector2.zero;
        }

        IEnumerator PlayRoutine()
        {
            if (_banner == null)
            {
                yield return new WaitForSecondsRealtime(_holdDuration);
                Hide();
                yield break;
            }

            Vector2 start = _restPosition + Vector2.up * _slideDistance;
            SetBannerPosition(start);
            SetBannerAlpha(0f);
            yield return Animate(_inDuration, t =>
            {
                float eased = EaseOutBack(t);
                SetBannerPosition(Vector2.LerpUnclamped(start, _restPosition, eased));
                SetBannerAlpha(t);
            });
            SetBannerPosition(_restPosition);
            SetBannerAlpha(1f);

            yield return new WaitForSecondsRealtime(_holdDuration);
            yield return Animate(_outDuration, t =>
            {
                float eased = t * t;
                SetBannerPosition(Vector2.Lerp(_restPosition, start, eased));
                SetBannerAlpha(1f - t);
            });
            Hide();
        }

        IEnumerator Animate(float duration, System.Action<float> step)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration)));
                yield return null;
            }
            step(1f);
        }

        void SetBannerAlpha(float alpha)
        {
            if (_bannerImage != null)
            {
                var color = _bannerImage.color;
                color.a = alpha;
                _bannerImage.color = color;
            }
            if (_title != null) _title.alpha = alpha;
            if (_subtitle != null) _subtitle.alpha = alpha;
            if (_numberMatchTitleIcon != null)
            {
                Color color = _numberMatchTitleIcon.color;
                color.a = alpha;
                _numberMatchTitleIcon.color = color;
            }
            if (_spine != null)
            {
                Color tint = _spine.Tint;
                tint.a = alpha;
                _spine.Tint = tint;
            }
        }

        void SetBannerPosition(Vector2 position)
        {
            if (_banner != null)
                _banner.anchoredPosition = position;
            if (!IsSeaHeroBanner || _spine == null)
                return;
            float deltaY = position.y - _restPosition.y;
            _spine.transform.localPosition =
                _spineRestLocalPosition + Vector3.up * deltaY;
        }

        void ConfigureSeaHeroComposition()
        {
            if (!IsSeaHeroBanner) return;

            // sea_hero_banner.tscn uses Banner only as a transformed container.
            // The visible ribbon, plants and starfish all come from
            // starfish_pop. Showing gp_feedback_sea_hero at the same time
            // produces the doubled, vertically stretched banner seen in the
            // first Unity port.
            if (_bannerImage != null)
                _bannerImage.enabled = false;

            if (_title != null)
                _title.rectTransform.anchoredPosition = new Vector2(0f, -402f);
            if (_subtitle != null)
                _subtitle.rectTransform.anchoredPosition = new Vector2(0f, -641f);

            if (_spine != null)
            {
                _spine.transform.localPosition = new Vector3(0f, 387f, 0f);
                _spine.transform.localScale = Vector3.one * 1.125f;
                _spine.SortingOrder = 2590;
            }
            BuildSeaHeroConfetti();
        }

        void BuildSeaHeroConfetti()
        {
            if (!IsSeaHeroBanner || worldRoot == null ||
                (_seaHeroConfetti != null && _seaHeroConfetti.Length == 4))
                return;

            Texture2D texture = AssetLib.Texture("Art/Sprites/Common/et_ribbon_007");
            Shader shader = Shader.Find("BubblePics/ParticleTintUv");
            if (texture == null || shader == null) return;

            _seaHeroConfetti = new ParticleSystem[4];
            _seaHeroConfettiMaterials = new Material[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("SeaHeroConfetti" + i);
                go.transform.SetParent(worldRoot, false);
                var particles = go.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _seaHeroConfetti[i] = particles;

                var material = new Material(shader) { mainTexture = texture };
                // Godot's four AtlasTexture regions are top-left to
                // bottom-right; Unity texture UVs start at bottom-left.
                float x = (i & 1) * 0.5f;
                float y = i < 2 ? 0.5f : 0f;
                material.SetVector("_UvRect", new Vector4(x, y, 0.5f, 0.5f));
                material.SetColor("_TintColor", Color.white);
                _seaHeroConfettiMaterials[i] = material;

                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sortingOrder = 2640;
                renderer.sharedMaterial = material;

                var main = particles.main;
                main.loop = false;
                main.playOnAwake = false;
                main.duration = 3.5f;
                main.startLifetime = 3.5f;
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(32f, 128f);
                main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
                main.maxParticles = 8;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                var emission = particles.emission;
                emission.enabled = false;
                var shape = particles.shape;
                shape.enabled = false;

                var force = particles.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.x = 0f;
                force.y = -520f;
                force.z = 0f;

                var rotation = particles.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(
                    -240f * Mathf.Deg2Rad,
                    240f * Mathf.Deg2Rad);

                var damping = particles.limitVelocityOverLifetime;
                damping.enabled = true;
                damping.space = ParticleSystemSimulationSpace.World;
                damping.limit = 10000f;
                damping.dampen = 0f;
                damping.drag = new ParticleSystem.MinMaxCurve(0.34f, 0.38f);
                damping.multiplyDragByParticleSize = false;
                damping.multiplyDragByParticleVelocity = false;

                var color = particles.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(Color.white, 1f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(1f, 0.08f),
                        new GradientAlphaKey(1f, 0.8f),
                        new GradientAlphaKey(0f, 1f),
                    });
                color.color = gradient;
            }
        }

        void EmitSeaHeroConfetti()
        {
            if (!IsSeaHeroBanner || _seaHeroConfetti == null || App.I == null)
                return;

            float viewWidth = DeviceLayout.ViewWidth > 0f
                ? DeviceLayout.ViewWidth
                : App.DesignW;
            float viewHeight = DeviceLayout.ViewHeight > 0f
                ? DeviceLayout.ViewHeight
                : App.DesignH;
            float leftX = viewWidth * 0.06f;
            float rightX = viewWidth * 0.94f;
            float centerY = viewHeight * 0.4f + 100f;
            float bandHalf = viewHeight * 0.18f;

            for (int i = 0; i < _seaHeroConfetti.Length; i++)
            {
                ParticleSystem particles = _seaHeroConfetti[i];
                if (particles == null) continue;
                particles.Clear(true);
                bool flyRight = i < 2;
                float originX = flyRight ? leftX : rightX;
                float baseAngle = flyRight ? 63.435f : 116.565f;
                for (int particleIndex = 0; particleIndex < 8; particleIndex++)
                {
                    float designY = centerY + Random.Range(-bandHalf, bandHalf);
                    Vector3 position = App.DesignToWorld(new Vector2(originX, designY));
                    float angle = (baseAngle + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
                    float speed = Random.Range(800f, 1150f);
                    var emit = new ParticleSystem.EmitParams
                    {
                        position = position,
                        velocity = new Vector3(
                            Mathf.Cos(angle) * speed,
                            Mathf.Sin(angle) * speed,
                            0f),
                        startLifetime = 3.5f,
                        startSize = Random.Range(32f, 128f),
                        rotation3D = new Vector3(0f, 0f, Random.Range(-180f, 180f)),
                        applyShapeToPosition = false,
                    };
                    particles.Emit(emit, 1);
                }
                particles.Play(false);
            }
        }

        public override void Hide()
        {
            if (_seaHeroConfetti != null)
            {
                foreach (ParticleSystem particles in _seaHeroConfetti)
                    if (particles != null)
                        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            base.Hide();
        }

        void OnDestroy()
        {
            if (_seaHeroConfettiMaterials == null) return;
            foreach (Material material in _seaHeroConfettiMaterials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
