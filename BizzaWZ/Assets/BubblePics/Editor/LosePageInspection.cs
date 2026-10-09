using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        public static async void ReviewLoseTitle(string folder)
        {
            if (!UnityEditor.EditorApplication.isPlaying || BizzaGameplayBridge.Page == null)
                throw new InvalidOperationException("Production gameplay startup must finish first.");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"title-check.txt"),"RUNNING");
            var report = new System.Text.StringBuilder();
            string locale = Localization.CurrentLocale;
            int revives = SaveDataUtils.GameData.currentReviveCount;
            int failures = SaveDataUtils.GameData.levelFailCount;
            LosePanel page = null;
            try
            {
                foreach (string language in new[] { "pt_BR", "en", "id", "pt_BR" })
                {
                    Localization.SetLocale(language);
                    page = (LosePanel)await UIModule.Instance.OpenPage(UIPageIds.LosePanel,LoseReason.Health,new LevelInfo());
                    await UniTask.Delay(450,ignoreTimeScale:true);
                    Canvas.ForceUpdateCanvases();
                    var title = page.transform.Find("TitleLabel").GetComponent<TMP_Text>();
                    var banner = page.transform.Find("ReferenceTitle");
                    title.ForceMeshUpdate();
                    if (title.text != Localization.Tr("ui_try_again") || title.isTextOverflowing ||
                        title.transform.GetSiblingIndex() <= banner.GetSiblingIndex())
                        throw new InvalidOperationException("Localized title is missing, clipped or behind its banner: " + language);
                    bool translated = language != "en";
                    if (translated && (title.GetComponent<CanvasGroup>().alpha < .99f || title.textInfo.characterCount == 0))
                        throw new InvalidOperationException("Translated title has no visible text mesh: " + language);
                    var restart = (page.loseObjs[0].activeSelf ? page.bizzaLoseButton2 : page.bizzaLoseButton1).GetComponent<Button>();
                    if (!HitStandard(restart)) throw new InvalidOperationException("Restart button is blocked.");
                    report.AppendLine("PASS " + language + ": " + title.text + "; title above banner, fits, native Restart reachable.");
                    await PayPalCapture(folder,language + ".png");
                    UIModule.Instance.ClosePage(UIPageIds.LosePanel);
                    await UniTask.DelayFrame(3);
                }
            }
            catch (Exception e) { report.AppendLine("FAIL " + e); Debug.LogException(e); }
            finally
            {
                if (page != null && UIModule.Instance.PageIsOpen(UIPageIds.LosePanel)) UIModule.Instance.ClosePage(UIPageIds.LosePanel);
                Localization.SetLocale(locale);
                report.AppendLine((revives == SaveDataUtils.GameData.currentReviveCount && failures == SaveDataUtils.GameData.levelFailCount
                    ? "PASS " : "FAIL ") + "Revive and failure counters unchanged; no ads or restart invoked.");
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"title-check.txt"),report.ToString());
            }
        }

        static async UniTask ReviewLoseCore(string folder,Action<bool,string> check)
        {
            var page=(LosePanel)await UIModule.Instance.OpenPage(UIPageIds.LosePanel,LoseReason.Health,new LevelInfo());inspectedPage=page;await UniTask.DelayFrame(5);
            bool available=BridgingUtil.CanRevive&&SaveDataUtils.GameData.currentReviveCount<BridgingUtil.MAX_REVIVE_COUNT;
            check(page.reviveButton.interactable==available,"Revive availability follows original gameplay condition");check(page.reviveObjs[0].activeSelf==available&&page.loseObjs[0].activeSelf!=available,"Exactly one original failure action group is visible");await PayPalCapture(folder,"Live-State.png");
            // View-only availability sample. No game defeat, revive counter, ad or reward is simulated.
            page.reviveButton.interactable=true;foreach(var go in page.reviveObjs)go.SetActive(true);foreach(var go in page.loseObjs)go.SetActive(false);await UniTask.DelayFrame(5);await PayPalCapture(folder,"Reference-State.png");
            check(HitStandard(page.reviveButton.GetComponent<Button>())&&HitStandard(page.bizzaLoseButton1.GetComponent<Button>()),"Revive and Restart own visible native Buttons");
            check(page.transform.Find("Reason").GetComponent<TMP_Text>().text==Localization.Tr("out_of_moves"),"Failure description reflects the actual move-based game");
            foreach(string lang in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(lang);await UniTask.DelayFrame(5);foreach(var label in page.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();check(!label.isTextOverflowing,"Failure caption fits "+lang+"/"+label.name);}await PayPalCapture(folder,lang+".png");}
            CloseRuntime();await UniTask.DelayFrame(4);page=(LosePanel)await UIModule.Instance.OpenPage(UIPageIds.LosePanel,LoseReason.Timeout,new LevelInfo());inspectedPage=page;await UniTask.DelayFrame(4);
            check(page.transform.Find("Reason").GetComponent<TMP_Text>().text==Localization.Tr("sequential_round_ended"),"Non-revivable failure uses separate explanation");check(HitStandard(page.bizzaLoseButton2.GetComponent<Button>()),"Unavailable-revive branch keeps Restart reachable");await PayPalCapture(folder,"Revive-Unavailable.png");
            PressStandard(page.bizzaLoseButton2.GetComponent<Button>());await UniTask.Delay(2400,ignoreTimeScale:true);check(!UIModule.Instance.PageIsOpen(UIPageIds.LosePanel)&&BizzaGameplayBridge.Page!=null&&BizzaGameplayBridge.Page.gameObject.activeInHierarchy,"Restart returns through original level-loading flow");inspectedPage=null;
        }
    }
}
