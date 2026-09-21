using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of bubble_tutorial.gd — hand loop + toast (level 1).</summary>
    public class Tutorial : MonoBehaviour
    {
        const float SETTLE_MIN_DELAY = 0.4f;
        const float SETTLE_MAX_DELAY = 4.0f;
        const float SETTLE_POLL = 0.1f;
        const float SETTLE_REST_SPEED = 45f;
        const float CLICK_HOLD = 0.35f;
        const float SLIDE_DURATION = 0.7f;
        const float UP_HOLD = 0.22f;
        const float FADE_OUT_DURATION = 0.25f;
        const float FADE_IN_DURATION = 0.18f;
        const float LOOP_GAP = 0.2f;
        const float FIND_RETRY_DELAY = 0.5f;
        const float IDLE_TIMEOUT = 3.0f;
        const float WRONG_TOAST_DURATION = 2.8f;
        const float MIX_DURATION = 0.15f;
        const float BANNER_UNFOLD_SEC = 0.42f;
        const float BANNER_LABEL_FADE_SEC = 0.2f;
        const int TOAST_FONT_SIZE_MAX = 56;
        const int TOAST_FONT_SIZE_MIN = 28;
        const int TOAST_MAX_LINES = 2;

        static readonly Color DefaultToastColor = new Color32(0x19, 0x2A, 0x49, 0xFF);
        static readonly Color WrongToastColor = new Color(1f, 0.45f, 0.45f, 1f);

        public BubblePage Page;

        BubbleTutorialView _view;
        SpineLite.SpineSprite _hand;
        Transform _handTransform;
        RectTransform _toastRoot;
        TMP_Text _toastLabel;
        Image _toastBg;
        CanvasGroup _toastLabelGroup;
        TutorialAmbientBubbles _ambient;
        Coroutine _startCo;
        Coroutine _loopCo;
        Coroutine _idleCo;
        Coroutine _wrongCo;
        Coroutine _handFadeCo;
        Coroutine _unfoldCo;
        bool _active;
        bool _wrongShown;
        bool _showingWrongToast;
        bool _localeSubscribed;
        int _loopToken;
        readonly List<BubbleView> _highlighted = new List<BubbleView>();

        public void Begin()
        {
            if (_active) return;
            if (!CreateView()) return;

            _active = true;
            _wrongShown = false;
            _showingWrongToast = false;
            _loopToken++;
            SubscribeLocale();
            _ambient.StartEffect();
            if (Page != null)
            {
                FunSmithTelemetry.TrackTutorialStart(
                    Page.CurrentLevelNumber,
                    Page.CountRemainingBubbles());
                FunSmithTelemetry.TrackTutorialStepShow(
                    Page.CurrentLevelNumber,
                    "complete_first_picture",
                    1,
                    "merge_matching_bubbles");
            }
            _startCo = StartCoroutine(StartCo(_loopToken));
        }

        /// <summary>
        /// Level restart / exit / jump cleanup. Detached prefab mounts are
        /// owned by PrefabMountSet and are removed together with the view.
        /// </summary>
        public void Cancel(string reason = "level_replaced")
        {
            if (!_active && _view == null) return;
            if (_active && Page != null && !SaveState.TutorialDone)
            {
                FunSmithTelemetry.TrackTutorialSkip(
                    Page.CurrentLevelNumber,
                    Page.CurrentRoundElapsedSeconds,
                    reason);
            }
            _active = false;
            _loopToken++;
            StopAllCoroutines();
            UnsubscribeLocale();
            ClearHighlight();
            _ambient?.StopEffect();
            if (_view != null)
            {
                _view.gameObject.SetActive(false);
                Destroy(_view.gameObject);
            }
            ClearViewReferences();
        }

        bool CreateView()
        {
            if (_view != null) return true;
            var catalog = App.I != null ? App.I.Prefabs : PrefabCatalog.Current;
            var prefab = catalog != null ? catalog.BubbleTutorial : null;
#if UNITY_EDITOR
            if (prefab == null)
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/RuntimePrefabs/Game/BubbleTutorial.prefab");
#endif
            if (prefab == null)
            {
                Debug.LogError("BubbleTutorial prefab is missing; the tutorial cannot be displayed.");
                return false;
            }

            _view = PrefabCatalog.InstantiateComponent<BubbleTutorialView>(prefab, transform);
            if (_view == null || !_view.InitializeRuntime())
            {
                if (_view != null) Destroy(_view.gameObject);
                ClearViewReferences();
                return false;
            }

            _hand = _view.Hand;
            _handTransform = _view.HandTransform;
            _toastRoot = _view.ToastContainer;
            _toastBg = _view.ToastBackground;
            _toastLabel = _view.ToastLabel;
            _toastLabelGroup = _view.ToastLabelGroup;
            _ambient = _view.AmbientBubbles;
            SetDefaultToast();
            _view.LayoutToast();
            return true;
        }

        void ClearViewReferences()
        {
            _view = null;
            _hand = null;
            _handTransform = null;
            _toastRoot = null;
            _toastLabel = null;
            _toastBg = null;
            _toastLabelGroup = null;
            _ambient = null;
            _startCo = null;
            _loopCo = null;
            _idleCo = null;
            _wrongCo = null;
            _handFadeCo = null;
            _unfoldCo = null;
        }

        IEnumerator StartCo(int token)
        {
            yield return new WaitForSeconds(SETTLE_MIN_DELAY);
            var elapsed = SETTLE_MIN_DELAY;
            while (elapsed < SETTLE_MAX_DELAY &&
                   !Page.Field.AreAllBubblesAtRest(SETTLE_REST_SPEED))
            {
                yield return new WaitForSeconds(SETTLE_POLL);
                elapsed += SETTLE_POLL;
                if (!IsActive(token)) yield break;
            }
            if (!IsActive(token)) yield break;

            ShowToast();
            _loopCo = StartCoroutine(FingerLoopCo(token));
        }

        IEnumerator FingerLoopCo(int token)
        {
            while (IsActive(token))
            {
                var pair = FindPair();
                if (pair == null)
                {
                    yield return new WaitForSeconds(FIND_RETRY_DELAY);
                    continue;
                }

                var (a, b) = pair.Value;
                Highlight(a, b);
                if (_handTransform == null || a == null || b == null)
                {
                    ClearHighlight();
                    yield break;
                }

                _handTransform.position = a.transform.position;
                _hand.gameObject.SetActive(true);
                _hand.SetAnimation("click", false, MIX_DURATION);
                if (_handFadeCo != null) StopCoroutine(_handFadeCo);
                _handFadeCo = StartCoroutine(FadeHand(token, 1f, FADE_IN_DURATION));
                yield return new WaitForSeconds(CLICK_HOLD);
                if (!IsPairActive(token, a, b)) yield break;

                _hand.SetAnimation("stilldown", true, MIX_DURATION);
                var slideElapsed = 0f;
                var from = a.transform.position;
                var to = b.transform.position;
                while (slideElapsed < SLIDE_DURATION)
                {
                    if (!IsPairActive(token, a, b)) yield break;
                    slideElapsed += Time.deltaTime;
                    var k = Tween.Evaluate(Ease.InOutCubic,
                        Mathf.Clamp01(slideElapsed / SLIDE_DURATION));
                    _handTransform.position = Vector3.Lerp(from, to, k);
                    yield return null;
                }

                _hand.SetAnimation("up", false, MIX_DURATION);
                yield return new WaitForSeconds(UP_HOLD);
                if (!IsPairActive(token, a, b)) yield break;
                yield return FadeHand(token, 0f, FADE_OUT_DURATION);
                if (!IsActive(token)) yield break;
                ClearHighlight();
                yield return new WaitForSeconds(LOOP_GAP);
            }
        }

        (BubbleView, BubbleView)? FindPair()
        {
            if (Page == null || Page.Field == null) return null;
            var alive = Page.Field.AllBubbles()
                .Where(b => b != null &&
                            b.State == BubbleState.Alive &&
                            !b.Dragging &&
                            !b.IsLocked &&
                            b.Fragment != null)
                .ToList();
            BubbleView bestA = null;
            BubbleView bestB = null;
            var bestDiff = -1;
            for (var i = 0; i < alive.Count; i++)
            {
                for (var j = i + 1; j < alive.Count; j++)
                {
                    var a = alive[i];
                    var b = alive[j];
                    if (!a.CanMergeWith(b)) continue;
                    var aSize = a.Fragment.HeldPaths.Count;
                    var bSize = b.Fragment.HeldPaths.Count;
                    var diff = Mathf.Abs(aSize - bSize);
                    if (bestA != null && diff <= bestDiff) continue;
                    bestDiff = diff;
                    if (aSize <= bSize)
                    {
                        bestA = a;
                        bestB = b;
                    }
                    else
                    {
                        bestA = b;
                        bestB = a;
                    }
                }
            }
            return bestA != null ? (bestA, bestB) : ((BubbleView, BubbleView)?)null;
        }

        void Highlight(BubbleView a, BubbleView b)
        {
            ClearHighlight();
            if (a != null)
            {
                a.SetHintHighlight(true);
                _highlighted.Add(a);
            }
            if (b != null)
            {
                b.SetHintHighlight(true);
                _highlighted.Add(b);
            }
        }

        void ClearHighlight()
        {
            foreach (var bubble in _highlighted)
                if (bubble != null)
                    bubble.SetHintHighlight(false);
            _highlighted.Clear();
        }

        IEnumerator FadeHand(int token, float targetAlpha, float duration)
        {
            if (_hand == null) yield break;
            var from = _hand.Tint.a;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (!IsActive(token)) yield break;
                elapsed += Time.deltaTime;
                SetHandAlpha(Mathf.Lerp(from, targetAlpha,
                    Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            SetHandAlpha(targetAlpha);
        }

        IEnumerator FadeHandUnconditional(float targetAlpha, float duration)
        {
            if (_hand == null) yield break;
            var from = _hand.Tint.a;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetHandAlpha(Mathf.Lerp(from, targetAlpha,
                    Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            SetHandAlpha(targetAlpha);
        }

        void SetHandAlpha(float alpha)
        {
            if (_hand != null)
                _hand.Tint = new Color(1f, 1f, 1f, alpha);
        }

        void ShowToast()
        {
            if (_toastRoot == null) return;
            _view.LayoutToast();
            _toastRoot.gameObject.SetActive(true);
            if (_unfoldCo != null) StopCoroutine(_unfoldCo);
            _unfoldCo = StartCoroutine(UnfoldToastCo());
        }

        IEnumerator UnfoldToastCo()
        {
            if (_toastBg == null || _toastLabelGroup == null) yield break;
            _toastBg.rectTransform.localScale = new Vector3(1f, 0f, 1f);
            _toastLabelGroup.alpha = 0f;
            var elapsed = 0f;
            while (elapsed < BANNER_UNFOLD_SEC)
            {
                elapsed += Time.deltaTime;
                var k = Tween.Evaluate(Ease.OutBack,
                    Mathf.Clamp01(elapsed / BANNER_UNFOLD_SEC));
                _toastBg.rectTransform.localScale = new Vector3(1f, k, 1f);
                yield return null;
            }
            _toastBg.rectTransform.localScale = Vector3.one;

            elapsed = 0f;
            while (elapsed < BANNER_LABEL_FADE_SEC)
            {
                elapsed += Time.deltaTime;
                _toastLabelGroup.alpha =
                    Mathf.Clamp01(elapsed / BANNER_LABEL_FADE_SEC);
                yield return null;
            }
            _toastLabelGroup.alpha = 1f;
        }

        void SetDefaultToast()
        {
            if (_toastLabel == null) return;
            _toastLabel.text = Localization.Tr("BUBBLE_GUIDE_MERGE");
            _toastLabel.color = DefaultToastColor;
            FitToastFontSize();
        }

        void FitToastFontSize()
        {
            if (_toastLabel == null || _toastLabel.font == null ||
                string.IsNullOrEmpty(_toastLabel.text))
                return;
            var width = Mathf.Max(1f, _toastLabel.rectTransform.rect.width);
            var size = TOAST_FONT_SIZE_MAX;
            while (size > TOAST_FONT_SIZE_MIN &&
                   MeasureWrappedLines(_toastLabel.text, size, width) > TOAST_MAX_LINES)
                size -= 2;
            _toastLabel.fontSize = size;
        }

        int MeasureWrappedLines(string text, int fontSize, float width)
        {
            _toastLabel.enableAutoSizing = false;
            _toastLabel.enableWordWrapping = true;
            _toastLabel.overflowMode = TextOverflowModes.Overflow;
            _toastLabel.fontSize = fontSize;
            // The toast hierarchy is hidden while its first localized message is fitted.
            _toastLabel.ForceMeshUpdate(true, true);
            return _toastLabel.textInfo.lineCount;
        }

        // ------------------------------------------------------------ events
        public void OnDragStarted()
        {
            if (!_active) return;
            PauseFingerLoop();
            if (_toastRoot != null)
                _toastRoot.gameObject.SetActive(false);
            if (_handFadeCo != null) StopCoroutine(_handFadeCo);
            _handFadeCo = StartCoroutine(FadeHandUnconditional(0f, 0.15f));
            RestartIdleTimer();
        }

        void PauseFingerLoop()
        {
            _loopToken++;
            if (_startCo != null)
            {
                StopCoroutine(_startCo);
                _startCo = null;
            }
            if (_loopCo != null)
            {
                StopCoroutine(_loopCo);
                _loopCo = null;
            }
            ClearHighlight();
        }

        void RestartIdleTimer()
        {
            if (_idleCo != null) StopCoroutine(_idleCo);
            _idleCo = StartCoroutine(IdleCo());
        }

        IEnumerator IdleCo()
        {
            yield return new WaitForSeconds(IDLE_TIMEOUT);
            if (!_active) yield break;
            _loopToken++;
            _loopCo = StartCoroutine(FingerLoopCo(_loopToken));
            _idleCo = null;
        }

        public void OnMergeRejected(BubbleView src, BubbleView target)
        {
            if (!_active || _wrongShown || _toastLabel == null) return;
            _wrongShown = true;
            var sameImage = src != null && target != null &&
                            src.Fragment != null && target.Fragment != null &&
                            src.Fragment.ImageId == target.Fragment.ImageId;
            var message = sameImage
                ? "Can't merge — doesn't match!"
                : "Can't merge — different pictures!";
            FunSmithTelemetry.TrackTutorialBlockedInteraction(
                Page != null ? Page.CurrentLevelNumber : 1,
                "complete_first_picture",
                sameImage ? "shape_mismatch" : "different_picture",
                "merge",
                src?.Fragment != null ? src.Fragment.ImageId : -1);
            _wrongCo = StartCoroutine(WrongToastCo(message));
        }

        IEnumerator WrongToastCo(string message)
        {
            _showingWrongToast = true;
            _toastLabel.text = message;
            _toastLabel.color = WrongToastColor;
            FitToastFontSize();
            yield return new WaitForSeconds(WRONG_TOAST_DURATION);
            if (!_active || _toastLabel == null) yield break;
            _showingWrongToast = false;
            SetDefaultToast();
            _wrongCo = null;
        }

        public void OnMergeCommitted(bool isClosure)
        {
            if (!_active) return;
            if (!isClosure)
            {
                RestartIdleTimer();
                return;
            }

            SaveState.TutorialDone = true;
            BizzaGameplayBridge.CompleteBaseTutorial();
            if (Page != null)
            {
                FunSmithTelemetry.TrackUserGuideStep(1);
                FunSmithTelemetry.TrackTutorialComplete(
                    Page.CurrentLevelNumber,
                    Page.CurrentRoundElapsedSeconds,
                    Page.StepRule.UsedSteps);
            }
            _active = false;
            _loopToken++;
            StopAllCoroutines();
            UnsubscribeLocale();
            StartCoroutine(FinishCo());
        }

        IEnumerator FinishCo()
        {
            ClearHighlight();
            _ambient?.StopEffect();
            var elapsed = 0f;
            var fromHand = _hand != null ? _hand.Tint.a : 0f;
            var fromLabel = _toastLabelGroup != null ? _toastLabelGroup.alpha : 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                var k = Mathf.Clamp01(elapsed / 0.3f);
                SetHandAlpha(Mathf.Lerp(fromHand, 0f, k));
                if (_toastLabelGroup != null)
                    _toastLabelGroup.alpha = Mathf.Lerp(fromLabel, 0f, k);
                yield return null;
            }

            if (_view != null)
                Destroy(_view.gameObject);
            ClearViewReferences();
        }

        bool IsActive(int token) => _active && token == _loopToken;

        bool IsPairActive(int token, BubbleView a, BubbleView b) =>
            IsActive(token) && a != null && b != null;

        void SubscribeLocale()
        {
            if (_localeSubscribed) return;
            Localization.LocaleChanged += OnLocaleChanged;
            _localeSubscribed = true;
        }

        void UnsubscribeLocale()
        {
            if (!_localeSubscribed) return;
            Localization.LocaleChanged -= OnLocaleChanged;
            _localeSubscribed = false;
        }

        void OnLocaleChanged()
        {
            if (!_showingWrongToast)
                SetDefaultToast();
            else
                FitToastFontSize();
        }

        void OnDestroy()
        {
            UnsubscribeLocale();
        }
    }
}
