using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewConfirmCore(string folder,Action<bool,string> check)
        {
            inspectedParent=await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);await UniTask.Delay(1200,ignoreTimeScale:true);
            inspectedAux=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BizzaWZ/Final/FunctionTools/CommonPublic/UI/CommonConfirmTipsPanel.prefab"));var p=inspectedAux.GetComponent<CommonConfirmTipsPanel>();int called=0;p.OnOpen(new CommonConfirmTipsPanel.Args{des="HTTPNetworkProblem",isLanguage=true,onFinish=()=>called++});await UniTask.DelayFrame(5);check(HitStandard(p.confirmBtn),"Confirmation owns visible native Button and receives input");check(!string.IsNullOrEmpty(p.desTxt.text),"Original network localization applied");await PayPalCapture(folder,"Reference-State.png");PressStandard(p.confirmBtn);check(called==1&&!p.gameObject.activeSelf,"Confirm hides panel and calls supplied completion once");
            p.gameObject.SetActive(true);p.OnOpen(new CommonConfirmTipsPanel.Args{des="Custom message for this operation.",isLanguage=false});check(p.desTxt.text=="Custom message for this operation.","Custom confirmation description remains supported");
            foreach(string lang in new[]{"pt-BR","id-ID","zh-CN","ar-SA"}){PlayerPrefs.SetString("SelectedLanguage",lang);p.OnOpen(new CommonConfirmTipsPanel.Args{des="HTTPNetworkProblem",isLanguage=true});await UniTask.DelayFrame(4);foreach(var text in p.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();check(!text.isTextOverflowing,"Common message fits "+lang+"/"+text.name);}await PayPalCapture(folder,lang+".png");}CloseRuntime();
        }
    }
}
