using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using SnakeEscape.Recovered;
using static BubblePics.EditorTools.ReferencePrefabTools;

namespace BubblePics.EditorTools
{
    public static class VictoryReferenceAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("WhiteWinPanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/d536948774fe-04-WhiteWinPanel-simple.png")),849,1852);
            a.Slice("Panel",78,883,695,464).Slice("Title",152,997,544,94).Slice("Next",173,1246,504,173);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(124,883,595,120),new Rect(145,994,563,100),new Rect(179,1120,493,93),new Rect(165,1241,520,105));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(720,1100,1,0));a.Mat("Panel").SetFloat("_EraseFeather",10);a.Round(a.Mat("Panel"),a.R("Panel"),81,1);a.Mat("Panel").SetVector("_TopCurve",new Vector4(425,885,347,78));
            a.Mat("Title").SetFloat("_InkOnly",1);a.Round(a.Mat("Next"),a.R("Next"),82,1);var blank=a.Material("NextBlank");a.Erase(blank,new Rect(264,1282,327,88));blank.SetFloat("_SampleX",220);a.Round(blank,a.R("Next"),82,1);
            const string path="Assets/BizzaWZ/Common/BizzaGame/WhiteWinPanel/WhiteWinPanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page=go.GetComponent<RecoveredVictoryPanel>();var tr=go.transform;Clear(tr);Stretch(tr);Bind(page,"canvasGroup",Ensure<CanvasGroup>(tr));a.Overlay(tr,new Color(0,.11f,.24f,.18f));var card=a.Graphic(tr,"Panel");Bind(page,"card",(RectTransform)card);
                var hero=Child(tr,"VictoryHero");a.Place(hero,new Rect(130,525,561,459));var heroImage=Ensure<Image>(hero);heroImage.raycastTarget=false;var data=new SerializedObject(Ensure<CoralResourceSprite>(hero));data.FindProperty("_resourcePath").stringValue="AllUI20260924/CharacterAtlas";data.FindProperty("_spriteName").stringValue="Victory";data.FindProperty("_image").objectReferenceValue=heroImage;data.ApplyModifiedPropertiesWithoutUndo();Bind(page,"hero",(RectTransform)hero);
                var title=a.Text(tr,"Title",new Rect(144,997,566,99),72);Localize(title,"ui_level_complete");a.Caption(tr,title,"Title","Level complete!");Bind(page,"titleText",title);
                var body=a.Text(tr,"Body",new Rect(158,1127,534,77),46);Localize(body,"ui_puzzle_complete");Bind(page,"bodyText",body);
                var next=a.Button(tr,"Next","Next",key:"ui_next_level",caption:"Next level",size:58);Bind(page,"nextButton",next.GetComponent<Button>());Validate(tr);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}
