using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of daily_first_step_bonus_panel.gd (+3 moves fly-in).</summary>
    public sealed class DailyFirstStepBonusView : MetaFlowView
    {
        [SerializeField] RectTransform _content;
        [SerializeField] RectTransform _plusStack;
        [SerializeField] Image _plusImage;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _moves;
        [SerializeField] TMP_Text _bottom;
        [SerializeField] SpineLite.SpineSprite _spineLoop;
        [SerializeField] SpineLite.SpineSprite _spineEntry;
        [SerializeField] string _plusSpritePath = "Art/Sprites/Bubble/daily_bonus_plus_three";

        RectTransform _target;
        Action<int> _grantSteps;
        int _amount;
        bool _previewOnly;
        Vector2 _plusRest;

        public override void InitializePrefabRuntime()
        {
            base.InitializePrefabRuntime();
            _content ??= FindNamed<RectTransform>(panelRoot, "Content");
            _plusStack ??= FindNamed<RectTransform>(panelRoot, "PlusStack");
            _plusImage ??= FindNamed<Image>(panelRoot, "PlusImage");
            _title ??= FindNamed<TMP_Text>(panelRoot, "TitleLabel");
            _moves ??= FindNamed<TMP_Text>(panelRoot, "MovesLabel");
            _bottom ??= FindNamed<TMP_Text>(panelRoot, "BottomLabel");
            if (worldRoot != null)
            {
                var spines = worldRoot.GetComponentsInChildren<SpineLite.SpineSprite>(true);
                if (_spineLoop == null && spines.Length > 0) _spineLoop = spines[0];
                if (_spineEntry == null && spines.Length > 1) _spineEntry = spines[1];
            }
            if (_plusImage != null && _plusImage.sprite == null)
                _plusImage.sprite = AssetLib.Sprite(_plusSpritePath);
            ConfigureRecoveredTextPresentation();
            LoadSpine(_spineLoop);
            LoadSpine(_spineEntry);
            if (_plusStack != null) _plusRest = _plusStack.anchoredPosition;
        }

        void ConfigureRecoveredTextPresentation()
        {
            if (_title != null)
            {
                TmpTextStyle.ClearEffects(_title);
                TmpTextStyle.ApplyOutlineAndShadow(
                    _title,
                    new Color(0.28f, 0.09f, 0f, 1f),
                    32f,
                    new Color(0f, 0f, 0f, 0.55f),
                    new Vector2(0f, -14f),
                    4f);
                TmpTextStyle.ApplyDisplayTitleFace(_title);
            }
            if (_moves != null)
            {
                TmpTextStyle.ClearEffects(_moves);
                TmpTextStyle.ApplyOutline(
                    _moves,
                    new Color(0.39215687f, 0.24313726f, 0.13333334f, 0.8f),
                    3f);
            }
            if (_bottom != null)
                TmpTextStyle.ClearEffects(_bottom);
        }

        public void Play(
            int amount,
            RectTransform target,
            Action<int> grantSteps,
            bool previewOnly = false)
        {
            _amount = Mathf.Max(0, amount);
            _target = target;
            _grantSteps = grantSteps;
            _previewOnly = previewOnly;
            if (_title != null) _title.text = Text("daily_first_step_bonus_bonus", $"+{_amount}");
            if (_moves != null) _moves.text = Text("str_moves", "MOVES");
            if (_bottom != null)
                _bottom.text = Text("daily_first_step_bonus_title", "Daily first-game bonus!");
            Show();
            Run(PlayRoutine());
        }

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            Transform authoredWorldRoot,
            CanvasGroup authoredCanvasGroup,
            RectTransform content,
            RectTransform plusStack,
            Image plusImage,
            TMP_Text title,
            TMP_Text moves,
            TMP_Text bottom,
            SpineLite.SpineSprite spineLoop,
            SpineLite.SpineSprite spineEntry)
        {
            base.ConfigurePrefabAuthoring(authoredPanelRoot, authoredWorldRoot, authoredCanvasGroup);
            _content = content;
            _plusStack = plusStack;
            _plusImage = plusImage;
            _title = title;
            _moves = moves;
            _bottom = bottom;
            _spineLoop = spineLoop;
            _spineEntry = spineEntry;
            _plusRest = plusStack != null ? plusStack.anchoredPosition : Vector2.zero;
        }

        IEnumerator PlayRoutine()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (_plusStack != null)
            {
                _plusStack.anchoredPosition = _plusRest;
                _plusStack.localScale = Vector3.one * 2.2f;
                SetPlusAlpha(0f);
            }
            if (_spineLoop != null)
            {
                _spineLoop.Tint = new Color(1f, 1f, 1f, 0f);
                _spineLoop.SetAnimation("hbdk_2", false, 0f);
            }

            yield return Animate(0.25f, t =>
            {
                if (canvasGroup != null) canvasGroup.alpha = t;
                if (_spineLoop != null)
                    _spineLoop.Tint = new Color(1f, 1f, 1f, Mathf.Lerp(0f, 0.7f, t));
            });
            yield return Animate(0.3f, t =>
            {
                if (_plusStack != null)
                    _plusStack.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, t * t);
                SetPlusAlpha(t);
            });

            if (_spineEntry != null)
            {
                _spineEntry.gameObject.SetActive(true);
                _spineEntry.Tint = new Color(1f, 1f, 1f, 0.7f);
                _spineEntry.SetAnimation("hbdk_1", false, 0f);
            }
            yield return Animate(0.1f, t => SetPlusScale(Mathf.Lerp(1f, 1.15f, t)));
            yield return Animate(0.18f, t => SetPlusScale(Mathf.Lerp(1.15f, 1f, t)));
            yield return new WaitForSecondsRealtime(1.3f);

            if (!_previewOnly && _plusStack != null && _target != null && panelRoot != null)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panelRoot, _target);
                Vector2 from = _plusStack.anchoredPosition;
                Vector2 to = bounds.center;
                float targetScale = Mathf.Max(0.15f, bounds.size.y / 375f * 1.15f);
                yield return Animate(0.45f, t =>
                {
                    float eased = t * t;
                    _plusStack.anchoredPosition = Vector2.Lerp(from, to, eased);
                    SetPlusScale(Mathf.Lerp(1f, targetScale, eased));
                });
                _grantSteps?.Invoke(_amount);
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

        void SetPlusScale(float scale)
        {
            if (_plusStack != null) _plusStack.localScale = Vector3.one * scale;
        }

        void SetPlusAlpha(float alpha)
        {
            if (_plusImage == null) return;
            var color = _plusImage.color;
            color.a = alpha;
            _plusImage.color = color;
        }
    }
}
