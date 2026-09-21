using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Native implementation of the seven semantic vibration levels from the
    /// restored Godot VibrateManager.
    /// </summary>
    public sealed class NativeHapticProvider : IHapticProvider
    {
#if UNITY_ANDROID
        const int HighMemoryThresholdMb = 3800;

        AndroidJavaObject _vibrator;
        bool _androidInitialized;
        bool _androidSupported;
        bool _hasAmplitudeControl;
        int _androidSdk;
#endif

#if UNITY_IOS
        [DllImport("__Internal", EntryPoint = "BubblePics_PlayHaptic")]
        static extern void PlayIosHaptic(int level);

        [DllImport("__Internal", EntryPoint = "BubblePics_HapticsAvailable")]
        static extern int IosHapticsAvailable();

        bool _iosAvailabilityChecked;
        bool _iosSupported;
#endif

        public bool IsSupported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                EnsureAndroidInitialized();
                return _androidSupported;
#elif UNITY_IOS && !UNITY_EDITOR
                return EnsureIosSupported();
#else
                return false;
#endif
            }
        }

        public void Play(HapticLevel level)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroid(level);
#elif UNITY_IOS && !UNITY_EDITOR
            if (!EnsureIosSupported())
                return;
            try
            {
                PlayIosHaptic((int)level);
            }
            catch (Exception ex)
            {
                _iosSupported = false;
                Debug.LogWarning("Native iOS haptic failed: " + ex.Message);
                Handheld.Vibrate();
            }
#endif
        }

#if UNITY_IOS
        bool EnsureIosSupported()
        {
            if (_iosAvailabilityChecked)
                return _iosSupported;

            _iosAvailabilityChecked = true;
            try
            {
                _iosSupported = IosHapticsAvailable() != 0;
            }
            catch (Exception ex)
            {
                _iosSupported = false;
                Debug.LogWarning(
                    "Native iOS haptic availability check failed: " +
                    ex.Message);
            }
            return _iosSupported;
        }
#endif

#if UNITY_ANDROID
        void EnsureAndroidInitialized()
        {
            if (_androidInitialized)
                return;

            _androidInitialized = true;
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _androidSdk = version.GetStatic<int>("SDK_INT");
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                _androidSupported = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
                if (_androidSupported && _androidSdk >= 26)
                    _hasAmplitudeControl = _vibrator.Call<bool>("hasAmplitudeControl");
            }
            catch (Exception ex)
            {
                _androidSupported = false;
                Debug.LogWarning("Native Android haptic initialization failed: " + ex.Message);
            }
        }

        void PlayAndroid(HapticLevel level)
        {
            EnsureAndroidInitialized();
            if (!_androidSupported)
                return;

            bool highMemory = SystemInfo.systemMemorySize > HighMemoryThresholdMb;
            ResolveAndroidPattern(level, highMemory, out long durationMs, out int amplitude);

            try
            {
                if (_androidSdk >= 26)
                {
                    // Devices without amplitude control still accept a one-shot
                    // effect, but Android must choose its default amplitude.
                    int effectiveAmplitude = _hasAmplitudeControl ? amplitude : -1;
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", durationMs, effectiveAmplitude))
                    {
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    _vibrator.Call("vibrate", durationMs);
                }
            }
            catch (Exception ex)
            {
                _androidSupported = false;
                Debug.LogWarning("Native Android haptic failed: " + ex.Message);
            }
        }

        static void ResolveAndroidPattern(
            HapticLevel level,
            bool highMemory,
            out long durationMs,
            out int amplitude)
        {
            switch (level)
            {
                case HapticLevel.VeryWeak:
                    durationMs = highMemory ? 20L : 40L;
                    amplitude = 10;
                    break;
                case HapticLevel.Weak:
                    durationMs = highMemory ? 20L : 40L;
                    amplitude = 80;
                    break;
                case HapticLevel.Medium:
                    durationMs = highMemory ? 20L : 40L;
                    amplitude = highMemory ? 150 : 200;
                    break;
                case HapticLevel.Strong:
                    durationMs = 200L;
                    amplitude = 50;
                    break;
                case HapticLevel.Success:
                    durationMs = highMemory ? 30L : 60L;
                    amplitude = 250;
                    break;
                case HapticLevel.Long:
                    durationMs = 130L;
                    amplitude = 50;
                    break;
                case HapticLevel.VeryLong:
                default:
                    durationMs = 200L;
                    amplitude = 50;
                    break;
            }
        }
#endif
    }
}
