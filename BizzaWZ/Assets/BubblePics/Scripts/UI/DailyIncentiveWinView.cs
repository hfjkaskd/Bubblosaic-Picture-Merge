using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of daily_incentive_win_page.gd.</summary>
    public sealed class DailyIncentiveWinView : MetaFlowView
    {
        [SerializeField] Image _scrim;
        [SerializeField] TMP_Text _title;
        [SerializeField] Button _tapCatcher;
        [SerializeField] SpineLite.SpineSprite _spine;
        [SerializeField] string _spineResourceName = "daily_incentive_win";

        bool _closing;

        public SpineLite.SpineSprite Spine => _spine;

        public override void InitializePrefabRuntime()
        {
            base.InitializePrefabRuntime();
            _scrim ??= FindNamed<Image>(panelRoot, "Scrim");
            _title ??= FindNamed<TMP_Text>(panelRoot, "Title");
            _tapCatcher ??= FindNamed<Button>(panelRoot, "TapCatcher");
            if (_spine == null && worldRoot != null)
                _spine = worldRoot.GetComponentInChildren<SpineLite.SpineSprite>(true);
            if (_spine != null)
            {
                if (_spine.Data == null) _spine.Load(_spineResourceName);
                // The recovered page draws its Spine after the 70% scrim.
                // World sprites default to order zero in Unity and would be
                // darkened beneath the screen-space dialog canvas instead.
                int dialogOrder = App.I != null && App.I.DialogCanvas != null
                    ? App.I.DialogCanvas.sortingOrder
                    : 2600;
                _spine.SortingOrder = dialogOrder + 20;
                // Keep the mesh a small positive distance in front of the
                // orthographic camera.  This effect has unusually large
                // conservative bounds and was being rejected at the camera
                // plane even though its mesh and materials were valid.
                Vector3 position = _spine.transform.localPosition;
                position.z = 1f;
                _spine.transform.localPosition = position;
            }
            if (_tapCatcher != null)
            {
                _tapCatcher.onClick.RemoveListener(Close);
                _tapCatcher.onClick.AddListener(Close);
            }
        }

        public void Play()
        {
            _closing = false;
            if (_title != null)
            {
                int tip = UnityEngine.Random.Range(1, 6);
                _title.text = Text(
                    $"daily_incentive_win_tips{tip}",
                    "Your first win today!");
            }
            Show();
            Run(PlayRoutine());
        }

        public void Close()
        {
            if (_closing) return;
            _closing = true;
            Run(CloseRoutine());
        }

        public override void HandleBackRequest() => Close();

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            Transform authoredWorldRoot,
            CanvasGroup authoredCanvasGroup,
            Image scrim,
            TMP_Text title,
            Button tapCatcher,
            SpineLite.SpineSprite spine)
        {
            base.ConfigurePrefabAuthoring(authoredPanelRoot, authoredWorldRoot, authoredCanvasGroup);
            _scrim = scrim;
            _title = title;
            _tapCatcher = tapCatcher;
            _spine = spine;
        }

        IEnumerator PlayRoutine()
        {
            SetScrimAlpha(0f);
            if (_spine != null) _spine.gameObject.SetActive(false);
            yield return Animate(0.25f, t => SetScrimAlpha(0.7f * t));
            if (_spine != null)
            {
                if (_spine.Data == null) _spine.Load(_spineResourceName);
                _spine.gameObject.SetActive(true);
                string animation = PickAnimation(_spine.Data);
                if (!string.IsNullOrEmpty(animation))
                    _spine.SetAnimation(animation, false, 0f);
            }
            SoundManager.I?.Play("daily_incentive_win");
            yield return new WaitForSecondsRealtime(1.8f);
            if (!_closing) Close();
        }

        IEnumerator CloseRoutine()
        {
            float start = canvasGroup != null ? canvasGroup.alpha : 1f;
            yield return Animate(0.25f, t =>
            {
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(start, 0f, t);
            });
            Hide();
        }

        static string PickAnimation(SpineLite.SkeletonData data)
        {
            if (data == null || data.Animations == null) return string.Empty;
            string first = string.Empty;
            foreach (var animation in data.Animations)
            {
                if (string.IsNullOrEmpty(first)) first = animation.Key;
                if (animation.Key != "idle") return animation.Key;
            }
            return first;
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

        void SetScrimAlpha(float alpha)
        {
            if (_scrim == null) return;
            var color = _scrim.color;
            color.a = alpha;
            _scrim.color = color;
        }
    }
}
