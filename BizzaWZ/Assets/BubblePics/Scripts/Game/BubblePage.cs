using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BubblePics.GameModes;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Port of bubble_page.gd + award/death flows + combo/auto-link plugins.
    /// </summary>
    public class BubblePage : MonoBehaviour
    {
        public const int MOVES_UNLOCK_LEVEL = 2;
        public const int STEP_SAFETY_BUFFER = 5;
        const float SPAWN_INTERVAL_SEC = 0.05f;
        public const int COIN_PER_STEP = 5;
        const float SETTLE_DELAY_BEFORE_BARS_SLIDE_SEC = 1.25f;

        public BubbleField Field { get; private set; }
        public WaveScheduler Scheduler { get; private set; }
        public MergeRunner Merge { get; private set; }
        public InputController Input { get; private set; }
        public ImageFlyAnimator Fly { get; private set; }
        public BackdropController Backdrop { get; private set; }
        public CompletionFx Completion { get; private set; }
        public TopGameBar TopBar { get; private set; }
        public ToolbarView Toolbar { get; private set; }
        public ComboOverlay Combo { get; private set; }
        public CompletePanel CompletePanel { get; private set; }
        public ContinuePanel ContinuePanel { get; private set; }
        public BombRevivalPanel BombRevivalPanel { get; private set; }
        public BombExitConfirmPanel BombExitConfirmPanel { get; private set; }
        public HardLevelBanner HardBanner { get; private set; }
        public Tutorial Tutorial { get; private set; }
        public MovesIntroPanel MovesIntro { get; private set; }
        public ToolEffects Tools { get; private set; }
        public BubbleSpecialMechanics Specials { get; private set; }

        public LevelData Level { get; private set; }
        public Texture2D[] PickedTextures { get; private set; } = new Texture2D[0];
        public List<int> CollectedImgs { get; } = new List<int>();
        public int RoundSeq { get; private set; }
        public int CurrentLevelNumber { get; private set; } = 1;
        public bool GmUnlimitedMoves { get; private set; }

        readonly StepLimitRule _stepRule = new StepLimitRule();
        public StepLimitRule StepRule => _stepRule;
        readonly ComboCounter _comboCounter = new ComboCounter();
        float _roundStartedRealtime;
        string _roundStartSource = "start";
        WaterWaveBand _waveBand;

        bool _dead;
        bool _bombDeath;
        bool _persisted, _awarding;
        int _pendingReward;
        bool _toolBusy;
        bool _deferNextWave;
        bool _firstRevivePending;
        int _continueAdCount;
        int _autoLinkCount;
        int _roundLinkSteps;
        bool _roundCoefficientRecorded;
        bool _roundTelemetryActive;
        bool _playtimeReported;
        bool _firstActionTracked;
        bool _standardEndTracked;
        bool _standardNextBehaviorTracked;
        int _successfulMoveCount;
        int _progressMoveMask;
        int _roundFailCount;
        int _reviveCount;
        int _hintUseCount;
        int _dropUseCount;
        int _magnetUseCount;
        BubbleView _pendingAutoLinkTarget;
        Coroutine _restartHighlightCo;
        GameObject _worldPrefabInstance;
        Texture2D[] _preparedTextures;
        Coroutine _openLevelCo;
        int _openLevelGeneration;
        bool _tryRestoreSnapshot;
        float _gmBubblesPerRow;

        // ------------------------------------------------------------ build
        public void BuildWarm(App app)
        {
            var prefabs = app != null ? app.Prefabs : null;
            if (prefabs != null && prefabs.BubbleWorld != null &&
                prefabs.TopGameBar != null && prefabs.BubbleToolbar != null)
            {
                InitializePrefabRuntime(app);
                return;
            }

            BuildLegacy(app);
        }

        /// <summary>
        /// Composes the authored BubblePage prefab from the same fixed scene
        /// parts referenced by the original Godot bubble_page.tscn. Per-level
        /// bubble counts, fragments and positions remain runtime data.
        /// </summary>
        public void InitializePrefabRuntime(App app)
        {
            if (app == null) return;
            var catalog = app.Prefabs;
            if (catalog == null)
            {
                BuildLegacy(app);
                return;
            }

            transform.SetParent(app.transform, false);

            if (_worldPrefabInstance == null && catalog.BubbleWorld != null)
            {
                _worldPrefabInstance = Instantiate(catalog.BubbleWorld, app.WorldRoot, false);
                _worldPrefabInstance.name = "BubbleWorld";
            }
            if (_worldPrefabInstance != null)
            {
                Field = _worldPrefabInstance.GetComponent<BubbleField>();
                Backdrop = _worldPrefabInstance.GetComponent<BackdropController>();
            }
            if (Field == null) Field = GetComponent<BubbleField>();
            if (Backdrop == null) Backdrop = GetComponent<BackdropController>();
            Field?.InitializePrefabRuntime(app.WorldRoot);
            if (Backdrop != null)
            {
                Backdrop.InitializePrefabRuntime(this, app.WorldRoot);
                Backdrop.Ambient?.Setup(
                    BubbleField.ViewW,
                    Field != null ? Field.FloorY : 2097f);
                Backdrop.MidDepth?.Setup(
                    BubbleField.ViewW,
                    BubbleField.ViewH);
            }

            Scheduler = new WaveScheduler();
            Merge = GetOrAddController<MergeRunner>();
            Merge.Page = this;
            Input = GetOrAddController<InputController>();
            Input.Page = this;
            Fly = GetOrAddController<ImageFlyAnimator>();
            Fly.Page = this;
            Tools = GetOrAddController<ToolEffects>();
            Tools.Page = this;
            Specials = GetOrAddController<BubbleSpecialMechanics>();
            Specials.Initialize(this);
            Tutorial = GetOrAddController<Tutorial>();
            Tutorial.Page = this;

            TopBar = InstantiateMounted<TopGameBar>(catalog.TopGameBar, app);
            TopBar?.InitializePrefabRuntime(app.HudRoot);
            TopBar?.InitDolphin();

            Toolbar = InstantiateMounted<ToolbarView>(catalog.BubbleToolbar, app);
            Toolbar?.InitializePrefabRuntime(app.HudRoot, this);

            Combo = InstantiateMounted<ComboOverlay>(catalog.ComboOverlay, app);
            Combo?.InitializePrefabRuntime(app.WorldRoot);

            Completion = GetOrAddController<CompletionFx>();
            Completion.InitializePrefabRuntime(this, app.WorldRoot);
            Completion.Setup();
            Completion.CompleteReveal -= OnCompleteReveal;
            Completion.CompleteReveal += OnCompleteReveal;

            _waveBand = InstantiateMounted<WaterWaveBand>(catalog.CompletionWaveBand, app);
            if (_waveBand != null)
            {
                _waveBand.InitializePrefabRuntime(135);
                _waveBand.gameObject.SetActive(false);
            }

            CompletePanel = InstantiateMounted<CompletePanel>(catalog.CompletePanel, app);
            CompletePanel?.InitializePrefabRuntime(this, app.PanelRoot);
            CompletePanel?.HidePanel();

            ContinuePanel = InstantiateMounted<ContinuePanel>(catalog.ContinuePanel, app);
            ContinuePanel?.InitializePrefabRuntime(this, app.DialogRoot);
            ContinuePanel?.HidePanel();

            BombRevivalPanel = InstantiateMounted<BombRevivalPanel>(
                catalog.BombRevivalPanel,
                app);
            BombRevivalPanel?.InitializePrefabRuntime(this, app.DialogRoot);

            BombExitConfirmPanel = InstantiateMounted<BombExitConfirmPanel>(
                catalog.BombExitConfirmPanel,
                app);
            BombExitConfirmPanel?.InitializePrefabRuntime(this, app.DialogRoot);

            MovesIntro = InstantiateMounted<MovesIntroPanel>(catalog.MovesIntroPanel, app);
            MovesIntro?.InitializePrefabRuntime(this, app.DialogRoot);

            HardBanner = InstantiateMounted<HardLevelBanner>(catalog.HardLevelBanner, app);
            HardBanner?.InitializePrefabRuntime(app.DialogRoot);

            _stepRule.StepsChanged -= OnStepsChanged;
            _stepRule.StepsChanged += OnStepsChanged;
        }

        void BuildLegacy(App app)
        {
            Field = gameObject.AddComponent<BubbleField>();
            Field.Init(app.WorldRoot);

            Backdrop = gameObject.AddComponent<BackdropController>();
            Backdrop.Page = this;
            Backdrop.Build(app.WorldRoot);
            Backdrop.Ambient?.Setup(BubbleField.ViewW, Field.FloorY);
            Backdrop.MidDepth?.Setup(BubbleField.ViewW, BubbleField.ViewH);

            Scheduler = new WaveScheduler();

            Merge = gameObject.AddComponent<MergeRunner>();
            Merge.Page = this;

            Input = gameObject.AddComponent<InputController>();
            Input.Page = this;

            Fly = gameObject.AddComponent<ImageFlyAnimator>();
            Fly.Page = this;

            TopBar = gameObject.AddComponent<TopGameBar>();
            TopBar.Build(app.HudRoot);
            TopBar.InitDolphin();

            Toolbar = gameObject.AddComponent<ToolbarView>();
            Toolbar.Page = this;
            Toolbar.Build(app.HudRoot);

            Tools = gameObject.AddComponent<ToolEffects>();
            Tools.Page = this;
            Specials = gameObject.AddComponent<BubbleSpecialMechanics>();
            Specials.Initialize(this);

            var comboGo = new GameObject("ComboOverlay");
            Combo = comboGo.AddComponent<ComboOverlay>();
            Combo.Build(app.WorldRoot);

            Completion = gameObject.AddComponent<CompletionFx>();
            Completion.Page = this;
            Completion.Setup();
            Completion.CompleteReveal += () => Backdrop.OnCompleteReveal();

            var wbGo = new GameObject("CompletionWaveBand");
            _waveBand = wbGo.AddComponent<WaterWaveBand>();
            _waveBand.Build(app.WorldRoot, 135);
            wbGo.SetActive(false);

            CompletePanel = gameObject.AddComponent<CompletePanel>();
            CompletePanel.Page = this;
            CompletePanel.Build(app.PanelRoot);

            ContinuePanel = gameObject.AddComponent<ContinuePanel>();
            ContinuePanel.Page = this;
            ContinuePanel.Build(app.DialogRoot);

            BombRevivalPanel = gameObject.AddComponent<BombRevivalPanel>();
            BombRevivalPanel.Page = this;
            BombRevivalPanel.Build(app.DialogRoot);

            BombExitConfirmPanel = gameObject.AddComponent<BombExitConfirmPanel>();
            BombExitConfirmPanel.Page = this;
            BombExitConfirmPanel.Build(app.DialogRoot);

            Tutorial = gameObject.AddComponent<Tutorial>();
            Tutorial.Page = this;

            MovesIntro = gameObject.AddComponent<MovesIntroPanel>();
            MovesIntro.Page = this;
            MovesIntro.Build(app.DialogRoot);

            HardBanner = gameObject.AddComponent<HardLevelBanner>();
            HardBanner.Build(app.DialogRoot);

            _stepRule.StepsChanged -= OnStepsChanged;
            _stepRule.StepsChanged += OnStepsChanged;
        }

        T GetOrAddController<T>() where T : Component
        {
            var component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        T InstantiateMounted<T>(GameObject prefab, App app) where T : Component
        {
            var component = PrefabCatalog.InstantiateComponent<T>(prefab, transform);
            if (component == null) return null;
            var mounts = component.GetComponentInParent<PrefabMountSet>(true);
            if (mounts == null)
                mounts = component.GetComponentInChildren<PrefabMountSet>(true);
            mounts?.Attach(app);
            return component;
        }

        void OnCompleteReveal()
        {
            Backdrop?.OnCompleteReveal();
        }

        void OnStepsChanged(int steps)
        {
            if (TopBar == null) return;
            if (IsMovesUnlimited()) TopBar.SetMovesUnlimited();
            else TopBar.SetMoves(steps);
        }

        public void SetVisible(bool v)
        {
            if (TopBar != null && TopBar.Root != null)
                TopBar.Root.gameObject.SetActive(v);
            if (Toolbar != null && Toolbar.Root != null)
                Toolbar.Root.gameObject.SetActive(v);
            if (App.I != null && App.I.WorldRoot != null)
                App.I.WorldRoot.gameObject.SetActive(v);
        }

        public void ShowCompletionWaveBand(float fadeSec)
        {
            if (_waveBand == null) return;
            _waveBand.gameObject.SetActive(true);
            _waveBand.SetAlpha(0);
            StartCoroutine(Tween.Run(fadeSec, k => _waveBand.SetAlpha(k), Ease.OutSine));
        }

        public void HideCompletionWaveBand()
        {
            if (_waveBand != null) _waveBand.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ level open
        public void OpenLevel(int levelNumber, string bgEntry, float entryMaskAlpha)
        {
            if (ModeLevelEntry.TryOpenAutomatic(
                    this,
                    levelNumber,
                    bgEntry,
                    entryMaskAlpha))
                return;
            OpenMainLevel(levelNumber, bgEntry, entryMaskAlpha);
        }

        /// <summary>
        /// Opens the unmodified picture mode without running alternate mode
        /// arbitration. Used as the fail-safe when an AB-selected asset cannot
        /// be validated or downloaded.
        /// </summary>
        public void OpenMainLevel(
            int levelNumber,
            string bgEntry,
            float entryMaskAlpha)
        {
            if (!LevelRepo.TryGet(levelNumber, out var level))
            {
                Debug.LogWarning($"Level {levelNumber} is beyond the bundled content.");
                SetVisible(false);
                BizzaGameplayBridge.OnLoadFailed();
                LevelOpenCompleted?.Invoke(levelNumber, false);
                return;
            }

            _openLevelGeneration++;
            if (_openLevelCo != null) StopCoroutine(_openLevelCo);
            _openLevelCo = StartCoroutine(
                OpenLevelCo(_openLevelGeneration, levelNumber, level, bgEntry, entryMaskAlpha));
        }

        /// <summary>
        /// Receives a fully validated special-mode level. Category/Word/Tangram
        /// source tokens have already been compiled into numeric Bubble tokens,
        /// so BubbleFragment remains strict and unchanged.
        /// </summary>
        public void OpenPreparedModeLevel(
            LevelModeSelection selection,
            ModePreparedLevel prepared,
            string bgEntry,
            float entryMaskAlpha)
        {
            if (selection == null || prepared?.Level == null ||
                prepared.Textures == null)
                return;
            _openLevelGeneration++;
            if (_openLevelCo != null) StopCoroutine(_openLevelCo);
            _openLevelCo = StartCoroutine(OpenPreparedModeLevelCo(
                _openLevelGeneration,
                selection,
                prepared,
                bgEntry,
                entryMaskAlpha));
        }

        IEnumerator OpenPreparedModeLevelCo(
            int generation,
            LevelModeSelection selection,
            ModePreparedLevel prepared,
            string bgEntry,
            float entryMaskAlpha)
        {
            if (selection.Kind != GameplayKind.NumberMatch)
                NumberMatchMode.ClearActive();
            CurrentLevelNumber = selection.GlobalLevel;
            Level = GameplayDifficulty.Prepare(prepared.Level, CurrentLevelNumber);
            JigsawChipRule.EvaluateOnLevelEnter(Level);
            _preparedTextures = prepared.Textures;
            ModeSession.Activate(
                selection,
                prepared.Compiled,
                prepared.CategoryIcons);
            _tryRestoreSnapshot = bgEntry == "fresh";
            HardBanner?.HideImmediate();
            CompletePanel.HidePanel();
            ContinuePanel.HidePanel();
            BombRevivalPanel?.HidePanel();
            BombExitConfirmPanel?.HidePanel();
            Completion.Stop();
            _dead = false;

            bool preparedHard = string.Equals(
                GameplayDifficulty.ForLevel(CurrentLevelNumber).label,
                "Hard",
                System.StringComparison.OrdinalIgnoreCase);
            TopBar.SetLevelDecoration(
                preparedHard,
                selection.Kind == GameplayKind.Bonus);

            Backdrop.PlayShowTransition(bgEntry, entryMaskAlpha);
            Field.BuildWalls();
            TopBar.Dolphin.ResumeIdle();
            StartRound();
            Toolbar.Refresh();
            Backdrop.Ambient?.StartLayer();
            Backdrop.MidDepth?.StartLayer();

            yield return null;
            if (generation != _openLevelGeneration) yield break;
            Canvas.ForceUpdateCanvases();
            App.I?.HideLoading();
            LevelOpenCompleted?.Invoke(selection.GlobalLevel, true);
            _openLevelCo = null;
        }

        IEnumerator OpenLevelCo(
            int generation,
            int levelNumber,
            LevelData level,
            string bgEntry,
            float entryMaskAlpha)
        {
            float loadStartedRealtime = Time.realtimeSinceStartup;
            NumberMatchMode.ClearActive();
            LevelData catalogLevel = level;
            LevelData frozenLevel =
                ResolveFrozenLevelForRestore(levelNumber, bgEntry, catalogLevel);
            bool usingFrozenLevel = frozenLevel != null;
            if (usingFrozenLevel) level = frozenLevel;

            LoadingOverlay loading = null; // Framework owns the loading page.
            loading?.SetProgress(0f);

            void UpdateLoadingProgress(float progress)
            {
                if (generation == _openLevelGeneration)
                    loading?.SetProgress(progress);
            }

            Texture2D[] loaded = null;
            string error = null;
            yield return LevelImageLoader.Load(
                level,
                textures => loaded = textures,
                message => error = message,
                UpdateLoadingProgress);

            if (generation != _openLevelGeneration)
            {
                // A newer OpenLevel call owns the shared loading overlay now.
                // Do not hide it from this stale request.
                yield break;
            }

            if (usingFrozenLevel &&
                (loaded == null || !string.IsNullOrEmpty(error)))
            {
                Debug.LogWarning(
                    $"Frozen level data for level {levelNumber} could not be " +
                    $"loaded; discarding the snapshot and retrying catalog data: {error}");
                SaveState.RoundSnapshot = "";
                level = catalogLevel;
                loaded = null;
                error = null;
                loading?.SetProgress(0f);
                yield return LevelImageLoader.Load(
                    level,
                    textures => loaded = textures,
                    message => error = message,
                    UpdateLoadingProgress);
            }

            if (generation != _openLevelGeneration) yield break;

            if (loaded == null || !string.IsNullOrEmpty(error))
            {
                FunSmithTelemetry.TrackGameLoadMonitor(
                    level,
                    levelNumber,
                    bgEntry,
                    "fail",
                    Mathf.RoundToInt(
                        (Time.realtimeSinceStartup - loadStartedRealtime) *
                        1000f),
                    error);
                loading?.Hide();
                _openLevelCo = null;
                Debug.LogError($"Could not open level {levelNumber}: {error}");
                SetVisible(false);
                BizzaGameplayBridge.OnLoadFailed();
                Toast.Show(Localization.Tr("NET_ERROR_LOADING_FAILED"));
                LevelOpenCompleted?.Invoke(levelNumber, false);
                yield break;
            }

            ModeSession.Clear();
            FunSmithTelemetry.TrackGameLoadMonitor(
                level,
                levelNumber,
                bgEntry,
                "success",
                Mathf.RoundToInt(
                    (Time.realtimeSinceStartup - loadStartedRealtime) *
                    1000f),
                string.Empty);
            CurrentLevelNumber = levelNumber;
            Level = GameplayDifficulty.Prepare(level, CurrentLevelNumber);
            JigsawChipRule.EvaluateOnLevelEnter(Level);
            _preparedTextures = loaded;
            App.I?.RemoteImages?.NotifyForegroundItemReady(levelNumber);
            if (string.Equals(
                    bgEntry,
                    "next",
                    System.StringComparison.Ordinal))
            {
                // Original StageDataManager starts its advance prefetch and
                // cache prune only after the next level opened successfully.
                App.I?.RemoteImages?.NotifyItemAdvanced(levelNumber);
            }
            _tryRestoreSnapshot = bgEntry == "fresh";
            HardBanner?.HideImmediate();
            CompletePanel.HidePanel();
            ContinuePanel.HidePanel();
            BombRevivalPanel?.HidePanel();
            BombExitConfirmPanel?.HidePanel();
            Completion.Stop();
            _dead = false;
            bool isHardLevel = string.Equals(
                GameplayDifficulty.ForLevel(CurrentLevelNumber).label,
                "Hard",
                System.StringComparison.OrdinalIgnoreCase);
            TopBar.SetLevelDecoration(isHardLevel, false);

            // Godot emits level_entered before resolving/starting the round.
            // Show the one-shot hard banner at the equivalent point instead
            // of waiting until bubbles have already begun spawning.
            HardBanner?.MaybeShow(Level);

            Backdrop.PlayShowTransition(bgEntry, entryMaskAlpha);
            Field.BuildWalls();

            // Gameplay opens with the portrait already settled. The home-page
            // decoration keeps its own entrance animation; only the in-level
            // top-bar dolphin skips the upward admission motion.
            TopBar.Dolphin.ResumeIdle();

            StartRound();
            Toolbar.Refresh();
            Backdrop.Ambient?.StartLayer();
            Backdrop.MidDepth?.StartLayer();

            // Keep the original modal loader over the synchronous setup and
            // first settled wave. Hiding it immediately after the download
            // exposed a blank gameplay background while these objects were
            // still being built. One rendered frame also lets Canvas/TMP and
            // the freshly created bubble meshes submit before the reveal.
            yield return null;
            if (generation != _openLevelGeneration) yield break;
            Canvas.ForceUpdateCanvases();
            loading?.Hide();
            LevelOpenCompleted?.Invoke(levelNumber, true);
            _openLevelCo = null;
        }

        public bool IsLevel1() => Level != null && Level.chapter == 1 && Level.level == 1;
        public bool IsTutorialRound() => IsLevel1() && !SaveState.TutorialDone;
        public bool IsMovesUnlimited() =>
            Level != null &&
            (GmUnlimitedMoves ||
             (AppConfig.LuckyBreak &&
              SaveState.IsLuckyBreakLevel(CurrentLevelNumber)) ||
             CurrentLevelNumber < AppConfig.MovesUnlockLevel);

        public int GetStepLimit()
        {
            if (Level == null) return 0;
            int baseSteps = AppConfig.ApplyInitialStep(Level.step_limit, CurrentLevelNumber);
            int minimum = MinCompletionSteps();
            int stickerCount = Specials != null ? Specials.StickerCount : 0;
            return GameplayDifficulty.StepLimit(CurrentLevelNumber, baseSteps, minimum,
                stickerCount, AppConfig.StepUseErrorOnly);
        }

        int MinCompletionSteps()
        {
            int total = Scheduler.CountTotalFragments();
            int images = Scheduler.DistinctImageCount();
            return Mathf.Max(0, total - images);
        }

        public int MinCompletionStepCount() => MinCompletionSteps();

        public int GetAddStep() => Level?.add_step ?? 0;
        public int CountTotalFragments() => Scheduler.CountTotalFragments();
        public int CountRemainingBubbles()
        {
            int board = Field != null ? Field.CountBoardBubbles() : 0;
            int pending = Scheduler != null
                ? Scheduler.CountTotalFragments()
                : 0;
            return Mathf.Max(0, board + pending);
        }
        public int CurrentRoundElapsedSeconds => Mathf.Max(
            0,
            Mathf.RoundToInt(
                Time.realtimeSinceStartup - _roundStartedRealtime) -
            FunSmithTelemetry.CurrentRoundAdSeconds);
        public int StepsLeft => _stepRule.StepsLeft;

        float ComputeDynamicPerRow()
        {
            if (_gmBubblesPerRow > 0f)
                return _gmBubblesPerRow;

            var sizes = Scheduler.WaveSizes();
            int onBoard = 0, peak = 0;
            for (int i = 0; i < sizes.Count; i++)
            {
                onBoard += sizes[i];
                if (onBoard > peak) peak = onBoard;
                if (i < sizes.Count - 1) onBoard -= 4;
            }
            return BubbleLayoutMath.ComputeDynamicPerRow(
                DeviceLayout.Current,
                peak,
                AppConfig.GameUi2);
        }

        public void HandleDeviceLayoutChanged()
        {
            if (Field == null) return;

            Field.ComputeLayout();
            if (Scheduler != null)
                Field.ApplyDynamicPerRow(ComputeDynamicPerRow());
            Field.BuildWalls();

            if (Backdrop != null)
            {
                Backdrop.Ambient?.Setup(BubbleField.ViewW, Field.FloorY);
                Backdrop.MidDepth?.Setup(
                    BubbleField.ViewW,
                    BubbleField.ViewH);
            }
            TopBar?.ApplyDeviceLayout();
            Toolbar?.ApplyDeviceLayout();
        }

        // ------------------------------------------------------------ round
        void ClearRound(string tutorialCancelReason = "level_replaced")
        {
            BizzaGameplayBridge.OnRoundClearing();
            RoundSeq++;
            Specials?.CancelRoundEffects();
            _deferNextWave = false;
            _delayDropOwned.Clear();
            _pendingAutoLinkTarget = null;
            _closureTimes.Clear();
            _closureSteps.Clear();
            _lastWonderfulTime = -10f;
            _suppressComboOnce = false;
            _awardStarted = false;
            FusionDurationMult = 1f;
            Merge.ResetAll();
            _toolBusy = false;
            Input.ResetState();
            Tools.ClearHintHighlight();
            Tutorial.Cancel(tutorialCancelReason);
            MovesIntro?.Cancel();
            Field.ClearBoard();
            Fly.Cancel();
            _awarding = false;
        }

        public void StartRound(string source = "start")
        {
            ClearRound(source == "restart"
                ? "manual_replay"
                : "level_replaced");
            _roundStartedRealtime = Time.realtimeSinceStartup;
            _roundTelemetryActive = false;
            _playtimeReported = false;
            _firstActionTracked = false;
            _standardEndTracked = false;
            _standardNextBehaviorTracked = false;
            _successfulMoveCount = 0;
            _progressMoveMask = 0;
            _roundFailCount = 0;
            _reviveCount = 0;
            _hintUseCount = 0;
            _dropUseCount = 0;
            _magnetUseCount = 0;
            _dead = false;
            _bombDeath = false;
            _persisted = false;
            _pendingReward = 0;
            _autoLinkCount = 0;
            _continueAdCount = 0;
            _firstRevivePending = false;
            _roundLinkSteps = 0;
            _roundCoefficientRecorded = false;
            _comboCounter.Reset();
            Combo.HideNow();
            CollectedImgs.Clear();
            ContinuePanel.HidePanel();
            BombRevivalPanel?.HidePanel();
            BombExitConfirmPanel?.HidePanel();

            // OpenLevel prepares all bundled/cached/remote textures before a
            // round starts. Restart reuses the same validated array.
            PickedTextures = _preparedTextures ?? System.Array.Empty<Texture2D>();

            Scheduler.Build(Level.layout, PickedTextures.Length);
            Specials?.PrepareRound();
            Field.ApplyDynamicPerRow(ComputeDynamicPerRow());
            TopBar.SetupTarget(PickedTextures.Length);
            if (_tryRestoreSnapshot && TryRestoreRound())
            {
                _tryRestoreSnapshot = false;
                _roundStartSource = "continue_start";
                Specials?.OnRoundStarted(true);
                EmitRoundStarted();
                TrackRoundStarted();
                Toolbar.CheckUnlockPopupsDeferred();
                return;
            }
            _tryRestoreSnapshot = false;
            _roundStartSource = string.IsNullOrEmpty(source) ? "start" : source;

            _stepRule.OnRoundStart(this);
            Specials?.OnRoundStarted(false);
            TopBar.SetMovesVisible(true);
            if (IsMovesUnlimited()) TopBar.SetMovesUnlimited();

            EmitRoundStarted();
            TrackRoundStarted();
            ShowModeIntro();
            StartCoroutine(ConsumeNextWaveCo(true, true));

            // The framework EnterCustomTutorial node starts the base tutorial.
            MovesIntro.MaybeShow();
            Toolbar.CheckUnlockPopupsDeferred();
        }

        void ShowModeIntro()
        {
            switch (ModeSession.ActiveKind)
            {
                case GameplayKind.CategoryMatch:
                    MetaFlowPresenter.ShowBanner(MetaFlowId.CategoryMergeBanner);
                    break;
                case GameplayKind.WordMatch:
                    MetaFlowPresenter.ShowBanner(MetaFlowId.WordMergeBanner);
                    break;
                case GameplayKind.Tangram:
                    MetaFlowPresenter.ShowBanner(MetaFlowId.ShapePuzzleBanner);
                    break;
                case GameplayKind.Bonus:
                    MetaFlowPresenter.ShowBanner(MetaFlowId.BonusLevelBanner, 18);
                    break;
                case GameplayKind.NumberMatch:
                    MetaFlowPresenter.ShowBanner(MetaFlowId.NumberMatchBanner);
                    break;
            }
        }

        public void RestartLevel()
        {
            FlowModule.LoadGameLevel();
        }

        /// <summary>
        /// GM-only direct jump. The active round is invalidated immediately;
        /// OpenLevel then either starts from prepared local/cache textures or
        /// presents the normal level-loading overlay until they are ready.
        /// </summary>
        public void GmJumpToLevelImmediately(int levelNumber)
        {
            SaveState.RoundSnapshot = "";
            _tryRestoreSnapshot = false;
            ClearRound();
            Completion?.Stop();
            CompletePanel?.HidePanel();
            ContinuePanel?.HidePanel();
            BombRevivalPanel?.HidePanel();
            BombExitConfirmPanel?.HidePanel();
            HardBanner?.HideImmediate();
            Combo?.HideNow();

            // Publish the target immediately so GM status cannot keep showing
            // the previous level while CDN images are being prepared. Level
            // remains null until OpenLevelCo has a matching validated set.
            CurrentLevelNumber = levelNumber;
            Level = null;
            _preparedTextures = null;
            PickedTextures = System.Array.Empty<Texture2D>();
            _dead = true;
            _persisted = true;

            App.I?.HideLoading();
            SetVisible(true);
            OpenLevel(levelNumber, "gm", 0f);
        }

        void RestartLevelImmediate(bool recordAbandon = true)
        {
            if (recordAbandon)
            {
                RecordRoundCoefficient(true);
                TrackLevelNextBehaviorOnce(4, 2);
                TrackRoundExit("restart", false);
            }
            SaveState.RoundSnapshot = "";
            _tryRestoreSnapshot = false;
            Field.ApplyDynamicPerRow(ComputeDynamicPerRow());
            Field.BuildWalls();
            TopBar.Dolphin.ResumeIdle();
            StartRound("restart");
        }

        /// <summary>GM restart that deliberately bypasses the ad gate.</summary>
        public void GmRestartImmediately()
        {
            if (Level == null) return;
            RestartLevelImmediate(false);
        }

        /// <summary>
        /// Mirrors the original win_round cheat: emit the real round-win state,
        /// clear the active board, then run the same completion FX, reward and
        /// completion-page sequence used after the final image lands.
        /// </summary>
        public bool GmCompleteLevel()
        {
            if (Level == null || _persisted ||
                CompletePanel == null || CompletePanel.Visible ||
                _awardStarted || _awarding || Fly.IsFlying)
                return false;

            // The recovered Godot cheat emits round_won before clearing the
            // board, so progress and rewards are committed exactly once. It
            // then enters the normal completion sequence instead of opening
            // the completion panel directly.
            FinalizeRound();
            ClearRound();
            _dead = false;
            _bombDeath = false;
            ContinuePanel?.HidePanel();
            BombRevivalPanel?.HidePanel();
            BombExitConfirmPanel?.HidePanel();
            HardBanner?.HideImmediate();
            Completion?.Stop();
            StartCoroutine(CheckWinCo());
            return true;
        }

        public string GmSetMoves(int moves)
        {
            if (Level == null) return "关卡尚未准备完成";
            if (IsMovesUnlimited())
                return "当前是无限步数模式，设置剩余步数不会生效";
            _stepRule.SetStepsLeft(Mathf.Max(0, moves));
            _dead = false;
            _bombDeath = false;
            ContinuePanel?.HidePanel();
            BombRevivalPanel?.HidePanel();
            BombExitConfirmPanel?.HidePanel();
            TopBar?.Dolphin?.ResumeIdle();
            return $"剩余步数已设为 {_stepRule.StepsLeft}";
        }

        public void GmSetUnlimitedMoves(bool enabled)
        {
            GmUnlimitedMoves = enabled;
            if (Level != null)
                RestartLevelImmediate();
        }

        public string GmSetBubblesPerRow(float perRow)
        {
            if (Level == null) return "关卡尚未准备完成";
            if (perRow < 2.5f || perRow > 9f)
                return "每行气泡数必须在 2.5～9 之间";
            _gmBubblesPerRow = perRow;
            RestartLevelImmediate();
            return $"每行气泡数 = {perRow:0.0}，已重开当前局";
        }

        public string GmLoadLayout(string layout)
        {
            if (Level == null) return "关卡尚未准备完成";
            if (string.IsNullOrWhiteSpace(layout))
                return "调试布局不能为空";
            try
            {
                // Build once before replacing live data so malformed input
                // cannot destroy the current playable round.
                var probe = new WaveScheduler();
                probe.Build(layout.Trim(), PickedTextures.Length);
                Level.layout = layout.Trim();
                SaveState.RoundSnapshot = "";
                RestartLevelImmediate();
                return
                    $"调试布局已载入：{probe.TotalTokenCount()} 个 token";
            }
            catch (System.Exception ex)
            {
                return "调试布局无效：" + ex.Message;
            }
        }

        /// <summary>
        /// Prevents OnApplicationQuit from writing the old board back after a
        /// GM full-profile reset.
        /// </summary>
        public void GmPrepareProfileReset()
        {
            _openLevelGeneration++;
            if (_openLevelCo != null)
            {
                StopCoroutine(_openLevelCo);
                _openLevelCo = null;
            }
            SaveState.RoundSnapshot = "";
            _persisted = true;
            _dead = false;
            ClearRound();
            Completion?.Stop();
            CompletePanel?.HidePanel();
            ContinuePanel?.HidePanel();
            BombRevivalPanel?.HidePanel();
            BombExitConfirmPanel?.HidePanel();
            HardBanner?.HideImmediate();
            SetVisible(false);
        }

        /// <summary>Port of go_home(): leave the level and show the home page.</summary>
        public void RequestGoHome()
        {
            FlowModule.LoadGameLevel();
        }

        /// <summary>Port of go_home(): leave the level and show the home page.</summary>
        public void GoHome(bool discardProgress = false)
        {
            FlowModule.LoadGameLevel();
        }

        // ------------------------------------------------------------ waves
        public bool HasPendingTokens() => Scheduler.HasPendingTokens();
        public bool HasCompletableGroup() => Field.HasCompletableGroup();
        public int CountBoardBubbleSlots() => Field.CountBoardBubbleSlots();

        public void ConsumeNextWave() { StartCoroutine(ConsumeNextWaveCo(false, false)); }

        IEnumerator ConsumeNextWaveCo(bool preSettled, bool isOpening)
        {
            int myRound = RoundSeq;
            var tokens = Scheduler.PullNextWave();
            Specials?.PrepareWave(tokens, isOpening);
            if (tokens.Count > 0) Field.BeginSpawnBatch();

            if (preSettled)
            {
                Field.SpawnWaveSettled(tokens, PickedTextures);
            }
            else if (IsLevel1())
            {
                Field.SpawnWaveSpread(tokens, PickedTextures);
            }
            else
            {
                for (int rank = 0; rank < tokens.Count; rank++)
                {
                    Field.SpawnToken(tokens[rank], PickedTextures);
                    if (rank < tokens.Count - 1)
                    {
                        yield return new WaitForSeconds(SPAWN_INTERVAL_SEC);
                        if (myRound < RoundSeq) { Field.EndSpawnBatch(); yield break; }
                    }
                }
            }
            if (tokens.Count > 0) Field.EndSpawnBatch();
            if (myRound == RoundSeq) Specials?.OnWaveDropped(true);
            if (myRound == RoundSeq) SaveRoundSnapshot();
        }

        public IEnumerator DropPendingBubbles(int count)
        {
            int myRound = RoundSeq;
            var toks = Scheduler.PullPendingTokens(count);
            if (toks.Count > 0) Field.BeginSpawnBatch();
            for (int i = 0; i < toks.Count; i++)
            {
                Field.SpawnToken(toks[i], PickedTextures);
                if (i < toks.Count - 1)
                {
                    yield return new WaitForSeconds(SPAWN_INTERVAL_SEC);
                    if (myRound < RoundSeq) { Field.EndSpawnBatch(); yield break; }
                }
            }
            if (toks.Count > 0) Field.EndSpawnBatch();
            if (myRound == RoundSeq) Specials?.OnWaveDropped(false);
            if (myRound == RoundSeq) SaveRoundSnapshot();
        }

        public void RequestDeferNextWave() { _deferNextWave = true; }

        public bool TakeDeferNextWaveFlag()
        {
            bool v = _deferNextWave;
            _deferNextWave = false;
            return v;
        }

        // ------------------------------------------------------------ locking / tools
        public bool IsInteractionLocked() => _toolBusy || _dead;
        public void SetToolBusy(bool b) { _toolBusy = b; }
        public bool IsToolBusy() => _toolBusy;
        public bool IsWon() => _persisted;
        public bool IsRoundFinalized() => _dead || Fly.IsFlying || _awarding || _persisted;
        public bool IsDead() => _dead;
        public void ClearHintHighlight() { Tools.ClearHintHighlight(); }
        public float FusionDurationMult { get; set; } = 1f;

        public IEnumerator AwaitMerge(BubbleView src, BubbleView target, bool fromTool = false)
        {
            if (src == null || target == null || Merge.Merging) yield break;
            if (_dead || _persisted) yield break;
            if (!src.CanMergeWith(target)) yield break;
            yield return Merge.Run(src, target, fromTool);
        }

        // ------------------------------------------------------------ events (bus equivalents)
        public System.Action RoundStartedEvent;
        public event System.Action<int, bool> LevelOpenCompleted;
        public event System.Action<BubbleView, bool> DragReleasedEvent;

        public void EmitRoundStarted() { RoundStartedEvent?.Invoke(); BizzaGameplayBridge.OnRoundStarted(); }

        public void EmitDragStarted(BubbleView b)
        {
            Specials?.OnFirstOperation();
            Tutorial.OnDragStarted();
        }

        public void EmitDragReleased(BubbleView b, bool moved)
        {
            DragReleasedEvent?.Invoke(b, moved);
        }
        public void EmitDragSettled() { SaveRoundSnapshot(); }

        public void EmitMergeAttempted(bool success)
        {
            Specials?.OnMergeAttempted(success);
            _stepRule.OnMergeAttempt(success);
            if (success)
            {
                _roundLinkSteps++;
                _successfulMoveCount++;
                TrackFirstActionIfNeeded();
                TrackProgressSnapshotIfNeeded();
            }
            if (!success)
            {
                _comboCounter.OnBreak();
                Combo.HideNow();
            }
        }

        public void EmitMergeStarted(BubbleView target, bool isUpgrade, bool isClosure, bool completedGroup)
        {
            SoundManager.I.Play("merge");
        }

        public void EmitMergeCommitted(BubbleView target, bool isUpgrade, bool isClosure, bool completedGroup, bool fromTool)
        {
            // sfx (bubble_sfx_plugin)
            if (isClosure) SoundManager.I.Play(AppConfig.CompleteAnimationNew ? "image_complete" : "pop");
            else if (isUpgrade) SoundManager.I.Play("sub_merge");

            // combo_plugin counts every committed merge. Only a round already
            // marked won suppresses the overlay; level 1 and non-final closure
            // merges participate in the same streak as ordinary upgrades.
            var info = _comboCounter.OnMerge();
            Specials?.OnMergeCommitted(target, fromTool);
            if (AppConfig.ComboMergeAnimation && info.show && !isClosure)
                target?.PlayComboMergeFeedback(info.tier);
            if (!_persisted)
            {
                if (info.tier == "excellent") TopBar.Dolphin.PlayApplaud();
                bool suppressed = AppConfig.ComboStepWonderful && ConsumeWonderfulSuppress();
                if (!suppressed && info.show)
                {
                    Combo.ShowCombo(info.skin);
                    PlayMilestoneVoice(info.tier, _comboCounter.Times);
                }
            }

            // wonderful tracker (combo_step plugin)
            TrackWonderful(isClosure);

            // auto-link: stash target for scan on resolve
            if (AppConfig.AutoLinkAfterMove && !fromTool && target != null)
                _pendingAutoLinkTarget = target;

            Tutorial.OnMergeCommitted(isClosure);
        }

        public void EmitMergeRejected(BubbleView src, BubbleView target)
        {
            Specials?.OnMergeRejected();
            _comboCounter.OnBreak();
            Combo.HideNow();
            Tutorial.OnMergeRejected(src, target);
        }

        public void EmitMergeResolved()
        {
            if (_pendingAutoLinkTarget != null)
            {
                var t = _pendingAutoLinkTarget;
                _pendingAutoLinkTarget = null;
                ScanAutoLink(t);
            }
            SaveRoundSnapshot();
        }

        // ---- puzzle delay-drop plugin: waves released 1.2s after the closure
        // collect starts (image_collected), never at merge commit.
        const float CLOSURE_DROP_DELAY_SEC = 1.2f;
        readonly HashSet<int> _delayDropOwned = new HashSet<int>();

        public void EmitImageClosurePending(int imageId, int slotIdx)
        {
            _delayDropOwned.Remove(imageId);
            if (!HasPendingTokens()) return;
            RequestDeferNextWave();
            _delayDropOwned.Add(imageId);
        }

        public void EmitImageCollected(int imageId, int slotIdx, Vector3 world)
        {
            Specials?.OnImageCollected(imageId);
            if (!_delayDropOwned.Remove(imageId)) return;
            StartCoroutine(DelayDropCo());
        }

        IEnumerator DelayDropCo()
        {
            int myRound = RoundSeq;
            yield return new WaitForSeconds(CLOSURE_DROP_DELAY_SEC);
            if (myRound < RoundSeq) yield break;
            if (IsWon() || IsDead()) yield break;
            if (HasPendingTokens()) ConsumeNextWave();
        }

        /// <summary>round_won: fired when the LAST image closure is reserved.
        /// Persists completion immediately (award UI waits for flight landing).</summary>
        public void EmitRoundWon()
        {
            FinalizeRound();
        }

        public void AddCollectedImage(int imageId)
        {
            CollectedImgs.Add(imageId);
        }

        public void NotifyImageLanded()
        {
            if (TopBar.IsFull && (Specials == null || Specials.CanCompleteRound))
                OnImagesAllCollected();
            RequestDeathUi();
        }

        public void NotifyAuxiliaryCompleted()
        {
            if (TopBar.IsFull) OnImagesAllCollected();
        }

        void PlayMilestoneVoice(string tier, int n)
        {
            switch (tier)
            {
                case "nice": SoundManager.I.Play("nice"); break;
                case "perfect": SoundManager.I.Play("perfect"); break;
                case "excellent":
                    int idx = ((n - 9) / 3) % 3;
                    SoundManager.I.Play("excellent" + (idx + 1));
                    break;
            }
        }

        // ------------------------------------------------------------ wonderful (combo_step plugin)
        readonly List<float> _closureTimes = new List<float>();
        readonly List<int> _closureSteps = new List<int>();
        float _lastWonderfulTime = -10f;
        bool _suppressComboOnce;

        void TrackWonderful(bool isClosure)
        {
            if (!AppConfig.ComboStepWonderful || !isClosure) return;
            float now = Time.time;
            int step = _stepRule.UsedSteps;
            _closureTimes.Add(now);
            _closureSteps.Add(step);
            while (_closureTimes.Count > 4) { _closureTimes.RemoveAt(0); _closureSteps.RemoveAt(0); }
            if (_closureTimes.Count >= 2)
            {
                int n = _closureTimes.Count;
                bool inTime = now - _closureTimes[n - 2] <= 6.0f;
                bool inSteps = step - _closureSteps[n - 2] <= 3;
                if (inTime && inSteps && now - _lastWonderfulTime >= 1.0f && !_persisted)
                {
                    _lastWonderfulTime = now;
                    _suppressComboOnce = true;
                    Combo.ShowWonderful();
                }
            }
        }

        bool ConsumeWonderfulSuppress()
        {
            if (!_suppressComboOnce) return false;
            _suppressComboOnce = false;
            return true;
        }

        // ------------------------------------------------------------ auto link (Lucky)
        void ScanAutoLink(BubbleView center)
        {
            if (!AppConfig.AutoLinkAfterMove) return;
            if (IsToolBusy()) return;
            if (IsLevel1()) return;
            if (_autoLinkCount >= 4) return;
            if (center == null || center.State != BubbleState.Alive ||
                center.Dragging || center.ReturningHome ||
                center.IsLocked || Merge.IsMergingTarget(center))
                return;
            var centerDesign = App.WorldToDesign(center.transform.position);
            if (centerDesign.y < center.GetRadius()) return;

            float slack = Field.BaseRadius * 0.5f;
            foreach (var b in Field.AllBubbles())
            {
                if (b == center || b.State != BubbleState.Alive) continue;
                if (b.Dragging || b.ReturningHome || b.IsLocked ||
                    Merge.IsMergingTarget(b))
                    continue;
                var bd = App.WorldToDesign(b.transform.position);
                if (bd.y < b.GetRadius()) continue; // must be in view
                float d = Vector3.Distance(center.transform.position, b.transform.position);
                if (center.CanMergeWith(b) && d <= center.GetRadius() + b.GetRadius() + slack)
                {
                    _autoLinkCount++;
                    ComboOverlay.SpawnLucky(App.I.WorldRoot,
                        b.transform.position + new Vector3(0, b.GetRadius() + 20f, 0));
                    StartCoroutine(Merge.Run(center, b));
                    return;
                }
            }
        }

        // ------------------------------------------------------------ death flow (bubble_death_flow.gd)
        public void CommitDeath()
        {
            if (_dead) return;
            if (!_stepRule.IsDead()) return;
            if (_persisted || CompletePanel.Visible || Fly.IsFlying || _awarding) return;
            if (IsMovesUnlimited()) return;
            _dead = true;
            _bombDeath = false;
            RecordRoundCoefficient(true);
            ModeProgress.RecordDeath(
                ModeSession.ActiveKind,
                CurrentLevelNumber);
            TrackRoundOver("step_limit");
            if (!SaveState.FreeContinueUsed && Level.difficulty_type != "Hard" && _continueAdCount == 0)
                _firstRevivePending = true;
            SoundManager.I.Play("death");
        }

        public bool ForceBombDeath()
        {
            if (_dead || _persisted || CompletePanel.Visible || Fly.IsFlying || _awarding)
                return false;
            _dead = true;
            _bombDeath = true;
            RecordRoundCoefficient(true);
            _firstRevivePending = false;
            SaveState.RoundSnapshot = "";
            ModeProgress.RecordDeath(
                ModeSession.ActiveKind,
                CurrentLevelNumber);
            TrackRoundOver("bomb");
            SoundManager.I?.Play("death");
            return true;
        }

        public void ShowBombDeathUi()
        {
            if (!_dead || !_bombDeath || _persisted || Fly.IsFlying || _awarding) return;
            TopBar.Dolphin.PlayFail();
            BizzaGameplayBridge.Lose(false);
        }

        public void RequestDeathUi()
        {
            if (!_dead || _bombDeath || _persisted || Fly.IsFlying || _awarding) return;
            if (IsMovesUnlimited()) { _dead = false; return; }
            TopBar.Dolphin.PlayFail();
            BizzaGameplayBridge.Lose(true);
        }

        public void OnContinueAd()
        {
            BizzaGameplayBridge.Lose(!_bombDeath);
        }

        public void OnContinueCoin()
        {
            BizzaGameplayBridge.Lose(!_bombDeath);
        }

        public void ApplyFrameworkRevive() => PlayContinueRevive("ad");

        void PlayContinueRevive(string reviveType)
        {
            _stepRule.Revive(15);
            _reviveCount++;
            _continueAdCount++;
            _dead = false;
            _bombDeath = false;
            TopBar.Dolphin.ResumeIdle();
            TopBar.SetMoves(_stepRule.StepsLeft);
            SaveRoundSnapshot();
        }

        public void OnContinueClose()
        {
            ContinuePanel.HidePanel();
            _dead = false;
            _bombDeath = false;
            TopBar.Dolphin.ResumeIdle();
            RestartLevelImmediate();
        }

        public void OnBombRevivalRestart()
        {
            BombRevivalPanel?.HidePanel();
            _dead = false;
            _bombDeath = false;
            TopBar.Dolphin.ResumeIdle();
            RestartLevelImmediate();
        }

        public void OnBombExitConfirmStay()
        {
            BombExitConfirmPanel?.HidePanel();
            Specials?.ResumeCountdowns();
        }

        public void OnBombExitConfirmExit()
        {
            BombExitConfirmPanel?.HidePanel();
            GoHome(true);
        }

        // ------------------------------------------------------------ round persistence
        public void SaveRoundSnapshot()
        {
            if (Level == null || Field == null || Scheduler == null) return;
            if (_persisted)
            {
                SaveState.RoundSnapshot = "";
                return;
            }
            // Keep the last playable snapshot while a death/completion flow is
            // showing. The reference game never persists a dead or won board.
            if (_dead || (CompletePanel != null && CompletePanel.Visible))
                return;
            if (Merge == null || Input == null || Fly == null) return;
            if (Merge.Merging || Input.Dragging != null || Fly.IsFlying ||
                Field.IsSpawnInFlight || _toolBusy || _awarding)
                return;

            var bubbles = Field.AllBubbles()
                .Where(b => b != null && b.State == BubbleState.Alive &&
                            b.Fragment != null && !b.Dragging &&
                            !b.ReturningHome && !b.IsFull())
                .Select(b => new BubbleProgress
                {
                    token = b.Fragment.ToTokenString(),
                    worldX = b.transform.position.x,
                    worldY = b.transform.position.y,
                    velocityX = b.Body != null ? b.Body.velocity.x : 0f,
                    velocityY = b.Body != null ? b.Body.velocity.y : 0f,
                    landed = b.HasLanded,
                    locked = b.IsLocked,
                    lockCount = b.LockCount,
                })
                .ToArray();

            var waves = Scheduler.SnapshotWaves()
                .Select(w => new RoundWaveProgress { tokens = w.ToArray() })
                .ToArray();
            // Empty-board snapshots are only observable between asynchronous
            // spawn phases and are not safe resume points.
            if (bubbles.Length == 0)
                return;

            var snapshot = new RoundProgress
            {
                level = CurrentLevelNumber,
                levelSignature = CurrentLevelSignature(),
                frozenLevelJson = JsonUtility.ToJson(Level),
                stepsLeft = _stepRule.StepsLeft,
                usedSteps = _stepRule.UsedSteps,
                linkSteps = _roundLinkSteps,
                unlimited = _stepRule.Unlimited,
                dead = false,
                continueAdCount = _continueAdCount,
                firstRevivePending = false,
                autoLinkCount = _autoLinkCount,
                collectedImages = CollectedImgs.ToArray(),
                pendingWaves = waves,
                bubbles = bubbles,
            };
            SaveState.RoundSnapshot = JsonUtility.ToJson(snapshot);
        }

        static LevelData ResolveFrozenLevelForRestore(
            int levelNumber,
            string bgEntry,
            LevelData catalogLevel)
        {
            if (bgEntry != "fresh") return null;
            string raw = SaveState.RoundSnapshot;
            if (string.IsNullOrWhiteSpace(raw)) return null;

            RoundProgress snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<RoundProgress>(raw);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    "Discarding corrupt round snapshot before level load: " +
                    ex.Message);
                SaveState.RoundSnapshot = "";
                return null;
            }

            if (snapshot == null || snapshot.version != 2 ||
                snapshot.level != levelNumber ||
                string.IsNullOrWhiteSpace(snapshot.frozenLevelJson))
                return null;

            LevelData frozen;
            try
            {
                frozen =
                    JsonUtility.FromJson<LevelData>(snapshot.frozenLevelJson);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    "Discarding round snapshot with corrupt frozen level data: " +
                    ex.Message);
                SaveState.RoundSnapshot = "";
                return null;
            }

            int imageCount = LevelRepo.ImageCount(frozen);
            bool identityMatches =
                frozen != null &&
                catalogLevel != null &&
                frozen.chapter == catalogLevel.chapter &&
                frozen.level == catalogLevel.level;
            bool levelIsValid =
                frozen != null &&
                imageCount > 0 &&
                LevelValidator.Validate(frozen.layout, imageCount).IsValid;
            bool signatureMatches =
                frozen != null &&
                snapshot.levelSignature == LevelSignature(frozen);
            if (!identityMatches || !levelIsValid || !signatureMatches)
            {
                Debug.LogWarning(
                    "Discarding round snapshot whose frozen level data is invalid.");
                SaveState.RoundSnapshot = "";
                return null;
            }
            return frozen;
        }

        bool TryRestoreRound()
        {
            string raw = SaveState.RoundSnapshot;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            RoundProgress snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<RoundProgress>(raw);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Discarding corrupt round snapshot: " + ex.Message);
                SaveState.RoundSnapshot = "";
                return false;
            }

            bool signatureMatches =
                snapshot != null &&
                snapshot.levelSignature == CurrentLevelSignature();
            bool legacySignatureMatches =
                snapshot != null &&
                string.IsNullOrWhiteSpace(snapshot.frozenLevelJson) &&
                snapshot.levelSignature == LegacyLevelSignature(Level);
            if (snapshot == null || snapshot.version != 2 ||
                snapshot.level != CurrentLevelNumber ||
                (!signatureMatches && !legacySignatureMatches) ||
                snapshot.pendingWaves == null || snapshot.bubbles == null)
            {
                SaveState.RoundSnapshot = "";
                return false;
            }

            if (!RoundProgressValidator.ValidateAndRepair(
                    snapshot,
                    Level,
                    PickedTextures.Length,
                    out bool repaired,
                    out string validationError))
            {
                Debug.LogWarning(
                    "Discarding invalid round snapshot: " + validationError);
                SaveState.RoundSnapshot = "";
                return false;
            }

            // An empty live board with queued fragments is a snapshot taken in
            // the middle of spawning. It cannot be resumed without duplicating
            // or losing ownership, so restart cleanly.
            if (snapshot.bubbles.Length == 0)
            {
                if (SnapshotHasPending(snapshot))
                    Debug.LogWarning(
                        "Discarding mid-spawn round snapshot (empty board with pending tokens).");
                SaveState.RoundSnapshot = "";
                return false;
            }

            Scheduler.RestoreWaves(snapshot.pendingWaves.Select(
                w => (System.Collections.Generic.IEnumerable<string>)
                    (w?.tokens ?? System.Array.Empty<string>())));

            int spawned =
                Field.SpawnRestoredSettled(snapshot.bubbles, PickedTextures).Count;
            if (spawned != snapshot.bubbles.Length)
            {
                Debug.LogWarning(
                    $"Discarding round snapshot: restored {spawned}/" +
                    $"{snapshot.bubbles.Length} board bubbles.");
                Field.ClearBoard();
                Scheduler.Build(Level.layout, PickedTextures.Length);
                TopBar.SetupTarget(PickedTextures.Length);
                SaveState.RoundSnapshot = "";
                return false;
            }

            CollectedImgs.Clear();
            if (snapshot.collectedImages != null)
            {
                foreach (int imageId in snapshot.collectedImages)
                {
                    if (imageId < 0 || imageId >= PickedTextures.Length) continue;
                    CollectedImgs.Add(imageId);
                    int slot = TopBar.ReserveNextIndex();
                    TopBar.FillCollected(slot, animate: false);
                }
            }

            _stepRule.Restore(
                snapshot.stepsLeft, snapshot.usedSteps, IsMovesUnlimited());
            _roundLinkSteps = Mathf.Clamp(
                snapshot.linkSteps,
                0,
                _stepRule.UsedSteps);
            TopBar.SetMovesVisible(true);
            if (IsMovesUnlimited()) TopBar.SetMovesUnlimited();
            else TopBar.SetMoves(_stepRule.StepsLeft);

            _continueAdCount = Mathf.Max(0, snapshot.continueAdCount);
            _firstRevivePending = false;
            _autoLinkCount = Mathf.Max(0, snapshot.autoLinkCount);
            _dead = false;

            if (repaired)
                Debug.LogWarning(
                    "Round snapshot was missing level leaves; appended a repair wave.");

            SaveRoundSnapshot();
            StartCoroutine(EnsurePlayableAfterRestoreCo());
            return true;
        }

        string CurrentLevelSignature()
        {
            return LevelSignature(Level);
        }

        static string LevelSignature(LevelData level)
        {
            return level == null
                ? ""
                : Hash128.Compute(JsonUtility.ToJson(level)).ToString();
        }

        static string LegacyLevelSignature(LevelData level)
        {
            if (level == null) return "";
            string urls = level.image_urls == null
                ? ""
                : string.Join("|", level.image_urls);
            return Hash128.Compute(
                $"{level.chapter}:{level.level}:{level.layout}|{urls}").ToString();
        }

        static bool SnapshotHasPending(RoundProgress snapshot)
        {
            return snapshot?.pendingWaves != null &&
                   snapshot.pendingWaves.Any(
                       wave => wave?.tokens != null &&
                               wave.tokens.Any(
                                   token => !string.IsNullOrWhiteSpace(token)));
        }

        IEnumerator EnsurePlayableAfterRestoreCo()
        {
            int myRound = RoundSeq;
            yield return null;
            int restoredWaves = 0;
            while (myRound == RoundSeq &&
                   !_persisted &&
                   !_dead &&
                   Field.HasAliveBubble() &&
                   Scheduler.HasPendingTokens() &&
                   !Field.HasCompletableGroup())
            {
                if (++restoredWaves > 100)
                {
                    Debug.LogError(
                        "Stopped restored-board wave repair after 100 waves.");
                    yield break;
                }
                yield return ConsumeNextWaveCo(false, false);
            }

            if (myRound == RoundSeq && restoredWaves > 0)
            {
                Debug.Log(
                    $"Restored board required {restoredWaves} pending wave(s) " +
                    "before a completable group was available.");
                SaveRoundSnapshot();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveRoundSnapshot();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveRoundSnapshot();
        }

        void OnApplicationQuit()
        {
            SaveRoundSnapshot();
        }

        // ------------------------------------------------------------ award flow (bubble_award_flow.gd)
        void OnImagesAllCollected()
        {
            if (!TopBar.IsFull) return;
            StartCoroutine(CheckWinCo());
        }

        IEnumerator CheckWinCo()
        {
            if (_awardStarted) yield break;
            _awardStarted = true;
            Completion.Arm();
            Backdrop.PlaySettleToCompleteBackdrop();
            Completion.PlayAtFrame(CompletionFx.EXIT_FRAMES_BEFORE_SPINE);
            yield return new WaitForSeconds(CompletionFx.EXIT_FRAMES_BEFORE_COIN_FLY / CompletionFx.EMIT_FPS_ASSUMED);
            yield return AwardRunCo();
        }

        bool _awardStarted;

        void FinalizeRound()
        {
            if (_persisted) return;
            RecordRoundCoefficient(false);
            _persisted = true;
            SaveState.RoundSnapshot = "";
            _pendingReward = 0;
            _roundTelemetryActive = false;
            Specials?.OnRoundWon(CurrentLevelNumber);
        }

        void TrackRoundStarted()
        {
            FunSmithTelemetry.TrackGameStart(
                Level,
                CurrentLevelNumber,
                _roundStartSource,
                ModeSession.ActiveKind.ToString().ToLowerInvariant());
            int enterType = _roundStartSource == "restart"
                ? 2
                : _roundStartSource == "next"
                    ? 3
                    : 1;
            FunSmithTelemetry.TrackStartGameLevel(
                CurrentLevelNumber,
                enterType);
            _roundTelemetryActive = true;
        }

        void TrackRoundOver(string reason)
        {
            _roundFailCount++;
            FunSmithTelemetry.TrackGameOver(
                Level,
                CurrentLevelNumber,
                _roundStartSource,
                ModeSession.ActiveKind.ToString().ToLowerInvariant(),
                CurrentRoundElapsedSeconds,
                CollectedImgs.Count,
                _stepRule.UsedSteps,
                reason);
            FunSmithTelemetry.TrackEndGameFail(
                CurrentLevelNumber,
                CountRemainingBubbles());
        }

        void TrackFirstActionIfNeeded()
        {
            if (_firstActionTracked || !_roundTelemetryActive) return;
            _firstActionTracked = true;
            FunSmithTelemetry.TrackLevelFirstAction(
                Level,
                CurrentLevelNumber,
                _roundStartSource,
                ModeSession.ActiveKind.ToString().ToLowerInvariant(),
                CurrentRoundElapsedSeconds,
                _stepRule.UsedSteps,
                CountRemainingBubbles(),
                CollectedImgs.Count);
        }

        void TrackProgressSnapshotIfNeeded()
        {
            int milestone;
            int bit;
            switch (_successfulMoveCount)
            {
                case 10:
                    milestone = 10;
                    bit = 1;
                    break;
                case 25:
                    milestone = 25;
                    bit = 2;
                    break;
                case 50:
                    milestone = 50;
                    bit = 4;
                    break;
                default:
                    return;
            }
            if ((_progressMoveMask & bit) != 0) return;
            _progressMoveMask |= bit;
            FunSmithTelemetry.TrackLevelProgressSnapshot(
                Level,
                CurrentLevelNumber,
                _roundStartSource,
                ModeSession.ActiveKind.ToString().ToLowerInvariant(),
                milestone,
                CurrentRoundElapsedSeconds,
                _stepRule.UsedSteps,
                CountRemainingBubbles(),
                CollectedImgs.Count,
                $"hint:{_hintUseCount},drop:{_dropUseCount}," +
                $"magnet:{_magnetUseCount}",
                $"count:{_reviveCount}");
        }

        void TrackRoundExit(string reason, bool progressSaved)
        {
            if (!_roundTelemetryActive || _persisted || Level == null) return;
            FunSmithTelemetry.TrackGameExit(
                Level,
                CurrentLevelNumber,
                _roundStartSource,
                ModeSession.ActiveKind.ToString().ToLowerInvariant(),
                CurrentRoundElapsedSeconds,
                CollectedImgs.Count,
                _stepRule.UsedSteps,
                reason,
                progressSaved);
            TrackStandardEndOnce(
                reason == "restart" ? 4 : _dead ? 2 : 3);
            ReportPlaytimeOnce(reason == "restart" ? "abandon" : "exit");
            _roundTelemetryActive = false;
        }

        void TrackStandardEndOnce(int endType)
        {
            if (_standardEndTracked) return;
            _standardEndTracked = true;
            FunSmithTelemetry.TrackEndGameLevel(
                CurrentLevelNumber,
                endType,
                _roundFailCount,
                CurrentRoundElapsedSeconds,
                CountRemainingBubbles());
        }

        void TrackLevelNextBehaviorOnce(int endType, int nextBehavior)
        {
            if (_standardNextBehaviorTracked) return;
            _standardNextBehaviorTracked = true;
            FunSmithTelemetry.TrackLevelNextBehavior(endType, nextBehavior);
        }

        void ReportPlaytimeOnce(string reason)
        {
            if (_playtimeReported) return;
            _playtimeReported = true;
            FunSmithTelemetry.TrackPlaytimeUpdate(
                CurrentRoundElapsedSeconds,
                reason);
        }

        public void TrackToolGranted(
            string toolId,
            string source,
            int amount,
            int left)
        {
            FunSmithTelemetry.TrackPropGet(
                CurrentLevelNumber,
                toolId,
                source,
                amount,
                left);
            FunSmithTelemetry.TrackPropGetStandard(
                toolId,
                amount,
                source);
        }

        public void TrackToolUsed(
            string toolId,
            string source,
            int left)
        {
            if (toolId == "hint") _hintUseCount++;
            else if (toolId == "drop") _dropUseCount++;
            else if (toolId == "magnet") _magnetUseCount++;
            FunSmithTelemetry.TrackPropUse(
                CurrentLevelNumber,
                toolId,
                source,
                1,
                left,
                _stepRule.UsedSteps);
            FunSmithTelemetry.TrackPropUseStandard(
                CurrentLevelNumber,
                toolId,
                PropSourceCode(source),
                CountRemainingBubbles());
        }

        static int PropSourceCode(string source)
        {
            switch (source)
            {
                case "free": return 1;
                case "inventory": return 2;
                case "white_package": return 3;
                default: return 0;
            }
        }

        void RecordRoundCoefficient(bool withDeath)
        {
            if (_roundCoefficientRecorded || _stepRule.UsedSteps <= 0)
                return;
            _roundCoefficientRecorded = true;
            int remaining = withDeath && Field != null
                ? Field.CountBoardBubbles()
                : 0;
            float coefficient = JigsawChipRule.ComputeRoundCoefficient(
                _stepRule.UsedSteps,
                _roundLinkSteps,
                withDeath,
                remaining);
            SaveState.RecordRoundCoefficient(coefficient);
        }

        int ComputeReward()
        {
            if (IsMovesUnlimited()) return 0;
            return Mathf.Max(_stepRule.StepsLeft, 0) * COIN_PER_STEP;
        }

        IEnumerator AwardRunCo()
        {
            int myRound = RoundSeq;
            FinalizeRound();
            int reward = _pendingReward;
            if (reward <= 0)
            {
                SoundManager.I.Play("game_completed");
                yield return new WaitForSeconds(SETTLE_DELAY_BEFORE_BARS_SLIDE_SEC);
                Backdrop.PlayBarsTransition(false);
                if (Backdrop.TopbarSlideCoroutine != null)
                    yield return Backdrop.TopbarSlideCoroutine;
                Completion.FinishForSettlement();
                PresentCompletion();
                yield break;
            }

            int current = SaveState.Coins - reward;
            _awarding = true;
            yield return new WaitForSeconds(0.3f);
            if (myRound < RoundSeq) yield break;
            TopBar.EnterRewardMode(current);
            yield return TopBar.PlayCoinReward(reward, reward / COIN_PER_STEP);
            if (myRound < RoundSeq) yield break;
            _awarding = false;
            Backdrop.PlayBarsTransition(false);
            if (Backdrop.TopbarSlideCoroutine != null)
                yield return Backdrop.TopbarSlideCoroutine;
            Completion.FinishForSettlement();
            PresentCompletion();
        }

        void PresentCompletion()
        {
            BizzaGameplayBridge.Win();
        }

        public void OnCompleteNext()
        {
            FlowModule.LoadGameLevel();
        }

        void ContinueAfterCompletion()
        {
            FlowModule.LoadGameLevel();
        }
    }
}
