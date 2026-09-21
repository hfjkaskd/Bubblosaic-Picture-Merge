#if BIZZA_REAL_WITHDRAW
using System;
using UnityEngine;

public static class RewardPopupTiming
{
    [Serializable, Obfuz.ObfuzIgnore]
    private sealed class Settings { public float intervalSeconds; }

    // Uses elapsed real time: UI pauses and timeScale do not change the interval.
    public sealed class Gate
    {
        private bool initialized;
        private double lastShown;
        private readonly double interval;
        public Gate(double intervalSeconds) { interval = intervalSeconds; }
        public void Begin(double now)
        {
            if (initialized) return;
            initialized = true;
            lastShown = now;
        }
        public void Shown(double now) { initialized = true; lastShown = now; }
        public bool TryReserve(double now)
        {
            Begin(now);
            if (now - lastShown < interval) return false;
            lastShown = now; // Debounce merges while the asynchronous panel is opening.
            return true;
        }
    }

    private static Gate gate;
    public static float IntervalSeconds
    {
        get
        {
            var asset = Resources.Load<TextAsset>("Config/reward_popup_timing");
            if (asset == null) throw new InvalidOperationException("Missing reward popup timing configuration.");
            var config = JsonUtility.FromJson<Settings>(asset.text);
            if (config == null || config.intervalSeconds <= 0) throw new InvalidOperationException("Invalid reward popup interval.");
            return config.intervalSeconds;
        }
    }
    private static Gate Current => gate ?? (gate = new Gate(IntervalSeconds));
    public static void BeginSession() => Current.Begin(Time.realtimeSinceStartupAsDouble);
    public static void OnPanelShown() => Current.Shown(Time.realtimeSinceStartupAsDouble);
    public static bool TryReserve() => Current.TryReserve(Time.realtimeSinceStartupAsDouble);
}
#endif
