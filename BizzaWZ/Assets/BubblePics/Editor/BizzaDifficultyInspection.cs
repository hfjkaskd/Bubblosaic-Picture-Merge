using System;
using System.IO;
using System.Collections.Generic;
using Bizza.Sdk;
using UnityEditor;
using UnityEngine;

namespace BubblePics.EditorTools
{
    public static class BizzaDifficultyInspection
    {
        public static void ConfigureAds(string folder)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var previous = ChannelConfig.Instance;
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, "ChannelConfig.bytes");
                var config = ChannelConfigBinarySerializer.Deserialize(File.ReadAllBytes(path));
                if (config.adStatisticsLevelRanges == null || config.adStatisticsLevelRanges.Count == 0)
                    throw new InvalidOperationException("Missing ad level ranges.");
                bool hasCoverage = config.adStatisticsLevelRanges.Exists(range => range.StartLevel == 1 && range.EndLevel == int.MaxValue);
                if (!hasCoverage)
                    config.adStatisticsLevelRanges.Insert(0, new AdStatisticsLevelRange
                    {
                        StartLevel = 1, EndLevel = int.MaxValue,
                        // Other missing-range fields retain the framework's previous default values.
                        Statistics = default
                    });
                foreach (var range in config.adStatisticsLevelRanges)
                {
                    var statistics = range.Statistics;
                    statistics.CloseGetRewardCount = 3;
                    statistics.InterAdStartLevel = 4;
                    if (statistics.ShowGetRewardCount <= 0) statistics.ShowGetRewardCount = 4;
                    range.Statistics = statistics;
                }
                File.WriteAllBytes(path, ChannelConfigBinarySerializer.Serialize(config));
                AssetDatabase.Refresh();
            }
            finally { ChannelConfig.Instance = previous; }
        }

        public static void Verify(string folder)
        {
            var lines = new List<string>();
            var previous = ChannelConfig.Instance;
            try
            {
                var config = ChannelConfigBinarySerializer.Deserialize(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "ChannelConfig.bytes")));
                foreach (var range in config.adStatisticsLevelRanges)
                {
                    Require(range.Statistics.CloseGetRewardCount == 3 && range.Statistics.InterAdStartLevel == 4 && range.Statistics.ShowGetRewardCount > 0, "ad configuration");
                    lines.Add($"ads levels={range.StartLevel}-{range.EndLevel} close=3 start=4 rewardEvery={range.Statistics.ShowGetRewardCount}");
                }
                for (int level = 1; level <= 15; level++)
                {
                    Require(AdStatisticsConfigMiddleware.Get(level).CloseGetRewardCount == 3, "effective close cadence");
                    Require(InterstitialProtection.IsProtected(level, 4) == (level <= 3), "interstitial protection boundary");
                    Require(LevelRepo.TryGet(level, out var source), "level load");
                    string before = JsonUtility.ToJson(source);
                    var prepared = GameplayDifficulty.Prepare(source, level);
                    Require(prepared.layout == source.layout && prepared.DistinctImageCount() == source.DistinctImageCount() && prepared.token_count == source.token_count, "unchanged puzzle count/layout");
                    Require(JsonUtility.ToJson(source) == before, "source data unchanged");
                    var scheduler = new WaveScheduler();
                    scheduler.Build(prepared.layout, prepared.DistinctImageCount());
                    int minimum = scheduler.CountTotalFragments() - scheduler.DistinctImageCount();
                    int steps = GameplayDifficulty.StepLimit(level, prepared.step_limit, minimum, 0, false);
                    Require(steps > minimum, "solvable step floor");
                    float expected = level <= 5 ? 0 : ((level - 6) % 5 == 2 ? .2f : ((level - 6) % 5 == 4 ? .4f : 0));
                    Require(Mathf.Approximately(GameplayDifficulty.ForLevel(level).targetFailureRate, expected), "cycle phase");
                    lines.Add($"level={level} images={prepared.DistinctImageCount()} minimumMerges={minimum} steps={steps} targetFailure={expected}");
                }
                int oldLevel = InterstitialProtection.ActiveLevel;
                int oldCounter = NumbericalStatistics.CloseGetRewardNum;
                try
                {
                    NumbericalStatistics.CloseGetRewardNum = 0;
                    for (int close = 1; close <= 9; close++)
                        Require(NumbericalStatistics.AdvanceRewardCloseCounter(3) == (close % 3 == 0), "close cadence 3/6/9");
                    lines.Add("close counter: triggers only on 3, 6, 9; resets once per trigger");
                    for (int level = 1; level <= 3; level++)
                    {
                        InterstitialProtection.BeginLevel(level);
                        int callbacks = 0;
                        BizzaSdk.Ad.ShowInterAd(new ShowAdArgs { onFinish = result => { Require(!result.success, "protected result"); callbacks++; } });
                        BizzaSdk.Ad.ShowInterAd("validation", 0, result => callbacks++, true);
                        Require(callbacks == 2, "both interstitial overloads complete exactly once without SDK");
                        NumbericalStatistics.CloseGetRewardNum = 2;
                        Require(!NumbericalStatistics.CheckCloseGetReward(E_AdPos.GetReward, 0, null) && NumbericalStatistics.CloseGetRewardNum == 0, "no protected close accumulation");
                        lines.Add($"protected actual API level={level} callbacks=2 no ad request");
                    }
                }
                finally
                {
                    InterstitialProtection.BeginLevel(oldLevel);
                    NumbericalStatistics.CloseGetRewardNum = oldCounter;
                }
                File.WriteAllLines(Path.Combine(folder, "difficulty-ad-verification.txt"), lines);
            }
            finally { ChannelConfig.Instance = previous; }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Validation failed: " + message);
        }
    }
}
