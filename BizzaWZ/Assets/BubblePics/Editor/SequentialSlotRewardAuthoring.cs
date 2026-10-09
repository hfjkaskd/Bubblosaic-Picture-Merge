using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class SequentialSlotRewardAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("SlotRewardPanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/835f69b1050e-17-SlotRewardPanel-simple.png")),849,1852);
            a.Slice("Panel",18,1094,812,580).Slice("Header",158,1028,534,145).Slice("CoinCard",79,1213,336,267).Slice("CashCard",436,1213,336,267).Slice("CoinAmount",111,1379,271,77).Slice("CashAmount",473,1383,255,71).Slice("OK",172,1504,503,128);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(76,1210,701,273),new Rect(169,1500,510,137));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(423,1422,1,0));a.Mat("Panel").SetFloat("_EraseRadius",50);a.Mat("Panel").SetFloat("_EraseFeather",5);a.Round(a.Mat("Panel"),a.R("Panel"),84);
            var h=a.Material("HeaderBlank");a.Erase(h,new Rect(207,1060,445,79));h.SetFloat("_SampleX",667);h.SetFloat("_EraseFeather",4);a.Round(h,a.R("Header"),63);a.Round(a.Mat("Header"),a.R("Header"),63);
            // Include the baked comma descender and dollar ascender, with clean source columns
            // that stay inside the card instead of copying its rounded bottom border.
            a.Erase(a.Mat("CoinCard"),new Rect(107,1377,280,86));a.Mat("CoinCard").SetFloat("_SampleX",380);a.Mat("CoinCard").SetFloat("_EraseFeather",2);a.Round(a.Mat("CoinCard"),a.R("CoinCard"),53);
            a.Erase(a.Mat("CashCard"),new Rect(469,1376,263,82));a.Mat("CashCard").SetFloat("_SampleX",740);a.Mat("CashCard").SetFloat("_EraseFeather",1);a.Round(a.Mat("CashCard"),a.R("CashCard"),53);
            var blank=a.Material("OKBlank");a.Erase(blank,new Rect(363,1534,124,71));blank.SetFloat("_SampleX",554);blank.SetFloat("_EraseFeather",4);a.Round(blank,a.R("OK"),61);a.Round(a.Mat("OK"),a.R("OK"),61);
            var go=PrefabUtility.LoadPrefabContents(SequentialSlotAuthoring.PathName);
            try
            {
                var p=go.GetComponent<SlotPanel>().slotRewardPanel;var t=p.transform;Clear(t);t.SetParent(go.transform,false);Stretch(t);t.SetAsLastSibling();a.Overlay(t,new Color(0,.13f,.2f,.18f));a.Graphic(t,"Panel");a.Graphic(t,"Header").GetComponent<Image>().material=h;var title=a.Text(t,"Title",new Rect(190,1055,470,89),66,color:Color.white);Localize(title,"seq_slot_reward");a.Caption(t,title,"Header","Your reward!");
                var coins=a.Graphic(t,"CoinCard");p.coinObj=coins.gameObject;p.coinText=a.Text(t,"CoinAmountText",new Rect(105,1372,283,94),75,color:new Color(.87f,.38f,0));a.Caption(t,p.coinText,"CoinAmount","+1,000");p.coinText.transform.SetParent(coins,true);t.Find("ReferenceCoinAmount").SetParent(coins,true);
                var cash=a.Graphic(t,"CashCard");p.dollarObj=cash.gameObject;p.dollarText=a.Text(t,"CashAmountText",new Rect(468,1375,267,94),72,color:new Color(0,.48f,.05f));a.Caption(t,p.dollarText,"CashAmount","+$0.25");p.dollarText.transform.SetParent(cash,true);t.Find("ReferenceCashAmount").SetParent(cash,true);
                SlotRewardLayoutAuthoring.Configure(p);
                var button=a.Button(t,"OK","OK",key:"seq_ok",caption:"OK",size:74);p.btnObj=button.gameObject;Bind(p,"collectButton",button);
                var state=Child(t,"ResultTypeState");Stretch(state);p.slotRewardIcons.Clear();foreach(var type in new[]{E_WzIconType.StackMoney,E_WzIconType.PileMoney,E_WzIconType.HundredMoney,E_WzIconType.GoldCoin,E_WzIconType.PileGold,E_WzIconType.PileWealth}){var im=Ensure<Image>(Child(state,"Type"+((int)type)));im.raycastTarget=false;p.slotRewardIcons.Add(new SlotRewardIcon{image=im,e_WzIconType=type});}p.resultImage=p.slotRewardIcons[0].image;state.gameObject.SetActive(false);Validate(t);t.gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAsset(go,SequentialSlotAuthoring.PathName);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}
