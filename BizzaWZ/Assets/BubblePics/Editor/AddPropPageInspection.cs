using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewAddPropCore(string folder,Action<bool,string> check)
        {
            foreach(var type in new[]{E_ItemType.GameProp_1,E_ItemType.GameProp_2,E_ItemType.GameProp_3})
            {
                var p=(AddPropPanel)await UIModule.Instance.OpenPage(UIPageIds.AddPropPanel,type);inspectedPage=p;await UniTask.DelayFrame(5);
                check(HitStandard(p.closeBtn.GetComponent<Button>())&&HitStandard(p.adBuyBtn.GetComponent<Button>()),"Tool page native close and video buttons reachable "+(int)type);check(!string.IsNullOrEmpty(p.propName.text),"Tool title bound "+(int)type);check(type==E_ItemType.GameProp_1?p.transform.Find("Hint").gameObject.activeSelf:p.propIcon.gameObject.activeSelf&&p.propIcon.sprite!=null,"Correct tool artwork "+(int)type);await PayPalCapture(folder,type==E_ItemType.GameProp_1?"Reference-State.png":"Tool-"+(int)type+".png");PressStandard(p.closeBtn.GetComponent<Button>());await UniTask.DelayFrame(4);check(!UIModule.Instance.PageIsOpen(UIPageIds.AddPropPanel),"Close retains game "+(int)type);inspectedPage=null;
            }
            foreach(string lang in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(lang);var p=(AddPropPanel)await UIModule.Instance.OpenPage(UIPageIds.AddPropPanel,E_ItemType.GameProp_1);inspectedPage=p;await UniTask.DelayFrame(4);foreach(var text in p.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();check(!text.isTextOverflowing,"Tool text fits "+lang+"/"+text.name);}await PayPalCapture(folder,lang+".png");CloseRuntime();await UniTask.DelayFrame(4);}
        }
    }
}
