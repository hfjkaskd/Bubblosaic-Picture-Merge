using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewTaskCore(string folder,Action<bool,string> check)
        {
            var p=(UIDailyTaskPage)await UIModule.Instance.OpenPage(UIPageIds.UI_DailyTaskPage);inspectedPage=p;await UniTask.DelayFrame(10);
            check(p.DailyRootObj.activeSelf&&!p.PlayTimeRootObj.activeSelf,"Daily opens through original controller");check(HitStandard(p.closeBtn),"Native close Button receives input");
            await PayPalCapture(folder,"Reference-State.png");
            foreach(var b in new[]{p.tabWeekly,p.tabPlaytime,p.tabDaily}){var button=b.GetComponent<Button>();check(HitStandard(button),"Reachable tab "+b.name);PressStandard(button);await UniTask.DelayFrame(5);check(p.gameObject.activeInHierarchy,"Switch tab retains page "+b.name);await PayPalCapture(folder,b.name+".png");}
            var scroll=p.dailyTaskRoot.GetComponentInParent<ScrollRect>();float content=scroll.content.rect.height;scroll.verticalNormalizedPosition=0;await UniTask.DelayFrame(5);check(content<=scroll.viewport.rect.height||scroll.content.anchoredPosition.y>0,"Long task list can reach bottom");await PayPalCapture(folder,"Scrolled.png");scroll.verticalNormalizedPosition=1;
            foreach(var row in p.dailyTaskRoot.GetComponentsInChildren<UIDailyTaskElement>()){check(!string.IsNullOrEmpty(row.descTxt.text),"Live task description bound");check(!float.IsNaN(row.progressBar.fillAmount),"Task progress finite");foreach(var b in new[]{row.btnReward,row.btnAds,row.btnGoto})check(b.targetGraphic!=null&&b.targetGraphic.gameObject==b.gameObject,"Task action owns visual "+b.name);}
            foreach(string lang in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(lang);await UniTask.DelayFrame(5);foreach(var label in p.GetComponentsInChildren<CoralLocalizedLabel>()){var text=label.GetComponent<TMP_Text>();text.ForceMeshUpdate();check(!text.isTextOverflowing,"Localized UI fits "+lang+"/"+text.name);}await PayPalCapture(folder,lang+".png");}
            PressStandard(p.closeBtn);await UniTask.DelayFrame(4);check(!UIModule.Instance.PageIsOpen(UIPageIds.UI_DailyTaskPage),"Close dismisses task page");inspectedPage=null;
        }
    }
}
