using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;

namespace BubblePics.EditorTools
{
    public static class TaskReferenceAuthoring
    {
        const string Dir="Assets/BizzaWZ/Final/MenuSystem/Common/Task/";
        public static void Apply()
        {
            var a=new ReferencePrefabTools("UIDailyTaskPage",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/4df2d8c20e83-06-UIDailyTaskPage-simple.png")),849,1852);
            a.Slice("Background",0,0,849,1852).Slice("Title",233,53,384,104).Slice("Close",719,49,113,116)
                .Slice("Tabs",31,178,787,127).Slice("Daily",52,191,250,102).Slice("Weekly",302,198,247,96).Slice("Playtime",550,199,244,94)
                .Slice("Activity",8,294,832,502).Slice("ActivityTitle",369,399,202,46).Slice("ActivityTrack",130,550,596,66).Slice("ActivityFill",136,560,240,47)
                .Slice("Row",11,800,826,210).Slice("Star",51,833,146,148).Slice("Clock",51,1259,145,150).Slice("Video",51,1471,145,147)
                .Slice("Track",213,1119,334,59).Slice("Fill",220,914,317,40).Slice("Claim",568,860,229,112).Slice("Go",569,1074,228,110).Slice("Claimed",570,1288,225,110).Slice("TitleInk",310,64,233,75);
            a.Import();a.Erase(a.Mat("Background"),new Rect(0,0,849,1852));a.Mat("Background").SetVector("_SamplePoint",new Vector4(425,1707,1,0));a.Mat("Background").SetFloat("_EraseFeather",0);
            a.Erase(a.Mat("Title"),new Rect(302,58,250,84));a.Mat("Title").SetFloat("_SampleX",579);a.Round(a.Mat("Title"),a.R("Title"),49);a.Round(a.Mat("Close"),a.R("Close"),56);
            a.Erase(a.Mat("Tabs"),new Rect(48,187,750,105));a.Mat("Tabs").SetVector("_SamplePoint",new Vector4(800,275,1,0));a.Round(a.Mat("Tabs"),a.R("Tabs"),48);
            foreach(var spec in new[]{new[]{"Daily","165","215","125","60","156"},new[]{"Weekly","398","217","142","58","390"},new[]{"Playtime","634","217","150","60","630"}})
            {var m=a.Material(spec[0]+"Blank");a.Erase(m,new Rect(float.Parse(spec[1]),float.Parse(spec[2]),float.Parse(spec[3]),float.Parse(spec[4])));m.SetFloat("_SampleX",float.Parse(spec[5]));a.Round(m,a.R(spec[0]),42);}
            a.Erase(a.Mat("Activity"),new Rect(242,323,367,53),new Rect(355,399,282,140),new Rect(125,543,607,219));a.Mat("Activity").SetVector("_SamplePoint",new Vector4(690,493,1,0));a.Mat("Activity").SetFloat("_EraseFeather",15);a.Round(a.Mat("Activity"),a.R("Activity"),57);
            a.Mat("ActivityTitle").SetFloat("_InkOnly",1);
            a.Erase(a.Mat("ActivityTrack"),new Rect(137,558,568,48));a.Mat("ActivityTrack").SetVector("_SamplePoint",new Vector4(672,585,1,0));a.Round(a.Mat("ActivityTrack"),a.R("ActivityTrack"),29);
            a.Erase(a.Mat("ActivityFill"),new Rect(136,560,240,47));a.Mat("ActivityFill").SetVector("_SamplePoint",new Vector4(260,582,1,0));a.Round(a.Mat("ActivityFill"),a.R("ActivityFill"),23);
            a.Erase(a.Mat("Row"),new Rect(46,826,762,161));a.Mat("Row").SetVector("_SamplePoint",new Vector4(510,979,1,0));a.Mat("Row").SetFloat("_EraseFeather",14);a.Round(a.Mat("Row"),a.R("Row"),53);
            a.Erase(a.Mat("Track"),new Rect(219,1124,322,48));a.Mat("Track").SetVector("_SamplePoint",new Vector4(501,1148,1,0));a.Round(a.Mat("Track"),a.R("Track"),29);
            a.Erase(a.Mat("Fill"),new Rect(327,914,154,40));a.Mat("Fill").SetFloat("_SampleX",280);a.Round(a.Mat("Fill"),a.R("Fill"),20);a.Mat("TitleInk").SetFloat("_BlueMatte",1);
            foreach(string n in new[]{"Claim","Go","Claimed"})
            {Rect r=a.R(n);a.Round(a.Mat(n),r,51);var m=a.Material(n+"Blank");a.Erase(m,new Rect(r.x+33,r.y+23,r.width-66,r.height-48));m.SetVector("_SamplePoint",new Vector4(r.x+r.width*.5f,r.y+19,1,0));m.SetFloat("_EraseFeather",9);a.Round(m,r,51);}
            Row(a);Page(a);AssetDatabase.SaveAssets();
        }
        static void Page(ReferencePrefabTools a)
        {
            string path=Dir+"UIDailyTaskPage.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<UIDailyTaskPage>();var t=go.transform;Clear(t);Stretch(t);var bg=a.Graphic(t,"Background");var bgData=new SerializedObject(bg.GetComponent<CoralResourceSprite>());bgData.FindProperty("_resourcePath").stringValue="CoralV3/CoralBackground";bgData.FindProperty("_spriteName").stringValue="";bgData.ApplyModifiedPropertiesWithoutUndo();bg.GetComponent<Image>().material=null;a.Graphic(t,"Title");p.titleText=a.Text(t,"PageTitle",new Rect(278,62,289,77),66);Localize(p.titleText,"seq_tasks");a.Caption(t,p.titleText,"TitleInk","Tasks");p.closeBtn=a.Button(t,"Close","Close").GetComponent<Button>();
                p.tabDaily=Tab(a,t,"Daily","seq_daily",0);p.tabWeekly=Tab(a,t,"Weekly","seq_weekly",1);p.tabPlaytime=Tab(a,t,"Playtime","seq_playtime",2);
                Bind(p,"dailySelection",p.tabDaily.transform.Find("Selection").gameObject);Bind(p,"weeklySelection",p.tabWeekly.transform.Find("Selection").gameObject);Bind(p,"playtimeSelection",p.tabPlaytime.transform.Find("Selection").gameObject);
                var daily=Child(t,"DailyContent");Stretch(daily);p.DailyRootObj=daily.gameObject;a.Graphic(daily,"Activity");
                var activity=a.Text(daily,"ActivityCaption",new Rect(360,397,280,58),42);Localize(activity,"seq_activity");p.txtActiveCount=a.Text(daily,"ActivityValue",new Rect(355,461,315,83),65);
                p.RefreshTimeText=a.Text(daily,"ActivityNote",new Rect(182,320,506,60),25);Localize(p.RefreshTimeText,"seq_activity_unavailable");
                a.Graphic(daily,"ActivityTrack");var fill=a.Graphic(daily,"ActivityFill");a.Place(fill,new Rect(136,560,579,47));p.activityBar=fill.GetComponent<Image>();p.activityBar.type=Image.Type.Filled;p.activityBar.fillMethod=Image.FillMethod.Horizontal;p.activityBar.fillAmount=0;
                p.activityTaskRoot=(RectTransform)Child(daily,"ActivityRewards");a.Place(p.activityTaskRoot,new Rect(200,635,425,121));
                p.dailyTaskRoot=Scroll(a,daily,"DailyList",new Rect(11,800,826,854));
                var play=Child(t,"PlaytimeContent");Stretch(play);p.PlayTimeRootObj=play.gameObject;p.dailyPlaytimeRoot=Scroll(a,play,"PlaytimeList",new Rect(11,320,826,1334));
                var empty=a.Text(t,"EmptyTasks",new Rect(135,1010,578,130),37);empty.enableWordWrapping=true;Localize(empty,"seq_no_tasks");Bind(p,"emptyText",empty);p.dailyTaskElement=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"UIDailyTaskElement.prefab").GetComponent<UIDailyTaskElement>();Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static BizzaButton Tab(ReferencePrefabTools a,Transform t,string n,string key,int index)
        {
            var b=a.Button(t,n,n);b.GetComponent<Image>().material=a.Mat(n+"Blank");var select=Child(b.transform,"Selection");a.Local(select,new Rect(34,a.R(n).height-6,a.R(n).width-68,4));
            var selection=Ensure<Image>(select);selection.color=new Color(1,.95f,.1f,1);selection.raycastTarget=false;
            var text=a.Text(t,n+"Caption",new Rect(a.R(n).x+81,a.R(n).y+15,a.R(n).width-86,a.R(n).height-23),36,color:Color.white);Localize(text,key);text.transform.SetParent(b.transform,true);return b;
        }
        static RectTransform Scroll(ReferencePrefabTools a,Transform parent,string name,Rect box)
        {
            var root=Child(parent,name);a.Place(root,box);var s=Ensure<ScrollRect>(root);s.horizontal=false;s.movementType=ScrollRect.MovementType.Clamped;
            var viewport=Child(root,"Viewport");Stretch(viewport);Ensure<RectMask2D>(viewport);var hit=Ensure<Image>(viewport);hit.color=Color.clear;hit.raycastTarget=true;
            var content=(RectTransform)Child(viewport,"Content");content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;content.anchoredPosition=Vector2.zero;
            var layout=Ensure<VerticalLayoutGroup>(content);layout.spacing=4*a.SY;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            Ensure<ContentSizeFitter>(content).verticalFit=ContentSizeFitter.FitMode.PreferredSize;s.viewport=(RectTransform)viewport;s.content=content;return content;
        }
        static void Row(ReferencePrefabTools a)
        {
            string path=Dir+"UIDailyTaskElement.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<UIDailyTaskElement>();var t=go.transform;Clear(t);a.Place(t,new Rect(0,0,826,210));Ensure<LayoutElement>(t).preferredHeight=210*a.SY;a.Visual(t,"Row");
                Transform LocalGraphic(string n,string slice,Rect r){var g=Child(t,n);a.Local(g,r);a.Visual(g,slice);return g;}
                p.descTxt=a.Text(t,"Description",default,35,TextAlignmentOptions.MidlineLeft);a.Local(p.descTxt.transform,new Rect(205,38,344,61));p.descTxt.enableWordWrapping=true;
                LocalGraphic("Track","Track",new Rect(202,112,334,59));var fill=LocalGraphic("Fill","Fill",new Rect(210,120,317,44));p.progressBar=fill.GetComponent<Image>();p.progressBar.type=Image.Type.Filled;p.progressBar.fillMethod=Image.FillMethod.Horizontal;
                p.progressTxt=a.Text(t,"Progress",default,34,color:Color.white);a.Local(p.progressTxt.transform,new Rect(208,112,324,59));
                var star=LocalGraphic("Star","Star",new Rect(40,33,146,148));var clock=LocalGraphic("Clock","Clock",new Rect(40,33,146,148));var video=LocalGraphic("Video","Video",new Rect(40,33,146,148));Bind(p,"defaultIcon",star.gameObject);
                var data=new SerializedObject(p);var icons=data.FindProperty("taskIcons");icons.arraySize=2;icons.GetArrayElementAtIndex(0).FindPropertyRelative("type").intValue=(int)E_AllTaskType.OnlineTime;icons.GetArrayElementAtIndex(0).FindPropertyRelative("visual").objectReferenceValue=clock.gameObject;icons.GetArrayElementAtIndex(1).FindPropertyRelative("type").intValue=(int)E_AllTaskType.WatchAd;icons.GetArrayElementAtIndex(1).FindPropertyRelative("visual").objectReferenceValue=video.gameObject;data.ApplyModifiedPropertiesWithoutUndo();
                Button Action(string n,string slice,string key){var b=a.Button(t,n,slice,key:key,caption:slice=="Claim"?"Claim":"Go",size:41);a.Local(b.transform,new Rect(557,60,229,112));foreach(Transform c in b.transform)Stretch(c);return b.GetComponent<Button>();}
                p.btnReward=Action("Claim","Claim","seq_claim");p.btnGoto=Action("Go","Go","seq_go");p.btnAds=Action("VideoGo","Go","seq_go");p.objRewarded=LocalGraphic("Claimed","Claimed",new Rect(557,60,229,112)).gameObject;
                p.objRewardedMask=Child(t,"RewardedMarker").gameObject;p.rewardRoot=Child(t,"RewardContent");p.redPointUI=Ensure<UIRedPoint>(Child(t,"RedPoint"));p.redPointUI.red=Child(p.redPointUI.transform,"Dot").gameObject;
                p.stateObjs=new[]{Child(t,"Complete").gameObject,Child(t,"Uncomplete").gameObject,Child(t,"Rewarded").gameObject};Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
    }
}
