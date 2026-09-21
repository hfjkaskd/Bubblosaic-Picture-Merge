using System;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Android dynamic-shortcut boundary restored from ShortcutPlugin. The
    /// original 1.0.9 launcher only consumes and clears Feedback/Important;
    /// feature code may subscribe without coupling to Android JNI.
    /// </summary>
    public static class PlatformShortcutService
    {
        public const string FeedbackActionId = "Feedback";
        public const string ImportantActionId = "Important";

        const string ShortcutExtra = "bubblepics_shortcut";
        static bool _installed;
        static string _pendingAction;

        public static event Action<string> ShortcutReceived;

        public static bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return GetAndroidSdkVersion() >= 25;
#else
                return false;
#endif
            }
        }

        public static string PendingAction => _pendingAction ?? string.Empty;

        // Keep project callbacks behind Obfuz's AfterAssembliesLoaded decryptor
        // bootstrap.  This remains early enough to clear all shortcut state
        // before scene objects can observe it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetRuntimeState()
        {
            if (_installed)
                Application.focusChanged -= OnFocusChanged;
            _installed = false;
            _pendingAction = null;
            ShortcutReceived = null;
        }

        public static void Install()
        {
            if (_installed)
                return;
            _installed = true;
            Application.focusChanged += OnFocusChanged;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsSupported)
                return;
            TryPublishAndroidShortcuts();
            TryReadAndroidShortcut();
#endif
        }

        public static bool TryConsume(out string action)
        {
            action = _pendingAction;
            _pendingAction = null;
            return action == FeedbackActionId || action == ImportantActionId;
        }

        static void OnFocusChanged(bool focused)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (focused)
                TryReadAndroidShortcut();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static int GetAndroidSdkVersion()
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

        static AndroidJavaObject GetActivity()
        {
            using (var unityPlayer = new AndroidJavaClass(
                       "com.unity3d.player.UnityPlayer"))
            {
                return unityPlayer.GetStatic<AndroidJavaObject>(
                    "currentActivity");
            }
        }

        static void TryPublishAndroidShortcuts()
        {
            try
            {
                using (AndroidJavaObject activity = GetActivity())
                using (AndroidJavaObject manager =
                       activity.Call<AndroidJavaObject>(
                           "getSystemService",
                           "shortcut"))
                using (var list = new AndroidJavaObject(
                           "java.util.ArrayList"))
                {
                    AddShortcut(
                        activity,
                        list,
                        FeedbackActionId,
                        "Feedback");
                    AddShortcut(
                        activity,
                        list,
                        ImportantActionId,
                        "Important");
                    manager.Call<bool>("setDynamicShortcuts", list);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "Android shortcuts are Unsupported: " + ex.Message);
            }
        }

        static void AddShortcut(
            AndroidJavaObject activity,
            AndroidJavaObject list,
            string id,
            string title)
        {
            using (AndroidJavaObject activityClass = activity.Call<AndroidJavaObject>(
                       "getClass"))
            using (var intent = new AndroidJavaObject(
                       "android.content.Intent",
                       activity,
                       activityClass))
            using (var builder = new AndroidJavaObject(
                       "android.content.pm.ShortcutInfo$Builder",
                       activity,
                       id))
            {
                intent.Call<AndroidJavaObject>(
                    "setAction",
                    "com.oakever.bubblepics.SHORTCUT." + id);
                intent.Call<AndroidJavaObject>(
                    "putExtra",
                    ShortcutExtra,
                    id);
                builder.Call<AndroidJavaObject>("setShortLabel", title);
                builder.Call<AndroidJavaObject>("setIntent", intent);

                using (AndroidJavaObject appInfo =
                       activity.Call<AndroidJavaObject>("getApplicationInfo"))
                {
                    int iconResource = appInfo.Get<int>("icon");
                    if (iconResource != 0)
                    {
                        using (var iconClass = new AndroidJavaClass(
                                   "android.graphics.drawable.Icon"))
                        using (AndroidJavaObject icon =
                               iconClass.CallStatic<AndroidJavaObject>(
                                   "createWithResource",
                                   activity,
                                   iconResource))
                        {
                            builder.Call<AndroidJavaObject>("setIcon", icon);
                        }
                    }
                }

                using (AndroidJavaObject shortcut =
                       builder.Call<AndroidJavaObject>("build"))
                {
                    list.Call<bool>("add", shortcut);
                }
            }
        }

        static void TryReadAndroidShortcut()
        {
            try
            {
                using (AndroidJavaObject activity = GetActivity())
                using (AndroidJavaObject intent =
                       activity.Call<AndroidJavaObject>("getIntent"))
                {
                    string action = intent.Call<string>(
                        "getStringExtra",
                        ShortcutExtra);
                    if (action != FeedbackActionId && action != ImportantActionId)
                        return;

                    intent.Call("removeExtra", ShortcutExtra);
                    _pendingAction = action;
                    ShortcutReceived?.Invoke(action);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "Could not read Android shortcut intent: " + ex.Message);
            }
        }
#endif
    }
}
