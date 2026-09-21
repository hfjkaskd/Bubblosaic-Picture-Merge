using System.Collections;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Port of dolphin_decoration.gd — the HUD dolphin SpineSprite living in
    /// TopGameBar CenterPanel. Gameplay completion stays on the framed HUD
    /// portrait; the settlement page rebuilds the gameplay skeleton directly
    /// into its large, frameless idle_1 centre pose.
    /// </summary>
    public class DolphinDecoration : MonoBehaviour
    {
        public const int COMPLETE_TOP_Z_INDEX = 300;
        public const float COMPLETE_PLAYBACK_SPEED = 1.5f;
        public const float MIX_DURATION = 0.15f;
        public const float REVERSE_SPEED = 2.0f;
        public const float NUMBER_TO_IDLE_MIX = 0.5f;
        public const float NUMBER_SCALE_MULTIPLIER = 2.5f;
        public const float NUMBER_COMPLETE_Y_OFFSET = -250f;
        // With the separate clear-level title removed from CompletePanel, use
        // the released upper space and keep the authored Level Complete board
        // clear of the next-level button.
        public const float COMPLETE_CENTER_Y_RATIO = 0.33f;
        // Settlement opens directly at this final large scale. There is no
        // smaller intro pose or subsequent animation-size hand-off.
        public const float COMPLETE_SCALE_MULTIPLIER = 1.87f;
        public static readonly Vector2 PORTRAIT_OFFSET_FROM_CENTER = new Vector2(0, -27.5f);
        public const float DOLPHIN_SCALE = 0.72f;

        [SerializeField] SpineLite.SpineSprite _spine;
        public SpineLite.SpineSprite Spine => _spine;

        public System.Action ExitFinished;
        public System.Action CompleteFinished;

        bool _exitRunning, _exitDone = true;
        bool _completeRunning, _completeDone = true;
        bool _waitingReturnToIdle;
        bool _holdingFail;
        bool _hiddenForBonus;
        bool _hardFrame;
        bool _reloadGameplayBeforeIntro;
        bool _settlementPose;
        float _reverseT = -1f;
        SpineLite.TrackEntry _reverseEntry;
        SpineLite.TrackEntry _completeEntry;
        Vector3 _basePosition;
        float _baseScale = 1f;

        const string ANIM_IN = "in";
        const string ANIM_IDLE = "idle";
        const string ANIM_FAILURE = "failure";
        const string GAMEPLAY_SPINE_MODULE = "dolphin";
        const string ANIM_GAMEPLAY_WIN = "youxishengli";
        const string ANIM_APPLAUD = "applaud";
        const string ANIM_NUMBER = "number";
        const string ANIM_IDLE_1 = "idle_1";

        /// <summary>
        /// Initializes an instance loaded from DolphinDecoration.prefab.  Spine
        /// delegates are runtime-only, so they must be rebound after every
        /// prefab instantiation.
        /// </summary>
        public void InitializePrefabRuntime(Vector3 centerWorldPos, float hudScale)
        {
            BindPrefabRuntime();
            ApplyHudLayout(centerWorldPos, hudScale);
            HoldIntroFirstFrame();
        }

        /// <summary>
        /// Re-applies the portrait's authored offset when the expanded viewport
        /// or safe area changes.  Keep settlement animation transforms intact.
        /// </summary>
        public void ApplyHudLayout(Vector3 centerWorldPos, float hudScale)
        {
            _basePosition = centerWorldPos +
                (Vector3)(PORTRAIT_OFFSET_FROM_CENTER * new Vector2(hudScale, -hudScale));
            _baseScale = DOLPHIN_SCALE * hudScale;
            if (_settlementPose) return;

            transform.position = _basePosition;
            transform.localScale = Vector3.one * _baseScale;
        }

        /// <summary>Compatibility entry point used by the existing BubblePage.</summary>
        public void Init(Vector3 centerWorldPos, float hudScale)
        {
            InitializePrefabRuntime(centerWorldPos, hudScale);
        }

        /// <summary>Restores references and non-serialized Spine callbacks.</summary>
        public void BindPrefabRuntime()
        {
            if (_spine == null)
                _spine = GetComponentInChildren<SpineLite.SpineSprite>(true);
            if (_spine == null)
            {
                var go = new GameObject("SpineSprite");
                go.transform.SetParent(transform, false);
                _spine = go.AddComponent<SpineLite.SpineSprite>();
            }
            if (_spine.Data == null)
                _spine.Load(GAMEPLAY_SPINE_MODULE);
            _spine.AnimationCompleted -= OnAnimCompleted;
            _spine.AnimationCompleted += OnAnimCompleted;
        }

        void Awake()
        {
            // A prefab can be enabled before its owner injects layout values.
            // Binding here is safe; InitializePrefabRuntime applies position,
            // scale and the intro pose once the HUD is ready.
            if (_spine != null)
                BindPrefabRuntime();
        }

        public void SetSortingOrder(int order) { if (_spine != null) _spine.SortingOrder = order; }

        public void SetHardFrame(bool hard)
        {
            _hardFrame = hard;
            if (_spine == null) BindPrefabRuntime();
            if (_spine == null) return;
            string attachment = hard ? "gp_ip_bg_hard" : "03";
            if (!_spine.SetAttachment("03", attachment))
            {
                // The replacement Spine export intentionally contains only
                // the normal portrait frame. Never retain a stale override
                // when the optional hard-frame attachment is absent.
                _spine.SetAttachment("03", "03");
            }
        }

        public void SetHiddenForBonus(bool hidden)
        {
            _hiddenForBonus = hidden;
            gameObject.SetActive(!hidden);
            if (!hidden)
            {
                BindPrefabRuntime();
                SetHardFrame(_hardFrame);
            }
        }

        void ResetToIntroPose()
        {
            _settlementPose = false;
            // idle_1 on the settlement page keys a different collection of
            // slots and draw-order values from the HUD admission animation.
            // Merely replacing track 0 can therefore leave unkeyed settlement
            // state in the next "in" animation. Reinitialize once after a
            // settlement visit, matching the editor component's Reload action.
            RestoreGameplaySpine(_reloadGameplayBeforeIntro);
            _reloadGameplayBeforeIntro = false;
            SetSortingOrder(SortOrder.DolphinNormal);
            transform.position = _basePosition;
            transform.localScale = Vector3.one * _baseScale;
            if (_spine == null) BindPrefabRuntime();
            SetHardFrame(_hardFrame);
            var entry = _spine.SetAnimation(ANIM_IN, false, 0f);
            if (entry != null)
            {
                entry.TimeScale = 0f;
                entry.TrackTime = 0f;
                // Load/Initialize renders the setup pose. Apply the authored
                // admission frame immediately so that pose cannot flash for
                // one frame before the next MonoBehaviour update.
                _spine.ApplyAndRender();
            }
        }

        public void HoldIntroFirstFrame()
        {
            gameObject.SetActive(!_hiddenForBonus);
            if (_hiddenForBonus) return;
            SetTintAlpha(1f);
            ResetToIntroPose();
        }

        public void SetTintAlpha(float a)
        {
            if (_spine != null) _spine.Tint = new Color(1, 1, 1, a);
        }

        public void PlayAdmission()
        {
            if (_hiddenForBonus) return;
            _settlementPose = false;
            gameObject.SetActive(true);
            SetTintAlpha(1f);
            SetSortingOrder(SortOrder.DolphinNormal);
            transform.position = _basePosition;
            transform.localScale = Vector3.one * _baseScale;
            PlayEmote(ANIM_IN, 0f);
        }

        public void PlayExit()
        {
            _exitDone = false;
            _exitRunning = true;
            // dolphin has no exit anim -> reverse admission
            PlayAdmissionReverse();
        }

        public void PlayAdmissionReverse()
        {
            if (_spine == null) BindPrefabRuntime();
            var entry = _spine.SetAnimation(ANIM_IN, false, MIX_DURATION);
            if (entry == null) return;
            float dur = entry.Animation.Duration;
            entry.TimeScale = 0f;
            entry.TrackTime = dur;
            _reverseEntry = entry;
            _reverseT = dur;
        }

        public void PlayComplete()
        {
            // Original flow: this phase belongs to the live gameplay HUD.
            // The replacement dolphin export names that authored framed win
            // animation "youxishengli". Never load the settlement skeleton
            // here, otherwise its full-body pose covers the top game bar.
            _completeDone = false;
            _completeRunning = true;
            _waitingReturnToIdle = false;
            _completeEntry = null;
            _settlementPose = false;
            if (_hiddenForBonus)
            {
                EmitCompleteFinished();
                return;
            }

            gameObject.SetActive(true);
            RestoreGameplaySpine();
            transform.position = _basePosition;
            transform.localScale = Vector3.one * _baseScale;
            SetSortingOrder(COMPLETE_TOP_Z_INDEX * 10);
            SetTintAlpha(1f);
            SetHardFrame(_hardFrame);

            string animation = _spine != null &&
                _spine.HasAnimation(ANIM_GAMEPLAY_WIN)
                    ? ANIM_GAMEPLAY_WIN
                    : ANIM_APPLAUD;
            var entry = _spine?.SetAnimation(animation, false, 0f);
            if (entry != null)
            {
                entry.TimeScale = COMPLETE_PLAYBACK_SPEED;
                _completeEntry = entry;
                return;
            }

            Debug.LogError(
                "The gameplay dolphin failed to start its framed victory " +
                "animation.",
                this);
            EmitCompleteFinished();
        }

        /// <summary>
        /// Shows the final large centre pose as soon as the settlement page is
        /// entered. The authored number entrance runs once, then hands off to
        /// the large idle pose for the rest of the page lifetime.
        /// </summary>
        public void PlaySettlementComplete()
        {
            // Settlement is shared by every gameplay rule, including bonus
            // rounds whose HUD dolphin is hidden during play.
            gameObject.SetActive(true);
            _completeDone = true;
            _completeRunning = false;
            _waitingReturnToIdle = false;
            _completeEntry = null;
            _reloadGameplayBeforeIntro = true;
            _settlementPose = true;
            // Rebuild once here so gameplay's framed victory draw order and
            // attachments cannot flash before the settlement pose.
            RestoreGameplaySpine(true);
            SetSortingOrder(COMPLETE_TOP_Z_INDEX * 10);
            SetTintAlpha(1f);
            _spine.ClearAttachmentOverride("03");
            float viewWidth = DeviceLayout.ViewWidth > 0f
                ? DeviceLayout.ViewWidth
                : App.DesignW;
            float viewHeight = DeviceLayout.ViewHeight > 0f
                ? DeviceLayout.ViewHeight
                : App.DesignH;
            transform.position = App.DesignToWorld(new Vector2(
                viewWidth * 0.5f,
                viewHeight * COMPLETE_CENTER_Y_RATIO));
            transform.localScale = Vector3.one *
                (_baseScale * COMPLETE_SCALE_MULTIPLIER);
            string idle = _spine.HasAnimation(ANIM_IDLE_1)
                ? ANIM_IDLE_1
                : ANIM_IDLE;
            _spine.SetDefaultMix(0f);
            string entrance = _spine.HasAnimation(ANIM_NUMBER)
                ? ANIM_NUMBER
                : idle;
            bool hasEntrance = entrance != idle;
            var entry = _spine.SetAnimation(
                entrance,
                !hasEntrance,
                0f);
            if (entry != null)
            {
                entry.TimeScale = 1f;
                entry.TrackTime = 0f;
                if (hasEntrance)
                    _spine.AddAnimation(idle, 0f, true, 0f);
                _spine.ApplyAndRender();
                return;
            }

            Debug.LogError(
                "The dolphin Spine failed to start its large settlement " +
                "number-to-idle animation sequence.",
                this);
        }

        /// <summary>
        /// Text-only modes use the original large centre-screen dolphin on
        /// the completion page instead of leaving the detached HUD portrait
        /// frame at the top of the screen.
        /// </summary>
        public void PlayNumberAtCenter()
        {
            if (_hiddenForBonus) return;
            if (_spine == null) BindPrefabRuntime();
            RestoreGameplaySpine();
            if (_spine == null || !_spine.HasAnimation(ANIM_NUMBER))
            {
                Dismiss();
                return;
            }

            gameObject.SetActive(true);
            _holdingFail = false;
            _completeRunning = false;
            _completeDone = true;
            _waitingReturnToIdle = false;
            _reverseT = -1f;
            _reverseEntry = null;
            SetTintAlpha(1f);
            SetSortingOrder(COMPLETE_TOP_Z_INDEX * 10);

            // The normal/hard HUD portrait frame is a runtime override on
            // slot 03. The original clears that override before the number
            // animation, allowing the animation to hide the frame entirely.
            _spine.ClearAttachmentOverride("03");

            float viewWidth = DeviceLayout.ViewWidth > 0f
                ? DeviceLayout.ViewWidth
                : App.DesignW;
            float viewHeight = DeviceLayout.ViewHeight > 0f
                ? DeviceLayout.ViewHeight
                : App.DesignH;
            transform.position = App.DesignToWorld(new Vector2(
                viewWidth * 0.5f,
                viewHeight * 0.5f + NUMBER_COMPLETE_Y_OFFSET));
            transform.localScale = Vector3.one *
                (_baseScale * NUMBER_SCALE_MULTIPLIER);

            _spine.SetDefaultMix(NUMBER_TO_IDLE_MIX);
            var entry = _spine.SetAnimation(ANIM_NUMBER, false, 0f);
            if (entry != null) entry.TimeScale = 1f;
            string idle = _spine.HasAnimation(ANIM_IDLE_1)
                ? ANIM_IDLE_1
                : ANIM_IDLE;
            _spine.AddAnimation(idle, 0f, true, NUMBER_TO_IDLE_MIX);
        }

        /// <summary>Hides the world-space portrait once the HUD has slid out.</summary>
        public void Dismiss()
        {
            if (_spine != null)
                _spine.ClearTracks();
            ResetToIntroPose();
            gameObject.SetActive(false);
        }

        public void StopComplete()
        {
            _completeEntry = null;
            EmitCompleteFinished();
            ResetToIntroPose();
        }

        public void PlayFail()
        {
            if (_hiddenForBonus) return;
            if (_exitRunning) return;
            _waitingReturnToIdle = false;
            _holdingFail = true;
            if (_spine == null) BindPrefabRuntime();
            var entry = _spine.SetAnimation(ANIM_FAILURE, false, MIX_DURATION);
            if (entry != null) StartCoroutine(FailFreezeCo(entry));
        }

        IEnumerator FailFreezeCo(SpineLite.TrackEntry entry)
        {
            const float threshold = 60f / 30f; // FAIL_PAUSE_FRAME / FAIL_FPS
            while (_holdingFail && _spine != null && _spine.Current == entry)
            {
                if (entry.TrackTime >= threshold)
                {
                    entry.TimeScale = 0f;
                    entry.TrackTime = threshold;
                    yield break;
                }
                yield return null;
            }
        }

        public void PlayApplaud()
        {
            if (_holdingFail || _exitRunning || _completeRunning) return;
            PlayEmote(ANIM_APPLAUD);
        }

        public void ResumeIdle()
        {
            _holdingFail = false;
            _settlementPose = false;
            gameObject.SetActive(!_hiddenForBonus);
            if (_hiddenForBonus) return;
            transform.position = _basePosition;
            transform.localScale = Vector3.one * _baseScale;
            SetSortingOrder(SortOrder.DolphinNormal);
            SetTintAlpha(1f);
            PlayIdle();
        }

        /// <summary>
        /// Mirrors the HUD top bar's vertical transition on this separately
        /// mounted world-space Spine. In Godot the dolphin is a child of the
        /// top bar and receives this offset through normal inheritance.
        /// </summary>
        public void SetHudBarOffset(float designYOffset)
        {
            if (_settlementPose) return;
            transform.position = _basePosition +
                Vector3.up * (designYOffset * App.WorldPerDesign);
        }

        void PlayEmote(string animName, float mixDur = MIX_DURATION)
        {
            _waitingReturnToIdle = true;
            if (_spine == null) BindPrefabRuntime();
            RestoreGameplaySpine();
            _spine.SetAnimation(animName, false, mixDur);
        }

        void PlayIdle()
        {
            _waitingReturnToIdle = false;
            if (_spine == null) BindPrefabRuntime();
            RestoreGameplaySpine();
            _spine.SetAnimation(ANIM_IDLE, true, MIX_DURATION);
        }

        void RestoreGameplaySpine(bool forceReload = false)
        {
            if (_spine == null)
            {
                BindPrefabRuntime();
                return;
            }
            if (!forceReload &&
                _spine.Data != null &&
                _spine.HasAnimation(ANIM_IDLE))
                return;

            _spine.Load(GAMEPLAY_SPINE_MODULE);
            SetHardFrame(_hardFrame);
        }

        void Update()
        {
            if (_reverseT < 0f) return;
            if (_reverseEntry == null || _spine == null || _spine.Current != _reverseEntry)
            {
                _reverseT = -1f;
                _reverseEntry = null;
                EmitExitFinished();
                return;
            }
            _reverseT -= Time.deltaTime * REVERSE_SPEED;
            if (_reverseT <= 0f)
            {
                _reverseEntry.TrackTime = 0f;
                _reverseT = -1f;
                _reverseEntry = null;
                EmitExitFinished();
                return;
            }
            _reverseEntry.TrackTime = _reverseT;
        }

        void OnAnimCompleted(SpineLite.TrackEntry entry)
        {
            string name = entry.Animation?.Name ?? "";
            if (_holdingFail && name == ANIM_FAILURE) return;
            if (_completeRunning && entry == _completeEntry)
            {
                EmitCompleteFinished();
                return;
            }
            if (_waitingReturnToIdle) PlayIdle();
        }

        void EmitExitFinished()
        {
            if (_exitDone) return;
            _exitDone = true;
            _exitRunning = false;
            ExitFinished?.Invoke();
        }

        void EmitCompleteFinished()
        {
            if (_completeDone) return;
            _completeDone = true;
            _completeRunning = false;
            _completeEntry = null;
            CompleteFinished?.Invoke();
        }

        void OnDestroy()
        {
            if (_spine != null)
                _spine.AnimationCompleted -= OnAnimCompleted;
        }
    }
}
