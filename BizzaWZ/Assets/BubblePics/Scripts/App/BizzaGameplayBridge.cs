using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BubblePics
{
    /// <summary>Gameplay calls FlowModule; FlowModule callbacks acknowledge state here.</summary>
    public static class BizzaGameplayBridge
    {
        private static App app;
        private static RealGamePanel panel;
        private static bool entered;
        private static bool loading;
        private static bool guideReady;
        private static bool guideEntered;
        private static bool guideCompleted;
        private static bool lossShown;
        private static bool canRevive;
        private static int startedRound = -1;
        private static int lossRound = -1;
        private static Coroutine toolRoutine;

        public static BubblePage Page => app != null ? app.Page : null;
        public static bool CanRevive => entered && lossShown && canRevive && Page != null && Page.IsDead() && lossRound == Page.RoundSeq;
        public static bool IsInputBlocked => !entered || loading || TransparentBlock.IsBlock || LoadingBlock.IsBlock ||
            (TransitionBlock.Instance != null && TransitionBlock.Instance._playingAnim);

        public static async UniTask LoadResourcesAsync()
        {
            if (app != null) return;
            entered = false;
            guideReady = guideEntered = guideCompleted = false;
            startedRound = -1;
            Localization.SetLocale(LanguageUtils.SelectedLanguage);
            var request = Resources.LoadAsync<GameObject>("Prefabs/AppRoot");
            await request;
            if (request.asset == null) throw new InvalidOperationException("BubblePics AppRoot prefab is missing.");
            app = UnityEngine.Object.Instantiate((GameObject)request.asset).GetComponent<App>();
            if (app == null) throw new InvalidOperationException("AppRoot has no App component.");
            app.InitializeGameplay();
            await OpenLevelAsync("fresh");
            app.SetPresentationVisible(false);
        }

        private static async UniTask OpenLevelAsync(string source)
        {
            loading = true;
            int level = SaveDataUtils.GameData.playerSelectedLv;
            if (level < 1) throw new InvalidOperationException("Framework level must start at 1.");
            bool completed = false;
            bool succeeded = false;
            void Finished(int loadedLevel, bool success)
            {
                if (loadedLevel != level) return;
                completed = true;
                succeeded = success;
            }
            Page.LevelOpenCompleted += Finished;
            try
            {
                Page.Input.InputEnabled = false;
                Page.OpenLevel(level, source, 0f);
                await UniTask.WaitUntil(() => completed || app == null);
                if (!succeeded || app == null) throw new InvalidOperationException("Could not load gameplay level " + level);
                await UniTask.WaitUntil(() => Page.Field.AreAllBubblesLanded());
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            }
            finally
            {
                if (Page != null) Page.LevelOpenCompleted -= Finished;
                loading = false;
            }
        }

        public static async UniTask EnterAsync()
        {
            panel = UIModule.Instance.GetPage<RealGamePanel>();
            if (panel == null || app == null) throw new InvalidOperationException("Gameplay scene and RealGamePanel must be ready before entry.");
            if (Page.CurrentLevelNumber != SaveDataUtils.GameData.playerSelectedLv)
                await OpenLevelAsync("fresh");
            app.MountGameplayUi(panel);
            app.SyncFrameworkLanguage();
            entered = true;
            app.SetPresentationVisible(true);
            OnRoundStarted();
            Page.Input.InputEnabled = true;
            FlowModule.CanShowGuide();
            Page.Toolbar.CheckUnlockPopupsDeferred();
        }

        public static void OnFrameworkGuideReady() { guideReady = true; }

        public static void BeginBaseTutorial()
        {
            if (!entered || !guideReady || guideEntered || Page == null) return;
            guideEntered = true;
            guideCompleted = false;
            if (Page.IsTutorialRound()) Page.Tutorial.Begin();
            else CompleteBaseTutorial();
        }

        public static void CompleteBaseTutorial()
        {
            if (!entered || !guideEntered || guideCompleted) return;
            guideCompleted = true;
            FlowModule.NewPlayerGuideEnd();
        }

        public static void OnFrameworkTutorialEnded() { guideCompleted = true; }

        public static void OnRoundStarted()
        {
            if (!entered || Page == null || startedRound == Page.RoundSeq) return;
            FlowModule.GameStart();
            GrantUnlockGifts();
        }

        public static void OnFrameworkGameStarted()
        {
            RewardPopupTiming.BeginSession();
            if (Page != null) InterstitialProtection.BeginLevel(Page.CurrentLevelNumber);
            startedRound = Page != null ? Page.RoundSeq : -1;
            lossShown = false;
            lossRound = -1;
            canRevive = false;
        }

        public static void OnRoundClearing()
        {
            if (toolRoutine != null && app != null) app.StopCoroutine(toolRoutine);
            toolRoutine = null;
            if (Page != null) Page.SetToolBusy(false);
            lossShown = false;
            canRevive = false;
        }

        private static LevelInfo LevelInfo()
        {
            int total = Page.PickedTextures.Length;
            int achieved = Page.CollectedImgs.Count;
            return new LevelInfo { levelTotalTarget = total, levelAchieveTarget = achieved,
                levelRemainingTarget = Math.Max(0, total - achieved), levelProgress = total > 0 ? (double)achieved / total : 0 };
        }

        public static void Win()
        {
            if (!entered || Page == null) return;
            Page.Input.InputEnabled = false;
            FlowModule.OpenGameWinPanel(BizzaLevelResultType.Win, LevelInfo());
            SaveDataUtils.Save();
        }

        public static void Lose(bool allowRevive)
        {
            if (!entered || Page == null || lossShown) return;
            lossShown = true;
            lossRound = Page.RoundSeq;
            canRevive = allowRevive;
            Page.Input.InputEnabled = false;
            FlowModule.OpenGameLosePanel(BizzaLevelResultType.Fail, allowRevive ? LoseReason.Health : LoseReason.Timeout, LevelInfo());
        }

        public static void ApplyRevive(bool success)
        {
            if (!success || !CanRevive) return;
            lossShown = false;
            canRevive = false;
            Page.ApplyFrameworkRevive();
            Page.Input.InputEnabled = true;
            UIModule.Instance.ClosePage(UIPageIds.LosePanel);
            SaveDataUtils.Save();
        }

        public static async UniTask ReloadLevelAsync()
        {
            if (loading || app == null) return;
            UIModule.Instance.ClosePage(UIPageIds.LosePanel);
            SaveState.RoundSnapshot = "";
            Page.Input.InputEnabled = false;
            OnRoundClearing();
            await LoadingBlock.AddBlock(typeof(BizzaGameplayBridge));
            try
            {
                await OpenLevelAsync("restart");
                Page.Input.InputEnabled = true;
                if (guideEntered && !guideCompleted && Page.IsTutorialRound()) Page.Tutorial.Begin();
            }
            finally { await LoadingBlock.RemoveBlock(typeof(BizzaGameplayBridge)); }
        }

        public static E_ItemType ToolType(string id) => id switch
        {
            "hint" => E_ItemType.GameProp_1,
            "drop" => E_ItemType.GameProp_2,
            "magnet" => E_ItemType.GameProp_3,
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown gameplay tool")
        };

        public static bool UseTool(int index)
        {
            if (IsInputBlocked || Page == null || Page.IsRoundFinalized() || Page.IsToolBusy()) return false;
            bool possible = index == 0 ? Page.Tools.CanApplyHint() : index == 1 ? Page.Tools.CanApplyDrop() : Page.Tools.CanApplyMagnet();
            if (!possible)
            {
                Toast.Show(Localization.Tr(index == 0 ? "BUBBLE_TOOL_NO_PAIR" : index == 1 ? "BUBBLE_TOOL_NO_DROP" : "BUBBLE_TOOL_NO_GROUP"));
                return false;
            }
            if (index == 0) Page.Tools.ApplyHint();
            else toolRoutine = app.StartCoroutine(ApplyToolEffect(index, Page.RoundSeq));
            Page.TrackToolUsed(ToolDef.All[index].Id, "framework", Mathf.RoundToInt(ItemUtils.GetItemCount(ToolType(ToolDef.All[index].Id))));
            return true; // Original tools commit immediately; the framework closes and consumes once.
        }

        private static IEnumerator ApplyToolEffect(int index, int round)
        {
            Page.SetToolBusy(true);
            try { yield return index == 1 ? Page.Tools.ApplyDrop() : Page.Tools.ApplyMagnet(); }
            finally
            {
                if (Page != null && Page.RoundSeq == round) Page.SetToolBusy(false);
                toolRoutine = null;
            }
        }

        public static void CancelToolEffect()
        {
            OnRoundClearing();
            Page?.Tools.ClearHintHighlight();
        }

        public static void GrantUnlockGifts()
        {
            if (Page == null) return;
            foreach (var definition in ToolDef.All)
            {
                string key = "tool_free_granted_" + definition.Id;
                if (Page.CurrentLevelNumber >= definition.UnlockLevel && !SaveState.GetFlag(key))
                {
                    ItemUtils.AddItem(ToolType(definition.Id), 1);
                    SaveState.SetFlag(key, true);
                }
            }
        }

        public static void OnLevelAssetsReady() { if (Page != null) Page.Input.InputEnabled = entered && !loading; }
        public static void OnLoadFailed() { if (Page != null) Page.Input.InputEnabled = false; }
        public static void OpenSettings() { UIModule.Instance.OpenPage(UIPageIds.PausePanel).Forget(); }
        public static void OnSettlementOpened() { if (Page != null) Page.Input.InputEnabled = false; }
    }
}
