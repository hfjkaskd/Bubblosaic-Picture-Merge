using System;

namespace BubblePics
{
    /// <summary>
    /// SDK-neutral rewarded-ad boundary. Platform integration registers a
    /// provider during bootstrap; gameplay never grants an ad reward before
    /// the provider reports a completed reward callback.
    /// </summary>
    public interface IRewardedAdProvider
    {
        bool IsReady { get; }
        void Show(string placement, Action<bool> completed);
    }

    public static class RewardedAds
    {
        public static IRewardedAdProvider Provider { get; private set; }
        static bool _showInFlight;

        /// <summary>
        /// Session-only GM override matching the reference project's VIP mode.
        /// It never persists and is not a production entitlement.
        /// </summary>
        public static bool DebugGrantWithoutShowing { get; set; }

        public static bool IsReady =>
            true &&
            (DebugGrantWithoutShowing ||
             (Provider != null && Provider.IsReady));

        public static bool CanRequest =>
            true &&
            (DebugGrantWithoutShowing || Provider != null);

        public static bool IsShowing =>
            _showInFlight ||
            (Provider is FunSmithSdkBridge bridge && bridge.HasPendingAd);

        public static void Register(IRewardedAdProvider provider)
        {
            if (!true)
                return;
            Provider = provider;
        }

        public static void Show(string placement, Action<bool> completed)
        {
            if (!true)
            {
                FunSmithTelemetry.TrackAdRequestRejected(
                    "rewarded",
                    placement,
                    false,
                    false
                        ? "white_package"
                        : "ads_disabled");
                completed?.Invoke(false);
                return;
            }
            if (DebugGrantWithoutShowing)
            {
                completed?.Invoke(true);
                return;
            }
            if (Provider == null)
            {
                FunSmithTelemetry.TrackAdRequestRejected(
                    "rewarded",
                    placement,
                    false,
                    "unsupported");
                completed?.Invoke(false);
                return;
            }

            // A rapid second tap can arrive before the first rewarded request
            // leaves Unity for the full-screen ad. Treat it as the same user
            // action instead of returning a false result: a false callback
            // creates the "no ad" toast, and Unity's paused game time keeps
            // that toast alive until the player comes back from the real ad.
            if (IsShowing)
            {
                FunSmithTelemetry.TrackAdRequestRejected(
                    "rewarded",
                    placement,
                    Provider.IsReady,
                    "duplicate_ignored");
                return;
            }

            _showInFlight = true;
            try
            {
                Provider.Show(placement, rewarded =>
                {
                    _showInFlight = false;
                    completed?.Invoke(rewarded);
                });
            }
            catch
            {
                _showInFlight = false;
                throw;
            }
        }
    }
}
