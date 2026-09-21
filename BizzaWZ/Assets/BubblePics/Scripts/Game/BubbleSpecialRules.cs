using System;
using System.Collections.Generic;
using System.Linq;
using BubblePics.GameModes;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Pure rules recovered from the 1.0.9 Godot plugins. Keeping selection
    /// and scheduling free of scene state makes AB branches reproducible in
    /// editor validation and prevents visual code from changing gameplay.
    /// </summary>
    public static class BubbleSpecialRules
    {
        public const int LockedStartLevel = 4;
        public const int LockedInterval = 2;
        public const int RainbowMinPuzzleCount = 10;
        public const int RainbowMaxCollectedChecks = 6;
        public const float RainbowChance = 0.11f;
        public const int StepIncreaseInitialDelta = -5;
        public const int StepIncreaseStreakTarget = 6;
        public const int StepIncreaseBonus = 1;
        public const int DailyFirstStepBonus = 3;
        public const int StarfishTotal = 3;
        public const int LuckyBreakMinHistoryLevel = 10;
        public const float LuckyBreakHighCoefficient = 1.5f;
        public const int LuckyBreakHighCoefficientMinCount = 2;

        static readonly string[] StickerShapes =
        {
            "acorn", "bear", "beetle", "butterfly", "car", "cat",
            "flower", "four_clover", "pumpkin", "shell", "windmill",
        };

        public readonly struct BombPlan
        {
            public readonly string TokenKey;
            public readonly int Count;

            public BombPlan(string tokenKey, int count)
            {
                TokenKey = tokenKey;
                Count = count;
            }
        }

        public readonly struct LockPlan
        {
            public readonly string TokenKey;
            public readonly int Count;

            public LockPlan(string tokenKey, int count)
            {
                TokenKey = tokenKey;
                Count = count;
            }
        }

        public readonly struct StickerAssignment
        {
            public readonly int ImageId;
            public readonly IReadOnlyList<int> Path;
            public readonly int ShapeId;

            public StickerAssignment(int imageId, IReadOnlyList<int> path, int shapeId)
            {
                ImageId = imageId;
                Path = path;
                ShapeId = shapeId;
            }
        }

        public static bool IsPeriodicLevel(int level, int start, int interval)
        {
            return start > 0 && interval > 0 && level >= start &&
                   (level - start) % interval == 0;
        }

        public static bool IsLockedLevel(int level)
        {
            return IsPeriodicLevel(level, LockedStartLevel, LockedInterval);
        }

        /// <summary>
        /// Sticker Puzzle replaces the visible picture piece with an opaque
        /// shape. Category/word/number/tangram/bonus rounds already replace
        /// that picture semantic, so the two renderers cannot be stacked.
        /// </summary>
        public static bool AllowsStickerPuzzle(GameplayKind kind)
        {
            return kind == GameplayKind.Main;
        }

        public static bool IsStickerPuzzleEnabled(
            int level,
            int start,
            int interval,
            bool isHard,
            bool jigsawChipEnabled,
            GameplayKind kind)
        {
            if (!AllowsStickerPuzzle(kind) || isHard || jigsawChipEnabled ||
                start <= 0 || level < start)
                return false;
            return interval <= 0 || IsPeriodicLevel(level, start, interval);
        }

        public static List<BombPlan> SelectBombs(
            IReadOnlyList<string> firstWave,
            int puzzleCount,
            bool timeMode,
            int seed)
        {
            var groups = GroupTokens(firstWave);
            var complete = groups.Values
                .Where(group => group.Quadrants.Count >= 4)
                .OrderBy(group => StableHash(group.Key, seed))
                .ToList();
            if (complete.Count == 0 || puzzleCount <= 1)
                return new List<BombPlan>();

            bool single = complete.Count < 2 || puzzleCount < 10;
            int take = single ? 1 : 2;
            var result = new List<BombPlan>(take);
            for (int i = 0; i < take; i++)
            {
                TokenGroup group = complete[i];
                string token = group.Tokens
                    .OrderBy(value => StableHash(value, seed + 31 * i))
                    .First();
                int count = timeMode
                    ? (i == 0 ? 30 : 40)
                    : (i == 0 ? 8 : 12);
                result.Add(new BombPlan(TokenKey(token), count));
            }
            return result;
        }

        public static LockPlan? SelectLock(
            IReadOnlyList<string> wave,
            IEnumerable<BubbleView> board,
            ISet<string> bombReserved,
            int seed,
            ISet<string> allowedGroupKeys = null)
        {
            var counts = new Dictionary<string, int>();
            var waveGroups = GroupTokens(wave);
            foreach (BubbleView bubble in board ?? Enumerable.Empty<BubbleView>())
            {
                if (bubble == null || bubble.Fragment == null ||
                    bubble.State != BubbleState.Alive)
                    continue;
                string key = GroupKey(bubble.Fragment);
                counts[key] = counts.TryGetValue(key, out int value) ? value + 1 : 1;
            }
            foreach (TokenGroup group in waveGroups.Values)
                counts[group.Key] = counts.TryGetValue(group.Key, out int value)
                    ? value + group.Tokens.Count
                    : group.Tokens.Count;

            int redundantMergeCount = counts.Values.Sum(value => Mathf.Max(0, value - 1));
            int lockMax = redundantMergeCount - 1;
            if (lockMax < 3) return null;
            int lockMin = Mathf.Max(redundantMergeCount - 4, 3);

            TokenGroup selected = waveGroups.Values
                .Where(group => counts.TryGetValue(group.Key, out int count) &&
                                (count == 3 || count == 4))
                .Where(group => allowedGroupKeys == null ||
                                allowedGroupKeys.Contains(group.Key))
                .Where(group => !(board ?? Enumerable.Empty<BubbleView>()).Any(
                    bubble => bubble != null && bubble.IsLocked &&
                              bubble.Fragment != null && GroupKey(bubble.Fragment) == group.Key))
                .Where(group => group.Tokens.Any(value =>
                    bombReserved == null ||
                    !bombReserved.Contains(TokenKey(value))))
                .OrderBy(group => StableHash(group.Key, seed))
                .FirstOrDefault();
            if (selected == null) return null;

            string token = selected.Tokens
                .Where(value => bombReserved == null || !bombReserved.Contains(TokenKey(value)))
                .OrderBy(value => StableHash(value, seed + 17))
                .FirstOrDefault();
            if (string.IsNullOrEmpty(token)) return null;
            int span = Mathf.Max(1, lockMax - lockMin + 1);
            int countValue = lockMin + Mathf.Abs(StableHash(selected.Key, seed)) % span;
            return new LockPlan(TokenKey(token), countValue);
        }

        public static int[] ComputeStarfishWavePlan(int waveCount, int total = StarfishTotal)
        {
            if (waveCount <= 0 || total <= 0) return Array.Empty<int>();
            int qualified = Mathf.Max(1, Mathf.CeilToInt(waveCount * 2f / 3f));
            var result = new int[qualified];
            for (int k = 0; k < total; k++)
            {
                int index = Mathf.Clamp(
                    Mathf.FloorToInt((k + 0.5f) * qualified / total),
                    0, qualified - 1);
                if (result[index] >= 1)
                {
                    index = Enumerable.Range(0, qualified)
                        .OrderBy(i => Mathf.Abs(i - index))
                        .FirstOrDefault(i => result[i] < 1);
                }
                result[index]++;
            }
            return result;
        }

        public static List<StickerAssignment> AssignStickers(
            IEnumerable<IEnumerable<string>> waves,
            int imageCount)
        {
            var pathsByImage = new Dictionary<int, List<List<int>>>();
            foreach (IEnumerable<string> wave in waves ?? Enumerable.Empty<IEnumerable<string>>())
            {
                foreach (string token in wave ?? Enumerable.Empty<string>())
                {
                    BubbleFragment fragment = BubbleFragment.FromToken(token);
                    if (fragment == null || fragment.ImageId < 0 ||
                        fragment.ImageId >= imageCount)
                        continue;
                    if (!pathsByImage.TryGetValue(fragment.ImageId, out var paths))
                    {
                        paths = new List<List<int>>();
                        pathsByImage[fragment.ImageId] = paths;
                    }
                    paths.AddRange(fragment.HeldPaths.Select(path => new List<int>(path)));
                }
            }

            var result = new List<StickerAssignment>();
            int shape = 0;
            for (int imageId = 0; imageId < imageCount; imageId += 2)
            {
                if (!pathsByImage.TryGetValue(imageId, out var paths)) continue;
                List<int> donor = paths
                    .Where(path => path.Count == 1)
                    .OrderBy(path => path[0])
                    .FirstOrDefault(path => !paths.Any(other =>
                        other.Count > path.Count && IsPrefix(path, other)));
                if (donor == null) continue;
                result.Add(new StickerAssignment(
                    imageId, donor, shape % StickerShapes.Length));
                shape++;
            }
            return result;
        }

        public static bool IsDiagonalPerfect(BubbleFragment first, BubbleFragment second)
        {
            if (first == null || second == null) return false;
            List<int> a = first.LastQuadrants();
            List<int> b = second.LastQuadrants();
            return IsDiagonalPair(a) && IsDiagonalPair(b) &&
                   BubbleFragment.CanMerge(first, second) &&
                   a.Union(b).Distinct().Count() == 4;
        }

        public static int ApplyStepIncreaseInitial(int steps, bool enabled)
        {
            return enabled ? Mathf.Max(0, steps + StepIncreaseInitialDelta) : steps;
        }

        public static bool ShouldConsumeStep(bool success, bool errorOnly)
        {
            return !errorOnly || !success;
        }

        public static bool LuckyBreakQualifies(
            int completedLevel,
            int dayLevelNumber,
            int targetNumber,
            IEnumerable<float> coefficients)
        {
            return completedLevel > LuckyBreakMinHistoryLevel &&
                   dayLevelNumber == targetNumber &&
                   (coefficients ?? Enumerable.Empty<float>()).Count(
                       value => value >= LuckyBreakHighCoefficient) >=
                   LuckyBreakHighCoefficientMinCount;
        }

        public static string StickerShapeName(int shapeId)
        {
            return StickerShapes[Mathf.Abs(shapeId) % StickerShapes.Length];
        }

        public static string TokenKey(string token)
        {
            BubbleFragment fragment = BubbleFragment.FromToken(token);
            return fragment == null ? "" : TokenKey(fragment);
        }

        public static string TokenKey(BubbleFragment fragment)
        {
            if (fragment == null) return "";
            return string.Join(",", fragment.HeldPaths
                .Select(path => (fragment.ImageId + 1) +
                    string.Concat(path.Select(value => "." + (value + 1))))
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        public static string GroupKey(BubbleFragment fragment)
        {
            if (fragment == null) return "";
            return fragment.ImageId + "|" + string.Join(",", fragment.ParentPath());
        }

        static bool IsDiagonalPair(IReadOnlyList<int> quadrants)
        {
            return quadrants != null && quadrants.Count == 2 &&
                   quadrants[0] + quadrants[1] == 3;
        }

        static bool IsPrefix(IReadOnlyList<int> prefix, IReadOnlyList<int> path)
        {
            if (prefix.Count > path.Count) return false;
            for (int i = 0; i < prefix.Count; i++)
                if (prefix[i] != path[i]) return false;
            return true;
        }

        static Dictionary<string, TokenGroup> GroupTokens(IEnumerable<string> tokens)
        {
            var result = new Dictionary<string, TokenGroup>();
            foreach (string token in tokens ?? Enumerable.Empty<string>())
            {
                BubbleFragment fragment = BubbleFragment.FromToken(token);
                if (fragment == null) continue;
                string key = GroupKey(fragment);
                if (!result.TryGetValue(key, out TokenGroup group))
                {
                    group = new TokenGroup(key);
                    result[key] = group;
                }
                group.Tokens.Add(token);
                foreach (int quadrant in fragment.LastQuadrants())
                    group.Quadrants.Add(quadrant);
            }
            return result;
        }

        static int StableHash(string value, int seed)
        {
            unchecked
            {
                uint hash = 2166136261u ^ (uint)seed;
                foreach (char ch in value ?? "")
                {
                    hash ^= ch;
                    hash *= 16777619u;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        sealed class TokenGroup
        {
            public readonly string Key;
            public readonly List<string> Tokens = new();
            public readonly HashSet<int> Quadrants = new();

            public TokenGroup(string key)
            {
                Key = key;
            }
        }
    }
}
