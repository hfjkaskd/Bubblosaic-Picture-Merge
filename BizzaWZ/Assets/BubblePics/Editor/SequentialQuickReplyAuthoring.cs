using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class SequentialQuickReplyAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("ServiceSelectPanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/afdad2cdc485-13-ServiceSelectPanel-simple.png")),849,1852);
            a.Slice("Panel",4,477,840,1226).Slice("Header",108,433,634,149).Slice("Close",729,434,103,106).Slice("Intro",239,601,382,81).Slice("Custom",69,1534,711,128);
            for(int i=0;i<7;i++)a.Slice("Question"+i,40,715+i*118,771,113);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(33,595,784,1069));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(813,685,1,0));a.Mat("Panel").SetFloat("_EraseRadius",55);a.Mat("Panel").SetFloat("_EraseFeather",6);a.Round(a.Mat("Panel"),a.R("Panel"),78);
            var h=a.Material("HeaderBlank");a.Erase(h,new Rect(195,469,464,77));h.SetFloat("_SampleX",682);a.Round(h,a.R("Header"),70);a.Round(a.Mat("Header"),a.R("Header"),70);a.Round(a.Mat("Close"),a.R("Close"),51);a.Mat("Intro").SetFloat("_InkOnly",1);
            var c=a.Material("CustomBlank");a.Erase(c,new Rect(266,1564,478,55));c.SetFloat("_SampleX",253);a.Round(c,a.R("Custom"),60);a.Round(a.Mat("Custom"),a.R("Custom"),60);
            for(int i=0;i<7;i++){var r=a.R("Question"+i);var m=a.Material("Question"+i+"Blank");a.Erase(m,new Rect(184,r.y+29,526,58));m.SetFloat("_SampleX",713);a.Round(m,r,36);a.Round(a.Mat("Question"+i),r,36);}
            const string path="Assets/BizzaWZ/Final/Real/UI/ServiceSelectPanel/ServiceSelectPanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<ServiceSelectPanel>();var t=go.transform;Clear(t);Stretch(t);a.Overlay(t,new Color(.1f,.5f,.7f,.15f));a.Graphic(t,"Panel");a.Graphic(t,"Header").GetComponent<Image>().material=h;
                var title=a.Text(t,"Title",new Rect(182,462,490,92),66,color:Color.white);Localize(title,"seq_quick_replies");a.Caption(t,title,"Header","Quick replies");
                var intro=a.Text(t,"IntroText",new Rect(155,598,540,81),35);intro.enableWordWrapping=true;Localize(intro,"seq_quick_intro");a.Caption(t,intro,"Intro","Choose a question below\nto get an instant answer.");
                p.defaultButtons=new BizzaButton[7];p.defaultTexts=new TMP_Text[7];
                for(int i=0;i<7;i++){var b=a.Button(t,"Question"+i,"Question"+i,key:"ServicePanel_Q"+(i+1),caption:null,size:32);var label=b.transform.Find("Question"+i+"Label").GetComponent<TMP_Text>();Object.DestroyImmediate(label.GetComponent<CoralLocalizedLabel>());var lang=Ensure<UILanguageLabel>(label);BindString(lang,"key","ServicePanel_Q"+(i+1));var r=(RectTransform)label.transform;Stretch(r);r.offsetMin=new Vector2(144*a.SX,8*a.SY);r.offsetMax=new Vector2(-91*a.SX,-8*a.SY);label.alignment=TextAlignmentOptions.MidlineLeft;label.color=new Color(.025f,.02f,.32f);label.enableWordWrapping=true;p.defaultButtons[i]=b;p.defaultTexts[i]=label;}
                p.customButton=a.Button(t,"Custom","Custom",key:"seq_own_question",caption:"Write my own question",size:44);var tx=p.customButton.transform.Find("CustomLabel").GetComponent<TMP_Text>();Stretch(tx.transform);tx.margin=new Vector4(184*a.SX,0,26*a.SX,0);Bind(p,"closeBtn",a.Button(t,"Close","Close"));Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
        static void BindString(Object target,string property,string value){var o=new SerializedObject(target);o.FindProperty(property).stringValue=value;o.ApplyModifiedPropertiesWithoutUndo();}
    }
}
