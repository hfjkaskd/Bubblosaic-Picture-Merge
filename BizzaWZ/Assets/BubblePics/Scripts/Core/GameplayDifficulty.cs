using System;
using UnityEngine;

namespace BubblePics
{
    public static class GameplayDifficulty
    {
        [Serializable, Obfuz.ObfuzIgnore]
        public sealed class Tier
        {
            public string label;
            public float targetFailureRate;
            public float extraStepsRatio;
            public int minimumExtraSteps;
            public bool retainOriginalMinimum;
        }

        [Serializable, Obfuz.ObfuzIgnore]
        public sealed class Settings
        {
            public int foundationLevels;
            public Tier foundation;
            public Tier[] cycle;
        }

        private static Settings settings;
        public static Settings Config
        {
            get
            {
                if (settings != null) return settings;
                var asset = Resources.Load<TextAsset>("Config/gameplay_difficulty");
                if (asset == null) throw new InvalidOperationException("Missing gameplay difficulty configuration.");
                settings = JsonUtility.FromJson<Settings>(asset.text);
                if (settings == null || settings.foundation == null || settings.cycle == null || settings.cycle.Length != 5)
                    throw new InvalidOperationException("Invalid five-level difficulty cycle.");
                return settings;
            }
        }

        public static Tier ForLevel(int level) => level <= Config.foundationLevels
            ? Config.foundation : Config.cycle[(level - Config.foundationLevels - 1) % Config.cycle.Length];

        public static LevelData Prepare(LevelData source, int level)
        {
            var copy = source.Clone();
            // Keep source special-mechanic eligibility and every content field unchanged.
            return copy;
        }

        public static int StepLimit(int level, int original, int minimum, int stickers, bool errorsOnly)
        {
            var tier = ForLevel(level);
            int extra = Mathf.Max(tier.minimumExtraSteps, Mathf.CeilToInt(minimum * tier.extraStepsRatio));
            int result = minimum + stickers + extra;
            if (tier.retainOriginalMinimum) result = Mathf.Max(original, result);
            return errorsOnly ? Mathf.Max(1, result - minimum) : result;
        }
    }
}
