using System;
using UnityEngine;

namespace RemoteImageDelivery
{
    public enum RemoteImagePriority
    {
        High = 0,
        Low = 1,
    }

    public enum RemoteImageResultSource
    {
        None = 0,
        MemoryCache = 1,
        DiskCache = 2,
        Network = 3,
    }

    public enum RemoteImageRequestState
    {
        Queued = 0,
        Downloading = 1,
        Retrying = 2,
        Succeeded = 3,
        Failed = 4,
    }

    [Serializable]
    public sealed class RemoteImageAsset
    {
        public string key;
        public string relativePath;
        public string absoluteUrl;
        public string sha256;
        public long byteSize;
        public int width;
        public int height;
        public int groupIndex;
        public int itemIndex;
        public string variant;

        public string StableKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(key))
                    return key.Trim() + "|" + (variant ?? "");
                if (!string.IsNullOrWhiteSpace(relativePath))
                    return relativePath.Replace('\\', '/') + "|" + (variant ?? "");
                return (absoluteUrl ?? "") + "|" + width + "x" + height +
                       "|" + (variant ?? "");
            }
        }

        public RemoteImageAsset Clone()
        {
            return (RemoteImageAsset)MemberwiseClone();
        }
    }

    public sealed class RemoteImageResult
    {
        public RemoteImageAsset Asset;
        public bool Success;
        public Texture2D Texture;
        public string LocalPath;
        public string Error;
        public long Bytes;
        public int Attempts;
        public long DurationMilliseconds;
        public RemoteImageResultSource Source;
    }

    [Serializable]
    public sealed class RemoteImageRequestRecord
    {
        public string key;
        public string url;
        public string relativePath;
        public string localPath;
        public string error;
        public string startedUtc;
        public long bytes;
        public long durationMilliseconds;
        public int attempts;
        public int groupIndex;
        public int itemIndex;
        public RemoteImagePriority priority;
        public RemoteImageResultSource source;
        public RemoteImageRequestState state;
    }

    public sealed class RemoteImageDeliverySnapshot
    {
        public int ActiveHigh;
        public int ActiveLow;
        public int QueuedHigh;
        public int QueuedLow;
        public int InFlightUnique;
        public long SessionDownloadedBytes;
        public int SessionNetworkSuccesses;
        public int SessionFailures;
        public int SessionMemoryHits;
        public int SessionDiskHits;
        public long CacheBytes;
        public int CacheFiles;
        public string CacheRoot;
    }

    [Serializable]
    public sealed class RemoteImageCatalog
    {
        public int schemaVersion = 1;
        public string projectId;
        public string channel;
        public string contentVersion;
        public string generatedUtc;
        public int itemsPerGroup = 25;
        public RemoteImageCatalogGroup[] groups;
    }

    [Serializable]
    public sealed class RemoteImageCatalogGroup
    {
        public string id;
        public int groupIndex;
        public int firstItem;
        public int lastItem;
        public int assetCount;
        public long byteSize;
        public string relativePath;
        public string sha256;
    }

    [Serializable]
    public sealed class RemoteImageGroupManifest
    {
        public int schemaVersion = 1;
        public string groupId;
        public int groupIndex;
        public string contentVersion;
        public RemoteImageManifestItem[] items;
    }

    [Serializable]
    public sealed class RemoteImageManifestItem
    {
        public int itemIndex;
        public RemoteImageAsset[] assets;
    }
}
