using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    public static partial class LevelImageLoader
    {
        // Metadata only: do not pin another level's textures in memory.
        static LevelData lastAvailableLevel;
        static readonly string[] LocalSources =
        {
            LevelContentPolicy.BundledSource,
            LevelContentPolicy.StreamingSource,
            LevelContentPolicy.CacheSource,
            LevelContentPolicy.CdnSource, // decoded cache only; never requests CDN here
        };

        static IEnumerator LoadAvailableForGameplay(LevelData requested,
            Action<Texture2D[]> completed, Action<string> failed, Action<float> progress)
        {
            if (requested == null) { failed?.Invoke("level data is null"); yield break; }
            float highestProgress = 0f;
            void Report(float value)
            {
                highestProgress = Mathf.Max(highestProgress, Mathf.Clamp01(value));
                progress?.Invoke(highestProgress);
            }
            LevelImageLoadResult result = null;
            yield return LoadCompleteLocalLevel(requested, value => result = value, Report);
            if (result == null)
            {
                foreach (LevelData donor in PreviousLevelCandidates(requested.level))
                {
                    yield return LoadCompleteLocalLevel(donor, value => result = value, Report);
                    if (result == null) continue;
                    result.Level.level_data_source = "reused_local_level_" + donor.level;
                    Debug.LogWarning($"Level {requested.level}: missing content; reusing complete local level {donor.level}.");
                    break;
                }
            }
            if (result == null)
            {
                failed?.Invoke("No complete local level is available, including bundled starter levels.");
                yield break;
            }
            // Keep progression/reward identity. The whole layout and its matching
            // pictures travel together, including in the existing frozen round save.
            requested.CopyPlayableContentFrom(result.Level);
            lastAvailableLevel = requested.Clone();
            Report(1f);
            completed?.Invoke(result.Textures);
        }

        static IEnumerator LoadCompleteLocalLevel(LevelData source,
            Action<LevelImageLoadResult> completed, Action<float> progress)
        {
            if (!LevelRepo.TryResolvePlayable(source, out LevelData playable, out _)) yield break;
            Texture2D[] textures = null;
            yield return LoadInternal(playable, LocalSources, false, false,
                value => textures = value, null, progress);
            if (textures != null)
                completed(new LevelImageLoadResult { Level = playable, Textures = textures });
        }

        static IEnumerable<LevelData> PreviousLevelCandidates(int requestedLevel)
        {
            if (lastAvailableLevel != null && lastAvailableLevel.level < requestedLevel)
                yield return lastAvailableLevel.Clone();

            int last = Math.Min(requestedLevel - 1, LevelRepo.Count);
            int first = Math.Max(1, last - LevelContentPolicy.Current.previous_level_search_limit + 1);
            for (int level = last; level >= first; level--)
            {
                if (LevelRepo.TryGet(level, out LevelData selected)) yield return selected;
                // Authored bundled data can differ from the remote AB selection.
                if (LevelRepo.TryGetBundled(level, out LevelData bundled) &&
                    (selected == null || selected.level_unique_id != bundled.level_unique_id ||
                     selected.layout != bundled.layout))
                    yield return bundled;
            }

            // Small, bounded seed range, independent of the missing chapter.
            int seedCount = BubblePicsRemoteImageDelivery.Current?.Config.bundledSeedItemCount ?? 10;
            seedCount = Math.Max(1, Math.Min(seedCount, 25));
            if (requestedLevel > 1) seedCount = Math.Min(seedCount, requestedLevel - 1);
            for (int level = seedCount; level >= 1; level--)
                if (LevelRepo.TryGetBundled(level, out LevelData seed)) yield return seed;
        }
    }
}
