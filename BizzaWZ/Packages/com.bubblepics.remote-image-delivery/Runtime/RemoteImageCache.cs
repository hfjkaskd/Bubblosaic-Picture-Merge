using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace RemoteImageDelivery
{
    [Serializable]
    sealed class RemoteImageCacheEntry
    {
        public string stableKey;
        public string relativePath;
        public string sourceUrl;
        public string sha256;
        public long byteSize;
        public long downloadedUtcTicks;
        public long lastAccessUtcTicks;
        public string etag;
        public string lastModified;
        public int width;
        public int height;
        public int groupIndex;
        public int itemIndex;
        public string variant;
    }

    public sealed class RemoteImageCache
    {
        readonly RemoteImageDeliveryConfig _config;
        readonly string _root;

        public string RootPath => _root;

        public RemoteImageCache(RemoteImageDeliveryConfig config)
        {
            _config = config != null
                ? config
                : throw new ArgumentNullException(nameof(config));
            _config.Sanitize();
            _root = Path.Combine(
                Application.persistentDataPath,
                _config.cacheNamespace);
            Directory.CreateDirectory(_root);
            RemoveOrphanedTemporaryMetadata();
        }

        public string CacheKey(RemoteImageAsset asset)
        {
            return Hash128.Compute(asset?.StableKey ?? "").ToString();
        }

        public string DataPath(RemoteImageAsset asset)
        {
            return Path.Combine(_root, CacheKey(asset) + ".img");
        }

        public string PartialPath(RemoteImageAsset asset)
        {
            return DataPath(asset) + ".part";
        }

        public bool HasValidFile(RemoteImageAsset asset)
        {
            return TryGetValidFile(asset, out _, out _);
        }

        public bool TryGetValidFile(
            RemoteImageAsset asset,
            out string path,
            out string error)
        {
            path = DataPath(asset);
            if (!File.Exists(path))
            {
                error = "cache miss";
                return false;
            }

            if (!ValidateFile(path, asset, false, out error))
            {
                DeleteAsset(asset);
                return false;
            }

            Touch(asset);
            return true;
        }

        public bool TryLoadTexture(
            RemoteImageAsset asset,
            out Texture2D texture,
            out string path,
            out string error)
        {
            texture = null;
            if (!TryGetValidFile(asset, out path, out error))
                return false;
            if (!TryDecodeTexture(path, out texture, out error))
            {
                DeleteAsset(asset);
                return false;
            }
            texture.name = "RemoteImage_" + CacheKey(asset);
            return true;
        }

        public bool ValidatePart(
            RemoteImageAsset asset,
            out long bytes,
            out string error)
        {
            string part = PartialPath(asset);
            bytes = File.Exists(part) ? new FileInfo(part).Length : 0L;
            return ValidateFile(part, asset, true, out error);
        }

        public bool CommitPart(
            RemoteImageAsset asset,
            string sourceUrl,
            string etag,
            string lastModified,
            out string finalPath,
            out string error)
        {
            finalPath = DataPath(asset);
            string part = PartialPath(asset);
            if (!ValidateFile(part, asset, true, out error))
                return false;

            try
            {
                Directory.CreateDirectory(_root);
                ReplaceAtomically(part, finalPath);
                var info = new FileInfo(finalPath);
                var entry = new RemoteImageCacheEntry
                {
                    stableKey = asset.StableKey,
                    relativePath = asset.relativePath ?? "",
                    sourceUrl = sourceUrl ?? "",
                    sha256 = asset.sha256 ?? "",
                    byteSize = info.Length,
                    downloadedUtcTicks = DateTime.UtcNow.Ticks,
                    lastAccessUtcTicks = DateTime.UtcNow.Ticks,
                    etag = etag ?? "",
                    lastModified = lastModified ?? "",
                    width = asset.width,
                    height = asset.height,
                    groupIndex = asset.groupIndex,
                    itemIndex = asset.itemIndex,
                    variant = asset.variant ?? "",
                };
                WriteEntry(asset, entry);
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = "cache commit failed: " + ex.Message;
                return false;
            }
        }

        public bool ValidateFile(
            string path,
            RemoteImageAsset asset,
            bool alwaysHash,
            out string error)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    error = "file does not exist";
                    return false;
                }
                var info = new FileInfo(path);
                if (info.Length < 64)
                {
                    error = "image is too small";
                    return false;
                }
                if (_config.verifyByteCountWhenProvided &&
                    asset != null &&
                    asset.byteSize > 0 &&
                    info.Length != asset.byteSize)
                {
                    error =
                        $"byte count mismatch ({info.Length}/{asset.byteSize})";
                    return false;
                }
                if (_config.validateImageHeader &&
                    !HasSupportedImageHeader(path))
                {
                    error = "unsupported or corrupt image header";
                    return false;
                }
                if (_config.verifySha256WhenProvided &&
                    asset != null &&
                    !string.IsNullOrWhiteSpace(asset.sha256) &&
                    (alwaysHash || ShouldVerifyCachedHash(asset)) &&
                    !string.Equals(
                        ComputeSha256(path),
                        NormalizeHash(asset.sha256),
                        StringComparison.OrdinalIgnoreCase))
                {
                    error = "SHA-256 mismatch";
                    return false;
                }
                if (_config.validateDecodedTexture && alwaysHash)
                {
                    if (!TryDecodeTexture(path, out Texture2D probe, out error))
                        return false;
                    UnityEngine.Object.Destroy(probe);
                }
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = "cache validation failed: " + ex.Message;
                return false;
            }
        }

        public long PartialLength(RemoteImageAsset asset)
        {
            string path = PartialPath(asset);
            try
            {
                return File.Exists(path) ? new FileInfo(path).Length : 0L;
            }
            catch
            {
                return 0L;
            }
        }

        public void DeletePartial(RemoteImageAsset asset)
        {
            TryDelete(PartialPath(asset));
        }

        public void DeleteAsset(RemoteImageAsset asset)
        {
            TryDelete(DataPath(asset));
            TryDelete(MetadataPath(asset));
        }

        public int PruneExcept(ISet<string> stableKeys)
        {
            int removed = 0;
            if (!Directory.Exists(_root)) return removed;

            // The recovered game enumerates committed image files directly.
            // Do the same instead of relying on our optional JSON sidecars, so
            // legacy/orphaned images are covered as well. A partial download is
            // deliberately never part of progression pruning.
            var keepStems = new HashSet<string>(StringComparer.Ordinal);
            if (stableKeys != null)
            {
                foreach (string stableKey in stableKeys)
                {
                    if (string.IsNullOrWhiteSpace(stableKey)) continue;
                    keepStems.Add(Hash128.Compute(stableKey).ToString());
                }
            }

            foreach (string dataPath in Directory.GetFiles(_root, "*.img"))
            {
                string stem = Path.GetFileNameWithoutExtension(dataPath);
                if (keepStems.Contains(stem)) continue;
                if (TryDelete(dataPath)) removed++;
                TryDelete(Path.Combine(_root, stem + ".json"));
            }

            // Sidecars are an implementation detail, not independently cached
            // content. Remove only sidecars whose committed image is gone.
            foreach (string metadataPath in Directory.GetFiles(_root, "*.json"))
            {
                string stem = Path.GetFileNameWithoutExtension(metadataPath);
                if (!File.Exists(Path.Combine(_root, stem + ".img")))
                    TryDelete(metadataPath);
            }
            return removed;
        }

        public int PruneToBudget()
        {
            long budget = (long)_config.maxDiskCacheMiB * 1024L * 1024L;
            List<CacheFileInfo> files = ScanDataFiles();
            long total = 0L;
            foreach (CacheFileInfo file in files) total += file.Bytes;
            if (total <= budget) return 0;

            files.Sort((a, b) => a.LastAccessTicks.CompareTo(b.LastAccessTicks));
            int removed = 0;
            foreach (CacheFileInfo file in files)
            {
                if (total <= budget) break;
                if (!TryDelete(file.Path)) continue;
                total -= file.Bytes;
                TryDelete(file.MetadataPath);
                removed++;
            }
            return removed;
        }

        public int ClearAll()
        {
            int removed = 0;
            if (!Directory.Exists(_root)) return removed;

            // Match ChapterRepo.clear_cache(): clear committed images, but do
            // not cancel or remove an in-flight .part file. Such a request may
            // still complete and repopulate the cache after a manual clear.
            foreach (string path in Directory.GetFiles(_root, "*.img"))
            {
                string stem = Path.GetFileNameWithoutExtension(path);
                if (TryDelete(path)) removed++;
                TryDelete(Path.Combine(_root, stem + ".json"));
            }

            foreach (string path in Directory.GetFiles(_root, "*.json"))
                TryDelete(path);
            foreach (string path in Directory.GetFiles(_root, "*.json.tmp"))
                TryDelete(path);
            return removed;
        }

        public void GetStats(out int files, out long bytes)
        {
            files = 0;
            bytes = 0L;
            if (!Directory.Exists(_root)) return;
            foreach (string path in Directory.GetFiles(_root, "*.img"))
            {
                try
                {
                    bytes += new FileInfo(path).Length;
                    files++;
                }
                catch
                {
                    // A concurrently replaced entry can disappear during scan.
                }
            }
        }

        public static string ComputeSha256(string path)
        {
            using var stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        static bool HasSupportedImageHeader(string path)
        {
            var head = new byte[12];
            using var stream = File.OpenRead(path);
            int count = stream.Read(head, 0, head.Length);
            bool jpeg = count >= 3 &&
                        head[0] == 0xFF &&
                        head[1] == 0xD8 &&
                        head[2] == 0xFF;
            bool png = count >= 8 &&
                       head[0] == 0x89 &&
                       head[1] == 0x50 &&
                       head[2] == 0x4E &&
                       head[3] == 0x47 &&
                       head[4] == 0x0D &&
                       head[5] == 0x0A &&
                       head[6] == 0x1A &&
                       head[7] == 0x0A;
            bool webp = count >= 12 &&
                        head[0] == (byte)'R' &&
                        head[1] == (byte)'I' &&
                        head[2] == (byte)'F' &&
                        head[3] == (byte)'F' &&
                        head[8] == (byte)'W' &&
                        head[9] == (byte)'E' &&
                        head[10] == (byte)'B' &&
                        head[11] == (byte)'P';
            return jpeg || png || webp;
        }

        static bool TryDecodeTexture(
            string path,
            out Texture2D texture,
            out string error)
        {
            texture = null;
            try
            {
                byte[] data = File.ReadAllBytes(path);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(data, false) ||
                    texture.width <= 4 ||
                    texture.height <= 4)
                {
                    UnityEngine.Object.Destroy(texture);
                    texture = null;
                    error = "Unity could not decode the image";
                    return false;
                }
                texture.wrapMode = TextureWrapMode.Clamp;
                error = "";
                return true;
            }
            catch (Exception ex)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                texture = null;
                error = "image decode failed: " + ex.Message;
                return false;
            }
        }

        bool ShouldVerifyCachedHash(RemoteImageAsset asset)
        {
            RemoteImageCacheEntry entry = ReadEntry(MetadataPath(asset));
            return entry == null ||
                   !string.Equals(
                       NormalizeHash(entry.sha256),
                       NormalizeHash(asset.sha256),
                       StringComparison.OrdinalIgnoreCase);
        }

        void Touch(RemoteImageAsset asset)
        {
            string path = MetadataPath(asset);
            RemoteImageCacheEntry entry = ReadEntry(path);
            if (entry == null) return;
            long now = DateTime.UtcNow.Ticks;
            if (now - entry.lastAccessUtcTicks < TimeSpan.TicksPerMinute)
                return;
            entry.lastAccessUtcTicks = now;
            WriteEntry(asset, entry);
        }

        string MetadataPath(RemoteImageAsset asset)
        {
            return Path.Combine(_root, CacheKey(asset) + ".json");
        }

        void WriteEntry(RemoteImageAsset asset, RemoteImageCacheEntry entry)
        {
            string path = MetadataPath(asset);
            string temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, JsonUtility.ToJson(entry, true));
                ReplaceAtomically(temp, path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[RemoteImageDelivery] Could not write cache metadata: " +
                    ex.Message);
                TryDelete(temp);
            }
        }

        static RemoteImageCacheEntry ReadEntry(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return JsonUtility.FromJson<RemoteImageCacheEntry>(
                    File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        static void ReplaceAtomically(string source, string destination)
        {
            if (!File.Exists(destination))
            {
                File.Move(source, destination);
                return;
            }
            try
            {
                File.Replace(source, destination, null);
            }
            catch
            {
                File.Delete(destination);
                File.Move(source, destination);
            }
        }

        static bool TryDelete(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        void RemoveOrphanedTemporaryMetadata()
        {
            if (!Directory.Exists(_root)) return;
            foreach (string path in Directory.GetFiles(_root, "*.json.tmp"))
                TryDelete(path);
            if (!_config.resumePartialDownloads)
            {
                foreach (string path in Directory.GetFiles(_root, "*.part"))
                    TryDelete(path);
            }
        }

        List<CacheFileInfo> ScanDataFiles()
        {
            var result = new List<CacheFileInfo>();
            if (!Directory.Exists(_root)) return result;
            foreach (string path in Directory.GetFiles(_root, "*.img"))
            {
                try
                {
                    var info = new FileInfo(path);
                    string stem = Path.GetFileNameWithoutExtension(path);
                    string metadata = Path.Combine(_root, stem + ".json");
                    RemoteImageCacheEntry entry = ReadEntry(metadata);
                    long access = entry != null && entry.lastAccessUtcTicks > 0
                        ? entry.lastAccessUtcTicks
                        : info.LastAccessTimeUtc.Ticks;
                    result.Add(new CacheFileInfo
                    {
                        Path = path,
                        MetadataPath = metadata,
                        Bytes = info.Length,
                        LastAccessTicks = access,
                    });
                }
                catch
                {
                    // Skip files replaced during the scan.
                }
            }
            return result;
        }

        static string NormalizeHash(string value)
        {
            return (value ?? "")
                .Trim()
                .Replace("-", "")
                .ToLower(CultureInfo.InvariantCulture);
        }

        sealed class CacheFileInfo
        {
            public string Path;
            public string MetadataPath;
            public long Bytes;
            public long LastAccessTicks;
        }
    }
}
