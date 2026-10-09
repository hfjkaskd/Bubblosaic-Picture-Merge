using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
using static BubblePics.EditorTools.SequentialContourAuthoring;
namespace BubblePics.EditorTools
{
    public static class SequentialRatingAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("StarRatingPopup",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/b188576fdedd-14-StarRatingPopup-simple.png")),849,1852);
            a.Slice("Panel",21,727,807,590).Slice("Mascot",278,439,286,337).Slice("Title",138,798,581,71).Slice("Intro",165,879,526,42).Slice("Track",65,944,720,164).Slice("StarOn",100,969,115,116).Slice("StarOff",633,969,117,116).Slice("Rate",213,1125,425,137);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(132,794,585,130),new Rect(62,940,728,329));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(745,890,1,0));a.Mat("Panel").SetFloat("_EraseRadius",58);a.Mat("Panel").SetFloat("_EraseFeather",4);a.Round(a.Mat("Panel"),a.R("Panel"),100);a.Mat("Title").SetFloat("_InkOnly",1);a.Mat("Intro").SetFloat("_InkOnly",1);
            a.Erase(a.Mat("Track"),new Rect(98,967,652,119));a.Mat("Track").SetFloat("_SampleX",754);a.Mat("Track").SetFloat("_EraseFeather",4);a.Round(a.Mat("Track"),a.R("Track"),48);
            var rate=a.Material("RateBlank");a.Erase(rate,new Rect(345,1160,165,69));rate.SetFloat("_SampleX",551);rate.SetFloat("_EraseFeather",3);a.Round(rate,a.R("Rate"),64);a.Round(a.Mat("Rate"),a.R("Rate"),64);
            const string path="Assets/BizzaWZ/Final/Real/UI/StarRatingPopup/StarRatingPopup.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<StarRatingPopup>();var t=go.transform;Clear(t);Stretch(t);a.Overlay(t,new Color(0,.11f,.2f,.48f));a.Graphic(t,"Panel");var mascot=a.Graphic(t,"Mascot");Set(a,mascot,"Mascot",Points(a.R("Mascot"),388,445,408,444,438,454,455,473,470,493,471,467,483,471,496,490,506,514,516,533,523,566,521,600,508,629,508,651,485,677,483,699,511,704,530,703,548,712,557,728,554,745,541,762,521,770,502,765,475,750,433,741,401,739,385,762,366,772,344,768,324,756,311,737,312,718,323,704,336,700,328,674,310,685,306,671,311,648,316,633,294,643,292,629,310,607,282,606,288,594,303,582,320,575,300,568,304,551,322,536,346,531,360,516,341,507,320,506,320,495,337,485,356,480,379,480,402,488,392,468));
                var title=a.Text(t,"Title",new Rect(105,788,640,90),65);Localize(title,"seq_enjoying");a.Caption(t,title,"Title","Enjoying the game?");var intro=a.Text(t,"IntroText",new Rect(117,875,615,55),34);Localize(intro,"seq_rating_intro");a.Caption(t,intro,"Intro","Your feedback helps us improve.");a.Graphic(t,"Track");
                p.starButtons=new BizzaButton[5];p.starImages=new Image[5];p.starOn=p.starOff=null;var data=new SerializedObject(p);data.FindProperty("starAtlasPath").stringValue=a.Resource;data.FindProperty("starOnName").stringValue="StarOn";data.FindProperty("starOffName").stringValue="StarOff";data.FindProperty("dimAlpha").floatValue=1;data.ApplyModifiedPropertiesWithoutUndo();
                var starPoints=Points(a.R("StarOn"),151,973,157,969,164,973,172,993,177,1002,201,1005,211,1010,213,1019,208,1027,189,1041,191,1064,190,1075,183,1081,174,1080,156,1068,140,1079,129,1082,121,1078,119,1070,124,1044,105,1028,101,1020,101,1013,107,1007,134,1003,140,988);
                Set(a,mascot,"Mascot",Read("RatingMascot"));starPoints=Read("RatingStar");
                for(int i=0;i<5;i++){var b=a.Button(t,"Star"+(i+1),"StarOn",new Rect(100+i*134,969,115,116));p.starButtons[i]=b;p.starImages[i]=Set(a,b.transform,"StarOn",starPoints);}
                p.goToRateButton=a.Button(t,"Rate","Rate",key:"seq_rate",caption:"Rate",size:65);p.popupRoot=t.gameObject;Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}
