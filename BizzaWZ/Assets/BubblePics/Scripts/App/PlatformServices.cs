using System;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Result reported by an optional native/platform permission provider.
    /// Providers must report the real native result; callers never infer success
    /// merely because an SDK is absent.
    /// </summary>
    public enum PlatformPermissionResult
    {
        NotRequested = 0,
        Granted = 1,
        Denied = 2,
        Restricted = 3,
        Error = 4,
    }

    public interface IHelpCenterProvider
    {
        bool IsAvailable { get; }
        bool TryShowHelpCenter();
    }

    public interface IInAppReviewProvider
    {
        bool CanRequestReview { get; }
        bool CanOpenStorePage { get; }
        bool TryRequestReview();
        bool TryOpenStorePage();
    }

    public interface IConsentProvider
    {
        bool IsAvailable { get; }
        bool IsPrivacyPreferencesRequired { get; }
        bool TryCheckConsent(Action<PlatformPermissionResult> completed);
        bool TryShowPrivacyPreferences();
    }

    public interface ITrackingAuthorizationProvider
    {
        bool IsAvailable { get; }
        bool TryRequestAuthorization(Action<PlatformPermissionResult> completed);
    }

    public interface INotificationPermissionProvider
    {
        bool IsAvailable { get; }
        bool TryRequestPermission(Action<PlatformPermissionResult> completed);
    }

    [Serializable]
    sealed class PlatformServicesConfigData
    {
        public string helpCenterUrl;
        public string privacyPolicyUrl = "https://oakevergames.com/pp.html";
        public string termsOfServiceUrl = "https://oakevergames.com/tos.html";
        public string iosStoreUrl = "itms-apps://itunes.apple.com/app/id6772909477";
        public string androidStoreUrl = "market://details?id=com.oakever.bubblepics";
        public string desktopStoreUrl = "https://apps.apple.com/app/id6772909477";
    }

    /// <summary>
    /// URLs used when a native SDK is unavailable. Values live in
    /// Resources/Config/platform_services.json so production builds can replace
    /// links without changing gameplay code.
    /// </summary>
    public static class PlatformLinks
    {
        const string ConfigPath = "Config/platform_services";
        static PlatformServicesConfigData _config;

        static PlatformServicesConfigData Config
        {
            get
            {
                if (_config != null) return _config;
                _config = new PlatformServicesConfigData();
                var asset = Resources.Load<TextAsset>(ConfigPath);
                if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                    return _config;

                try
                {
                    JsonUtility.FromJsonOverwrite(asset.text, _config);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Platform service config is invalid: {ex.Message}");
                }
                return _config;
            }
        }

        public static string HelpCenterUrl => ResolveLocale(Config.helpCenterUrl);
        public static string PrivacyPolicyUrl => ResolveLocale(Config.privacyPolicyUrl);
        public static string TermsOfServiceUrl => ResolveLocale(Config.termsOfServiceUrl);

        public static string StoreUrl
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return Config.iosStoreUrl;
#elif UNITY_ANDROID && !UNITY_EDITOR
                return Config.androidStoreUrl;
#else
                return Config.desktopStoreUrl;
#endif
            }
        }

        public static bool CanOpenHelpCenter => IsUsableUrl(HelpCenterUrl);
        public static bool CanOpenPrivacyPolicy => IsUsableUrl(PrivacyPolicyUrl);
        public static bool CanOpenTerms => IsUsableUrl(TermsOfServiceUrl);
        public static bool CanOpenStore => IsUsableUrl(StoreUrl);

        public static bool TryOpenHelpCenter() => TryOpen(HelpCenterUrl, "help center");
        public static bool TryOpenPrivacyPolicy() => TryOpen(PrivacyPolicyUrl, "privacy policy");
        public static bool TryOpenTerms() => TryOpen(TermsOfServiceUrl, "terms of service");
        public static bool TryOpenStore() => TryOpen(StoreUrl, "store page");

        internal static void ResetForTests()
        {
            _config = null;
        }

        static string ResolveLocale(string value)
        {
            if (string.IsNullOrEmpty(value)
                || value.IndexOf("{locale}", StringComparison.Ordinal) < 0)
                return value;
            return value.Replace(
                "{locale}",
                Uri.EscapeDataString(Localization.CurrentLocale));
        }

        static bool TryOpen(string url, string destination)
        {
            if (!IsUsableUrl(url))
                return false;
            try
            {
                Application.OpenURL(url);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not open {destination}: {ex.Message}");
                return false;
            }
        }

        static bool IsUsableUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
                return false;

            string scheme = uri.Scheme.ToLowerInvariant();
            return scheme == Uri.UriSchemeHttp
                || scheme == Uri.UriSchemeHttps
                || scheme == "market"
                || scheme == "itms-apps";
        }
    }

    /// <summary>Optional help-center SDK with a real URL fallback.</summary>
    public static class HelpCenterService
    {
        public static IHelpCenterProvider Provider { get; private set; }

        public static bool IsAvailable =>
            (Provider != null && Provider.IsAvailable) || PlatformLinks.CanOpenHelpCenter;

        public static void SetProvider(IHelpCenterProvider provider)
        {
            Provider = provider;
        }

        public static bool TryShow()
        {
            if (Provider != null && Provider.IsAvailable)
            {
                try
                {
                    if (Provider.TryShowHelpCenter())
                        return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Help-center provider failed: {ex.Message}");
                }
            }
            return PlatformLinks.TryOpenHelpCenter();
        }
    }

    /// <summary>
    /// Native in-app review boundary plus the original one-shot level-5 rule.
    /// The shown flag is written only when a real native request was initiated.
    /// </summary>
    public static class InAppReviewService
    {
        const string ShownFlag = "rate_us_shown";
        public static IInAppReviewProvider Provider { get; private set; }

        public static bool CanOpenStore =>
            (Provider != null && Provider.CanOpenStorePage) || PlatformLinks.CanOpenStore;

        public static bool CanRequestReview
        {
            get
            {
#if BUBBLEPICS_WHITE_PACKAGE
                return false;
#else
                if (Provider != null && Provider.CanRequestReview)
                    return true;
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
#endif
            }
        }

        public static void SetProvider(IInAppReviewProvider provider)
        {
            Provider = provider;
        }

        public static bool TryOpenStore()
        {
            if (Provider != null && Provider.CanOpenStorePage)
            {
                try
                {
                    if (Provider.TryOpenStorePage())
                        return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Review provider could not open the store: {ex.Message}");
                }
            }
            return PlatformLinks.TryOpenStore();
        }

        public static bool TryRequestReview()
        {
#if BUBBLEPICS_WHITE_PACKAGE
            // White-package builds must never show a native review prompt.
            return false;
#else
            if (Provider != null && Provider.CanRequestReview)
            {
                try
                {
                    return Provider.TryRequestReview();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Review provider request failed: {ex.Message}");
                    return false;
                }
            }

#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                return UnityEngine.iOS.Device.RequestStoreReview();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"iOS review request failed: {ex.Message}");
            }
#endif
            return false;
#endif
        }

        /// <summary>
        /// Call when the completion panel for <paramref name="completedLevel"/>
        /// has appeared. Mirrors bubble_rate_us_plugin.gd.
        /// </summary>
        public static bool NotifyLevelCompleted(int completedLevel)
        {
            if (completedLevel != 5 || SaveState.GetFlag(ShownFlag))
                return false;
            if (!TryRequestReview())
                return false;

            SaveState.SetFlag(ShownFlag, true);
            return true;
        }
    }

    /// <summary>CMP/consent boundary. No provider means no CMP entry or fake completion.</summary>
    public static class ConsentService
    {
        public static IConsentProvider Provider { get; private set; }

        public static bool IsPrivacyPreferencesAvailable =>
            Provider != null && Provider.IsAvailable && Provider.IsPrivacyPreferencesRequired;

        public static void SetProvider(IConsentProvider provider)
        {
            Provider = provider;
        }

        public static bool TryCheck(Action<PlatformPermissionResult> completed)
        {
            if (Provider == null || !Provider.IsAvailable)
                return false;
            try
            {
                return Provider.TryCheckConsent(result =>
                {
                    if (result != PlatformPermissionResult.NotRequested)
                    {
                        SaveState.SetFlag("cmp_check_completed",
                            result != PlatformPermissionResult.Error);
                    }
                    completed?.Invoke(result);
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Consent provider check failed: {ex.Message}");
                return false;
            }
        }

        public static bool TryShowPrivacyPreferences()
        {
            if (!IsPrivacyPreferencesAvailable)
                return false;
            try
            {
                return Provider.TryShowPrivacyPreferences();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Consent provider UI failed: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Registration point for native startup services. The restored project does
    /// not pretend those permissions were requested when no provider is present.
    /// </summary>
    public static class PlatformServices
    {
        const string NotificationAskCountKey = "bp_platform_notification_ask_count";
        const int MaxNotificationAsks = 2;

        public static ITrackingAuthorizationProvider TrackingProvider { get; private set; }
        public static INotificationPermissionProvider NotificationProvider { get; private set; }

        static bool _trackingRequestInFlight;
        static bool _notificationRequestInFlight;
        static bool _privacyStartupFlowInFlight;
        static Action _privacyStartupCompleted;

        // Obfuz installs the static decryptor in AfterAssembliesLoaded.  A
        // SubsystemRegistration callback runs earlier than that and this reset
        // reaches other protected project methods, which can permanently fault
        // Obfuz's generated constant-holder type before the decryptor exists.
        // BeforeSceneLoad still resets the services before any scene behaviour
        // runs, while guaranteeing that the decryptor has already been set.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetRuntimeState()
        {
            TrackingProvider = null;
            NotificationProvider = null;
            HelpCenterService.SetProvider(null);
            InAppReviewService.SetProvider(null);
            ConsentService.SetProvider(null);
            _trackingRequestInFlight = false;
            _notificationRequestInFlight = false;
            _privacyStartupFlowInFlight = false;
            _privacyStartupCompleted = null;
            PlatformLinks.ResetForTests();
        }

        public static void SetTrackingProvider(ITrackingAuthorizationProvider provider)
        {
            TrackingProvider = provider;
        }

        public static void SetNotificationProvider(INotificationPermissionProvider provider)
        {
            NotificationProvider = provider;
        }

        /// <summary>
        /// Hook for App after the local privacy agreement is accepted. It runs
        /// CMP first and then tracking authorization. Notification permission is
        /// intentionally a separate hook because the original launcher tracks
        /// and retries it independently.
        /// </summary>
        public static bool AfterPrivacyAccepted()
        {
            return AfterPrivacyAccepted(null);
        }

        /// <summary>
        /// The callback fires once the CMP-to-tracking chain ends. With no
        /// provider it fires synchronously; callers may impose their own timeout
        /// when a native provider is asynchronous.
        /// </summary>
        public static bool AfterPrivacyAccepted(Action completed)
        {
            if (completed != null)
                _privacyStartupCompleted += completed;
            if (_privacyStartupFlowInFlight)
                return true;

            _privacyStartupFlowInFlight = true;
            bool consentStarted = ConsentService.TryCheck(_ => ContinueTrackingAuthorization());
            if (consentStarted)
                return true;
            return ContinueTrackingAuthorization();
        }

        public static int NotificationAskCount
        {
            get
            {
                if (GameplayPreferences.HasKey(NotificationAskCountKey))
                    return Mathf.Clamp(
                        GameplayPreferences.GetInt(NotificationAskCountKey, 0),
                        0,
                        MaxNotificationAsks);
                // Upgrade the previous one-shot bool without discarding the fact
                // that a real request was already made on an older build.
                return SaveState.NotificationAsked ? 1 : 0;
            }
        }

        public static bool RequestNotificationIfNeeded()
        {
            if (_notificationRequestInFlight || NotificationAskCount >= MaxNotificationAsks)
                return false;
            if (NotificationProvider == null || !NotificationProvider.IsAvailable)
                return false;

            _notificationRequestInFlight = true;
            try
            {
                bool started = NotificationProvider.TryRequestPermission(result =>
                {
                    _notificationRequestInFlight = false;
                    if (result == PlatformPermissionResult.NotRequested
                        || result == PlatformPermissionResult.Error)
                        return;

                    int newCount = Mathf.Min(NotificationAskCount + 1, MaxNotificationAsks);
                    GameplayPreferences.SetInt(NotificationAskCountKey, newCount);
                    GameplayPreferences.Save();
                    SaveState.NotificationAsked = true;
                    SaveState.SetFlag("notification_permission_granted",
                        result == PlatformPermissionResult.Granted);
                    SaveState.SetFlag("notification_permission_denied",
                        result == PlatformPermissionResult.Denied
                        || result == PlatformPermissionResult.Restricted);
                });
                if (!started)
                    _notificationRequestInFlight = false;
                return started;
            }
            catch (Exception ex)
            {
                _notificationRequestInFlight = false;
                Debug.LogWarning($"Notification permission request failed: {ex.Message}");
                return false;
            }
        }

        static bool ContinueTrackingAuthorization()
        {
            if (!_privacyStartupFlowInFlight)
                return false;

            if (TryRequestTrackingAuthorization(_ => FinishPrivacyStartupFlow()))
                return true;
            FinishPrivacyStartupFlow();
            return false;
        }

        static bool TryRequestTrackingAuthorization(
            Action<PlatformPermissionResult> completed)
        {
            if (_trackingRequestInFlight
                || TrackingProvider == null
                || !TrackingProvider.IsAvailable)
                return false;

            _trackingRequestInFlight = true;
            try
            {
                bool started = TrackingProvider.TryRequestAuthorization(result =>
                {
                    _trackingRequestInFlight = false;
                    if (result != PlatformPermissionResult.NotRequested
                        && result != PlatformPermissionResult.Error)
                    {
                        SaveState.SetFlag("tracking_authorization_granted",
                            result == PlatformPermissionResult.Granted);
                        SaveState.SetFlag("tracking_authorization_denied",
                            result == PlatformPermissionResult.Denied
                            || result == PlatformPermissionResult.Restricted);
                    }
                    completed?.Invoke(result);
                });
                if (!started)
                    _trackingRequestInFlight = false;
                return started;
            }
            catch (Exception ex)
            {
                _trackingRequestInFlight = false;
                Debug.LogWarning($"Tracking authorization request failed: {ex.Message}");
                return false;
            }
        }

        static void FinishPrivacyStartupFlow()
        {
            if (!_privacyStartupFlowInFlight)
                return;
            _privacyStartupFlowInFlight = false;
            var completed = _privacyStartupCompleted;
            _privacyStartupCompleted = null;
            completed?.Invoke();
        }
    }
}
