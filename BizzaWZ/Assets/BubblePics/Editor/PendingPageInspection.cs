using System;
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
        static async void ReviewPending()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/PendingPage-20260928"));
            Directory.CreateDirectory(folder);var report=new StringBuilder();int failures=0;
            string language=LanguageUtils.SelectedLanguage,locale=Localization.CurrentLocale;
            long lastWithdraw=SaveDataUtils.GameData.lastWithdrawTime;
            void Check(bool value,string name){report.AppendLine((value?"PASS ":"FAIL ")+name);if(!value)failures++;}
            Button Action(UIWithdrawalPendingPanel page)=>page.transform.Find("BtnConfirm").GetComponent<Button>();
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null)throw new InvalidOperationException("Formal InitWZ startup must finish first.");
                CloseRuntime();UIWithdrawalPendingPanel.ResetNetworkCallbackState();
                LanguageUtils.SelectedLanguage="pt-BR";Localization.SetLocale("pt_BR");
                int confirms=0,completions=0,closes=0;bool completedSuccess=false,closedSuccess=false;
                var info=new UIWithdrawalPendingInfo(E_PayeeAccountType.Paypal,"$2.50",UIWithdrawalPanel.paypalInfo,
                    onConfirm:()=>confirms++,onProgressComplete:success=>{completions++;completedSuccess=success;},onResultClose:success=>{closes++;closedSuccess=success;});
                var page=(UIWithdrawalPendingPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPendingPanel,info);inspectedPage=page;
                await UniTask.Delay(700,ignoreTimeScale:true);
                Check(!Action(page).IsInteractable(),"Confirm remains disabled before network completion");
                Check(!page.NativeClose,"Native close remains blocked during submission");
                Check(page.progressRoot.activeInHierarchy&&!page.resultRoot.activeInHierarchy,"Submission progress is shown before response");
                Check(page.progressFill.fillAmount>0&&page.progressFill.fillAmount<1,"Submission progress advances without completing early");
                Check(page.titleText.text==Localization.Tr("ui_review_processing"),"Pending state does not claim the request succeeded");
                await PayPalCapture(folder,"Submitting.png");
                UIWithdrawalPendingPanel.NotifyNetworkCallback(true);
                await UniTask.Delay(750,ignoreTimeScale:true);
                Check(completions==1&&completedSuccess,"Success completion callback occurs once with true");
                Check(page.NativeClose&&HitStandard(Action(page)),"Success enables the visible standard Confirm Button and native close");
                Check(!page.progressRoot.activeInHierarchy&&page.resultRoot.activeInHierarchy,"Submission progress transitions to result state");
                Check(page.transform.Find("Hourglass").gameObject.activeSelf&&page.transform.Find("ResultRoot/SubmittedReview").gameObject.activeInHierarchy,"Success retains the hourglass and awaiting-review indicator");
                Check(!page.resultIcon.gameObject.activeSelf,"Failure icon is hidden on success");
                Check(page.amountText.text=="$2.50","Amount passed to the page is preserved");
                Check(ReleaseArtInspection.NamedSprite(page.paymentImage.sprite,"PayPal"),"PayPal logo uses configured channel artwork");
                Check(page.titleText.text=="Solicitação enviada!"&&page.hintText.text==Localization.Tr("withdraw_pending_submitted"),"Success title and message match the approved wording");
                Check(!page.transform.Find("PageBackdrop").gameObject.activeSelf&&!page.transform.Find("OpaqueUnderlay").gameObject.activeSelf,"Legacy full-screen backgrounds cannot cover the authored panel");
                Check(ReleaseArtInspection.Caption(page.transform.Find("ReferenceTitleCaption").GetComponent<Image>(),true),"Portuguese caption artwork tracks the live title");
                foreach(var pair in new[]{
                    ("BG",new Rect(39,313,776,1239)),
                    ("Hourglass",new Rect(299,488,255,249)),
                    ("CloudPaymentPlate",new Rect(94,864,667,155)),
                    ("ReviewPlate",new Rect(91,1041,673,280)),
                    ("BtnConfirm",new Rect(83,1349,689,155))})
                {
                    Rect actual=PayPalRect((RectTransform)page.transform.Find(pair.Item1));Rect expected=pair.Item2;
                    float error=Mathf.Max(Mathf.Abs(actual.x-expected.x),Mathf.Abs(actual.y-expected.y),Mathf.Abs(actual.width-expected.width),Mathf.Abs(actual.height-expected.height));
                    Check(error<1,"Reference geometry "+pair.Item1+" actual="+actual+" maxDifference="+error.ToString("F2")+" source px");
                }
                await PayPalCapture(folder,"Reference-State.png");
                PressStandard(Action(page));await UniTask.Delay(150,ignoreTimeScale:true);
                Check(confirms==1&&closes==1&&closedSuccess,"Confirm and close callbacks each occur once after success");
                Check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalPendingPanel),"Confirm closes the result page");inspectedPage=null;
                Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input restores after confirming success");

                UIWithdrawalPendingPanel.ResetNetworkCallbackState();
                info=new UIWithdrawalPendingInfo(E_PayeeAccountType.Paypal,"$2.50",onProgressComplete:success=>{completions++;completedSuccess=success;},onResultClose:success=>{closes++;closedSuccess=success;});
                page=(UIWithdrawalPendingPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPendingPanel,info);inspectedPage=page;
                Check(!Action(page).IsInteractable()&&page.progressRoot.activeSelf&&!page.resultRoot.activeSelf,"Reopened page resets the previous successful state");
                const string errorMessage="Não foi possível confirmar a conta. Confira os dados e tente novamente.";
                UIWithdrawalPendingPanel.NotifyNetworkCallback(false,errorMessage);await UniTask.Delay(650,ignoreTimeScale:true);
                Check(completions==2&&!completedSuccess,"Failure completion callback preserves false result");
                Check(page.hintText.text==errorMessage,"Server failure message remains visible without replacement");
                Check(page.resultIcon.gameObject.activeInHierarchy&&page.resultIcon.sprite==page.failResultSprite,"Failure uses the bound failure icon");
                Check(!page.transform.Find("Hourglass").gameObject.activeSelf&&!page.transform.Find("ResultRoot/SubmittedReview").gameObject.activeSelf,"Failure hides success-only waiting visuals");
                page.hintText.ForceMeshUpdate();Check(!page.hintText.isTextOverflowing,"Failure message fits without hiding the action");
                Check(HitStandard(Action(page)),"Failure can be dismissed through the visible Confirm Button");
                await PayPalCapture(folder,"Failure.png");PressStandard(Action(page));await UniTask.Delay(150,ignoreTimeScale:true);inspectedPage=null;
                Check(closes==2&&!closedSuccess,"Failure result reaches close callback unchanged");

                UIWithdrawalPendingPanel.NotifyNetworkCallback(true);
                page=(UIWithdrawalPendingPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPendingPanel,new UIWithdrawalPendingInfo(E_PayeeAccountType.PIX,"R$12,34",UIWithdrawalPanel.pixInfo));inspectedPage=page;await UniTask.Delay(650,ignoreTimeScale:true);
                Check(page.NativeClose&&HitStandard(Action(page)),"Callback received before opening is consumed correctly");
                Check(page.amountText.text=="R$12,34"&&page.paymentImage.sprite==page.paymentList.GetSpriteByIconKey(UIWithdrawalPanel.pixInfo),"PIX channel and amount remain dynamic");
                await PayPalCapture(folder,"PIX-Success.png");PressStandard(Action(page));await UniTask.Delay(150,ignoreTimeScale:true);inspectedPage=null;

                LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                page=(UIWithdrawalPendingPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPendingPanel,new UIWithdrawalPendingInfo(E_PayeeAccountType.Dana,"$123,456.78",UIWithdrawalPanel.danaInfo));inspectedPage=page;
                UIWithdrawalPendingPanel.NotifyNetworkCallback(true);await UniTask.Delay(650,ignoreTimeScale:true);
                Check(page.titleText.text=="Request sent!"&&page.titleText.GetComponent<CanvasGroup>().alpha==1,"English uses the native localized title");
                Check(ReleaseArtInspection.Caption(page.transform.Find("ReferenceTitleCaption").GetComponent<Image>(),false),"Reference Portuguese title hides for English");
                page.amountText.ForceMeshUpdate();Check(page.amountText.text=="$123,456.78"&&!page.amountText.isTextOverflowing,"Long amount fits without losing its value");
                Check(page.paymentImage.sprite==page.paymentList.GetSpriteByIconKey(UIWithdrawalPanel.danaInfo),"DANA channel logo remains configurable");
                await PayPalCapture(folder,"English-DANA.png");PressStandard(Action(page));await UniTask.Delay(150,ignoreTimeScale:true);inspectedPage=null;
                Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input restores after other channels close");
                Check(SaveDataUtils.GameData.lastWithdrawTime==lastWithdraw,"No withdrawal request or saved withdrawal timestamp was changed");
                report.AppendLine("All images are raw Unity Play Mode Game view captures. Reference-State uses explicit $2.50 and a locally delivered success callback; no financial request was submitted. The awaiting-review bar is a state illustration, not a server review percentage.");
            }
            catch(Exception exception){Check(false,exception.ToString());Debug.LogException(exception);}
            finally
            {
                CloseRuntime();UIWithdrawalPendingPanel.ResetNetworkCallbackState();LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);
                report.AppendLine("Failures="+failures+" Completed="+DateTime.UtcNow.ToString("O"));File.WriteAllText(folder+"/inspection.txt",report.ToString());
            }
        }
    }
}
