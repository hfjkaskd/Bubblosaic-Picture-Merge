using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;

namespace BubblePics.EditorTools
{
    public static class LoseReferenceAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("LosePanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/937e827901b4-03-LosePanel-simple.png")),849,1852);
            a.Slice("Panel",40,557,772,980).Slice("Title",136,468,579,176).Slice("Reason",235,1030,383,70)
                .Slice("Revive",118,1134,616,175).Slice("Restart",137,1315,580,167);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(225,1024,402,82),new Rect(106,1128,638,364));a.Mat("Panel").SetFloat("_SampleX",763);a.Mat("Panel").SetFloat("_EraseFeather",9);a.Round(a.Mat("Panel"),a.R("Panel"),90,1);a.Mat("Panel").SetVector("_TopCurve",new Vector4(425,558,386,90));
            a.Mat("Title").SetFloat("_CoolMatte",1);var titleBlank=a.Material("TitleBlank");a.Erase(titleBlank,new Rect(196,500,456,106));titleBlank.SetFloat("_SampleX",681);titleBlank.SetFloat("_CoolMatte",1);a.Round(titleBlank,a.R("Title"),50,1);
            a.Mat("Reason").SetFloat("_InkOnly",1);
            a.Mat("Panel").SetFloat("_SampleX",740);a.Round(a.Mat("Title"),a.R("Title"),50,1);a.Mat("Title").SetVector("_TopCurve",new Vector4(425,475,289,80));titleBlank.SetVector("_TopCurve",new Vector4(425,475,289,80));
            foreach(string button in new[]{"Revive","Restart"}){var r=a.R(button);a.Round(a.Mat(button),r,79,1);var m=a.Material(button+"Blank");a.Erase(m,new Rect(343,r.y+42,308,95));m.SetFloat("_SampleX",r.x+54);a.Round(m,r,79,1);}
            const string path="Assets/BizzaWZ/Common/BizzaGame/LosePanel/LosePanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page=go.GetComponent<LosePanel>();var tr=go.transform;Clear(tr);Stretch(tr);var rootImage=tr.GetComponent<Image>();if(rootImage!=null)rootImage.enabled=false;var binder=tr.GetComponent<CoralResourceSprite>();if(binder!=null)binder.enabled=false;var anim=tr.GetComponent<Animation>();if(anim!=null)anim.enabled=false;
                a.Overlay(tr,new Color(0,.11f,.24f,.18f));a.Graphic(tr,"Panel");var banner=a.Graphic(tr,"Title");banner.GetComponent<Image>().material=titleBlank;
                var title=a.Text(tr,"TitleLabel",new Rect(198,507,456,103),79,color:Color.white);Localize(title,"ui_try_again");a.Caption(tr,title,"Title","Try again!","TitleBlank");UnityEngine.Object.DestroyImmediate(banner.gameObject);
                title.transform.SetAsLastSibling();
                var reason=a.Text(tr,"Reason",new Rect(179,1023,493,95),62);Localize(reason,"out_of_moves");Bind(page,"reasonLabel",reason.GetComponent<CoralLocalizedLabel>());
                var available=Child(tr,"ReviveAvailable");Stretch(available);page.reviveButton=a.Button(available,"Revive","Revive",key:"sequential_revive",caption:"Revive",size:67);page.bizzaLoseButton1=a.Button(available,"Restart","Restart",key:"restart",caption:"Restart",size:65);
                foreach(var b in new[]{page.reviveButton,page.bizzaLoseButton1}){var label=b.GetComponentInChildren<TMP_Text>();var r=(RectTransform)label.transform;r.anchorMin=new Vector2(.37f,.2f);r.anchorMax=new Vector2(.9f,.8f);r.offsetMin=r.offsetMax=Vector2.zero;}
                var unavailable=Child(tr,"ReviveUnavailable");Stretch(unavailable);page.bizzaLoseButton2=a.Button(unavailable,"Restart","Restart",new Rect(137,1280,580,167),"restart","Restart",65);Stretch(page.bizzaLoseButton2.transform.Find("ReferenceRestart"));var native=(RectTransform)page.bizzaLoseButton2.GetComponentInChildren<TMP_Text>().transform;native.anchorMin=new Vector2(.37f,.2f);native.anchorMax=new Vector2(.9f,.8f);native.offsetMin=native.offsetMax=Vector2.zero;
                page.reviveObjs=new[]{available.gameObject};page.loseObjs=new[]{unavailable.gameObject};Validate(tr);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}
