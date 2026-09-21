using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Round-local formula mapping consumed by BubbleView and the mode adapter.
    /// IDs and slots are zero-based internally; emitted bubble tokens are the
    /// one-based group.slot representation used by BubbleFragment.
    /// </summary>
    public static class NumberMatchContent
    {
        static readonly Dictionary<int, string[]> FormulaByGroup =
            new Dictionary<int, string[]>();
        static readonly Dictionary<int, int> ResultByGroup =
            new Dictionary<int, int>();
        static readonly Dictionary<string, string> TokenByFormula =
            new Dictionary<string, string>(StringComparer.Ordinal);

        static NumberPuzzle _puzzle;

        public static bool IsActive => _puzzle != null;
        public static bool IsPureNumber => IsActive;
        public static int GroupCount => _puzzle?.GroupCount ?? 0;
        public static string PuzzleId => _puzzle?.id ?? string.Empty;

        public static IReadOnlyList<string> SetPuzzle(NumberPuzzle puzzle)
        {
            Clear();
            if (puzzle == null) return Array.Empty<string>();
            if (!NumberPuzzleValidator.Validate(puzzle, out List<string> errors))
            {
                Debug.LogError($"Cannot activate invalid number puzzle '{puzzle.id}': " +
                               string.Join("; ", errors));
                return Array.Empty<string>();
            }

            _puzzle = puzzle;
            var tokens = new List<string>(puzzle.GroupCount * NumberPuzzleValidator.GroupSize);
            for (int groupIndex = 0; groupIndex < puzzle.GroupCount; groupIndex++)
            {
                NumberPuzzleGroup group = puzzle.groups[groupIndex];
                FormulaByGroup[groupIndex] = (string[])group.formulas.Clone();
                ResultByGroup[groupIndex] = group.result;
                for (int slot = 0; slot < group.formulas.Length; slot++)
                {
                    string token = $"{groupIndex + 1}.{slot + 1}";
                    tokens.Add(token);
                    TokenByFormula[group.formulas[slot]] = token;
                }
            }
            return tokens;
        }

        public static void Clear()
        {
            _puzzle = null;
            FormulaByGroup.Clear();
            ResultByGroup.Clear();
            TokenByFormula.Clear();
        }

        public static bool IsNumberImage(int imageId)
        {
            return IsActive && FormulaByGroup.ContainsKey(imageId);
        }

        public static string FormulaFor(int imageId, int slot)
        {
            return FormulaByGroup.TryGetValue(imageId, out string[] formulas) &&
                   slot >= 0 && slot < formulas.Length
                ? formulas[slot]
                : string.Empty;
        }

        public static int ResultFor(int imageId)
        {
            return ResultByGroup.TryGetValue(imageId, out int result) ? result : 0;
        }

        public static string TokenForFormula(string formula)
        {
            return formula != null && TokenByFormula.TryGetValue(formula, out string token)
                ? token
                : string.Empty;
        }

        public static List<List<string>> BuildWaveTokens(NumberPuzzle puzzle, int seed)
        {
            IReadOnlyList<string> allTokens = SetPuzzle(puzzle);
            var result = new List<List<string>>();
            var random = new System.Random(seed);
            if (puzzle?.waves != null)
            {
                for (int waveIndex = 0; waveIndex < puzzle.waves.Length; waveIndex++)
                {
                    string[] formulas = puzzle.waves[waveIndex];
                    if (formulas == null || formulas.Length == 0) continue;
                    var tokens = new List<string>(formulas.Length);
                    for (int formulaIndex = 0; formulaIndex < formulas.Length; formulaIndex++)
                    {
                        string token = TokenForFormula(formulas[formulaIndex]);
                        if (token.Length > 0) tokens.Add(token);
                    }
                    Shuffle(tokens, random);
                    if (tokens.Count > 0) result.Add(tokens);
                }
            }

            if (result.Count == 0)
            {
                var fallback = new List<string>(allTokens);
                Shuffle(fallback, random);
                result.Add(fallback);
            }
            return result;
        }

        public static string BuildLayout(NumberPuzzle puzzle, int seed)
        {
            return string.Join(
                "|",
                BuildWaveTokens(puzzle, seed).Select(wave => string.Join(",", wave)));
        }

        public static bool IsComplete(IEnumerable<int> collectedImageIds)
        {
            if (!IsActive || collectedImageIds == null) return false;
            var found = new HashSet<int>();
            foreach (int imageId in collectedImageIds)
            {
                if (imageId >= 0 && imageId < GroupCount)
                    found.Add(imageId);
            }
            return found.Count == GroupCount;
        }

        public static Dictionary<int, HashSet<string>> FullLeafUniverse()
        {
            var result = new Dictionary<int, HashSet<string>>();
            for (int group = 0; group < GroupCount; group++)
            {
                result[group] = new HashSet<string>(
                    new[] { "0", "1", "2", "3" },
                    StringComparer.Ordinal);
            }
            return result;
        }

        public static float FormationCell(IReadOnlyCollection<int> quadrants, float radius, int count = -1)
        {
            int actual = count > 0 ? count : quadrants?.Count ?? 0;
            actual = Mathf.Clamp(actual, 1, 4);
            float factor = actual == 1 ? 0.7f : actual == 2 ? 0.5f : 0.42f;
            return 2f * radius * factor;
        }

        public static Vector2 FormationOffset(
            int quadrant,
            IReadOnlyCollection<int> quadrants,
            float cell)
        {
            var sorted = quadrants == null
                ? new List<int> { quadrant }
                : quadrants.OrderBy(value => value).ToList();
            int rank = sorted.IndexOf(quadrant);
            if (rank < 0) rank = 0;
            int count = Mathf.Clamp(sorted.Count, 1, 4);
            return FormationUnit(rank, count) * (cell * 0.5f);
        }

        public static int StableSeed(string puzzleId, int cursor, int globalLevel)
        {
            unchecked
            {
                int hash = 17;
                string value = puzzleId ?? string.Empty;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];
                hash = hash * 31 + cursor;
                hash = hash * 31 + globalLevel;
                return hash;
            }
        }

        static Vector2 FormationUnit(int rank, int count)
        {
            switch (count)
            {
                case 2:
                    return rank == 0 ? new Vector2(0f, -0.6f) : new Vector2(0f, 0.6f);
                case 3:
                    if (rank == 0) return new Vector2(0f, -1.1f);
                    return rank == 1
                        ? new Vector2(0.95f, 0.55f)
                        : new Vector2(-0.95f, 0.55f);
                case 4:
                    return new[]
                    {
                        new Vector2(-1f, -1f),
                        new Vector2(1f, -1f),
                        new Vector2(-1f, 1f),
                        new Vector2(1f, 1f),
                    }[Mathf.Clamp(rank, 0, 3)];
                default:
                    return Vector2.zero;
            }
        }

        static void Shuffle<T>(IList<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                (values[i], values[other]) = (values[other], values[i]);
            }
        }
    }
}
