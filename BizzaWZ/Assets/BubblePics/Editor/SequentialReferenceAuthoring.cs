using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;

namespace BubblePics.EditorTools
{
    public static class SequentialReferenceAuthoring
    {
        public static void Apply(string id)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring.");
            if(id=="WithdrawHintPanel")Hint();
            else if(id=="DailyWithdrawPanel")Daily();
            else if(id=="ExchangeRatePanel")Rate();
            else if(id=="UIWithdrawalPanel-PIX")FormPresentationAuthoring.ApplyPix();
            else if(id=="UIWithdrawalPanel-DANA")FormPresentationAuthoring.ApplyDana();
            else if(id=="WithdrawalSingleMethod")FormPresentationAuthoring.ApplySingleMethodLayout();
            else if(id=="LoadingPanel")LoadingReferenceAuthoring.Apply();
            else if(id=="PausePanel")PauseReferenceAuthoring.Apply();
            else if(id=="LosePanel")LoseReferenceAuthoring.Apply();
            else if(id=="WhiteWinPanel")VictoryReferenceAuthoring.Apply();
            else if(id=="UIDailyTaskPage")TaskReferenceAuthoring.Apply();
            else if(id=="DailyMissionPanel")DailyMissionReferenceAuthoring.Apply();
            else if(id=="AddPropPanel")AddPropReferenceAuthoring.Apply();
            else if(id=="GetRewardPanel")RewardReferenceAuthoring.Apply();
            else if(id=="NewbieGiftPage")NewbieReferenceAuthoring.Apply();
            else if(id=="CommonConfirmTipsPanel")SequentialCommonNoticeAuthoring.Apply();
            else if(id=="ServicePanel")SequentialServiceAuthoring.Apply();
            else if(id=="ServiceSelectPanel")SequentialQuickReplyAuthoring.Apply();
            else if(id=="StarRatingPopup")SequentialRatingAuthoring.Apply();
            else if(id=="SlotPanel")SequentialSlotAuthoring.Apply();
            else if(id=="SlotFAQPanel")SequentialSlotGuideAuthoring.Apply();
            else if(id=="SlotRewardPanel")SequentialSlotRewardAuthoring.Apply();
            else if(id=="UI_TeachFinterMove")SequentialSwipeAuthoring.Apply();
            else if(id=="UITeachMaskFocusPage")SequentialFocusAuthoring.Apply();
            else if(id=="UI_TeachMask")SequentialMaskAuthoring.Apply();
            else if(id=="UI_TeachTip")SequentialTipAuthoring.Apply();
            else if(id=="TransitionBlock")SequentialTransitionAuthoring.Apply();
            else if(id=="BroadCastBar")SequentialBroadcastAuthoring.Apply();
            else if(id=="UIBizzaAAA")SequentialFloatChestAuthoring.Apply();
            else throw new InvalidOperationException("No reviewed page authoring yet: "+id);
            AssetDatabase.SaveAssets();
        }
        static string Art(string name)=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/"+name));
        static void Rate()
        {
            var a=new ReferencePrefabTools("ExchangeRatePanel",Art("723dfc90259a-13-兑换比例提升.png"),852,1846);
            a.Slice("Panel",41,301,770,1268).Slice("Title",161,396,535,94).Slice("Level",299,488,254,64)
                .Slice("Intro",139,581,575,81).Slice("Before",97,693,658,283).Slice("BeforeCaption",350,730,157,39).Slice("BeforeBadge",280,717,292,65)
                .Slice("Now",96,1061,660,285).Slice("NowCaption",378,1098,105,41).Slice("NowBadge",280,1084,292,66).Slice("Arrow",381,974,91,83)
                .Slice("Check",120,1373,614,136).Slice("Close",693,333,91,94);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(94,389,665,1150),new Rect(706,349,65,63));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(102,570,1,0));a.Mat("Panel").SetFloat("_EraseFeather",22);a.Round(a.Mat("Panel"),a.R("Panel"),79,2);
            a.Mat("Title").SetFloat("_InkOnly",1);a.Mat("Intro").SetFloat("_InkOnly",1);a.Mat("BeforeCaption").SetFloat("_InkOnly",1);a.Mat("NowCaption").SetFloat("_InkOnly",1);
            a.Erase(a.Mat("Level"),new Rect(335,500,183,44));a.Mat("Level").SetFloat("_SampleX",322);a.Round(a.Mat("Level"),a.R("Level"),31,1);
            a.Erase(a.Mat("Before"),new Rect(343,727,175,47),new Rect(308,794,323,152));a.Mat("Before").SetFloat("_SampleX",629);a.Mat("Before").SetFloat("_EraseFeather",7);a.Round(a.Mat("Before"),a.R("Before"),43,1);
            a.Erase(a.Mat("Now"),new Rect(369,1095,121,48),new Rect(308,1167,323,155));a.Mat("Now").SetFloat("_SampleX",627);a.Mat("Now").SetFloat("_EraseFeather",7);a.Round(a.Mat("Now"),a.R("Now"),40,1);
            a.Erase(a.Mat("BeforeBadge"),new Rect(343,727,175,47));a.Mat("BeforeBadge").SetFloat("_SampleX",310);a.Round(a.Mat("BeforeBadge"),a.R("BeforeBadge"),32,1);
            a.Erase(a.Mat("NowBadge"),new Rect(369,1095,121,48));a.Mat("NowBadge").SetFloat("_SampleX",310);a.Round(a.Mat("NowBadge"),a.R("NowBadge"),32,1);
            a.Round(a.Mat("Arrow"),a.R("Arrow"),15,1);a.Round(a.Mat("Close"),a.R("Close"),45,1);
            var blank=a.Material("CheckBlank");a.Erase(blank,new Rect(319,1400,221,75));blank.SetFloat("_SampleX",223);a.Round(blank,a.R("Check"),65,2);a.Round(a.Mat("Check"),a.R("Check"),65,2);
            const string path="Assets/BizzaWZ/Final/Real/UI/ExchangeRatePanel/ExchangeRatePanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page=go.GetComponent<ExchangeRatePanel>();var tr=go.transform;Clear(tr);Stretch(tr);a.Overlay(tr,new Color(.025f,.15f,.26f,.3f));a.Graphic(tr,"Panel");
                var title=a.Text(tr,"Title",new Rect(127,395,598,90),65);Localize(title,"sequential_rate_title");a.Caption(tr,title,"Title","Rate increased!");
                a.Graphic(tr,"Level");var level=a.Text(tr,"LevelValue",new Rect(309,492,234,58),41);Bind(page,"levelText",level);
                var intro=a.Text(tr,"Intro",new Rect(133,578,590,84),33);intro.enableWordWrapping=true;Localize(intro,"sequential_rate_intro");a.Caption(tr,intro,"Intro","Great progress! You now get a higher\npayout rate for your coins!");
                a.Graphic(tr,"Before");a.Graphic(tr,"Arrow");a.Graphic(tr,"Now");a.Graphic(tr,"BeforeBadge");a.Graphic(tr,"NowBadge");
                page.beforeDefiniteText=a.Text(tr,"BeforeLabel",new Rect(280,715,292,66),42,color:new Color(.26f,.38f,.65f));Localize(page.beforeDefiniteText,"sequential_before");a.Caption(tr,page.beforeDefiniteText,"BeforeCaption","BEFORE");
                page.nowDefiniteText=a.Text(tr,"NowLabel",new Rect(280,1084,292,66),42);Localize(page.nowDefiniteText,"sequential_now");a.Caption(tr,page.nowDefiniteText,"NowCaption","NOW");
                page.beforeBlanceText=a.Text(tr,"BeforeCoins",new Rect(318,791,338,63),51,TextAlignmentOptions.MidlineLeft);
                page.beforeClashText=a.Text(tr,"BeforeAmount",new Rect(318,858,338,88),77,TextAlignmentOptions.MidlineLeft);
                page.nowBlanceText=a.Text(tr,"NowCoins",new Rect(318,1164,338,63),51,TextAlignmentOptions.MidlineLeft);
                page.nowClashText=a.Text(tr,"NowAmount",new Rect(318,1231,338,88),77,TextAlignmentOptions.MidlineLeft);
                Bind(page,"withdrawBtn",a.Button(tr,"Check","Check",key:"sequential_check",caption:"Check",size:65));Bind(page,"clickBtn",a.Button(tr,"Close","Close"));page.root=(RectTransform)tr;Validate(tr);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void Daily()
        {
            var a=new ReferencePrefabTools("DailyWithdrawPanel",Art("9cb804a53bac-12-每日可提现提示.png"),852,1846);
            a.Slice("Panel",40,424,771,1000).Slice("Title",122,561,613,76).Slice("Detail",100,931,653,258)
                .Slice("Available",303,1122,253,43).Slice("Withdraw",101,1215,652,149).Slice("Close",688,446,98,105);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(126,558,607,82),new Rect(177,850,501,71),new Rect(116,947,622,225),new Rect(127,1240,595,110),new Rect(706,465,62,65));
            a.Mat("Panel").SetFloat("_SampleX",109);a.Mat("Panel").SetFloat("_EraseFeather",8);a.Round(a.Mat("Panel"),a.R("Panel"),78,2);
            a.Mat("Title").SetFloat("_InkOnly",1);a.Mat("Available").SetFloat("_InkOnly",1);
            a.Erase(a.Mat("Detail"),new Rect(216,958,425,159),new Rect(288,1117,278,53));a.Mat("Detail").SetFloat("_SampleX",715);a.Mat("Detail").SetFloat("_EraseFeather",13);
            var blank=a.Material("WithdrawBlank");a.Erase(blank,new Rect(258,1241,340,91));blank.SetFloat("_SampleX",191);a.Round(blank,a.R("Withdraw"),70,2);a.Round(a.Mat("Withdraw"),a.R("Withdraw"),70,2);a.Round(a.Mat("Close"),a.R("Close"),49,2);
            const string path="Assets/BizzaWZ/Final/Real/UI/DailyWithdrawPanel/DailyWithdrawPanel.prefab";
            var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page=go.GetComponent<DailyWithdrawPanel>();var tr=go.transform;Clear(tr);Stretch(tr);a.Overlay(tr,new Color(.025f,.15f,.26f,.3f));a.Graphic(tr,"Panel");
                var title=a.Text(tr,"Title",new Rect(92,556,668,92),63);Localize(title,"sequential_daily_title");a.Caption(tr,title,"Title","Ready to withdraw!");
                var summary=Child(tr,"ExchangeSummary");a.Place(summary,new Rect(127,846,598,76));var group=Ensure<HorizontalLayoutGroup>(summary);group.childAlignment=TextAnchor.MiddleCenter;group.spacing=9*a.SY;group.childControlWidth=group.childControlHeight=true;group.childForceExpandWidth=group.childForceExpandHeight=false;
                page.balanceTxt=a.Text(summary,"Coins",new Rect(0,0,260,76),50);var coinsLayout=Ensure<LayoutElement>(page.balanceTxt);coinsLayout.preferredWidth=260*a.SX;coinsLayout.minWidth=100*a.SX;coinsLayout.preferredHeight=76*a.SY;
                page.withdrawalTxt=a.Text(summary,"Equivalent",new Rect(0,0,220,76),50);var cashLayout=Ensure<LayoutElement>(page.withdrawalTxt);cashLayout.preferredWidth=220*a.SX;cashLayout.minWidth=100*a.SX;cashLayout.preferredHeight=76*a.SY;
                a.Graphic(tr,"Detail");page.clashTxt=a.Text(tr,"Amount",new Rect(127,941,598,174),176);ApplyMoney(page.clashTxt);page.clashTxt.transform.localScale=new Vector3(1.125f,1,1);
                var available=a.Text(tr,"Available",new Rect(235,1114,388,59),42,color:new Color(.26f,.34f,.69f));Localize(available,"sequential_available");a.Caption(tr,available,"Available","Available now");
                var withdraw=a.Button(tr,"Withdraw","Withdraw",key:"sequential_withdraw",caption:"Withdraw",size:65);var close=a.Button(tr,"Close","Close");Bind(page,"withdrawBtn",withdraw);Bind(page,"clickBtn",close);page.root=(RectTransform)summary;Validate(tr);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void ApplyMoney(TMP_Text label)
        {
            label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/Resources/PayPalReference20260928/FredokaAmount-SDF.asset");label.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/BubblePics/Resources/PayPalReference20260928/AmountText.mat");label.color=Color.white;label.enableVertexGradient=true;label.colorGradient=new VertexGradient(new Color(.98f,1,.72f),new Color(.98f,1,.72f),new Color(.51f,.94f,.14f),new Color(.51f,.94f,.14f));label.UpdateMeshPadding();
        }
        static void Hint()
        {
            var a=new ReferencePrefabTools("WithdrawHintPanel",Art("05421dee3145-11-提现条件提示.png"),852,1846);
            a.Slice("Panel",46,624,760,804).Slice("Title",211,842,435,83).Slice("Detail",104,942,644,278)
                .Slice("Track",148,1109,558,36,new Vector4(18,18,18,18)).Slice("Fill",148,1109,518,36,new Vector4(18,18,18,18))
                .Slice("Confirm",135,1238,580,141);
            a.Import();
            a.Erase(a.Mat("Panel"),new Rect(205,837,444,92),new Rect(100,938,652,445));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(748,897,1,0));a.Round(a.Mat("Panel"),a.R("Panel"),67,2);
            a.Mat("Title").SetFloat("_InkOnly",1);
            a.Erase(a.Mat("Detail"),new Rect(142,964,571,242));a.Mat("Detail").SetFloat("_SampleX",725);
            a.Erase(a.Mat("Track"),a.R("Track"));a.Mat("Track").SetVector("_SamplePoint",new Vector4(680,1125,1,0));a.Round(a.Mat("Track"),a.R("Track"),18);
            var blank=a.Material("ConfirmBlank");a.Erase(blank,new Rect(332,1275,194,69));blank.SetFloat("_SampleX",224);a.Round(blank,a.R("Confirm"),62,2);a.Round(a.Mat("Confirm"),a.R("Confirm"),62,2);
            const string path="Assets/BizzaWZ/Final/Real/UI/RealWithdrawalPanl/RealWithdrawPanel.prefab";
            var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hint=go.GetComponent<RealWithdrawPanel>().hintPanel;var tr=hint.transform;Clear(tr);Stretch(tr);a.Overlay(tr,new Color(.025f,.15f,.26f,.45f));a.Graphic(tr,"Panel");
                var title=a.Text(tr,"Title",new Rect(158,840,537,93),75);Localize(title,"sequential_keep_going");a.Caption(tr,title,"Title","Keep going!");
                a.Graphic(tr,"Detail");var requirement=a.Text(tr,"Requirement",new Rect(146,956,560,98),77,color:new Color(0,.23f,.75f));
                var count=a.Text(tr,"ProgressCount",new Rect(273,1055,308,53),41);
                var track=a.Graphic(tr,"Track").GetComponent<Image>();a.Sliced(track);var fill=Ensure<Image>(Child(track.transform,"Fill"));Stretch(fill.transform);a.Visual(fill.transform,"Fill");a.Sliced(fill);Ensure<WithdrawCloudProgressFill>(fill);
                hint.hintText=a.Text(tr,"Hint",new Rect(137,1155,584,56),26,color:new Color(.23f,.29f,.69f));hint.hintText.enableWordWrapping=true;
                var close=a.Button(tr,"Confirm","Confirm",key:"sequential_got_it",caption:"Got it",size:62);
                Bind(hint,"closeBtn",close);Bind(hint,"requirementText",requirement);Bind(hint,"progressText",count);Bind(hint,"progressFill",fill);Validate(tr);tr.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
    }
}
