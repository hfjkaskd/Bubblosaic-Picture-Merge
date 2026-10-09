using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewTransitionCore(string folder,Action<bool,string> check)
        {
            var p=TransitionBlock.Instance;int calls=0;p.Open(()=>calls++);await UniTask.Delay(420,ignoreTimeScale:true);check(p.gameObject.activeInHierarchy&&p._playingAnim,"Production transition opens and blocks input");await PayPalCapture(folder,"Reference-State.png");await UniTask.Delay(800,ignoreTimeScale:true);check(calls==1,"Open animation invokes original completion callback once");await PayPalCapture(folder,"Fully-Covered.png");p.Close();await UniTask.Delay(1200,ignoreTimeScale:true);check(!p.gameObject.activeSelf&&!p._playingAnim,"Close animation restores gameplay visibility");p.ShowTransition(()=>calls++);await UniTask.Delay(2200,ignoreTimeScale:true);check(calls==2&&!p.gameObject.activeSelf,"Combined production transition completes both phases");
        }
        static async UniTask ReviewTipCore(string folder,Action<bool,string> check)
        {
            var args=new UITeachTipsPage.InitParam{content=Localization.Tr("BUBBLE_GUIDE_MERGE"),posIdx=-1,heightValue=.62f,block=false,alpha=0};var p=(UITeachTipsPage)await UIModule.Instance.OpenPage(UIPageIds.UI_TeachTip,args);inspectedPage=p;await UniTask.DelayFrame(6);check(HitStandard(p.panel.GetComponent<Button>()),"Visible tutorial bubble owns native Button");check(!p.bg.raycastTarget,"Non-blocking guide leaves game input available");check(!string.IsNullOrEmpty(p.content.text),"Actual game tutorial message remains dynamic");await PayPalCapture(folder,"Reference-State.png");PressStandard(p.panel.GetComponent<Button>());await UniTask.DelayFrame(3);check(!UIModule.Instance.PageIsOpen(UIPageIds.UI_TeachTip),"Bubble click closes guide");
            args.block=true;args.alpha=120;args.posIdx=0;args.content="This is a longer tutorial message to verify that translated instructions remain readable.";p=(UITeachTipsPage)await UIModule.Instance.OpenPage(UIPageIds.UI_TeachTip,args);inspectedPage=p;await UniTask.DelayFrame(5);p.content.ForceMeshUpdate();check(!p.content.isTextOverflowing,"Long dynamic instructions fit the authored bubble");check(Vector3.Distance(p.panel.transform.position,p.panels[0].position)<.01f,"Named position anchor remains functional");PressStandard(p.bg.GetComponent<Button>());await UniTask.DelayFrame(3);check(!UIModule.Instance.PageIsOpen(UIPageIds.UI_TeachTip),"Blocking backdrop dismisses via native Button");
        }
        static async UniTask ReviewMaskCore(string folder,Action<bool,string> check)
        {
            var parent=(RealWithdrawPanel)await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);inspectedParent=parent;await UniTask.Delay(1200,ignoreTimeScale:true);var button=parent.transform.Find("Title/HistoryBtn").GetComponentInChildren<Button>();int closed=0;
            var args=new UITeachMaskPage.InitParam{target=button.gameObject,type=UITeachMaskPage.Type.Pos,width=120,height=120,alpha=145,showHand=true,shape=UITeachMaskPage.Shape.Circle,block=true,bgBlock=true,onClose=()=>closed++};
            var p=(UITeachMaskPage)await UIModule.Instance.OpenPage(UIPageIds.UI_TeachMask,args);inspectedPage=p;await UniTask.DelayFrame(8);check(HitStandard(button),"Highlight exposes the original native History button");check(p.finger.gameObject.activeSelf,"Configured glove is shown");await PayPalCapture(folder,"Reference-State.png");PressStandard(button);await UniTask.Delay(1300,ignoreTimeScale:true);check(closed==1&&!UIModule.Instance.PageIsOpen(UIPageIds.UI_TeachMask),"Native target click closes guide exactly once");check(UIModule.Instance.PageIsOpen(UIPageIds.WithdrawHistory),"History target retains its original navigation");UIModule.Instance.ClosePage(UIPageIds.WithdrawHistory);inspectedPage=null;
            await UniTask.Delay(800,ignoreTimeScale:true);args.showHand=false;args.clickCD=.25f;p=(UITeachMaskPage)await UIModule.Instance.OpenPage(UIPageIds.UI_TeachMask,args);inspectedPage=p;check(!p.finger.gameObject.activeSelf&&!button.interactable,"Hand visibility and configured input cooldown are honored");await UniTask.Delay(600,ignoreTimeScale:true);check(button.interactable,"Target interaction restores after cooldown");var bg=p.transform.Find("Dim0").GetComponent<Button>();PressStandard(bg);await UniTask.DelayFrame(3);check(!UIModule.Instance.PageIsOpen(UIPageIds.UI_TeachMask),"Native backdrop Button closes guide");
        }
        static async UniTask ReviewFocusCore(string folder,Action<bool,string> check)
        {
            var toolbar=UnityEngine.Object.FindObjectOfType<ToolbarView>();var button=toolbar.Hint.GetComponentInChildren<Button>();var p=(UITeachMaskFocusPage)await UIModule.Instance.OpenPage(UIPageIds.UI_TeachMaskFocus,new UITeachMaskFocusPage.FocusArgs{target=button.transform,width=180,height=180,alpha=160});inspectedPage=p;await UniTask.DelayFrame(8);check(HitStandard(button),"Highlighted target remains the real native game Button");await PayPalCapture(folder,"Reference-State.png");var saved=button.transform.localPosition;var before=p.maskParent.anchoredPosition;button.transform.localPosition=saved+Vector3.up*60;await UniTask.DelayFrame(5);check(Vector2.Distance(before,p.maskParent.anchoredPosition)>40,"Highlight follows target movement");button.transform.localPosition=saved;await UniTask.DelayFrame(3);CloseRuntime();check(!UIModule.Instance.PageIsOpen(UIPageIds.UI_TeachMaskFocus),"Highlight closes without changing gameplay state");
        }
        static async UniTask ReviewSwipeCore(string folder,Action<bool,string> check)
        {
            inspectedParent=await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);await UniTask.Delay(1200,ignoreTimeScale:true);
            var camera=Camera.main;int closed=0;var args=new UITeachFingerMovePage.MoveArgs{worldStartPos=camera.ScreenToWorldPoint(new Vector3(Screen.width*.68f,Screen.height*.27f,10)),worldEndPos=camera.ScreenToWorldPoint(new Vector3(Screen.width*.32f,Screen.height*.27f,10)),duration=2f,onClose=()=>closed++};
            var p=(UITeachFingerMovePage)await UIModule.Instance.OpenPage(UIPageIds.UI_TeachFinterMove,args);inspectedPage=p;await UniTask.Delay(550,ignoreTimeScale:true);var before=p.finger.anchoredPosition;check(p.finger.GetComponent<ApprovedContourImage>()!=null,"Approved hand outline is authored in the native prefab");check(!p.finger.GetComponent<Image>().raycastTarget,"Decorative hand does not intercept the underlying page");await PayPalCapture(folder,"Reference-State.png");await UniTask.Delay(500,ignoreTimeScale:true);check(Vector2.Distance(before,p.finger.anchoredPosition)>50,"Hand moves across supplied world-space endpoints");
            args.duration=1;var temp=args.worldStartPos;args.worldStartPos=args.worldEndPos;args.worldEndPos=temp;p.MoveFinger(args);await UniTask.Delay(500,ignoreTimeScale:true);await PayPalCapture(folder,"Reverse-Direction.png");CloseRuntime();await UniTask.DelayFrame(2);check(closed==1,"Closing the guide invokes the original callback once");
        }
    }
}
