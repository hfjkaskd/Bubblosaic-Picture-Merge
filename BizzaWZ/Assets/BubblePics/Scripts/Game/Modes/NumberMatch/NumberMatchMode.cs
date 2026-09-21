using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BubblePics.GameModes;
using UnityEngine;

namespace BubblePics
{
    public sealed class NumberMatchRoundDefinition
    {
        public NumberPuzzle Puzzle;
        public string Tier;
        public int Cursor;
        public int Seed;
        public List<List<string>> Waves;
        public string Layout;

        public int GroupCount => Puzzle?.GroupCount ?? 0;

        public Texture2D[] CreateTextureSlots()
        {
            return new Texture2D[GroupCount];
        }
    }

    /// <summary>
    /// Runtime adapter for the existing four-quadrant bubble merger. It owns
    /// puzzle activation and cursor advancement; Stage routing is handled by
    /// NumberMatchSelector before BubblePage starts the round.
    /// </summary>
    public sealed class NumberMatchMode : MonoBehaviour
    {
        static NumberMatchRoundDefinition _activeRound;

        public NumberMatchRoundDefinition Round { get; private set; }

        public static NumberMatchRoundDefinition ActiveRound => _activeRound;

        public bool BuildRound(LevelModeSelection selection, out string error)
        {
            error = string.Empty;
            if (selection == null || selection.Kind != GameplayKind.NumberMatch)
            {
                error = "selection is not Number Match";
                return false;
            }
            if (!(selection.Payload is NumberMatchRoundDefinition definition) ||
                definition.Puzzle == null)
            {
                error = "Number Match selection has no puzzle payload";
                return false;
            }

            Round = definition;
            _activeRound = definition;
            NumberMatchContent.SetPuzzle(definition.Puzzle);
            return NumberMatchContent.GroupCount == definition.GroupCount;
        }

        public void OnRoundWon()
        {
            if (Round == null) return;
            SaveState.AdvanceNumberPuzzleCursor(Round.Tier);
        }

        public static void Activate(NumberMatchRoundDefinition definition)
        {
            _activeRound = definition;
            NumberMatchContent.SetPuzzle(definition?.Puzzle);
        }

        public static void HandleRoundWon()
        {
            if (_activeRound == null) return;
            SaveState.AdvanceNumberPuzzleCursor(_activeRound.Tier);
            _activeRound = null;
        }

        public static void ClearActive()
        {
            _activeRound = null;
            NumberMatchContent.Clear();
        }

        public bool IsComplete(IEnumerable<int> collectedImageIds)
        {
            return Round != null && NumberMatchContent.IsComplete(collectedImageIds);
        }

        public void ResetMode()
        {
            Round = null;
            ClearActive();
        }

        void OnDestroy()
        {
            if (Round != null && NumberMatchContent.PuzzleId == Round.Puzzle.id)
                NumberMatchContent.Clear();
        }
    }

    /// <summary>
    /// Automatic rounds use the production 25+15n experiment schedule. Sparse
    /// Category/Tangram/Word manifest rounds have higher priority, so every
    /// enabled gameplay kind remains reachable without an AB assignment.
    /// </summary>
    public sealed class NumberMatchSelector : ILevelModeSelector
    {
        static readonly NumberMatchSelector Instance = new NumberMatchSelector();
        static bool _registered;

        public int Priority => 700;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterAtStartup()
        {
            EnsureRegistered();
            ModeLevelEntry.RegisterPreparationHandler(
                new NumberMatchPreparationHandler());
        }

        public static void EnsureRegistered()
        {
            if (_registered) return;
            LevelModeRouter.RegisterSelector(Instance);
            _registered = true;
        }

        public bool TrySelect(
            int globalLevel,
            bool automatic,
            out LevelModeSelection selection)
        {
            selection = null;
            if (globalLevel < 1 || !NumberPuzzleBank.HasData) return false;
            if (automatic)
            {
                if (!AppConfig.NumberMatchMode ||
                    !AppConfig.IsNumberMatchLevel(globalLevel))
                    return false;
            }

            if (!LevelRepo.TryGet(globalLevel, out LevelData level) || level == null)
                return false;

            string tier = NumberPuzzleBank.TierKey(level.difficulty_type);
            int cursor = SaveState.GetNumberPuzzleCursor(tier);
            NumberPuzzle puzzle = NumberPuzzleBank.Peek(level.difficulty_type, cursor);
            if (puzzle == null) return false;

            int seed = NumberMatchContent.StableSeed(puzzle.id, cursor, globalLevel);
            List<List<string>> waves = NumberMatchContent.BuildWaveTokens(puzzle, seed);
            string layout = JoinWaves(waves);
            var definition = new NumberMatchRoundDefinition
            {
                Puzzle = puzzle,
                Tier = tier,
                Cursor = cursor,
                Seed = seed,
                Waves = waves,
                Layout = layout,
            };

            var coordinate = new LevelCoordinate(globalLevel);
            selection = new LevelModeSelection
            {
                Kind = GameplayKind.NumberMatch,
                GlobalLevel = globalLevel,
                Chapter = coordinate.Chapter,
                LevelIndex = coordinate.LevelIndex,
                Layout = layout,
                StepLimit = level.step_limit,
                Source = NumberPuzzleBank.ResourcePath + ":" + puzzle.id,
                Payload = definition,
                IsAutomatic = automatic,
            };
            return true;
        }

        static string JoinWaves(IReadOnlyList<List<string>> waves)
        {
            var values = new string[waves.Count];
            for (int i = 0; i < waves.Count; i++)
                values[i] = string.Join(",", waves[i]);
            return string.Join("|", values);
        }
    }

    sealed class NumberMatchPreparationHandler : IModeLevelPreparationHandler
    {
        public GameplayKind Kind => GameplayKind.NumberMatch;

        public IEnumerator Prepare(
            LevelModeSelection selection,
            Action<ModePreparedLevel> completed,
            Action<string> failed,
            Action<float> progress)
        {
            if (!(selection?.Payload is NumberMatchRoundDefinition definition) ||
                definition.Puzzle == null)
            {
                failed?.Invoke("Number Match selection has no puzzle payload");
                yield break;
            }

            if (!LevelRepo.TryGet(selection.GlobalLevel, out LevelData source) ||
                source == null)
            {
                failed?.Invoke("Number Match base level metadata is missing");
                yield break;
            }

            int groups = definition.GroupCount;
            string[] identities = Enumerable.Range(0, groups)
                .Select(index => $"number:{definition.Puzzle.id}:{index}")
                .ToArray();
            var level = source.Clone();
            level.layout = definition.Layout;
            level.image_urls = identities;
            level.image_ids = Enumerable.Range(1, groups).ToArray();
            level.local_images = Array.Empty<string>();
            level.local_images_hd = Array.Empty<string>();
            level.local_image_files = Array.Empty<string>();
            level.local_image_files_hd = Array.Empty<string>();
            level.level_data_source = selection.Source;
            level.level_unique_id =
                $"NumberMatch:{definition.Puzzle.id}:{definition.Cursor}";

            LevelValidationResult validation = LevelValidator.Validate(
                level.layout,
                groups);
            if (!validation.IsValid)
            {
                failed?.Invoke("Number Match layout is invalid: " + validation);
                yield break;
            }

            NumberMatchMode.Activate(definition);
            progress?.Invoke(1f);
            completed?.Invoke(new ModePreparedLevel
            {
                Level = level,
                Textures = definition.CreateTextureSlots(),
            });
        }
    }
}
