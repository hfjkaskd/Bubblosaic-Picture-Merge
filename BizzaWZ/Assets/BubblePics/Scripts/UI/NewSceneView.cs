using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of new_scene_page.gd claim presentation.</summary>
    public sealed class NewSceneView : MetaFlowView
    {
        [SerializeField] Image _scrim;
        [SerializeField] CanvasGroup _content;
        [SerializeField] TMP_Text _title;
        [SerializeField] RectTransform _picture;
        [SerializeField] Image _chapterImage;
        [SerializeField] Image _frame;
        [SerializeField] Button _claim;
        [SerializeField] RectTransform _claimRect;
        [SerializeField] TMP_Text _claimText;
        TMP_Text _claimBackText;
        [SerializeField] Image _flash;
        [SerializeField] string _framePath = "Art/Sprites/Home/scene_pic_frame";

        Action _claimed;
        bool _canClaim;
        Vector2 _pictureRest;
        Sprite _coveredChapterSprite;

        public Vector4 FrameBorder => _frame != null && _frame.sprite != null
            ? _frame.sprite.border
            : Vector4.zero;
        public Vector4 ClaimBorder => _claim != null && _claim.image != null &&
                                      _claim.image.sprite != null
            ? _claim.image.sprite.border
            : Vector4.zero;
        public string ClaimTextureName => _claim != null && _claim.image != null &&
                                          _claim.image.sprite != null &&
                                          _claim.image.sprite.texture != null
            ? _claim.image.sprite.texture.name
            : string.Empty;
        public bool ClaimHasLayeredLabel => _claimBackText != null;
        public float ChapterImageAspect => _chapterImage != null &&
                                           _chapterImage.sprite != null
            ? _chapterImage.sprite.rect.width /
              Mathf.Max(1f, _chapterImage.sprite.rect.height)
            : 0f;
        public float ChapterRectAspect => _chapterImage != null
            ? _chapterImage.rectTransform.rect.width /
              Mathf.Max(1f, _chapterImage.rectTransform.rect.height)
            : 0f;

        public override void InitializePrefabRuntime()
        {
            base.InitializePrefabRuntime();
            _scrim ??= FindNamed<Image>(panelRoot, "Scrim");
            _content ??= FindNamed<CanvasGroup>(panelRoot, "Content");
            _title ??= FindNamed<TMP_Text>(panelRoot, "Title");
            _picture ??= FindNamed<RectTransform>(panelRoot, "Picture");
            _chapterImage ??= FindNamed<Image>(panelRoot, "Image");
            _frame ??= FindNamed<Image>(panelRoot, "Frame");
            _claim ??= FindNamed<Button>(panelRoot, "ClaimBtn");
            if (_claimText == null && _claim != null)
                _claimText = FindNamed<TMP_Text>(_claim.transform, "Label");
            if (_claim != null && _claim.image != null)
            {
                _claimBackText = CommonButton.StyleExisting(
                    _claim,
                    CommonButton.Variant.Orange,
                    _claimText);
            }
            if (_claimRect == null && _claim != null)
                _claimRect = _claim.transform as RectTransform;
            if (_title != null)
            {
                TmpTextStyle.ClearEffects(_title);
                TmpTextStyle.ApplyOutline(
                    _title,
                    new Color(0.960784f, 0.45098f, 0.031373f, 1f),
                    18f);
                TmpTextStyle.ApplyDisplayTitleFace(_title);
            }
            _flash ??= FindNamed<Image>(panelRoot, "Flash");
            if (_frame != null)
            {
                _frame.sprite = AssetLib.Sprite9Design(
                    _framePath,
                    72f,
                    72f,
                    72f,
                    72f);
                _frame.type = Image.Type.Sliced;
                _frame.preserveAspect = false;
            }
            ConfigureChapterImageRect();
            if (_picture != null) _pictureRest = _picture.anchoredPosition;
            if (_claim != null)
            {
                _claim.onClick.RemoveListener(OnClaim);
                _claim.onClick.AddListener(OnClaim);
            }
        }

        public void Play(Sprite chapterImage, Action claimed = null)
        {
            _claimed = claimed;
            _canClaim = false;
            ApplyCoveredChapterImage(chapterImage);
            if (_title != null) _title.text = Text("new_scene", "NEW SCENE");
            if (_claimText != null) _claimText.text = Text("claim", "CLAIM");
            if (_claimBackText != null) _claimBackText.text = _claimText.text;
            Show();
            Run(IntroRoutine());
        }

        void ConfigureChapterImageRect()
        {
            if (_chapterImage == null) return;
            RectTransform rect = _chapterImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24f, 24f);
            rect.offsetMax = new Vector2(-24f, -24f);
            rect.anchoredPosition = Vector2.zero;
            _chapterImage.type = Image.Type.Simple;
            _chapterImage.preserveAspect = false;
        }

        /// <summary>
        /// Godot TextureRect stretch mode 6 is KEEP_ASPECT_COVERED. UGUI's
        /// Image only exposes contain-style preserveAspect, so create a centered
        /// crop whose aspect exactly matches the recovered 24 px inset rect.
        /// The Image can then fill that rect without deforming the source.
        /// </summary>
        void ApplyCoveredChapterImage(Sprite source)
        {
            ReleaseCoveredChapterSprite();
            if (_chapterImage == null || source == null)
            {
                if (_chapterImage != null) _chapterImage.sprite = source;
                return;
            }

            ConfigureChapterImageRect();
            float targetWidth = _picture != null
                ? Mathf.Max(1f, _picture.rect.width - 48f)
                : 684f;
            float targetHeight = _picture != null
                ? Mathf.Max(1f, _picture.rect.height - 48f)
                : 1217f;
            float targetAspect = targetWidth / targetHeight;
            Rect crop = source.rect;
            float sourceAspect = crop.width / Mathf.Max(1f, crop.height);
            if (sourceAspect > targetAspect)
            {
                float width = crop.height * targetAspect;
                crop.x += (crop.width - width) * 0.5f;
                crop.width = width;
            }
            else if (sourceAspect < targetAspect)
            {
                float height = crop.width / targetAspect;
                crop.y += (crop.height - height) * 0.5f;
                crop.height = height;
            }

            _coveredChapterSprite = Sprite.Create(
                source.texture,
                crop,
                new Vector2(0.5f, 0.5f),
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            _coveredChapterSprite.name = source.name + "_Covered";
            _chapterImage.sprite = _coveredChapterSprite;
        }

        void ReleaseCoveredChapterSprite()
        {
            if (_coveredChapterSprite == null) return;
            if (Application.isPlaying)
                Destroy(_coveredChapterSprite);
            else
                DestroyImmediate(_coveredChapterSprite);
            _coveredChapterSprite = null;
        }

        void OnDestroy()
        {
            ReleaseCoveredChapterSprite();
        }

        public override void HandleBackRequest()
        {
            if (_canClaim) OnClaim();
        }

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            CanvasGroup authoredCanvasGroup,
            Image scrim,
            CanvasGroup content,
            TMP_Text title,
            RectTransform picture,
            Image chapterImage,
            Image frame,
            Button claim,
            RectTransform claimRect,
            Image flash)
        {
            base.ConfigurePrefabAuthoring(authoredPanelRoot, null, authoredCanvasGroup);
            _scrim = scrim;
            _content = content;
            _title = title;
            _picture = picture;
            _chapterImage = chapterImage;
            _frame = frame;
            _claim = claim;
            _claimRect = claimRect;
            _flash = flash;
            _pictureRest = picture != null ? picture.anchoredPosition : Vector2.zero;
        }

        IEnumerator IntroRoutine()
        {
            SetImageAlpha(_scrim, 0f);
            SetImageAlpha(_flash, 0f);
            if (_content != null) _content.alpha = 0f;
            if (_picture != null)
            {
                _picture.anchoredPosition = _pictureRest;
                _picture.localScale = Vector3.one * 0.05f;
            }
            if (_claimRect != null) _claimRect.localScale = Vector3.zero;
            SoundManager.I?.Play("outerloop_chapter_bg_unlock");
            yield return Animate(0.55f, t =>
            {
                SetImageAlpha(_scrim, 0.88f * t);
                if (_content != null) _content.alpha = t;
                if (_picture != null)
                    _picture.localScale = Vector3.one * Mathf.Lerp(0.05f, 1f, EaseOutCubic(t));
            });
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Animate(0.28f, t =>
            {
                if (_claimRect != null)
                    _claimRect.localScale = Vector3.one * EaseOutBack(t);
            });
            _canClaim = true;
        }

        void OnClaim()
        {
            if (!_canClaim) return;
            _canClaim = false;
            SoundManager.I?.Play("outerloop_chapter_bg_claim");
            Run(OutroRoutine());
        }

        IEnumerator OutroRoutine()
        {
            yield return Animate(0.12f, t =>
            {
                if (_title != null) _title.alpha = 1f - t;
                if (_claimRect != null) _claimRect.localScale = Vector3.one * (1f - t);
            });
            yield return Animate(0.5f, t =>
            {
                if (_picture == null) return;
                float zoom = Mathf.Max(
                    App.DesignW / Mathf.Max(_picture.rect.width, 1f),
                    App.DesignH / Mathf.Max(_picture.rect.height, 1f)) * 1.02f;
                _picture.localScale = Vector3.one * Mathf.Lerp(1f, zoom, EaseInCubic(t));
                _picture.anchoredPosition = Vector2.Lerp(_pictureRest, Vector2.zero, EaseInCubic(t));
            });
            yield return Animate(0.12f, t => SetImageAlpha(_flash, t));
            _claimed?.Invoke();
            yield return Animate(0.3f, t => SetImageAlpha(_flash, 1f - t));
            Hide();
        }

        IEnumerator Animate(float duration, Action<float> step)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(elapsed / Mathf.Max(duration, 0.001f)));
                yield return null;
            }
            step(1f);
        }

        static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null) return;
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        static float EaseInCubic(float t) => t * t * t;

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
