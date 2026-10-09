using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class DailyMissionReferenceAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("DailyMissionPanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/32efd1143898-07-DailyMissionPanel-simple.png")),849,1852);
            a.Slice("Panel",19,502,811,1033).Slice("Title",154,448,540,119).Slice("TitleInk",235,471,384,74).Slice("Close",722,486,113,109)
                .Slice("Reward",65,880,719,196).Slice("RewardCaption",354,892,148,45).Slice("AmountCaption",286,934,280,108).Slice("Track",95,1164,661,74).Slice("Fill",110,1178,385,50,new Vector4(25,25,25,25))
                .Slice("Timer",241,1252,367,59).Slice("ClockIcon",271,1259,43,44)
                .Slice("Watch",109,1328,634,171).Slice("WatchIcon",188,1357,105,107);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(153,502,544,71),new Rect(725,503,94,90),new Rect(58,873,733,636));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(766,1113,1,0));a.Mat("Panel").SetFloat("_EraseFeather",7);a.Round(a.Mat("Panel"),a.R("Panel"),65);
            a.Erase(a.Mat("Title"),new Rect(227,470,400,78));a.Mat("Title").SetFloat("_SampleX",661);a.Mat("Title").SetFloat("_EraseFeather",6);a.Round(a.Mat("Title"),a.R("Title"),47);a.Mat("TitleInk").SetFloat("_InkOnly",1);a.Round(a.Mat("Close"),a.R("Close"),54);
            a.Erase(a.Mat("Reward"),new Rect(342,885,177,58),new Rect(272,927,307,124));a.Mat("Reward").SetFloat("_SampleX",641);a.Mat("Reward").SetFloat("_EraseFeather",2);a.Round(a.Mat("Reward"),a.R("Reward"),48);a.Mat("RewardCaption").SetFloat("_InkOnly",1);a.Mat("AmountCaption").SetFloat("_WhiteMatte",.7f);
            a.Erase(a.Mat("Track"),new Rect(106,1173,638,60));a.Mat("Track").SetVector("_SamplePoint",new Vector4(699,1201,1,0));a.Mat("Track").SetFloat("_EraseRadius",30);a.Round(a.Mat("Track"),a.R("Track"),37);
            a.Erase(a.Mat("Fill"),new Rect(369,1178,109,50));a.Mat("Fill").SetFloat("_SampleX",311);a.Round(a.Mat("Fill"),a.R("Fill"),25);
            a.Erase(a.Mat("Timer"),new Rect(267,1258,317,47));a.Mat("Timer").SetFloat("_SampleX",590);a.Round(a.Mat("Timer"),a.R("Timer"),29);
            a.Mat("ClockIcon").SetFloat("_InkOnly",1);a.Mat("WatchIcon").SetFloat("_WarmMatte",0);a.Mat("WatchIcon").SetFloat("_WarmChroma",1);
            a.Round(a.Mat("Watch"),a.R("Watch"),79);var blank=a.Material("WatchBlank");a.Erase(blank,new Rect(292,1371,408,73));blank.SetFloat("_SampleX",687);blank.SetFloat("_EraseFeather",4);a.Round(blank,a.R("Watch"),79);
            const string path="Assets/BizzaWZ/Final/Real/UI/DailyMissionPanel/DailyMissionPanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<DailyMissionPanel>();var t=go.transform;Clear(t);Stretch(t);a.Overlay(t,new Color(0,.12f,.24f,.14f));a.Graphic(t,"Panel");a.Graphic(t,"Title");var title=a.Text(t,"TitleCaption",new Rect(214,473,425,76),60);Localize(title,"seq_daily_mission");a.Caption(t,title,"TitleInk","Daily mission");Bind(p,"closeBtn",a.Button(t,"Close","Close"));
                a.Graphic(t,"Reward");var reward=a.Text(t,"RewardCaption",new Rect(320,890,218,57),41);Localize(reward,"seq_reward");a.Caption(t,reward,"RewardCaption","Reward");var amount=a.Text(t,"RewardAmount",new Rect(262,939,326,104),100,color:new Color(1,.6f,.03f));a.Caption(t,amount,"AmountCaption","$0.20");Bind(p,"rewardAmountText",amount);
                p.hintsTxt=a.Text(t,"Requirement",new Rect(114,1087,620,74),37);p.hintsTxt.enableWordWrapping=true;
                a.Graphic(t,"Track");var fill=a.Graphic(t,"Fill").GetComponent<Image>();a.Place(fill.transform,new Rect(110,1178,630,50));a.Sliced(fill);Ensure<WithdrawCloudProgressFill>(fill);Bind(p,"taskProgressFill",fill);Bind(p,"taskProgressText",a.Text(t,"Progress",new Rect(167,1174,515,54),42,color:Color.white));
                var timer=a.Graphic(t,"Timer");a.Place(timer,new Rect(165,1248,519,70));
                var clock=a.Graphic(t,"ClockIcon");a.Place(clock,new Rect(185,1261,43,44));
                p.refreshTimeTxt=a.Text(t,"ResetTime",new Rect(240,1256,419,54),34,TextAlignmentOptions.Midline);
                p.refreshTimeTxt.enableAutoSizing=false;
                p.GoObj=Child(t,"GoState").gameObject;Stretch(p.GoObj.transform);var watch=a.Button(p.GoObj.transform,"Watch","Watch",key:"seq_watch_video",caption:"Watch video",size:56);var label=watch.transform.Find("WatchLabel");Bind(p,"goBtn",watch);
                var actionBlank=a.Material("ActionBlank");a.Erase(actionBlank,new Rect(182,1357,514,110));actionBlank.SetFloat("_SampleX",299);actionBlank.SetFloat("_EraseFeather",5);a.Round(actionBlank,a.R("Watch"),79);
                actionBlank.SetFloat("_HueShift",0);actionBlank.SetFloat("_Saturation",1);actionBlank.SetFloat("_Brightness",1);
                var watchBlue=a.Material("ActionWatchBlue");watchBlue.CopyPropertiesFromMaterial(actionBlank);watchBlue.SetFloat("_HueShift",.53f);watchBlue.SetFloat("_Saturation",.88f);watchBlue.SetFloat("_Brightness",1);
                var claimedGray=a.Material("ActionClaimedGray");claimedGray.CopyPropertiesFromMaterial(actionBlank);claimedGray.SetFloat("_HueShift",0);claimedGray.SetFloat("_Saturation",.12f);claimedGray.SetFloat("_Brightness",.88f);
                watch.GetComponent<Image>().material=watchBlue;watch.transform.Find("ReferenceWatch").gameObject.SetActive(false);
                a.Local(label,new Rect(123,30,388,106));var watchText=label.GetComponent<TMP_Text>();
                watchText.alignment=TextAlignmentOptions.MidlineLeft;watchText.fontSize=watchText.fontSizeMax=64*a.SY;watchText.fontSizeMin=48*a.SY;
                label.GetComponent<CanvasGroup>().alpha=1;
                var video=a.Graphic(watch.transform,"WatchIcon");a.Local(video,new Rect(52,41,68,82));video.GetComponent<Image>().preserveAspect=true;
                video.SetAsFirstSibling();
                var iconLayout=Ensure<LayoutElement>(video);iconLayout.minWidth=iconLayout.preferredWidth=68*a.SX;
                var watchLayout=Ensure<HorizontalLayoutGroup>(watch);watchLayout.padding=new RectOffset(Mathf.RoundToInt(60*a.SX),Mathf.RoundToInt(60*a.SX),0,0);
                watchLayout.childAlignment=TextAnchor.MiddleCenter;watchLayout.spacing=24*a.SX;
                watchLayout.childControlWidth=true;watchLayout.childControlHeight=false;
                watchLayout.childForceExpandWidth=false;watchLayout.childForceExpandHeight=false;
                p.WithdrawObj=Child(t,"WithdrawState").gameObject;Stretch(p.WithdrawObj.transform);var withdraw=a.Button(p.WithdrawObj.transform,"Withdraw","Watch",key:"sequential_withdraw",caption:"Watch video",size:56);withdraw.GetComponent<Image>().material=actionBlank;Bind(p,"withdrawBtn",withdraw);
                p.ClaimedObj=Child(t,"ClaimedState").gameObject;Stretch(p.ClaimedObj.transform);var claimed=a.Button(p.ClaimedObj.transform,"Claimed","Watch",key:"seq_claimed",caption:"Watch video",size:56);claimed.GetComponent<Image>().material=claimedGray;Bind(p,"claimedBtn",claimed);p.claimedHint=a.Text(t,"ClaimedNote",new Rect(127,1501,595,53),27);Localize(p.claimedHint,"seq_claimed");Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}

