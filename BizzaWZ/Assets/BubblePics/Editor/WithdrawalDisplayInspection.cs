using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewWithdrawalDisplayCore(string folder, Action<bool, string> check)
        {
            float balance = ItemUtils.GetItemCount(E_ItemType.Dollar);
            string stages = JsonUtility.ToJson(SaveDataUtils.FakeWithDrawPanelData);
            bool claimed = SaveDataUtils.GameData.fakeWithdrawPanelFirstWithdraw;
            var page = (FakeWithdrawPanel)await UIModule.Instance.OpenPage(UIPageIds.FakeWithdrawPanel);
            inspectedPage = page;
            for (int i = 0; i < 100 && page.GetComponentsInChildren<NewPlayerPayoutOption>().Length == 0; i++) await UniTask.Delay(100, ignoreTimeScale:true);
            await UniTask.Delay(600, ignoreTimeScale:true);
            check(page.gameObject.activeInHierarchy, "Production withdrawal request completed");
            Canvas.ForceUpdateCanvases();
            var bounds = PayPalRect((RectTransform)page.transform.Find("Backdrop"));
            check(bounds.xMin <= 1 && bounds.xMax >= 851 && bounds.yMin <= 1 && bounds.yMax >= 1845, "Background covers viewport / " + bounds);
            for (int i = 0; i < page.withdrawMissionSOList.Count; i++)
            {
                var item = page.items[i];
                check(HitStandard(item.btn.GetComponent<Button>()), "Native amount button " + i);
                var r = PayPalRect((RectTransform)item.transform);
                check(r.xMin >= 0 && r.xMax <= 852, "Amount card remains inside viewport " + i);
            }
            foreach (string name in new[]{"Back", "History", "FAQ", "Withdraw", "Service"})
                check(HitStandard(page.transform.Find(name).GetComponent<Button>()), "Native action reachable / " + name);
            foreach (var text in page.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate(); check(!text.isTextOverflowing, "Text fits / " + text.name);
            }
            if (page.withdrawMissionSOList.Count > 1)
            {
                PressStandard(page.items[1].btn.GetComponent<Button>());
                await UniTask.Delay(600, ignoreTimeScale:true);
                check(page.curSelectIndex == 1 && page.items[1].selectObj.activeSelf, "Amount selection updates native state");
            }
            await PayPalCapture(folder, "Unity-" + Screen.width + "x" + Screen.height + ".png");
            page.progressImg.DOKill();
            try
            {
                foreach (float amount in new[]{0f, .01f, .0965f, .5f, 1f})
                {
                    page.progressImg.fillAmount = amount;
                    page.progressTxt.text = "Preview: " + (amount * 100).ToString("0.##") + "%";
                    await UniTask.DelayFrame(4);
                    Canvas.ForceUpdateCanvases();
                    var mesh = page.progressImg.canvasRenderer.GetMesh();
                    float renderedWidth = mesh.vertexCount == 0 ? 0 : mesh.bounds.size.x;
                    float expected = page.progressImg.GetPixelAdjustedRect().width * amount;
                    check(Mathf.Abs(renderedWidth - expected) < 2, "Fill geometry proportional / " + amount + " / " + renderedWidth + " vs " + expected);
                    await PayPalCapture(folder, "Progress-" + Mathf.RoundToInt(amount * 10000) + "-" + Screen.height + ".png");
                }
            }
            finally { page.UpdateProgress(false); }
            PressStandard(page.transform.Find("Back").GetComponent<Button>());
            await UniTask.DelayFrame(4); inspectedPage = null;
            check(!UIModule.Instance.PageIsOpen(UIPageIds.FakeWithdrawPanel), "Back closes withdrawal page");
            check(BizzaGameplayBridge.Page.Input.InputEnabled && !BizzaGameplayBridge.IsInputBlocked, "Gameplay input restored");
            check(balance == ItemUtils.GetItemCount(E_ItemType.Dollar) && stages == JsonUtility.ToJson(SaveDataUtils.FakeWithDrawPanelData) && claimed == SaveDataUtils.GameData.fakeWithdrawPanelFirstWithdraw, "No balances or claim state changed");
        }
    }
}
