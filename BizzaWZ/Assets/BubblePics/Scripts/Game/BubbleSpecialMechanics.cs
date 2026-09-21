using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BubblePics.GameModes;
using TMPro;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Runtime bridge for the data-driven 1.0.9 bubble plugins. Selection stays
    /// in BubbleSpecialRules; this component owns wave/event lifecycle only.
    /// </summary>
    public sealed class BubbleSpecialMechanics : MonoBehaviour
    {
        BubblePage _page;
        readonly Dictionary<string, int> _bombs = new Dictionary<string, int>();
        readonly Dictionary<string, int> _locks = new Dictionary<string, int>();
        readonly Dictionary<string, BubbleSpecialRules.StickerAssignment> _stickers =
            new Dictionary<string, BubbleSpecialRules.StickerAssignment>();
        readonly HashSet<string> _usedStickers = new HashSet<string>();
        readonly HashSet<string> _bombReserved = new HashSet<string>();
        readonly HashSet<string> _lockFrontHalfGroups = new HashSet<string>();
        int[] _starfishPlan = System.Array.Empty<int>();
        int _waveIndex;
        int _maxLocksThisRound;
        int _totalLocksThisRound;
        int _totalLockTiles;
        int _droppedLockTiles;
        int _currentLockWaveTiles;
        int _rainbowChecks;
        bool _rainbowPending;
        bool _rainbowSpawned;
        bool _timerStarted;
        bool _countdownsPaused;
        bool _spawningSpecial;
        int _mergeStreak;
        int _rescuedStarfish;
        bool _magnetActive;
        bool _magnetCompleted;
        int _magnetImageId;
        GameObject _bombExplosionFx;
        readonly List<GameObject> _magnetFxRoots = new List<GameObject>();

        const float BOMB_EXPLOSION_DURATION_SEC = 0.933333f;
        const float BOMB_BUBBLES_FADE_DURATION_SEC = 0.8f;
        const float BOMB_BUBBLES_FADE_DELAY_SEC =
            BOMB_EXPLOSION_DURATION_SEC - BOMB_BUBBLES_FADE_DURATION_SEC;

        public int StickerCount => _stickers.Count;
        public bool CanCompleteRound => !_magnetActive || _magnetCompleted;
        public bool HasActiveTimedBomb =>
            AppConfig.BombModeType == "time" &&
            Alive().Any(value => value.IsBomb);

        public void Initialize(BubblePage page)
        {
            _page = page;
        }

        public static void NotifyBubbleSpawned(BubbleView bubble)
        {
            BubbleSpecialMechanics mechanics = App.I != null && App.I.Page != null
                ? App.I.Page.Specials
                : null;
            mechanics?.OnBubbleSpawned(bubble);
        }

        public void PrepareRound()
        {
            CancelRoundEffects();
            _bombs.Clear();
            _locks.Clear();
            _stickers.Clear();
            _usedStickers.Clear();
            _bombReserved.Clear();
            _lockFrontHalfGroups.Clear();
            _waveIndex = 0;
            _maxLocksThisRound = 0;
            _totalLocksThisRound = 0;
            _totalLockTiles = 0;
            _droppedLockTiles = 0;
            _currentLockWaveTiles = 0;
            _rainbowChecks = 0;
            _rainbowPending = false;
            _rainbowSpawned = false;
            _timerStarted = false;
            _countdownsPaused = false;
            _mergeStreak = 0;
            _rescuedStarfish = 0;

            List<List<string>> waves = _page.Scheduler.SnapshotWaves();
            PrepareLockRound(waves);
            bool saveEnabled = (AppConfig.SaveBubble || AppConfig.StarfishBubble) &&
                BubbleSpecialRules.IsPeriodicLevel(
                    _page.CurrentLevelNumber,
                    AppConfig.SaveBubbleStartLevel,
                    AppConfig.SaveBubbleInterval);
            _starfishPlan = saveEnabled
                ? BubbleSpecialRules.ComputeStarfishWavePlan(waves.Count)
                : System.Array.Empty<int>();

            bool hardLevel = _page.Level != null &&
                string.Equals(
                    _page.Level.difficulty_type,
                    "Hard",
                    System.StringComparison.OrdinalIgnoreCase);
            bool stickerEnabled = AppConfig.StickerPuzzle &&
                BubbleSpecialRules.IsStickerPuzzleEnabled(
                    _page.CurrentLevelNumber,
                    AppConfig.StickerStartLevel,
                    AppConfig.StickerInterval,
                    hardLevel,
                    JigsawChipRule.IsEnabled,
                    ModeSession.ActiveKind);
            if (stickerEnabled)
            {
                foreach (var assignment in BubbleSpecialRules.AssignStickers(
                    waves, _page.PickedTextures.Length))
                    _stickers[StickerKey(assignment.ImageId, assignment.Path)] = assignment;
            }

            _magnetActive = AppConfig.MagnetBubbleGroup > 0 &&
                _page.CurrentLevelNumber >= 11 && _page.Level != null &&
                string.Equals(_page.Level.difficulty_type, "Hard",
                    System.StringComparison.OrdinalIgnoreCase);
            _magnetCompleted = !_magnetActive;
            _magnetImageId = 1000000 + _page.CurrentLevelNumber;
        }

        public void CancelRoundEffects()
        {
            StopAllCoroutines();
            if (_bombExplosionFx != null) Destroy(_bombExplosionFx);
            _bombExplosionFx = null;
            foreach (GameObject root in _magnetFxRoots)
                if (root != null) Destroy(root);
            _magnetFxRoots.Clear();
            _timerStarted = false;
            _countdownsPaused = false;
        }

        void PrepareLockRound(IReadOnlyList<List<string>> waves)
        {
            if (!AppConfig.LockedMode ||
                !BubbleSpecialRules.IsLockedLevel(_page.CurrentLevelNumber) ||
                waves == null)
                return;

            var firstPositions = new Dictionary<string, int>();
            var imageIds = new HashSet<int>();
            int position = 0;
            foreach (IReadOnlyList<string> wave in waves)
            {
                if (wave == null) continue;
                foreach (string token in wave)
                {
                    BubbleFragment fragment = BubbleFragment.FromToken(token);
                    if (fragment == null) continue;
                    string groupKey = BubbleSpecialRules.GroupKey(fragment);
                    if (!firstPositions.ContainsKey(groupKey))
                        firstPositions[groupKey] = position;
                    imageIds.Add(fragment.ImageId);
                    position++;
                }
            }

            _totalLockTiles = position;
            float halfPosition = _totalLockTiles / 2f;
            foreach (KeyValuePair<string, int> pair in firstPositions)
            {
                if (pair.Value < halfPosition)
                    _lockFrontHalfGroups.Add(pair.Key);
            }

            int min = imageIds.Count <= 10 ? 1 : 2;
            int max = imageIds.Count <= 10 ? 2 : 4;
            var random = new System.Random(
                _page.CurrentLevelNumber * 7919 + _totalLockTiles);
            _maxLocksThisRound = random.Next(min, max + 1);
        }

        public void PauseCountdowns()
        {
            _countdownsPaused = true;
        }

        public void ResumeCountdowns()
        {
            _countdownsPaused = false;
        }

        public void OnRoundStarted(bool restored)
        {
            if (restored) return;

            if (SaveState.TryConsumeDailyFirstStepBonus() &&
                AppConfig.DailyFirstStepBonus && !_page.IsMovesUnlimited())
            {
                DailyFirstStepBonusView view =
                    MetaFlowPresenter.ShowDailyFirstStepBonus(
                        _page.TopBar?.Root,
                        _page.StepRule.GrantBonusSteps);
                if (view == null)
                    _page.StepRule.GrantBonusSteps(
                        BubbleSpecialRules.DailyFirstStepBonus);
            }

            if (AppConfig.LuckyBreak && SaveState.PopLuckyBreakPending())
                MetaFlowPresenter.ShowUnlimitedLucky(_page.TopBar?.Root);
            else if (AppConfig.LuckyBreak &&
                     SaveState.IsLuckyBreakLevel(_page.CurrentLevelNumber))
                MetaFlowPresenter.ShowLuckyBreakRoundIntro(_page.TopBar?.Root);
        }

        public void PrepareWave(IReadOnlyList<string> tokens, bool opening)
        {
            _locks.Clear();
            _currentLockWaveTiles = tokens != null ? tokens.Count : 0;
            int seed = _page.CurrentLevelNumber * 397 ^ _waveIndex * 53;
            if (opening && AppConfig.BombMode &&
                BubbleSpecialRules.IsPeriodicLevel(
                    _page.CurrentLevelNumber,
                    AppConfig.BombStartLevel,
                    AppConfig.BombInterval))
            {
                foreach (var plan in BubbleSpecialRules.SelectBombs(
                    tokens, _page.PickedTextures.Length,
                    AppConfig.BombModeType == "time", seed))
                {
                    _bombs[plan.TokenKey] = plan.Count;
                    _bombReserved.Add(plan.TokenKey);
                }
            }

            if (AppConfig.LockedMode &&
                BubbleSpecialRules.IsLockedLevel(_page.CurrentLevelNumber) &&
                _totalLocksThisRound < _maxLocksThisRound &&
                _droppedLockTiles <= _totalLockTiles / 2f)
            {
                var plan = BubbleSpecialRules.SelectLock(
                    tokens,
                    _page.Field.AllBubbles(),
                    _bombReserved,
                    seed + 11,
                    _lockFrontHalfGroups);
                if (plan.HasValue) _locks[plan.Value.TokenKey] = plan.Value.Count;
            }
        }

        public void OnWaveDropped(bool automatic)
        {
            if (automatic)
                _droppedLockTiles += _currentLockWaveTiles;
            _currentLockWaveTiles = 0;
            _locks.Clear();
            if (_waveIndex < _starfishPlan.Length)
            {
                for (int i = 0; i < _starfishPlan[_waveIndex]; i++) SpawnStarfish();
            }
            if (_magnetActive && _waveIndex < 2)
            {
                int first = _waveIndex * 2;
                SpawnMagnetPiece(first);
                SpawnMagnetPiece(first + 1);
            }
            if (automatic && _rainbowPending && !_rainbowSpawned)
                SpawnRainbow();
            _waveIndex++;
        }

        void OnBubbleSpawned(BubbleView bubble)
        {
            if (_spawningSpecial || bubble == null || bubble.Fragment == null) return;
            string tokenKey = BubbleSpecialRules.TokenKey(bubble.Fragment);
            if (_bombs.TryGetValue(tokenKey, out int bomb))
            {
                _bombs.Remove(tokenKey);
                bubble.SetBomb(bomb);
                return;
            }
            if (_locks.TryGetValue(tokenKey, out int locked))
            {
                _locks.Remove(tokenKey);
                bubble.SetLock(locked);
                _totalLocksThisRound++;
                return;
            }

            if (bubble.Fragment.HeldPaths.Count != 1) return;
            string stickerKey = StickerKey(
                bubble.Fragment.ImageId, bubble.Fragment.HeldPaths[0]);
            if (!_stickers.TryGetValue(stickerKey, out var assignment) ||
                !_usedStickers.Add(stickerKey))
                return;
            bubble.Fragment.PendingStickerReturn = true;
            bubble.Fragment.StickerShapeId = assignment.ShapeId;
            bubble.Fragment.StickerDonorPath = new List<int>(assignment.Path);
            bubble.Fragment.StickerHalf = 1;
            bubble.Refresh();
            BubbleFragment companion = bubble.Fragment.Clone();
            companion.StickerHalf = 2;
            _spawningSpecial = true;
            _page.Field.SpawnSpecial(companion, bubble.SourceTexture);
            _spawningSpecial = false;
        }

        public void OnFirstOperation()
        {
            _timerStarted = true;
        }

        public void OnMergeAttempted(bool success)
        {
            _timerStarted = true;
            if (!success) _mergeStreak = 0;
        }

        public void OnMergeCommitted(BubbleView target, bool fromTool)
        {
            foreach (BubbleView bubble in Alive().Where(value => value.IsLocked).ToList())
                bubble.DecrementLock();
            if (!fromTool && AppConfig.BombModeType == "step")
                TickStepBombs();
            if (!fromTool && AppConfig.StepIncrease &&
                ++_mergeStreak >= BubbleSpecialRules.StepIncreaseStreakTarget)
            {
                _mergeStreak = 0;
                _page.StepRule.GrantBonusSteps(BubbleSpecialRules.StepIncreaseBonus);
            }
        }

        public void OnMergeRejected()
        {
            _mergeStreak = 0;
            if (AppConfig.BombModeType == "step")
                TickStepBombs();
        }

        public void OnImageCollected(int imageId)
        {
            if (imageId >= 0 && Alive().Any(value => value.IsStarfish))
                StartCoroutine(RescueStarfishCo());

            if (!AppConfig.RainbowBubble || _rainbowSpawned || _rainbowPending ||
                _page.Scheduler.DistinctImageCount() < BubbleSpecialRules.RainbowMinPuzzleCount ||
                _rainbowChecks >= BubbleSpecialRules.RainbowMaxCollectedChecks)
                return;
            _rainbowChecks++;
            if (Random.value < BubbleSpecialRules.RainbowChance)
                _rainbowPending = true;
        }

        public bool IsAuxiliaryClosure(BubbleView bubble)
        {
            return bubble != null && bubble.IsMagnetBubble && bubble.IsFull();
        }

        public void ResolveAuxiliaryClosure(BubbleView bubble)
        {
            if (!IsAuxiliaryClosure(bubble)) return;
            _magnetCompleted = true;
            if (AppConfig.MagnetBubbleGroup == 2)
            {
                StartCoroutine(ResolveMagnetGrantCo(bubble));
                return;
            }

            SoundManager.I?.Play("magnet_bubble_burst");
            bubble.Explode();
            if (_page.Tools != null && _page.Tools.CanApplyMagnet())
            {
                StartCoroutine(_page.Tools.ApplyMagnet());
            }
            _page.NotifyAuxiliaryCompleted();
        }

        IEnumerator ResolveMagnetGrantCo(BubbleView bubble)
        {
            int round = _page != null ? _page.RoundSeq : -1;
            Vector3 source = bubble != null
                ? bubble.transform.position
                : App.DesignToWorld(new Vector2(
                    BubbleField.ViewW * 0.5f,
                    BubbleField.ViewH * 0.55f));
            float diameter = bubble != null ? bubble.GetRadius() * 2f : 180f;

            SpawnMagnetSpine(
                "effect_haitun_01",
                "ef_haitunhecheng_01",
                source,
                new Vector2(
                    diameter * 1.2f / 427f,
                    diameter * 1.2f / 427f),
                0f,
                2f);
            if (bubble != null) bubble.Explode();

            yield return new WaitForSeconds(18f / 30f / 2f);
            if (_page == null || round != _page.RoundSeq) yield break;

            Vector3 target = MagnetButtonWorldPosition(source);
            Vector2 delta = target - source;
            float distance = Mathf.Max(1f, delta.magnitude);
            float rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
            SpawnMagnetSpine(
                "effect_haitun_04",
                "ef_haitunhecheng_04",
                source,
                new Vector2(134f / 517.225f, distance / 2203.044f),
                rotation,
                2f);
            SoundManager.I?.Play("magnet_bubble_burst");

            yield return new WaitForSeconds(18f / 30f / 2f);
            if (_page == null || round != _page.RoundSeq) yield break;

            SpawnMagnetSpine(
                "effect_haitun_02",
                "ef_haitunhecheng_02",
                target,
                Vector2.one * (204f * 1.05f / 360.5f),
                0f,
                1f);
            yield return new WaitForSeconds(16f / 30f);
            if (_page == null || round != _page.RoundSeq) yield break;

            SaveState.SetToolCount(
                "magnet",
                SaveState.GetToolCount("magnet") + 1);
            _page.Toolbar?.Refresh();
            _page.NotifyAuxiliaryCompleted();
        }

        Vector3 MagnetButtonWorldPosition(Vector3 fallback)
        {
            RectTransform button = _page?.Toolbar?.Magnet?.Root;
            Camera camera = App.I != null ? App.I.Cam : null;
            if (button == null || camera == null) return fallback;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(
                null, button.position);
            float distance = Mathf.Abs(camera.transform.position.z);
            Vector3 world = camera.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, distance));
            world.z = fallback.z;
            return world;
        }

        SpineLite.SpineSprite SpawnMagnetSpine(
            string module,
            string animation,
            Vector3 position,
            Vector2 scale,
            float rotation,
            float timeScale)
        {
            Transform parent = App.I != null
                ? App.I.WorldRoot
                : _page != null ? _page.transform : transform;
            var root = new GameObject(module + "Fx");
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
            root.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            _magnetFxRoots.Add(root);

            SpineLite.SpineSprite spine =
                root.AddComponent<SpineLite.SpineSprite>();
            spine.Load(module);
            if (spine.Data == null || !spine.HasAnimation(animation))
            {
                _magnetFxRoots.Remove(root);
                Destroy(root);
                return null;
            }
            spine.SortingOrder = 5000;
            SpineLite.TrackEntry entry = spine.SetAnimation(
                animation, false, 0f);
            if (entry == null)
            {
                _magnetFxRoots.Remove(root);
                Destroy(root);
                return null;
            }
            entry.TimeScale = Mathf.Max(0.01f, timeScale);
            entry.Completed += () =>
            {
                if (root == null) return;
                _magnetFxRoots.Remove(root);
                Destroy(root);
            };
            return spine;
        }

        public bool ResolveRainbowMerge(BubbleView source, BubbleView target)
        {
            BubbleView rainbow = source != null && source.Fragment != null && source.Fragment.IsRainbow
                ? source
                : target != null && target.Fragment != null && target.Fragment.IsRainbow ? target : null;
            BubbleView real = rainbow == source ? target : source;
            if (rainbow == null || real == null || real.Fragment == null) return true;

            List<int> parent = real.Fragment.ParentPath();
            var used = new HashSet<int>(real.Fragment.LastQuadrants());
            BubbleFragment replacement = null;
            if (_page.Scheduler.TryTakeCompatibleToken(
                    real.Fragment.ImageId, parent, used, out string token))
                replacement = BubbleFragment.FromToken(token);
            if (replacement == null)
            {
                BubbleView candidate = Alive().FirstOrDefault(value =>
                    value != source && value != target && value.Fragment != null &&
                    value.Fragment.ImageId == real.Fragment.ImageId &&
                    value.Fragment.ParentPath().SequenceEqual(parent) &&
                    !value.Fragment.LastQuadrants().Any(used.Contains));
                if (candidate != null)
                {
                    replacement = candidate.Fragment.Clone();
                    candidate.Explode();
                }
            }
            if (replacement == null)
            {
                int missing = Enumerable.Range(0, 4).FirstOrDefault(value => !used.Contains(value));
                replacement = new BubbleFragment
                {
                    ImageId = real.Fragment.ImageId,
                    HeldPaths = new List<List<int>> { new List<int>(parent) { missing } },
                };
            }
            rainbow.Fragment = replacement;
            rainbow.ImageId = replacement.ImageId;
            rainbow.SourceTexture = real.SourceTexture;
            rainbow.Refresh();
            return true;
        }

        public void OnRoundWon(int completedLevel)
        {
            if (!AppConfig.LuckyBreak) return;
            int minimum = Mathf.Max(1, _page.MinCompletionStepCount());
            float coefficient = Mathf.Max(0f, _page.StepRule.UsedSteps) / (float)minimum;
            int today = SaveState.RecordLuckyBreakWin(coefficient);
            if (!SaveState.LuckyBreakDayChecked && BubbleSpecialRules.LuckyBreakQualifies(
                    completedLevel, today, SaveState.LuckyBreakDayTarget,
                    SaveState.LuckyBreakDayCoefficients))
            {
                SaveState.LuckyBreakDayChecked = true;
                SaveState.CommitLuckyBreakWindow(completedLevel);
            }
        }

        void Update()
        {
            if (_page == null || !_timerStarted || _countdownsPaused ||
                _page.IsWon() || _page.IsDead())
                return;
            if (AppConfig.BombModeType == "time")
            {
                bool exploded = false;
                foreach (BubbleView bubble in Alive().Where(value => value.IsBomb).ToList())
                {
                    if (bubble.TickBombTime(Time.deltaTime)) exploded = true;
                }
                if (exploded) TriggerBombDeath();
            }
            foreach (BubbleView bubble in Alive().Where(value => value.IsStarfish).ToList())
                if (bubble.TickStarfish(Time.deltaTime)) bubble.ClearStarfish(false);
        }

        IEnumerable<BubbleView> Alive()
        {
            return _page != null && _page.Field != null
                ? _page.Field.AllBubbles().Where(value => value != null && value.State == BubbleState.Alive)
                : Enumerable.Empty<BubbleView>();
        }

        IEnumerator RescueStarfishCo()
        {
            yield return new WaitForSeconds(0.6f);
            foreach (BubbleView starfish in Alive().Where(value => value.IsStarfish).ToList())
            {
                _rescuedStarfish++;
                starfish.ClearStarfish(true);
            }
            if (_rescuedStarfish >= BubbleSpecialRules.StarfishTotal)
            {
                _page.TopBar?.Dolphin?.PlayApplaud();
                SoundManager.I?.Play("sea_hero");
                MetaFlowPresenter.ShowBanner(MetaFlowId.SeaHeroBanner);
            }
        }

        void SpawnStarfish()
        {
            int[] seconds = { 45, 40, 35, 30, 25 };
            int index = Random.Range(0, seconds.Length);
            var fragment = new BubbleFragment
            {
                ImageId = -1,
                HeldPaths = new List<List<int>> { new List<int>() },
            };
            _spawningSpecial = true;
            BubbleView bubble = _page.Field.SpawnSpecial(
                fragment, AssetLib.Texture("Art/Sprites/Bubble/starfish_bubble_normal"));
            _spawningSpecial = false;
            bubble?.SetStarfish(seconds[index]);
            if (SaveState.TryConsumeFeatureTutorial("starfish"))
                MetaFlowPresenter.ShowTutorial(
                    MetaFlowId.StarfishTutorialPage,
                    AssetLib.Sprite("Art/Sprites/Bubble/starfish_bubble_normal"));
        }

        void SpawnRainbow()
        {
            var fragment = new BubbleFragment
            {
                ImageId = -1,
                IsRainbow = true,
                HeldPaths = new List<List<int>> { new List<int>() },
            };
            _spawningSpecial = true;
            _page.Field.SpawnSpecial(fragment, AssetLib.Texture("Art/Sprites/Bubble/rainbow_bubble"));
            _spawningSpecial = false;
            _rainbowSpawned = true;
            _rainbowPending = false;
            if (SaveState.TryConsumeFeatureTutorial("rainbow"))
                MetaFlowPresenter.ShowTutorial(
                    MetaFlowId.RainbowTutorialPage,
                    AssetLib.Sprite("Art/Sprites/Bubble/rainbow_bubble"));
        }

        void SpawnMagnetPiece(int quadrant)
        {
            var fragment = new BubbleFragment
            {
                ImageId = _magnetImageId,
                HeldPaths = new List<List<int>> { new List<int> { quadrant } },
            };
            _spawningSpecial = true;
            BubbleView bubble = _page.Field.SpawnSpecial(
                fragment, AssetLib.Texture("Art/Sprites/Bubble/magnet_bubble_dophin"));
            _spawningSpecial = false;
            bubble?.SetMagnetBubble(true);
        }

        void TriggerBombDeath()
        {
            _timerStarted = false;
            if (_page == null || !_page.ForceBombDeath()) return;
            StartCoroutine(PlayBombDeathCo());
        }

        void TickStepBombs()
        {
            bool exploded = false;
            foreach (BubbleView bubble in
                     Alive().Where(value => value.IsBomb).ToList())
            {
                if (bubble.DecrementBomb()) exploded = true;
            }
            if (exploded) TriggerBombDeath();
        }

        IEnumerator PlayBombDeathCo()
        {
            int round = _page.RoundSeq;
            SpineLite.SpineSprite explosion = SpawnBombExplosion();
            if (explosion == null)
            {
                _page.ShowBombDeathUi();
                yield break;
            }

            yield return new WaitForSeconds(BOMB_BUBBLES_FADE_DELAY_SEC);
            if (_page == null || round != _page.RoundSeq || !_page.IsDead())
                yield break;

            var spriteStates = new List<SpriteFadeState>();
            var textStates = new List<TextFadeState>();
            var textMeshStates = new List<TextMeshFadeState>();
            var lineStates = new List<LineFadeState>();
            var spineStates = new List<SpineFadeState>();
            CaptureBubbleFadeStates(
                spriteStates,
                textStates,
                textMeshStates,
                lineStates,
                spineStates);

            float elapsed = 0f;
            while (elapsed < BOMB_BUBBLES_FADE_DURATION_SEC)
            {
                if (_page == null || round != _page.RoundSeq || !_page.IsDead())
                    yield break;
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(
                    elapsed / BOMB_BUBBLES_FADE_DURATION_SEC);
                ApplyFade(
                    spriteStates,
                    textStates,
                    textMeshStates,
                    lineStates,
                    spineStates,
                    alpha);
                yield return null;
            }
            ApplyFade(
                spriteStates,
                textStates,
                textMeshStates,
                lineStates,
                spineStates,
                0f);

            if (_bombExplosionFx != null) Destroy(_bombExplosionFx);
            _bombExplosionFx = null;
            if (_page != null && round == _page.RoundSeq && _page.IsDead())
                _page.ShowBombDeathUi();
        }

        SpineLite.SpineSprite SpawnBombExplosion()
        {
            if (_page == null || _page.Field == null || _page.Field.Container == null)
                return null;
            _bombExplosionFx = new GameObject("BombExplosionFx");
            _bombExplosionFx.transform.SetParent(_page.Field.Container, true);
            _bombExplosionFx.transform.position = BubbleField.D2W(
                BubbleField.ViewW * 0.5f,
                BubbleField.ViewH * 0.5f);
            var spine = _bombExplosionFx.AddComponent<SpineLite.SpineSprite>();
            spine.Load("bomb_explosion");
            if (spine.Data == null || !spine.HasAnimation("animation"))
            {
                Destroy(_bombExplosionFx);
                _bombExplosionFx = null;
                return null;
            }
            spine.SortingOrder = 650;
            if (spine.SetAnimation("animation", false) == null)
            {
                Destroy(_bombExplosionFx);
                _bombExplosionFx = null;
                return null;
            }
            return spine;
        }

        void CaptureBubbleFadeStates(
            List<SpriteFadeState> sprites,
            List<TextFadeState> texts,
            List<TextMeshFadeState> textMeshes,
            List<LineFadeState> lines,
            List<SpineFadeState> spines)
        {
            foreach (BubbleView bubble in Alive().ToList())
            {
                if (bubble == null) continue;
                foreach (SpriteRenderer renderer in
                         bubble.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sprites.Add(new SpriteFadeState
                    {
                        Renderer = renderer,
                        Color = renderer.color,
                    });
                }
                foreach (TMP_Text text in
                         bubble.GetComponentsInChildren<TMP_Text>(true))
                {
                    texts.Add(new TextFadeState
                    {
                        Text = text,
                        Color = text.color,
                    });
                }
                foreach (TextMesh text in
                         bubble.GetComponentsInChildren<TextMesh>(true))
                {
                    textMeshes.Add(new TextMeshFadeState
                    {
                        Text = text,
                        Color = text.color,
                    });
                }
                foreach (LineRenderer line in
                         bubble.GetComponentsInChildren<LineRenderer>(true))
                {
                    lines.Add(new LineFadeState
                    {
                        Line = line,
                        StartColor = line.startColor,
                        EndColor = line.endColor,
                    });
                }
                foreach (SpineLite.SpineSprite spine in
                         bubble.GetComponentsInChildren<SpineLite.SpineSprite>(true))
                {
                    spines.Add(new SpineFadeState
                    {
                        Spine = spine,
                        Color = spine.Tint,
                    });
                }
            }
        }

        static void ApplyFade(
            List<SpriteFadeState> sprites,
            List<TextFadeState> texts,
            List<TextMeshFadeState> textMeshes,
            List<LineFadeState> lines,
            List<SpineFadeState> spines,
            float alpha)
        {
            foreach (SpriteFadeState state in sprites)
            {
                if (state.Renderer == null) continue;
                Color color = state.Color;
                color.a *= alpha;
                state.Renderer.color = color;
            }
            foreach (TextFadeState state in texts)
            {
                if (state.Text == null) continue;
                Color color = state.Color;
                color.a *= alpha;
                state.Text.color = color;
            }
            foreach (TextMeshFadeState state in textMeshes)
            {
                if (state.Text == null) continue;
                Color color = state.Color;
                color.a *= alpha;
                state.Text.color = color;
            }
            foreach (LineFadeState state in lines)
            {
                if (state.Line == null) continue;
                Color start = state.StartColor;
                Color end = state.EndColor;
                start.a *= alpha;
                end.a *= alpha;
                state.Line.startColor = start;
                state.Line.endColor = end;
            }
            foreach (SpineFadeState state in spines)
            {
                if (state.Spine == null) continue;
                Color color = state.Color;
                color.a *= alpha;
                state.Spine.Tint = color;
            }
        }

        struct SpriteFadeState
        {
            public SpriteRenderer Renderer;
            public Color Color;
        }

        struct TextFadeState
        {
            public TMP_Text Text;
            public Color Color;
        }

        struct TextMeshFadeState
        {
            public TextMesh Text;
            public Color Color;
        }

        struct LineFadeState
        {
            public LineRenderer Line;
            public Color StartColor;
            public Color EndColor;
        }

        struct SpineFadeState
        {
            public SpineLite.SpineSprite Spine;
            public Color Color;
        }

        static string StickerKey(int imageId, IEnumerable<int> path)
        {
            return imageId + "|" + string.Join(",", path ?? Enumerable.Empty<int>());
        }
    }
}
