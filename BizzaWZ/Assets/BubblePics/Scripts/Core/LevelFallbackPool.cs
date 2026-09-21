using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BubblePics
{
    public sealed class LevelFallbackEntry
    {
        public int ImageCount;
        public string Layout;
        public int Moves;
        public int SourceLevel;
    }

    /// <summary>
    /// Runtime equivalent of the restored BubbleFallbackPool. The original
    /// loads a compact, prevalidated fallback bank on first fallback use; it
    /// does not parse and validate the whole live catalog during startup.
    /// </summary>
    public static class LevelFallbackPool
    {
        static readonly Dictionary<int, List<LevelFallbackEntry>> Buckets = new();
        static bool _initialized;

        public static void Initialize(IEnumerable<LevelData> bundledLevels = null)
        {
            if (_initialized) return;
            _initialized = true;
            Buckets.Clear();

            TextAsset asset = Resources.Load<TextAsset>(
                "Levels/fallback_levels");
            if (asset != null && TryLoadCompactBank(asset.text)) return;

            // Compatibility fallback only for an older checkout that has not
            // copied the recovered bank. When the bank exists, keep its
            // authored 6-15 image-count coverage unchanged.
            if (bundledLevels == null) return;

            foreach (LevelData level in bundledLevels)
            {
                if (level == null) continue;
                int availableImages = LevelRepo.ImageCount(level);
                int imageCount = WaveScheduler.MaxImageIndex(level.layout ?? "");
                if (imageCount <= 0 || imageCount > availableImages) continue;
                LevelValidationResult validation =
                    LevelValidator.Validate(level.layout, imageCount);
                if (!validation.IsValid) continue;

                if (!Buckets.TryGetValue(imageCount, out var bucket))
                {
                    bucket = new List<LevelFallbackEntry>();
                    Buckets[imageCount] = bucket;
                }
                if (bucket.Exists(entry =>
                        string.Equals(entry.Layout, level.layout,
                            StringComparison.Ordinal)))
                    continue;
                bucket.Add(new LevelFallbackEntry
                {
                    ImageCount = imageCount,
                    Layout = level.layout,
                    Moves = Math.Max(1, level.step_limit),
                    SourceLevel = level.level,
                });
            }
        }

        static void EnsureLoaded()
        {
            if (!_initialized) Initialize();
        }

        static bool TryLoadCompactBank(string json)
        {
            Dictionary<string, object> root;
            try
            {
                root = SpineLite.MiniJson.Parse(json ?? string.Empty) as
                    Dictionary<string, object>;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "Invalid compact fallback bank: " + ex.Message);
                return false;
            }
            if (root == null) return false;

            foreach (KeyValuePair<string, object> pair in root)
            {
                if (!int.TryParse(
                        pair.Key,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int imageCount) ||
                    imageCount <= 0 ||
                    !(pair.Value is IList entries))
                {
                    continue;
                }

                var bucket = new List<LevelFallbackEntry>();
                int sourceIndex = 0;
                foreach (object raw in entries)
                {
                    sourceIndex++;
                    if (!(raw is Dictionary<string, object> entry))
                        continue;
                    string layout = StringValue(entry, "layout");
                    int moves = IntValue(entry, "moves", 1);
                    if (string.IsNullOrWhiteSpace(layout)) continue;
                    bucket.Add(new LevelFallbackEntry
                    {
                        ImageCount = imageCount,
                        Layout = layout,
                        Moves = Math.Max(1, moves),
                        SourceLevel = sourceIndex,
                    });
                }
                if (bucket.Count > 0) Buckets[imageCount] = bucket;
            }
            return Buckets.Count > 0;
        }

        static string StringValue(
            Dictionary<string, object> source,
            string key)
        {
            return source != null && source.TryGetValue(key, out object value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
                : "";
        }

        static int IntValue(
            Dictionary<string, object> source,
            string key,
            int fallback)
        {
            if (source == null || !source.TryGetValue(key, out object value) ||
                value == null)
                return fallback;
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static bool HasImageCount(int imageCount)
        {
            EnsureLoaded();
            return Buckets.TryGetValue(imageCount, out var bucket) &&
                   bucket.Count > 0;
        }

        public static int BestImageCount(int preferred, int available)
        {
            int start = preferred > 0
                ? Math.Min(preferred, available)
                : available;
            for (int count = start; count > 0; count--)
            {
                if (HasImageCount(count)) return count;
            }
            return 0;
        }

        public static bool TryPick(
            int imageCount,
            int stableSeed,
            out LevelFallbackEntry entry)
        {
            EnsureLoaded();
            entry = null;
            if (!Buckets.TryGetValue(imageCount, out var bucket) ||
                bucket.Count == 0)
            {
                return false;
            }

            // Stable selection avoids silently changing an in-progress level
            // every time the app restarts while retaining pool variety.
            int index = (stableSeed & int.MaxValue) % bucket.Count;
            entry = bucket[index];
            return true;
        }
    }
}
