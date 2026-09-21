using System;
using System.Globalization;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Data-backed release switches for the restored 1.0.9 mechanics. Values
    /// live in Resources/Config/app_config.json and do not depend on an AB SDK
    /// assignment or a remote experiment response.
    /// </summary>
    public static class AppConfig
    {
        const string ResourcePath = "Config/app_config";
        const string DebugOverridePrefix = "bp_gm_app_config_";
        const string DebugOverrideProfileKey =
            DebugOverridePrefix + "active_profile";

        [Serializable]
        sealed class InitialStepRule
        {
            public int startLevel = 2;
            public int endLevel = 6;
            public float multiplier = 0.85f;
        }

        [Serializable]
        sealed class PeriodicRule
        {
            public int startLevel = 25;
            public int interval = 15;
            public string mode = "exp_25_15";
        }

        [Serializable]
        sealed class RuntimeValues
        {
            public string profileId =
                "release_1.0.9_original_main_category_number_locked_zh_CN";

            public bool mergeAnimationGroup1 = true;
            public bool completeAnimationNew = true;
            public bool lastLinkEnhance;
            public bool gameUi2;
            public bool chipShape;
            public bool mergeDistanceNew = true;
            public bool relaxStepDeduction = true;
            public float pickupEnlargePeak = 1.15f;
            public bool mainSoundNew = true;
            public bool touchSoundOn = true;
            public bool autoLinkAfterMove;
            public bool comboStepWonderful;
            public bool moveVibrationOn = true;

            // The player release intentionally exposes only Main, Category
            // Match, and Number Match. Other restored branches remain in the
            // project for explicit editor/GM parity previews.
            public bool bombMode;
            public bool lockedMode = true;
            public bool numberMatchMode = true;
            public bool singlePictureMode;
            public bool pointsMode;
            public bool operatedMarkMode;
            public bool restartHighlight;
            public bool adaptiveLayout;
            public bool bubbleLineWeaken;
            public bool bubblePic2New;
            public bool fpsIdle;
            public bool newerGuideV2;
            public bool picNumberCombine;
            public bool rightTogether;
            public bool threeDie;

            public PeriodicRule numberMatchSchedule = new PeriodicRule();

            // Optional 1.0.8/1.0.9 meta-flow and alternate-level features.
            public bool dailyFirstStepBonus;
            public bool dailyIncentiveWin;
            public bool completePageEncourageText;
            public bool luckyBreak;
            public bool homeCycle;
            public bool categoryLevel = true;
            public bool wordLevel;
            public bool shapeLevel;
            public bool bonusLevel;
            public bool starfishBubble;

            // Optional 1.0.8/1.0.9 bubble-plugin branches.
            public string bombModeType = "step";
            public int bombStartLevel = 5;
            public int bombInterval = 3;
            public bool rainbowBubble;
            public int magnetBubbleGroup;
            public bool saveBubble;
            public int saveBubbleStartLevel = 9;
            public int saveBubbleInterval = 4;
            public bool stickerPuzzle;
            public int stickerStartLevel = 2;
            public int stickerInterval;
            public bool diagonalPerfectFx;
            public bool comboMergeAnimation;
            public bool stepIncrease;
            public bool stepUseErrorOnly;
            public bool hardLevelDiff;

            public int movesUnlockLevel = 2;
            public int interstitialUnlockLevel = 10;
            public int interstitialCooldownSeconds = 60;
            public string interstitialCooldownByInstallDay = "[0_inf:60]";
            public string interstitialProtectionByInstallDay = "[1_inf:0]";
            public InitialStepRule initialStep = new InitialStepRule();
        }

        static RuntimeValues _values;

        static RuntimeValues Values
        {
            get
            {
                if (_values == null)
                    _values = Load();
                return _values;
            }
        }

        public static string ProfileId => Values.profileId;
        public static bool MergeAnimationGroup1 => Values.mergeAnimationGroup1;
        public static bool CompleteAnimationNew => Values.completeAnimationNew;
        public static bool LastLinkEnhance => Values.lastLinkEnhance;
        public static bool GameUi2 => Values.gameUi2;
        public static bool ChipShape => Values.chipShape;
        public static bool MergeDistanceNew => Values.mergeDistanceNew;
        public static bool RelaxStepDeduction => Values.relaxStepDeduction;
        public static float PickupEnlargePeak => Values.pickupEnlargePeak;
        public static bool MainSoundNew => Values.mainSoundNew;
        public static bool TouchSoundOn => Values.touchSoundOn;
        public static bool AutoLinkAfterMove => Values.autoLinkAfterMove;
        public static bool ComboStepWonderful => Values.comboStepWonderful;
        public static bool MoveVibrationOn => Values.moveVibrationOn;
        public static bool BombMode => Values.bombMode;
        public static bool LockedMode => Values.lockedMode;
        public static bool NumberMatchMode => Values.numberMatchMode;
        public static bool SinglePictureMode => Values.singlePictureMode;
        public static bool PointsMode => Values.pointsMode;
        public static bool OperatedMarkMode => Values.operatedMarkMode;
        public static bool RestartHighlight => Values.restartHighlight;
        public static bool AdaptiveLayout => Values.adaptiveLayout;
        public static bool BubbleLineWeaken => Values.bubbleLineWeaken;
        public static bool BubblePic2New => Values.bubblePic2New;
        public static bool FpsIdle => Values.fpsIdle;
        public static bool NewerGuideV2 => Values.newerGuideV2;
        public static bool PicNumberCombine => Values.picNumberCombine;
        public static bool RightTogether => Values.rightTogether;
        public static bool ThreeDie => Values.threeDie;
        public static int NumberMatchStartLevel =>
            Mathf.Max(1, Values.numberMatchSchedule?.startLevel ?? 25);
        public static int NumberMatchInterval =>
            Mathf.Max(1, Values.numberMatchSchedule?.interval ?? 15);
        public static bool DailyFirstStepBonus => Values.dailyFirstStepBonus;
        public static bool DailyIncentiveWin => Values.dailyIncentiveWin;
        public static bool CompletePageEncourageText => Values.completePageEncourageText;
        public static bool LuckyBreak => Values.luckyBreak;
        public static bool HomeCycle => Values.homeCycle;
        public static bool CategoryLevel => Values.categoryLevel;
        public static bool WordLevel => Values.wordLevel;
        public static bool ShapeLevel => Values.shapeLevel;
        public static bool BonusLevel => Values.bonusLevel;
        public static bool StarfishBubble => Values.starfishBubble;
        public static string BombModeType =>
            Values.bombModeType == "time" ? "time" : "step";
        public static int BombStartLevel => Mathf.Max(1, Values.bombStartLevel);
        public static int BombInterval => Mathf.Max(1, Values.bombInterval);
        public static bool RainbowBubble => Values.rainbowBubble;
        public static int MagnetBubbleGroup =>
            Mathf.Clamp(Values.magnetBubbleGroup, 0, 2);
        public static bool SaveBubble => Values.saveBubble;
        public static int SaveBubbleStartLevel =>
            Mathf.Max(1, Values.saveBubbleStartLevel);
        public static int SaveBubbleInterval =>
            Mathf.Max(1, Values.saveBubbleInterval);
        public static bool StickerPuzzle => Values.stickerPuzzle;
        public static int StickerStartLevel =>
            Mathf.Max(1, Values.stickerStartLevel);
        public static int StickerInterval => Mathf.Max(0, Values.stickerInterval);
        public static bool DiagonalPerfectFx => Values.diagonalPerfectFx;
        public static bool ComboMergeAnimation => Values.comboMergeAnimation;
        public static bool StepIncrease => Values.stepIncrease;
        public static bool StepUseErrorOnly => Values.stepUseErrorOnly;
        public static bool HardLevelDiff => Values.hardLevelDiff;
        public static int MovesUnlockLevel => Mathf.Max(1, Values.movesUnlockLevel);
        public static int InterstitialUnlockLevel =>
            Mathf.Max(1, Values.interstitialUnlockLevel);
        public static int InterstitialCooldownSeconds =>
            Mathf.Max(0, Values.interstitialCooldownSeconds);

        public static int GetInterstitialCooldownSeconds(int installDay)
        {
            return Mathf.Max(
                0,
                LookupDaySegment(
                    Values.interstitialCooldownByInstallDay,
                    installDay,
                    InterstitialCooldownSeconds));
        }

        public static int GetInterstitialProtectionCount(int installDay)
        {
            return Mathf.Max(
                0,
                LookupDaySegment(
                    Values.interstitialProtectionByInstallDay,
                    installDay,
                    0));
        }

        static int LookupDaySegment(string raw, int day, int fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            string[] segments = raw.Trim().Split(
                new[] { ',' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string segment in segments)
            {
                string body = segment.Trim().Trim('[', ']');
                string[] valueParts = body.Split(':');
                if (valueParts.Length != 2 ||
                    !int.TryParse(
                        valueParts[1].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int value))
                {
                    continue;
                }

                string[] range = valueParts[0].Trim().Split('_');
                if (range.Length != 2 ||
                    !int.TryParse(
                        range[0].Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int lower))
                {
                    continue;
                }

                int upper;
                if (string.Equals(
                        range[1].Trim(),
                        "inf",
                        StringComparison.Ordinal))
                {
                    upper = 1 << 30;
                }
                else if (!int.TryParse(
                             range[1].Trim(),
                             NumberStyles.Integer,
                             CultureInfo.InvariantCulture,
                             out upper))
                {
                    continue;
                }

                if (day >= lower && day <= upper) return value;
            }
            return fallback;
        }

        /// <summary>Original production Number Match experiment: 25+15n.</summary>
        public static bool IsNumberMatchLevel(int level)
        {
            int start = NumberMatchStartLevel;
            int interval = NumberMatchInterval;
            return level >= start && (level - start) % interval == 0;
        }

        /// <summary>
        /// Applies the recorded online initial-step rule. A disabled/invalid
        /// range or non-positive multiplier safely falls back to source data.
        /// </summary>
        public static int ApplyInitialStep(int original, int level)
        {
            InitialStepRule rule = Values.initialStep;
            if (rule == null || rule.multiplier <= 0f ||
                rule.endLevel < rule.startLevel ||
                level < rule.startLevel || level > rule.endLevel)
            {
                return original;
            }

            int adjusted = Mathf.Max(
                0, Mathf.RoundToInt(original * rule.multiplier));
            return adjusted;
        }

        static RuntimeValues Load()
        {
            RuntimeValues defaults = new RuntimeValues();
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                Debug.LogWarning(
                    $"Runtime config Resources/{ResourcePath}.json is missing; " +
                    "using the recorded profile defaults.");
                return defaults;
            }

            try
            {
                RuntimeValues loaded = JsonUtility.FromJson<RuntimeValues>(asset.text);
                if (loaded == null)
                    loaded = defaults;
                if (loaded.initialStep == null)
                    loaded.initialStep = defaults.initialStep;
                if (loaded.numberMatchSchedule == null)
                    loaded.numberMatchSchedule = defaults.numberMatchSchedule;
                if (string.IsNullOrWhiteSpace(loaded.profileId))
                    loaded.profileId = defaults.profileId;
                return loaded;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Invalid runtime config '{asset.name}': {ex.Message}");
                return defaults;
            }
        }

#if UNITY_EDITOR
        /// <summary>Allows edit-mode validation to reload a changed JSON asset.</summary>
        internal static void ReloadForEditorValidation()
        {
            _values = null;
        }
#endif
    }
}
