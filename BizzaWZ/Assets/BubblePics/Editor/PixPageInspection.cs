using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewPixCore(string folder,Action<bool,string> check)
        {
            var saved=SaveDataUtils.GameData;
            string cpf=saved.withdrawCPFInfo,name=saved.withdrawNameInfo,mail=saved.withdrawEmailInfo,phone=saved.withdrawPhoneInfo;
            string keyEmail=saved.accountIdentificationInfo_E,keyCpf=saved.accountIdentificationInfo_C,keyPhone=saved.accountIdentificationInfo_P,keyRandom=saved.accountIdentificationInfo_V;int keyIndex=saved.pixChannelIndex;
            var pix=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.pixInfo,Os_Me="PIX",Os_Mlt=.01};
            var pag=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.pagBankInfo,Os_Me="PagBank",Os_Mlt=.01};
            try
            {
                LanguageUtils.SelectedLanguage="pt-BR";Localization.SetLocale("pt_BR");
                var page=(UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel,pix,new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{pix,pag},E_WithdrawType.Real,(Action)null,true);inspectedPage=page;await UniTask.Delay(700,ignoreTimeScale:true);
                await PayPalCapture(folder,"Live-State.png");page.OnClickBaXiChannel(0);page.CPFNumberInput.Text="";page.accountNameInput.Text="";page.accountIdentificationInput.Text="";page.balanceText.text="R$0,03";await UniTask.DelayFrame(5);await PayPalCapture(folder,"Reference-State.png");
                var action=page.transform.Find("Root/FillRoot/pageContent/BtnWithdrawal").GetComponent<Button>();var close=page.transform.Find("Root/FillRoot/pageClose").GetComponent<Button>();
                check(HitStandard(action)&&HitStandard(close),"PIX action and close own standard Buttons and receive raycasts");
                var rows=page.PlatformRoot.GetComponentsInChildren<WithdrawWay>();check(rows.Length==2,"PIX and PagBank remain directly visible");foreach(var row in rows)check(HitStandard(row.GetComponent<Button>()),"Payment method hit area "+row.data.Os_Cn);
                foreach(var row in rows)if(row.data.Os_Cn==UIWithdrawalPanel.pagBankInfo){PressStandard(row.GetComponent<Button>());break;}await UniTask.DelayFrame(5);
                check(page.paypalMailInput.gameObject.activeInHierarchy&&page.CPFNumberInput.gameObject.activeInHierarchy&&page.accountNameInput.gameObject.activeInHierarchy,"PagBank keeps all three required native inputs visible");check(HitStandard(action),"PagBank Continue remains visible after in-place channel switch");await PayPalCapture(folder,"PagBank-State.png");
                foreach(var row in page.PlatformRoot.GetComponentsInChildren<WithdrawWay>())if(row.data.Os_Cn==UIWithdrawalPanel.pixInfo){PressStandard(row.GetComponent<Button>());break;}await UniTask.DelayFrame(5);check(page.accountIdentificationInput.gameObject.activeInHierarchy,"Switching back to PIX restores key controls");
                foreach(var field in new[]{page.CPFNumberInput,page.accountNameInput,page.accountIdentificationInput})
                {
                    var rect=PayPalRect((RectTransform)field.transform);check(rect.yMin>600&&rect.yMax<1548&&rect.height>=95,"Input stays visible above Continue: "+field.name);
                    field.ManualSelect();await UniTask.DelayFrame(2);check(field.Selected,"Native input accepts focus: "+field.name);field.ManualDeselect();page.client.HideKeyboard();
                }
                page.CPFNumberInput.Text="invalid";page.accountNameInput.Text="x";page.accountIdentificationInput.Text="invalid";PressStandard(action);await UniTask.Delay(400,ignoreTimeScale:true);page.client.HideKeyboard();
                check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalConfirmPanel),"Invalid PIX identity cannot advance");check(page.CPFNumberErrorTra.gameObject.activeSelf&&page.accountIdentificationErrorTra.gameObject.activeSelf,"Invalid identity displays inline errors");await PayPalCapture(folder,"Invalid-Input.png");
                string[] values={"player@example.com","52998224725","11912345678","123e4567-e89b-12d3-a456-426655440000"};
                for(int i=0;i<4;i++)
                {
                    var option=page.accountTypeDropdown[i];check(HitStandard(option.btn),"PIX type button reachable "+i);PressStandard(option.btn);await UniTask.DelayFrame(2);page.accountIdentificationInput.Text=values[i];check(page.ValidateAndShowError(),"Original validation accepts representative PIX type "+i);
                    int selected=0;foreach(var other in page.accountTypeDropdown)if(other.selectedIcon.activeSelf)selected++;check(selected==1&&option.selected,"Exactly one PIX type selected "+i);
                }
                PressStandard(page.accountTypeDropdown[0].btn);await UniTask.DelayFrame(2);check(page.accountIdentificationInput.Text==values[0],"Changing key type preserves typed value per type");
                page.CPFNumberInput.Text="52998224725";page.accountNameInput.Text="Teste Silva";PressStandard(action);await UniTask.Delay(500,ignoreTimeScale:true);page.client.HideKeyboard();
                var confirmation=UIModule.Instance.GetPage<UIWithdrawalConfirmPanel>();check(confirmation!=null,"Valid PIX form opens original confirmation without submitting payout");if(confirmation!=null)UIModule.Instance.ClosePage(confirmation);
                foreach(string locale in new[]{"en","id","zh_CN"})
                {
                    Localization.SetLocale(locale);await UniTask.DelayFrame(5);foreach(var label in page.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();if(label.name!="Text"&&label.name!="Processed"&&label.name!="Placeholder")check(!label.isTextOverflowing,"PIX localized label fits "+locale+"/"+label.name);}
                    await PayPalCapture(folder,locale+".png");
                }
                PressStandard(close);await UniTask.DelayFrame(3);check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalPanel),"PIX close restores original navigation");
                LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                var paypal=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.paypalInfo,Os_Me="PayPal",Os_Mlt=.01};
                page=(UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel,paypal,new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{paypal},E_WithdrawType.Real,(Action)null,true);inspectedPage=page;await UniTask.Delay(450,ignoreTimeScale:true);
                page.paypalMailInput.Text="player@example.com";page.balanceText.text="$2.50";await UniTask.DelayFrame(4);await PayPalCapture(folder,"PayPal-Regression.png");
                var panel=PayPalRect(page.BG);check(Mathf.Abs(panel.x-27)<1&&Mathf.Abs(panel.y-309)<1&&Mathf.Abs(panel.width-799)<1,"PayPal retains approved panel geometry");check(!page.transform.Find("PixArtwork").gameObject.activeSelf,"PIX artwork is hidden in PayPal");check(HitStandard(page.transform.Find("Root/FillRoot/pageContent/BtnWithdrawal").GetComponent<Button>()),"PayPal Continue remains reachable");
            }
            finally
            {
                CloseRuntime();saved.withdrawCPFInfo=cpf;saved.withdrawNameInfo=name;saved.withdrawEmailInfo=mail;saved.withdrawPhoneInfo=phone;saved.accountIdentificationInfo_E=keyEmail;saved.accountIdentificationInfo_C=keyCpf;saved.accountIdentificationInfo_P=keyPhone;saved.accountIdentificationInfo_V=keyRandom;saved.pixChannelIndex=keyIndex;SaveDataUtils.gameStrategy.SaveData();
            }
        }
    }
}
