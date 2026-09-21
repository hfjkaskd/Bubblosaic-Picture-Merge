using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RemoteImageDelivery;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// BubblePics-facing adapter for the reusable remote-image package.
    /// It keeps puzzle-specific level/depth rules out of the package while
    /// exposing the original 25-level batching cadence to its prefetcher.
    /// </summary>
    public sealed class BubblePicsRemoteImageDelivery :
        IRemoteImageBatchSource,
        IRemoteImagePruneSource,
        IDisposable
    {
        public const string ConfigResourcePath = "RemoteImageDeliveryConfig";
        public const string ImageIndexResourcePath =
            "RemoteImageDelivery/BubblePicsImageIndex";
        const int LevelsPerGroup = 25;

        static readonly string[] ConfigResourceFallbacks =
        {
            ConfigResourcePath,
            "Config/RemoteImageDeliveryConfig",
            "RemoteImageDelivery/RemoteImageDeliveryConfig",
        };

        readonly RemoteImageDeliveryClient _client;
        readonly RemoteImageBatchPrefetcher _prefetcher;
        readonly RemoteImageDeliveryConfig _config;
        readonly bool _ownsConfig;
        readonly Dictionary<int, IReadOnlyList<RemoteImageAsset>> _itemAssets =
            new();
        Dictionary<string, ImageIndexEntry> _imageIndex;
        Task<Dictionary<string, ImageIndexEntry>> _imageIndexWarmup;
        bool _disposed;

        [Serializable]
        sealed class ImageIndexDocument
        {
            public ImageIndexEntry[] entries;
        }

        [Serializable]
        sealed class ImageIndexEntry
        {
            public string localPath;
            public string remotePath;
            public string sha256;
            public long byteSize;
        }

        public static BubblePicsRemoteImageDelivery Current { get; private set; }

        public RemoteImageDeliveryClient Client => _client;
        public RemoteImageDeliveryConfig Config => _config;
        public int ItemCount => LevelRepo.Count;
        public IEnumerable<int> KnownItemIndices => _itemAssets.Keys;

        public BubblePicsRemoteImageDelivery(MonoBehaviour host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            RemoteImageDeliveryConfig authoredConfig = LoadConfig();
            _ownsConfig = true;
            if (authoredConfig == null)
            {
                _config = ScriptableObject.CreateInstance<RemoteImageDeliveryConfig>();
                _config.name = "BubblePicsRemoteImageDeliveryConfig (Runtime Default)";
                _config.hideFlags = HideFlags.HideAndDontSave;

                // The recovered catalog contains original absolute image URLs.
                // Match the original 512/1024 imageView rule until a project
                // config points relative paths at the Cloudflare deployment.
                _config.allowAbsoluteFallbackUrl = true;
                _config.resizeAbsoluteFallbackWithImageView = true;
                _config.itemsPerGroup = LevelsPerGroup;
                _config.acceptHeader = "image/webp";
                _config.foregroundMaxAttempts = 1;
                _config.backgroundMaxAttempts = 1;
                _config.cancelNetworkRequestOnTimeout = false;
                _config.foregroundBatchTimeoutSeconds = 20;
                _config.advancePrefetchWindow = 11;
                _config.nextGroupThreshold = 5;
                _config.bundledSeedItemCount = 10;
                _config.preferRemoteAfterSeedItems = true;
                _config.cacheNamespace = "level_images";
                _config.enforceDiskCacheBudget = false;
                _config.resumePartialDownloads = false;
            }
            else
            {
                // Sanitize and compatibility adjustments must never dirty the
                // authored Resources asset during editor play mode.
                _config = UnityEngine.Object.Instantiate(authoredConfig);
                _config.name = authoredConfig.name + " (Runtime)";
                _config.hideFlags = HideFlags.HideAndDontSave;
                if (string.IsNullOrWhiteSpace(_config.baseUrl) &&
                    _config.allowAbsoluteFallbackUrl)
                {
                    _config.resizeAbsoluteFallbackWithImageView = true;
                }
            }

            _config.itemsPerGroup = LevelsPerGroup;
            _config.Sanitize();
            PurgeObsoleteDeliveryCache(_config.cacheNamespace);
            _client = new RemoteImageDeliveryClient(host, _config);
            _prefetcher = new RemoteImageBatchPrefetcher(_client, this);
            Current = this;
        }

        public IReadOnlyList<RemoteImageAsset> GetAssetsForItem(int itemIndex)
        {
            if (_disposed || itemIndex < 1 || itemIndex > ItemCount)
                return Array.Empty<RemoteImageAsset>();
            // This interface is consumed only by the background/high-next
            // prefetch planner. Do not mirror-download files that the current
            // build is intentionally serving from StreamingAssets, and do not
            // download the authored seed range in remote mode.
            if (!_config.preferRemoteAfterSeedItems ||
                itemIndex <= Mathf.Max(0, _config.bundledSeedItemCount))
            {
                return Array.Empty<RemoteImageAsset>();
            }
            if (_itemAssets.TryGetValue(itemIndex, out var cached))
                return cached;
            if (!LevelRepo.TryGet(itemIndex, out LevelData level) || level == null)
                return Array.Empty<RemoteImageAsset>();

            IReadOnlyList<RemoteImageAsset> assets = CreateAssets(level);
            _itemAssets[itemIndex] = assets;
            return assets;
        }

        public RemoteImageAsset CreateAsset(LevelData level, int imageIndex)
        {
            if (level == null || imageIndex < 0 ||
                imageIndex >= LevelRepo.ImageCount(level))
            {
                return null;
            }

            Dictionary<int, int> depths =
                WaveScheduler.ImageMaxDepths(level.layout ?? "");
            int size =
                depths.TryGetValue(imageIndex + 1, out int depth) && depth >= 2
                    ? 1024
                    : 512;
            return CreateAssetVariant(level, imageIndex, size);
        }

        public IReadOnlyList<RemoteImageAsset> GetAssetsToKeepForItem(
            int itemIndex)
        {
            if (_disposed || itemIndex < 1 || itemIndex > ItemCount ||
                !_config.preferRemoteAfterSeedItems ||
                itemIndex <= Mathf.Max(0, _config.bundledSeedItemCount) ||
                !LevelRepo.TryGet(itemIndex, out LevelData level) ||
                level == null)
            {
                return Array.Empty<RemoteImageAsset>();
            }

            int count = LevelRepo.ImageCount(level);
            var keep = new List<RemoteImageAsset>(count * 2);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int imageIndex = 0; imageIndex < count; imageIndex++)
            {
                AddVariantToKeep(level, imageIndex, 512, keep, seen);
                AddVariantToKeep(level, imageIndex, 1024, keep, seen);
            }
            return keep;
        }

        RemoteImageAsset CreateAssetVariant(
            LevelData level,
            int imageIndex,
            int size)
        {
            if (level == null || imageIndex < 0 ||
                imageIndex >= LevelRepo.ImageCount(level))
            {
                return null;
            }

            string normalPath = At(level.local_image_files, imageIndex);
            string relativePath = size >= 1024
                ? FirstNonEmpty(
                    At(level.local_image_files_hd, imageIndex),
                    normalPath)
                : normalPath;
            string normalizedLocalPath =
                RemoteImageDeliveryConfig.NormalizeRelativePart(relativePath);
            string sha256 = "";
            long byteSize = 0L;
            if (!string.IsNullOrEmpty(normalizedLocalPath) &&
                ImageIndex.TryGetValue(
                    normalizedLocalPath,
                    out ImageIndexEntry indexed))
            {
                relativePath = FirstNonEmpty(
                    indexed.remotePath,
                    normalizedLocalPath);
                sha256 = indexed.sha256 ?? "";
                byteSize = Math.Max(0L, indexed.byteSize);
            }
            else
            {
                relativePath = normalizedLocalPath;
            }
            relativePath =
                RemoteImageDeliveryConfig.NormalizeRelativePart(relativePath);
            string absoluteUrl = At(level.image_urls, imageIndex).Trim();
            // Original cache identity is the raw source URL plus requested
            // resolution. Keep the CDN blob path as a transport detail so a
            // future manifest layout does not create duplicate cache entries.
            string identity = !string.IsNullOrEmpty(absoluteUrl)
                ? absoluteUrl
                : relativePath;
            if (string.IsNullOrEmpty(identity)) return null;

            int item = level.level > 0 ? level.level : 1;
            return new RemoteImageAsset
            {
                key = identity,
                relativePath = relativePath,
                absoluteUrl = absoluteUrl,
                sha256 = sha256,
                byteSize = byteSize,
                width = size,
                height = size,
                groupIndex = (item - 1) / LevelsPerGroup + 1,
                itemIndex = item,
                variant = size.ToString(),
            };
        }

        void AddVariantToKeep(
            LevelData level,
            int imageIndex,
            int size,
            ICollection<RemoteImageAsset> output,
            ISet<string> seen)
        {
            RemoteImageAsset asset =
                CreateAssetVariant(level, imageIndex, size);
            if (asset != null && seen.Add(asset.StableKey))
                output.Add(asset);
        }

        public bool CanRequest(LevelData level, int imageIndex)
        {
            if (_disposed || !_config.remoteEnabled) return false;
            RemoteImageAsset asset = CreateAsset(level, imageIndex);
            return asset != null &&
                   !string.IsNullOrWhiteSpace(_config.ResolveUrl(asset));
        }

        public bool HasCached(LevelData level, int imageIndex)
        {
            if (_disposed) return false;
            RemoteImageAsset asset = CreateAsset(level, imageIndex);
            return asset != null && _client.HasCached(asset);
        }

        public void Request(
            LevelData level,
            int imageIndex,
            Action<RemoteImageResult> completed)
        {
            RemoteImageAsset asset = CreateAsset(level, imageIndex);
            if (asset == null)
            {
                completed?.Invoke(Failure(
                    null,
                    $"level {level?.level ?? 0} image {imageIndex + 1} " +
                    "has no remote source"));
                return;
            }
            Request(asset, completed);
        }

        public void Request(
            RemoteImageAsset asset,
            Action<RemoteImageResult> completed)
        {
            if (_disposed)
            {
                completed?.Invoke(Failure(
                    asset,
                    "BubblePics remote image delivery is disposed"));
                return;
            }

            if (asset == null)
            {
                completed?.Invoke(Failure(
                    null,
                    "image has no remote source"));
                return;
            }
            _client.RequestTexture(asset, RemoteImagePriority.High, completed);
        }

        public void NotifyForegroundItemReady(int itemIndex)
        {
            if (!_disposed)
                _prefetcher.NotifyForegroundItemReady(itemIndex);
        }

        /// <summary>
        /// Starts the large CDN lookup parse after the splash has rendered.
        /// JsonUtility.FromJson is safe for plain serializable data on a
        /// worker thread, while Resources access remains on the main thread.
        /// </summary>
        public void BeginImageIndexWarmup()
        {
            if (_disposed || _imageIndex != null ||
                _imageIndexWarmup != null)
            {
                return;
            }

            TextAsset json = Resources.Load<TextAsset>(ImageIndexResourcePath);
            string source = json != null ? json.text : "";
            if (string.IsNullOrWhiteSpace(source))
            {
                _imageIndex = EmptyImageIndex();
                return;
            }

            _imageIndexWarmup = Task.Run(() => ParseImageIndex(source));
        }

        public void NotifyItemAdvanced(int currentItemIndex)
        {
            if (!_disposed)
                _prefetcher.NotifyItemAdvanced(currentItemIndex);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _itemAssets.Clear();
            _client.Dispose();
            if (_ownsConfig && _config != null)
                UnityEngine.Object.Destroy(_config);
            if (ReferenceEquals(Current, this)) Current = null;
        }

        IReadOnlyList<RemoteImageAsset> CreateAssets(LevelData level)
        {
            int count = LevelRepo.ImageCount(level);
            var assets = new List<RemoteImageAsset>(count);
            for (int imageIndex = 0; imageIndex < count; imageIndex++)
            {
                RemoteImageAsset asset = CreateAsset(level, imageIndex);
                if (asset != null) assets.Add(asset);
            }
            return assets;
        }

        static RemoteImageDeliveryConfig LoadConfig()
        {
            foreach (string path in ConfigResourceFallbacks)
            {
                RemoteImageDeliveryConfig config =
                    Resources.Load<RemoteImageDeliveryConfig>(path);
                if (config != null) return config;
            }
            return null;
        }

        static Dictionary<string, ImageIndexEntry> ParseImageIndex(
            string source)
        {
            Dictionary<string, ImageIndexEntry> index = EmptyImageIndex();

            ImageIndexDocument document =
                JsonUtility.FromJson<ImageIndexDocument>(source);
            if (document?.entries == null) return index;
            foreach (ImageIndexEntry entry in document.entries)
            {
                if (entry == null) continue;
                string localPath =
                    RemoteImageDeliveryConfig.NormalizeRelativePart(
                        entry.localPath);
                if (string.IsNullOrEmpty(localPath)) continue;
                index[localPath] = entry;
            }
            return index;
        }

        static Dictionary<string, ImageIndexEntry> EmptyImageIndex()
        {
            return new Dictionary<string, ImageIndexEntry>(
                StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, ImageIndexEntry> ImageIndex
        {
            get
            {
                if (_imageIndex != null) return _imageIndex;
                BeginImageIndexWarmup();
                try
                {
                    _imageIndex = _imageIndexWarmup != null
                        ? _imageIndexWarmup.GetAwaiter().GetResult()
                        : EmptyImageIndex();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[BubblePicsRemoteImageDelivery] Ignoring invalid " +
                        "image index: " + ex.Message);
                    _imageIndex = EmptyImageIndex();
                }
                _imageIndexWarmup = null;
                return _imageIndex;
            }
        }

        static void PurgeObsoleteDeliveryCache(string activeNamespace)
        {
            const string obsoleteNamespace =
                "bubblepics_remote_images_v1";
            if (string.Equals(
                    activeNamespace,
                    obsoleteNamespace,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                string persistentRoot =
                    Path.GetFullPath(Application.persistentDataPath);
                string obsoleteRoot = Path.GetFullPath(Path.Combine(
                    persistentRoot,
                    obsoleteNamespace));
                string boundary = persistentRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                if (!obsoleteRoot.StartsWith(
                        boundary,
                        StringComparison.OrdinalIgnoreCase) ||
                    !Directory.Exists(obsoleteRoot))
                {
                    return;
                }

                int removed = 0;
                foreach (string path in Directory.GetFiles(obsoleteRoot))
                {
                    string name = Path.GetFileName(path);
                    if (!name.EndsWith(
                            ".img",
                            StringComparison.OrdinalIgnoreCase) &&
                        !name.EndsWith(
                            ".img.part",
                            StringComparison.OrdinalIgnoreCase) &&
                        !name.EndsWith(
                            ".json",
                            StringComparison.OrdinalIgnoreCase) &&
                        !name.EndsWith(
                            ".json.tmp",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    File.Delete(path);
                    removed++;
                }

                if (Directory.GetFileSystemEntries(obsoleteRoot).Length == 0)
                    Directory.Delete(obsoleteRoot);
                if (removed > 0)
                {
                    Debug.Log(
                        "[BubblePicsRemoteImageDelivery] Removed " +
                        $"{removed} obsolete split-cache files; active cache " +
                        $"is now {activeNamespace}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[BubblePicsRemoteImageDelivery] Could not remove the " +
                    "obsolete split cache: " + ex.Message);
            }
        }

        static RemoteImageResult Failure(
            RemoteImageAsset asset,
            string error)
        {
            return new RemoteImageResult
            {
                Asset = asset,
                Success = false,
                Error = error ?? "remote image request failed",
                Source = RemoteImageResultSource.None,
            };
        }

        static string At(string[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length
                ? values[index] ?? ""
                : "";
        }

        static string FirstNonEmpty(string preferred, string fallback)
        {
            return !string.IsNullOrWhiteSpace(preferred)
                ? preferred
                : fallback ?? "";
        }
    }
}
