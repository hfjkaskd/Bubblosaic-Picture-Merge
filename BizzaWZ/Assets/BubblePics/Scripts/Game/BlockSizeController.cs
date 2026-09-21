using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Port of bubble_block_size_plugin.gd.
    ///
    /// BubblePage currently exposes round-start but not its bubbles-dropped or
    /// merge-resolved bus signals. Round-start is subscribed directly,
    /// spawn-batch completion is observed in LateUpdate, and MergeRunner calls
    /// RecomputeAndApply when its merge-resolved event has been dispatched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlockSizeController : MonoBehaviour
    {
        public const float GROW_DURATION = 0.25f;
        public const float SETTLED_SPEED = 60f;

        BubblePage _page;
        bool _roundSubscribed;
        bool _observed;
        bool _queuedGameStarted;
        bool _wasSpawnInFlight;
        int _lastRoundSeq;
        int _lastPendingTokens;

        public void Initialize(BubblePage page)
        {
            if (_page == page)
            {
                SubscribeRoundStarted();
                return;
            }

            UnsubscribeRoundStarted();
            _page = page;
            _observed = false;
            _queuedGameStarted = true;
            SubscribeRoundStarted();
        }

        void OnEnable()
        {
            TryBindPage();
        }

        void Start()
        {
            TryBindPage();
        }

        void OnDisable()
        {
            UnsubscribeRoundStarted();
        }

        void TryBindPage()
        {
            if (_page == null)
                _page = GetComponent<BubblePage>();
            SubscribeRoundStarted();
        }

        void SubscribeRoundStarted()
        {
            if (_roundSubscribed || _page == null) return;
            _page.RoundStartedEvent += OnGameStarted;
            _roundSubscribed = true;
        }

        void UnsubscribeRoundStarted()
        {
            if (!_roundSubscribed) return;
            if (_page != null)
                _page.RoundStartedEvent -= OnGameStarted;
            _roundSubscribed = false;
        }

        void OnGameStarted()
        {
            // Unity's opening wave may be spawned by a coroutine immediately
            // after RoundStartedEvent. Apply once the batch has completed, as
            // the original game_started signal is emitted after that drop.
            _queuedGameStarted = true;
        }

        void LateUpdate()
        {
            TryBindPage();
            if (_page == null || _page.Field == null || _page.Scheduler == null)
                return;

            bool spawning = _page.Field.IsSpawnInFlight;
            int roundSeq = _page.RoundSeq;
            int pendingTokens = _page.Scheduler.TotalTokenCount();

            if (!_observed)
            {
                _observed = true;
                _lastRoundSeq = roundSeq;
                _lastPendingTokens = pendingTokens;
                _wasSpawnInFlight = spawning;
                if (roundSeq > 0) _queuedGameStarted = true;
            }
            else if (roundSeq != _lastRoundSeq)
            {
                _lastRoundSeq = roundSeq;
                _queuedGameStarted = true;
            }

            bool spawnBatchCompleted = _wasSpawnInFlight && !spawning;
            bool synchronousDropCompleted =
                !_wasSpawnInFlight && !spawning &&
                pendingTokens < _lastPendingTokens;

            if (_queuedGameStarted && !spawning)
            {
                _queuedGameStarted = false;
                RecomputeAndApply("game_started");
            }
            else if (spawnBatchCompleted || synchronousDropCompleted)
            {
                RecomputeAndApply("bubbles_dropped");
            }

            _lastPendingTokens = pendingTokens;
            _wasSpawnInFlight = spawning;
        }

        public void RecomputeAndApply(string source)
        {
            if (_page == null)
                Initialize(GetComponent<BubblePage>());
            if (_page == null || _page.Field == null || _page.Scheduler == null)
                return;

            int peak = ComputePeakSlots();
            float mult = MultForPeak(peak);
            ApplyToBoard(mult, mult);
        }

        int ComputePeakSlots()
        {
            int onBoard = _page.Field.CountBoardBubbleSlots();
            int peak = onBoard;
            foreach (int size in _page.Scheduler.WaveSizes())
            {
                onBoard += size;
                if (onBoard > peak) peak = onBoard;
                onBoard = Mathf.Max(0, onBoard - 4);
            }

            peak += _page.Scheduler.PendingGroupCount();
            return peak;
        }

        static float MultForPeak(int peak)
        {
            if (peak <= 15) return 1.2f;
            if (peak <= 18) return 1.15f;
            if (peak <= 22) return 1.1f;
            return 1f;
        }

        void ApplyToBoard(float mult2, float mult3)
        {
            float fieldBaseRadius = _page.Field.BaseRadius;
            List<BubbleView> bubbles = _page.Field.AllBubbles();
            bool anyGrew = false;
            foreach (BubbleView bubble in bubbles)
            {
                if (bubble == null || bubble.Fragment == null) continue;
                if (bubble.Fragment.HeldPaths.Count < 2) continue;
                float mult = SelectMult(bubble, mult2, mult3);
                if (bubble.ApplyBlockSizeAbsSmooth(
                    fieldBaseRadius, mult, GROW_DURATION))
                    anyGrew = true;
            }

            if (!anyGrew) return;
            foreach (BubbleView bubble in _page.Field.AllBubbles())
            {
                if (bubble == null || bubble.State != BubbleState.Alive ||
                    bubble.Body == null)
                    continue;
                if (bubble.Body.velocity.magnitude < SETTLED_SPEED)
                    bubble.ApplySoftPushSpeedCap(GROW_DURATION);
            }
        }

        static float SelectMult(BubbleView bubble, float mult2, float mult3)
        {
            int held = bubble.Fragment.HeldPaths.Count;
            if (held == 2)
            {
                List<int> quads = bubble.Fragment.LastQuadrants();
                if (quads.Count == 2)
                {
                    quads.Sort();
                    bool diagonal =
                        (quads[0] == 0 && quads[1] == 3) ||
                        (quads[0] == 1 && quads[1] == 2);
                    if (diagonal) return mult3;
                }
                return mult2;
            }

            return held == 3 ? mult3 : 1f;
        }
    }
}
