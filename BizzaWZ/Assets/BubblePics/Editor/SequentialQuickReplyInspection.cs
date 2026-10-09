using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewQuickReplyCore(string folder,Action<bool,string> check)
        {
            var parent=(ServicePanel)await UIModule.Instance.OpenPage(UIPageIds.ServicePanel);inspectedParent=parent;await UniTask.Delay(2000,ignoreTimeScale:true);
            var p=(ServiceSelectPanel)await UIModule.Instance.OpenPage(UIPageIds.ServiceSelectPanel,parent);inspectedPage=p;await UniTask.DelayFrame(5);await PayPalCapture(folder,"Reference-State.png");int saved=SaveDataUtils.GameData.chatInfos.Count;
            for(int i=0;i<7;i++){if(i>0){p=(ServiceSelectPanel)await UIModule.Instance.OpenPage(UIPageIds.ServiceSelectPanel,parent);inspectedPage=p;await UniTask.DelayFrame(3);}string expected=p.defaultTexts[i].text;check(HitStandard(p.defaultButtons[i].GetComponent<Button>()),"Question "+(i+1)+" owns visible native Button");PressStandard(p.defaultButtons[i].GetComponent<Button>());await UniTask.DelayFrame(2);check(parent.inputText.Text==expected&&parent.CanSend,"Question "+(i+1)+" fills original selected question");check(!UIModule.Instance.PageIsOpen(UIPageIds.ServiceSelectPanel),"Question closes selector "+(i+1));}
            p=(ServiceSelectPanel)await UIModule.Instance.OpenPage(UIPageIds.ServiceSelectPanel,parent);inspectedPage=p;await UniTask.DelayFrame(3);PressStandard(p.customButton.GetComponent<Button>());await UniTask.DelayFrame(3);check(parent.inputText.gameObject.activeSelf&&parent.inputText.Text=="","Custom question opens editable input");
            p=(ServiceSelectPanel)await UIModule.Instance.OpenPage(UIPageIds.ServiceSelectPanel,parent);inspectedPage=p;
            foreach(string locale in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(locale);await UniTask.DelayFrame(5);foreach(var text in p.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();check(!text.isTextOverflowing,"Question text fits "+locale+"/"+text.name);}await PayPalCapture(folder,locale+".png");}
            PressStandard(p.transform.Find("Close").GetComponent<Button>());await UniTask.DelayFrame(2);check(!UIModule.Instance.PageIsOpen(UIPageIds.ServiceSelectPanel)&&parent.gameObject.activeInHierarchy,"Close returns to feedback");check(saved==SaveDataUtils.GameData.chatInfos.Count,"Selection checks did not save or send messages");
        }
    }
}
