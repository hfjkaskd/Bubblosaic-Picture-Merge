using System.Collections.Generic;
using UnityEngine;

namespace BubblePics.GameModes
{
    /// <summary>
    /// Persistent 1.0.9 alternate-mode state. Kept outside SaveState so the
    /// base profile remains backwards-compatible with existing installs.
    /// </summary>
    public static class ModeProgress
    {
        const string Prefix = "bp_mode_109_";
        const int EliteTarget = 5;
        const float EliteCoefficientMax = 1.5f;
        const string BonusRewardToolKey = Prefix + "bonus_reward_tool";
        const string BonusRewardCountKey = Prefix + "bonus_reward_count";

        public static int EliteStreak
        {
            get => GameplayPreferences.GetInt(Prefix + "elite_streak", 0);
            private set => SetInt("elite_streak", Mathf.Max(0, value));
        }

        public static bool BonusPending
        {
            get => GameplayPreferences.GetInt(Prefix + "bonus_pending", 0) == 1;
            set => SetInt("bonus_pending", value ? 1 : 0);
        }

        public static int BonusCursor
        {
            get => GameplayPreferences.GetInt(Prefix + "bonus_cursor", 0);
            private set => SetInt("bonus_cursor", Mathf.Max(0, value));
        }

        public static bool ShapeTutorialDone
        {
            get => GameplayPreferences.GetInt(Prefix + "shape_tutorial", 0) == 1;
            set => SetInt("shape_tutorial", value ? 1 : 0);
        }

        public static void RecordRoundWon(
            GameplayKind kind,
            int globalLevel,
            int usedSteps,
            int minimumMergeSteps)
        {
            if (!AppConfig.BonusLevel)
            {
                if (EliteStreak != 0) EliteStreak = 0;
                if (BonusPending) BonusPending = false;
                return;
            }

            if (kind == GameplayKind.Tangram)
                ShapeTutorialDone = true;
            if (kind == GameplayKind.Bonus)
            {
                BonusCursor = Mathf.Min(
                    BonusCursor + 1,
                    ModeCatalogRepository.BonusLevels.Count);
                BonusPending = false;
                GrantBonusTool(globalLevel);
                return;
            }

            if (globalLevel <= 1) return;
            float coefficient =
                usedSteps / (float)Mathf.Max(1, minimumMergeSteps) + 0.1f;
            bool elite = coefficient <= EliteCoefficientMax;
            EliteStreak = elite ? EliteStreak + 1 : 0;
            if (EliteStreak < EliteTarget) return;

            EliteStreak = 0;
            if (BonusCursor < ModeCatalogRepository.BonusLevels.Count)
                BonusPending = true;
        }

        public static void RecordDeath(
            GameplayKind kind,
            int globalLevel)
        {
            if (kind != GameplayKind.Bonus && globalLevel > 1)
                EliteStreak = 0;
        }

        public static void Reset()
        {
            GameplayPreferences.DeleteKey(Prefix + "elite_streak");
            GameplayPreferences.DeleteKey(Prefix + "bonus_pending");
            GameplayPreferences.DeleteKey(Prefix + "bonus_cursor");
            GameplayPreferences.DeleteKey(Prefix + "shape_tutorial");
            GameplayPreferences.DeleteKey(BonusRewardToolKey);
            GameplayPreferences.DeleteKey(BonusRewardCountKey);
            GameplayPreferences.Save();
        }

        public static bool TryConsumeBonusReward(out string tool, out int count)
        {
            tool = GameplayPreferences.GetString(BonusRewardToolKey, "");
            count = Mathf.Max(0, GameplayPreferences.GetInt(BonusRewardCountKey, 0));
            if (string.IsNullOrEmpty(tool) || count <= 0)
            {
                tool = string.Empty;
                count = 0;
                return false;
            }
            GameplayPreferences.DeleteKey(BonusRewardToolKey);
            GameplayPreferences.DeleteKey(BonusRewardCountKey);
            GameplayPreferences.Save();
            return true;
        }

        static void GrantBonusTool(int globalLevel)
        {
            string[] unlocked = globalLevel >= 10
                ? new[] { "hint", "drop", "magnet" }
                : globalLevel >= 5
                    ? new[] { "hint", "drop" }
                    : new[] { "hint" };
            string tool = unlocked[Random.Range(0, unlocked.Length)];
            int count = tool == "hint" ? 3 : tool == "drop" ? 2 : 1;
            SaveState.SetToolCount(tool, SaveState.GetToolCount(tool) + count);
            GameplayPreferences.SetString(BonusRewardToolKey, tool);
            GameplayPreferences.SetInt(BonusRewardCountKey, count);
            GameplayPreferences.Save();
        }

        static void SetInt(string key, int value)
        {
            GameplayPreferences.SetInt(Prefix + key, value);
            GameplayPreferences.Save();
        }
    }

    public static class ModeSession
    {
        public static LevelModeSelection ActiveSelection { get; private set; }
        public static CompiledModeLevel ActiveCompiledLevel { get; private set; }
        public static IReadOnlyDictionary<string, Texture2D> ActiveCategoryIcons
        {
            get;
            private set;
        }

        public static GameplayKind ActiveKind =>
            ActiveSelection?.Kind ?? GameplayKind.Main;

        // Compatibility alias for mode implementations that only need the active kind.
        public static GameplayKind Kind => ActiveKind;

        public static void Activate(
            LevelModeSelection selection,
            CompiledModeLevel compiled,
            IReadOnlyDictionary<string, Texture2D> categoryIcons = null)
        {
            ActiveSelection = selection?.Clone();
            ActiveCompiledLevel = compiled;
            ActiveCategoryIcons = categoryIcons;
        }

        public static void Clear()
        {
            ActiveSelection = null;
            ActiveCompiledLevel = null;
            ActiveCategoryIcons = null;
        }
    }
}
