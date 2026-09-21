using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Semantic haptic levels used by the restored Godot build. A native
    /// platform package can register a provider; the built-in fallback is kept
    /// only for mobile devices where the native provider is unavailable.
    /// </summary>
    public enum HapticLevel
    {
        VeryWeak = 1,
        Weak = 2,
        Medium = 3,
        Strong = 4,
        Success = 5,
        Long = 7,
        VeryLong = 10,
    }

    public interface IHapticProvider
    {
        bool IsSupported { get; }
        void Play(HapticLevel level);
    }

    public static class Haptics
    {
        public static IHapticProvider Provider { get; private set; }

        public static void Register(IHapticProvider provider)
        {
            Provider = provider;
        }

        public static void Play(HapticLevel level)
        {
            if (!SaveState.VibrateOn)
                return;

            if (Provider != null && Provider.IsSupported)
            {
                Provider.Play(level);
                return;
            }

#if UNITY_IOS || UNITY_ANDROID
            // Unity's built-in API exposes only a generic vibration. App
            // registers NativeHapticProvider during bootstrap so this path is
            // normally reached only on unsupported/failed native devices.
            Handheld.Vibrate();
#endif
        }

        public static HapticLevel FromLegacyIndex(int level)
        {
            if (level <= 0) return HapticLevel.VeryWeak;
            if (level == 1) return HapticLevel.Weak;
            if (level == 2) return HapticLevel.Medium;
            if (level == 3) return HapticLevel.Strong;
            if (level <= 5) return HapticLevel.Success;
            if (level <= 7) return HapticLevel.Long;
            return HapticLevel.VeryLong;
        }
    }
}
