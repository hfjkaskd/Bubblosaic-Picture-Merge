using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class NewbieReferenceAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("NewbieGiftPage",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/3d212c5dbd41-10-NewbieGiftPage-simple.png")),849,1852);
            a.Slice("Backdrop",0,0,849,1852).Slice("Title",131,296,590,184).Slice("Amount",177,1085,495,160).Slice("Intro",207,1255,443,103).Slice("Claim",108,1390,625,181);
            a.Import();a.Erase(a.Mat("Backdrop"),new Rect(213,1083,447,166),new Rect(205,1252,448,111));a.Mat("Backdrop").SetFloat("_SampleX",779);a.Mat("Backdrop").SetFloat("_EraseFeather",6);
            var title=a.Material("TitleBlank");a.Erase(title,new Rect(188,335,479,98));title.SetFloat("_SampleX",681);title.SetFloat("_EraseFeather",5);
            a.Mat("Intro").SetFloat("_InkOnly",1);var blank=a.Material("ClaimBlank");a.Erase(blank,new Rect(295,1424,266,98));blank.SetFloat("_SampleX",603);a.Round(blank,a.R("Claim"),86);a.Round(a.Mat("Claim"),a.R("Claim"),86);
            const string path="Assets/BizzaWZ/Final/Real/UI/NewbieGiftPage/NewbieGiftPage.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<NewbieGiftPage>();var t=go.transform;Clear(t);Stretch(t);a.Graphic(t,"Backdrop");a.Graphic(t,"Title").GetComponent<Image>().material=title;var label=a.Text(t,"WelcomeTitle",new Rect(181,332,492,103),64,color:Color.white);Localize(label,"seq_welcome_gift");a.Caption(t,label,"Title","Welcome gift!");
                p.Os_ComText=a.Text(t,"GiftAmount",new Rect(214,1082,447,163),134,color:new Color(0,.4f,.12f));a.Caption(t,p.Os_ComText,"Amount","$0.10");var intro=a.Text(t,"Intro",new Rect(185,1249,488,116),42);intro.enableWordWrapping=true;Localize(intro,"seq_welcome_intro");a.Caption(t,intro,"Intro","A little gift to start\nyour journey.");p.continueBtn=a.Button(t,"Claim","Claim",key:"seq_claim",caption:"Claim",size:85);Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}
