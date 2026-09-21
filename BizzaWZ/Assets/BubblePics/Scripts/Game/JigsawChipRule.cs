using System;
using System.Collections.Generic;

namespace BubblePics
{
    /// <summary>
    /// Port of jigsaw_chip_rule.gd. The jigsaw presentation is an elite Hard
    /// round: it is selected only when the immediately preceding recorded
    /// round coefficient did not exceed the original 1.5 threshold.
    /// </summary>
    public static class JigsawChipRule
    {
        public const float EliteCoefficientMax = 1.5f;

        public static bool IsEnabled { get; private set; }

        public static void EvaluateOnLevelEnter(LevelData level)
        {
            IsEnabled = ShouldEnable(level, SaveState.RecentRoundCoefficients);
        }

        public static bool ShouldEnable(
            LevelData level,
            IReadOnlyList<float> recentCoefficients)
        {
            if (level == null || !string.Equals(
                    level.difficulty_type,
                    "Hard",
                    StringComparison.OrdinalIgnoreCase))
                return false;
            if (recentCoefficients == null || recentCoefficients.Count == 0)
                return false;
            return recentCoefficients[recentCoefficients.Count - 1] <=
                   EliteCoefficientMax;
        }

        public static float ComputeRoundCoefficient(
            int totalSteps,
            int linkSteps,
            bool withDeath,
            int remainingBubbles)
        {
            float ratio = Math.Max(0, totalSteps) /
                          (float)Math.Max(1, linkSteps);
            return withDeath
                ? ratio + Math.Max(0, remainingBubbles) * 0.7f * ratio + 0.1f
                : ratio + 0.1f;
        }
    }
}
