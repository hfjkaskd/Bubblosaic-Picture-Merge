using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace RemoteImageDelivery
{
    /// <summary>
    /// Reusable priority download scheduler. The caller supplies an authored
    /// MonoBehaviour host, so integrating the package never creates a hidden
    /// scene object or changes a project's scene hierarchy.
    /// </summary>
    public sealed class RemoteImageDeliveryClient : IDisposable
    {
        sealed class RequestEntry
        {
            public RemoteImageAsset Asset;
            public RemoteImagePriority Priority;
            public bool NeedsTexture;
            public bool Started;
            public float Progress;
            public DateTime StartedUtc;
            public readonly List<Action<RemoteImageResult>> Callbacks = new();
        }

        sealed class MemoryEntry
        {
            public string Key;
            public Texture2D Texture;
        }

        readonly MonoBehaviour _host;
        readonly RemoteImageDeliveryConfig _config;
        readonly RemoteImageCache _cache;
        readonly LinkedList<RequestEntry> _highQueue = new();
        readonly LinkedList<RequestEntry> _lowQueue = new();
        readonly Dictionary<string, RequestEntry> _requests =
            new(StringComparer.Ordinal);
        readonly Dictionary<string, LinkedListNode<MemoryEntry>> _memory =
            new(StringComparer.Ordinal);
        readonly LinkedList<MemoryEntry> _memoryLru = new();
        readonly List<RemoteImageRequestRecord> _history = new();

        int _activeHigh;
        int _activeLow;
        long _sessionDownloadedBytes;
        int _sessionNetworkSuccesses;
        int _sessionFailures;
        int _sessionMemoryHits;
        int _sessionDiskHits;
        int _cacheDecodeFrame = -1;
        int _cacheDecodesThisFrame;
        bool _disposed;

        // Texture2D.LoadImage runs on Unity's main thread.  A first-entry
        // batch can contain dozens of already-cached files, so decoding every
        // cache hit synchronously from Enqueue stalls the frame before the
        // level has a chance to call StartRound.  Keep the original full
        // decode validation, but time-slice it across rendered frames.

        /// <summary>
        /// Latest client created in this process. This is only for editor
        /// diagnostics; game code should keep its own explicit reference.
        /// </summary>
        public static RemoteImageDeliveryClient Active { get; private set; }

        public RemoteImageDeliveryConfig Config => _config;
        public RemoteImageCache Cache => _cache;
        public IReadOnlyList<RemoteImageRequestRecord> History => _history;

        public event Action SnapshotChanged;

        public RemoteImageDeliveryClient(
            MonoBehaviour host,
            RemoteImageDeliveryConfig config)
        {
            _host = host != null
                ? host
                : throw new ArgumentNullException(nameof(host));
            _config = config != null
                ? config
                : throw new ArgumentNullException(nameof(config));
            _config.Sanitize();
            _cache = new RemoteImageCache(_config);
            Active = this;
        }

        public bool HasCached(RemoteImageAsset asset)
        {
            if (asset == null) return false;
            if (TryGetMemory(asset.StableKey, out _)) return true;
            return _cache.HasValidFile(asset);
        }

        /// <summary>
        /// Returns the current transport progress for a queued or active
        /// request. Completion callbacks remain the source of truth for the
        /// final 100 percent state.
        /// </summary>
        public float GetRequestProgress(RemoteImageAsset asset)
        {
            if (asset == null ||
                string.IsNullOrWhiteSpace(asset.StableKey) ||
                !_requests.TryGetValue(asset.StableKey, out RequestEntry entry))
            {
                return 0f;
            }
            return Mathf.Clamp01(entry.Progress);
        }

        public void RequestTexture(
            RemoteImageAsset asset,
            RemoteImagePriority priority,
            Action<RemoteImageResult> completed)
        {
            Enqueue(asset, priority, true, completed);
        }

        public void Prefetch(
            RemoteImageAsset asset,
            RemoteImagePriority priority = RemoteImagePriority.Low,
            Action<RemoteImageResult> completed = null)
        {
            Enqueue(asset, priority, false, completed);
        }

        public RemoteImageDeliverySnapshot GetSnapshot()
        {
            _cache.GetStats(out int cacheFiles, out long cacheBytes);
            return new RemoteImageDeliverySnapshot
            {
                ActiveHigh = _activeHigh,
                ActiveLow = _activeLow,
                QueuedHigh = _highQueue.Count,
                QueuedLow = _lowQueue.Count,
                InFlightUnique = _requests.Count,
                SessionDownloadedBytes = _sessionDownloadedBytes,
                SessionNetworkSuccesses = _sessionNetworkSuccesses,
                SessionFailures = _sessionFailures,
                SessionMemoryHits = _sessionMemoryHits,
                SessionDiskHits = _sessionDiskHits,
                CacheBytes = cacheBytes,
                CacheFiles = cacheFiles,
                CacheRoot = _cache.RootPath,
            };
        }

        public int PruneExcept(ISet<string> stableKeys)
        {
            int removed = _cache.PruneExcept(stableKeys);
            if (_config.enforceDiskCacheBudget)
                removed += _cache.PruneToBudget();
            NotifyChanged();
            return removed;
        }

        public int PruneToBudget()
        {
            int removed = _cache.PruneToBudget();
            NotifyChanged();
            return removed;
        }

        public int ClearDiskCache()
        {
            int removed = _cache.ClearAll();
            NotifyChanged();
            return removed;
        }

        /// <summary>
        /// Drops only the client's lookup table. Dynamic textures are not
        /// destroyed here because active UI/game objects may still reference
        /// them; Unity can unload them after those consumers release them.
        /// </summary>
        public void ClearMemoryCache()
        {
            _memory.Clear();
            _memoryLru.Clear();
            NotifyChanged();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            FailQueued(_highQueue, "remote image client was disposed");
            FailQueued(_lowQueue, "remote image client was disposed");
            _requests.Clear();
            ClearMemoryCache();
            if (ReferenceEquals(Active, this)) Active = null;
        }

        void Enqueue(
            RemoteImageAsset original,
            RemoteImagePriority priority,
            bool needsTexture,
            Action<RemoteImageResult> completed)
        {
            if (_disposed)
            {
                completed?.Invoke(Failure(
                    original,
                    "remote image client is disposed"));
                return;
            }
            if (original == null ||
                string.IsNullOrWhiteSpace(original.StableKey))
            {
                completed?.Invoke(Failure(original, "image asset is invalid"));
                return;
            }

            RemoteImageAsset asset = original.Clone();
            string key = asset.StableKey;
            if (needsTexture &&
                TryGetMemory(key, out Texture2D memoryTexture))
            {
                _sessionMemoryHits++;
                RemoteImageResult hit = Success(
                    asset,
                    memoryTexture,
                    _cache.DataPath(asset),
                    RemoteImageResultSource.MemoryCache,
                    0,
                    0);
                AddHistory(asset, priority, hit, "", DateTime.UtcNow);
                completed?.Invoke(hit);
                NotifyChanged();
                return;
            }

            if (_requests.TryGetValue(key, out RequestEntry existing))
            {
                existing.NeedsTexture |= needsTexture;
                if (completed != null) existing.Callbacks.Add(completed);
                if (!existing.Started &&
                    priority == RemoteImagePriority.High &&
                    existing.Priority == RemoteImagePriority.Low)
                {
                    existing.Priority = RemoteImagePriority.High;
                    RemoveFromQueue(_lowQueue, existing);
                    _highQueue.AddLast(existing);
                }
                Pump();
                NotifyChanged();
                return;
            }

            var entry = new RequestEntry
            {
                Asset = asset,
                Priority = priority,
                NeedsTexture = needsTexture,
            };
            if (completed != null) entry.Callbacks.Add(completed);
            _requests.Add(key, entry);
            if (priority == RemoteImagePriority.High)
                _highQueue.AddLast(entry);
            else
                _lowQueue.AddLast(entry);
            Pump();
            NotifyChanged();
        }

        void Pump()
        {
            if (_disposed) return;
            while (_activeHigh < _config.highPriorityConcurrency &&
                   _highQueue.Count > 0)
            {
                RequestEntry entry = TakeFirst(_highQueue);
                entry.Started = true;
                entry.StartedUtc = DateTime.UtcNow;
                _activeHigh++;
                _host.StartCoroutine(
                    RunRequest(entry, RemoteImagePriority.High));
            }
            while (_activeLow < _config.lowPriorityConcurrency &&
                   _lowQueue.Count > 0)
            {
                RequestEntry entry = TakeFirst(_lowQueue);
                entry.Started = true;
                entry.StartedUtc = DateTime.UtcNow;
                _activeLow++;
                _host.StartCoroutine(
                    RunRequest(entry, RemoteImagePriority.Low));
            }
        }

        IEnumerator RunRequest(
            RequestEntry entry,
            RemoteImagePriority scheduledPriority)
        {
            RemoteImageAsset asset = entry.Asset;
            string url = _config.ResolveUrl(asset);
            int maxAttempts = scheduledPriority == RemoteImagePriority.High
                ? _config.foregroundMaxAttempts
                : _config.backgroundMaxAttempts;
            int attempts = 0;
            string error = "";
            string etag = "";
            string lastModified = "";
            long downloaded = 0L;
            float retryAfterSeconds = 0f;

            // Disk hits used to be decoded synchronously by Enqueue.  Besides
            // blocking the caller, an immediate prefetch callback recursively
            // advanced through every cached level in the batch in one frame.
            // Defer the expensive read/decode and serialize it across frames.
            if (!_disposed && HasCacheCandidate(asset))
            {
                yield return WaitForCacheDecodeSlot();
                if (!_disposed &&
                    _cache.TryLoadTexture(
                        asset,
                        out Texture2D diskTexture,
                        out string diskPath,
                        out _))
                {
                    Texture2D resultTexture = null;
                    if (entry.NeedsTexture)
                    {
                        resultTexture = diskTexture;
                        Remember(asset.StableKey, diskTexture);
                    }
                    else
                    {
                        // Original ChapterRepo fully decodes a cached image
                        // while warming a level and deletes it immediately if
                        // decoding fails.  Preserve that validation contract.
                        UnityEngine.Object.Destroy(diskTexture);
                    }

                    _sessionDiskHits++;
                    RemoteImageResult diskHit = Success(
                        asset,
                        resultTexture,
                        diskPath,
                        RemoteImageResultSource.DiskCache,
                        0,
                        ElapsedMilliseconds(entry.StartedUtc));
                    Finish(
                        entry,
                        scheduledPriority,
                        diskHit,
                        url);
                    yield break;
                }
                // TryLoadTexture removes a corrupt committed file.  Continue
                // into the normal network path so the same request repairs it.
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                error = "no delivery URL could be resolved";
            }
            else if (!_config.remoteEnabled)
            {
                error = "remote delivery is disabled";
            }
            else
            {
                while (!_disposed && attempts < maxAttempts)
                {
                    attempts++;
                    bool retry = false;
                    bool forceFull = false;
                    bool repeatWithoutCounting = true;

                    while (!_disposed && repeatWithoutCounting)
                    {
                        repeatWithoutCounting = false;
                        long partialLength = _cache.PartialLength(asset);
                        bool resume =
                            !forceFull &&
                            _config.resumePartialDownloads &&
                            partialLength > 0 &&
                            (asset.byteSize <= 0 ||
                             asset.byteSize >= _config.resumeMinimumBytes) &&
                            (asset.byteSize <= 0 ||
                             partialLength < asset.byteSize);

                        if (!resume && partialLength > 0)
                        {
                            _cache.DeletePartial(asset);
                            partialLength = 0;
                        }

                        using var request = new UnityWebRequest(
                            url,
                            UnityWebRequest.kHttpVerbGET);
                        var handler = new DownloadHandlerFile(
                            _cache.PartialPath(asset),
                            resume);
                        handler.removeFileOnAbort = false;
                        request.downloadHandler = handler;
                        request.disposeDownloadHandlerOnDispose = true;
                        request.timeout =
                            _config.cancelNetworkRequestOnTimeout
                                ? _config.requestTimeoutSeconds
                                : 0;
                        if (!string.IsNullOrWhiteSpace(_config.acceptHeader))
                        {
                            request.SetRequestHeader(
                                "Accept",
                                _config.acceptHeader);
                        }
                        if (resume)
                        {
                            request.SetRequestHeader(
                                "Range",
                                "bytes=" + partialLength + "-");
                        }

                        if (_config.verboseLogging)
                        {
                            Debug.Log(
                                $"[RemoteImageDelivery] {scheduledPriority} " +
                                $"GET {url}" +
                                (resume ? $" from {partialLength}" : ""));
                        }

                        UnityWebRequestAsyncOperation operation;
                        try
                        {
                            operation = request.SendWebRequest();
                        }
                        catch (Exception ex)
                        {
                            error =
                                "request start failed: " + ex.Message;
                            retry = false;
                            _cache.DeletePartial(asset);
                            break;
                        }

                        while (!operation.isDone)
                        {
                            float requestProgress;
                            if (asset.byteSize > 0)
                            {
                                requestProgress =
                                    (partialLength +
                                     (long)request.downloadedBytes) /
                                    (float)asset.byteSize;
                            }
                            else
                            {
                                requestProgress = request.downloadProgress;
                            }
                            // Reserve the final percent for the completion
                            // callback, after validation and cache commit.
                            entry.Progress = Mathf.Clamp(
                                requestProgress,
                                0f,
                                0.99f);
                            yield return null;
                        }
                        long responseCode = request.responseCode;
                        etag = request.GetResponseHeader("ETag") ?? "";
                        lastModified =
                            request.GetResponseHeader("Last-Modified") ?? "";
                        retryAfterSeconds = ParseRetryAfter(
                            request.GetResponseHeader("Retry-After"));

                        if (resume && responseCode == 200)
                        {
                            // A server that ignored Range wrote a complete file
                            // after the partial bytes. Restart cleanly without
                            // consuming an additional configured retry.
                            _cache.DeletePartial(asset);
                            forceFull = true;
                            repeatWithoutCounting = true;
                            continue;
                        }

                        bool httpSuccess =
                            responseCode >= 200 && responseCode < 300;
                        if (request.result ==
                                UnityWebRequest.Result.Success &&
                            httpSuccess)
                        {
                            if (_cache.CommitPart(
                                    asset,
                                    url,
                                    etag,
                                    lastModified,
                                    out _,
                                    out error))
                            {
                                downloaded = Math.Max(
                                    0L,
                                    (long)request.downloadedBytes);
                                retry = false;
                                break;
                            }
                            retry = true;
                            _cache.DeletePartial(asset);
                        }
                        else if (responseCode == 416 &&
                                 asset.byteSize > 0 &&
                                 _cache.PartialLength(asset) ==
                                 asset.byteSize &&
                                 _cache.CommitPart(
                                     asset,
                                     url,
                                     etag,
                                     lastModified,
                                     out _,
                                     out error))
                        {
                            retry = false;
                            break;
                        }
                        else
                        {
                            error = BuildRequestError(request, responseCode);
                            retry = IsRetryable(request, responseCode);
                            if (!_config.resumePartialDownloads ||
                                responseCode >= 400)
                                _cache.DeletePartial(asset);
                        }
                    }

                    if (_cache.HasValidFile(asset))
                    {
                        error = "";
                        break;
                    }
                    if (!retry || attempts >= maxAttempts) break;

                    float delay = retryAfterSeconds > 0f
                        ? retryAfterSeconds
                        : _config.retryBaseDelaySeconds *
                          Mathf.Pow(2f, attempts - 1);
                    delay *= 1f + UnityEngine.Random.Range(
                        -_config.retryJitter,
                        _config.retryJitter);
                    if (delay > 0f)
                        yield return new WaitForSecondsRealtime(delay);
                }
            }

            RemoteImageResult result;
            string cacheError = "";
            string path = "";
            if (!_disposed &&
                _cache.TryGetValidFile(asset, out path, out cacheError))
            {
                Texture2D texture = null;
                if (entry.NeedsTexture)
                {
                    if (!_cache.TryLoadTexture(
                            asset,
                            out texture,
                            out path,
                            out cacheError))
                    {
                        result = Failure(
                            asset,
                            cacheError,
                            attempts,
                            ElapsedMilliseconds(entry.StartedUtc));
                        Finish(
                            entry,
                            scheduledPriority,
                            result,
                            url);
                        yield break;
                    }
                    Remember(asset.StableKey, texture);
                }

                long bytes = 0L;
                try
                {
                    if (File.Exists(path))
                        bytes = new FileInfo(path).Length;
                }
                catch
                {
                    bytes = downloaded;
                }
                result = Success(
                    asset,
                    texture,
                    path,
                    RemoteImageResultSource.Network,
                    attempts,
                    ElapsedMilliseconds(entry.StartedUtc),
                    bytes);
                _sessionDownloadedBytes += downloaded > 0 ? downloaded : bytes;
                _sessionNetworkSuccesses++;
                if (_config.enforceDiskCacheBudget)
                    _cache.PruneToBudget();
            }
            else
            {
                if (!_config.resumePartialDownloads)
                    _cache.DeletePartial(asset);
                if (string.IsNullOrWhiteSpace(error)) error = cacheError;
                if (string.IsNullOrWhiteSpace(error))
                    error = _disposed
                        ? "remote image client was disposed"
                        : "download did not produce a valid cache file";
                result = Failure(
                    asset,
                    error,
                    attempts,
                    ElapsedMilliseconds(entry.StartedUtc));
                _sessionFailures++;
            }

            Finish(entry, scheduledPriority, result, url);
        }

        bool HasCacheCandidate(RemoteImageAsset asset)
        {
            if (asset == null) return false;
            try
            {
                // File.Exists is intentionally the only synchronous cache
                // work here. Header/hash/decode validation remains in the
                // time-sliced path below.
                return File.Exists(_cache.DataPath(asset));
            }
            catch
            {
                return false;
            }
        }

        IEnumerator WaitForCacheDecodeSlot()
        {
            // Always give the caller one render opportunity.  In particular,
            // OpenLevel can finish StartRound before any background batch
            // starts decoding cached textures.
            yield return null;

            while (!_disposed)
            {
                int frame = Time.frameCount;
                if (_cacheDecodeFrame != frame)
                {
                    _cacheDecodeFrame = frame;
                    _cacheDecodesThisFrame = 0;
                }

                int limit = Mathf.Max(1, _config.cacheDecodesPerFrame);
                if (_cacheDecodesThisFrame < limit)
                {
                    _cacheDecodesThisFrame++;
                    yield break;
                }
                yield return null;
            }
        }

        void Finish(
            RequestEntry entry,
            RemoteImagePriority scheduledPriority,
            RemoteImageResult result,
            string url)
        {
            if (scheduledPriority == RemoteImagePriority.High)
                _activeHigh = Math.Max(0, _activeHigh - 1);
            else
                _activeLow = Math.Max(0, _activeLow - 1);
            _requests.Remove(entry.Asset.StableKey);
            AddHistory(
                entry.Asset,
                entry.Priority,
                result,
                url,
                entry.StartedUtc);

            Action<RemoteImageResult>[] callbacks =
                entry.Callbacks.ToArray();
            foreach (Action<RemoteImageResult> callback in callbacks)
            {
                try
                {
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            Pump();
            NotifyChanged();
        }

        void AddHistory(
            RemoteImageAsset asset,
            RemoteImagePriority priority,
            RemoteImageResult result,
            string url,
            DateTime startedUtc)
        {
            if (!_config.keepRequestHistory) return;
            _history.Add(new RemoteImageRequestRecord
            {
                key = asset?.StableKey ?? "",
                url = url ?? "",
                relativePath = asset?.relativePath ?? "",
                localPath = result?.LocalPath ?? "",
                error = result?.Error ?? "",
                startedUtc = startedUtc.ToString("O"),
                bytes = result?.Bytes ?? 0,
                durationMilliseconds =
                    result?.DurationMilliseconds ?? 0,
                attempts = result?.Attempts ?? 0,
                groupIndex = asset?.groupIndex ?? 0,
                itemIndex = asset?.itemIndex ?? 0,
                priority = priority,
                source = result?.Source ?? RemoteImageResultSource.None,
                state = result != null && result.Success
                    ? RemoteImageRequestState.Succeeded
                    : RemoteImageRequestState.Failed,
            });
            int over = _history.Count - _config.requestHistoryLimit;
            if (over > 0) _history.RemoveRange(0, over);
        }

        void Remember(string key, Texture2D texture)
        {
            if (string.IsNullOrWhiteSpace(key) || texture == null) return;
            if (_memory.TryGetValue(key, out var existing))
            {
                existing.Value.Texture = texture;
                _memoryLru.Remove(existing);
                _memoryLru.AddLast(existing);
            }
            else
            {
                var item = new MemoryEntry { Key = key, Texture = texture };
                LinkedListNode<MemoryEntry> node =
                    _memoryLru.AddLast(item);
                _memory.Add(key, node);
            }

            while (_memoryLru.Count > _config.memoryTextureLimit)
            {
                LinkedListNode<MemoryEntry> first = _memoryLru.First;
                _memoryLru.RemoveFirst();
                _memory.Remove(first.Value.Key);
                // Do not Destroy: a level that received this texture may still
                // be displaying it. See ClearMemoryCache documentation.
            }
        }

        bool TryGetMemory(string key, out Texture2D texture)
        {
            texture = null;
            if (!_memory.TryGetValue(key, out var node)) return false;
            if (node.Value.Texture == null)
            {
                _memory.Remove(key);
                _memoryLru.Remove(node);
                return false;
            }
            texture = node.Value.Texture;
            _memoryLru.Remove(node);
            _memoryLru.AddLast(node);
            return true;
        }

        void FailQueued(
            LinkedList<RequestEntry> queue,
            string error)
        {
            while (queue.Count > 0)
            {
                RequestEntry entry = TakeFirst(queue);
                RemoteImageResult result = Failure(entry.Asset, error);
                foreach (Action<RemoteImageResult> callback in entry.Callbacks)
                    callback?.Invoke(result);
            }
        }

        void NotifyChanged()
        {
            SnapshotChanged?.Invoke();
        }

        static RequestEntry TakeFirst(LinkedList<RequestEntry> queue)
        {
            RequestEntry value = queue.First.Value;
            queue.RemoveFirst();
            return value;
        }

        static void RemoveFromQueue(
            LinkedList<RequestEntry> queue,
            RequestEntry entry)
        {
            LinkedListNode<RequestEntry> node = queue.First;
            while (node != null)
            {
                LinkedListNode<RequestEntry> next = node.Next;
                if (ReferenceEquals(node.Value, entry))
                {
                    queue.Remove(node);
                    return;
                }
                node = next;
            }
        }

        static bool IsRetryable(
            UnityWebRequest request,
            long responseCode)
        {
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
                return true;
            return responseCode == 408 ||
                   responseCode == 425 ||
                   responseCode == 429 ||
                   responseCode >= 500;
        }

        static string BuildRequestError(
            UnityWebRequest request,
            long responseCode)
        {
            string detail = request.error ?? "request failed";
            return responseCode > 0
                ? $"HTTP {responseCode}: {detail}"
                : detail;
        }

        static float ParseRetryAfter(string value)
        {
            if (float.TryParse(value, out float seconds))
                return Mathf.Clamp(seconds, 0f, 120f);
            if (DateTime.TryParse(value, out DateTime when))
            {
                return Mathf.Clamp(
                    (float)(when.ToUniversalTime() - DateTime.UtcNow)
                        .TotalSeconds,
                    0f,
                    120f);
            }
            return 0f;
        }

        static long ElapsedMilliseconds(DateTime startedUtc)
        {
            return Math.Max(
                0L,
                (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds);
        }

        static RemoteImageResult Success(
            RemoteImageAsset asset,
            Texture2D texture,
            string path,
            RemoteImageResultSource source,
            int attempts,
            long elapsed,
            long bytes = 0)
        {
            return new RemoteImageResult
            {
                Asset = asset,
                Success = true,
                Texture = texture,
                LocalPath = path ?? "",
                Error = "",
                Bytes = bytes,
                Attempts = attempts,
                DurationMilliseconds = elapsed,
                Source = source,
            };
        }

        static RemoteImageResult Failure(
            RemoteImageAsset asset,
            string error,
            int attempts = 0,
            long elapsed = 0)
        {
            return new RemoteImageResult
            {
                Asset = asset,
                Success = false,
                Error = error ?? "request failed",
                Attempts = attempts,
                DurationMilliseconds = elapsed,
                Source = RemoteImageResultSource.None,
            };
        }
    }
}
