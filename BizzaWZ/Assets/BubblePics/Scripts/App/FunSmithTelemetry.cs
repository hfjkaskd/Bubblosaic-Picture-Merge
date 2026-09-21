using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BubblePics
{
    /// <summary>
    /// Game-side custom events sent through SDKCustomEvent. Existing restored
    /// event names stay intact, while diagnostic events add the session and
    /// result fields needed to reconstruct a complete round and ad funnel.
    /// </summary>
    public static class FunSmithTelemetry
    {
        const string FirstLoginDateKey = "bp_telemetry_first_login_date";
        const string TotalPlaySecondsKey = "bp_telemetry_total_play_seconds";
        const string InterstitialSuccessKey =
            "bp_telemetry_interstitial_success_count";
        const string RewardedSuccessKey =
            "bp_telemetry_rewarded_success_count";
        const string LoginSeenKey = "bp_telemetry_login_seen";
        const string AppTouchSeenKey = "bp_telemetry_app_touch_seen";

        static string _sessionId = string.Empty;
        static bool _sdkLifecycleTracked;
        static float _roundAdStartedRealtime = -1f;
        static float _roundAdElapsedSeconds;

        public static string CurrentSessionId => _sessionId;

        public static int CurrentRoundAdSeconds
        {
            get
            {
                float active = _roundAdStartedRealtime < 0f
                    ? 0f
                    : Time.realtimeSinceStartup - _roundAdStartedRealtime;
                return Mathf.Max(
                    0,
                    Mathf.RoundToInt(_roundAdElapsedSeconds + active));
            }
        }

        public static void TrackSdkReadyLifecycle()
        {
            if (_sdkLifecycleTracked) return;
            _sdkLifecycleTracked = true;

            bool isFirstLogin = GameplayPreferences.GetInt(LoginSeenKey, 0) == 0;
            bool isFirstTouch = GameplayPreferences.GetInt(AppTouchSeenKey, 0) == 0;
            Send("login", new FirstOpenEvent { is_first = isFirstLogin });
            Send("app_touch", new FirstOpenEvent { is_first = isFirstTouch });
            GameplayPreferences.SetInt(LoginSeenKey, 1);
            GameplayPreferences.SetInt(AppTouchSeenKey, 1);
            GameplayPreferences.Save();
        }

        public static void TrackAdSessionStarted()
        {
            if (_roundAdStartedRealtime < 0f)
                _roundAdStartedRealtime = Time.realtimeSinceStartup;
        }

        public static void TrackAdSessionFinished()
        {
            if (_roundAdStartedRealtime < 0f) return;
            _roundAdElapsedSeconds += Mathf.Max(
                0f,
                Time.realtimeSinceStartup - _roundAdStartedRealtime);
            _roundAdStartedRealtime = -1f;
        }

        public static void TrackStartGameLevel(int levelNum, int enterType)
        {
            Send("start_game_level", new StartGameLevelEvent
            {
                level_num = Mathf.Max(1, levelNum),
                enter_type = Mathf.Clamp(enterType, 1, 3),
            });
        }

        public static void TrackEndGameLevel(
            int levelNum,
            int endType,
            int failCount,
            int time,
            int progress)
        {
            Send("end_game_level", new EndGameLevelEvent
            {
                level_num = Mathf.Max(1, levelNum),
                end_type = Mathf.Clamp(endType, 1, 4),
                fail_count = Mathf.Max(0, failCount),
                time = Mathf.Max(0, time),
                progress = Mathf.Max(0, progress),
            });
        }

        public static void TrackLevelNextBehavior(int endType, int nextBehavior)
        {
            Send("level_next_behavior", new LevelNextBehaviorEvent
            {
                end_type = Mathf.Clamp(endType, 1, 4),
                next_behavoir = Mathf.Clamp(nextBehavior, 1, 2),
            });
        }

        public static void TrackEndGameFail(
            int levelNum,
            int progress)
        {
            Send("end_game_fail", new EndGameFailEvent
            {
                fail_type = 1,
                level_num = Mathf.Max(1, levelNum),
                progress = Mathf.Max(0, progress),
            });
        }

        public static void TrackUserGuideStep(int stepFinish)
        {
            Send("user_guide", new UserGuideEvent
            {
                step_finish = Mathf.Max(1, stepFinish),
            });
        }

        public static void TrackPropGetStandard(
            string propName,
            int amount,
            string propSource)
        {
            Send("prop_get", new PropGetStandardEvent
            {
                prop_name = propName ?? string.Empty,
                amout = Mathf.Max(0, amount),
                prop_source = propSource ?? string.Empty,
            });
        }

        public static void TrackPropUseStandard(
            int levelNum,
            string propName,
            int propSource,
            int triggerTime)
        {
            Send("prop_use", new PropUseStandardEvent
            {
                level_num = Mathf.Max(1, levelNum),
                prop_name = propName ?? string.Empty,
                prop_source = Mathf.Max(0, propSource),
                trigger_time = Mathf.Max(0, triggerTime),
            });
        }

        public static void TrackResourceChange(
            int resourceType,
            int changeAmount,
            int balance,
            int changeReason,
            int levelNum)
        {
            Send("resource_change", new ResourceChangeEvent
            {
                resource_type = Mathf.Max(1, resourceType),
                change_amout = changeAmount,
                balance = Mathf.Max(0, balance),
                change_reason = Mathf.Max(1, changeReason),
                level_num = Mathf.Max(1, levelNum),
            });
        }

        public static void TrackGameStart(
            LevelData level,
            int globalLevel,
            string source,
            string gameplay)
        {
            _sessionId = Guid.NewGuid().ToString("N");
            _roundAdStartedRealtime = -1f;
            _roundAdElapsedSeconds = 0f;
            Send("game_start", BuildRound(
                level,
                globalLevel,
                source,
                gameplay,
                0,
                0,
                0,
                string.Empty));
        }

        public static void TrackGameOver(
            LevelData level,
            int globalLevel,
            string source,
            string gameplay,
            int costSeconds,
            int finishedPictures,
            int usedSteps,
            string reason)
        {
            Send("game_over", BuildRound(
                level,
                globalLevel,
                source,
                gameplay,
                costSeconds,
                finishedPictures,
                usedSteps,
                reason));
        }

        public static void TrackGameEnd(
            LevelData level,
            int globalLevel,
            string source,
            string gameplay,
            int costSeconds,
            int finishedPictures,
            int usedSteps)
        {
            Send("game_end", BuildRound(
                level,
                globalLevel,
                source,
                gameplay,
                costSeconds,
                finishedPictures,
                usedSteps,
                string.Empty));
        }

        public static void TrackGameExit(
            LevelData level,
            int globalLevel,
            string source,
            string gameplay,
            int costSeconds,
            int finishedPictures,
            int usedSteps,
            string reason,
            bool progressSaved)
        {
            var payload = PrepareLevel(
                new GameExitEvent(),
                level,
                globalLevel,
                source,
                gameplay);
            payload.cost_time = Mathf.Max(0, costSeconds);
            payload.finished_pics_num = Mathf.Max(0, finishedPictures);
            payload.used_step_num = Mathf.Max(0, usedSteps);
            payload.exit_reason = reason ?? string.Empty;
            payload.progress_saved = progressSaved;
            Send("game_exit", payload);
        }

        public static void TrackLevelFirstAction(
            LevelData level,
            int globalLevel,
            string source,
            string gameplay,
            int elapsedSeconds,
            int usedSteps,
            int remainingBubbles,
            int finishedPictures)
        {
            var payload = PrepareLevel(
                new FirstActionEvent(),
                level,
                globalLevel,
                source,
                gameplay);
            payload.time_to_first_action_seconds = Mathf.Max(0, elapsedSeconds);
            payload.first_action_type = "successful_merge";
            payload.move_count = 1;
            payload.used_step_num = Mathf.Max(0, usedSteps);
            payload.remaining_bubble_num = Mathf.Max(0, remainingBubbles);
            payload.finished_pics_num = Mathf.Max(0, finishedPictures);
            Send("level_first_action", payload);
        }

        public static void TrackLevelProgressSnapshot(
            LevelData level,
            int globalLevel,
            string source,
            string gameplay,
            int successfulMoves,
            int elapsedSeconds,
            int usedSteps,
            int remainingBubbles,
            int finishedPictures,
            string toolUseRecord,
            string reviveRecord)
        {
            var payload = PrepareLevel(
                new ProgressSnapshotEvent(),
                level,
                globalLevel,
                source,
                gameplay);
            payload.snapshot_type = "move";
            payload.snapshot_value = Mathf.Max(0, successfulMoves);
            payload.move_count = Mathf.Max(0, successfulMoves);
            payload.elapsed_seconds = Mathf.Max(0, elapsedSeconds);
            payload.used_step_num = Mathf.Max(0, usedSteps);
            payload.remaining_bubble_num = Mathf.Max(0, remainingBubbles);
            payload.finished_pics_num = Mathf.Max(0, finishedPictures);
            payload.item_use_record = toolUseRecord ?? string.Empty;
            payload.revive_record = reviveRecord ?? string.Empty;
            Send("level_progress_snapshot", payload);
        }

        public static void TrackReviveUse(
            int globalLevel,
            string reviveType,
            int remainingBubbles,
            int failCount,
            int elapsedSeconds,
            int usedSteps)
        {
            var payload = Prepare(new ReviveEvent
            {
                level_num = Mathf.Max(1, globalLevel),
                revive_type = reviveType ?? string.Empty,
                remaining_bubble_num = Mathf.Max(0, remainingBubbles),
                fail_count = Mathf.Max(0, failCount),
                elapsed_seconds = Mathf.Max(0, elapsedSeconds),
                used_step_num = Mathf.Max(0, usedSteps),
            });
            Send("revive_use", payload);
        }

        public static void TrackPropGet(
            int globalLevel,
            string toolId,
            string source,
            int amount,
            int left)
        {
            Send("prop_get_detail", Prepare(new PropEvent
            {
                level_num = Mathf.Max(1, globalLevel),
                tool_id = toolId ?? string.Empty,
                source = source ?? string.Empty,
                prop_num = Mathf.Max(0, amount),
                prop_left = Mathf.Max(0, left),
            }));
        }

        public static void TrackPropUse(
            int globalLevel,
            string toolId,
            string source,
            int amount,
            int left,
            int usedSteps)
        {
            var payload = Prepare(new PropEvent
            {
                level_num = Mathf.Max(1, globalLevel),
                tool_id = toolId ?? string.Empty,
                source = source ?? string.Empty,
                prop_num = Mathf.Max(0, amount),
                prop_left = Mathf.Max(0, left),
                used_step_num = Mathf.Max(0, usedSteps),
            });
            Send("prop_use_detail", payload);
        }

        public static void TrackTutorialStart(
            int globalLevel,
            int totalBubbleCount)
        {
            Send("tutorial_start", Prepare(new TutorialEvent
            {
                level_num = Mathf.Max(1, globalLevel),
                total_bubble_count = Mathf.Max(0, totalBubbleCount),
                entry_scene = "level_1",
            }));
        }

        public static void TrackTutorialStepShow(
            int globalLevel,
            string stepId,
            int stepIndex,
            string allowedAction)
        {
            Send("tutorial_step_show", Prepare(new TutorialEvent
            {
                level_num = Mathf.Max(1, globalLevel),
                step_id = stepId ?? string.Empty,
                step_index = Mathf.Max(0, stepIndex),
                allowed_action = allowedAction ?? string.Empty,
            }));
        }

        public static void TrackTutorialBlockedInteraction(
            int globalLevel,
            string stepId,
            string reason,
            string attemptAction,
            int imageId)
        {
            Send("tutorial_blocked_interaction", Prepare(new TutorialEvent
            {
                level_num = Mathf.Max(1, globalLevel),
                step_id = stepId ?? string.Empty,
                reason = reason ?? string.Empty,
                attempt_action = attemptAction ?? string.Empty,
                image_id = imageId,
            }));
        }

        public static void TrackTutorialComplete(
            int globalLevel,
            int elapsedSeconds,
            int moveCount)
        {
            Send("tutorial_complete", Prepare(new TutorialEvent
            {
                level_num = Mathf.Max(1, globalLevel),
                elapsed_seconds = Mathf.Max(0, elapsedSeconds),
                move_count = Mathf.Max(0, moveCount),
            }));
        }

        public static void TrackTutorialSkip(
            int globalLevel,
            int elapsedSeconds,
            string reason)
        {
            Send("tutorial_skip", Prepare(new TutorialEvent
            {
                level_num = Mathf.Max(1, globalLevel),
                from_state = "in_progress",
                elapsed_seconds = Mathf.Max(0, elapsedSeconds),
                reason = reason ?? string.Empty,
            }));
        }

        public static void TrackAd(
            string eventName,
            string showId,
            string placement,
            string position,
            bool? filled = null)
        {
            Send(eventName, Prepare(new AdEvent
            {
                ad_show_id = showId ?? string.Empty,
                placement = placement ?? string.Empty,
                position = position ?? string.Empty,
                is_fill = filled.HasValue
                    ? (filled.Value ? "1" : "0")
                    : string.Empty,
            }));
        }

        public static void TrackAdRequestRejected(
            string adType,
            string adPlace,
            bool isReady,
            string reason,
            string showId = "")
        {
            Send("ad_request_rejected", Prepare(new AdDiagnosticEvent
            {
                ad_show_id = showId ?? string.Empty,
                ad_type = NormalizeAdType(adType),
                ad_place = adPlace ?? string.Empty,
                level_num = Mathf.Max(1, SaveState.CurrentLevel),
                is_ready = isReady,
                reason = reason ?? string.Empty,
                scene = ActiveSceneName(),
            }));
        }

        public static void TrackAdShowResult(
            string adType,
            string adPlace,
            string showId,
            bool success,
            string errorCode = "",
            string errorMessage = "")
        {
            string normalizedType = NormalizeAdType(adType);
            if (success && normalizedType == "rewarded")
            {
                IncrementCounter(RewardedSuccessKey);
            }
            else if (success && normalizedType == "interstitial")
            {
                IncrementCounter(InterstitialSuccessKey);
            }

            Send("ad_show_result", Prepare(new AdDiagnosticEvent
            {
                ad_show_id = showId ?? string.Empty,
                ad_type = normalizedType,
                ad_place = adPlace ?? string.Empty,
                level_num = Mathf.Max(1, SaveState.CurrentLevel),
                result = success ? "success" : "fail",
                scene = ActiveSceneName(),
                error_code = errorCode ?? string.Empty,
                error_message = SafeText(errorMessage),
            }));
        }

        public static void TrackAdGateResult(
            string adPlace,
            bool allowed,
            string blockReason,
            bool isReady,
            int gateLevel,
            int unlockLevel,
            int minIntervalSeconds,
            int elapsedSinceLastInterstitial)
        {
            Send("ad_gate_result", Prepare(new AdGateEvent
            {
                ad_place = adPlace ?? string.Empty,
                allowed = allowed,
                block_reason = blockReason ?? string.Empty,
                is_ready = isReady,
                unlock_level = Mathf.Max(0, unlockLevel),
                level_num = Mathf.Max(1, gateLevel),
                min_interval_seconds = Mathf.Max(0, minIntervalSeconds),
                elapsed_since_last_interstitial =
                    elapsedSinceLastInterstitial,
            }));
        }

        public static void TrackRewardedRewardGrant(
            string adPlace,
            string rewardType,
            int amount,
            string showId)
        {
            Send("rewarded_reward_grant", Prepare(new RewardGrantEvent
            {
                ad_show_id = showId ?? string.Empty,
                ad_place = adPlace ?? string.Empty,
                level_num = Mathf.Max(1, SaveState.CurrentLevel),
                reward_type = rewardType ?? string.Empty,
                amount = Mathf.Max(0, amount),
            }));
        }

        public static void TrackPlaytimeUpdate(
            int sessionPlaySeconds,
            string reason)
        {
            int seconds = Mathf.Max(0, sessionPlaySeconds);
            int total = Mathf.Max(
                0,
                GameplayPreferences.GetInt(TotalPlaySecondsKey, 0));
            total = total > int.MaxValue - seconds
                ? int.MaxValue
                : total + seconds;
            GameplayPreferences.SetInt(TotalPlaySecondsKey, total);
            GameplayPreferences.Save();

            Send("playtime_update", Prepare(new PlaytimeEvent
            {
                session_play_seconds = seconds,
                total_play_seconds = total,
                reason = reason ?? string.Empty,
            }));
        }

        public static void TrackSettingsChange(
            string settingKey,
            bool oldValue,
            bool newValue,
            string sourceScene)
        {
            Send("settings_change", Prepare(new SettingsEvent
            {
                setting_key = settingKey ?? string.Empty,
                old_value = oldValue ? "1" : "0",
                new_value = newValue ? "1" : "0",
                source_scene = sourceScene ?? string.Empty,
            }));
        }

        public static void TrackHomePlayClick(LevelData level, int globalLevel)
        {
            var payload = Prepare(new HomePlayEvent
            {
                level = level?.level ?? globalLevel,
                level_num = Mathf.Max(1, globalLevel),
                level_type = Difficulty(level),
            }, false);
            Send("home_play_click", payload);
        }

        public static void TrackNextLevelClick(
            int completedLevel,
            int nextLevel)
        {
            Send("next_level_click", Prepare(new NextLevelEvent
            {
                completed_level = Mathf.Max(1, completedLevel),
                next_level = Mathf.Max(1, nextLevel),
            }));
        }

        public static void TrackRatePromptOpen(int globalLevel)
        {
            Send("rate_prompt_open", Prepare(new RatePromptEvent
            {
                level = Mathf.Max(1, globalLevel),
            }));
        }

        public static void TrackGameLoadMonitor(
            LevelData level,
            int globalLevel,
            string source,
            string state,
            int costTimeMs,
            string errorMessage)
        {
            Send("game_load_monitor", Prepare(new GameLoadEvent
            {
                level_num = Mathf.Max(1, globalLevel),
                level_id = level?.level_unique_id ?? string.Empty,
                level_data_source = level?.level_data_source ?? string.Empty,
                source = source ?? string.Empty,
                state = state ?? string.Empty,
                cost_time = Mathf.Max(0, costTimeMs),
                err_msg = SafeText(errorMessage),
            }, false));
        }

        static RoundEvent BuildRound(
            LevelData level,
            int globalLevel,
            string source,
            string gameplay,
            int costSeconds,
            int finishedPictures,
            int usedSteps,
            string reason)
        {
            var payload = PrepareLevel(
                new RoundEvent(),
                level,
                globalLevel,
                source,
                gameplay);
            payload.target_pics_num = LevelRepo.ImageCount(level);
            payload.limited_step_num = level?.step_limit ?? 0;
            payload.level_user_tag = level?.level_user_tags ?? string.Empty;
            payload.level_data_source = level?.level_data_source ?? string.Empty;
            payload.cost_time = Mathf.Max(0, costSeconds);
            payload.finished_pics_num = Mathf.Max(0, finishedPictures);
            payload.used_step_num = Mathf.Max(0, usedSteps);
            payload.game_over_reason = reason ?? string.Empty;
            return payload;
        }

        static T PrepareLevel<T>(
            T payload,
            LevelData level,
            int globalLevel,
            string source,
            string gameplay)
            where T : LevelEvent
        {
            Prepare(payload);
            payload.level_num = Mathf.Max(1, globalLevel);
            payload.level_type = "level";
            payload.level_id = level?.level_unique_id ?? string.Empty;
            payload.chapter_id = level?.chapter ?? 0;
            payload.level = level?.level ?? 0;
            payload.difficulty_level = Difficulty(level);
            payload.game_source = string.IsNullOrEmpty(source)
                ? "start"
                : source;
            payload.gameplay_set = gameplay ?? string.Empty;
            return payload;
        }

        static T Prepare<T>(T payload, bool includeSession = true)
            where T : CommonEvent
        {
            payload.app_version = Application.version ?? string.Empty;
            payload.platform = Application.platform.ToString();
            payload.first_login_date = FirstLoginDate();
            payload.total_play_seconds = Mathf.Max(
                0,
                GameplayPreferences.GetInt(TotalPlaySecondsKey, 0));
            payload.interstitial_success_count = Mathf.Max(
                0,
                GameplayPreferences.GetInt(InterstitialSuccessKey, 0));
            payload.rewarded_success_count = Mathf.Max(
                0,
                GameplayPreferences.GetInt(RewardedSuccessKey, 0));
            payload.session_id = includeSession ? _sessionId : string.Empty;
            return payload;
        }

        static string FirstLoginDate()
        {
            string value = GameplayPreferences.GetString(FirstLoginDateKey, string.Empty);
            if (!string.IsNullOrEmpty(value)) return value;
            value = DateTime.Now.ToString("yyyy-MM-dd");
            GameplayPreferences.SetString(FirstLoginDateKey, value);
            GameplayPreferences.Save();
            return value;
        }

        static void IncrementCounter(string key)
        {
            int value = Mathf.Max(0, GameplayPreferences.GetInt(key, 0));
            if (value < int.MaxValue) value++;
            GameplayPreferences.SetInt(key, value);
            GameplayPreferences.Save();
        }

        static string NormalizeAdType(string adType)
        {
            if (string.Equals(
                    adType,
                    "reward",
                    StringComparison.OrdinalIgnoreCase))
                return "rewarded";
            return string.IsNullOrEmpty(adType)
                ? "unknown"
                : adType.ToLowerInvariant();
        }

        static string Difficulty(LevelData level)
        {
            return string.Equals(
                level?.difficulty_type,
                "Hard",
                StringComparison.OrdinalIgnoreCase)
                ? "hard"
                : "normal";
        }

        static string ActiveSceneName()
        {
            Scene scene = SceneManager.GetActiveScene();
            return scene.IsValid() ? scene.name : string.Empty;
        }

        static string SafeText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            const int maxLength = 512;
            return value.Length <= maxLength
                ? value
                : value.Substring(0, maxLength);
        }

        static void Send(string eventName, object payload)
        {
            if (string.IsNullOrEmpty(eventName) || payload == null)
                return;
            FunSmithSdkBridge.SendCustomEvent(
                eventName,
                JsonUtility.ToJson(payload));
        }

        [Serializable]
        sealed class FirstOpenEvent
        {
            public bool is_first;
        }

        [Serializable]
        sealed class StartGameLevelEvent
        {
            public int level_num;
            public int enter_type;
        }

        [Serializable]
        sealed class EndGameLevelEvent
        {
            public int level_num;
            public int end_type;
            public int fail_count;
            public int time;
            public int progress;
        }

        [Serializable]
        sealed class LevelNextBehaviorEvent
        {
            public int end_type;
            public int next_behavoir;
        }

        [Serializable]
        sealed class EndGameFailEvent
        {
            public int fail_type;
            public int level_num;
            public int progress;
        }

        [Serializable]
        sealed class UserGuideEvent
        {
            public int step_finish;
        }

        [Serializable]
        sealed class PropGetStandardEvent
        {
            public string prop_name;
            public int amout;
            public string prop_source;
        }

        [Serializable]
        sealed class PropUseStandardEvent
        {
            public int level_num;
            public string prop_name;
            public int prop_source;
            public int trigger_time;
        }

        [Serializable]
        sealed class ResourceChangeEvent
        {
            public int resource_type;
            public int change_amout;
            public int balance;
            public int change_reason;
            public int level_num;
        }

        [Serializable]
        class CommonEvent
        {
            public string app_version;
            public string platform;
            public string first_login_date;
            public int total_play_seconds;
            public int interstitial_success_count;
            public int rewarded_success_count;
            public string session_id;
        }

        [Serializable]
        class LevelEvent : CommonEvent
        {
            public int level_num;
            public string level_type;
            public string level_id;
            public int chapter_id;
            public int level;
            public string difficulty_level;
            public string game_source;
            public string gameplay_set;
        }

        [Serializable]
        sealed class RoundEvent : LevelEvent
        {
            public int target_pics_num;
            public int limited_step_num;
            public string level_user_tag;
            public string level_data_source;
            public int cost_time;
            public int finished_pics_num;
            public int used_step_num;
            public string game_over_reason;
        }

        [Serializable]
        sealed class GameExitEvent : LevelEvent
        {
            public int cost_time;
            public int finished_pics_num;
            public int used_step_num;
            public string exit_reason;
            public bool progress_saved;
        }

        [Serializable]
        sealed class FirstActionEvent : LevelEvent
        {
            public int time_to_first_action_seconds;
            public string first_action_type;
            public int move_count;
            public int used_step_num;
            public int remaining_bubble_num;
            public int finished_pics_num;
        }

        [Serializable]
        sealed class ProgressSnapshotEvent : LevelEvent
        {
            public string snapshot_type;
            public int snapshot_value;
            public int move_count;
            public int elapsed_seconds;
            public int used_step_num;
            public int remaining_bubble_num;
            public int finished_pics_num;
            public string item_use_record;
            public string revive_record;
        }

        [Serializable]
        sealed class ReviveEvent : CommonEvent
        {
            public int level_num;
            public string revive_type;
            public int remaining_bubble_num;
            public int fail_count;
            public int elapsed_seconds;
            public int used_step_num;
        }

        [Serializable]
        sealed class PropEvent : CommonEvent
        {
            public int level_num;
            public string tool_id;
            public string source;
            public int prop_num;
            public int prop_left;
            public int used_step_num;
        }

        [Serializable]
        sealed class TutorialEvent : CommonEvent
        {
            public int level_num;
            public int total_bubble_count;
            public string entry_scene;
            public string step_id;
            public int step_index;
            public string allowed_action;
            public string reason;
            public string attempt_action;
            public int image_id;
            public int elapsed_seconds;
            public int move_count;
            public string from_state;
        }

        [Serializable]
        sealed class AdEvent : CommonEvent
        {
            public string ad_show_id;
            public string placement;
            public string position;
            public string is_fill;
        }

        [Serializable]
        sealed class AdDiagnosticEvent : CommonEvent
        {
            public string ad_show_id;
            public string ad_type;
            public string ad_place;
            public int level_num;
            public bool is_ready;
            public string result;
            public string reason;
            public string scene;
            public string error_code;
            public string error_message;
        }

        [Serializable]
        sealed class AdGateEvent : CommonEvent
        {
            public string ad_place;
            public bool allowed;
            public string block_reason;
            public bool is_ready;
            public int unlock_level;
            public int level_num;
            public int min_interval_seconds;
            public int elapsed_since_last_interstitial;
        }

        [Serializable]
        sealed class RewardGrantEvent : CommonEvent
        {
            public string ad_show_id;
            public string ad_place;
            public int level_num;
            public string reward_type;
            public int amount;
        }

        [Serializable]
        sealed class PlaytimeEvent : CommonEvent
        {
            public int session_play_seconds;
            public int total_play_seconds;
            public string reason;
        }

        [Serializable]
        sealed class SettingsEvent : CommonEvent
        {
            public string setting_key;
            public string old_value;
            public string new_value;
            public string source_scene;
        }

        [Serializable]
        sealed class HomePlayEvent : CommonEvent
        {
            public int level;
            public int level_num;
            public string level_type;
        }

        [Serializable]
        sealed class NextLevelEvent : CommonEvent
        {
            public int completed_level;
            public int next_level;
        }

        [Serializable]
        sealed class RatePromptEvent : CommonEvent
        {
            public int level;
        }

        [Serializable]
        sealed class GameLoadEvent : CommonEvent
        {
            public int level_num;
            public string level_id;
            public string level_data_source;
            public string source;
            public string state;
            public int cost_time;
            public string err_msg;
        }
    }
}
