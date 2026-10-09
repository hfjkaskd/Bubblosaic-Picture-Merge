using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewRatingCore(string folder,Action<bool,string> check)
        {
            var p=(StarRatingPopup)await UIModule.Instance.OpenPage(UIPageIds.StarRatingPopup);inspectedPage=p;await UniTask.DelayFrame(4);for(int i=0;i<5;i++)check(p.starImages[i].sprite==p.starOff,"Initial star "+i+" is unselected");
            foreach(int rating in new[]{1,5,2,4}){check(HitStandard(p.starButtons[rating-1].GetComponent<Button>()),"Star "+rating+" owns native input");PressStandard(p.starButtons[rating-1].GetComponent<Button>());await UniTask.Delay(1000,ignoreTimeScale:true);for(int i=0;i<5;i++)check(p.starImages[i].sprite==(i<rating?p.starOn:p.starOff),"Rating "+rating+" updates star "+i);}
            await PayPalCapture(folder,"Reference-State.png");check(HitStandard(p.goToRateButton.GetComponent<Button>()),"Rate is reachable native Button");foreach(string locale in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(locale);await UniTask.DelayFrame(4);foreach(var text in p.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();check(!text.isTextOverflowing,"Rating caption fits "+locale+"/"+text.name);}await PayPalCapture(folder,locale+".png");}
            // Close only from four stars, which does not invoke the native store review flow.
            PressStandard(p.goToRateButton.GetComponent<Button>());await UniTask.DelayFrame(3);check(!UIModule.Instance.PageIsOpen(UIPageIds.StarRatingPopup),"Four-star Rate closes; no store review submitted");
        }
    }
}
