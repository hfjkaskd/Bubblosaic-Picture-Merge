using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace BubblePics
{
    /// <summary>Persistent player profile, mirroring Godot user://save.cfg defaults.</summary>
    public static class SaveState
    {
        const string P = "bp_";
        const int RecentRoundCoefficientLimit = 2;

        internal static void EnsureLifecycleState()
        {
            EnsureFirstInstallDate();
            EnsureGameDataToday();
        }

        public static int InstallDays
        {
            get
            {
                EnsureFirstInstallDate();
                string raw = GameplayPreferences.GetString(
                    P + "first_install_date",
                    TodayKey);
                if (!DateTime.TryParseExact(
                        raw,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime installed))
                {
                    return 0;
                }
                return Math.Max(0, (DateTime.Today - installed.Date).Days);
            }
        }

        public static int DayLevelCount
        {
            get
            {
                EnsureGameDataToday();
                return Mathf.Max(
                    0,
                    GameplayPreferences.GetInt(P + "day_level_num", 0));
            }
        }

        public static int CurrentLevel
        {
            get => SaveDataUtils.GameData.playerSelectedLv;
            set { SaveDataUtils.GameData.playerSelectedLv = value; SaveDataUtils.Save(); }
        }

        /// <summary>
        /// Write-ahead journal for level completion. A non-zero value means
        /// the durable completed-stage store still needs an idempotent commit
        /// (or that commit succeeded but progress finalization was interrupted).
        /// </summary>
        public static int PendingLevelCompletion =>
            Mathf.Max(0, GameplayPreferences.GetInt(P + "pending_level_completion", 0));

        internal static bool BeginLevelCompletion(int level)
        {
            if (level <= 0) return false;
            GameplayPreferences.SetInt(P + "pending_level_completion", level);
            GameplayPreferences.Save();
            return true;
        }

        internal static bool FinalizeLevelCompletion(int level)
        {
            if (level <= 0 || PendingLevelCompletion != level) return false;
            EnsureGameDataToday();
            int currentLevel = CurrentLevel;
            if (level >= currentLevel)
            {
                GameplayPreferences.SetInt(
                    P + "day_level_num",
                    DayLevelCount + 1);
            }
            GameplayPreferences.SetInt(
                P + "current_level",
                Mathf.Max(currentLevel, level + 1));
            GameplayPreferences.SetInt(P + "pending_level_completion", 0);
            GameplayPreferences.Save();
            return true;
        }

        public static int Coins
        {
            get => GameplayPreferences.GetInt(P + "coins", 1000);
            set { GameplayPreferences.SetInt(P + "coins", value); GameplayPreferences.Save(); }
        }

        /// <summary>
        /// Persistent no-ads entitlement used by the original GM command and
        /// by a future production purchase integration. Rewarded placements
        /// remain available unless the session-only VIP GM mode is enabled.
        /// </summary>
        public static bool AdsDisabled
        {
            get => GameplayPreferences.GetInt(P + "ads_disabled", 0) == 1;
            set
            {
                GameplayPreferences.SetInt(P + "ads_disabled", value ? 1 : 0);
                GameplayPreferences.Save();
            }
        }

        public static int GetToolCount(string tool)
        {
            return Mathf.RoundToInt(ItemUtils.GetItemCount(BizzaGameplayBridge.ToolType(tool)));
        }

        public static void SetToolCount(string tool, int v)
        {
            var type = BizzaGameplayBridge.ToolType(tool);
            int delta = v - Mathf.RoundToInt(ItemUtils.GetItemCount(type));
            if (delta > 0) ItemUtils.AddItem(type, delta);
            else if (delta < 0) throw new System.InvalidOperationException("Only framework UI may consume gameplay props.");
        }

        public static bool TutorialDone
        {
            get => GameplayPreferences.GetInt(P + "tutorial_done", 0) == 1;
            set { GameplayPreferences.SetInt(P + "tutorial_done", value ? 1 : 0); GameplayPreferences.Save(); }
        }

        public static bool SoundOn
        {
            get => SaveDataUtils.SettingData.enableSound;
            set { SaveDataUtils.SettingData.enableSound = value; SaveDataUtils.Save(); }
        }

        public static bool VibrateOn
        {
            get => SaveDataUtils.SettingData.enableVibrate;
            set { SaveDataUtils.SettingData.enableVibrate = value; SaveDataUtils.Save(); }
        }

        public static bool PrivacyAccepted
        {
            get => GameplayPreferences.GetInt(P + "privacy_accepted", 0) == 1;
            set { GameplayPreferences.SetInt(P + "privacy_accepted", value ? 1 : 0); GameplayPreferences.Save(); }
        }

        public static bool NotificationAsked
        {
            get => GameplayPreferences.GetInt(P + "notify_asked", 0) == 1;
            set { GameplayPreferences.SetInt(P + "notify_asked", value ? 1 : 0); GameplayPreferences.Save(); }
        }

        public static string SplashQuoteDate =>
            GameplayPreferences.GetString(P + "splash_quote_date", "");

        /// <summary>
        /// Legacy field kept so existing PlayerPrefs remain readable. The
        /// original splash flow does not use or update the slogan index.
        /// </summary>
        public static int SplashQuoteIndex =>
            GameplayPreferences.GetInt(P + "splash_quote_index", 0);

        public static void SetSplashQuoteDate(string date)
        {
            GameplayPreferences.SetString(P + "splash_quote_date", date ?? "");
            GameplayPreferences.Save();
        }

        public static bool GetFlag(string key)
        {
            return GameplayPreferences.GetInt(P + "flag_" + key, 0) == 1;
        }

        public static void SetFlag(string key, bool v)
        {
            GameplayPreferences.SetInt(P + "flag_" + key, v ? 1 : 0);
            GameplayPreferences.Save();
        }

        public static bool FreeContinueUsed
        {
            get => GetFlag("free_continue_used");
            set => SetFlag("free_continue_used", value);
        }

        public static string RoundSnapshot
        {
            get => GameplayPreferences.GetString(P + "round_snapshot", "");
            set { GameplayPreferences.SetString(P + "round_snapshot", value); GameplayPreferences.Save(); }
        }

        public static float[] RecentRoundCoefficients =>
            GameplayPreferences.GetString(P + "recent_round_coefficients", "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsed) ? (float?)parsed : null)
                .Where(value => value.HasValue)
                .Select(value => value.Value)
                .ToArray();

        public static bool TryGetLastRoundCoefficient(out float coefficient)
        {
            float[] values = RecentRoundCoefficients;
            coefficient = values.Length > 0 ? values[values.Length - 1] : 0f;
            return values.Length > 0;
        }

        public static void RecordRoundCoefficient(float coefficient)
        {
            float[] appended = RecentRoundCoefficients
                .Concat(new[] { coefficient })
                .ToArray();
            float[] values = appended
                .Skip(Mathf.Max(0, appended.Length - RecentRoundCoefficientLimit))
                .ToArray();
            GameplayPreferences.SetString(
                P + "recent_round_coefficients",
                string.Join(",", values.Select(value =>
                    value.ToString("R", CultureInfo.InvariantCulture))));
            GameplayPreferences.Save();
        }

        public static int GetNumberPuzzleCursor(string tier)
        {
            string key = string.Equals(
                tier,
                NumberPuzzle.HardTier,
                System.StringComparison.Ordinal)
                ? NumberPuzzle.HardTier
                : NumberPuzzle.SimpleTier;
            return Mathf.Max(0, GameplayPreferences.GetInt(P + "number_puzzle_" + key, 0));
        }

        public static void AdvanceNumberPuzzleCursor(string tier)
        {
            string key = string.Equals(
                tier,
                NumberPuzzle.HardTier,
                System.StringComparison.Ordinal)
                ? NumberPuzzle.HardTier
                : NumberPuzzle.SimpleTier;
            GameplayPreferences.SetInt(
                P + "number_puzzle_" + key,
                GetNumberPuzzleCursor(key) + 1);
            GameplayPreferences.Save();
        }

        static string TodayKey => DateTime.Now.ToString(
            "yyyy-MM-dd", CultureInfo.InvariantCulture);

        static void EnsureFirstInstallDate()
        {
            string key = P + "first_install_date";
            if (!string.IsNullOrEmpty(GameplayPreferences.GetString(key, ""))) return;
            GameplayPreferences.SetString(key, TodayKey);
            GameplayPreferences.Save();
        }

        static void EnsureGameDataToday()
        {
            string key = P + "game_data_date";
            string today = TodayKey;
            if (GameplayPreferences.GetString(key, "") == today) return;
            GameplayPreferences.SetString(key, today);
            GameplayPreferences.SetInt(P + "day_level_num", 0);
            GameplayPreferences.Save();
        }

        public static bool TryConsumeDailyFirstStepBonus()
        {
            string key = P + "daily_first_step_bonus_date";
            if (GameplayPreferences.GetString(key, "") == TodayKey) return false;
            // The recovered control group also consumes today's eligibility.
            GameplayPreferences.SetString(key, TodayKey);
            GameplayPreferences.Save();
            return true;
        }

        public static bool TryConsumeDailyFirstWin()
        {
            string key = P + "daily_first_win_date";
            if (GameplayPreferences.GetString(key, "") == TodayKey) return false;
            GameplayPreferences.SetString(key, TodayKey);
            GameplayPreferences.Save();
            return true;
        }

        public static bool TryConsumeFeatureTutorial(string feature)
        {
            if (string.IsNullOrWhiteSpace(feature)) return false;
            string key = P + "tutorial_" + feature.Trim().ToLowerInvariant();
            if (GameplayPreferences.GetInt(key, 0) == 1) return false;
            GameplayPreferences.SetInt(key, 1);
            GameplayPreferences.Save();
            return true;
        }

        public static bool IsLuckyBreakLevel(int level)
        {
            int from = GameplayPreferences.GetInt(P + "lucky_break_from", 0);
            int until = GameplayPreferences.GetInt(P + "lucky_break_until", 0);
            return from > 0 && level >= from && level <= until;
        }

        public static void CommitLuckyBreakWindow(int completedLevel)
        {
            GameplayPreferences.SetInt(P + "lucky_break_from", completedLevel + 1);
            GameplayPreferences.SetInt(P + "lucky_break_until", completedLevel + 3);
            GameplayPreferences.SetInt(P + "lucky_break_pending", 1);
            GameplayPreferences.Save();
        }

        public static bool PopLuckyBreakPending()
        {
            string key = P + "lucky_break_pending";
            bool pending = GameplayPreferences.GetInt(key, 0) == 1;
            if (pending)
            {
                GameplayPreferences.SetInt(key, 0);
                GameplayPreferences.Save();
            }
            return pending;
        }

        public static int RecordLuckyBreakWin(float coefficient)
        {
            EnsureLuckyDay();
            int count = GameplayPreferences.GetInt(P + "lucky_day_level_count", 0) + 1;
            GameplayPreferences.SetInt(P + "lucky_day_level_count", count);
            string key = P + "lucky_day_coefficients";
            string value = GameplayPreferences.GetString(key, "");
            string encoded = coefficient.ToString("R", CultureInfo.InvariantCulture);
            GameplayPreferences.SetString(key, string.IsNullOrEmpty(value)
                ? encoded : value + "," + encoded);
            GameplayPreferences.Save();
            return count;
        }

        public static int LuckyBreakDayTarget
        {
            get
            {
                EnsureLuckyDay();
                return GameplayPreferences.GetInt(P + "lucky_day_target", 3);
            }
        }

        public static float[] LuckyBreakDayCoefficients
        {
            get
            {
                EnsureLuckyDay();
                return GameplayPreferences.GetString(P + "lucky_day_coefficients", "")
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => float.TryParse(
                        value, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out float parsed) ? parsed : 0f)
                    .ToArray();
            }
        }

        public static bool LuckyBreakDayChecked
        {
            get
            {
                EnsureLuckyDay();
                return GameplayPreferences.GetInt(P + "lucky_day_checked", 0) == 1;
            }
            set
            {
                EnsureLuckyDay();
                GameplayPreferences.SetInt(P + "lucky_day_checked", value ? 1 : 0);
                GameplayPreferences.Save();
            }
        }

        static void EnsureLuckyDay()
        {
            string key = P + "lucky_day_key";
            string today = TodayKey;
            if (GameplayPreferences.GetString(key, "") == today) return;
            int hash = 17;
            foreach (char character in today)
                hash = unchecked(hash * 31 + character);
            GameplayPreferences.SetString(key, today);
            GameplayPreferences.SetInt(P + "lucky_day_target", 3 + Mathf.Abs(hash % 4));
            GameplayPreferences.SetInt(P + "lucky_day_level_count", 0);
            GameplayPreferences.SetString(P + "lucky_day_coefficients", "");
            GameplayPreferences.SetInt(P + "lucky_day_checked", 0);
            GameplayPreferences.Save();
        }

        public static void ResetAll()
        {
            GameplayPreferences.DeleteAll();
            GameplayPreferences.Save();
        }
    }
}
