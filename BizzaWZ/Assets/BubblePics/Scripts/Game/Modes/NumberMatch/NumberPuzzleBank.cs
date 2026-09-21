using System;
using System.Collections.Generic;
using BubblePics.SpineLite;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Ordered local bank restored from 1.0.9. Simple and Hard cursors advance
    /// independently and wrap, matching NumberPuzzleBank.peek in Godot.
    /// </summary>
    public static class NumberPuzzleBank
    {
        public const string ResourcePath = "Config/number_puzzle_bank";

        static readonly List<NumberPuzzle> Simple = new List<NumberPuzzle>();
        static readonly List<NumberPuzzle> Hard = new List<NumberPuzzle>();
        static readonly Dictionary<string, NumberPuzzle> ById =
            new Dictionary<string, NumberPuzzle>(StringComparer.Ordinal);
        static readonly List<string> LoadErrors = new List<string>();
        static bool _loaded;
        static int _version;

        public static int Version
        {
            get { EnsureLoaded(); return _version; }
        }

        public static int Count
        {
            get { EnsureLoaded(); return ById.Count; }
        }

        public static int SimpleCount
        {
            get { EnsureLoaded(); return Simple.Count; }
        }

        public static int HardCount
        {
            get { EnsureLoaded(); return Hard.Count; }
        }

        public static IReadOnlyList<string> Errors
        {
            get { EnsureLoaded(); return LoadErrors; }
        }

        public static bool HasData
        {
            get { EnsureLoaded(); return ById.Count > 0; }
        }

        public static string TierKey(string difficultyType)
        {
            return string.Equals(
                difficultyType,
                "Hard",
                StringComparison.OrdinalIgnoreCase)
                ? NumberPuzzle.HardTier
                : NumberPuzzle.SimpleTier;
        }

        public static NumberPuzzle Peek(string difficultyType, int cursor)
        {
            EnsureLoaded();
            List<NumberPuzzle> pool = string.Equals(
                difficultyType,
                "Hard",
                StringComparison.OrdinalIgnoreCase)
                ? Hard
                : Simple;
            if (pool.Count == 0)
                pool = Simple.Count > 0 ? Simple : Hard;
            if (pool.Count == 0) return null;
            int index = ((cursor % pool.Count) + pool.Count) % pool.Count;
            return pool[index];
        }

        public static NumberPuzzle Get(string puzzleId)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(puzzleId) &&
                   ById.TryGetValue(puzzleId, out NumberPuzzle puzzle)
                ? puzzle
                : null;
        }

        public static IReadOnlyList<NumberPuzzle> Pool(string tier)
        {
            EnsureLoaded();
            return string.Equals(tier, NumberPuzzle.HardTier, StringComparison.Ordinal)
                ? (IReadOnlyList<NumberPuzzle>)Hard
                : Simple;
        }

        public static void ResetCacheForValidation()
        {
            _loaded = false;
            _version = 0;
            Simple.Clear();
            Hard.Clear();
            ById.Clear();
            LoadErrors.Clear();
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                LoadErrors.Add($"Resources/{ResourcePath}.json is missing or empty");
                return;
            }

            try
            {
                var root = MiniJson.Parse(asset.text) as Dictionary<string, object>;
                if (root == null)
                {
                    LoadErrors.Add("number puzzle bank root is not an object");
                    return;
                }

                _version = IntValue(root, "version");
                if (!(root.TryGetValue("puzzles", out object rawPuzzles) &&
                      rawPuzzles is List<object> puzzles))
                {
                    LoadErrors.Add("number puzzle bank has no puzzles array");
                    return;
                }

                for (int index = 0; index < puzzles.Count; index++)
                {
                    if (!(puzzles[index] is Dictionary<string, object> raw))
                    {
                        LoadErrors.Add($"puzzles[{index}] is not an object");
                        continue;
                    }

                    NumberPuzzle puzzle = ParsePuzzle(raw);
                    if (!NumberPuzzleValidator.Validate(puzzle, out List<string> errors))
                    {
                        for (int errorIndex = 0; errorIndex < errors.Count; errorIndex++)
                            LoadErrors.Add($"puzzles[{index}] {errors[errorIndex]}");
                        continue;
                    }

                    if (!ById.TryAdd(puzzle.id, puzzle))
                    {
                        LoadErrors.Add($"duplicate puzzle id '{puzzle.id}'");
                        continue;
                    }

                    if (string.Equals(puzzle.tier, NumberPuzzle.HardTier, StringComparison.Ordinal))
                        Hard.Add(puzzle);
                    else
                        Simple.Add(puzzle);
                }
            }
            catch (Exception ex)
            {
                LoadErrors.Add("cannot parse number puzzle bank: " + ex.Message);
            }

            if (LoadErrors.Count > 0)
                Debug.LogError("NumberPuzzleBank: " + string.Join("; ", LoadErrors));
            else
                Debug.Log($"[NumberPuzzleBank] loaded {Count} puzzles " +
                          $"(simple={Simple.Count} hard={Hard.Count})");
        }

        static NumberPuzzle ParsePuzzle(Dictionary<string, object> raw)
        {
            var puzzle = new NumberPuzzle
            {
                id = StringValue(raw, "id"),
                tier = StringValue(raw, "tier"),
                groups = ParseGroups(raw),
                waves = ParseWaves(raw),
            };
            return puzzle;
        }

        static NumberPuzzleGroup[] ParseGroups(Dictionary<string, object> raw)
        {
            if (!(raw.TryGetValue("groups", out object value) &&
                  value is List<object> source))
                return Array.Empty<NumberPuzzleGroup>();

            var groups = new NumberPuzzleGroup[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                if (!(source[i] is Dictionary<string, object> group))
                {
                    groups[i] = null;
                    continue;
                }

                groups[i] = new NumberPuzzleGroup
                {
                    result = IntValue(group, "result"),
                    formulas = StringArray(group, "formulas"),
                };
            }
            return groups;
        }

        static string[][] ParseWaves(Dictionary<string, object> raw)
        {
            if (!(raw.TryGetValue("waves", out object value) &&
                  value is List<object> source))
                return Array.Empty<string[]>();

            var waves = new string[source.Count][];
            for (int i = 0; i < source.Count; i++)
            {
                if (!(source[i] is List<object> wave))
                {
                    waves[i] = Array.Empty<string>();
                    continue;
                }
                waves[i] = ToStringArray(wave);
            }
            return waves;
        }

        static string[] StringArray(Dictionary<string, object> raw, string key)
        {
            return raw.TryGetValue(key, out object value) && value is List<object> list
                ? ToStringArray(list)
                : Array.Empty<string>();
        }

        static string[] ToStringArray(List<object> list)
        {
            var result = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                result[i] = list[i]?.ToString() ?? string.Empty;
            return result;
        }

        static string StringValue(Dictionary<string, object> raw, string key)
        {
            return raw.TryGetValue(key, out object value)
                ? value?.ToString() ?? string.Empty
                : string.Empty;
        }

        static int IntValue(Dictionary<string, object> raw, string key)
        {
            if (!raw.TryGetValue(key, out object value) || value == null) return 0;
            return Convert.ToInt32(value);
        }
    }
}
