using System;
using System.Collections;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Exercises the live page and its standard Button callbacks; does not edit save data.
    public static class BizzaSettingsInspection
    {
        const double ObservationSeconds = 20d;
        const double SampleSeconds = 0.25d;
        static bool running;

        public static void Open(string folder)
        {
            RequireAvailable();
            var before = ReadState();
            if (!before.Ready || before.PauseExists || UIModule.Instance.Opening)
                throw new InvalidOperationException("Gameplay must be ready and the settings page closed.");

            string prefix = Prepare(folder, "open", before);
            running = true;
            try
            {
                UIModule.Instance.OpenPage(UIPageIds.PausePanel).Forget(exception =>
                {
                    File.AppendAllText(prefix + "-result.txt", "\nopenException=" + exception);
                    Debug.LogException(exception);
                });
                // UIModule survives a round reset; do not attach diagnostics to the closing page.
                UIModule.Instance.StartCoroutine(Guard(Observe(prefix, "open", before.Round), prefix));
            }
            catch (Exception exception)
            {
                running = false;
                File.AppendAllText(prefix + "-result.txt", "\nFAIL\n" + exception);
                throw;
            }
        }

        public static void Click(string folder, string action)
        {
            RequireAvailable();
            if (action != "retry" && action != "continue" && action != "close")
                throw new ArgumentException("Settings action must be retry, continue or close.", nameof(action));
            var rootPanel = UIModule.Instance.GetPage<PausePanel>();
            if (rootPanel == null || rootPanel.IsClosing || !rootPanel.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The live settings page must be open.");
            BizzaButton legacy = action == "retry" ? rootPanel.BackButton :
                action == "continue" ? rootPanel.ContinueButton : rootPanel.CloseButton;
            Button button = legacy != null ? legacy.GetComponent<Button>() : null;
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable())
                throw new InvalidOperationException("The requested standard settings Button is unavailable or disabled.");

            var before = ReadState();
            string prefix = Prepare(folder, action, before);
            running = true;
            try
            {
                // WithdrawCloudButton.Awake wires this event to the serialized BizzaButton owner.
                button.onClick.Invoke();
                AppendState(prefix, "afterClick", ReadState(), 0d);
                UIModule.Instance.StartCoroutine(Guard(Observe(prefix, action, before.Round), prefix));
            }
            catch (Exception exception)
            {
                running = false;
                File.AppendAllText(prefix + "-result.txt", "\nFAIL\n" + exception);
                throw;
            }
        }

        public static void Inspect(string folder)
        {
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            report.AppendLine("utc=" + DateTime.UtcNow.ToString("O"));
            report.AppendLine("observationRunning=" + running);
            AppendState(report, "current", ReadState(), 0d);
            if (CanInspectUi())
            {
                var rootPanel = UIModule.Instance.GetPage<PausePanel>();
                if (rootPanel != null)
                {
                    AppendButton(report, "retry", rootPanel.BackButton);
                    AppendButton(report, "continue", rootPanel.ContinueButton);
                    AppendButton(report, "close", rootPanel.CloseButton);
                }
            }
            File.WriteAllText(Path.Combine(folder, "settings-state.txt"), report.ToString());
        }

        static void RequireAvailable()
        {
            if (running) throw new InvalidOperationException("A settings observation is already running.");
            if (!EditorApplication.isPlaying || UIModule.Instance == null ||
                BizzaGameplayBridge.Page == null || World.Current == null)
                throw new InvalidOperationException("Enter Play Mode and finish gameplay initialization first.");
        }

        static string Prepare(string folder, string action, State before)
        {
            Directory.CreateDirectory(folder);
            string prefix = Path.Combine(folder, "settings-" + action);
            var report = new StringBuilder("RUNNING action=" + action + " utc=" + DateTime.UtcNow.ToString("O") + "\n");
            AppendState(report, "before", before, 0d);
            File.WriteAllText(prefix + "-result.txt", report.ToString());
            return prefix;
        }

        static IEnumerator Observe(string prefix, string action, int beforeRound)
        {
            double start = Time.realtimeSinceStartupAsDouble;
            double nextSample = start;
            double stableSince = -1d;
            bool pass = false;
            while (EditorApplication.isPlaying && Time.realtimeSinceStartupAsDouble - start < ObservationSeconds)
            {
                double now = Time.realtimeSinceStartupAsDouble;
                if (now >= nextSample)
                {
                    nextSample = now + SampleSeconds;
                    State state = ReadState();
                    AppendState(prefix, "sample", state, now - start);
                    bool expected = action == "open"
                        ? state.PauseExists && state.PauseActive && !state.PauseClosing && !state.UiOpening
                        : !state.PauseExists && state.Ready &&
                            (action == "retry" ? state.Round > beforeRound : state.Round == beforeRound);
                    if (expected)
                    {
                        if (stableSince < 0d) stableSince = now;
                        if (now - stableSince >= 0.5d) { pass = true; break; }
                    }
                    else stableSince = -1d;
                }
                yield return null;
            }

            State after = ReadState();
            AppendState(prefix, "after", after, Time.realtimeSinceStartupAsDouble - start);
            File.AppendAllText(prefix + "-result.txt", "\n" + (pass ? "PASS" : "FAIL") +
                " action=" + action + " beforeRound=" + beforeRound + " afterRound=" + after.Round +
                " expected=" + (action == "open" ? "live settings page visible" :
                    action == "retry" ? "round increased, settings closed and gameplay ready" :
                    "round unchanged, settings closed and gameplay ready") + "\n");
            Inspect(Path.GetDirectoryName(prefix));
            if (EditorApplication.isPlaying)
            {
                Canvas.ForceUpdateCanvases();
                ScreenCapture.CaptureScreenshot(prefix + ".png");
                yield return null; // Let the queued screenshot finish before the next command.
            }
        }

        static IEnumerator Guard(IEnumerator observation, string prefix)
        {
            bool completed = false;
            try
            {
                while (true)
                {
                    bool next = false;
                    Exception failure = null;
                    try { next = observation.MoveNext(); }
                    catch (Exception exception) { failure = exception; }
                    if (failure != null)
                    {
                        File.AppendAllText(prefix + "-result.txt", "\nFAIL\n" + failure);
                        Debug.LogException(failure);
                        completed = true;
                        yield break;
                    }
                    if (!next) { completed = true; yield break; }
                    yield return observation.Current;
                }
            }
            finally
            {
                (observation as IDisposable)?.Dispose();
                running = false;
                if (!completed) File.AppendAllText(prefix + "-result.txt", "\nFAIL observation interrupted before completion.\n");
            }
        }

        struct State
        {
            public bool Playing, PageExists, PageActive, InputEnabled, WorldExists, WorldPaused;
            public bool BridgeLoading, BridgeBlocked, TransparentBlocked, LoadingBlocked, TransitionBlocked;
            public bool PauseExists, PauseActive, PauseClosing, UiOpening;
            public bool Dead, Won, ToolBusy, MergeBusy, Flying, SpawnInFlight;
            public int Round, Level, TotalBubbles, AliveBubbles, LandedBubbles;
            public float TimeScale;
            public bool Ready => Playing && PageExists && PageActive && InputEnabled && WorldExists &&
                !WorldPaused && !BridgeLoading && !BridgeBlocked && !Dead && !Won && !ToolBusy && !MergeBusy && !Flying;
        }

        static State ReadState()
        {
            var state = new State { Playing = EditorApplication.isPlaying, Round = -1, Level = -1, TimeScale = Time.timeScale };
            if (!state.Playing) return state;
            state.WorldExists = World.Current != null;
            state.WorldPaused = state.WorldExists && World.Current.IsPause;
            state.BridgeLoading = BizzaGameplayBridge.IsLoadingLevel;
            state.BridgeBlocked = BizzaGameplayBridge.IsInputBlocked;
            state.TransparentBlocked = TransparentBlock.IsBlock;
            state.LoadingBlocked = LoadingBlock.IsBlock;
            state.TransitionBlocked = TransitionBlock.Instance != null && TransitionBlock.Instance._playingAnim;
            if (CanInspectUi())
            {
                state.UiOpening = UIModule.Instance.Opening;
                var rootPanel = UIModule.Instance.GetPage<PausePanel>();
                state.PauseExists = rootPanel != null;
                state.PauseActive = rootPanel != null && rootPanel.gameObject.activeInHierarchy;
                state.PauseClosing = rootPanel != null && rootPanel.IsClosing;
            }
            var page = BizzaGameplayBridge.Page;
            state.PageExists = page != null;
            if (page == null) return state;
            state.PageActive = page.isActiveAndEnabled;
            state.Round = page.RoundSeq;
            state.Level = page.CurrentLevelNumber;
            state.InputEnabled = page.Input != null && page.Input.InputEnabled;
            state.Dead = page.IsDead();
            state.Won = page.IsWon();
            state.ToolBusy = page.IsToolBusy();
            state.MergeBusy = page.Merge != null && page.Merge.IsBusy;
            state.Flying = page.Fly != null && page.Fly.IsFlying;
            if (page.Field != null)
            {
                state.SpawnInFlight = page.Field.IsSpawnInFlight;
                foreach (BubbleView bubble in page.Field.AllBubbles())
                {
                    if (bubble == null) continue;
                    state.TotalBubbles++;
                    if (bubble.State != BubbleState.Alive) continue;
                    state.AliveBubbles++;
                    if (bubble.HasLanded) state.LandedBubbles++;
                }
            }
            return state;
        }

        static bool CanInspectUi() => EditorApplication.isPlaying && UIModule.Instance != null &&
            World.Current != null && World.Current.CurGameMode != null;

        static void AppendState(string prefix, string label, State state, double elapsed)
        {
            var report = new StringBuilder();
            AppendState(report, label, state, elapsed);
            File.AppendAllText(prefix + "-result.txt", report.ToString());
        }

        static void AppendState(StringBuilder report, string label, State state, double elapsed)
        {
            report.Append(label).Append(" elapsed=").Append(elapsed.ToString("F2"))
                .Append(" playing=").Append(state.Playing).Append(" page=").Append(state.PageExists)
                .Append(" active=").Append(state.PageActive).Append(" level=").Append(state.Level)
                .Append(" round=").Append(state.Round).Append(" InputEnabled=").Append(state.InputEnabled)
                .Append(" WorldExists=").Append(state.WorldExists).Append(" World.IsPause=").Append(state.WorldPaused)
                .Append(" bridgeLoading=").Append(state.BridgeLoading).Append(" bridgeBlocked=").Append(state.BridgeBlocked)
                .Append(" blocks[transparent/loading/transition]=").Append(state.TransparentBlocked).Append('/')
                .Append(state.LoadingBlocked).Append('/').Append(state.TransitionBlocked)
                .Append(" pauseExists=").Append(state.PauseExists).Append(" pauseActive=").Append(state.PauseActive)
                .Append(" pauseIsClosing=").Append(state.PauseClosing).Append(" uiOpening=").Append(state.UiOpening)
                .Append(" dead=").Append(state.Dead).Append(" won=").Append(state.Won).Append(" toolBusy=").Append(state.ToolBusy)
                .Append(" mergeBusy=").Append(state.MergeBusy).Append(" flying=").Append(state.Flying)
                .Append(" spawnInFlight=").Append(state.SpawnInFlight).Append(" bubbles[total/alive/landed]=")
                .Append(state.TotalBubbles).Append('/').Append(state.AliveBubbles).Append('/').Append(state.LandedBubbles)
                .Append(" Time.timeScale=").Append(state.TimeScale).Append(" ready=").Append(state.Ready).AppendLine();
        }

        static void AppendButton(StringBuilder report, string action, BizzaButton legacy)
        {
            Button button = legacy != null ? legacy.GetComponent<Button>() : null;
            Image face = button != null ? button.targetGraphic as Image : null;
            report.Append("button=").Append(action).Append(" exists=").Append(button != null)
                .Append(" active=").Append(button != null && button.isActiveAndEnabled)
                .Append(" legacyInteractable=").Append(legacy != null && legacy.interactable)
                .Append(" effectiveInteractable=").Append(button != null && button.IsInteractable())
                .Append(" sprite=").Append(face != null && face.sprite != null ? face.sprite.name : "missing")
                .AppendLine();
        }
    }
}
