using System;
using System.Collections.Generic;
using UnityEngine;

namespace RemoteImageDelivery
{
    /// <summary>
    /// Project adapters expose their ordered items (levels, cards, episodes,
    /// etc.) through this small interface. Item indices are one-based.
    /// </summary>
    public interface IRemoteImageBatchSource
    {
        int ItemCount { get; }
        IReadOnlyList<RemoteImageAsset> GetAssetsForItem(int itemIndex);
    }

    /// <summary>
    /// Optional cache-pruning view for projects where one playable asset maps
    /// to several disk-cache variants. It also exposes only the item metadata
    /// that the project has actually loaded, matching the recovered game's
    /// known-current-and-future keep set.
    /// </summary>
    public interface IRemoteImagePruneSource
    {
        IEnumerable<int> KnownItemIndices { get; }
        IReadOnlyList<RemoteImageAsset> GetAssetsToKeepForItem(int itemIndex);
    }

    /// <summary>
    /// Implements the recovered BubblePics download cadence in generic terms:
    /// 25 items/group, next item HIGH, first-entry current+next groups LOW,
    /// then an 11-item LOW sliding window after every advance.
    /// </summary>
    public sealed class RemoteImageBatchPrefetcher
    {
        readonly RemoteImageDeliveryClient _client;
        readonly IRemoteImageBatchSource _source;
        readonly RemoteImageDeliveryConfig _config;
        readonly SortedSet<int> _pendingLowItems = new();
        readonly SortedSet<int> _knownItems = new();
        bool _initialGroupsQueued;
        bool _lowItemRunning;

        public RemoteImageBatchPrefetcher(
            RemoteImageDeliveryClient client,
            IRemoteImageBatchSource source)
        {
            _client = client ??
                throw new ArgumentNullException(nameof(client));
            _source = source ??
                throw new ArgumentNullException(nameof(source));
            _config = client.Config;
        }

        public void NotifyForegroundItemReady(int itemIndex)
        {
            int current = ClampItem(itemIndex);
            if (current <= 0) return;
            _knownItems.Add(current);

            if (_config.prefetchNextItemHigh)
                QueueItem(current + 1, RemoteImagePriority.High);

            if (_config.prefetchCurrentAndNextGroup &&
                !_initialGroupsQueued)
            {
                _initialGroupsQueued = true;
                int group = GroupOf(current);
                QueueWholeGroup(group);
                QueueWholeGroup(group + 1);
            }
        }

        public void NotifyItemAdvanced(int currentItemIndex)
        {
            int current = ClampItem(currentItemIndex);
            if (current <= 0) return;

            _pendingLowItems.RemoveWhere(item => item < current);
            int last = Math.Min(
                _source.ItemCount,
                current + _config.advancePrefetchWindow - 1);
            MarkKnownRange(current, last);

            int position = PositionInGroup(current);
            if (_config.itemsPerGroup - position <
                _config.nextGroupThreshold)
            {
                // The original fetches the next chapter metadata here. Unity's
                // level catalog is already local, so mark that group as known
                // without eagerly downloading the entire group.
                MarkKnownGroup(GroupOf(current) + 1);
            }

            // ChapterRepo waits for the 11-level window to finish (success or
            // failure per item) before deleting old committed files.
            PrefetchAdvanceItem(current, current, last);
        }

        public void QueueForegroundItem(int itemIndex)
        {
            QueueItem(itemIndex, RemoteImagePriority.High);
        }

        public void QueueRange(
            int firstInclusive,
            int lastInclusive,
            RemoteImagePriority priority)
        {
            if (_source.ItemCount <= 0) return;
            int first = Mathf.Clamp(firstInclusive, 1, _source.ItemCount);
            int last = Mathf.Clamp(lastInclusive, 1, _source.ItemCount);
            if (last < first) return;
            for (int item = first; item <= last; item++)
            {
                _knownItems.Add(item);
                if (priority == RemoteImagePriority.Low)
                    _pendingLowItems.Add(item);
                else
                    QueueItemImmediate(item, priority);
            }
            if (priority == RemoteImagePriority.Low)
                StartNextLowItem();
        }

        public void PruneItemsBefore(int currentItemIndex)
        {
            var futureKeys = new HashSet<string>(StringComparer.Ordinal);
            int first = Mathf.Clamp(
                currentItemIndex,
                1,
                Math.Max(1, _source.ItemCount));

            var known = new SortedSet<int>(_knownItems);
            IRemoteImagePruneSource pruneSource =
                _source as IRemoteImagePruneSource;
            if (pruneSource?.KnownItemIndices != null)
            {
                foreach (int item in pruneSource.KnownItemIndices)
                {
                    if (item >= 1 && item <= _source.ItemCount)
                        known.Add(item);
                }
            }

            foreach (int item in known)
            {
                if (item < first) continue;
                IReadOnlyList<RemoteImageAsset> assets =
                    pruneSource != null
                        ? pruneSource.GetAssetsToKeepForItem(item)
                        : _source.GetAssetsForItem(item);
                AddStableKeys(assets, futureKeys);
            }
            _client.PruneExcept(futureKeys);
            _knownItems.RemoveWhere(item => item < first);
        }

        void QueueWholeGroup(int group)
        {
            if (group <= 0) return;
            int first = (group - 1) * _config.itemsPerGroup + 1;
            int last = group * _config.itemsPerGroup;
            QueueRange(first, last, RemoteImagePriority.Low);
        }

        void QueueItem(int item, RemoteImagePriority priority)
        {
            if (priority == RemoteImagePriority.Low)
            {
                if (item >= 1 && item <= _source.ItemCount)
                {
                    _knownItems.Add(item);
                    _pendingLowItems.Add(item);
                }
                StartNextLowItem();
                return;
            }
            QueueItemImmediate(item, priority);
        }

        void QueueItemImmediate(int item, RemoteImagePriority priority)
        {
            if (item < 1 || item > _source.ItemCount) return;
            _knownItems.Add(item);
            IReadOnlyList<RemoteImageAsset> assets =
                _source.GetAssetsForItem(item);
            if (assets == null) return;
            foreach (RemoteImageAsset asset in assets)
            {
                if (asset != null)
                    _client.Prefetch(asset, priority);
            }
        }

        void PrefetchAdvanceItem(
            int pruneCurrent,
            int item,
            int last)
        {
            if (item > last)
            {
                if (_config.pruneItemsBeforeCurrent)
                    PruneItemsBefore(pruneCurrent);
                return;
            }

            _knownItems.Add(item);
            IReadOnlyList<RemoteImageAsset> sourceAssets =
                _source.GetAssetsForItem(item);
            List<RemoteImageAsset> unique = UniqueAssets(sourceAssets);
            if (unique.Count == 0)
            {
                PrefetchAdvanceItem(pruneCurrent, item + 1, last);
                return;
            }

            int remaining = unique.Count;
            foreach (RemoteImageAsset asset in unique)
            {
                _client.Prefetch(
                    asset,
                    RemoteImagePriority.Low,
                    _ =>
                    {
                        remaining--;
                        if (remaining == 0)
                        {
                            PrefetchAdvanceItem(
                                pruneCurrent,
                                item + 1,
                                last);
                        }
                    });
            }
        }

        void StartNextLowItem()
        {
            if (_lowItemRunning || _pendingLowItems.Count == 0) return;
            int item = _pendingLowItems.Min;
            _pendingLowItems.Remove(item);
            IReadOnlyList<RemoteImageAsset> sourceAssets =
                _source.GetAssetsForItem(item);
            if (sourceAssets == null || sourceAssets.Count == 0)
            {
                StartNextLowItem();
                return;
            }

            List<RemoteImageAsset> unique = UniqueAssets(sourceAssets);
            if (unique.Count == 0)
            {
                StartNextLowItem();
                return;
            }

            _lowItemRunning = true;
            int remaining = unique.Count;
            foreach (RemoteImageAsset asset in unique)
            {
                _client.Prefetch(
                    asset,
                    RemoteImagePriority.Low,
                    _ =>
                    {
                        remaining--;
                        if (remaining > 0) return;
                        _lowItemRunning = false;
                        StartNextLowItem();
                    });
            }
        }

        void MarkKnownRange(int first, int last)
        {
            if (_source.ItemCount <= 0) return;
            int clampedFirst = Mathf.Clamp(first, 1, _source.ItemCount);
            int clampedLast = Mathf.Clamp(last, 1, _source.ItemCount);
            for (int item = clampedFirst; item <= clampedLast; item++)
                _knownItems.Add(item);
        }

        void MarkKnownGroup(int group)
        {
            if (group <= 0) return;
            int first = (group - 1) * _config.itemsPerGroup + 1;
            int last = Math.Min(
                _source.ItemCount,
                group * _config.itemsPerGroup);
            if (first <= last) MarkKnownRange(first, last);
        }

        static List<RemoteImageAsset> UniqueAssets(
            IReadOnlyList<RemoteImageAsset> sourceAssets)
        {
            var unique = new List<RemoteImageAsset>(
                sourceAssets?.Count ?? 0);
            if (sourceAssets == null) return unique;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (RemoteImageAsset asset in sourceAssets)
            {
                if (asset != null && seen.Add(asset.StableKey))
                    unique.Add(asset);
            }
            return unique;
        }

        static void AddStableKeys(
            IReadOnlyList<RemoteImageAsset> assets,
            ISet<string> output)
        {
            if (assets == null || output == null) return;
            foreach (RemoteImageAsset asset in assets)
            {
                if (asset != null)
                    output.Add(asset.StableKey);
            }
        }

        int ClampItem(int item)
        {
            if (_source.ItemCount <= 0) return 0;
            return Mathf.Clamp(item, 1, _source.ItemCount);
        }

        int GroupOf(int item)
        {
            return (item - 1) / _config.itemsPerGroup + 1;
        }

        int PositionInGroup(int item)
        {
            return (item - 1) % _config.itemsPerGroup + 1;
        }
    }
}
