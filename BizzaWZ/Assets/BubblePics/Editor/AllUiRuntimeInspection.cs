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
        static UIPageBase inspectedPage;
        static GameObject inspectedAux;
        static UIPageBase inspectedParent;
        static readonly System.Collections.Generic.List<GameObject> inspectedRows=new System.Collections.Generic.List<GameObject>();
        static void CloseRuntime()
        {
            foreach(var row in inspectedRows)if(row!=null)UnityEngine.Object.Destroy(row);inspectedRows.Clear();
            if(inspectedPage!=null&&UIModule.Instance!=null)UIModule.Instance.ClosePage(inspectedPage);
            inspectedPage=null;
            if(inspectedParent!=null){UIModule.Instance.ClosePage(inspectedParent);inspectedParent=null;}
            if(inspectedAux!=null){UnityEngine.Object.Destroy(inspectedAux);inspectedAux=null;}
            if(UIModule.Instance!=null){var parent=UIModule.Instance.GetPage<ServicePanel>();if(parent!=null)UIModule.Instance.ClosePage(parent);}
        }
        static async void OpenRuntime(string id,string output=null)
        {
            string validation=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation"));
            string folder=Path.GetFullPath(Path.Combine(validation,string.IsNullOrEmpty(output)?"AllUI-20260924/Runtime":output));
            if(!folder.StartsWith(validation+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Capture output must remain under Validation.");
            Directory.CreateDirectory(folder);
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null)throw new InvalidOperationException("Formal InitWZ gameplay must finish loading first.");
                CloseRuntime();await UniTask.Delay(300,ignoreTimeScale:true);
                bool fixture=false;int captureDelay=2500;
                switch(id)
                {
                    case "RealGamePanel":break;
                    case "WhiteWinPanel":inspectedPage=await UIModule.Instance.OpenPage<Action>(UIPageIds.WhiteWinPanel,null);fixture=true;break;
                    case "LosePanel":inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.LosePanel,LoseReason.Health,new LevelInfo());fixture=true;break;
                    case "ServiceSelectPanel":
                        var service=UIModule.Instance.GetPage<ServicePanel>();if(service==null)service=(ServicePanel)await UIModule.Instance.OpenPage(UIPageIds.ServicePanel);
                        inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.ServiceSelectPanel,service);break;
                    case "UIDailyTaskPage":inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.UI_DailyTaskPage);break;
                    case "WithdrawHistory":
                        var history=(WithdrawHistory)await UIModule.Instance.OpenPage(UIPageIds.WithdrawHistory);inspectedPage=history;
                        await UniTask.Delay(3500,ignoreTimeScale:true);
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder,"WithdrawHistory-empty.png"));await UniTask.Delay(300,ignoreTimeScale:true);
                        if(history.root.GetComponentsInChildren<WithdrawHistoryItem>().Length==0)
                        {
                            history.emptyHint.SetActive(false);
                            int[] states={1,3,4};
                            for(int rowIndex=0;rowIndex<3;rowIndex++)
                            {
                                var row=UnityEngine.Object.Instantiate(history.item,history.root,false);inspectedRows.Add(row.gameObject);row.gameObject.SetActive(true);
                                row.Init(new AccountModule.OceanShineWithdrawalRecord{Os_Pym="paypal",Os_Prc=rowIndex==0?2.5:rowIndex==1?.25:.10,Os_Ra="player@example.com",Os_Re="player@example.com",Os_Rn="",Os_Cp="",Os_Dat="2026-09-"+(24-rowIndex),Os_Sts=states[rowIndex],Os_Tsm=rowIndex==2?"Check account details":""});
                            }
                            LayoutRebuilder.ForceRebuildLayoutImmediate(history.rectTransform);fixture=true;
                        }
                        break;
                    case "AddPropPanel":inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.AddPropPanel,E_ItemType.GameProp_1);fixture=true;break;
                    // DailyTask presents the reward without the WinPanel close-time claim or level report.
                    // Do not click Claim while collecting this fixture.
                    case "GetRewardPanel":inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.GetRewardPanel,new ItemEntry{Type=E_ItemType.Gold,Count=1000},new ItemEntry{Type=E_ItemType.Dollar,Count=.25f},DoubleGetRewardPanel.E_UseScene.DailyTask,(Action<bool>)null);fixture=true;break;
                    case "ExchangeRatePanel":inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.ExchangeRatePanel,new ExchangeRateInfo{beforeBlance=10000,nowBlance=10000,beforeClash=2.5,nowClash=5,beforeRate=.00025,nowRate=.0005});fixture=true;break;
                    case "UIWithdrawalPendingPanel":inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPendingPanel,new UIWithdrawalPendingInfo(E_PayeeAccountType.Paypal,"$2.50"));fixture=true;break;
                    case "UIWithdrawalPanel":case "UIWithdrawalPanel-PIX":case "UIWithdrawalPanel-DANA":
                        string channel=id.EndsWith("-PIX")?UIWithdrawalPanel.pixInfo:id.EndsWith("-DANA")?UIWithdrawalPanel.danaInfo:UIWithdrawalPanel.paypalInfo;
                        var platform=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=channel,Os_Me=channel,Os_Mlt=.01};
                        inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalPanel,platform,new System.Collections.Generic.List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>{platform},E_WithdrawType.Real,(Action)null,true);if(id=="UIWithdrawalPanel")((UIWithdrawalPanel)inspectedPage).paypalMailInput.Text="player@example.com";fixture=true;break;
                    case "UIWithdrawalConfirmPanel":
                        var details=new WithDrawInfo{payType=E_PayeeAccountType.Paypal,withdrawType=E_WithdrawType.Real,type=PayeeAccountType.Email,Re="player@example.com",Ra="player@example.com",data=new AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform{Os_Cn=UIWithdrawalPanel.paypalInfo,Os_Me="PayPal"}};
                        inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.UIWithdrawalConfirmPanel,details);fixture=true;break;
                    case "WithdrawHintPanel":
                        inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);await UniTask.Delay(1000,ignoreTimeScale:true);((RealWithdrawPanel)inspectedPage).hintPanel.Init(25,24);fixture=true;break;
                    case "SlotRewardPanel":
                        inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.SlotPanel);var reward=((SlotPanel)inspectedPage).slotRewardPanel;reward.gameObject.SetActive(true);reward.Init(1000,.25f,"PileWealth");fixture=true;break;
                    case "SlotFAQPanel":
                        inspectedParent=await UIModule.Instance.OpenPage(UIPageIds.SlotPanel);await UniTask.Delay(500,ignoreTimeScale:true);
                        inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.SlotFAQPanel);break;
                    case "LoadingPanel":
                        inspectedPage=await UIModule.Instance.OpenPage(new PageId("LoadingPanel"));BizzaEventSystem.Emit<float>(EventDefine.Frame.LoadingProgress,.68f);fixture=true;break;
                    case "UITeachMaskFocusPage":
                        var focus=UnityEngine.Object.FindObjectOfType<ToolbarView>().Hint.GetComponentInChildren<Button>();
                        inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.UI_TeachMaskFocus,new UITeachMaskFocusPage.FocusArgs{target=focus.transform,width=160,height=160,alpha=170});fixture=true;break;
                    case "UI_TeachTip":
                        inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.UI_TeachTip,new UITeachTipsPage.InitParam{content=Localization.Tr("BUBBLE_GUIDE_MERGE"),posIdx=-1,heightValue=.62f,block=false,alpha=0});fixture=true;break;
                    case "UI_TeachMask":
                        inspectedParent=await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);await UniTask.Delay(1000,ignoreTimeScale:true);
                        var button=inspectedParent.transform.Find("Title/HistoryBtn").GetComponentInChildren<Button>();
                        inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.UI_TeachMask,new UITeachMaskPage.InitParam{target=button.gameObject,type=UITeachMaskPage.Type.Pos,width=112,height=112,alpha=170,showHand=true,shape=UITeachMaskPage.Shape.Circle,block=false,bgBlock=true});fixture=true;break;
                    case "UI_TeachFinterMove":
                        inspectedParent=await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);await UniTask.Delay(1000,ignoreTimeScale:true);
                        inspectedPage=await UIModule.Instance.OpenPage(UIPageIds.UI_TeachFinterMove,new UITeachFingerMovePage.MoveArgs{worldStartPos=Camera.main.ScreenToWorldPoint(new Vector3(Screen.width*.65f,Screen.height*.25f,10)),worldEndPos=Camera.main.ScreenToWorldPoint(new Vector3(Screen.width*.65f,Screen.height*.4f,10)),duration=1.5f});fixture=true;break;
                    case "CommonConfirmTipsPanel":
                        inspectedParent=await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);await UniTask.Delay(1000,ignoreTimeScale:true);
                        inspectedAux=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BizzaWZ/Final/FunctionTools/CommonPublic/UI/CommonConfirmTipsPanel.prefab"),UIModule.Instance.UICanvas.transform,false);
                        inspectedAux.GetComponent<CommonConfirmTipsPanel>().OnOpen(new CommonConfirmTipsPanel.Args{des="HTTPNetworkProblem",isLanguage=true});fixture=true;break;
                    case "TransitionBlock":
                        var transition=TransitionBlock.Instance;transition.Open(null);captureDelay=500;inspectedAux=null;fixture=true;break;
                    case "BroadCastBar":
                        BroadcastBarController.Instance.ShowMessage(new ItemEntry{Type=E_ItemType.Gold,Count=250},new ItemEntry{Type=E_ItemType.Dollar,Count=.05f});captureDelay=Mathf.RoundToInt((BroadcastBarController.Instance.animDuration+BroadcastBarController.Instance.stayDuration*.5f)*1000);fixture=true;break;
                    case "UIBizzaAAA":
                        inspectedPage=await UIModule.Instance.OpenPage(new PageId(id));captureDelay=32500;break;
                    case "PausePanel":case "RealWithdrawPanel":case "FAQPanel":case "WithdrawDanPanel":case "FakeWithdrawPanel":case "DailyWithdrawPanel":case "DailyMissionPanel":case "NewbieGiftPage":case "ServicePanel":case "StarRatingPopup":case "SlotPanel":
                        inspectedPage=await UIModule.Instance.OpenPage(new PageId(id));break;
                    default:throw new InvalidOperationException("Typed arguments required before capturing: "+id);
                }
                await UniTask.Delay(captureDelay,ignoreTimeScale:true);
                if(inspectedPage==null&&inspectedAux==null&&id!="RealGamePanel"&&id!="TransitionBlock"&&id!="BroadCastBar")throw new InvalidOperationException("Requested page did not open: "+id);
                var report=new StringBuilder();report.AppendLine("Page="+id+" Capture="+(fixture?"runtime component state, explicit arguments":"formal runtime page, current live data"));report.AppendLine("Resolution="+Screen.width+"x"+Screen.height);
                GameObject root=id=="BroadCastBar"?BroadcastBarController.Instance.gameObject:inspectedAux!=null?inspectedAux:inspectedPage!=null?inspectedPage.gameObject:BizzaGameplayBridge.Page.gameObject;
                report.AppendLine("Root="+root.name+" active="+root.activeInHierarchy+" scale="+root.transform.lossyScale);
                InspectAspectRuntime(root, report);
                foreach(var rt in root.GetComponentsInChildren<RectTransform>(true))report.AppendLine("RECT "+AnimationUtility.CalculateTransformPath(rt,root.transform)+" active="+rt.gameObject.activeInHierarchy+" pos="+rt.position+" anchored="+rt.anchoredPosition+" size="+rt.rect.size);
                foreach(var cg in root.GetComponentsInParent<CanvasGroup>())report.AppendLine("CANVASGROUP "+cg.name+" alpha="+cg.alpha);
                foreach(var button in root.GetComponentsInChildren<Button>())report.AppendLine("BUTTON "+button.name+" interactable="+button.IsInteractable()+" graphic="+(button.targetGraphic!=null));
                foreach(var text in root.GetComponentsInChildren<TMP_Text>())report.AppendLine("TEXT "+text.name+"="+text.text);
                foreach(var im in root.GetComponentsInChildren<Image>())report.AppendLine("IMAGE "+AnimationUtility.CalculateTransformPath(im.transform,root.transform)+"="+(im.sprite!=null?im.sprite.name:"null")+" type="+(int)im.type+" tint="+im.color);
                if(!root.activeInHierarchy)throw new InvalidOperationException("Requested page closed before capture: "+id);
                string capturePath=Path.Combine(folder,id+".png");DateTime captureRequested=DateTime.UtcNow;
                ScreenCapture.CaptureScreenshot(capturePath);File.WriteAllText(Path.Combine(folder,id+".txt"),report.ToString());
                for(int attempt=0;attempt<30&&(!File.Exists(capturePath)||File.GetLastWriteTimeUtc(capturePath)<captureRequested);attempt++)await UniTask.Delay(100,ignoreTimeScale:true);
                if(!File.Exists(capturePath)||File.GetLastWriteTimeUtc(capturePath)<captureRequested)throw new InvalidOperationException("Unity did not write a fresh Game view screenshot. Keep a rendering Game view open; do not treat this result as captured.");
                if(id=="TransitionBlock")TransitionBlock.Instance.Close();
                File.WriteAllText(Path.Combine(Folder,"runtime-result.txt"),"CAPTURED "+id+" "+DateTime.UtcNow.ToString("O"));
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(Folder,"runtime-result.txt"),ex.ToString());Debug.LogException(ex);}
        }
    }
}
