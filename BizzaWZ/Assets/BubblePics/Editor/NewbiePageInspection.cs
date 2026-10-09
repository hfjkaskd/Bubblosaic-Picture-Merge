using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewNewbieCore(string folder,Action<bool,string> check)
        {
            var p=(NewbieGiftPage)await UIModule.Instance.OpenPage(UIPageIds.NewbieGiftPage);inspectedPage=p;await UniTask.DelayFrame(5);check(!string.IsNullOrEmpty(p.Os_ComText.text),"Gift value arrives through original country/config formatting");check(HitStandard(p.continueBtn.GetComponent<Button>()),"Visible gift action owns native Button");await PayPalCapture(folder,"Live-State.png");p.Os_ComText.text="$0.10";await UniTask.DelayFrame(5);await PayPalCapture(folder,"Reference-State.png");
            foreach(string lang in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(lang);await UniTask.DelayFrame(4);foreach(var text in p.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();check(!text.isTextOverflowing,"Gift text fits "+lang+"/"+text.name);}await PayPalCapture(folder,lang+".png");}CloseRuntime();await UniTask.DelayFrame(4);check(!UIModule.Instance.PageIsOpen(UIPageIds.NewbieGiftPage),"Inspection dismisses without claiming gift");
        }
    }
}
