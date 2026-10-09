using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewSlotGuideCore(string folder,Action<bool,string> check)
        {
            inspectedParent=await UIModule.Instance.OpenPage(UIPageIds.SlotPanel);var p=(SlotFAQPanel)await UIModule.Instance.OpenPage(UIPageIds.SlotFAQPanel);inspectedPage=p;await UniTask.DelayFrame(5);await PayPalCapture(folder,"Live-State.png");p.transform.Find("Coins").GetComponent<TMP_Text>().text="10,000";p.transform.Find("Cash").GetComponent<TMP_Text>().text="$2.50";await UniTask.DelayFrame(3);await PayPalCapture(folder,"Reference-State.png");check(HitStandard(p.bizzaButton.GetComponent<Button>())&&HitStandard(p.transform.Find("Back").GetComponent<Button>()),"Both OK and Back are native reachable buttons");check(SlotProgressUtil.RequiredPassedLevels==5,"Five-level help rule matches production requirement");
            foreach(string locale in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(locale);await UniTask.DelayFrame(4);foreach(var text in p.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();check(!text.isTextOverflowing,"Rule fits "+locale+"/"+text.name);}await PayPalCapture(folder,locale+".png");}
            PressStandard(p.bizzaButton.GetComponent<Button>());await UniTask.DelayFrame(3);check(!UIModule.Instance.PageIsOpen(UIPageIds.SlotFAQPanel)&&UIModule.Instance.PageIsOpen(UIPageIds.SlotPanel),"OK returns to machine");p=(SlotFAQPanel)await UIModule.Instance.OpenPage(UIPageIds.SlotFAQPanel);inspectedPage=p;await UniTask.DelayFrame(3);PressStandard(p.transform.Find("Back").GetComponent<Button>());await UniTask.DelayFrame(3);check(!UIModule.Instance.PageIsOpen(UIPageIds.SlotFAQPanel),"Back returns to machine");
        }
    }
}
