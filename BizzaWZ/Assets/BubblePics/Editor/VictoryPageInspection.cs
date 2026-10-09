using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SnakeEscape.Recovered;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewVictoryCore(string folder,Action<bool,string> check)
        {
            int invoked=0;var page=(RecoveredVictoryPanel)await UIModule.Instance.OpenPage<Action>(UIPageIds.WhiteWinPanel,()=>invoked++);inspectedPage=page;await UniTask.DelayFrame(5);
            var next=page.transform.Find("Next").GetComponent<Button>();check(HitStandard(next),"Next Level owns its native Button and receives raycasts");check(page.transform.Find("Body").GetComponent<TMP_Text>().text==Localization.Tr("ui_puzzle_complete"),"Victory message matches current puzzle game");await PayPalCapture(folder,"Reference-State.png");
            foreach(string lang in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(lang);await UniTask.DelayFrame(5);foreach(var label in page.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();check(!label.isTextOverflowing,"Victory caption fits "+lang+"/"+label.name);}await PayPalCapture(folder,lang+".png");}
            PressStandard(next);PressStandard(next);check(invoked==1,"Repeated Next Level clicks invoke callback only once");await UniTask.Delay(2400,ignoreTimeScale:true);check(!UIModule.Instance.PageIsOpen(UIPageIds.WhiteWinPanel)&&BizzaGameplayBridge.Page!=null&&BizzaGameplayBridge.Page.gameObject.activeInHierarchy,"Next Level closes victory and uses original level-loading flow");inspectedPage=null;
        }
    }
}
