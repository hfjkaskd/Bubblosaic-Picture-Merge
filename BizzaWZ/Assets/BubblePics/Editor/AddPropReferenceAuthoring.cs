using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class AddPropReferenceAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("AddPropPanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/dc423974e186-08-AddPropPanel-simple.png")),849,1852);
            a.Slice("Panel",19,429,811,1030).Slice("Title",171,481,506,141).Slice("TitleInk",224,510,408,73).Slice("Close",693,541,123,127)
                .Slice("Hint",220,648,410,417).Slice("Free",166,1160,520,167).Slice("Description",133,1080,595,57);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(203,645,443,428),new Rect(122,1077,617,66),new Rect(154,1153,550,185),new Rect(374,1337,106,53));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(742,1151,1,0));a.Mat("Panel").SetFloat("_EraseFeather",7);a.Round(a.Mat("Panel"),a.R("Panel"),81);a.Mat("Panel").SetVector("_TopCurve",new Vector4(424,429,405,55));
            var titleBlank=a.Material("TitleBlank");a.Erase(titleBlank,new Rect(215,509,418,77));titleBlank.SetFloat("_SampleX",642);a.Round(titleBlank,a.R("Title"),66);a.Round(a.Mat("Title"),a.R("Title"),66);a.Round(a.Mat("Close"),a.R("Close"),62);
            a.Round(a.Mat("Hint"),a.R("Hint"),204);a.Round(a.Mat("Free"),a.R("Free"),80);var blank=a.Material("FreeBlank");a.Erase(blank,new Rect(384,1200,215,83));blank.SetFloat("_SampleX",618);a.Round(blank,a.R("Free"),80);a.Mat("Description").SetFloat("_InkOnly",1);
            const string path="Assets/BizzaWZ/Final/Real/UI/AddPropPanel/AddPropPanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<AddPropPanel>();var t=go.transform;Clear(t);Stretch(t);a.Overlay(t,new Color(0,.12f,.24f,.16f));a.Graphic(t,"Panel");a.Graphic(t,"Title").GetComponent<Image>().material=titleBlank;p.propName=a.Text(t,"TitleCaption",new Rect(216,511,418,75),60,color:Color.white);a.Caption(t,p.propName,"Title","Need a hint?");
                p.closeBtn=a.Button(t,"Close","Close");var hint=a.Graphic(t,"Hint");Bind(p,"authoredHintVisual",hint.gameObject);
                var icon=Child(t,"OtherTool");a.Place(icon,new Rect(281,714,287,287));p.propIcon=Ensure<Image>(icon);p.propIcon.preserveAspect=true;p.propIcon.raycastTarget=false;
                var desc=a.Text(t,"Description",new Rect(127,1076,601,64),39);desc.enableWordWrapping=true;Bind(p,"descriptionText",desc);
                p.adBuyBtn=a.Button(t,"Free","Free",key:"seq_free",caption:"Free",size:68);a.Local(p.adBuyBtn.transform.Find("FreeLabel"),new Rect(202,35,260,104));p.limitTxt=a.Text(t,"Limit",new Rect(275,1330,300,66),46);Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}
