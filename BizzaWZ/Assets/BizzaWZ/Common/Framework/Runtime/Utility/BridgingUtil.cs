using BubblePics;
using Cysharp.Threading.Tasks;

public static class BridgingUtil
{
    public static int GameLevel
    {
        get => SaveDataUtils.GameData != null ? SaveDataUtils.GameData.playerSelectedLv : 1;
        set { SaveDataUtils.GameData.playerSelectedLv = value; SaveDataUtils.Save(); }
    }
    public const int MAX_REVIVE_COUNT = 1;
    public static bool CanRevive => BizzaGameplayBridge.CanRevive;
    public static UniTask LoadGamePlayAsync() => BizzaGameplayBridge.LoadResourcesAsync();
    public static UniTask EnterGamePlayAsync() => BizzaGameplayBridge.EnterAsync();
    public static void GameStart() => BizzaGameplayBridge.OnFrameworkGameStarted();
    public static void NewPlayerEnter() => BizzaGameplayBridge.GrantUnlockGifts();
    public static void CanShowGuide() => BizzaGameplayBridge.OnFrameworkGuideReady();
    public static void BeginGameplayTutorial() => BizzaGameplayBridge.BeginBaseTutorial();
    public static void NewPlayerGuideEnd() => BizzaGameplayBridge.OnFrameworkTutorialEnded();
    public static void LoadGameLevel() => BizzaGameplayBridge.ReloadLevelAsync().Forget();
    public static bool PropUse_1() => BizzaGameplayBridge.UseTool(0);
    public static bool PropUse_2() => BizzaGameplayBridge.UseTool(1);
    public static bool PropUse_3() => BizzaGameplayBridge.UseTool(2);
    public static bool PropUse_4() => false;
    public static bool PropUse_5() => false;
    public static void PropUseOver(E_ItemType type, bool breakFlow) { if (breakFlow) BizzaGameplayBridge.CancelToolEffect(); }
    public static void PropUse_1_Over(bool breakFlow) => PropUseOver(E_ItemType.GameProp_1, breakFlow);
    public static void PropUse_2_Over(bool breakFlow) => PropUseOver(E_ItemType.GameProp_2, breakFlow);
    public static void PropUse_3_Over(bool breakFlow) => PropUseOver(E_ItemType.GameProp_3, breakFlow);
    public static void PropUse_4_Over(bool breakFlow) => PropUseOver(E_ItemType.GameProp_4, breakFlow);
    public static void PropUse_5_Over(bool breakFlow) => PropUseOver(E_ItemType.GameProp_5, breakFlow);
    public static void OnOpenGameWinPanel() => BizzaGameplayBridge.OnSettlementOpened();
    public static void OnOpenGameLosePanel() => BizzaGameplayBridge.OnSettlementOpened();
    public static void OnOpenGameRevivePanel() => BizzaGameplayBridge.OnSettlementOpened();
    public static void OnReviveResult(bool isRevive) => BizzaGameplayBridge.ApplyRevive(isRevive);
}
