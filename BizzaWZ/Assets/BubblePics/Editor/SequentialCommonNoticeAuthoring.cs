using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class SequentialCommonNoticeAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("CommonConfirmTipsPanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/308370e4cc87-22-CommonConfirmTipsPanel.png")),849,1852);
            a.Slice("Panel",32,618,786,647).Slice("Header",107,546,634,139).Slice("Confirm",209,1070,435,133);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(169,940,515,103),new Rect(204,1065,446,141));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(667,1039,1,0));a.Mat("Panel").SetFloat("_EraseFeather",4);a.Round(a.Mat("Panel"),a.R("Panel"),72);
            var header=a.Material("HeaderBlank");a.Erase(header,new Rect(190,576,469,78));header.SetFloat("_SampleX",181);header.SetFloat("_EraseFeather",4);header.SetFloat("_ChromaBlue",1);a.Mat("Header").SetFloat("_ChromaBlue",1);
            var blank=a.Material("ConfirmBlank");a.Erase(blank,new Rect(295,1103,264,68));blank.SetFloat("_SampleX",584);blank.SetFloat("_EraseFeather",3);a.Round(blank,a.R("Confirm"),65);a.Round(a.Mat("Confirm"),a.R("Confirm"),65);
            const string path="Assets/BizzaWZ/Final/FunctionTools/CommonPublic/UI/CommonConfirmTipsPanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<CommonConfirmTipsPanel>();var t=go.transform;Clear(t);Stretch(t);var animation=go.GetComponent<Animation>();if(animation!=null){animation.playAutomatically=false;animation.enabled=false;}var scaler=Ensure<CanvasScaler>(t);scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(2360f*1080/2340,2360);scaler.matchWidthOrHeight=1;a.Overlay(t,new Color(0,.06f,.11f,.65f));a.Graphic(t,"Panel");a.Graphic(t,"Header").GetComponent<Image>().material=header;p.titleTxt=a.Text(t,"Title",new Rect(178,579,491,79),49,color:Color.white);a.Caption(t,p.titleTxt,"Header","Connection notice");p.desTxt=a.Text(t,"Description",new Rect(137,934,575,117),39);p.desTxt.enableWordWrapping=true;
                var button=a.Button(t,"Confirm","Confirm",key:"seq_confirm",caption:"Try again",size:54);p.confirmBtn=button.GetComponent<Button>();p.btnTxt=button.transform.Find("ConfirmLabel").GetComponent<TMP_Text>();Object.DestroyImmediate(p.btnTxt.GetComponent<CoralLocalizedLabel>());Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}
