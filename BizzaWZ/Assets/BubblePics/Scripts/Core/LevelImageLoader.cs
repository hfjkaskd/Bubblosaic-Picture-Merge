using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RemoteImageDelivery;
using UnityEngine;
using UnityEngine.Networking;

namespace BubblePics
{
    public sealed class LevelImageLoadResult
    {
        public LevelData Level;
        public Texture2D[] Textures;
        public bool UsedOfflineFallback;
        public string Detail;
    }

    /// <summary>
    /// Chapter image pipeline with a configurable source order. A normal load
    /// tries bundled Resources, StreamingAssets, persistent cache and CDN in
    /// the order declared by content_policy.json. If that chain is incomplete,
    /// it can rebuild a valid level using only known local images.
    /// </summary>
    public static class LevelImageLoader
    {
        static readonly Dictionary<string, Texture2D> MemoryCache = new();
        static readonly LinkedList<string> MemoryOrder = new();

        sealed class Pending : IDisposable
        {
            public int Index;
            public string Url;
            public string MemoryKey;
            public string CachePath;
            public bool StorePersistentCache;
            public UnityWebRequest Request;

            public void Dispose()
            {
                Request?.Dispose();
                Request = null;
            }
        }

        sealed class RemotePending
        {
            public int Index;
            public RemoteImageAsset Asset;
            public bool Done;
            public RemoteImageResult Result;
        }

        /// <summary>
        /// Drops loader-owned lookup entries without destroying textures that
        /// may still be referenced by the current page.
        /// </summary>
        public static int ClearMemoryCacheForGm()
        {
            int count = MemoryCache.Count;
            MemoryCache.Clear();
            MemoryOrder.Clear();
            return count;
        }

        /// <summary>
        /// Removes only .img files from the configured legacy level cache.
        /// The reusable remote-delivery cache is cleared through its own client.
        /// </summary>
        public static int ClearPersistentCacheForGm()
        {
            try
            {
                string persistentRoot =
                    Path.GetFullPath(Application.persistentDataPath);
                string directory = Path.GetFullPath(Path.Combine(
                    persistentRoot,
                    LevelContentPolicy.Current.cache_directory));
                string boundary =
                    persistentRoot.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                if (!directory.StartsWith(
                        boundary,
                        System.StringComparison.OrdinalIgnoreCase) ||
                    !Directory.Exists(directory))
                    return 0;

                int removed = 0;
                foreach (string file in Directory.GetFiles(
                             directory,
                             "*.img",
                             SearchOption.AllDirectories))
                {
                    File.Delete(file);
                    removed++;
                }
                return removed;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    "Could not clear level image cache: " + ex.Message);
                return 0;
            }
        }

        public static bool RequiresNetwork(LevelData level)
        {
            if (!LevelRepo.TryResolvePlayable(level, out LevelData playable, out _))
                return false;

            int count = ImageCount(playable);
            var depths = WaveScheduler.ImageMaxDepths(playable.layout ?? "");
            var policy = LevelContentPolicy.Current;
            for (int index = 0; index < count; index++)
            {
                foreach (string source in policy.source_order)
                {
                    if (source == LevelContentPolicy.BundledSource &&
                        HasBundled(playable, depths, index))
                    {
                        break;
                    }
                    if (source == LevelContentPolicy.StreamingSource &&
                        !ShouldPreferRemoteAfterSeed(playable) &&
                        HasStreamingFile(playable, depths, index))
                    {
                        break;
                    }
                    if (source == LevelContentPolicy.CacheSource &&
                        HasPersistentCache(playable, depths, index))
                    {
                        break;
                    }
                    if (source == LevelContentPolicy.CdnSource)
                    {
                        BubblePicsRemoteImageDelivery delivery =
                            BubblePicsRemoteImageDelivery.Current;
                        if (delivery != null &&
                            delivery.HasCached(playable, index))
                        {
                            break;
                        }
                        if (policy.network_enabled &&
                            ((delivery != null &&
                             delivery.CanRequest(playable, index)) ||
                            (delivery == null &&
                             !string.IsNullOrWhiteSpace(
                                 UrlAt(playable, index)))))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Compatibility entry point. If an offline fallback is selected, the
        /// supplied per-round LevelData instance is updated before completion,
        /// so existing callers automatically use its matching layout.
        /// </summary>
        public static IEnumerator Load(
            LevelData level,
            Action<Texture2D[]> completed,
            Action<string> failed,
            Action<float> progress = null)
        {
            LevelImageLoadResult loaded = null;
            string error = null;
            yield return LoadResolved(
                level,
                value => loaded = value,
                value => error = value,
                progress);

            if (loaded == null)
            {
                failed?.Invoke(error ?? "level images could not be prepared");
                yield break;
            }

            if (loaded.Level != null && !ReferenceEquals(level, loaded.Level))
                level?.CopyPlayableContentFrom(loaded.Level);
            completed?.Invoke(loaded.Textures);
        }

        public static IEnumerator LoadResolved(
            LevelData requested,
            Action<LevelImageLoadResult> completed,
            Action<string> failed,
            Action<float> progress = null)
        {
            float reportedProgress = 0f;
            Action<float> reportProgress = null;
            if (progress != null)
            {
                progress(0f);
                reportProgress = value =>
                {
                    value = Mathf.Clamp01(value);
                    if (value <= reportedProgress) return;
                    reportedProgress = value;
                    progress(value);
                };
            }

            if (!LevelRepo.TryResolvePlayable(
                    requested,
                    out LevelData playable,
                    out LevelResolveResult resolve))
            {
                failed?.Invoke(resolve?.Reason ?? "level data is invalid");
                yield break;
            }

            Texture2D[] textures = null;
            string primaryError = null;
            var policy = LevelContentPolicy.Current;
            yield return LoadInternal(
                playable,
                policy.source_order,
                policy.network_enabled,
                true,
                value => textures = value,
                value => primaryError = value,
                reportProgress);

            if (textures != null)
            {
                completed?.Invoke(new LevelImageLoadResult
                {
                    Level = playable,
                    Textures = textures,
                    Detail = resolve.UsedFallback ? resolve.Reason : "",
                });
                yield break;
            }

            if (!policy.offline_fallback_enabled)
            {
                failed?.Invoke(primaryError ?? "image sources exhausted");
                yield break;
            }

            List<LevelData> localImages = CollectLocalImageSources(playable);
            if (!LevelRepo.TryCreateOfflineFallback(
                    playable,
                    localImages,
                    out LevelData offline,
                    out string fallbackReason))
            {
                failed?.Invoke(
                    $"{primaryError ?? "image sources exhausted"}; " +
                    $"offline fallback unavailable: {fallbackReason}");
                yield break;
            }

            textures = null;
            string fallbackError = null;
            string[] offlineOrder =
            {
                LevelContentPolicy.BundledSource,
                LevelContentPolicy.StreamingSource,
                LevelContentPolicy.CacheSource,
            };
            yield return LoadInternal(
                offline,
                offlineOrder,
                false,
                false,
                value => textures = value,
                value => fallbackError = value,
                reportProgress);

            if (textures == null)
            {
                failed?.Invoke(
                    $"{primaryError ?? "image sources exhausted"}; " +
                    $"offline fallback failed: {fallbackError}");
                yield break;
            }

            Debug.LogWarning(
                $"Level {requested?.level ?? 0} entered offline fallback: " +
                fallbackReason);
            completed?.Invoke(new LevelImageLoadResult
            {
                Level = offline,
                Textures = textures,
                UsedOfflineFallback = true,
                Detail = fallbackReason,
            });
        }

        static IEnumerator LoadInternal(
            LevelData level,
            IReadOnlyList<string> sourceOrder,
            bool allowNetwork,
            bool honorRemotePreference,
            Action<Texture2D[]> completed,
            Action<string> failed,
            Action<float> progress)
        {
            int count = ImageCount(level);
            if (count <= 0)
            {
                failed?.Invoke("level has no images");
                yield break;
            }

            var textures = new Texture2D[count];
            var errors = new string[count];
            var depths = WaveScheduler.ImageMaxDepths(level.layout ?? "");
            progress?.Invoke(0f);

            foreach (string source in sourceOrder)
            {
                if (AllPrepared(textures)) break;
                switch (source)
                {
                    case LevelContentPolicy.BundledSource:
                        for (int i = 0; i < count; i++)
                        {
                            if (IsValid(textures[i])) continue;
                            if (TryLoadBundled(level, depths, i, out Texture2D bundled))
                                textures[i] = bundled;
                            // Resources.Load can upload/decompress imported
                            // textures on first use. Keep the splash moving and
                            // distribute that work like the original loader.
                            if (i < count - 1) yield return null;
                        }
                        break;

                    case LevelContentPolicy.StreamingSource:
                    {
                        if (honorRemotePreference &&
                            ShouldPreferRemoteAfterSeed(level))
                            break;

                        var pending = new List<Pending>();
                        for (int i = 0; i < count; i++)
                        {
                            if (IsValid(textures[i])) continue;
                            string localFile = LocalFileAt(level, depths, i);
                            if (string.IsNullOrWhiteSpace(localFile)) continue;
                            string key = "streaming:" + localFile.Replace('\\', '/');
                            if (TryGetMemory(key, out Texture2D memory))
                            {
                                textures[i] = memory;
                                continue;
                            }

                            string localUrl = StreamingAssetUrl(localFile);
                            if (TryLoadStreamingFile(localUrl, out Texture2D local))
                            {
                                local.name = CacheName(key);
                                Remember(key, local);
                                textures[i] = local;
                                continue;
                            }

                            if (localUrl.Contains("://"))
                            {
                                pending.Add(new Pending
                                {
                                    Index = i,
                                    Url = localUrl,
                                    MemoryKey = key,
                                    StorePersistentCache = false,
                                });
                            }
                        }
                        yield return DownloadPending(
                            pending,
                            textures,
                            errors,
                            progress);
                        break;
                    }

                    case LevelContentPolicy.CacheSource:
                        for (int i = 0; i < count; i++)
                        {
                            if (IsValid(textures[i])) continue;
                            string url = UrlAt(level, i);
                            if (string.IsNullOrWhiteSpace(url)) continue;
                            string key = SizedCacheKey(
                                url,
                                ImageSizeAt(depths, i));
                            if (TryGetMemory(key, out Texture2D memory))
                            {
                                textures[i] = memory;
                                continue;
                            }
                            string cachePath = CachePath(key);
                            if (TryLoadDisk(cachePath, out Texture2D disk))
                            {
                                disk.name = CacheName(key);
                                Remember(key, disk);
                                textures[i] = disk;
                            }
                            else if (File.Exists(cachePath))
                            {
                                DeleteCorruptCacheFile(cachePath);
                            }
                        }
                        break;

                    case LevelContentPolicy.CdnSource:
                    {
                        BubblePicsRemoteImageDelivery delivery =
                            BubblePicsRemoteImageDelivery.Current;
                        if (delivery == null)
                        {
                            if (allowNetwork)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    if (!IsValid(textures[i]))
                                    {
                                        errors[i] =
                                            "remote image delivery is not initialized";
                                    }
                                }
                            }
                            break;
                        }

                        bool mayUseNetwork =
                            allowNetwork &&
                            Application.internetReachability !=
                            NetworkReachability.NotReachable;
                        var pending = new List<RemotePending>();
                        for (int i = 0; i < count; i++)
                        {
                            if (IsValid(textures[i])) continue;
                            bool cached = delivery.HasCached(level, i);
                            if (!cached &&
                                (!mayUseNetwork ||
                                 !delivery.CanRequest(level, i)))
                            {
                                continue;
                            }
                            pending.Add(new RemotePending
                            {
                                Index = i,
                                Asset = delivery.CreateAsset(level, i),
                            });
                        }
                        yield return DownloadRemotePending(
                            delivery,
                            pending,
                            textures,
                            errors,
                            progress);
                        break;
                    }
                }
                progress?.Invoke(PreparedProgress(textures));
            }

            var missing = new List<string>();
            for (int i = 0; i < textures.Length; i++)
            {
                if (IsValid(textures[i])) continue;
                string detail = string.IsNullOrWhiteSpace(errors[i])
                    ? "no configured source contains this image"
                    : errors[i];
                missing.Add($"image {i + 1}: {detail}");
            }

            if (missing.Count > 0)
            {
                failed?.Invoke(string.Join("; ", missing));
                yield break;
            }
            progress?.Invoke(1f);
            completed?.Invoke(textures);
        }

        static IEnumerator DownloadRemotePending(
            BubblePicsRemoteImageDelivery delivery,
            List<RemotePending> pending,
            Texture2D[] textures,
            string[] errors,
            Action<float> progress)
        {
            int readyBeforeDownload = PreparedCount(textures);
            foreach (RemotePending item in pending)
            {
                RemotePending captured = item;
                delivery.Request(
                    item.Asset,
                    result =>
                    {
                        captured.Result = result;
                        captured.Done = true;
                    });
            }

            float deadline = Time.realtimeSinceStartup +
                Mathf.Max(
                    1,
                    delivery.Config.foregroundBatchTimeoutSeconds);
            while (pending.Any(item => !item.Done) &&
                   Time.realtimeSinceStartup < deadline)
            {
                progress?.Invoke(
                    RemoteBatchProgress(
                        delivery,
                        textures.Length,
                        readyBeforeDownload,
                        pending));
                yield return null;
            }

            progress?.Invoke(
                RemoteBatchProgress(
                    delivery,
                    textures.Length,
                    readyBeforeDownload,
                    pending));

            foreach (RemotePending item in pending)
            {
                if (!item.Done)
                {
                    errors[item.Index] =
                        "remote image batch timed out after " +
                        delivery.Config.foregroundBatchTimeoutSeconds +
                        " seconds";
                    continue;
                }

                RemoteImageResult result = item.Result;
                if (result == null || !result.Success ||
                    !IsValid(result.Texture))
                {
                    errors[item.Index] =
                        result?.Error ?? "remote image request failed";
                    continue;
                }

                result.Texture.name = CacheName(
                    "remote-delivery:" +
                    (result.Asset?.StableKey ?? item.Index.ToString()));
                result.Texture.wrapMode = TextureWrapMode.Clamp;
                textures[item.Index] = result.Texture;
                errors[item.Index] = "";
            }
        }

        static bool ShouldPreferRemoteAfterSeed(LevelData level)
        {
            BubblePicsRemoteImageDelivery delivery =
                BubblePicsRemoteImageDelivery.Current;
            if (delivery == null || level == null) return false;
            RemoteImageDeliveryConfig config = delivery.Config;
            return config.preferRemoteAfterSeedItems &&
                   level.level > Mathf.Max(0, config.bundledSeedItemCount);
        }

        static IEnumerator DownloadPending(
            List<Pending> pending,
            Texture2D[] textures,
            string[] errors,
            Action<float> progress)
        {
            int concurrency = LevelContentPolicy.Current.max_concurrent_downloads;
            int timeout = LevelContentPolicy.Current.request_timeout_seconds;
            int readyBeforeDownload = PreparedCount(textures);
            for (int start = 0; start < pending.Count; start += concurrency)
            {
                int end = Mathf.Min(start + concurrency, pending.Count);
                for (int i = start; i < end; i++)
                {
                    Pending item = pending[i];
                    item.Request = UnityWebRequestTexture.GetTexture(
                        item.Url,
                        nonReadable: false);
                    if (item.StorePersistentCache)
                        item.Request.SetRequestHeader("Accept", "image/jpeg,image/png,image/webp");
                    item.Request.timeout = timeout;
                    item.Request.SendWebRequest();
                }

                bool waiting = true;
                while (waiting)
                {
                    waiting = false;
                    for (int i = start; i < end; i++)
                    {
                        if (pending[i].Request.isDone) continue;
                        waiting = true;
                        break;
                    }
                    if (waiting)
                    {
                        float activeProgress = 0f;
                        for (int i = start; i < end; i++)
                        {
                            UnityWebRequest request = pending[i].Request;
                            activeProgress += request == null
                                ? 0f
                                : Mathf.Clamp01(request.downloadProgress);
                        }
                        progress?.Invoke(
                            BatchProgress(
                                textures.Length,
                                readyBeforeDownload,
                                start + activeProgress));
                        yield return null;
                    }
                }

                for (int i = start; i < end; i++)
                {
                    Pending item = pending[i];
                    try
                    {
                        if (item.Request.result != UnityWebRequest.Result.Success)
                        {
                            errors[item.Index] = item.Request.error;
                            continue;
                        }

                        Texture2D texture =
                            DownloadHandlerTexture.GetContent(item.Request);
                        if (!IsValid(texture))
                        {
                            errors[item.Index] = "decoded image is invalid";
                            continue;
                        }

                        texture.name = CacheName(item.MemoryKey);
                        texture.wrapMode = TextureWrapMode.Clamp;
                        textures[item.Index] = texture;
                        Remember(item.MemoryKey, texture);
                        if (item.StorePersistentCache)
                        {
                            TryWriteCache(
                                item.CachePath,
                                item.Request.downloadHandler.data);
                        }
                        errors[item.Index] = "";
                    }
                    finally
                    {
                        item.Dispose();
                    }
                }

                progress?.Invoke(
                    BatchProgress(
                        textures.Length,
                        readyBeforeDownload,
                        end));
            }
        }

        static int PreparedCount(Texture2D[] textures)
        {
            if (textures == null) return 0;
            int count = 0;
            for (int i = 0; i < textures.Length; i++)
                if (IsValid(textures[i])) count++;
            return count;
        }

        static float PreparedProgress(Texture2D[] textures)
        {
            return textures == null || textures.Length == 0
                ? 0f
                : PreparedCount(textures) / (float)textures.Length;
        }

        static float BatchProgress(
            int totalCount,
            int readyBeforeDownload,
            float completedInBatch)
        {
            if (totalCount <= 0) return 0f;
            return Mathf.Clamp01(
                (readyBeforeDownload + completedInBatch) / totalCount);
        }

        static float RemoteBatchProgress(
            BubblePicsRemoteImageDelivery delivery,
            int totalCount,
            int readyBeforeDownload,
            IEnumerable<RemotePending> pending)
        {
            float completed = 0f;
            foreach (RemotePending item in pending)
            {
                completed += item.Done
                    ? 1f
                    : delivery.Client.GetRequestProgress(item.Asset);
            }
            return BatchProgress(
                totalCount,
                readyBeforeDownload,
                completed);
        }

        static List<LevelData> CollectLocalImageSources(LevelData requested)
        {
            int preferred = Mathf.Max(1, ImageCount(requested));
            int target = Mathf.Min(24, preferred + 8);
            var result = new List<LevelData>(target);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (LevelData level in LevelRepo.BundledLevels())
            {
                if (level == null) continue;
                int count = ImageCount(level);
                var depths = WaveScheduler.ImageMaxDepths(level.layout ?? "");
                for (int i = 0; i < count; i++)
                {
                    string normalResource = At(level.local_images, i);
                    string normalFile = At(level.local_image_files, i);
                    bool resourceReady =
                        !string.IsNullOrWhiteSpace(normalResource) &&
                        ResourceAssetLoader.Load<Texture2D>(
                            "Art/Builtin/" + normalResource) != null;
                    bool fileReady = false;
                    if (!string.IsNullOrWhiteSpace(normalFile))
                    {
                        string path = StreamingAssetUrl(normalFile);
                        fileReady = path.Contains("://") || File.Exists(path);
                    }
                    if (!resourceReady && !fileReady) continue;

                    string identity =
                        normalResource + "|" + normalFile + "|" + At(level.image_urls, i);
                    if (!seen.Add(identity)) continue;

                    result.Add(SingleImageSource(level, i));
                    if (result.Count >= target) return result;
                }
            }
            return result;
        }

        static LevelData SingleImageSource(LevelData source, int index)
        {
            return new LevelData
            {
                image_urls = new[] { At(source.image_urls, index) },
                local_images = new[] { At(source.local_images, index) },
                local_images_hd = new[] { At(source.local_images_hd, index) },
                local_image_files = new[] { At(source.local_image_files, index) },
                local_image_files_hd =
                    new[] { At(source.local_image_files_hd, index) },
                image_ids = new[] { 1 },
            };
        }

        static int ImageCount(LevelData level)
        {
            return LevelRepo.ImageCount(level);
        }

        static string UrlAt(LevelData level, int index)
        {
            return At(level?.image_urls, index);
        }

        static bool HasBundled(
            LevelData level,
            Dictionary<int, int> depths,
            int index)
        {
            return TryLoadBundled(level, depths, index, out _);
        }

        static bool TryLoadBundled(
            LevelData level,
            Dictionary<int, int> depths,
            int index,
            out Texture2D texture)
        {
            texture = null;
            if (level == null || index < 0) return false;
            bool hd = depths.TryGetValue(index + 1, out int depth) && depth >= 2;
            string name = hd
                ? FirstNonEmpty(
                    At(level.local_images_hd, index),
                    At(level.local_images, index))
                : At(level.local_images, index);
            if (string.IsNullOrWhiteSpace(name) && level.level <= 10)
            {
                // The restored game maps its first ten seed levels to imported
                // res:// textures. Import data may retain only the original
                // StreamingAssets filename; its content hash is the matching
                // Resources texture name in this project.
                name = ResourceNameFromLocalFile(
                    hd
                        ? FirstNonEmpty(
                            At(level.local_image_files_hd, index),
                            At(level.local_image_files, index))
                        : At(level.local_image_files, index));
            }
            if (string.IsNullOrWhiteSpace(name)) return false;
            texture = ResourceAssetLoader.Load<Texture2D>("Art/Builtin/" + name);
            return IsValid(texture);
        }

        static string ResourceNameFromLocalFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            string normalized = path.Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            string file = slash >= 0
                ? normalized.Substring(slash + 1)
                : normalized;
            int dot = file.LastIndexOf('.');
            return dot > 0 ? file.Substring(0, dot) : file;
        }

        static bool HasStreamingFile(
            LevelData level,
            Dictionary<int, int> depths,
            int index)
        {
            string local = LocalFileAt(level, depths, index);
            if (string.IsNullOrWhiteSpace(local)) return false;
            string path = StreamingAssetUrl(local);
            return path.Contains("://") || File.Exists(path);
        }

        static string LocalFileAt(
            LevelData level,
            Dictionary<int, int> depths,
            int index)
        {
            if (level == null || index < 0) return "";
            bool hd = depths.TryGetValue(index + 1, out int depth) && depth >= 2;
            return hd
                ? FirstNonEmpty(
                    At(level.local_image_files_hd, index),
                    At(level.local_image_files, index))
                : At(level.local_image_files, index);
        }

        static bool HasPersistentCache(
            LevelData level,
            Dictionary<int, int> depths,
            int index)
        {
            string url = UrlAt(level, index);
            if (string.IsNullOrWhiteSpace(url)) return false;
            string key = SizedCacheKey(url, ImageSizeAt(depths, index));
            return HasMemory(key) || File.Exists(CachePath(key));
        }

        static int ImageSizeAt(Dictionary<int, int> depths, int index)
        {
            return depths.TryGetValue(index + 1, out int depth) && depth >= 2
                ? 1024
                : 512;
        }

        static string SizedCacheKey(string url, int size)
        {
            return (url ?? "") + "|" + size;
        }

        static string SizedImageUrl(string rawUrl, int size)
        {
            if (string.IsNullOrWhiteSpace(rawUrl) ||
                rawUrl.IndexOf(
                    "/imageView/",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return rawUrl;
            }
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri uri))
                return rawUrl;

            string path = uri.AbsolutePath.TrimStart('/');
            string authority = uri.GetLeftPart(UriPartial.Authority);
            return $"{authority}/imageView/{size}/{size}/{path}{uri.Query}";
        }

        static string StreamingAssetUrl(string relativePath)
        {
            string clean = (relativePath ?? "")
                .Replace('\\', '/')
                .TrimStart('/');
            string root = Application.streamingAssetsPath
                .Replace('\\', '/')
                .TrimEnd('/');
            return root + "/" + clean;
        }

        static bool TryLoadStreamingFile(
            string pathOrUrl,
            out Texture2D texture)
        {
            texture = null;
            if (string.IsNullOrWhiteSpace(pathOrUrl) ||
                pathOrUrl.Contains("://"))
            {
                return false;
            }
            return TryLoadDisk(pathOrUrl, out texture);
        }

        static string CachePath(string cacheKey)
        {
            string directory = Path.Combine(
                Application.persistentDataPath,
                LevelContentPolicy.Current.cache_directory);
            return Path.Combine(directory, CacheName(cacheKey) + ".img");
        }

        static string CacheName(string value)
        {
            return Hash128.Compute(value ?? "").ToString();
        }

        static bool HasMemory(string key)
        {
            return MemoryCache.TryGetValue(key, out Texture2D texture) &&
                   IsValid(texture);
        }

        static bool TryGetMemory(string key, out Texture2D texture)
        {
            if (!MemoryCache.TryGetValue(key, out texture) || !IsValid(texture))
            {
                MemoryCache.Remove(key);
                MemoryOrder.Remove(key);
                texture = null;
                return false;
            }
            MemoryOrder.Remove(key);
            MemoryOrder.AddLast(key);
            return true;
        }

        static void Remember(string key, Texture2D texture)
        {
            if (string.IsNullOrEmpty(key) || !IsValid(texture)) return;
            if (MemoryCache.TryGetValue(key, out Texture2D old) &&
                old != null &&
                old != texture)
            {
                UnityEngine.Object.Destroy(old);
            }

            MemoryCache[key] = texture;
            MemoryOrder.Remove(key);
            MemoryOrder.AddLast(key);
            int limit = LevelContentPolicy.Current.memory_cache_limit;
            while (MemoryOrder.Count > limit)
            {
                string oldest = MemoryOrder.First.Value;
                MemoryOrder.RemoveFirst();
                if (!MemoryCache.TryGetValue(oldest, out Texture2D evicted))
                    continue;
                MemoryCache.Remove(oldest);
                if (evicted != null) UnityEngine.Object.Destroy(evicted);
            }
        }

        static bool TryLoadDisk(string path, out Texture2D texture)
        {
            texture = null;
            try
            {
                if (!File.Exists(path)) return false;
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 64) return false;
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes, markNonReadable: false) ||
                    !IsValid(texture))
                {
                    UnityEngine.Object.Destroy(texture);
                    texture = null;
                    return false;
                }
                texture.wrapMode = TextureWrapMode.Clamp;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Could not read level image cache: " + ex.Message);
                return false;
            }
        }

        static void TryWriteCache(string path, byte[] bytes)
        {
            if (bytes == null || bytes.Length < 64) return;
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Could not write level image cache: " + ex.Message);
            }
        }

        static void DeleteCorruptCacheFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                string metadata = Path.ChangeExtension(path, ".json");
                if (File.Exists(metadata)) File.Delete(metadata);
                Debug.LogWarning(
                    "Deleted corrupt level image cache: " + path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "Could not delete corrupt level image cache: " +
                    ex.Message);
            }
        }

        static bool AllPrepared(Texture2D[] textures)
        {
            return textures.All(IsValid);
        }

        static bool IsValid(Texture2D texture)
        {
            return texture != null && texture.width > 4 && texture.height > 4;
        }

        static string At(string[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length
                ? values[index] ?? ""
                : "";
        }

        static string FirstNonEmpty(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
        }
    }
}
