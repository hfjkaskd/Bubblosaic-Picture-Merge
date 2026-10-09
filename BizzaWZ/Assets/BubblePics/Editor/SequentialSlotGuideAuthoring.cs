using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class SequentialSlotGuideAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("SlotFAQPanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/f83716ad167a-16-SlotFAQPanel-simple.png")),849,1852);
            a.Slice("Background",0,0,849,1852).Slice("Back",19,69,104,107).Slice("OK",143,1518,566,154).Slice("Header",172,411,505,110).Slice("HeaderText",229,429,392,76).Slice("TopTitle",236,77,377,65).Slice("SpinRule",271,1350,313,57).Slice("VideoRule",272,1423,447,57);
            int[] rows={539,667,792,918,1051,1182};int[] heights={117,117,116,117,119,121};
            for(int i=0;i<6;i++)a.Slice("Row"+i,49,rows[i],749,heights[i]);
            a.Slice("CoinsAmount",230,268,220,74).Slice("CashAmount",605,280,181,72).Slice("RulePanel",49,1325,750,182);
            a.Import();a.Erase(a.Mat("Background"),new Rect(236,274,205,58),new Rect(613,287,167,60),new Rect(268,1350,385,61),new Rect(270,1421,456,63));a.Mat("Background").SetVector("_SamplePoint",new Vector4(733,1350,1,0));a.Mat("Background").SetFloat("_EraseFeather",4);
            a.Erase(a.Mat("Background"),a.R("CoinsAmount"),a.R("CashAmount"));a.Mat("Background").SetFloat("_SampleX",780);
            a.Erase(a.Mat("RulePanel"),new Rect(271,1351,461,53),new Rect(271,1430,461,44));a.Mat("RulePanel").SetFloat("_EraseFeather",3);a.Mat("TopTitle").SetFloat("_WarmMatte",1);
            var blank=a.Material("OKBlank");a.Erase(blank,new Rect(359,1555,132,86));blank.SetFloat("_SampleX",540);a.Round(blank,a.R("OK"),69);a.Round(a.Mat("OK"),a.R("OK"),69);a.Round(a.Mat("Back"),a.R("Back"),51);
            var header=a.Material("HeaderBlank");a.Erase(header,new Rect(228,430,398,74));header.SetFloat("_SampleX",640);header.SetFloat("_EraseFeather",3);a.Round(header,a.R("Header"),48);a.Round(a.Mat("Header"),a.R("Header"),48);
            const string path="Assets/BizzaWZ/Final/Real/UI/SlotsPanel/SlotFQAPanel/SlotFQAPanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<SlotFAQPanel>();var t=go.transform;Clear(t);Stretch(t);a.Graphic(t,"Background");p.bizzaButton=a.Button(t,"OK","OK",key:"seq_ok",caption:"OK",size:77);Bind(p,"backButton",a.Button(t,"Back","Back"));a.Graphic(t,"Header").GetComponent<Image>().material=header;var heading=a.Text(t,"GuideTitle",new Rect(221,429,412,79),63,color:Color.white);Localize(heading,"seq_reward_guide");a.Caption(t,heading,"Header","Reward guide");
                var coins=a.Text(t,"Coins",new Rect(231,268,216,78),68);var coinInfo=Ensure<CurrencyInfo>(coins);coinInfo.e_ItemType=E_ItemType.Gold;coinInfo.valueText=coins;var cash=a.Text(t,"Cash",new Rect(607,284,178,76),66,color:new Color(0,.51f,.01f));var cashInfo=Ensure<CurrencyInfo>(cash);cashInfo.e_ItemType=E_ItemType.Dollar;cashInfo.valueText=cash;
                a.Caption(t,coins,"CoinsAmount","10,000");a.Caption(t,cash,"CashAmount","$2.50");a.Graphic(t,"RulePanel");
                for(int i=0;i<6;i++){var r=a.R("Row"+i);a.Erase(a.Mat("Row"+i),new Rect(587,r.y+20,204,r.height-38));a.Mat("Row"+i).SetFloat("_EraseFeather",3);a.Round(a.Mat("Row"+i),r,30);a.Graphic(t,"Row"+i);var label=a.Text(t,"Rule"+i,new Rect(586,r.y+17,203,r.height-30),32);label.enableWordWrapping=true;Localize(label,i<3?"seq_slot_cash_reward":"seq_slot_bonus_reward");}
                var rule=a.Text(t,"SpinRuleText",new Rect(272,1348,443,61),35,TextAlignmentOptions.MidlineLeft);Localize(rule,"seq_slot_rule");a.Caption(t,rule,"SpinRule","1 spin every 5 levels");var video=a.Text(t,"VideoRuleText",new Rect(271,1423,480,66),34,TextAlignmentOptions.MidlineLeft);Localize(video,"seq_slot_video_rule");a.Caption(t,video,"VideoRule","Watch a video for an extra spin");Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}
