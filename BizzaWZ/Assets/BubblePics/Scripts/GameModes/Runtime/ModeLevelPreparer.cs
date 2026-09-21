using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace BubblePics.GameModes
{
    public sealed class ModePreparedLevel
    {
        public LevelData Level;
        public Texture2D[] Textures;
        public CompiledModeLevel Compiled;
        public IReadOnlyDictionary<string, Texture2D> CategoryIcons;
    }

    public static class ModeLevelPreparer
    {
        const int CategoryDownloadConcurrency = 6;
        const int CategoryDownloadTimeoutSeconds = 20;
        const int CategoryMemoryLimit = 48;

        static readonly Dictionary<string, Texture2D> IconMemory =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        static readonly LinkedList<string> IconMemoryOrder =
            new LinkedList<string>();

        public static IEnumerator Prepare(
            LevelModeSelection selection,
            Action<ModePreparedLevel> completed,
            Action<string> failed,
            Action<float> progress = null)
        {
            if (selection == null)
            {
                failed?.Invoke("mode selection is null");
                yield break;
            }

            switch (selection.Kind)
            {
                case GameplayKind.CategoryMatch:
                    yield return PrepareCategory(
                        selection,
                        completed,
                        failed,
                        progress);
                    yield break;
                case GameplayKind.WordMatch:
                    PrepareWord(selection, completed, failed);
                    yield break;
                case GameplayKind.Tangram:
                    PrepareTangram(selection, completed, failed);
                    yield break;
                case GameplayKind.Bonus:
                    PrepareBonus(selection, completed, failed);
                    yield break;
                default:
                    failed?.Invoke(
                        "no built-in preparer for " + selection.Kind);
                    yield break;
            }
        }

        static IEnumerator PrepareCategory(
            LevelModeSelection selection,
            Action<ModePreparedLevel> completed,
            Action<string> failed,
            Action<float> progress)
        {
            CompiledModeLevel compiled = selection.Payload as CompiledModeLevel ??
                SpecialTokenCompiler.CompileCategory(
                    selection.Layout,
                    selection.StepLimit,
                    ModeCatalogRepository.CategoryCatalog);
            if (!compiled.IsValid)
            {
                failed?.Invoke(string.Join("; ", compiled.Errors));
                yield break;
            }

            var codes = compiled.Groups
                .SelectMany(group => group.CodesBySlot.Values)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var loaded = new Dictionary<string, Texture2D>(
                StringComparer.Ordinal);
            string error = null;
            yield return LoadCategoryIcons(
                codes,
                loaded,
                message => error = message,
                progress);
            if (!string.IsNullOrWhiteSpace(error))
            {
                failed?.Invoke(error);
                yield break;
            }

            var textures = new Texture2D[compiled.Groups.Count];
            var identities = new string[compiled.Groups.Count];
            for (int i = 0; i < compiled.Groups.Count; i++)
            {
                CompiledModeGroup group = compiled.Groups[i];
                var members = new Texture2D[4];
                for (int slot = 0; slot < 4; slot++)
                {
                    if (!group.CodesBySlot.TryGetValue(
                            slot,
                            out string code) ||
                        !loaded.TryGetValue(code, out members[slot]) ||
                        members[slot] == null)
                    {
                        failed?.Invoke(
                            $"category icon {code ?? "?"} is unavailable");
                        yield break;
                    }
                }
                textures[i] = ModeTextureComposer.ComposeCategory(members);
                identities[i] = "category:" +
                    string.Join(",", group.CodesBySlot.OrderBy(p => p.Key)
                        .Select(p => p.Value));
            }
            CompleteCompiled(
                selection,
                compiled,
                textures,
                identities,
                completed,
                failed,
                loaded);
        }

        static void PrepareWord(
            LevelModeSelection selection,
            Action<ModePreparedLevel> completed,
            Action<string> failed)
        {
            CompiledModeLevel compiled = selection.Payload as CompiledModeLevel ??
                SpecialTokenCompiler.CompileWord(
                    selection.Layout,
                    selection.StepLimit,
                    ModeCatalogRepository.WordCatalog);
            if (!compiled.IsValid)
            {
                failed?.Invoke(string.Join("; ", compiled.Errors));
                return;
            }
            var textures = new Texture2D[compiled.Groups.Count];
            var identities = new string[compiled.Groups.Count];
            WordMatchCatalogData catalog = ModeCatalogRepository.WordCatalog;
            for (int i = 0; i < compiled.Groups.Count; i++)
            {
                CompiledModeGroup group = compiled.Groups[i];
                var codes = new string[4];
                for (int slot = 0; slot < 4; slot++)
                {
                    codes[slot] = group.CodesBySlot[slot];
                }
                // BubbleView renders the localized words directly. Keep an
                // invisible carrier texture only because the shared scheduler
                // uses texture-array length as the target group count.
                textures[i] = ModeTextureComposer.ComposeWords(
                    new[] { "", "", "", "" });
                identities[i] = "word:" + string.Join(",", codes);
            }
            CompleteCompiled(
                selection,
                compiled,
                textures,
                identities,
                completed,
                failed);
        }

        static CompiledModeLevel CloneCompiled(CompiledModeLevel source)
        {
            var clone = new CompiledModeLevel
            {
                Kind = source.Kind,
                SourceLayout = source.SourceLayout,
                NumericLayout = source.NumericLayout,
                StepLimit = source.StepLimit,
            };
            clone.Groups.AddRange(source.Groups);
            clone.Errors.AddRange(source.Errors);
            return clone;
        }

        static string BuildTangramTutorialLayout(CompiledModeLevel compiled)
        {
            CompiledModeGroup group = compiled?.Groups.FirstOrDefault(
                value => value?.TangramTarget != null);
            TangramPieceData preplaced = group?.TangramTarget?.Pieces?
                .FirstOrDefault(value => value != null && value.Preplaced);
            if (group == null || preplaced == null)
                return compiled?.NumericLayout ?? string.Empty;

            string mold = (group.ImageId + 1) + ".M";
            string piece = (group.ImageId + 1) + "." +
                           (preplaced.PieceId + 1);
            string[] waves = (compiled.NumericLayout ?? string.Empty).Split('|');
            bool moldUpdated = false;
            bool pieceRemoved = false;
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                var entries = waves[waveIndex].Split(',')
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .ToList();
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    if (!moldUpdated && entries[entryIndex] == mold)
                    {
                        entries[entryIndex] = mold + "+" + piece;
                        moldUpdated = true;
                        continue;
                    }
                    if (!pieceRemoved && entries[entryIndex] == piece)
                    {
                        entries.RemoveAt(entryIndex--);
                        pieceRemoved = true;
                    }
                }
                waves[waveIndex] = string.Join(",", entries);
            }
            return moldUpdated && pieceRemoved
                ? string.Join("|", waves)
                : compiled.NumericLayout;
        }

        static void PrepareTangram(
            LevelModeSelection selection,
            Action<ModePreparedLevel> completed,
            Action<string> failed)
        {
            CompiledModeLevel compiled = selection.Payload as CompiledModeLevel ??
                SpecialTokenCompiler.CompileTangram(
                    selection.Layout,
                    selection.StepLimit,
                    ModeCatalogRepository.TangramCatalog);
            if (!compiled.IsValid)
            {
                failed?.Invoke(string.Join("; ", compiled.Errors));
                return;
            }
            if (!ModeProgress.ShapeTutorialDone)
            {
                compiled = CloneCompiled(compiled);
                compiled.NumericLayout = BuildTangramTutorialLayout(compiled);
            }

            var textures = new Texture2D[compiled.Groups.Count];
            var identities = new string[compiled.Groups.Count];
            for (int i = 0; i < compiled.Groups.Count; i++)
            {
                CompiledModeGroup group = compiled.Groups[i];
                TangramTargetData target = group.TangramTarget;
                Texture2D original = string.IsNullOrWhiteSpace(
                    target?.OriginalResourceStem)
                    ? null
                    : ResourceAssetLoader.Load<Texture2D>(
                        ModeCatalogRepository.TangramOriginalRoot +
                        target.OriginalResourceStem);
                textures[i] = original != null
                    ? original
                    : ModeTextureComposer.ComposeTangramPlaceholder(
                        target);
                identities[i] = "tangram:" + group.SourceGroupId;
            }
            CompleteCompiled(
                selection,
                compiled,
                textures,
                identities,
                completed,
                failed);
        }

        static void PrepareBonus(
            LevelModeSelection selection,
            Action<ModePreparedLevel> completed,
            Action<string> failed)
        {
            if (!(selection.Payload is BonusLevelData bonus))
            {
                failed?.Invoke("bonus selection has no level payload");
                return;
            }
            var textures = new Texture2D[bonus.ImageUrls.Length];
            for (int i = 0; i < bonus.ImageUrls.Length; i++)
            {
                string stem = bonus.ResourceStems != null &&
                              bonus.ResourceStems.Length == bonus.ImageUrls.Length
                    ? bonus.ResourceStems[i]
                    : Md5(bonus.ImageUrls[i]);
                textures[i] = ResourceAssetLoader.Load<Texture2D>(
                    ModeCatalogRepository.BonusImageRoot + stem);
                if (textures[i] == null)
                {
                    failed?.Invoke(
                        $"bundled bonus image is missing: {stem} " +
                        $"(bonus {bonus.Sequence})");
                    return;
                }
            }
            var level = BaseLevel(
                selection,
                bonus.Layout,
                bonus.StepLimit,
                bonus.ImageUrls);
            level.difficulty_type = bonus.DifficultyType;
            if (!ValidatePrepared(level, textures, out string error))
            {
                failed?.Invoke(error);
                return;
            }
            completed?.Invoke(new ModePreparedLevel
            {
                Level = level,
                Textures = textures,
            });
        }

        static void CompleteCompiled(
            LevelModeSelection selection,
            CompiledModeLevel compiled,
            Texture2D[] textures,
            string[] identities,
            Action<ModePreparedLevel> completed,
            Action<string> failed,
            IReadOnlyDictionary<string, Texture2D> categoryIcons = null)
        {
            LevelData level = BaseLevel(
                selection,
                compiled.NumericLayout,
                compiled.StepLimit,
                identities);
            if (!ValidatePrepared(level, textures, out string error))
            {
                failed?.Invoke(error);
                return;
            }
            completed?.Invoke(new ModePreparedLevel
            {
                Level = level,
                Textures = textures,
                Compiled = compiled,
                CategoryIcons = categoryIcons,
            });
        }

        static LevelData BaseLevel(
            LevelModeSelection selection,
            string numericLayout,
            int stepLimit,
            string[] identities)
        {
            int count = identities?.Length ?? 0;
            LevelData level = LevelRepo.TryGet(
                    selection.GlobalLevel,
                    out LevelData catalogLevel)
                ? catalogLevel
                : new LevelData
                {
                    difficulty_type = "Normal",
                    difficulty = "Normal",
                };

            // A special mode replaces only playable content. Retain the
            // catalog level's identity/difficulty so hard-level rules and UI
            // do not silently change when the mode is prepared.
            level.chapter = selection.Chapter;
            level.level = selection.GlobalLevel;
            level.step_limit = stepLimit;
            level.layout = numericLayout;
            level.rows = 0;
            level.cols = 0;
            level.image_urls = identities ?? Array.Empty<string>();
            level.image_ids = Enumerable.Range(1, count).ToArray();
            level.local_images = Array.Empty<string>();
            level.local_images_hd = Array.Empty<string>();
            level.local_image_files = Array.Empty<string>();
            level.local_image_files_hd = Array.Empty<string>();
            level.level_data_source = selection.Source;
            level.level_unique_id =
                selection.Kind + ":" + selection.GlobalLevel + ":" +
                Hash128.Compute(numericLayout ?? string.Empty);

            var scheduler = new WaveScheduler();
            scheduler.Build(numericLayout, count);
            level.wave_count = scheduler.WaveCount();
            level.wave_sizes = scheduler.WaveSizes().ToArray();
            level.token_count = scheduler.TotalTokenCount();
            level.max_depth = 0;
            foreach (int depth in WaveScheduler.ImageMaxDepths(numericLayout).Values)
                level.max_depth = Math.Max(level.max_depth, depth);
            return level;
        }

        static bool ValidatePrepared(
            LevelData level,
            Texture2D[] textures,
            out string error)
        {
            error = string.Empty;
            if (level == null || textures == null || textures.Length == 0 ||
                textures.Any(texture => texture == null))
            {
                error = "prepared mode textures are incomplete";
                return false;
            }
            LevelValidationResult validation = LevelValidator.Validate(
                level.layout,
                textures.Length);
            if (!validation.IsValid)
            {
                error = "prepared mode layout is invalid: " + validation;
                return false;
            }
            return true;
        }

        static IEnumerator LoadCategoryIcons(
            IReadOnlyList<string> codes,
            IDictionary<string, Texture2D> output,
            Action<string> failed,
            Action<float> progress)
        {
            CategoryMatchCatalogData catalog =
                ModeCatalogRepository.CategoryCatalog;
            var pending = new List<(string Code, CategoryIconEntry Icon)>(
                codes.Count);
            for (int i = 0; i < codes.Count; i++)
            {
                string code = codes[i];
                CategoryIconEntry icon = catalog.Icons[code];
                if (TryLoadIcon(icon, out Texture2D texture))
                    output[code] = texture;
                else
                    pending.Add((code, icon));
            }
            progress?.Invoke((codes.Count - pending.Count) /
                             (float)Mathf.Max(1, codes.Count));

            for (int start = 0;
                 start < pending.Count;
                 start += CategoryDownloadConcurrency)
            {
                int end = Mathf.Min(
                    pending.Count,
                    start + CategoryDownloadConcurrency);
                var requests = new List<UnityWebRequest>(end - start);
                for (int i = start; i < end; i++)
                {
                    UnityWebRequest request =
                        UnityWebRequestTexture.GetTexture(
                            pending[i].Icon.Url,
                            nonReadable: false);
                    request.timeout = CategoryDownloadTimeoutSeconds;
                    request.SetRequestHeader(
                        "Accept",
                        "image/png,image/jpeg,image/webp");
                    request.SendWebRequest();
                    requests.Add(request);
                }
                while (requests.Any(request => !request.isDone))
                {
                    float batch = requests.Sum(request =>
                        Mathf.Clamp01(request.downloadProgress));
                    progress?.Invoke(
                        (codes.Count - pending.Count + start + batch) /
                        Mathf.Max(1, codes.Count));
                    yield return null;
                }

                for (int local = 0; local < requests.Count; local++)
                {
                    int pendingIndex = start + local;
                    UnityWebRequest request = requests[local];
                    try
                    {
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            failed?.Invoke(
                                $"category icon {pending[pendingIndex].Code} " +
                                $"download failed: {request.error}");
                            yield break;
                        }
                        Texture2D texture =
                            null;
                        byte[] bytes = request.downloadHandler.data;
                        if (!ValidateIconBytes(
                                pending[pendingIndex].Icon,
                                bytes,
                                out string validationError))
                        {
                            failed?.Invoke(
                                $"category icon {pending[pendingIndex].Code} " +
                                validationError);
                            yield break;
                        }
                        texture = DownloadHandlerTexture.GetContent(request);
                        if (texture == null || texture.width < 4 ||
                            texture.height < 4)
                        {
                            failed?.Invoke(
                                $"category icon {pending[pendingIndex].Code} " +
                                "decoded invalid image data");
                            yield break;
                        }
                        texture.wrapMode = TextureWrapMode.Clamp;
                        output[pending[pendingIndex].Code] = texture;
                        RememberIcon(
                            pending[pendingIndex].Icon.Url,
                            texture);
                        TryWriteIconCache(
                            pending[pendingIndex].Icon.Url,
                            bytes);
                    }
                    finally
                    {
                        request.Dispose();
                    }
                }
                progress?.Invoke(
                    (codes.Count - pending.Count + end) /
                    (float)Mathf.Max(1, codes.Count));
            }
        }

        static bool TryLoadIcon(
            CategoryIconEntry icon,
            out Texture2D texture)
        {
            string url = icon.Url;
            if (IconMemory.TryGetValue(url, out texture) && texture != null)
            {
                TouchIcon(url);
                return true;
            }
            string path = IconCachePath(url);
            if (!File.Exists(path))
            {
                texture = null;
                return false;
            }
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (!ValidateIconBytes(icon, bytes, out _))
                {
                    texture = null;
                    File.Delete(path);
                    return false;
                }
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, markNonReadable: false) ||
                    texture.width < 4 || texture.height < 4)
                {
                    UnityEngine.Object.Destroy(texture);
                    texture = null;
                    File.Delete(path);
                    return false;
                }
                texture.wrapMode = TextureWrapMode.Clamp;
                RememberIcon(url, texture);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Category icon cache read failed: " + ex.Message);
                texture = null;
                return false;
            }
        }

        static bool ValidateIconBytes(
            CategoryIconEntry icon,
            byte[] bytes,
            out string error)
        {
            if (bytes == null || bytes.Length < 64)
            {
                error = "returned empty image data";
                return false;
            }
            if (icon.ByteSize > 0 && bytes.LongLength != icon.ByteSize)
            {
                error =
                    $"byte count mismatch ({bytes.LongLength} != " +
                    $"{icon.ByteSize})";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(icon.Sha256))
            {
                using (SHA256 sha = SHA256.Create())
                {
                    string actual = string.Concat(
                        sha.ComputeHash(bytes)
                            .Select(value => value.ToString("x2")));
                    if (!string.Equals(
                            actual,
                            icon.Sha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        error = "SHA-256 mismatch";
                        return false;
                    }
                }
            }
            error = string.Empty;
            return true;
        }

        static void RememberIcon(string url, Texture2D texture)
        {
            IconMemory[url] = texture;
            TouchIcon(url);
            while (IconMemoryOrder.Count > CategoryMemoryLimit)
            {
                string oldest = IconMemoryOrder.First.Value;
                IconMemoryOrder.RemoveFirst();
                IconMemory.Remove(oldest);
                // Textures may still be referenced by a composite operation;
                // the page owns only composites, so defer actual destruction
                // to Unity's resource lifecycle.
            }
        }

        static void TouchIcon(string url)
        {
            IconMemoryOrder.Remove(url);
            IconMemoryOrder.AddLast(url);
        }

        static void TryWriteIconCache(string url, byte[] bytes)
        {
            if (bytes == null || bytes.Length < 64) return;
            try
            {
                string path = IconCachePath(url);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "Category icon cache write failed: " + ex.Message);
            }
        }

        static string IconCachePath(string url)
        {
            return Path.Combine(
                Application.persistentDataPath,
                "category_icon_img",
                Md5(url) + ".img");
        }

        public static string Md5(string value)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var text = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    text.Append(hash[i].ToString("x2"));
                return text.ToString();
            }
        }
    }
}
