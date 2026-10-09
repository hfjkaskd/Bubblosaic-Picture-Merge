using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async void ReviewSequential(string id)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/SequentialUI-20260928/"+id));Directory.CreateDirectory(folder);
            var report=new StringBuilder();int failures=0;string language=LanguageUtils.SelectedLanguage,locale=Localization.CurrentLocale;
            File.WriteAllText(folder+"/inspection.txt","RUNNING "+DateTime.UtcNow.ToString("O"));
            void Check(bool ok,string name){report.AppendLine((ok?"PASS ":"FAIL ")+name);if(!ok)failures++;}
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null)throw new InvalidOperationException("Production InitWZ startup must finish first.");
                CloseRuntime();LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                if(id=="WithdrawHintPanel")
                {
                    var page=(RealWithdrawPanel)await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);inspectedPage=page;await UniTask.Delay(1800,ignoreTimeScale:true);
                    var hint=page.hintPanel;hint.Init(25,24);await UniTask.DelayFrame(5);
                    var button=hint.transform.Find("Confirm").GetComponent<Button>();var fill=hint.transform.Find("Track/Fill").GetComponent<Image>();
                    var count=hint.transform.Find("ProgressCount").GetComponent<TMP_Text>();var requirement=hint.transform.Find("Requirement").GetComponent<TMP_Text>();
                    Check(count.text=="24 / 25","Level progress uses current and target levels");Check(Mathf.Abs(fill.fillAmount-.96f)<.001f,"Level progress is 96 percent");
                    Check(requirement.text.Contains("25"),"Target level remains dynamic");Check(HitStandard(button),"Visible Got it owns standard Button and receives raycasts");
                    foreach(var name in new[]{"Panel","Detail","Track","Confirm"})
                    {
                        var rect=PayPalRect((RectTransform)hint.transform.Find(name));report.AppendLine("GEOMETRY "+name+"="+rect);
                        Check(rect.xMin>=0&&rect.xMax<=852&&rect.yMin>=0&&rect.yMax<=1846,"Visible bounds "+name);
                    }
                    await PayPalCapture(folder,"Reference-State.png");PressStandard(button);await UniTask.DelayFrame(3);
                    Check(!hint.gameObject.activeSelf&&page.gameObject.activeInHierarchy,"Got it closes only the hint and retains withdrawal page");
                    hint.Init(.4f,.5f);await UniTask.DelayFrame(3);Check(Mathf.Abs(fill.fillAmount-.2f)<.001f,"Money overload preserves remaining-amount semantics");await PayPalCapture(folder,"Amount-State.png");
                    hint.Init(0,0);Check(!float.IsNaN(fill.fillAmount)&&fill.fillAmount==0,"Zero requirement has finite progress");
                    hint.Init(25,30);Check(fill.fillAmount==1&&count.text=="25 / 25","Completed progress is clamped");
                    foreach(string lang in new[]{"pt_BR","id","zh_CN"})
                    {
                        Localization.SetLocale(lang);hint.Init(25,24);await UniTask.DelayFrame(5);
                        foreach(var text in hint.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Localized text fits "+lang+"/"+text.name);}
                        await PayPalCapture(folder,lang+".png");
                    }
                    report.AppendLine("Reference-State uses a view-only 24/25 requirement; background is the live production withdrawal page. No payout request, balance mutation, claim or withdrawal-counter change is made by this review.");
                }
                else if(id=="DailyWithdrawPanel")
                {
                    inspectedParent=await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);await UniTask.Delay(1200,ignoreTimeScale:true);
                    var page=(DailyWithdrawPanel)await UIModule.Instance.OpenPage(UIPageIds.DailyWithdrawPanel);inspectedPage=page;await UniTask.Delay(1800,ignoreTimeScale:true);
                    Check(page.gameObject.activeInHierarchy&&!string.IsNullOrEmpty(page.clashTxt.text),"Daily balance arrives through production request");await PayPalCapture(folder,"Live-State.png");
                    page.balanceTxt.text="10,000 coins";page.withdrawalTxt.text="≈ $2.50";page.clashTxt.text="$2.50";LayoutRebuilder.ForceRebuildLayoutImmediate(page.root);await UniTask.DelayFrame(5);await PayPalCapture(folder,"Reference-State.png");
                    var close=page.transform.Find("Close").GetComponent<Button>();var withdraw=page.transform.Find("Withdraw").GetComponent<Button>();
                    Check(HitStandard(close)&&HitStandard(withdraw),"Close and Withdraw own reachable standard Buttons");Check(page.transform.Find("Withdraw/WithdrawLabel").GetComponent<TMP_Text>().text=="Withdraw","English Withdraw caption is localized");
                    foreach(var name in new[]{"Panel","Detail","Withdraw","Close"}){var r=PayPalRect((RectTransform)page.transform.Find(name));Check(r.xMin>=0&&r.xMax<=852&&r.yMin>=0&&r.yMax<=1846,"Visible bounds "+name);report.AppendLine("GEOMETRY "+name+"="+r);}
                    foreach(string lang in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(lang);await UniTask.DelayFrame(5);foreach(var text in page.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Localized text fits "+lang+"/"+text.name);}await PayPalCapture(folder,lang+".png");}
                    PressStandard(close);await UniTask.DelayFrame(3);Check(!UIModule.Instance.PageIsOpen(UIPageIds.DailyWithdrawPanel),"Close dismisses daily prompt");CloseRuntime();
                    page=(DailyWithdrawPanel)await UIModule.Instance.OpenPage(UIPageIds.DailyWithdrawPanel);inspectedPage=page;await UniTask.Delay(1400,ignoreTimeScale:true);PressStandard(page.transform.Find("Withdraw").GetComponent<Button>());await UniTask.Delay(700,ignoreTimeScale:true);
                    Check(!UIModule.Instance.PageIsOpen(UIPageIds.DailyWithdrawPanel)&&UIModule.Instance.PageIsOpen(UIPageIds.RealWithdrawPanel),"Withdraw navigates to original withdrawal page without submitting money");inspectedPage=UIModule.Instance.GetPage<RealWithdrawPanel>();
                    report.AppendLine("Reference-State assigns only displayed labels to 10,000 coins and $2.50. Live-State retains the production response. No payout or reward request was submitted.");
                }
                else if(id=="ExchangeRatePanel")
                {
                    inspectedParent=await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);await UniTask.Delay(1200,ignoreTimeScale:true);
                    var info=new ExchangeRateInfo{beforeBlance=10000,nowBlance=10000,beforeClash=2.5,nowClash=5,beforeRate=.00025,nowRate=.0005};
                    var page=(ExchangeRatePanel)await UIModule.Instance.OpenPage(UIPageIds.ExchangeRatePanel,info);inspectedPage=page;await UniTask.DelayFrame(5);
                    Check(page.beforeClashText.text!=page.nowClashText.text,"Before and after amounts use separate supplied values");await PayPalCapture(folder,"CurrentLocale-State.png");
                    page.beforeBlanceText.text=page.nowBlanceText.text="10,000 coins";page.beforeClashText.text="≈ $2.50";page.nowClashText.text="≈ $5.00";page.transform.Find("LevelValue").GetComponent<TMP_Text>().text="LEVEL 25";await UniTask.DelayFrame(5);await PayPalCapture(folder,"Reference-State.png");
                    foreach(var name in new[]{"Check","Close"})Check(HitStandard(page.transform.Find(name).GetComponent<Button>()),"Reachable standard Button "+name);
                    foreach(string lang in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(lang);await UniTask.DelayFrame(5);foreach(var text in page.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Localized text fits "+lang+"/"+text.name);}await PayPalCapture(folder,lang+".png");}
                    PressStandard(page.transform.Find("Close").GetComponent<Button>());await UniTask.DelayFrame(3);Check(!UIModule.Instance.PageIsOpen(UIPageIds.ExchangeRatePanel),"Close dismisses rate prompt");CloseRuntime();
                    page=(ExchangeRatePanel)await UIModule.Instance.OpenPage(UIPageIds.ExchangeRatePanel,info);inspectedPage=page;await UniTask.DelayFrame(3);PressStandard(page.transform.Find("Check").GetComponent<Button>());await UniTask.Delay(700,ignoreTimeScale:true);
                    Check(!UIModule.Instance.PageIsOpen(UIPageIds.ExchangeRatePanel)&&UIModule.Instance.PageIsOpen(UIPageIds.RealWithdrawPanel),"Check navigates to original withdrawal page");inspectedPage=UIModule.Instance.GetPage<RealWithdrawPanel>();
                    report.AppendLine("Review passes view-only rate-change values through the production page API. Reference-State changes displayed strings only to align the artwork. Actual account rates, balance and level are unchanged.");
                }
                else if(id=="UIWithdrawalPanel-PIX")await ReviewPixCore(folder,Check);
                else if(id=="UIWithdrawalPanel-DANA")await ReviewDanaCore(folder,Check);
                else if(id=="WithdrawalSingleMethod")await ReviewSingleMethodCore(folder,Check);
                else if(id=="PausePanel")await ReviewPauseCore(folder,Check);
                else if(id=="LosePanel")await ReviewLoseCore(folder,Check);
                else if(id=="WhiteWinPanel")await ReviewVictoryCore(folder,Check);
                else if(id=="UIDailyTaskPage")await ReviewTaskCore(folder,Check);
                else if(id=="DailyMissionPanel")await ReviewDailyMissionCore(folder,Check);
                else if(id=="AddPropPanel")await ReviewAddPropCore(folder,Check);
                else if(id=="GetRewardPanel")await ReviewRewardCore(folder,Check);
                else if(id=="GetRewardPresentation")await ReviewRewardPresentationCore(folder,Check);
                else if(id=="RewardDisplay")await ReviewRewardDisplayCore(folder,Check);
                else if(id=="WithdrawalDisplay")await ReviewWithdrawalDisplayCore(folder,Check);
                else if(id=="PageAspect")await ReviewPageAspectCore(folder,Check);
                else if(id=="RewardFxMaterials")await ReviewRewardFxMaterialsCore(folder,Check);
                else if(id=="SlotRewardFlight")await ReviewSlotRewardFlightCore(folder,Check);
                else if(id=="NewbieGiftPage")await ReviewNewbieCore(folder,Check);
                else if(id=="CommonConfirmTipsPanel")await ReviewConfirmCore(folder,Check);
                else if(id=="ServicePanel")await ReviewServiceCore(folder,Check);
                else if(id=="ServiceSelectPanel")await ReviewQuickReplyCore(folder,Check);
                else if(id=="StarRatingPopup")await ReviewRatingCore(folder,Check);
                else if(id=="SlotPanel")await ReviewSlotCore(folder,Check);
                else if(id=="SlotAnimationAlignment")await ReviewSlotAnimationCore(folder,Check);
                else if(id=="SlotFAQPanel")await ReviewSlotGuideCore(folder,Check);
                else if(id=="SlotRewardPanel")await ReviewSlotRewardCore(folder,Check);
                else if(id=="UI_TeachFinterMove")await ReviewSwipeCore(folder,Check);
                else if(id=="UITeachMaskFocusPage")await ReviewFocusCore(folder,Check);
                else if(id=="UI_TeachMask")await ReviewMaskCore(folder,Check);
                else if(id=="UI_TeachTip")await ReviewTipCore(folder,Check);
                else if(id=="TransitionBlock")await ReviewTransitionCore(folder,Check);
                else if(id=="BroadCastBar")await ReviewBroadcastCore(folder,Check);
                else if(id=="UIBizzaAAA")await ReviewFloatChestCore(folder,Check);
                else if(id=="LoadingPanel")
                {
                    var page=(LoadingPanel)await UIModule.Instance.OpenPage(new PageId("LoadingPanel"));inspectedPage=page;await UniTask.DelayFrame(4);
                    var visual=page.GetComponentInChildren<SplashPage>();var data=new SerializedObject(visual);var fill=(RectTransform)data.FindProperty("_progressFill").objectReferenceValue;var label=(TMP_Text)data.FindProperty("_progressLabel").objectReferenceValue;float fullWidth=data.FindProperty("_progressWidth").floatValue;
                    foreach(float value in new[]{-.1f,0f,.2f,.5f,.68f,1f,1.2f})
                    {
                        BizzaEventSystem.Emit<float>(EventDefine.Frame.LoadingProgress,value);await UniTask.DelayFrame(5);float expected=Mathf.Clamp01(value);
                        Check(Mathf.Abs(fill.sizeDelta.x-fullWidth*expected)<.01f,"Loading width follows production event "+value);Check(label.text==Mathf.RoundToInt(expected*100)+"%","Loading percent follows production event "+value);
                        if(value==.68f)await PayPalCapture(folder,"Reference-State.png");else if(value>=0&&value<=1)await PayPalCapture(folder,"Progress-"+Mathf.RoundToInt(value*100)+".png");
                    }
                    foreach(string lang in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(lang);await UniTask.DelayFrame(4);var loading=visual.GetComponentInChildren<CoralLocalizedLabel>();Check(loading!=null,"Localized loading label remains authored "+lang);}
                    CloseRuntime();await UniTask.DelayFrame(4);Check(BizzaGameplayBridge.Page!=null&&BizzaGameplayBridge.Page.gameObject.activeInHierarchy,"Closing loading screen retains original gameplay");
                }
                else throw new InvalidOperationException("Review not implemented yet: "+id);
            }
            catch(Exception e){Check(false,e.ToString());Debug.LogException(e);}
            finally
            {
                try{CloseRuntime();LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);}catch(Exception cleanup){Check(false,"Cleanup: "+cleanup);}
                report.AppendLine("Failures="+failures+" Completed="+DateTime.UtcNow.ToString("O"));File.WriteAllText(folder+"/inspection.txt",report.ToString());
            }
        }
    }
}
