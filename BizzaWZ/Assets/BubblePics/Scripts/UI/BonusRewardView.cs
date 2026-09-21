using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of bonus_reward_page.gd reveal and toolbar flight.</summary>
    public sealed class BonusRewardView : MetaFlowView
    {
        static readonly Vector2[][] LayoutCenters =
        {
            null,
            new[] { new Vector2(540f, 1080f) },
            new[] { new Vector2(315f, 1080f), new Vector2(765f, 1080f) },
            new[]
            {
                new Vector2(540f, 849f),
                new Vector2(340f, 1195f),
                new Vector2(740f, 1195f),
            },
        };

        static readonly float[] RevealScales = { 0f, 1f, 0.72f, 0.58f };

        [SerializeField] Image _overlay;
        [SerializeField] RectTransform _content;
        [SerializeField] Image[] _icons = Array.Empty<Image>();

        RectTransform _target;
        Action _completed;
        int _count;

        public override void InitializePrefabRuntime()
        {
            base.InitializePrefabRuntime();
            _overlay ??= FindNamed<Image>(panelRoot, "Overlay");
            _content ??= FindNamed<RectTransform>(panelRoot, "Content");
            if (_icons == null || _icons.Length < 3)
            {
                _icons = new[]
                {
                    FindNamed<Image>(panelRoot, "Icon1"),
                    FindNamed<Image>(panelRoot, "Icon2"),
                    FindNamed<Image>(panelRoot, "Icon3"),
                };
            }
        }

        public void Play(
            Sprite rewardIcon,
            int count,
            RectTransform target,
            Action completed = null)
        {
            _count = Mathf.Clamp(count, 1, 3);
            _target = target;
            _completed = completed;
            for (int i = 0; i < _icons.Length; i++)
            {
                var icon = _icons[i];
                if (icon == null) continue;
                icon.sprite = rewardIcon;
                icon.gameObject.SetActive(i < _count);
            }
            Show();
            Run(PlayRoutine());
        }

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            CanvasGroup authoredCanvasGroup,
            Image overlay,
            RectTransform content,
            Image[] icons)
        {
            base.ConfigurePrefabAuthoring(authoredPanelRoot, null, authoredCanvasGroup);
            _overlay = overlay;
            _content = content;
            _icons = icons ?? Array.Empty<Image>();
        }

        IEnumerator PlayRoutine()
        {
            SetOverlayAlpha(0f);
            Vector2 origin = new Vector2(540f, -1200f);
            for (int i = 0; i < _count; i++)
            {
                var rt = _icons[i].rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.anchoredPosition = origin;
                rt.localScale = Vector3.zero;
                SetIconAlpha(i, 0f);
            }
            yield return Animate(0.25f, t => SetOverlayAlpha(0.8f * t));
            SoundManager.I?.Play("bonus_chest_open");

            Vector2[] centers = LayoutCenters[_count];
            float revealScale = RevealScales[_count];
            yield return Animate(0.5f, t =>
            {
                float eased = EaseOutBack(t);
                for (int i = 0; i < _count; i++)
                {
                    var rt = _icons[i].rectTransform;
                    Vector2 destination = new Vector2(centers[i].x, -centers[i].y);
                    rt.anchoredPosition = Vector2.LerpUnclamped(origin, destination, eased);
                    rt.localScale = Vector3.one * Mathf.LerpUnclamped(0f, revealScale, eased);
                    SetIconAlpha(i, t);
                }
            });
            yield return new WaitForSecondsRealtime(0.3f);

            if (_target != null && panelRoot != null)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panelRoot, _target);
                var starts = new Vector2[_count];
                var scales = new Vector3[_count];
                for (int i = 0; i < _count; i++)
                {
                    starts[i] = _icons[i].rectTransform.anchoredPosition;
                    scales[i] = _icons[i].rectTransform.localScale;
                }
                yield return Animate(0.45f + (_count - 1) * 0.12f, elapsed =>
                {
                    float total = 0.45f + (_count - 1) * 0.12f;
                    for (int i = 0; i < _count; i++)
                    {
                        float local = Mathf.Clamp01((elapsed * total - i * 0.12f) / 0.45f);
                        float eased = local * local;
                        var rt = _icons[i].rectTransform;
                        rt.anchoredPosition = Vector2.Lerp(starts[i], bounds.center, eased);
                        rt.localScale = Vector3.Lerp(scales[i], Vector3.one * 0.34f, eased);
                    }
                });
            }
            yield return Animate(0.25f, t =>
            {
                SetOverlayAlpha(0.8f * (1f - t));
                for (int i = 0; i < _count; i++) SetIconAlpha(i, 1f - t);
            });
            _completed?.Invoke();
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

        void SetOverlayAlpha(float alpha)
        {
            if (_overlay == null) return;
            var color = _overlay.color;
            color.a = alpha;
            _overlay.color = color;
        }

        void SetIconAlpha(int index, float alpha)
        {
            if (index < 0 || index >= _icons.Length || _icons[index] == null) return;
            var color = _icons[index].color;
            color.a = alpha;
            _icons[index].color = color;
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
