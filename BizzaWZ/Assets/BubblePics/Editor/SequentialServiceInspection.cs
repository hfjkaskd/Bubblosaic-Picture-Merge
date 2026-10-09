using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewServiceCore(string folder,Action<bool,string> check)
        {
            var p=(ServicePanel)await UIModule.Instance.OpenPage(UIPageIds.ServicePanel);inspectedPage=p;await UniTask.Delay(2500,ignoreTimeScale:true);await PayPalCapture(folder,"Live-State.png");int saved=SaveDataUtils.GameData.chatInfos.Count;
            foreach(var row in p.chatElements)if(row!=null)UnityEngine.Object.Destroy(row.gameObject);p.chatElements.Clear();await UniTask.DelayFrame(3);
            p.CreateElements(new ChatInfo{time="2026-09-28 10:24:00",spokesperson=Spokesperson.Issue,chatcontent="How can I help?"},0,false);
            p.CreateElements(new ChatInfo{time="2026-09-28 10:24:00",spokesperson=Spokesperson.Player,chatcontent="Where can I check my request?"},0,false);
            p.CreateElements(new ChatInfo{time="2026-09-28 10:24:00",spokesperson=Spokesperson.Issue,chatcontent="Open History to see its status."},0,false);
            p.FillCustomQuent();await UniTask.DelayFrame(8);check(p.chatElements.Count==3,"Dynamic chat records render both speakers");check(saved==SaveDataUtils.GameData.chatInfos.Count,"View-only messages are not saved or sent");check(!p.CanSend&&!p.canSendObj.activeSelf,"Empty input retains send guard");await PayPalCapture(folder,"Reference-State.png");
            foreach(string path in new[]{"Title/CloseBtn","Title/HistoryBtn","Title/FQA","SequentialQuick","Content/InputNode/ClearBtn"})check(HitStandard(p.transform.Find(path).GetComponent<Button>()),"Reachable standard Button "+path);check(!p.notCanSendObj.GetComponent<Button>().interactable,"Empty-send button remains intentionally disabled");
            p.inputText.Text="Local layout inspection — not sent.";p.inputText.OnValueChanged.Invoke(p.inputText.Text);await UniTask.DelayFrame(4);check(p.CanSend&&p.canSendObj.activeSelf,"Input value event enables original send state");check(HitStandard(p.canSendObj.GetComponent<Button>()),"Nonempty Send owns reachable native Button");PressStandard(p.transform.Find("Content/InputNode/ClearBtn").GetComponent<Button>());await UniTask.DelayFrame(3);check(!p.CanSend&&p.selectQuestionButton.gameObject.activeSelf,"Clear returns to original quick selection mode");
            PressStandard(p.transform.Find("SequentialQuick").GetComponent<Button>());await UniTask.Delay(500,ignoreTimeScale:true);check(UIModule.Instance.PageIsOpen(UIPageIds.ServiceSelectPanel),"Quick replies opens actual selection panel");var select=UIModule.Instance.GetPage<ServiceSelectPanel>();if(select!=null)UIModule.Instance.ClosePage(select);
            foreach(string locale in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(locale);await UniTask.DelayFrame(4);foreach(var text in p.transform.Find("Title").GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();check(!text.isTextOverflowing,"Header text fits "+locale+"/"+text.name);}await PayPalCapture(folder,locale+".png");}
            PressStandard(p.transform.Find("Title/CloseBtn").GetComponent<Button>());await UniTask.DelayFrame(3);check(!UIModule.Instance.PageIsOpen(UIPageIds.ServicePanel),"Back closes feedback without sending");
        }
    }
}
