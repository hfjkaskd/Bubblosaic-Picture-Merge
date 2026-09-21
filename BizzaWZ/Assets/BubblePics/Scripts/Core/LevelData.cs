using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BubblePics
{
    [Serializable]
    public class LevelData
    {
        public int chapter;
        public int level;
        public string difficulty_type;
        public string difficulty;
        public int step_limit;
        public int add_step;
        public int coins;
        public string level_user_tags;
        public string level_data_source;
        public string level_unique_id;
        public string layout;
        public int rows;
        public int cols;
        public int wave_count;
        public int[] wave_sizes;
        public int token_count;
        public int[] image_ids;
        public int max_depth;
        public string[] image_urls;
        public string[] local_images;
        public string[] local_images_hd;
        public string[] local_image_files;
        public string[] local_image_files_hd;

        public int DistinctImageCount() => LevelRepo.ImageCount(this);

        public LevelData Clone()
        {
            return new LevelData
            {
                chapter = chapter,
                level = level,
                difficulty_type = difficulty_type,
                difficulty = difficulty,
                step_limit = step_limit,
                add_step = add_step,
                coins = coins,
                level_user_tags = level_user_tags,
                level_data_source = level_data_source,
                level_unique_id = level_unique_id,
                layout = layout,
                rows = rows,
                cols = cols,
                wave_count = wave_count,
                wave_sizes = CloneArray(wave_sizes),
                token_count = token_count,
                image_ids = CloneArray(image_ids),
                max_depth = max_depth,
                image_urls = CloneArray(image_urls),
                local_images = CloneArray(local_images),
                local_images_hd = CloneArray(local_images_hd),
                local_image_files = CloneArray(local_image_files),
                local_image_files_hd = CloneArray(local_image_files_hd),
            };
        }

        /// <summary>
        /// Replaces only round content while retaining the requested
        /// chapter/level/difficulty identity.
        /// </summary>
        public void CopyPlayableContentFrom(LevelData source)
        {
            if (source == null) return;
            layout = source.layout;
            step_limit = source.step_limit;
            rows = source.rows;
            cols = source.cols;
            wave_count = source.wave_count;
            wave_sizes = CloneArray(source.wave_sizes);
            token_count = source.token_count;
            image_ids = CloneArray(source.image_ids);
            max_depth = source.max_depth;
            image_urls = CloneArray(source.image_urls);
            local_images = CloneArray(source.local_images);
            local_images_hd = CloneArray(source.local_images_hd);
            local_image_files = CloneArray(source.local_image_files);
            local_image_files_hd = CloneArray(source.local_image_files_hd);
            level_data_source = source.level_data_source;
        }

        static T[] CloneArray<T>(T[] source)
        {
            return source == null ? null : (T[])source.Clone();
        }
    }

    [Serializable]
    public class ChapterData
    {
        public LevelData[] levels;
    }

    public sealed class LevelResolveResult
    {
        public bool IsValid;
        public bool UsedFallback;
        public int RequestedImageCount;
        public int EffectiveImageCount;
        public int FallbackSourceLevel;
        public string Reason;
        public LevelValidationResult Validation;
    }

    public static class LevelRepo
    {
        static ChapterData _chapter;
        static int _loadedChapter;
        static Dictionary<int, LevelData> _resolved;
        static Dictionary<int, LevelResolveResult> _resolveReports;

        static void EnsureLoaded(int levelHint = 1)
        {
            int chapter = ChapterForLevel(levelHint);
            if (_chapter != null && _loadedChapter == chapter) return;

            // Match the restored ChapterRepo: load only the requested 25-level
            // chapter. The flattened 1800-level catalog stays an editor/import
            // artifact and is never parsed on the player startup path.
            var txt = Resources.Load<TextAsset>($"Levels/chapter_{chapter}");
            if (txt == null)
            {
                Debug.LogError(
                    $"Missing level chapter: Resources/Levels/chapter_{chapter}.json");
                _chapter = new ChapterData { levels = Array.Empty<LevelData>() };
                _loadedChapter = chapter;
                return;
            }
            _chapter = JsonUtility.FromJson<ChapterData>(txt.text)
                ?? new ChapterData { levels = Array.Empty<LevelData>() };
            if (_chapter.levels == null)
                _chapter.levels = Array.Empty<LevelData>();

            Array.Sort(_chapter.levels, (a, b) =>
            {
                int al = a != null ? a.level : int.MaxValue;
                int bl = b != null ? b.level : int.MaxValue;
                return al.CompareTo(bl);
            });

            int expectedFirst = (chapter - 1) * 25 + 1;
            if (_chapter.levels.Length > 25 && chapter > 1)
            {
                _chapter.levels = _chapter.levels
                    .Where(level => level != null && level.chapter == chapter)
                    .ToArray();
            }
            for (int i = 0; i < _chapter.levels.Length; i++)
            {
                LevelData level = _chapter.levels[i];
                if (level == null || level.level != expectedFirst + i)
                {
                    Debug.LogError(
                        $"Level chapter must be contiguous: expected {expectedFirst + i}, " +
                        $"found {(level == null ? "null" : level.level.ToString())}");
                    _chapter = new ChapterData { levels = Array.Empty<LevelData>() };
                    _loadedChapter = chapter;
                    return;
                }
            }

            _loadedChapter = chapter;
            _resolved = new Dictionary<int, LevelData>();
            _resolveReports = new Dictionary<int, LevelResolveResult>();
            LevelFallbackPool.Initialize(_chapter.levels);
        }

        public static int Count
        {
            get
            {
                // Imported normalized content is 72 fixed 25-level chapters.
                // Avoid parsing the 6.2 MB flattened catalog merely to answer
                // a bound check or plan background prefetch.
                return Math.Max(
                    1800,
                    ChapterMetadataRepository.HighestKnownGlobalLevel);
            }
        }

        public static bool HasLevel(int level)
        {
            EnsureLoaded(level);
            if (level < 1) return false;
            if (FindLoaded(level) != null) return true;
            ChapterMetadataRepository.ToChapterLevel(
                level, out int chapter, out int levelIndex);
            return ChapterMetadataRepository.TryGetCandidates(
                chapter, levelIndex, out _);
        }

        public static bool TryGet(int level, out LevelData data)
        {
            EnsureLoaded(level);
            if (!HasLevel(level))
            {
                data = null;
                return false;
            }

            if (!_resolved.TryGetValue(level, out LevelData cached))
            {
                LevelData source = FindLoaded(level);
                ChapterMetadataRepository.ToChapterLevel(
                    level, out int chapter, out int levelIndex);
                if (ChapterMetadataRepository.TryCreateSelectedLevel(
                        chapter,
                        levelIndex,
                        source,
                        out LevelData selected))
                {
                    source = selected;
                }
                if (!TryResolvePlayable(
                        source,
                        out LevelData resolved,
                        out LevelResolveResult report))
                {
                    Debug.LogError(
                        $"Level {level} is not playable: {report?.Reason ?? "unknown error"}");
                    data = null;
                    return false;
                }
                _resolved[level] = resolved;
                _resolveReports[level] = report;
                if (report.UsedFallback)
                {
                    Debug.LogWarning(
                        $"Level {level} uses bundled fallback layout " +
                        $"from level {report.FallbackSourceLevel}: {report.Reason}");
                }
                cached = resolved;
            }
            data = cached.Clone();
            return true;
        }

        public static LevelData Get(int level)
        {
            EnsureLoaded(level);
            if (Count == 0) return null;
            int bounded = Mathf.Clamp(level, 1, Count);
            return TryGet(bounded, out LevelData data) ? data : null;
        }

        public static bool TryGetResolveReport(
            int level,
            out LevelResolveResult report)
        {
            report = null;
            if (!TryGet(level, out _)) return false;
            return _resolveReports.TryGetValue(level, out report) &&
                   report != null;
        }

        /// <summary>
        /// Called when chapter metadata or user tags change. The bundled
        /// fallback pool is retained; only selected/resolved level instances
        /// are rebuilt on demand.
        /// </summary>
        public static void InvalidateCandidateSelections()
        {
            _resolved?.Clear();
            _resolveReports?.Clear();
        }

        public static bool TryResolvePlayable(
            LevelData source,
            out LevelData resolved,
            out LevelResolveResult report)
        {
            resolved = null;
            report = new LevelResolveResult();
            if (source == null)
            {
                report.Reason = "level data is null";
                return false;
            }

            int availableImages = ImageCount(source);
            report.RequestedImageCount = availableImages;
            if (availableImages <= 0)
            {
                report.Reason = "level has no image sources";
                return false;
            }

            int referencedImages = WaveScheduler.MaxImageIndex(source.layout ?? "");
            if (referencedImages > 0 && referencedImages < availableImages)
            {
                resolved = TrimImages(source.Clone(), referencedImages);
                report.Reason =
                    $"trimmed {availableImages - referencedImages} unused image sources";
                availableImages = referencedImages;
            }
            else
            {
                resolved = source.Clone();
            }

            LevelValidationResult validation =
                LevelValidator.Validate(resolved.layout, availableImages);
            report.Validation = validation;
            report.EffectiveImageCount = availableImages;
            if (validation.IsValid)
            {
                report.IsValid = true;
                return true;
            }

            int fallbackImages =
                LevelFallbackPool.BestImageCount(availableImages, availableImages);
            int stableSeed = StableSeed(source);
            if (fallbackImages <= 0 ||
                !LevelFallbackPool.TryPick(
                    fallbackImages,
                    stableSeed,
                    out LevelFallbackEntry fallback))
            {
                report.Reason =
                    "layout invalid and no compatible fallback exists: " +
                    validation;
                resolved = null;
                return false;
            }

            resolved = TrimImages(source.Clone(), fallbackImages);
            ApplyFallbackLayout(resolved, fallback, "layout_fallback");
            report.IsValid = true;
            report.UsedFallback = true;
            report.EffectiveImageCount = fallbackImages;
            report.FallbackSourceLevel = fallback.SourceLevel;
            report.Reason = validation.ToString();
            return true;
        }

        public static bool TryCreateOfflineFallback(
            LevelData requested,
            IReadOnlyList<LevelData> localImageSources,
            out LevelData fallback,
            out string reason)
        {
            fallback = null;
            reason = "";
            if (requested == null || localImageSources == null ||
                localImageSources.Count == 0)
            {
                reason = "no local images are available";
                return false;
            }

            int preferred = ImageCount(requested);
            int count = LevelFallbackPool.BestImageCount(
                preferred,
                localImageSources.Count);
            if (count <= 0 ||
                !LevelFallbackPool.TryPick(
                    count,
                    StableSeed(requested),
                    out LevelFallbackEntry entry))
            {
                reason =
                    $"fallback pool has no layout for {localImageSources.Count} local images";
                return false;
            }

            fallback = requested.Clone();
            fallback.image_urls = new string[count];
            fallback.local_images = new string[count];
            fallback.local_images_hd = new string[count];
            fallback.local_image_files = new string[count];
            fallback.local_image_files_hd = new string[count];
            fallback.image_ids = new int[count];

            for (int i = 0; i < count; i++)
            {
                LevelData donor = localImageSources[i];
                int donorCount = ImageCount(donor);
                if (donor == null || donorCount <= 0)
                {
                    reason = $"local fallback image {i + 1} is invalid";
                    fallback = null;
                    return false;
                }

                int slotSeed = unchecked(StableSeed(requested) + i * 397);
                int slot = (slotSeed & int.MaxValue) % donorCount;
                fallback.image_urls[i] = At(donor.image_urls, slot);
                fallback.local_images[i] = At(donor.local_images, slot);
                fallback.local_images_hd[i] = At(donor.local_images_hd, slot);
                fallback.local_image_files[i] = At(donor.local_image_files, slot);
                fallback.local_image_files_hd[i] = At(donor.local_image_files_hd, slot);
                fallback.image_ids[i] = i + 1;
            }

            ApplyFallbackLayout(fallback, entry, "offline_fallback");
            reason =
                $"using {count} bundled images and fallback layout from " +
                $"level {entry.SourceLevel}";
            return true;
        }

        public static IReadOnlyList<LevelData> BundledLevels()
        {
            EnsureLoaded(SaveState.CurrentLevel);
            var copy = new LevelData[_chapter.levels.Length];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = _chapter.levels[i]?.Clone();
            return copy;
        }

        static int ChapterForLevel(int level)
        {
            return (Math.Max(1, level) - 1) / 25 + 1;
        }

        static LevelData FindLoaded(int level)
        {
            int index = level - ((_loadedChapter - 1) * 25 + 1);
            return index >= 0 && index < (_chapter?.levels?.Length ?? 0)
                ? _chapter.levels[index]
                : null;
        }

        public static int ImageCount(LevelData level)
        {
            if (level == null) return 0;
            return Math.Max(
                Math.Max(
                    Math.Max(Length(level.image_urls), Length(level.image_ids)),
                    Math.Max(Length(level.local_images), Length(level.local_images_hd))),
                Math.Max(
                    Length(level.local_image_files),
                    Length(level.local_image_files_hd)));
        }

        /// <summary>Parse layout string into waves of tokens: "a,b|c" -> [[a,b],[c]]</summary>
        public static List<List<string>> ParseLayout(string layout)
        {
            var waves = new List<List<string>>();
            foreach (var w in (layout ?? "").Split('|'))
            {
                var tokens = new List<string>();
                foreach (var t in w.Split(','))
                {
                    foreach (var chip in t.Split('+'))
                    {
                        var tt = chip.Trim();
                        if (tt.Length > 0) tokens.Add(tt);
                    }
                }
                waves.Add(tokens); // empty waves allowed (timing separators)
            }
            return waves;
        }

        static void ApplyFallbackLayout(
            LevelData target,
            LevelFallbackEntry entry,
            string source)
        {
            target.layout = entry.Layout;
            target.step_limit = entry.Moves;
            target.level_data_source =
                string.IsNullOrEmpty(target.level_data_source)
                    ? source
                    : target.level_data_source + "+" + source;
            var scheduler = new WaveScheduler();
            scheduler.Build(target.layout, entry.ImageCount);
            target.wave_count = scheduler.WaveCount();
            target.wave_sizes = scheduler.WaveSizes().ToArray();
            target.token_count = scheduler.TotalTokenCount();
            target.max_depth = 0;
            foreach (int depth in WaveScheduler.ImageMaxDepths(target.layout).Values)
                target.max_depth = Math.Max(target.max_depth, depth);
        }

        static LevelData TrimImages(LevelData level, int count)
        {
            level.image_urls = Trim(level.image_urls, count);
            level.local_images = Trim(level.local_images, count);
            level.local_images_hd = Trim(level.local_images_hd, count);
            level.local_image_files = Trim(level.local_image_files, count);
            level.local_image_files_hd = Trim(level.local_image_files_hd, count);
            level.image_ids = Trim(level.image_ids, count);
            return level;
        }

        static T[] Trim<T>(T[] values, int count)
        {
            if (values == null) return null;
            if (values.Length <= count) return (T[])values.Clone();
            var trimmed = new T[count];
            Array.Copy(values, trimmed, count);
            return trimmed;
        }

        static int Length<T>(T[] values)
        {
            return values?.Length ?? 0;
        }

        static string At(string[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length
                ? values[index] ?? ""
                : "";
        }

        static int StableSeed(LevelData level)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (level?.chapter ?? 0);
                hash = hash * 31 + (level?.level ?? 0);
                string unique = level?.level_unique_id ?? "";
                for (int i = 0; i < unique.Length; i++)
                    hash = hash * 31 + unique[i];
                return hash;
            }
        }
    }
}
