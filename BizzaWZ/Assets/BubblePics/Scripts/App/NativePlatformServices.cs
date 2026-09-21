using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Installs native capabilities that do not depend on the proprietary
    /// UniKit SDK. Unsupported SDK-dependent paths remain unregistered.
    /// </summary>
    public static class NativePlatformServices
    {
        public static void Install(MonoBehaviour host)
        {
            PlatformShortcutService.Install();

#if UNITY_ANDROID && !UNITY_EDITOR
            FunSmithSdkBridge.Install();
            InAppReviewService.SetProvider(new GooglePlayReviewProvider());
            PlatformServices.SetNotificationProvider(
                new AndroidNotificationPermissionProvider());
#elif UNITY_IOS && !UNITY_EDITOR
            if (host != null)
            {
                var provider = new IosNativePermissionProvider(host);
                PlatformServices.SetNotificationProvider(provider);
#if BUBBLEPICS_ENABLE_ATT
                // ATT is intentionally opt-in. An offline build without an ad
                // or attribution SDK must not show a tracking prompt.
                PlatformServices.SetTrackingProvider(provider);
#endif
            }
#endif
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    sealed class AndroidNotificationPermissionProvider :
        INotificationPermissionProvider
    {
        const string PostNotifications =
            "android.permission.POST_NOTIFICATIONS";

        UnityEngine.Android.PermissionCallbacks _callbacks;

        public bool IsAvailable => true;

        public bool TryRequestPermission(
            Action<PlatformPermissionResult> completed)
        {
            int sdk = AndroidPlatformUtility.SdkVersion;
            if (sdk < 33 ||
                UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                    PostNotifications))
            {
                completed?.Invoke(PlatformPermissionResult.Granted);
                return true;
            }

            _callbacks = new UnityEngine.Android.PermissionCallbacks();
            _callbacks.PermissionGranted += _ =>
                Finish(completed, PlatformPermissionResult.Granted);
            _callbacks.PermissionDenied += _ =>
                Finish(completed, PlatformPermissionResult.Denied);
            _callbacks.PermissionDeniedAndDontAskAgain += _ =>
                Finish(completed, PlatformPermissionResult.Restricted);
            UnityEngine.Android.Permission.RequestUserPermission(
                PostNotifications,
                _callbacks);
            return true;
        }

        void Finish(
            Action<PlatformPermissionResult> completed,
            PlatformPermissionResult result)
        {
            _callbacks = null;
            completed?.Invoke(result);
        }
    }

    sealed class GooglePlayReviewProvider : IInAppReviewProvider
    {
        AndroidJavaObject _activity;
        AndroidJavaObject _manager;
        AndroidJavaObject _requestTask;
        AndroidJavaObject _launchTask;
        OnCompleteListener _requestListener;
        OnCompleteListener _launchListener;
        bool _initialized;
        bool _supported;
        bool _requestInFlight;

        public bool CanRequestReview
        {
            get
            {
                EnsureInitialized();
                return _supported && !_requestInFlight;
            }
        }

        public bool CanOpenStorePage => false;

        public bool TryRequestReview()
        {
            EnsureInitialized();
            if (!_supported || _requestInFlight)
                return false;

            try
            {
                _requestInFlight = true;
                _requestTask = _manager.Call<AndroidJavaObject>(
                    "requestReviewFlow");
                if (_requestTask == null)
                {
                    _requestInFlight = false;
                    return false;
                }

                _requestListener = new OnCompleteListener(OnReviewInfoReady);
                _requestTask.Call<AndroidJavaObject>(
                    "addOnCompleteListener",
                    _requestListener);
                return true;
            }
            catch (Exception ex)
            {
                _requestInFlight = false;
                _supported = false;
                Debug.LogWarning(
                    "Google Play in-app review request failed: " + ex.Message);
                return false;
            }
        }

        public bool TryOpenStorePage()
        {
            return false;
        }

        void EnsureInitialized()
        {
            if (_initialized)
                return;
            _initialized = true;

            try
            {
                _activity = AndroidPlatformUtility.CurrentActivity;
                using (var factory = new AndroidJavaClass(
                           "com.google.android.play.core.review.ReviewManagerFactory"))
                {
                    _manager = factory.CallStatic<AndroidJavaObject>(
                        "create",
                        _activity);
                }
                _supported = _activity != null && _manager != null;
            }
            catch (Exception ex)
            {
                _supported = false;
                Debug.LogWarning(
                    "Google Play in-app review is Unsupported: " + ex.Message);
            }
        }

        void OnReviewInfoReady(AndroidJavaObject task)
        {
            try
            {
                if (task == null || !task.Call<bool>("isSuccessful"))
                {
                    Debug.LogWarning(
                        "Google Play in-app review did not return ReviewInfo.");
                    return;
                }

                using (AndroidJavaObject reviewInfo =
                       task.Call<AndroidJavaObject>("getResult"))
                {
                    _launchTask = _manager.Call<AndroidJavaObject>(
                        "launchReviewFlow",
                        _activity,
                        reviewInfo);
                }
                if (_launchTask == null)
                    return;

                _launchListener = new OnCompleteListener(OnReviewFlowFinished);
                _launchTask.Call<AndroidJavaObject>(
                    "addOnCompleteListener",
                    _launchListener);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "Google Play in-app review launch failed: " + ex.Message);
            }
            finally
            {
                _requestTask?.Dispose();
                _requestTask = null;
                _requestListener = null;
                _requestInFlight = false;
            }
        }

        void OnReviewFlowFinished(AndroidJavaObject task)
        {
            // Google intentionally does not reveal whether the review UI was
            // displayed or whether the user submitted a review.
            _launchTask?.Dispose();
            _launchTask = null;
            _launchListener = null;
        }

        sealed class OnCompleteListener : AndroidJavaProxy
        {
            readonly Action<AndroidJavaObject> _completed;

            public OnCompleteListener(Action<AndroidJavaObject> completed)
                : base("com.google.android.gms.tasks.OnCompleteListener")
            {
                _completed = completed;
            }

            public void onComplete(AndroidJavaObject task)
            {
                _completed?.Invoke(task);
            }
        }
    }

    static class AndroidPlatformUtility
    {
        public static int SdkVersion
        {
            get
            {
                try
                {
                    using (var version = new AndroidJavaClass(
                               "android.os.Build$VERSION"))
                    {
                        return version.GetStatic<int>("SDK_INT");
                    }
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        public static AndroidJavaObject CurrentActivity
        {
            get
            {
                using (var unityPlayer = new AndroidJavaClass(
                           "com.unity3d.player.UnityPlayer"))
                {
                    return unityPlayer.GetStatic<AndroidJavaObject>(
                        "currentActivity");
                }
            }
        }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    sealed class IosNativePermissionProvider :
        ITrackingAuthorizationProvider,
        INotificationPermissionProvider
    {
        const float PollIntervalSeconds = 0.1f;
        const float PermissionTimeoutSeconds = 30f;

        readonly MonoBehaviour _host;
        bool _trackingPending;
        bool _notificationPending;

        public IosNativePermissionProvider(MonoBehaviour host)
        {
            _host = host;
        }

        bool ITrackingAuthorizationProvider.IsAvailable =>
            BubblePics_GetTrackingAuthorizationStatus() >= 0;

        bool INotificationPermissionProvider.IsAvailable => true;

        bool ITrackingAuthorizationProvider.TryRequestAuthorization(
            Action<PlatformPermissionResult> completed)
        {
            if (_trackingPending ||
                BubblePics_RequestTrackingAuthorization() == 0)
                return false;

            _trackingPending = true;
            _host.StartCoroutine(PollTracking(completed));
            return true;
        }

        bool INotificationPermissionProvider.TryRequestPermission(
            Action<PlatformPermissionResult> completed)
        {
            if (_notificationPending ||
                BubblePics_RequestNotificationPermission() == 0)
                return false;

            _notificationPending = true;
            _host.StartCoroutine(PollNotification(completed));
            return true;
        }

        IEnumerator PollTracking(Action<PlatformPermissionResult> completed)
        {
            float deadline = Time.realtimeSinceStartup + PermissionTimeoutSeconds;
            int status;
            do
            {
                status = BubblePics_GetTrackingAuthorizationStatus();
                if (status != 0)
                    break;
                yield return new WaitForSecondsRealtime(PollIntervalSeconds);
            }
            while (Time.realtimeSinceStartup < deadline);

            _trackingPending = false;
            completed?.Invoke(MapTracking(status));
        }

        IEnumerator PollNotification(
            Action<PlatformPermissionResult> completed)
        {
            float deadline = Time.realtimeSinceStartup + PermissionTimeoutSeconds;
            int result;
            do
            {
                result = BubblePics_GetNotificationPermissionResult();
                if (result != 0)
                    break;
                yield return new WaitForSecondsRealtime(PollIntervalSeconds);
            }
            while (Time.realtimeSinceStartup < deadline);

            _notificationPending = false;
            PlatformPermissionResult mapped = result == 1
                ? PlatformPermissionResult.Granted
                : result == 2
                    ? PlatformPermissionResult.Denied
                    : PlatformPermissionResult.Error;
            completed?.Invoke(mapped);
        }

        static PlatformPermissionResult MapTracking(int status)
        {
            switch (status)
            {
                case 1: return PlatformPermissionResult.Restricted;
                case 2: return PlatformPermissionResult.Denied;
                case 3: return PlatformPermissionResult.Granted;
                case 0: return PlatformPermissionResult.NotRequested;
                default: return PlatformPermissionResult.Error;
            }
        }

        [DllImport("__Internal")]
        static extern int BubblePics_RequestTrackingAuthorization();

        [DllImport("__Internal")]
        static extern int BubblePics_GetTrackingAuthorizationStatus();

        [DllImport("__Internal")]
        static extern int BubblePics_RequestNotificationPermission();

        [DllImport("__Internal")]
        static extern int BubblePics_GetNotificationPermissionResult();
    }
#endif
}
