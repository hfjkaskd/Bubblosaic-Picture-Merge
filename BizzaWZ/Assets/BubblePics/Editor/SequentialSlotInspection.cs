using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewSlotCore(string folder,Action<bool,string> check)
        {
            var p=(SlotPanel)await UIModule.Instance.OpenPage(UIPageIds.SlotPanel);inspectedPage=p;await UniTask.DelayFrame(6);int progress=SlotProgressUtil.Current;double balance=ItemUtils.Get(E_ItemType.Dollar).Count;
            check(p.canClickObj.activeSelf==SlotProgressUtil.CanFreeSpin&&p.notCanClickObj.activeSelf!=SlotProgressUtil.CanFreeSpin,"Spin state follows actual earned progress");foreach(var button in new[]{p.closeBtn,p.faqBtn,p.slotBtn})check(HitStandard(button.GetComponent<Button>()),"Reachable native "+button.name);await PayPalCapture(folder,"Live-State.png");
            p.slotRewardPanel.coinTarget.valueText.text="10,000";p.slotRewardPanel.dollarTarget.valueText.text="$2.50";await UniTask.DelayFrame(4);await PayPalCapture(folder,"Reference-State.png");
            bool completed=false;p.slotMachineManager.PlayAnim(false,_=>completed=true);await UniTask.Delay(8500,ignoreTimeScale:true);check(completed,"Original reel animation reaches completion callback");foreach(var entry in p.slotMachineManager.slotEntries)check(entry.img1.sprite!=null&&entry.img1.sprite.vertices.Length>4,"Animated reel retains authored symbol geometry");await PayPalCapture(folder,"Reel-Result.png");check(progress==SlotProgressUtil.Current&&balance==ItemUtils.Get(E_ItemType.Dollar).Count,"Visual animation test does not consume spins or award money");
            PressStandard(p.faqBtn.GetComponent<Button>());await UniTask.Delay(1600,ignoreTimeScale:true);check(UIModule.Instance.PageIsOpen(UIPageIds.SlotFAQPanel),"FAQ opens actual rules panel");var faq=UIModule.Instance.GetPage<SlotFAQPanel>();if(faq!=null)UIModule.Instance.ClosePage(faq);
            foreach(string locale in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(locale);await UniTask.DelayFrame(4);foreach(var text in p.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();if(text.name.StartsWith("Sequential"))check(!text.isTextOverflowing,"Slot label fits "+locale+"/"+text.name);}await PayPalCapture(folder,locale+".png");}
            PressStandard(p.closeBtn.GetComponent<Button>());await UniTask.DelayFrame(3);check(!UIModule.Instance.PageIsOpen(UIPageIds.SlotPanel),"Back closes the reward machine");
        }
    }
}
