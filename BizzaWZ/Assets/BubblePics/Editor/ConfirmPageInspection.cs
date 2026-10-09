using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async void ReviewConfirm()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/ConfirmPage-20260928"));
            Directory.CreateDirectory(folder);var report=new StringBuilder();int failures=0;
            void Check(bool ok,string name){report.AppendLine((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
            string language=LanguageUtils.SelectedLanguage,locale=Localization.CurrentLocale,savedMail=SaveDataUtils.GameData.withdrawEmailInfo;
            long lastWithdraw=SaveDataUtils.GameData.lastWithdrawTime;
            Button Submit(UIWithdrawalConfirmPanel page)=>page.transform.Find("ReferencePanel/SubmitRow/BtnOk").GetComponent<Button>();
            Button Close(UIWithdrawalConfirmPanel page)=>page.transform.Find("ReferencePanel/comfirmClose").GetComponent<Button>();
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null)throw new InvalidOperationException("Formal InitWZ startup must finish first.");
                CloseRuntime();LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                var channel=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.paypalInfo,Os_Me="PayPal",Os_Mlt=.01};
                var form=(UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel,channel,new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{channel},E_WithdrawType.Real,(Action)null,true);
                inspectedParent=form;await UniTask.Delay(350,ignoreTimeScale:true);form.paypalMailInput.Text="player@example.com";
                PressStandard(form.transform.Find("Root/FillRoot/pageContent/BtnWithdrawal").GetComponent<Button>());
                await UniTask.Delay(650,ignoreTimeScale:true);
                var page=UIModule.Instance.GetPage<UIWithdrawalConfirmPanel>();inspectedPage=page;
                Check(page!=null,"Account form opens original confirmation page");if(page==null)throw new InvalidOperationException("Confirmation did not open.");
                Check(page.EmailText.text=="player@example.com","Entered account arrives unchanged");
                Check(page.EmailObj.activeInHierarchy&&!page.CPFObj.activeSelf&&!page.NameObj.activeSelf,"PayPal displays its account field and hides unrelated fields");
                Check(page.paymentImage.sprite!=null,"PayPal logo comes from configured payment data");
                await PayPalCapture(folder,"Live-English.png");
                string amount=page.PaymentValueText.text;page.PaymentValueText.text="$2.50";await UniTask.DelayFrame(3);await PayPalCapture(folder,"Reference-State.png");page.PaymentValueText.text=amount;
                foreach(var pair in new[]{
                    ("ReferencePanel/BG",new Rect(40,335,771,1175)),
                    ("ReferencePanel/PaymentRow/CloudPaymentPlate",new Rect(194,565,464,177)),
                    ("ReferencePanel/AmountRow/Plate",new Rect(95,838,661,145)),
                    ("ReferencePanel/txtContext/Account/AccountText",new Rect(95,1074,661,126)),
                    ("ReferencePanel/SubmitRow/BtnOk",new Rect(92.5f,1322,666,140))})
                {
                    Rect actual=PayPalRect((RectTransform)page.transform.Find(pair.Item1));Rect expected=pair.Item2;
                    float error=Mathf.Max(Mathf.Abs(actual.x-expected.x),Mathf.Abs(actual.y-expected.y),Mathf.Abs(actual.width-expected.width),Mathf.Abs(actual.height-expected.height));
                    Check(error<1.5f,"Reference geometry "+pair.Item1+" actual="+actual+" maxDifference="+error.ToString("F2")+" source px");
                }
                Check(HitStandard(Submit(page)),"Visible Submit is an enabled standard Button and receives raycast");
                Check(HitStandard(Close(page)),"Visible close Button receives raycast");
                PressStandard(Close(page));await UniTask.Delay(220,ignoreTimeScale:true);
                Check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalConfirmPanel)&&UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalPanel),"Closing confirmation returns to the account form");
                Check(form.paypalMailInput.Text=="player@example.com","Closing confirmation retains entered account");
                CloseRuntime();

                var methods=new[]{E_PayeeAccountType.PIX,E_PayeeAccountType.Pagbank,E_PayeeAccountType.Dana};
                var icons=new[]{UIWithdrawalPanel.pixInfo,UIWithdrawalPanel.pagBankInfo,UIWithdrawalPanel.danaInfo};
                for(int i=0;i<methods.Length;i++)
                {
                    var info=new WithDrawInfo{payType=methods[i],withdrawType=E_WithdrawType.Real,type=PayeeAccountType.Email,Re="example@example.com",Ra="081234567890",Name="Test Account",CPF_CNPJ=i<2?"123.456.789-00":"",data=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=icons[i]}};
                    page=(UIWithdrawalConfirmPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalConfirmPanel,info);inspectedPage=page;await UniTask.Delay(350,ignoreTimeScale:true);
                    Check(page.NameText.text==info.Name&&page.NameObj.activeSelf,"Optional name displays unchanged: "+icons[i]);
                    Check(page.CPFObj.activeSelf==(i<2),"Optional identity field visibility: "+icons[i]);
                    var panel=PayPalRect((RectTransform)page.transform.Find("ReferencePanel/BG"));Check(panel.yMin>=0&&panel.yMax<=1846,"Expanded panel stays on screen: "+icons[i]);
                    float previousBottom=-1;
                    foreach(Transform row in page.transform.Find("ReferencePanel/txtContext"))if(row.gameObject.activeSelf){var box=PayPalRect((RectTransform)row);Check(box.yMin>=previousBottom,"Account rows do not overlap: "+icons[i]+"/"+row.name);previousBottom=box.yMax;}
                    Check(HitStandard(Submit(page))&&HitStandard(Close(page)),"Expanded panel buttons remain reachable: "+icons[i]);
                    await PayPalCapture(folder,"Shared-"+icons[i]+".png");CloseRuntime();await UniTask.DelayFrame(2);
                }
                var longInfo=new WithDrawInfo{payType=E_PayeeAccountType.Paypal,withdrawType=E_WithdrawType.Real,type=PayeeAccountType.Email,Re="very.long.account+review.2026@example.com",data=channel};
                page=(UIWithdrawalConfirmPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalConfirmPanel,longInfo);inspectedPage=page;await UniTask.Delay(350,ignoreTimeScale:true);page.EmailText.ForceMeshUpdate();
                Check(page.EmailText.text==longInfo.Re&&!page.EmailText.isTextOverflowing,"Long email is preserved and fits its native text field");await PayPalCapture(folder,"Long-Account.png");CloseRuntime();

                LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);
                longInfo.Re=string.IsNullOrEmpty(savedMail)?"player@example.com":savedMail;
                page=(UIWithdrawalConfirmPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalConfirmPanel,longInfo);inspectedPage=page;await UniTask.Delay(350,ignoreTimeScale:true);await PayPalCapture(folder,"Live-CurrentLocale.png");
                Check(page.EmailText.text==longInfo.Re,"Current locale retains account data");CloseRuntime();await UniTask.DelayFrame(2);
                Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input restores after all dialogs close");
                Check(SaveDataUtils.GameData.lastWithdrawTime==lastWithdraw,"No withdrawal request was initiated");
                report.AppendLine("Reference-State.png uses the display-only amount $2.50 and example email for comparison. Live captures retain the runtime amount. Submit hit testing is verified, but the financial submission itself was not executed.");
            }
            catch(Exception exception){Check(false,exception.ToString());Debug.LogException(exception);}
            finally
            {
                CloseRuntime();SaveDataUtils.GameData.withdrawEmailInfo=savedMail;SaveDataUtils.gameStrategy.SaveData();LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);
                report.AppendLine("Failures="+failures+" Completed="+DateTime.UtcNow.ToString("O"));File.WriteAllText(folder+"/inspection.txt",report.ToString());
            }
        }
    }
}
