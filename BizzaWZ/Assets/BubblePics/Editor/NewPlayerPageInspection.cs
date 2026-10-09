using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Bizza;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async void ReviewNewPlayer()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/NewPlayerPage-20260928"));Directory.CreateDirectory(folder);
            var report=new StringBuilder();int failures=0;
            string language=LanguageUtils.SelectedLanguage,locale=Localization.CurrentLocale;
            string stages=JsonUtility.ToJson(SaveDataUtils.FakeWithDrawPanelData);
            bool claimed=SaveDataUtils.GameData.fakeWithdrawPanelFirstWithdraw;
            float balance=ItemUtils.GetItemCount(E_ItemType.Dollar);
            long last=SaveDataUtils.GameData.lastWithdrawTime;
            void Check(bool value,string name){report.AppendLine((value?"PASS ":"FAIL ")+name);if(!value)failures++;}
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null)throw new InvalidOperationException("Production InitWZ startup must finish first.");
                CloseRuntime();LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                var page=(FakeWithdrawPanel)await UIModule.Instance.OpenPage(UIPageIds.FakeWithdrawPanel);inspectedPage=page;
                for(int i=0;i<100&&page.GetComponentsInChildren<NewPlayerPayoutOption>().Length==0;i++)await UniTask.Delay(100,ignoreTimeScale:true);
                await UniTask.Delay(500,ignoreTimeScale:true);
                if(!page.gameObject.activeInHierarchy)throw new InvalidOperationException("Production platform request failed and closed the page.");
                var options=page.GetComponentsInChildren<NewPlayerPayoutOption>();
                Check(options.Length>0,"Payout options arrive through the production server request");
                var actualPlatforms=new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>();foreach(var option in options)actualPlatforms.Add(option.Data);
                Check(page.USWithdrawMissionSOList.Count==5&&page.BRWithdrawMissionSOList.Count==6&&page.IDWithdrawMissionSOList.Count==6,"Original 5 US / 6 BR / 6 ID mission configurations retained");
                Check(page.root.GetComponentsInChildren<WithdrawAmountItem>().Length==page.withdrawMissionSOList.Count,"All active amount cards use the current country's mission count");
                for(int i=0;i<page.withdrawMissionSOList.Count;i++)
                {
                    Check(page.items[i].amountTxt.text==FakeWithdrawPanel.FormatAmount(ItemUtils.FormatCountFloat(new ItemEntry{Type=E_ItemType.Dollar,Count=page.withdrawMissionSOList[i].withdrawMoney})),"Live amount uses original configuration "+i);
                    Check(HitStandard(page.items[i].btn.GetComponent<Button>()),"Amount card owns a native clickable Button "+i);
                    page.items[i].amountTxt.ForceMeshUpdate();Check(!page.items[i].amountTxt.isTextOverflowing,"Live amount fits "+i);
                }
                foreach(var name in new[]{"Back","History","FAQ","Withdraw","Service"})Check(HitStandard(page.transform.Find(name).GetComponent<Button>()),"Native navigation/action Button: "+name);
                foreach(var pair in new[]{("BalancePlate",new Rect(85,306,683,157)),("PayoutOptions",new Rect(86,550,683,180)),("Amounts",new Rect(84,823,685,385)),("Withdraw",new Rect(104,1445,647,143))})
                {
                    var r=TierRect((RectTransform)page.transform.Find(pair.Item1));var e=pair.Item2;float delta=Mathf.Max(Mathf.Abs(r.x-e.x),Mathf.Abs(r.y-e.y),Mathf.Abs(r.width-e.width),Mathf.Abs(r.height-e.height));Check(delta<1,"Reference geometry: "+pair.Item1+" delta="+delta.ToString("F2"));
                }
                Check(page.progressImg.fillAmount>=0&&page.progressImg.fillAmount<=1&&!float.IsNaN(page.progressImg.fillAmount),"Live progress is finite and clamped");
                await PayPalCapture(folder,"Live-State.png");
                // Amount selection is view-only. Do not press the potentially eligible withdrawal action.
                if(page.withdrawMissionSOList.Count>1){PressStandard(page.items[1].btn.GetComponent<Button>());await UniTask.DelayFrame(3);Check(page.curSelectIndex==1&&page.items[1].selectObj.activeSelf&&!page.items[0].selectObj.activeSelf,"Native amount click updates selection and requirements");}
                PressStandard(page.transform.Find("FAQ").GetComponent<Button>());
                for(int i=0;i<50&&!UIModule.Instance.PageIsOpen(UIPageIds.QFA);i++)await UniTask.Delay(100,ignoreTimeScale:true);
                Check(UIModule.Instance.PageIsOpen(UIPageIds.QFA),"FAQ opens production help");
                var faq=UIModule.Instance.GetPage<FAQPanel>();PressStandard(faq.transform.Find("CloseBtn").GetComponent<Button>());await UniTask.Delay(200,ignoreTimeScale:true);Check(!UIModule.Instance.PageIsOpen(UIPageIds.QFA),"FAQ close returns to new-player page");
                PressStandard(page.transform.Find("History").GetComponent<Button>());
                for(int i=0;i<100&&!UIModule.Instance.PageIsOpen(UIPageIds.WithdrawHistory);i++)await UniTask.Delay(100,ignoreTimeScale:true);
                Check(UIModule.Instance.PageIsOpen(UIPageIds.WithdrawHistory),"History opens production history");
                var history=UIModule.Instance.GetPage<WithdrawHistory>();PressStandard(history.transform.Find("CloseBtn").GetComponentInChildren<Button>());await UniTask.Delay(200,ignoreTimeScale:true);Check(!UIModule.Instance.PageIsOpen(UIPageIds.WithdrawHistory),"History close returns to new-player page");
                // Explicit presentation data: no account-country, server response, save or financial state is overwritten.
                var paypal=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.paypalInfo,Os_Me="PayPal"};
                var pagbank=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.pagBankInfo,Os_Me="PagBank"};
                page.PresentPlatforms(new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{paypal,pagbank});await UniTask.DelayFrame(3);
                options=page.GetComponentsInChildren<NewPlayerPayoutOption>();Check(options.Length==2&&HitStandard(options[0].button)&&HitStandard(options[1].button),"Two payout options are directly visible and clickable");
                PressStandard(options[1].button);Check(options[1].selection.activeSelf&&!options[0].selection.activeSelf,"Native payout selection remains exclusive");await PayPalCapture(folder,"Two-Methods.png");
                page.PresentPlatforms(new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{paypal});
                page.items.SetCmptListCount(page.item,page.root,6);
                string[] amounts={"$0.10","$1.00","$2.00","$5.00","$10.00","$20.00"};
                for(int i=0;i<6;i++){page.items[i].Init(page,i,amounts[i],false,false);page.items[i].PresentSelection(i==0,i==0);}
                page.PresentBalance(.1f);page.balanceTxt.text="$0,10";page.hintTxt.text=Localization.Tr("newplayer_requirements_met");page.progressTxt.text="1 / 1";page.progressImg.fillAmount=1;
                page.transform.Find("Service").GetComponent<ServiceBtn>().redDot.gameObject.SetActive(true);
                await UniTask.Delay(1200,ignoreTimeScale:true);Check(page.root.GetComponentsInChildren<WithdrawAmountItem>().Length==6,"All six reference-state amount cards are visible");
                Check(page.items[0].selectObj.activeSelf&&!page.items[1].selectObj.activeSelf,"Reference selected amount uses the native state view");
                await PayPalCapture(folder,"Reference-State.png");
                page.PresentPlatforms(actualPlatforms);page.items.SetCmptListCount(page.item,page.root,page.withdrawMissionSOList.Count);
                foreach(var choice in new[]{("pt-BR","pt_BR","Portuguese.png"),("id-ID","id","Indonesian.png"),("zh-CN","zh_CN","Chinese.png")})
                {
                    LanguageUtils.SelectedLanguage=choice.Item1;Localization.SetLocale(choice.Item2);await UniTask.Delay(300,ignoreTimeScale:true);
                    bool fits=true,glyphs=true;
                    foreach(var label in page.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();fits&=!label.isTextOverflowing;foreach(char c in label.text)if(!char.IsWhiteSpace(c))glyphs&=label.font.HasCharacter(c,true,true);}
                    Check(fits,"Localized visible text fits: "+choice.Item2);Check(glyphs,"Localized text and currency glyphs covered: "+choice.Item2);await PayPalCapture(folder,choice.Item3);
                }
                PressStandard(page.transform.Find("Back").GetComponent<Button>());await UniTask.Delay(200,ignoreTimeScale:true);inspectedPage=null;
                Check(!UIModule.Instance.PageIsOpen(UIPageIds.FakeWithdrawPanel),"Back closes the new-player page");Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input restores after closing");
                LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                page=(FakeWithdrawPanel)await UIModule.Instance.OpenPage(UIPageIds.FakeWithdrawPanel);inspectedPage=page;
                for(int i=0;i<100&&page.balanceTxt.text=="$0,10";i++)await UniTask.Delay(100,ignoreTimeScale:true);
                await UniTask.Delay(1500,ignoreTimeScale:true);
                Check(page.balanceTxt.text==FakeWithdrawPanel.FormatAmount(WithdrawalUtil.GetCustomizedFloatByCountryType(balance)),"Reopening binds the live balance again");
                Check(page.root.GetComponentsInChildren<WithdrawAmountItem>().Length==page.withdrawMissionSOList.Count,"Reopening restores the actual country's amount count");
                Check(JsonUtility.ToJson(SaveDataUtils.FakeWithDrawPanelData)==stages&&SaveDataUtils.GameData.fakeWithdrawPanelFirstWithdraw==claimed&&ItemUtils.GetItemCount(E_ItemType.Dollar)==balance&&SaveDataUtils.GameData.lastWithdrawTime==last,"Inspection leaves claim flags, balances, mission stages and withdrawal history unchanged");
                report.AppendLine("Raw Unity Play Mode captures. Reference-State / Two-Methods use explicitly labeled presentation-only example values through native prefab views. Production initialization, resource loading and server request paths are unchanged. No claim, eligible withdrawal, deduction, payout or synthetic server success executed. Service entry raycast was checked; a support message was not sent.");
            }
            catch(Exception e){Check(false,e.ToString());Debug.LogException(e);}
            finally{UIModule.Instance.ClosePage(UIPageIds.WithdrawHistory);UIModule.Instance.ClosePage(UIPageIds.QFA);CloseRuntime();LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);report.AppendLine("Failures="+failures+" Completed="+DateTime.UtcNow.ToString("O"));File.WriteAllText(folder+"/inspection.txt",report.ToString());}
        }
    }
}
