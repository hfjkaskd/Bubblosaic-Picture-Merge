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
        static async void ReviewPayPal()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/PayPalPage-20260928"));Directory.CreateDirectory(folder);
            var report=new StringBuilder();int failures=0;
            void Check(bool ok,string name){report.AppendLine((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
            string language=LanguageUtils.SelectedLanguage,locale=Localization.CurrentLocale;
            string savedMail=SaveDataUtils.GameData.withdrawEmailInfo;
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null)throw new InvalidOperationException("Enter Play Mode through InitWZ first.");
                CloseRuntime();LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                var channel=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.paypalInfo,Os_Me="PayPal",Os_Mlt=.01};
                var form=(UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel,channel,new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{channel},E_WithdrawType.Real,(Action)null,true);
                inspectedPage=form;await UniTask.Delay(1500,ignoreTimeScale:true);
                form.paypalMailInput.Text="player@example.com";await UniTask.DelayFrame(3);
                await PayPalCapture(folder,"Live-English.png");
                string actualAmount=form.balanceText.text;
                // Explicit visual fixture only: no account, currency configuration or reward mutation.
                form.balanceText.text="$2.50";await UniTask.DelayFrame(3);await PayPalCapture(folder,"Reference-State.png");form.paypalMailInput.ManualSelect();form.paypalMailInput.SetCaretToTextEnd();await UniTask.DelayFrame(3);
                Check(form.paypalMailInput.Selected,"Native email field accepts focus");
                await PayPalCapture(folder,"Input-Focused.png");form.client.HideKeyboard();await UniTask.Delay(150,ignoreTimeScale:true);await PayPalCapture(folder,"Reference-Focused.png");form.paypalMailInput.ManualDeselect();form.client.HideKeyboard();form.balanceText.text=actualAmount;
                var tr=form.transform;
                foreach(var item in new[]{
                    ("Root/FillRoot/BG",new Rect(27,309,799,1187)),
                    ("Root/FillRoot/SelectPlatform",new Rect(67,501,718,205)),
                    ("Root/FillRoot/AmountPlate",new Rect(67,736,718,218)),
                    ("Root/FillRoot/pageContent/InfoContent/EmailInfo/mail_input/Background",new Rect(67,1053,718,132)),
                    ("Root/FillRoot/pageContent/BtnWithdrawal",new Rect(67,1292,718,159))})
                {
                    Rect actual=PayPalRect((RectTransform)tr.Find(item.Item1));Rect expected=item.Item2;
                    float error=Mathf.Max(Mathf.Abs(actual.x-expected.x),Mathf.Abs(actual.y-expected.y),Mathf.Abs(actual.width-expected.width),Mathf.Abs(actual.height-expected.height));
                    Check(error<1.5f,"Reference geometry "+item.Item1+" actual="+actual+" maxDifference="+error.ToString("F2")+" source px");
                }
                var action=tr.Find("Root/FillRoot/pageContent/BtnWithdrawal").GetComponent<Button>();var close=tr.Find("Root/FillRoot/pageClose").GetComponent<Button>();
                var captionGraphic=action.transform.Find("ReferenceContinueCaption").GetComponent<Image>();Check(captionGraphic.material.name=="ContinueCaption","Continue caption uses approved pixels");Check(HitStandard(action),"Visible Continue Button receives raycast");Check(HitStandard(close),"Visible close Button receives raycast");
                form.paypalMailInput.Text="invalid-address";PressStandard(action);await UniTask.Delay(300,ignoreTimeScale:true);form.client.HideKeyboard();await UniTask.DelayFrame(3);
                Check(form.paypalMailError.gameObject.activeInHierarchy,"Invalid email displays bound error");Check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalConfirmPanel),"Invalid email cannot advance");
                await PayPalCapture(folder,"Invalid-Email.png");
                form.paypalMailInput.Text="player@example.com";PressStandard(action);await UniTask.Delay(650,ignoreTimeScale:true);
                var confirm=UIModule.Instance.GetPage<UIWithdrawalConfirmPanel>();Check(confirm!=null,"Valid email opens original confirmation page");
                if(confirm!=null){Check(confirm.EmailText.text=="player@example.com","Email passed to confirmation unchanged");UIModule.Instance.ClosePage(confirm);}
                await UniTask.Delay(250,ignoreTimeScale:true);PressStandard(close);await UniTask.Delay(200,ignoreTimeScale:true);Check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalPanel),"Close returns from PayPal form");
                Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input restores after close");
                foreach(var method in new[]{UIWithdrawalPanel.pixInfo,UIWithdrawalPanel.pagBankInfo,UIWithdrawalPanel.danaInfo,UIWithdrawalPanel.ovoInfo})
                {
                    var other=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=method,Os_Me=method,Os_Mlt=.01};
                    var shared=(UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel,other,new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{other},E_WithdrawType.Real,(Action)null,true);inspectedPage=shared;await UniTask.Delay(350,ignoreTimeScale:true);
                    var next=shared.transform.Find("Root/FillRoot/pageContent/BtnWithdrawal").GetComponent<Button>();var box=PayPalRect((RectTransform)next.transform);
                    Check(box.yMin>=0&&box.yMax<=1846&&HitStandard(next),"Shared form action remains visible and reachable: "+method);
                    Check(shared.PlatformRoot.GetComponentsInChildren<WithdrawWay>().Length==1,"Shared form retains payment row: "+method);
                    foreach(var input in shared.GetComponentsInChildren<AdvancedInputFieldPlugin.AdvancedInputField>()){var inputRect=PayPalRect((RectTransform)input.transform);Check(inputRect.height>30&&inputRect.yMax<box.yMin,"Input remains above action: "+method+"/"+input.name);}await PayPalCapture(folder,"Shared-"+method+".png");UIModule.Instance.ClosePage(shared);await UniTask.Delay(150,ignoreTimeScale:true);
                }
                var second=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.pagBankInfo,Os_Me="PagBank",Os_Mlt=.01};
                form=(UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel,channel,new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{channel,second},E_WithdrawType.Real,(Action)null,true);inspectedPage=form;await UniTask.Delay(400,ignoreTimeScale:true);
                var rows=form.PlatformRoot.GetComponentsInChildren<WithdrawWay>();Check(rows.Length==2,"Two methods remain directly visible");
                foreach(var row in rows){var button=row.GetComponent<Button>();Check(HitStandard(button),"Payment method Button receives raycast: "+row.data.Os_Cn);PressStandard(button);await UniTask.DelayFrame(2);Check(row.selectedObj.activeSelf,"Payment selection changes: "+row.data.Os_Cn);var rowRect=PayPalRect((RectTransform)row.transform);var tick=PayPalRect((RectTransform)row.selectedObj.transform.Find("CloudSelectionCheck"));Check(tick.xMin>=rowRect.xMin&&tick.xMax<=rowRect.xMax,"Selection tick stays within payment Button: "+row.data.Os_Cn);}
                await PayPalCapture(folder,"Two-Methods.png");
                CloseRuntime();LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);SaveDataUtils.GameData.withdrawEmailInfo=savedMail;
                form=(UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel,channel,new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{channel},E_WithdrawType.Real,(Action)null,true);inspectedPage=form;await UniTask.Delay(400,ignoreTimeScale:true);await PayPalCapture(folder,"Live-CurrentLocale.png");
                var liveCaption=form.transform.Find("Root/FillRoot/pageContent/BtnWithdrawal/Text (TMP)").GetComponent<TMPro.TMP_Text>();Check(!string.IsNullOrEmpty(liveCaption.text),"Current locale has a nonempty Continue caption");

                report.AppendLine("Reference-State.png uses the explicitly assigned display amount $2.50 and example email for visual alignment only. Live-English.png retains runtime amount. No withdrawal submission or reward claim was executed.");
            }
            catch(Exception e){Check(false,e.ToString());Debug.LogException(e);}
            finally
            {
                CloseRuntime();SaveDataUtils.GameData.withdrawEmailInfo=savedMail;SaveDataUtils.gameStrategy.SaveData();LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);
                report.AppendLine("Failures="+failures+" Completed="+DateTime.UtcNow.ToString("O"));File.WriteAllText(folder+"/inspection.txt",report.ToString());
            }
        }
        static Rect PayPalRect(RectTransform rt)
        {
            var canvas=rt.GetComponentInParent<Canvas>().rootCanvas;var cam=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;var corners=new Vector3[4];rt.GetWorldCorners(corners);Vector2 a=RectTransformUtility.WorldToScreenPoint(cam,corners[0]),b=RectTransformUtility.WorldToScreenPoint(cam,corners[2]);return new Rect(a.x*852/Screen.width,(Screen.height-b.y)*1846/Screen.height,(b.x-a.x)*852/Screen.width,(b.y-a.y)*1846/Screen.height);
        }
        static async UniTask PayPalCapture(string folder,string name)
        {
            string path=Path.Combine(folder,name);DateTime request=DateTime.UtcNow;ScreenCapture.CaptureScreenshot(path);for(int i=0;i<30&&(!File.Exists(path)||File.GetLastWriteTimeUtc(path)<request);i++)await UniTask.Delay(100,ignoreTimeScale:true);if(!File.Exists(path)||File.GetLastWriteTimeUtc(path)<request)throw new IOException("No fresh Unity Game view capture: "+name);
        }
    }
}
