using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of lucky_break_round_intro.gd.</summary>
    public class LuckyBreakRoundIntroViewBase : MetaFlowView
    {
        [SerializeField] RectTransform _icon;
        [SerializeField] Image _iconImage;
        [SerializeField] string _spritePath = "Art/Sprites/Bubble/unlimit_lucky_infinity";

        RectTransform _target;
        Action _arrived;
        Vector2 _baseSize;

        public override void InitializePrefabRuntime()
        {
            base.InitializePrefabRuntime();
            _icon ??= FindNamed<RectTransform>(panelRoot, "Icon");
            _iconImage ??= FindNamed<Image>(panelRoot, "Icon");
            if (_iconImage != null && _iconImage.sprite == null)
                _iconImage.sprite = AssetLib.Sprite(_spritePath);
            if (_icon != null) _baseSize = _icon.sizeDelta;
        }

        public void Play(RectTransform target, Action arrived = null)
        {
            _target = target;
            _arrived = arrived;
            Show();
            Run(PlayRoutine());
        }

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            CanvasGroup authoredCanvasGroup,
            RectTransform icon,
            Image iconImage)
        {
            base.ConfigurePrefabAuthoring(authoredPanelRoot, null, authoredCanvasGroup);
            _icon = icon;
            _iconImage = iconImage;
            _baseSize = icon != null ? icon.sizeDelta : new Vector2(880f, 496f);
        }

        IEnumerator PlayRoutine()
        {
            if (_icon == null)
            {
                _arrived?.Invoke();
                Hide();
                yield break;
            }

            _icon.anchorMin = _icon.anchorMax = new Vector2(0.5f, 0.5f);
            _icon.anchoredPosition = Vector2.zero;
            _icon.sizeDelta = _baseSize;
            _icon.localScale = Vector3.one * 0.5f;
            SetAlpha(1f);
            yield return Animate(0.22f, t =>
                _icon.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.12f, EaseOutBack(t)));
            yield return Animate(0.12f, t =>
                _icon.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, t * t));
            yield return new WaitForSecondsRealtime(0.5f);

            if (_target == null || panelRoot == null)
            {
                _arrived?.Invoke();
                Hide();
                yield break;
            }

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panelRoot, _target);
            Vector2 start = _icon.anchoredPosition;
            Vector2 mid = start + Vector2.down * 90f;
            Vector2 end = bounds.center;
            Vector2 startSize = _baseSize;
            Vector2 midSize = _baseSize * 0.7f;
            Vector2 endSize = new Vector2(
                Mathf.Max(bounds.size.x, 64f),
                Mathf.Max(bounds.size.y, 36f));
            yield return Animate(0.24f, t =>
            {
                _icon.anchoredPosition = Vector2.Lerp(start, mid, Mathf.Sin(t * Mathf.PI * 0.5f));
                _icon.sizeDelta = Vector2.Lerp(startSize, midSize, t);
            });
            yield return Animate(0.44f, t =>
            {
                _icon.anchoredPosition = Vector2.Lerp(mid, end, t * t);
                _icon.sizeDelta = Vector2.Lerp(midSize, endSize, t * t);
            });
            _arrived?.Invoke();
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

        void SetAlpha(float alpha)
        {
            if (_iconImage == null) return;
            var color = _iconImage.color;
            color.a = alpha;
            _iconImage.color = color;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }

    /// <summary>Port of unlimit_lucky_page.gd.</summary>
    public class UnlimitedLuckyViewBase : MetaFlowView
    {
        [SerializeField] Image _overlay;
        [SerializeField] CanvasGroup _titleGroup;
        [SerializeField] RectTransform _iconGroup;
        [SerializeField] RectTransform _icon;
        [SerializeField] Image _iconImage;
        [SerializeField] CanvasGroup _bottomGroup;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _description;
        [SerializeField] Button _confirm;
        [SerializeField] TMP_Text _confirmText;
        TMP_Text _confirmBackText;
        [SerializeField] SpineLite.SpineSprite _spineLoop;
        [SerializeField] SpineLite.SpineSprite _spineEntry;
        [SerializeField] string _spritePath = "Art/Sprites/Bubble/unlimit_lucky_infinity";

        RectTransform _badge;
        Action _badgeArrived;
        bool _canConfirm;
        Vector2 _iconRest;

        public float BottomAlpha => _bottomGroup != null ? _bottomGroup.alpha : 0f;
        public Sprite IconSprite => _iconImage != null ? _iconImage.sprite : null;
        public bool ConfirmVisible => _confirm != null && _confirm.gameObject.activeInHierarchy;
        public Vector4 ConfirmBorder => _confirm != null && _confirm.image != null &&
                                        _confirm.image.sprite != null
            ? _confirm.image.sprite.border
            : Vector4.zero;
        public bool ConfirmHasLayeredLabel => _confirmBackText != null;

        public override void InitializePrefabRuntime()
        {
            base.InitializePrefabRuntime();
            _overlay ??= FindNamed<Image>(panelRoot, "Overlay");
            _titleGroup ??= FindNamed<CanvasGroup>(panelRoot, "TitleGroup");
            _iconGroup ??= FindNamed<RectTransform>(panelRoot, "IconGroup");
            _icon ??= FindNamed<RectTransform>(panelRoot, "InfinityIcon");
            _iconImage ??= FindNamed<Image>(panelRoot, "InfinityIcon");
            _bottomGroup ??= FindNamed<CanvasGroup>(panelRoot, "BottomGroup");
            _title ??= FindNamed<TMP_Text>(panelRoot, "Title");
            _description ??= FindNamed<TMP_Text>(panelRoot, "DescLabel");
            _confirm ??= FindNamed<Button>(panelRoot, "ConfirmBtn");
            if (_confirmText == null && _confirm != null)
                _confirmText = FindNamed<TMP_Text>(_confirm.transform, "Label");
            if (_confirm != null && _confirm.image != null)
            {
                _confirmBackText = CommonButton.StyleExisting(
                    _confirm,
                    CommonButton.Variant.Green,
                    _confirmText);
            }
            if (_title != null)
            {
                TmpTextStyle.ClearEffects(_title);
                TmpTextStyle.ApplyOutline(
                    _title,
                    new Color(0.42f, 0.18f, 0.02f, 1f),
                    24f);
                TmpTextStyle.ApplyDisplayTitleFace(_title);
            }
            if (_iconImage != null && _iconImage.sprite == null)
                _iconImage.sprite = AssetLib.Sprite(_spritePath);
            if (_icon != null) _iconRest = _icon.anchoredPosition;
            if (worldRoot != null)
            {
                var spines = worldRoot.GetComponentsInChildren<SpineLite.SpineSprite>(true);
                if (_spineLoop == null && spines.Length > 0) _spineLoop = spines[0];
                if (_spineEntry == null && spines.Length > 1) _spineEntry = spines[1];
            }
            LoadSpine(_spineLoop);
            LoadSpine(_spineEntry);
            if (_confirm != null)
            {
                _confirm.onClick.RemoveListener(OnConfirm);
                _confirm.onClick.AddListener(OnConfirm);
            }
        }

        public void Play(RectTransform badge, Action badgeArrived = null)
        {
            _badge = badge;
            _badgeArrived = badgeArrived;
            _canConfirm = false;
            if (_title != null)
                _title.text = Text("lucky_puzzle_title", "Lucky Puzzle!");
            if (_description != null)
                _description.text = Text(
                    "lucky_puzzle_tips",
                    "You've unlocked unlimited moves for the next 3 levels.");
            if (_confirmText != null)
                _confirmText.text = Text("got_it", "GOT IT");
            if (_confirmBackText != null)
                _confirmBackText.text = _confirmText.text;
            Show();
            Run(IntroRoutine());
        }

        public override void HandleBackRequest()
        {
            if (_canConfirm) OnConfirm();
        }

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            Transform authoredWorldRoot,
            CanvasGroup authoredCanvasGroup,
            Image overlay,
            CanvasGroup titleGroup,
            RectTransform iconGroup,
            RectTransform icon,
            Image iconImage,
            CanvasGroup bottomGroup,
            TMP_Text title,
            TMP_Text description,
            Button confirm,
            SpineLite.SpineSprite spineLoop,
            SpineLite.SpineSprite spineEntry)
        {
            base.ConfigurePrefabAuthoring(authoredPanelRoot, authoredWorldRoot, authoredCanvasGroup);
            _overlay = overlay;
            _titleGroup = titleGroup;
            _iconGroup = iconGroup;
            _icon = icon;
            _iconImage = iconImage;
            _bottomGroup = bottomGroup;
            _title = title;
            _description = description;
            _confirm = confirm;
            _spineLoop = spineLoop;
            _spineEntry = spineEntry;
            _iconRest = icon != null ? icon.anchoredPosition : Vector2.zero;
        }

        IEnumerator IntroRoutine()
        {
            SetOverlayAlpha(0f);
            if (_titleGroup != null) _titleGroup.alpha = 0f;
            if (_bottomGroup != null) _bottomGroup.alpha = 0f;
            if (_icon != null)
            {
                _icon.anchoredPosition = _iconRest + Vector2.up * 320f;
                _icon.localScale = Vector3.one * 2.2f;
            }
            SetIconAlpha(0f);
            yield return Animate(0.25f, t => SetOverlayAlpha(0.8f * t));
            yield return Animate(0.25f, t =>
            {
                if (_titleGroup != null) _titleGroup.alpha = t;
            });

            if (_spineLoop != null)
            {
                _spineLoop.Tint = new Color(1f, 1f, 1f, 0.6f);
                _spineLoop.SetAnimation("hbdk_2", false, 0f);
            }
            Vector2 start = _iconRest + Vector2.up * 320f;
            yield return Animate(0.3f, t =>
            {
                if (_icon == null) return;
                float eased = t * t;
                _icon.anchoredPosition = Vector2.Lerp(start, _iconRest, eased);
                _icon.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, eased);
                SetIconAlpha(t);
            });
            SoundManager.I?.Play("lucky_break_unlock");
            if (_spineEntry != null)
            {
                _spineEntry.gameObject.SetActive(true);
                _spineEntry.SetAnimation("hbdk_1", false, 0f);
            }
            // Godot reveals the bottom group as soon as the 0.3s slam ends;
            // the rebound runs concurrently.  Reveal it before the rebound
            // here as well so slow devices cannot leave the action offscreen.
            yield return Animate(0.25f, t =>
            {
                if (_bottomGroup != null) _bottomGroup.alpha = t;
            });
            yield return Animate(0.1f, t => SetIconScale(Mathf.Lerp(1f, 1.15f, t)));
            yield return Animate(0.18f, t => SetIconScale(Mathf.Lerp(1.15f, 1f, t)));
            _canConfirm = true;
        }

        void OnConfirm()
        {
            if (!_canConfirm) return;
            _canConfirm = false;
            Run(FlyRoutine());
        }

        IEnumerator FlyRoutine()
        {
            if (_badge != null && _icon != null && panelRoot != null)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panelRoot, _badge);
                Vector2 start = _icon.anchoredPosition;
                Vector2 end = bounds.center;
                Vector2 control = (start + end) * 0.5f + Vector2.up * 200f;
                Vector2 fromSize = _icon.sizeDelta;
                Vector2 toSize = bounds.size;
                yield return Animate(0.62f, t =>
                {
                    float q = 1f - t;
                    _icon.anchoredPosition = q * q * start + 2f * q * t * control + t * t * end;
                    _icon.sizeDelta = Vector2.Lerp(fromSize, toSize, t);
                    if (_titleGroup != null) _titleGroup.alpha = 1f - Mathf.Clamp01(t / 0.45f);
                    if (_bottomGroup != null) _bottomGroup.alpha = 1f - Mathf.Clamp01(t / 0.45f);
                    SetOverlayAlpha(0.8f * (1f - t));
                });
                _badgeArrived?.Invoke();
            }
            Hide();
        }

        static void LoadSpine(SpineLite.SpineSprite spine)
        {
            if (spine != null && spine.Data == null) spine.Load("hbdk");
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

        void SetIconScale(float scale)
        {
            if (_icon != null) _icon.localScale = Vector3.one * scale;
        }

        void SetIconAlpha(float alpha)
        {
            if (_iconImage == null) return;
            var color = _iconImage.color;
            color.a = alpha;
            _iconImage.color = color;
        }

        void SetOverlayAlpha(float alpha)
        {
            if (_overlay == null) return;
            var color = _overlay.color;
            color.a = alpha;
            _overlay.color = color;
        }
    }

    /// <summary>Serialized 130x130 lucky-break badge from lucky_break_badge.tscn.</summary>
    public class LuckyBreakBadgeViewBase : MetaFlowView
    {
        [SerializeField] Image _background;
        [SerializeField] TMP_Text _symbol;
        [SerializeField] string _spritePath = "Art/Sprites/Bubble/gp_Infinite_game_tag_bg";

        public RectTransform BadgeRect => panelRoot;

        public override void InitializePrefabRuntime()
        {
            base.InitializePrefabRuntime();
            _background ??= FindNamed<Image>(panelRoot, "Bg");
            _symbol ??= FindNamed<TMP_Text>(panelRoot, "Symbol");
            if (_background != null && _background.sprite == null)
                _background.sprite = AssetLib.Sprite(_spritePath);
            if (_symbol != null)
            {
                _symbol.text = "∞";
                TmpTextStyle.ClearEffects(_symbol);
                TmpTextStyle.ApplyOutline(
                    _symbol,
                    new Color(0f, 0f, 0f, 0.4f),
                    3f);
            }
        }

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            CanvasGroup authoredCanvasGroup,
            Image background,
            TMP_Text symbol)
        {
            base.ConfigurePrefabAuthoring(authoredPanelRoot, null, authoredCanvasGroup);
            _background = background;
            _symbol = symbol;
        }
    }
}
