#if BIZZA_REAL_WITHDRAW
public static class InterstitialProtection
{
    // Captured at GameStart: the saved next level can already advance while settlement is open.
    public static int ActiveLevel { get; private set; }
    public static void BeginLevel(int level) { ActiveLevel = level; }
    public static bool IsProtected(int level, int configuredStartLevel) =>
        level < System.Math.Max(4, configuredStartLevel);
    public static bool IsBlocked
    {
        get
        {
            int level = ActiveLevel > 0 ? ActiveLevel : SaveDataUtils.GameData.playerSelectedLv;
            return IsProtected(level, AdStatisticsConfigMiddleware.Get(level).InterAdStartLevel);
        }
    }
}
#endif
