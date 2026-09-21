using System.Collections;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Drives the live-board victory sequence. It plays the framed gameplay
    /// dolphin win animation while the HUD is still present. The settlement
    /// page then opens directly on its large centre idle pose; the former
    /// full-screen waves stay disabled.
    /// </summary>
    public class CompletionFx : MonoBehaviour
    {
        public const float EMIT_FPS_ASSUMED = 30f;
        public const int EXIT_FRAMES_BEFORE_SPINE = 0;
        public const int EXIT_FRAMES_BEFORE_COIN_FLY = 15;
        const float COMPLETE_WATCHDOG_SEC = 5f;

        BubblePage _page;
        public BubblePage Page
        {
            get => _page;
            set => _page = value;
        }

        // Kept only so existing prefabs deserialize without losing their
        // references. These objects are never loaded or played at runtime.
        [SerializeField] SpineLite.SpineSprite _waves;
        [SerializeField] GameObject _wavesRoot;

        DolphinDecoration _boundDolphin;
        Coroutine _playCo;
        Coroutine _watchdogCo;
        bool _revealEmitted;
        bool _done = true;

        public System.Action CompleteReveal;
        public System.Action Done;

        [ContextMenu("Authoring/Disable Legacy Waves")]
        public void SetupPrefabAuthoring()
        {
            DisableLegacyWaves();
        }

        public void Setup()
        {
            BindPrefabRuntime();
        }

        public void InitializePrefabRuntime(BubblePage page, Transform worldRoot = null)
        {
            Page = page;
            BindPrefabRuntime();
        }

        /// <summary>Restores the runtime-only dolphin completion callback.</summary>
        public void BindPrefabRuntime()
        {
            DisableLegacyWaves();

            var dolphin = Page != null && Page.TopBar != null
                ? Page.TopBar.Dolphin
                : null;
            if (_boundDolphin != dolphin)
            {
                if (_boundDolphin != null)
                    _boundDolphin.CompleteFinished -= OnDolphinCompleteFinished;
                _boundDolphin = dolphin;
            }
            if (_boundDolphin != null)
            {
                _boundDolphin.CompleteFinished -= OnDolphinCompleteFinished;
                _boundDolphin.CompleteFinished += OnDolphinCompleteFinished;
            }
        }

        void DisableLegacyWaves()
        {
            if (_waves == null && _wavesRoot != null)
                _waves = _wavesRoot.GetComponentInChildren<SpineLite.SpineSprite>(true);
            if (_waves != null)
            {
                _waves.ClearTracks();
                _waves.Tint = Color.white;
            }
            if (_wavesRoot != null)
                _wavesRoot.SetActive(false);
        }

        public void Arm()
        {
            if (_playCo != null)
            {
                StopCoroutine(_playCo);
                _playCo = null;
            }
            if (_watchdogCo != null)
            {
                StopCoroutine(_watchdogCo);
                _watchdogCo = null;
            }

            _done = false;
            _revealEmitted = false;
            DisableLegacyWaves();
            BindPrefabRuntime();
            _boundDolphin?.SetTintAlpha(1f);
        }

        public void PlayAtFrame(int frame)
        {
            if (_playCo != null)
                StopCoroutine(_playCo);
            _playCo = StartCoroutine(PlayCo(frame / EMIT_FPS_ASSUMED));
        }

        IEnumerator PlayCo(float delaySec)
        {
            if (delaySec > 0f)
                yield return new WaitForSeconds(delaySec);

            _playCo = null;
            BindPrefabRuntime();
            if (_boundDolphin == null)
            {
                EmitRevealOnce();
                Settle();
                yield break;
            }

            // This is still the live board: keep the original framed HUD
            // victory animation here. BubblePage switches to the dedicated
            // centre-screen skeleton only when PresentCompletion runs.
            _boundDolphin.PlayComplete();
            EmitRevealOnce();
            if (!_done)
                _watchdogCo = StartCoroutine(CompleteWatchdogCo());
        }

        IEnumerator CompleteWatchdogCo()
        {
            yield return new WaitForSeconds(COMPLETE_WATCHDOG_SEC);
            _watchdogCo = null;
            if (!_done)
                Settle();
        }

        void OnDolphinCompleteFinished()
        {
            EmitRevealOnce();
            Settle();
        }

        void EmitRevealOnce()
        {
            if (_revealEmitted) return;
            _revealEmitted = true;
            CompleteReveal?.Invoke();
        }

        void Settle()
        {
            if (_done) return;
            _done = true;
            Done?.Invoke();
        }

        public void Stop()
        {
            if (_playCo != null)
            {
                StopCoroutine(_playCo);
                _playCo = null;
            }
            if (_watchdogCo != null)
            {
                StopCoroutine(_watchdogCo);
                _watchdogCo = null;
            }
            _boundDolphin?.StopComplete();
            DisableLegacyWaves();
            Settle();
        }

        public IEnumerator AwaitDone()
        {
            while (!_done)
                yield return null;
        }

        /// <summary>
        /// Ends the live-board animation phase when the HUD has physically
        /// left the screen. Unlike Stop, this does not reset or reveal the HUD
        /// dolphin; BubblePage immediately replaces it with the settlement
        /// pose in the same frame.
        /// </summary>
        public void FinishForSettlement()
        {
            if (_playCo != null)
            {
                StopCoroutine(_playCo);
                _playCo = null;
            }
            if (_watchdogCo != null)
            {
                StopCoroutine(_watchdogCo);
                _watchdogCo = null;
            }
            EmitRevealOnce();
            Settle();
        }

        void OnDestroy()
        {
            if (_boundDolphin != null)
                _boundDolphin.CompleteFinished -= OnDolphinCompleteFinished;
        }
    }
}
