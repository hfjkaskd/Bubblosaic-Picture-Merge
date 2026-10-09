using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static void InspectAspectRuntime(GameObject page, System.Text.StringBuilder report)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var fit in page.GetComponentsInChildren<PageContentFit>())
            {
                var content = fit.Content;
                if (content == null) throw new InvalidOperationException("Missing fitted content.");
                var bounds = PayPalRect(content);
                bool valid = Mathf.Abs(content.localScale.x - content.localScale.y) < .0001f &&
                    bounds.xMin >= -1 && bounds.xMax <= 853 && bounds.yMin >= -1 && bounds.yMax <= 1847;
                report.AppendLine((valid ? "PASS" : "FAIL") + " ASPECT fitted content " + bounds);
                if (!valid) throw new InvalidOperationException("Content is stretched or clipped: " + page.name);
            }
            foreach (var extension in page.GetComponentsInChildren<BackdropEdgeExtension>())
            {
                var bg = extension.transform.parent.Find("BackgroundUnderlay").GetComponent<Image>();
                var bounds = PayPalRect(bg.rectTransform);
                var mesh = bg.canvasRenderer.GetMesh();
                bool covers = bounds.xMin <= 1 && bounds.xMax >= 851 && bounds.yMin <= 1 && bounds.yMax >= 1845 &&
                    mesh != null && Mathf.Abs(mesh.bounds.size.x - bg.rectTransform.rect.width) < 1;
                report.AppendLine((covers ? "PASS" : "FAIL") + " ASPECT background covers viewport " + bounds);
                if (!covers) throw new InvalidOperationException("Background does not cover the screen: " + page.name);
            }
            foreach (var button in page.GetComponentsInChildren<Button>())
            {
                var r = (RectTransform)button.transform;
                report.AppendLine("ASPECT BUTTON " + UnityEditor.AnimationUtility.CalculateTransformPath(r,page.transform) +
                    " authored=" + r.rect.size + " reachable=" + HitStandard(button));
            }
        }

        static async UniTask ReviewPageAspectCore(string folder, Action<bool,string> check)
        {
            float balance = ItemUtils.GetItemCount(E_ItemType.Dollar);
            var reward = (GetRewardPanel)await UIModule.Instance.OpenPage(UIPageIds.GetRewardPanel,
                new ItemEntry { Type=E_ItemType.Gold, Count=1000 }, new ItemEntry { Type=E_ItemType.Dollar, Count=12.60f },
                DoubleGetRewardPanel.E_UseScene.DailyTask, (Action<bool>)null);
            inspectedPage = reward;
            await UniTask.Delay(400,ignoreTimeScale:true);
            check(!reward.closeBtn.gameObject.activeSelf,"Reward collect action retains its delay");
            await UniTask.Delay(3200,ignoreTimeScale:true);
            CheckPageAspect(reward.gameObject,check);
            check(HitStandard(reward.claimBtn.GetComponent<Button>()) && HitStandard(reward.closeBtn.GetComponent<Button>()),"Reward actions reachable");
            await PayPalCapture(folder,"GetRewardPanel-"+Screen.width+"x"+Screen.height+".png");
            CloseRuntime();
            var withdraw = (FakeWithdrawPanel)await UIModule.Instance.OpenPage(UIPageIds.FakeWithdrawPanel);
            inspectedPage = withdraw;
            for(int i=0;i<100 && withdraw.GetComponentsInChildren<NewPlayerPayoutOption>().Length==0;i++)await UniTask.Delay(100,ignoreTimeScale:true);
            await UniTask.Delay(500,ignoreTimeScale:true);
            check(withdraw.gameObject.activeInHierarchy,"Withdrawal production response available");
            CheckPageAspect(withdraw.gameObject,check);
            PressStandard(withdraw.items[1].btn.GetComponent<Button>());
            await UniTask.Delay(500,ignoreTimeScale:true);
            check(withdraw.curSelectIndex==1 && withdraw.items[1].selectObj.activeSelf,"Amount selection remains functional");
            check(withdraw.progressImg.GetComponentInParent<Mask>()!=null,"Progress retains rounded track clipping");
            await PayPalCapture(folder,"FakeWithdrawPanel-"+Screen.width+"x"+Screen.height+".png");
            PressStandard(withdraw.transform.Find("Content/Back").GetComponent<Button>());
            await UniTask.DelayFrame(4);inspectedPage=null;
            check(!UIModule.Instance.PageIsOpen(UIPageIds.FakeWithdrawPanel) && BizzaGameplayBridge.Page.Input.InputEnabled,"Back restores gameplay input");
            check(balance==ItemUtils.GetItemCount(E_ItemType.Dollar),"Review does not change account balance");
        }

        static void CheckPageAspect(GameObject page, Action<bool,string> check)
        {
            Canvas.ForceUpdateCanvases();
            var content=(RectTransform)page.transform.Find("Content");
            check(content!=null && Mathf.Abs(content.localScale.x-content.localScale.y)<.0001f,"Uniform content scale / "+page.name);
            var background=page.transform.Find("BackgroundUnderlay").GetComponent<Image>();
            var bounds=PayPalRect(background.rectTransform);
            check(bounds.xMin<=1 && bounds.xMax>=851 && bounds.yMin<=1 && bounds.yMax>=1845,"Background covers viewport / "+page.name);
            var mesh=background.canvasRenderer.GetMesh();
            check(mesh!=null && Mathf.Abs(mesh.bounds.size.x-background.GetPixelAdjustedRect().width)<1,"Background mesh covers viewport width / "+page.name);
            foreach(var button in page.GetComponentsInChildren<Button>())
            {
                var rect=(RectTransform)button.transform;
                var corners=new Vector3[4];rect.GetWorldCorners(corners);
                float ratio=Vector3.Distance(corners[0],corners[3])/Vector3.Distance(corners[0],corners[1]);
                check(Mathf.Abs(ratio-rect.rect.width/rect.rect.height)<.002f,"No non-uniform button scaling / "+button.name);
                check(HitStandard(button),"Native button receives raycast / "+button.name);
            }
            foreach(var text in page.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();check(!text.isTextOverflowing,"Text fits / "+text.name);
            }
        }
    }
}
