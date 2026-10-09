using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class RewardReferenceAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("GetRewardPanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/0c45a9a862db-09-GetRewardPanel-simple.png")),849,1852);
            a.Slice("Backdrop",0,0,849,1852).Slice("Header",190,91,468,123).Slice("Level",277,191,300,82).Slice("Panel",18,644,813,1028)
                .Slice("RewardTitle",278,684,307,53).Slice("Coins",111,751,310,317).Slice("Cash",427,751,310,317).Slice("CoinsCaption",156,930,213,77).Slice("CashCaption",474,931,215,81)
                .Slice("Claim",91,1091,665,167).Slice("Collect",186,1267,475,110).Slice("Rate",46,1409,755,87).Slice("Progress",46,1497,755,151)
                .Slice("Track",133,1568,595,57,new Vector4(28,28,28,28)).Slice("Fill",144,1575,451,45,new Vector4(22,22,22,22)).Slice("CoinsFull",111,751,310,317).Slice("CashFull",427,751,310,317)
                .Slice("InformationCard",46,1409,755,87,new Vector4(43,43,43,43));
            a.Import();
            Blank(a,"Header",new Rect(236,112,387,69),219,42);Blank(a,"Level",new Rect(335,208,181,43),322,35);
            a.Erase(a.Mat("Panel"),new Rect(68,682,713,726),new Rect(38,1395,764,251));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(784,954,1,0));a.Mat("Panel").SetFloat("_EraseFeather",5);a.Round(a.Mat("Panel"),a.R("Panel"),84);a.Mat("RewardTitle").SetFloat("_InkOnly",1);
            // These legacy tiles are superseded by the clean reward sprites when
            // ConfigurePrefab runs below; keep other source slices unchanged.
            a.Erase(a.Mat("Coins"),new Rect(146,934,232,77));a.Mat("Coins").SetVector("_SampleRows",new Vector4(933,1042,1,0));a.Mat("Coins").SetFloat("_EraseFeather",.5f);a.Round(a.Mat("Coins"),a.R("Coins"),151);
            a.Erase(a.Mat("Cash"),new Rect(467,930,231,87));a.Mat("Cash").SetVector("_SampleRows",new Vector4(930,1020,1,0));a.Mat("Cash").SetFloat("_EraseFeather",3);a.Round(a.Mat("Cash"),a.R("Cash"),151);a.Mat("CoinsCaption").SetFloat("_WhiteMatte",.55f);a.Mat("CashCaption").SetFloat("_WhiteMatte",.55f);
            Blank(a,"Claim",new Rect(330,1126,321,86),677,81);Blank(a,"Collect",new Rect(278,1287,299,64),617,54);
            a.Erase(a.Mat("Rate"),new Rect(143,1427,209,60),new Rect(640,1422,129,65));a.Mat("Rate").SetFloat("_SampleX",542);a.Round(a.Mat("Rate"),a.R("Rate"),42);
            a.Erase(a.Mat("Progress"),new Rect(143,1513,435,54),new Rect(124,1561,612,69));a.Mat("Progress").SetFloat("_SampleX",748);a.Round(a.Mat("Progress"),a.R("Progress"),43);
            a.Erase(a.Mat("Track"),new Rect(141,1575,577,43));a.Mat("Track").SetVector("_SamplePoint",new Vector4(656,1596,1,0));a.Mat("Track").SetFloat("_EraseRadius",22);a.Round(a.Mat("Track"),a.R("Track"),28);
            a.Erase(a.Mat("Fill"),new Rect(391,1576,66,42));a.Mat("Fill").SetFloat("_SampleX",280);a.Round(a.Mat("Fill"),a.R("Fill"),22);
            a.Erase(a.Mat("InformationCard"),new Rect(62,1417,721,69));a.Mat("InformationCard").SetVector("_SamplePoint",new Vector4(540,1445,1,0));a.Mat("InformationCard").SetFloat("_EraseFeather",1);a.Round(a.Mat("InformationCard"),a.R("InformationCard"),43);
            a.Mat("Panel").SetFloat("_EraseRadius",48);a.Round(a.Mat("CoinsFull"),a.R("CoinsFull"),151);a.Round(a.Mat("CashFull"),a.R("CashFull"),151);Build(a);AssetDatabase.SaveAssets();
        }
        static void Blank(ReferencePrefabTools a,string n,Rect slot,float x,float radius){var m=a.Material(n+"Blank");a.Erase(m,slot);m.SetFloat("_SampleX",x);a.Round(m,a.R(n),radius);a.Round(a.Mat(n),a.R(n),radius);}
        static void Build(ReferencePrefabTools a)
        {
            const string path="Assets/BizzaWZ/Final/Real/UI/GetRewardPanel/GetRewardPanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<GetRewardPanel>();var pay=p.progress.payCfg;var us=p.progress.USMission;var id=p.progress.IDMission;var t=go.transform;Clear(t);Stretch(t);a.Graphic(t,"Backdrop");var panel=a.Graphic(t,"Panel");a.Place(panel,new Rect(18,644,813,1170));a.Graphic(t,"Header").GetComponent<Image>().material=a.Mat("HeaderBlank");var title=a.Text(t,"HeaderCaption",new Rect(231,110,401,75),53,color:Color.white);Localize(title,"seq_level_reward");a.Caption(t,title,"Header","Level reward");
                var level=Child(t,"LevelRoot");Stretch(level);p.LevelObj=level.gameObject;a.Graphic(level,"Level").GetComponent<Image>().material=a.Mat("LevelBlank");p.levelTxt=a.Text(level,"LevelText",new Rect(317,205,215,54),40,color:Color.white);a.Caption(level,p.levelTxt,"Level","LEVEL 24");var pageData=new SerializedObject(p);pageData.FindProperty("alwaysShowLevel").boolValue=true;pageData.FindProperty("normalCollectRevealDelay").floatValue=3;pageData.ApplyModifiedPropertiesWithoutUndo();
                var rt=a.Text(t,"RewardTitle",new Rect(243,682,364,58),48);Localize(rt,"seq_your_reward");a.Caption(t,rt,"RewardTitle","Your Reward");a.Graphic(t,"Coins");
                var cash=Child(t,"CashReward");Stretch(cash);Ensure<SingleCurrencyMode>(cash);a.Graphic(cash,"Cash");
                p.itemATxt=a.Text(t,"CoinsAmount",new Rect(141,929,249,79),77,color:new Color(1,.55f,0));p.itemBTxt=a.Text(cash,"CashAmount",new Rect(458,930,250,86),68,color:new Color(0,.43f,0));p.itemAPos=p.itemATxt.transform;p.itemBPos=p.itemBTxt.transform;
                p.claimBtn=a.Button(t,"Claim","Claim",key:"seq_claim",caption:"Claim x2",size:66);p.rewardText=p.claimBtn.transform.Find("ClaimLabel").GetComponent<TMP_Text>();a.Local(p.rewardText.transform,new Rect(232,30,365,104));
                p.closeBtn=a.Button(t,"Collect","Collect",key:"seq_collect",caption:"Collect $0.25",size:49);p.noThanksText=p.closeBtn.transform.Find("CollectLabel").GetComponent<TMP_Text>();
                // Keep the original bonus badge conditional, without leaving a whole empty row at 0%.
                var rate=Child(t,"BonusRate");Stretch(rate);p.bonusRate=Ensure<BonusRate>(rate);Card(a,rate,"RateBadge",new Rect(615,1064,144,61));p.bonusRate.bonusRateTxt=a.Text(rate,"RateValue",new Rect(624,1071,127,45),34,color:new Color(0,.4f,.05f));p.bonusRate.coin_normal=Ensure<Image>(Child(rate,"Normal"));p.bonusRate.coin_normal.color=Color.clear;p.bonusRate.coin_normal.raycastTarget=false;p.bonusRate.coin_normal.gameObject.SetActive(false);p.bonusRate.coin_Bubble=Ensure<Image>(Child(rate,"Bubble"));p.bonusRate.coin_Bubble.color=Color.clear;p.bonusRate.coin_Bubble.raycastTarget=false;
                BuildAdProgress(a,t);
                var progress=Child(t,"WithdrawalProgress");Stretch(progress);Ensure<SingleCurrencyMode>(progress);p.progress=Ensure<WithdrawProgress>(progress);p.progress.payCfg=pay;p.progress.USMission=us;p.progress.IDMission=id;
                ProgressCard(a,progress,out var fill,out var payment,out var hint);
                p.progress.progressImg=fill;p.progress.paymentImg=payment;p.progress.progressTxt=hint;p.progress.percentageText=a.Text(progress,"Percent",new Rect(224,1677,399,40),29,color:Color.white);
                var d=new SerializedObject(p.progress);d.FindProperty("percentOnly").boolValue=false;d.ApplyModifiedPropertiesWithoutUndo();
                var real=Child(t,"RealWithdrawalProgress");Stretch(real);var mode=Ensure<SingleCurrencyMode>(real);var m=new SerializedObject(mode);m.FindProperty("isReverse").boolValue=true;m.ApplyModifiedPropertiesWithoutUndo();var realProgress=Ensure<Real_AdWatchProgress>(real);realProgress.payCfg=pay;ProgressCard(a,real,out fill,out payment,out hint);realProgress.progressImg=fill;realProgress.paymentImg=payment;realProgress.hintTxt=hint;
                p.levelTips=level.gameObject;
                var max=Child(t,"MaxDollarTip");Stretch(max);Card(a,max,"MaxBadge",new Rect(324,767,90,44));a.Text(max,"MaxLabel",new Rect(329,768,80,40),28,color:new Color(.8f,.3f,0)).text="MAX";p.MaxDollarTip=max.gameObject;
                RewardDisplayRepair.ConfigurePrefab(go);Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void Card(ReferencePrefabTools a,Transform parent,string name,Rect box)
        {
            var card=a.Graphic(parent,name,"InformationCard");a.Place(card,box);a.Sliced(card.GetComponent<Image>());
        }
        static Image Bar(ReferencePrefabTools a,Transform parent,Rect box)
        {
            var track=a.Graphic(parent,"Track");a.Place(track,box);a.Sliced(track.GetComponent<Image>());
            var fill=a.Graphic(parent,"Fill").GetComponent<Image>();a.Place(fill.transform,new Rect(box.x+7,box.y+6,box.width-14,box.height-12));a.Sliced(fill);Ensure<WithdrawCloudProgressFill>(fill);return fill;
        }
        static void BuildAdProgress(ReferencePrefabTools a,Transform parent)
        {
            var root=Child(parent,"AdBonusProgress");Stretch(root);var progress=Ensure<WathAdProgress>(root);
            Card(a,root,"AdBonusCard",new Rect(46,1395,755,215));
            Localize(a.Text(root,"AdBonusTitle",new Rect(95,1405,660,47),32),"seq_video_bonus");
            progress.startText=a.Text(root,"CurrentBonus",new Rect(69,1460,133,45),34,color:new Color(0,.43f,0));
            progress.endText=a.Text(root,"NextBonus",new Rect(648,1460,133,45),34,color:new Color(0,.43f,0));
            progress.progressBar=Bar(a,root,new Rect(204,1464,440,43));
            progress.progressText=a.Text(root,"VideoCount",new Rect(220,1464,408,43),29,color:Color.white);
            Localize(a.Text(root,"CurrentBonusCaption",new Rect(58,1506,155,37),22),"seq_current_bonus");
            Localize(a.Text(root,"NextBonusCaption",new Rect(641,1506,155,37),22),"seq_next_bonus");
            progress.hintText=a.Text(root,"VideoHint",new Rect(79,1546,690,53),26);progress.hintText.enableWordWrapping=true;
        }
        static void ProgressCard(ReferencePrefabTools a,Transform root,out Image fill,out Image payment,out TMP_Text hint)
        {
            Card(a,root,"ProgressCard",new Rect(46,1618,755,163));
            Localize(a.Text(root,"ProgressCaption",new Rect(80,1627,606,43),31,TextAlignmentOptions.MidlineLeft),"seq_withdraw_progress");
            payment=Ensure<Image>(Child(root,"PaymentMethod"));a.Place(payment.transform,new Rect(702,1628,60,43));payment.preserveAspect=true;payment.raycastTarget=false;
            fill=Bar(a,root,new Rect(90,1676,665,43));
            hint=a.Text(root,"WithdrawalHint",new Rect(79,1723,690,49),25);hint.enableWordWrapping=true;hint.overrideColorTags=true;
        }
    }
}
