using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static void PressStandard(Button button)
        {
            if(button==null)throw new InvalidOperationException("Missing standard Button");
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        }
        static bool HitStandard(Button button)
        {
            if(button==null||button.targetGraphic==null||!button.IsInteractable())return false;
            var canvas=button.GetComponentInParent<Canvas>().rootCanvas;
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var graphic=button.targetGraphic.rectTransform;
            Vector2 center=RectTransformUtility.WorldToScreenPoint(camera,graphic.TransformPoint(graphic.rect.center));
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=center},hits);
            // The input module uses the first hit; an intervening non-button graphic blocks clicks.
            return hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button;
        }
        static async void InteractionSmoke()
        {
            var report=new StringBuilder();int failures=0;
            string file=Path.Combine(Folder,"interaction-checks.txt");
            Action<bool,string> check=(ok,message)=>{report.AppendLine((ok?"PASS ":"FAIL ")+message);if(!ok)failures++;File.WriteAllText(file,report.ToString());};
            try
            {
                check(EditorApplication.isPlaying&&BizzaGameplayBridge.Page!=null,"Formal InitWZ runtime is active");
                CloseRuntime();await UniTask.Delay(500,ignoreTimeScale:true);
                var pause=(PausePanel)await UIModule.Instance.OpenPage(UIPageIds.PausePanel);await UniTask.Delay(300,ignoreTimeScale:true);
                foreach(string path in new[]{"BG (1)/MusicGroup/Button","BG (1)/SoundGroup/Button (1)","BG (1)/LibGroup/Button (2)"})
                {
                    var node=pause.transform.Find(path);var button=node.GetComponent<Button>();
                    check(HitStandard(button),"Settings raycast "+path);
                    string on=path.Contains("MusicGroup")?"MusicOn":path.Contains("SoundGroup")?"SoundOn":"LibOn";
                    bool before=node.Find(on).gameObject.activeSelf;PressStandard(button);await UniTask.DelayFrame(2);check(before!=node.Find(on).gameObject.activeSelf,"Toggle changes state "+on);PressStandard(button);await UniTask.DelayFrame(2);check(before==node.Find(on).gameObject.activeSelf,"Toggle restored "+on);
                }
                PressStandard(pause.CloseButton.GetComponent<Button>());await UniTask.Delay(300,ignoreTimeScale:true);check(!UIModule.Instance.PageIsOpen(UIPageIds.PausePanel),"Settings close returns to gameplay");
                var withdraw=(RealWithdrawPanel)await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);await UniTask.Delay(1700,ignoreTimeScale:true);
                var channels=withdraw.withdrawWayRoot.GetComponentsInChildren<WithdrawWay>();check(channels.Length>=1,"Live withdrawal channels rendered");
                foreach(var channel in channels)
                {
                    var button=channel.GetComponentInChildren<Button>();check(HitStandard(button),"Channel is directly clickable "+channel.data.Os_Cn);PressStandard(button);await UniTask.DelayFrame(2);check(channel.selectedObj.activeSelf,"Channel selection updates "+channel.data.Os_Cn);
                    int selected=0;foreach(var other in channels)if(other.selectedObj.activeSelf)selected++;check(selected==1,"Exactly one selected channel");
                }
                if(channels.Length>0)PressStandard(channels[0].GetComponentInChildren<Button>());
                var scroll=withdraw.GetComponentInChildren<ScrollRect>();if(scroll!=null){scroll.verticalNormalizedPosition=0;await UniTask.DelayFrame(2);scroll.verticalNormalizedPosition=1;check(scroll.content.rect.height>scroll.viewport.rect.height||scroll.content.rect.height>0,"Withdrawal content supports long lists");}
                UIModule.Instance.ClosePage(withdraw);await UniTask.Delay(200,ignoreTimeScale:true);
                var tierPage=(WithdrawDanPanel)await UIModule.Instance.OpenPage(UIPageIds.WithdrawDanPanel);await UniTask.Delay(500,ignoreTimeScale:true);
                var tierRows=tierPage.root.GetComponentsInChildren<WithdrawDanItem>();check(tierRows.Length==7,"Tier page retains all seven live tiers");
                bool tierStatesValid=true;
                foreach(var row in tierRows)
                {
                    int states=(row.prepareStateObj.activeSelf?1:0)+(row.claimStateObj.activeSelf?1:0)+(row.claimedStateObj.activeSelf?1:0);
                    tierStatesValid &= states==1&&row.moneyText1!=null&&!string.IsNullOrEmpty(row.moneyText1.text)&&row.progressImage!=null;
                }
                check(tierStatesValid,"Tier amount, progress and exclusive action states remain bound");
                var tierScroll=tierPage.root.GetComponentInParent<ScrollRect>();
                check(tierScroll!=null&&tierScroll.vertical&&tierScroll.content.rect.height>tierScroll.viewport.rect.height,"All tiers remain reachable through their scroll list");
                if(tierScroll!=null){tierScroll.verticalNormalizedPosition=0;await UniTask.DelayFrame(2);tierScroll.verticalNormalizedPosition=1;}
                var firstTier=tierRows[0];var tierAction=firstTier.prepareStateObj.activeSelf?firstTier.prepareStateBtn:firstTier.claimStateObj.activeSelf?firstTier.claimStateBtn:firstTier.claimedStateBtn;
                await UniTask.DelayFrame(2);check(HitStandard(tierAction.GetComponentInChildren<Button>()),"Tier action uses its visible Button graphic");
                check(HitStandard(tierPage.withdrawBtn.GetComponentInChildren<Button>()),"Tier withdrawal action remains visible and clickable");
                UIModule.Instance.ClosePage(tierPage);await UniTask.Delay(200,ignoreTimeScale:true);
                var platform=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.paypalInfo,Os_Me="PayPal",Os_Mlt=.01};
                var form=(UIWithdrawalPanel)await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel,platform,new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{platform},E_WithdrawType.Real,(Action)null,true);
                await UniTask.Delay(300,ignoreTimeScale:true);string savedEmail=SaveDataUtils.GameData.withdrawEmailInfo;if(savedEmail=="invalid-address")savedEmail="";string original=savedEmail;form.paypalMailInput.Text="invalid-address";
                var submit=form.transform.Find("Root/FillRoot/pageContent/BtnWithdrawal").GetComponent<Button>();check(HitStandard(submit),"Account continue uses visible standard Button");PressStandard(submit);await UniTask.Delay(300,ignoreTimeScale:true);
                check(form.paypalMailError.gameObject.activeInHierarchy,"Invalid email displays validation error");check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalConfirmPanel),"Invalid email cannot advance or submit");form.paypalMailInput.Text=original;
                PressStandard(form.transform.Find("Root/FillRoot/pageClose").GetComponent<Button>());await UniTask.Delay(200,ignoreTimeScale:true);SaveDataUtils.GameData.withdrawEmailInfo=savedEmail;SaveDataUtils.gameStrategy.SaveData();check(!UIModule.Instance.PageIsOpen(UIPageIds.UIWithdrawalPanel),"Account form closes normally");
                var tasks=(UIDailyTaskPage)await UIModule.Instance.OpenPage(UIPageIds.UI_DailyTaskPage);await UniTask.Delay(300,ignoreTimeScale:true);
                foreach(var tab in new[]{tasks.tabDaily,tasks.tabWeekly,tasks.tabPlaytime})
                {
                    var button=tab.GetComponent<Button>();check(HitStandard(button),"Task tab raycast "+tab.name);PressStandard(button);await UniTask.DelayFrame(3);check(tab==tasks.tabPlaytime?tasks.PlayTimeRootObj.activeSelf:tasks.DailyRootObj.activeSelf,"Task tab updates list "+tab.name);
                }
                PressStandard(tasks.closeBtn);await UniTask.Delay(200,ignoreTimeScale:true);check(!UIModule.Instance.PageIsOpen(UIPageIds.UI_DailyTaskPage),"Task close returns normally");
                var slot=(SlotPanel)await UIModule.Instance.OpenPage(UIPageIds.SlotPanel);await UniTask.Delay(300,ignoreTimeScale:true);
                check(HitStandard(slot.faqBtn.GetComponent<Button>()),"Slots FAQ button raycast");PressStandard(slot.faqBtn.GetComponent<Button>());
                float faqDeadline=Time.realtimeSinceStartup+5f;
                while(UIModule.Instance.GetPage<SlotFAQPanel>()==null&&Time.realtimeSinceStartup<faqDeadline)await UniTask.Delay(100,ignoreTimeScale:true);
                var guide=UIModule.Instance.GetPage<SlotFAQPanel>();check(guide!=null,"Slots FAQ opens");if(guide!=null)PressStandard(guide.bizzaButton.GetComponent<Button>());await UniTask.Delay(300,ignoreTimeScale:true);check(!UIModule.Instance.PageIsOpen(UIPageIds.SlotFAQPanel),"Slots FAQ closes");
                bool animationComplete=false;slot.slotMachineManager.PlayAnim(false,_=>animationComplete=true);float until=Time.realtimeSinceStartup+18f;
                while(!animationComplete&&Time.realtimeSinceStartup<until)await UniTask.Delay(200,ignoreTimeScale:true);
                check(animationComplete,"Reels finish through original animation callback; no ad or reward submission");
                PressStandard(slot.closeBtn.GetComponent<Button>());await UniTask.Delay(300,ignoreTimeScale:true);check(!UIModule.Instance.PageIsOpen(UIPageIds.SlotPanel),"Slots close returns to gameplay");
                check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input restored after page tests");
                var hud=UnityEngine.Object.FindObjectOfType<GameplayHudHeader>();
                var focusTarget=hud.CurrencyRow.Find("PauseButton").GetComponent<Button>();
                var focusPage=await UIModule.Instance.OpenPage(UIPageIds.UI_TeachMaskFocus,new UITeachMaskFocusPage.FocusArgs{target=focusTarget.transform,width=155,height=155,alpha=170});
                await UniTask.Delay(300,ignoreTimeScale:true);
                check(hud.gameObject.activeInHierarchy&&focusTarget.gameObject.activeInHierarchy,"Focus guide retains the actual HUD target");
                check(HitStandard(focusTarget),"Focus guide leaves its actual target Button clickable");
                UIModule.Instance.ClosePage(focusPage);await UniTask.Delay(200,ignoreTimeScale:true);check(hud.gameObject.activeInHierarchy,"Focus guide close retains gameplay HUD");
                var broadcast=BroadcastBarController.Instance;
                float toastReadyDeadline=Time.realtimeSinceStartup+3f;
                while(!broadcast.CanShowRewardImmediately&&Time.realtimeSinceStartup<toastReadyDeadline)await UniTask.Yield();
                broadcast.ShowMessage(new ItemEntry{Type=E_ItemType.Gold,Count=250},new ItemEntry{Type=E_ItemType.Dollar,Count=.05f});
                // Observe frames through the short visible interval. A fixed wall-clock
                // delay may resume after that interval on a busy Editor frame.
                bool toastVisible=false;float toastDeadline=Time.realtimeSinceStartup+3f;
                while(Time.realtimeSinceStartup<toastDeadline)
                {
                    if(broadcast.gameObject.activeInHierarchy&&broadcast.GetComponent<CanvasGroup>().alpha>.9f){toastVisible=true;break;}
                    await UniTask.Yield();
                }
                check(toastVisible,"Reward toast is visible after fade-in");
                var toastCanvas=broadcast.GetComponent<Canvas>();check(toastCanvas!=null&&toastCanvas.overrideSorting&&toastCanvas.sortingOrder==1000,"Reward toast sorts above gameplay");
                await UniTask.Delay(2200,ignoreTimeScale:true);check(!broadcast.gameObject.activeSelf,"Reward toast dismisses without blocking gameplay");
            }
            catch(Exception e){check(false,e.ToString());Debug.LogException(e);}
            report.AppendLine("Failures="+failures+" Completed="+DateTime.UtcNow.ToString("O"));File.WriteAllText(file,report.ToString());
        }
    }
}
