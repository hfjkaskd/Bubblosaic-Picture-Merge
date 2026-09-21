using System;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// SDK-neutral interstitial boundary. The original gates next-level and
    /// restart actions, then continues only after the ad closes. With no native
    /// provider installed the action continues immediately, preserving offline
    /// play instead of blocking progression.
    /// </summary>
    public interface IInterstitialAdProvider
    {
        bool IsReady { get; }
        void Show(int levelNum, string placement, Action<bool> completed);
    }

    public static class InterstitialAds
    {
        static long _lastCloseUnix;

        public static IInterstitialAdProvider Provider { get; private set; }
        public static bool IsReady =>
            true &&
            Provider != null && Provider.IsReady;

        /// <summary>
        /// Session-only GM override. Persistent no-ads state is stored in
        /// SaveState so normal builds can later share it with an IAP provider.
        /// </summary>
        public static bool DebugSkip { get; set; }

        /// <summary>
        /// The recovered Bubble flow keeps interstitial cooldown in memory and
        /// deliberately does not apply first-session suppression.
        /// </summary>
        public static void BeginSession()
        {
            _lastCloseUnix = 0;
            SaveState.EnsureLifecycleState();
        }

        public static void Register(IInterstitialAdProvider provider)
        {
            if (!true)
                return;
            Provider = provider;
        }

        public static void Gate(int gateLevel, string placement, Action continuation)
        {
            if (!true)
            {
                TrackGate(
                    gateLevel,
                    placement,
                    false,
                    false
                        ? "white_package"
                        : "ads_disabled",
                    false,
                    -1);
                continuation?.Invoke();
                return;
            }
            if (DebugSkip || SaveState.AdsDisabled)
            {
                TrackGate(
                    gateLevel,
                    placement,
                    false,
                    DebugSkip ? "debug_skip" : "ads_disabled",
                    IsReady,
                    -1);
                continuation?.Invoke();
                return;
            }

            bool unlocked = SaveState.GetFlag("interstitial_unlocked");
            if (!unlocked && gateLevel >= AppConfig.InterstitialUnlockLevel)
            {
                unlocked = true;
                SaveState.SetFlag("interstitial_unlocked", true);
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int installDay = SaveState.InstallDays;
            int protectionCount =
                AppConfig.GetInterstitialProtectionCount(installDay);
            int cooldownSeconds =
                AppConfig.GetInterstitialCooldownSeconds(installDay);
            int elapsedSinceLast = _lastCloseUnix <= 0
                ? -1
                : (int)Math.Min(
                    int.MaxValue,
                    Math.Max(0, now - _lastCloseUnix));
            bool coolingDown =
                now - _lastCloseUnix < cooldownSeconds;
            bool ready = IsReady;

            string blockReason = !unlocked
                ? "not_unlocked"
                : protectionCount > 0 &&
                  SaveState.DayLevelCount <= protectionCount
                    ? "daily_level_protection"
                    : coolingDown
                        ? "min_interval"
                        : Provider == null
                            ? "unsupported"
                            : string.Empty;
            bool allowed = string.IsNullOrEmpty(blockReason);
            TrackGate(
                gateLevel,
                placement,
                allowed,
                blockReason,
                ready,
                elapsedSinceLast,
                cooldownSeconds);

            if (!allowed)
            {
                continuation?.Invoke();
                return;
            }

            bool continued = false;
            Provider.Show(gateLevel, placement, succeeded =>
            {
                if (continued) return;
                continued = true;
                if (succeeded)
                {
                    _lastCloseUnix =
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
                continuation?.Invoke();
            });
        }

        static void TrackGate(
            int gateLevel,
            string placement,
            bool allowed,
            string blockReason,
            bool ready,
            int elapsedSinceLast,
            int cooldownSeconds = -1)
        {
            FunSmithTelemetry.TrackAdGateResult(
                placement,
                allowed,
                blockReason,
                ready,
                gateLevel,
                AppConfig.InterstitialUnlockLevel,
                cooldownSeconds >= 0
                    ? cooldownSeconds
                    : AppConfig.GetInterstitialCooldownSeconds(
                        SaveState.InstallDays),
                elapsedSinceLast);
        }

        /// <summary>Compatibility overload for callers without a level gate.</summary>
        public static void Gate(string placement, Action continuation)
        {
            Gate(SaveState.CurrentLevel, placement, continuation);
        }
    }
}
