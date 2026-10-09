using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewDailyMissionCore(string folder,Action<bool,string> check)
        {
            var p=(DailyMissionPanel)await UIModule.Instance.OpenPage(UIPageIds.DailyMissionPanel);inspectedPage=p;await UniTask.Delay(2500,ignoreTimeScale:true);check(p!=null&&p.gameObject.activeInHierarchy,"Daily mission accepts production response");await PayPalCapture(folder,"Live-State.png");
            check(!string.IsNullOrEmpty(p.refreshTimeTxt.text),"Reset timer remains live");check(HitStandard(p.transform.Find("Close").GetComponent<Button>()),"Native close receives raycasts");
            LanguageUtils.SelectedLanguage="pt-BR";Localization.SetLocale("pt_BR");p.OnRequestData();await UniTask.Delay(2000,ignoreTimeScale:true);
            await PayPalCapture(folder,"pt_BR-Live-Unity.png");
            p.refreshTimeTxt.ForceMeshUpdate();check(!p.refreshTimeTxt.isTextOverflowing,"Enlarged Portuguese countdown fits without shrinking");
            LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");await UniTask.Delay(1100,ignoreTimeScale:true);
            // View-only art comparison; no save, ad, claim or withdrawal request.
            p.transform.Find("RewardAmount").GetComponent<TMP_Text>().text="$0.20";p.hintsTxt.text="Watch 5 videos";p.transform.Find("Progress").GetComponent<TMP_Text>().text="3 / 5";p.transform.Find("Fill").GetComponent<Image>().fillAmount=.6f;p.GoObj.SetActive(true);p.WithdrawObj.SetActive(false);p.ClaimedObj.SetActive(false);p.claimedHint.gameObject.SetActive(false);await UniTask.DelayFrame(5);await PayPalCapture(folder,"Reference-State.png");
            foreach(var group in new[]{p.GoObj,p.WithdrawObj,p.ClaimedObj}){p.GoObj.SetActive(group==p.GoObj);p.WithdrawObj.SetActive(group==p.WithdrawObj);p.ClaimedObj.SetActive(group==p.ClaimedObj);await UniTask.DelayFrame(3);check(HitStandard(group.GetComponentInChildren<Button>()),"Native action receives input in "+group.name);await PayPalCapture(folder,group.name+".png");}
            p.GoObj.SetActive(true);p.WithdrawObj.SetActive(false);p.ClaimedObj.SetActive(false);
            foreach(string lang in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(lang);await UniTask.DelayFrame(4);foreach(var label in p.GetComponentsInChildren<CoralLocalizedLabel>()){var text=label.GetComponent<TMP_Text>();text.ForceMeshUpdate();check(!text.isTextOverflowing,"Localized caption fits "+lang+"/"+text.name);}}
            PressStandard(p.transform.Find("Close").GetComponent<Button>());await UniTask.DelayFrame(4);check(!UIModule.Instance.PageIsOpen(UIPageIds.DailyMissionPanel),"Close dismisses page");inspectedPage=null;
        }
    }
}
