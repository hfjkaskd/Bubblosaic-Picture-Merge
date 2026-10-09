using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewSlotRewardCore(string folder,Action<bool,string> check)
        {
            var parent=(SlotPanel)await UIModule.Instance.OpenPage(UIPageIds.SlotPanel);inspectedPage=parent;await UniTask.DelayFrame(4);var p=parent.slotRewardPanel;double balance=ItemUtils.Get(E_ItemType.Dollar).Count;p.gameObject.SetActive(true);p.Init(1000,.25f,nameof(E_WzIconType.PileWealth));await UniTask.DelayFrame(4);await PayPalCapture(folder,"Live-State.png");p.coinText.text="+1,000";p.dollarText.text="+$0.25";parent.slotRewardPanel.coinTarget.valueText.text="10,000";parent.slotRewardPanel.dollarTarget.valueText.text="$2.50";await UniTask.DelayFrame(4);await PayPalCapture(folder,"Reference-State.png");check(p.coinObj.activeSelf&&p.dollarObj.activeSelf,"Both nonzero rewards are visible");check(HitStandard(p.btnObj.GetComponent<Button>()),"OK owns native Button, reward callback bound in code");
            LanguageUtils.SelectedLanguage="pt-BR";Localization.SetLocale("pt_BR");
            p.coinTarget.Refresh();p.dollarTarget.Refresh();await UniTask.Delay(650,ignoreTimeScale:true);
            p.Init(.02f,11.32f,nameof(E_WzIconType.PileWealth));await UniTask.DelayFrame(4);await PayPalCapture(folder,"Small-Amounts-Unity.png");
            // Empty live captions reveal any old baked glyphs that longer review amounts conceal.
            p.coinText.text="";p.dollarText.text="";await UniTask.DelayFrame(4);await PayPalCapture(folder,"Clean-Cards-Unity.png");
            p.Init(0,.25f,nameof(E_WzIconType.HundredMoney));check(!p.coinObj.activeSelf&&p.dollarObj.activeSelf,"Cash-only reward hides coin card");await PayPalCapture(folder,"Cash-Only.png");p.Init(1000,0,nameof(E_WzIconType.GoldCoin));check(p.coinObj.activeSelf&&!p.dollarObj.activeSelf,"Coin-only reward hides cash card");p.Init(1000,.25f,nameof(E_WzIconType.PileWealth));
            foreach(string locale in new[]{"pt_BR","id","zh_CN"}){Localization.SetLocale(locale);await UniTask.DelayFrame(4);foreach(var text in p.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();check(!text.isTextOverflowing,"Reward text fits "+locale+"/"+text.name);}await PayPalCapture(folder,locale+".png");}
            check(balance==ItemUtils.Get(E_ItemType.Dollar).Count,"Review did not claim or add displayed reward");p.gameObject.SetActive(false);CloseRuntime();
        }
    }
}
