using System;
using System.Collections;
using System.Collections.Generic;
using BubblePics.SpineLite;
using Obfuz;
using UnityEngine;
using UnityEngine.Scripting;

namespace BubblePics
{
    /// <summary>
    /// Unity side of the FunSmith Android shell contract. The native shell
    /// calls the public methods on the persistent GameApp object through
    /// UnityPlayer.UnitySendMessage.
    /// </summary>
    [Preserve]
    [ObfuzIgnore]
    public sealed class FunSmithSdkBridge : MonoBehaviour,
        IRewardedAdProvider,
        IInterstitialAdProvider
    {
        const string CallbackObjectName = "GameApp";
        const float ReadinessRequestTimeoutSeconds = 3f;
        const float CallbackTimeoutSeconds = 120f;

        const int BannerCode = 1;
        const int InterstitialCode = 2;
        const int RewardedCode = 3;

        static FunSmithSdkBridge _instance;
        static string _lastRewardedShowId = string.Empty;

        bool _bannerReady;
        bool _interstitialReady;
        bool _rewardedReady;
        bool _initialized;
        float _readinessRequestDeadline;
        bool _readinessRequestInFlight;

        int _pendingAdCode;
        string _pendingPlacement = string.Empty;
        string _pendingShowId = string.Empty;
        int _pendingLevelNum;
        bool _pendingShowInvoked;
        bool _pendingAdRequestTracked;
        Action<bool> _rewardedCompleted;
        Action<bool> _interstitialCompleted;
        Coroutine _pendingTimeout;

        public static FunSmithSdkBridge Instance => _instance;
        public static bool IsInstalled => _instance != null;
        public static bool IsInitialized => _instance != null && _instance._initialized;
        public static string LastRewardedShowId => _lastRewardedShowId;
        public bool HasPendingAd => _pendingAdCode != 0;

        bool IRewardedAdProvider.IsReady => IsAdReady(RewardedCode);

        bool IInterstitialAdProvider.IsReady => IsAdReady(InterstitialCode);

        public static FunSmithSdkBridge Install()
        {
            if (_instance != null)
                return _instance;

            GameObject host = GameObject.Find(CallbackObjectName);
            if (host == null)
                host = new GameObject(CallbackObjectName);
            FunSmithSdkBridge bridge = host.GetComponent<FunSmithSdkBridge>();
            if (bridge == null)
                bridge = host.AddComponent<FunSmithSdkBridge>();
            DontDestroyOnLoad(host);

#if UNITY_ANDROID && !UNITY_EDITOR && !BUBBLEPICS_WHITE_PACKAGE
            RewardedAds.Register(bridge);
            InterstitialAds.Register(bridge);
#endif
            return bridge;
        }

        public static bool ShowBanner(string placement = "gameplay")
        {
            return true &&
                   _instance != null &&
                   _instance.BeginAd(BannerCode, placement, null, null);
        }

        public static void CloseBanner()
        {
            if (!true || _instance == null)
                return;
            _instance.CallActivity("SDKCloseBanner");
        }

        public static bool SendCustomEvent(string eventName, string json)
        {
            string normalizedEventName = eventName ?? string.Empty;
            string normalizedJson = string.IsNullOrEmpty(json) ? "{}" : json;

            // Keep the native FunSmith route intact while mirroring the same
            // event name and payload to Bizza Analytics. Bizza queues events
            // raised before its encrypted configuration has finished loading.
            BubblePicsBizzaAnalytics.TrackJson(
                normalizedEventName,
                normalizedJson);

            return _instance != null &&
                   _instance.CallActivity(
                       "SDKCustomEvent",
                       normalizedEventName,
                       normalizedJson);
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            gameObject.name = CallbackObjectName;
        }

        void OnDestroy()
        {
            if (_instance != this)
                return;
            _instance = null;
            CompletePending(false);
        }

        void IRewardedAdProvider.Show(string placement, Action<bool> completed)
        {
            BeginAd(RewardedCode, placement, completed, null);
        }

        void IInterstitialAdProvider.Show(
            int levelNum,
            string placement,
            Action<bool> completed)
        {
            BeginAd(
                InterstitialCode,
                placement,
                null,
                completed,
                levelNum);
        }

        bool BeginAd(
            int code,
            string placement,
            Action<bool> rewardedCompleted,
            Action<bool> interstitialCompleted,
            int levelNum = 0)
        {
            if (!true)
            {
                if (code == RewardedCode)
                    rewardedCompleted?.Invoke(false);
                else if (code == InterstitialCode)
                    interstitialCompleted?.Invoke(false);
                return false;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_pendingAdCode != 0)
            {
                FunSmithTelemetry.TrackAdRequestRejected(
                    AdTypeName(code),
                    placement,
                    IsReady(code),
                    "already_showing");
                if (code == RewardedCode)
                    rewardedCompleted?.Invoke(false);
                else if (code == InterstitialCode)
                    interstitialCompleted?.Invoke(false);
                return false;
            }

            _pendingAdCode = code;
            _pendingPlacement = string.IsNullOrEmpty(placement)
                ? "unknown"
                : placement;
            _pendingShowId = Guid.NewGuid().ToString("N");
            _pendingLevelNum = Mathf.Max(
                1,
                levelNum > 0 ? levelNum : SaveState.CurrentLevel);
            if (code == RewardedCode)
                _lastRewardedShowId = string.Empty;
            _pendingShowInvoked = false;
            _pendingAdRequestTracked = false;
            _rewardedCompleted = rewardedCompleted;
            _interstitialCompleted = interstitialCompleted;
            FunSmithTelemetry.TrackAdSessionStarted();
            FunSmithTelemetry.TrackAd(
                "ad_show_timing",
                _pendingShowId,
                PlacementName(code),
                _pendingPlacement);

            RestartTimeout();
            if (RequestAdReadiness())
                return true;

            FunSmithTelemetry.TrackAdRequestRejected(
                AdTypeName(code),
                _pendingPlacement,
                false,
                "bridge_call_failed",
                _pendingShowId);
            CompletePending(false);
            return false;
#else
            FunSmithTelemetry.TrackAdRequestRejected(
                AdTypeName(code),
                placement,
                false,
                "unsupported");
            if (code == RewardedCode)
                rewardedCompleted?.Invoke(false);
            else if (code == InterstitialCode)
                interstitialCompleted?.Invoke(false);
            return false;
#endif
        }

        bool RequestAdReadiness()
        {
            if (_readinessRequestInFlight)
            {
                if (Time.unscaledTime < _readinessRequestDeadline)
                    return true;
                _readinessRequestInFlight = false;
            }

            _readinessRequestInFlight = true;
            _readinessRequestDeadline =
                Time.unscaledTime + ReadinessRequestTimeoutSeconds;
            if (CallActivity("SDKCheckAdReady"))
                return true;

            _readinessRequestInFlight = false;
            return false;
        }

        void ShowPendingAd()
        {
            if (_pendingAdCode == 0 || _pendingShowInvoked)
                return;

            _pendingShowInvoked = true;

            string method = _pendingAdCode == BannerCode
                ? "SDKShowBanner"
                : _pendingAdCode == InterstitialCode
                    ? "SDKShowInterstitial"
                    : "SDKShowRewarded";
            string json = JsonUtility.ToJson(new AdRequest
            {
                ad_place = ResolveAdPlace(_pendingPlacement),
                level_num = _pendingLevelNum,
            });
            if (!CallActivity(method, json))
            {
                FunSmithTelemetry.TrackAdRequestRejected(
                    AdTypeName(_pendingAdCode),
                    _pendingPlacement,
                    IsReady(_pendingAdCode),
                    "bridge_call_failed",
                    _pendingShowId);
                CompletePending(false);
            }
        }

        void RestartTimeout()
        {
            if (_pendingTimeout != null)
                StopCoroutine(_pendingTimeout);
            _pendingTimeout = StartCoroutine(PendingTimeout());
        }

        IEnumerator PendingTimeout()
        {
            yield return new WaitForSecondsRealtime(CallbackTimeoutSeconds);
            Debug.LogWarning(
                "FunSmith SDK ad callback timed out: " + _pendingPlacement);
            if (!_pendingShowInvoked)
            {
                FunSmithTelemetry.TrackAdRequestRejected(
                    AdTypeName(_pendingAdCode),
                    _pendingPlacement,
                    IsReady(_pendingAdCode),
                    "readiness_timeout",
                    _pendingShowId);
            }
            CompletePending(false, "timeout", "ad callback timed out");
        }

        void CompletePending(
            bool succeeded,
            string errorCode = "",
            string errorMessage = "")
        {
            if (_pendingTimeout != null)
            {
                StopCoroutine(_pendingTimeout);
                _pendingTimeout = null;
            }

            int code = _pendingAdCode;
            string placement = _pendingPlacement;
            string showId = _pendingShowId;
            bool showInvoked = _pendingShowInvoked;
            Action<bool> rewarded = _rewardedCompleted;
            Action<bool> interstitial = _interstitialCompleted;

            _pendingAdCode = 0;
            _pendingPlacement = string.Empty;
            _pendingShowId = string.Empty;
            _pendingLevelNum = 0;
            _pendingShowInvoked = false;
            _pendingAdRequestTracked = false;
            _rewardedCompleted = null;
            _interstitialCompleted = null;
            _readinessRequestInFlight = false;
            FunSmithTelemetry.TrackAdSessionFinished();

            if (succeeded)
            {
                FunSmithTelemetry.TrackAd(
                    "ad_real_show",
                    showId,
                    PlacementName(code),
                    placement);
            }
            if (showInvoked || succeeded)
            {
                FunSmithTelemetry.TrackAdShowResult(
                    AdTypeName(code),
                    placement,
                    showId,
                    succeeded,
                    errorCode,
                    errorMessage);
            }
            if (succeeded && code == RewardedCode)
                _lastRewardedShowId = showId;

            if (code == RewardedCode)
                rewarded?.Invoke(succeeded);
            else if (code == InterstitialCode)
                interstitial?.Invoke(succeeded);
        }

        bool IsReady(int code)
        {
            switch (code)
            {
                case BannerCode: return _bannerReady;
                case InterstitialCode: return _interstitialReady;
                case RewardedCode: return _rewardedReady;
                default: return false;
            }
        }

        bool IsAdReady(int code)
        {
            if (!true)
                return false;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_pendingAdCode != 0)
                return false;
            return IsReady(code);
#else
            return false;
#endif
        }

        static string PlacementName(int code)
        {
            return code == RewardedCode
                ? "reward"
                : code == InterstitialCode
                    ? "interstitial"
                    : "banner";
        }

        static string AdTypeName(int code)
        {
            return code == RewardedCode
                ? "rewarded"
                : code == InterstitialCode
                    ? "interstitial"
                    : "banner";
        }

        internal static int ResolveAdPlace(string placement)
        {
            switch (placement)
            {
                case "continue": return 1;
                case "tool_hint": return 2;
                case "tool_drop": return 3;
                case "tool_magnet": return 4;
                case "game_end": return 5;
                case "gameplay": return 6;
                default: return 0;
            }
        }

        bool CallActivity(string method, params object[] args)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass(
                           "com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null)
                    {
                        Debug.LogWarning(
                            "FunSmith SDK call skipped because currentActivity is null: " +
                            method);
                        return false;
                    }
                    activity.Call(method, args);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "FunSmith SDK call failed (" + method + "): " + ex.Message);
                return false;
            }
#else
            return false;
#endif
        }

        // Android -> Unity callbacks. Names and signatures are fixed by the SDK.

        [Preserve]
        public void InitSuccess(string data)
        {
            _initialized = true;
            Debug.Log("FunSmith SDK initialized.");
            FunSmithTelemetry.TrackSdkReadyLifecycle();
        }

        [Preserve]
        public void CheckAdReady(string data)
        {
            _readinessRequestInFlight = false;
            if (!TryParseReadiness(
                    data,
                    out _bannerReady,
                    out _interstitialReady,
                    out _rewardedReady))
            {
                Debug.LogWarning(
                    "FunSmith SDK returned invalid ad readiness JSON: " + data);
                if (_pendingAdCode != 0)
                {
                    FunSmithTelemetry.TrackAdRequestRejected(
                        AdTypeName(_pendingAdCode),
                        _pendingPlacement,
                        false,
                        "invalid_readiness",
                        _pendingShowId);
                    CompletePending(false);
                }
                return;
            }

            if (_pendingAdCode == 0)
                return;

            bool ready = IsReady(_pendingAdCode);
            FunSmithTelemetry.TrackAd(
                "ad_is_fill",
                _pendingShowId,
                PlacementName(_pendingAdCode),
                _pendingPlacement,
                ready);
            if (ready)
                ShowPendingAd();
            else
            {
                FunSmithTelemetry.TrackAdRequestRejected(
                    AdTypeName(_pendingAdCode),
                    _pendingPlacement,
                    false,
                    "not_ready",
                    _pendingShowId);
                CompletePending(false);
            }
        }

        [Preserve]
        public void PreloadCallback(string data)
        {
            // Preloading is owned by the native SDK and may complete before
            // Unity has an active show request. Every valid preload callback
            // still represents one ad_request event.
            TrackAdRequestFromCallback(
                data,
                "PreloadCallback",
                allowWithoutPendingRequest: true);
        }

        [Preserve]
        public void ShowAdSuccess(string data)
        {
            // The current read-only Demo shell reports its preload success as
            // ShowAdSuccess. Newer shells may send PreloadCallback first. The
            // per-request guard keeps both contracts from duplicating the
            // ad_request event.
            TrackAdRequestFromCallback(
                data,
                "ShowAdSuccess",
                allowWithoutPendingRequest: false);
            int code = ParseAdCode(data, _pendingAdCode);
            SetReady(code, false);
            if (_pendingAdCode == 0)
                return;
            if (code != 0 && code != _pendingAdCode)
            {
                Debug.LogWarning(
                    "FunSmith SDK returned an unexpected ad type: " + data);
                return;
            }
            CompletePending(true);
        }

        [Preserve]
        public void ShowAdFail(string data)
        {
            bool parsed = TryParseAdFailure(
                data,
                _pendingAdCode,
                out int code,
                out string errorCode,
                out string errorMessage);
            if (!parsed)
            {
                errorCode = "invalid_failure_callback";
                errorMessage = data ?? string.Empty;
            }
            SetReady(code, false);
            Debug.LogWarning(
                "FunSmith SDK ad failed (code=" + code +
                ", errorCode=" + errorCode +
                "): " + errorMessage);
            if (_pendingAdCode == 0)
                return;

            // A failure callback must always release the in-flight request.
            // Otherwise a malformed or mismatched native code would block all
            // later ads until the long callback timeout expires.
            if (code != 0 && code != _pendingAdCode)
                SetReady(_pendingAdCode, false);
            CompletePending(false, errorCode, errorMessage);
        }

        [Preserve]
        public void PaySuccess(string data)
        {
            Debug.Log("FunSmith SDK payment success callback received.");
        }

        [Preserve]
        public void PayFail(string data)
        {
            Debug.LogWarning("FunSmith SDK payment failed: " + data);
        }

        [Preserve]
        public void QuerySuccess(string data)
        {
            Debug.Log("FunSmith SDK product query callback received.");
        }

        void TrackAdRequestFromCallback(
            string data,
            string callbackName,
            bool allowWithoutPendingRequest)
        {
            bool hasPendingRequest = _pendingAdCode != 0;
            if ((!hasPendingRequest && !allowWithoutPendingRequest) ||
                (hasPendingRequest && _pendingAdRequestTracked))
                return;

            int callbackCode = ParseAdCode(
                data,
                hasPendingRequest ? _pendingAdCode : 0);
            if (hasPendingRequest &&
                callbackCode != 0 &&
                callbackCode != _pendingAdCode)
            {
                Debug.LogWarning(
                    "FunSmith SDK " + callbackName +
                    " returned an unexpected ad type: " + data);
                return;
            }

            if (!TryCreateAdRequestEventPayload(
                    data,
                    hasPendingRequest
                        ? _pendingLevelNum
                        : SaveState.CurrentLevel,
                    out string jsonParams))
            {
                Debug.LogWarning(
                    "FunSmith SDK " + callbackName +
                    " returned invalid ad_request data: " + data);
                return;
            }

            if (hasPendingRequest)
                _pendingAdRequestTracked = true;
            if (!SendCustomEvent("ad_request", jsonParams))
            {
                Debug.LogWarning(
                    "FunSmith SDK ad_request forwarding failed: " +
                    jsonParams);
            }
        }

        void SetReady(int code, bool ready)
        {
            switch (code)
            {
                case BannerCode: _bannerReady = ready; break;
                case InterstitialCode: _interstitialReady = ready; break;
                case RewardedCode: _rewardedReady = ready; break;
            }
        }

        internal static bool TryParseReadiness(
            string json,
            out bool banner,
            out bool interstitial,
            out bool rewarded)
        {
            banner = false;
            interstitial = false;
            rewarded = false;
            try
            {
                var values = MiniJson.Parse(json) as Dictionary<string, object>;
                if (values == null)
                    return false;
                banner = ReadBool(values, "isBanner");
                interstitial = ReadBool(values, "isInter");
                rewarded = ReadBool(values, "isReward");
                return values.ContainsKey("isBanner") &&
                       values.ContainsKey("isInter") &&
                       values.ContainsKey("isReward");
            }
            catch (Exception)
            {
                return false;
            }
        }

        static bool ReadBool(Dictionary<string, object> values, string key)
        {
            if (!values.TryGetValue(key, out object value) || value == null)
                return false;
            if (value is bool boolValue)
                return boolValue;
            if (value is double number)
                return Math.Abs(number) > double.Epsilon;
            string text = Convert.ToString(value);
            return string.Equals(text, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
        }

        internal static int ParseAdCode(string json, int fallback)
        {
            try
            {
                var values = MiniJson.Parse(json) as Dictionary<string, object>;
                if (values == null || !values.TryGetValue("code", out object value))
                    return fallback;
                return int.TryParse(Convert.ToString(value), out int code)
                    ? code
                    : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        internal static bool TryParseAdFailure(
            string json,
            int fallbackCode,
            out int code,
            out string errorCode,
            out string errorMessage)
        {
            code = fallbackCode;
            errorCode = string.Empty;
            errorMessage = string.Empty;
            try
            {
                var values = MiniJson.Parse(json) as Dictionary<string, object>;
                if (values == null ||
                    !values.TryGetValue("code", out object codeValue) ||
                    !int.TryParse(Convert.ToString(codeValue), out code))
                {
                    code = fallbackCode;
                    return false;
                }

                if (!values.TryGetValue("errorCode", out object errorCodeValue) ||
                    !values.TryGetValue("errorMsg", out object errorMessageValue))
                {
                    return false;
                }

                errorCode = Convert.ToString(errorCodeValue) ?? string.Empty;
                errorMessage = Convert.ToString(errorMessageValue) ?? string.Empty;
                return true;
            }
            catch (Exception)
            {
                code = fallbackCode;
                errorCode = string.Empty;
                errorMessage = string.Empty;
                return false;
            }
        }

        internal static bool TryCreateAdRequestEventPayload(
            string callbackData,
            int currentLevelNum,
            out string jsonParams)
        {
            jsonParams = string.Empty;
            if (string.IsNullOrWhiteSpace(callbackData))
                return false;

            try
            {
                var callback = MiniJson.Parse(callbackData) as
                    Dictionary<string, object>;
                if (callback == null ||
                    !callback.TryGetValue("jsonParms", out object rawData))
                {
                    return false;
                }

                Dictionary<string, object> eventData;
                if (rawData == null)
                {
                    eventData = new Dictionary<string, object>();
                }
                else if (rawData is Dictionary<string, object> embeddedObject)
                {
                    eventData = new Dictionary<string, object>(embeddedObject);
                }
                else if (rawData is string embeddedJson)
                {
                    eventData = string.IsNullOrWhiteSpace(embeddedJson)
                        ? new Dictionary<string, object>()
                        : MiniJson.Parse(embeddedJson) as
                            Dictionary<string, object>;
                    if (eventData == null)
                        return false;
                }
                else
                {
                    return false;
                }

                // Match the proven FruitsHarvestMaster integration: inherit
                // jsonParms, exclude the outer ad-type code, and override the
                // level with the current game-side value.
                eventData["level_num"] = Mathf.Max(1, currentLevelNum);
                jsonParams = BizzaJson.Serialize(eventData);
                return !string.IsNullOrEmpty(jsonParams);
            }
            catch (Exception)
            {
                jsonParams = string.Empty;
                return false;
            }
        }

        [Serializable]
        sealed class AdRequest
        {
            public int ad_place;
            public int level_num;
        }
    }
}
