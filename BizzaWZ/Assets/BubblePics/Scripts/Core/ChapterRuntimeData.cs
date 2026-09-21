using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace BubblePics
{
    [Serializable]
    public sealed class ChapterApiResponse
    {
        public ChapterMetadata[] data;
        public ChapterAbInfo ab_info;
    }

    [Serializable]
    public sealed class ChapterAbInfo
    {
        public string[] ab_all_tags;
    }

    [Serializable]
    public sealed class ChapterMetadata
    {
        public string imageUrl;
        public string thumbnailUrl;
        public string mp4Url;
        public int chapterIndex;
        public int chapterNumber;
        public ChapterLevelCandidate[] levels;
        public string chapterDataSource;
    }

    /// <summary>
    /// Exact DTO for one entry in the production chapter API. There may be
    /// several entries with the same levelIndex; levelIndex, rather than array
    /// position, is the authoritative slot inside a 25-level chapter.
    /// </summary>
    [Serializable]
    public sealed class ChapterLevelCandidate
    {
        public int levelIndex;
        public string[] imageUrls;
        public string animation;
        public string difficultyType;
        public string difficulty;
        public string initialLayout;
        public string levelUserTags;
        public int stepLimit;
        public int addStep;
        public int coins;
        public string levelDataSource;
        public string levelUniqueId;
    }

    /// <summary>
    /// Matches chapter_repo/level_user_tag_matcher.gd. A default candidate is
    /// deliberately not considered a tag match; it is only the fallback after
    /// all tagged candidates have been checked in server order.
    /// </summary>
    public static class ChapterLevelTagMatcher
    {
        public static bool IsDefault(string expression)
        {
            string value = (expression ?? "").Trim();
            return value.Length == 0 || string.Equals(
                value, "default", StringComparison.OrdinalIgnoreCase);
        }

        public static bool Matches(
            string expression,
            IReadOnlyDictionary<string, string[]> userTags)
        {
            if (IsDefault(expression) || userTags == null) return false;
            string text = expression ?? "";
            int colon = text.IndexOf(':');
            if (colon <= 0) return false;

            string key = text.Substring(0, colon).Trim().ToLowerInvariant();
            if (key.Length == 0 || !TryGetTag(userTags, key, out string[] actual))
                return false;

            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string entry in text.Substring(colon + 1).Split(','))
            {
                string value = (entry ?? "").Trim();
                if (value.Length > 0) wanted.Add(value);
            }
            if (wanted.Count == 0 || actual == null) return false;
            return actual.Any(value => wanted.Contains((value ?? "").Trim()));
        }

        static bool TryGetTag(
            IReadOnlyDictionary<string, string[]> tags,
            string normalizedKey,
            out string[] values)
        {
            if (tags.TryGetValue(normalizedKey, out values)) return true;
            foreach (KeyValuePair<string, string[]> pair in tags)
            {
                if (string.Equals(
                        (pair.Key ?? "").Trim(),
                        normalizedKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    values = pair.Value;
                    return true;
                }
            }
            values = null;
            return false;
        }
    }

    /// <summary>
    /// Dynamic chapter metadata/cache and candidate selector. It consumes the
    /// original encrypted chapter DTO directly and leaves image transport to
    /// BubblePicsRemoteImageDelivery, including its absolute-URL fallback.
    /// No terminal chapter number is compiled into this repository.
    /// </summary>
    public static class ChapterMetadataRepository
    {
        const int LevelsPerChapter = 25;
        const string BundledChapterPrefix = "Chapters/chapter_";
        const string CacheDirectoryName = "chapter_meta";

        static readonly Dictionary<int, ChapterMetadata> Chapters = new();
        static readonly Dictionary<int, Dictionary<int, ChapterLevelCandidate[]>>
            Candidates = new();
        static readonly HashSet<int> ConfirmedEmpty = new();
        static readonly Dictionary<string, string[]> UserTags = new(
            StringComparer.OrdinalIgnoreCase);

        static bool _initialized;

        public static event Action EndStateChanged;

        public static IReadOnlyCollection<int> KnownChapterIndices
        {
            get
            {
                EnsureInitialized();
                return Chapters.Keys.OrderBy(value => value).ToArray();
            }
        }

        public static int HighestKnownGlobalLevel
        {
            get
            {
                EnsureInitialized();
                int highest = 0;
                foreach (KeyValuePair<int, Dictionary<int, ChapterLevelCandidate[]>>
                             chapter in Candidates)
                {
                    if (chapter.Value.Count == 0) continue;
                    int index = chapter.Value.Keys.Max();
                    highest = Math.Max(
                        highest,
                        ToGlobalLevel(chapter.Key, index));
                }
                return highest;
            }
        }

        public static void SetUserTags(
            IReadOnlyDictionary<string, string[]> tags)
        {
            UserTags.Clear();
            if (tags == null) return;
            foreach (KeyValuePair<string, string[]> pair in tags)
            {
                string key = (pair.Key ?? "").Trim().ToLowerInvariant();
                if (key.Length == 0) continue;
                UserTags[key] = (pair.Value ?? Array.Empty<string>())
                    .Select(value => (value ?? "").Trim().ToLowerInvariant())
                    .ToArray();
            }
            LevelRepo.InvalidateCandidateSelections();
        }

        public static bool TryGetMetadata(
            int chapter,
            out ChapterMetadata metadata)
        {
            EnsureInitialized();
            if (Chapters.TryGetValue(chapter, out metadata)) return true;
            if (TryLoadCached(chapter))
                return Chapters.TryGetValue(chapter, out metadata);
            return false;
        }

        public static bool TryGetCandidates(
            int chapter,
            int levelIndex,
            out IReadOnlyList<ChapterLevelCandidate> candidates)
        {
            EnsureInitialized();
            candidates = Array.Empty<ChapterLevelCandidate>();
            if (!Candidates.TryGetValue(chapter, out var byLevel) &&
                !TryLoadCached(chapter))
                return false;
            if (!Candidates.TryGetValue(chapter, out byLevel) ||
                !byLevel.TryGetValue(levelIndex, out ChapterLevelCandidate[] found))
                return false;
            candidates = found;
            return found.Length > 0;
        }

        public static bool TrySelectCandidate(
            int chapter,
            int levelIndex,
            out ChapterLevelCandidate selected)
        {
            selected = null;
            if (!TryGetCandidates(chapter, levelIndex, out var candidates))
                return false;

            ChapterLevelCandidate fallback = null;
            foreach (ChapterLevelCandidate candidate in candidates)
            {
                if (candidate == null) continue;
                if (ChapterLevelTagMatcher.Matches(
                        candidate.levelUserTags, UserTags))
                {
                    selected = candidate;
                    return true;
                }
                if (fallback == null &&
                    ChapterLevelTagMatcher.IsDefault(candidate.levelUserTags))
                    fallback = candidate;
            }
            selected = fallback ?? candidates.FirstOrDefault(value => value != null);
            return selected != null;
        }

        public static bool TryCreateSelectedLevel(
            int chapter,
            int levelIndex,
            LevelData bundledFallback,
            out LevelData level)
        {
            level = null;
            if (!TrySelectCandidate(chapter, levelIndex, out var candidate))
                return false;
            if (!ChapterCipher.TryDecrypt(candidate.initialLayout, out string layout))
                return false;

            var urls = new List<string>();
            foreach (string encrypted in candidate.imageUrls ?? Array.Empty<string>())
            {
                if (!ChapterCipher.TryDecrypt(encrypted, out string plain))
                    return false;
                foreach (string part in plain.Split(';'))
                {
                    string url = (part ?? "").Trim();
                    if (url.Length > 0) urls.Add(url);
                }
            }
            if (urls.Count == 0) return false;

            ParseDifficulty(candidate.difficulty, out int rows, out int cols);
            int globalLevel = ToGlobalLevel(chapter, levelIndex);
            level = new LevelData
            {
                chapter = chapter,
                level = globalLevel,
                difficulty_type = candidate.difficultyType ?? "",
                difficulty = candidate.difficulty ?? "",
                step_limit = candidate.stepLimit,
                add_step = candidate.addStep,
                coins = candidate.coins,
                level_user_tags = string.IsNullOrWhiteSpace(candidate.levelUserTags)
                    ? "default"
                    : candidate.levelUserTags,
                level_data_source = candidate.levelDataSource ?? "",
                level_unique_id = candidate.levelUniqueId ?? "",
                layout = layout,
                rows = rows,
                cols = cols,
                image_urls = urls.ToArray(),
                image_ids = Enumerable.Range(1, urls.Count).ToArray(),
            };

            CopyLocalMappings(level, bundledFallback);
            var scheduler = new WaveScheduler();
            scheduler.Build(level.layout, urls.Count);
            level.wave_count = scheduler.WaveCount();
            level.wave_sizes = scheduler.WaveSizes().ToArray();
            level.token_count = scheduler.TotalTokenCount();
            level.max_depth = WaveScheduler.ImageMaxDepths(level.layout)
                .Values.DefaultIfEmpty(0).Max();
            return true;
        }

        public static bool TryIngestServerResponse(
            string json,
            out int ingestedCount,
            bool persist = true)
        {
            EnsureInitialized();
            ingestedCount = 0;
            if (string.IsNullOrWhiteSpace(json)) return false;
            ChapterApiResponse response;
            try
            {
                response = JsonUtility.FromJson<ChapterApiResponse>(json);
            }
            catch (Exception)
            {
                return false;
            }
            if (response?.data == null) return false;
            foreach (ChapterMetadata metadata in response.data)
            {
                if (Ingest(metadata, persist)) ingestedCount++;
            }
            return ingestedCount > 0;
        }

        public static bool TryIngestChapterMetadata(
            string json,
            bool persist = true)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                return Ingest(JsonUtility.FromJson<ChapterMetadata>(json), persist);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void ConfirmChapterEmpty(int chapter)
        {
            if (chapter <= 0) return;
            EnsureInitialized();
            if (ConfirmedEmpty.Add(chapter)) EndStateChanged?.Invoke();
        }

        public static bool IsChapterConfirmedEmpty(int chapter)
        {
            EnsureInitialized();
            return chapter > 0 && ConfirmedEmpty.Contains(chapter);
        }

        public static bool IsConfirmedEndAfter(int chapter)
        {
            return chapter > 0 && IsChapterConfirmedEmpty(chapter + 1);
        }

        public static int ToGlobalLevel(int chapter, int levelIndex)
        {
            if (chapter <= 0 || levelIndex <= 0) return 0;
            return checked((chapter - 1) * LevelsPerChapter + levelIndex);
        }

        public static void ToChapterLevel(
            int globalLevel,
            out int chapter,
            out int levelIndex)
        {
            int bounded = Math.Max(1, globalLevel);
            chapter = (bounded - 1) / LevelsPerChapter + 1;
            levelIndex = (bounded - 1) % LevelsPerChapter + 1;
        }

        static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            LoadBundled(1);
        }

        static bool LoadBundled(int chapter)
        {
            TextAsset asset = Resources.Load<TextAsset>(
                BundledChapterPrefix + chapter);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return false;
            try
            {
                ChapterApiResponse response =
                    JsonUtility.FromJson<ChapterApiResponse>(asset.text);
                bool loaded = false;
                foreach (ChapterMetadata entry in
                         response?.data ?? Array.Empty<ChapterMetadata>())
                {
                    loaded |= Ingest(entry, false);
                }
                return loaded;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ChapterMetadataRepository] Invalid bundled chapter " +
                    $"{chapter}: {ex.Message}");
                return false;
            }
        }

        static bool TryLoadCached(int chapter)
        {
            if (chapter <= 0) return false;
            string path = CachePath(chapter);
            if (!File.Exists(path)) return false;
            try
            {
                return TryIngestChapterMetadata(File.ReadAllText(path), false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ChapterMetadataRepository] Ignoring invalid chapter " +
                    $"cache {chapter}: {ex.Message}");
                return false;
            }
        }

        static bool Ingest(ChapterMetadata metadata, bool persist)
        {
            if (metadata == null || metadata.chapterIndex <= 0 ||
                metadata.levels == null)
                return false;

            var groups = new Dictionary<int, List<ChapterLevelCandidate>>();
            foreach (ChapterLevelCandidate candidate in metadata.levels)
            {
                if (candidate == null || candidate.levelIndex <= 0) continue;
                if (!groups.TryGetValue(candidate.levelIndex, out var entries))
                {
                    entries = new List<ChapterLevelCandidate>();
                    groups[candidate.levelIndex] = entries;
                }
                entries.Add(candidate);
            }
            if (groups.Count == 0) return false;

            Chapters[metadata.chapterIndex] = metadata;
            Candidates[metadata.chapterIndex] = groups.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray());
            ConfirmedEmpty.Remove(metadata.chapterIndex);
            LevelRepo.InvalidateCandidateSelections();
            if (persist) SaveCache(metadata);
            EndStateChanged?.Invoke();
            return true;
        }

        static void SaveCache(ChapterMetadata metadata)
        {
            try
            {
                string directory = Path.Combine(
                    Application.persistentDataPath, CacheDirectoryName);
                Directory.CreateDirectory(directory);
                string path = CachePath(metadata.chapterIndex);
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(metadata));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ChapterMetadataRepository] Could not cache chapter " +
                    $"{metadata.chapterIndex}: {ex.Message}");
            }
        }

        static string CachePath(int chapter)
        {
            return Path.Combine(
                Application.persistentDataPath,
                CacheDirectoryName,
                $"chapter_{chapter}.json");
        }

        static void CopyLocalMappings(LevelData target, LevelData fallback)
        {
            int count = target.image_urls?.Length ?? 0;
            target.local_images = new string[count];
            target.local_images_hd = new string[count];
            target.local_image_files = new string[count];
            target.local_image_files_hd = new string[count];
            if (fallback == null || fallback.image_urls == null) return;

            for (int i = 0; i < count; i++)
            {
                int source = Array.FindIndex(
                    fallback.image_urls,
                    url => string.Equals(
                        url,
                        target.image_urls[i],
                        StringComparison.Ordinal));
                if (source < 0) continue;
                target.local_images[i] = At(fallback.local_images, source);
                target.local_images_hd[i] = At(fallback.local_images_hd, source);
                target.local_image_files[i] = At(
                    fallback.local_image_files, source);
                target.local_image_files_hd[i] = At(
                    fallback.local_image_files_hd, source);
            }
        }

        static void ParseDifficulty(
            string difficulty,
            out int rows,
            out int columns)
        {
            rows = 0;
            columns = 0;
            string[] parts = (difficulty ?? "").Split('*');
            if (parts.Length != 2) return;
            int.TryParse(parts[0].Trim(), out rows);
            int.TryParse(parts[1].Trim(), out columns);
        }

        static string At(string[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length
                ? values[index] ?? ""
                : "";
        }
    }

    static class ChapterCipher
    {
        const string KeyBase64 =
            "bHmJQ0X7EixyOe+LuW8ShBaXz1S4cNxL+Ebe3rQKHlA=";

        public static bool TryDecrypt(string value, out string plaintext)
        {
            plaintext = "";
            if (string.IsNullOrWhiteSpace(value)) return true;
            try
            {
                byte[] all = Convert.FromBase64String(value);
                if (all.Length < 32 || (all.Length - 16) % 16 != 0)
                    return false;
                byte[] iv = new byte[16];
                byte[] cipher = new byte[all.Length - iv.Length];
                Buffer.BlockCopy(all, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(all, iv.Length, cipher, 0, cipher.Length);
                using Aes aes = Aes.Create();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = Convert.FromBase64String(KeyBase64);
                aes.IV = iv;
                using ICryptoTransform decryptor = aes.CreateDecryptor();
                byte[] decoded = decryptor.TransformFinalBlock(
                    cipher, 0, cipher.Length);
                plaintext = Encoding.UTF8.GetString(decoded);
                return true;
            }
            catch (Exception)
            {
                plaintext = "";
                return false;
            }
        }
    }
}
