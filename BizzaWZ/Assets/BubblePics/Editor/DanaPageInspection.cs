using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewDanaCore(string folder,Action<bool,string> check)
        {
            var saved=SaveDataUtils.GameData;string savedName=saved.withdrawNameInfo,savedPhone=saved.withdrawPhoneInfo;
            var dana=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.danaInfo,Os_Me="DANA",Os_Mlt=.01};
            var ovo=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.ovoInfo,Os_Me="OVO",Os_Mlt=.01};
            try
            {
                var page=(UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel,dana,new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{ovo,dana},E_WithdrawType.Real,(Action)null,true);inspectedPage=page;await UniTask.Delay(700,ignoreTimeScale:true);
                await PayPalCapture(folder,"Live-State.png");page.balanceText.text="Rp10,000";page.accountNameInput.Text=page.accPhoneMailInput.Text="";await UniTask.DelayFrame(5);await PayPalCapture(folder,"Reference-State.png");
                var action=page.transform.Find("Root/FillRoot/pageContent/BtnWithdrawal").GetComponent<Button>();var close=page.transform.Find("Root/FillRoot/pageClose").GetComponent<Button>();check(HitStandard(action)&&HitStandard(close),"DANA action and close own reachable standard Buttons");
                var ways=page.PlatformRoot.GetComponentsInChildren<WithdrawWay>();check(ways.Length==2,"OVO and DANA are both directly visible");foreach(var way in ways)check(HitStandard(way.GetComponent<Button>()),"Payment method hit area "+way.data.Os_Cn);
                foreach(var field in new[]{page.accountNameInput,page.accPhoneMailInput}){var r=PayPalRect((RectTransform)field.transform);check(field.gameObject.activeInHierarchy&&r.height>=110&&r.yMin>900&&r.yMax<1342,"Required input visible above action "+field.name);field.ManualSelect();await UniTask.DelayFrame(2);check(field.Selected,"Native keyboard focus "+field.name);field.ManualDeselect();page.client.HideKeyboard();}
                page.accountNameInput.Text="";page.accPhoneMailInput.Text="081234567890";PressStandard(action);await UniTask.DelayFrame(5);page.client.HideKeyboard();check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalConfirmPanel)&&page.accountNameErrorTra.gameObject.activeSelf,"Empty required name prevents confirmation");
                page.accountNameInput.Text="Test User";page.accPhoneMailInput.Text="player@example.com";PressStandard(action);await UniTask.DelayFrame(5);page.client.HideKeyboard();check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalConfirmPanel)&&page.accPhoneMailErrorTra.gameObject.activeSelf,"Email is rejected by original Indonesian phone validation");await PayPalCapture(folder,"Invalid-Input.png");
                page.accPhoneMailInput.Text="081234567890";PressStandard(action);await UniTask.Delay(400,ignoreTimeScale:true);page.client.HideKeyboard();var confirm=UIModule.Instance.GetPage<UIWithdrawalConfirmPanel>();check(confirm!=null,"Valid name and phone reach original confirmation without payout submission");if(confirm!=null)UIModule.Instance.ClosePage(confirm);
                foreach(var way in ways)if(way.data.Os_Cn==UIWithdrawalPanel.ovoInfo){PressStandard(way.GetComponent<Button>());break;}await UniTask.DelayFrame(5);check(page.accountNameInput.gameObject.activeInHierarchy&&page.accPhoneMailInput.gameObject.activeInHierarchy,"OVO switch retains both required inputs");check(HitStandard(action),"OVO action remains reachable");await PayPalCapture(folder,"OVO-State.png");
                foreach(var way in page.PlatformRoot.GetComponentsInChildren<WithdrawWay>())if(way.data.Os_Cn==UIWithdrawalPanel.danaInfo){PressStandard(way.GetComponent<Button>());break;}await UniTask.DelayFrame(4);
                foreach(string locale in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(locale);await UniTask.DelayFrame(5);foreach(var label in page.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();if(label.name!="Text"&&label.name!="Processed"&&label.name!="Placeholder")check(!label.isTextOverflowing,"DANA localized label fits "+locale+"/"+label.name);}await PayPalCapture(folder,locale+".png");}
                PressStandard(close);await UniTask.Delay(350,ignoreTimeScale:true);check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalPanel),"DANA close restores original navigation");inspectedPage=null;
                string regression=folder+"/SharedFormRegression";Directory.CreateDirectory(regression);await ReviewPixCore(regression,check);
            }
            finally{CloseRuntime();saved.withdrawNameInfo=savedName;saved.withdrawPhoneInfo=savedPhone;SaveDataUtils.gameStrategy.SaveData();}
        }
    }
}
