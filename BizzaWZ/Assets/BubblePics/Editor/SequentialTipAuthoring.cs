using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
 public static class SequentialTipAuthoring
 {
  public static void Apply()
  {
   var a=new ReferencePrefabTools("UI_TeachTip",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/a3f9dc2c76dc-21-UI_TeachTip-simple.png")),849,1852);a.Slice("Panel",208,394,630,181);a.Import();a.Erase(a.Mat("Panel"),new Rect(285,437,505,56));a.Mat("Panel").SetFloat("_SampleY",434);a.Mat("Panel").SetFloat("_EraseFeather",3);
   const string path="Assets/BizzaWZ/Final/Framework/Runtime/Module/Teach/UITeach/UITeachTipsPage.prefab";var go=PrefabUtility.LoadPrefabContents(path);
   try{var p=go.GetComponent<UITeachTipsPage>();var t=go.transform;Clear(t);Stretch(t);p.bg=a.Overlay(t,Color.clear);var bg=Ensure<Button>(p.bg);bg.targetGraphic=p.bg;bg.transition=Selectable.Transition.None;Bind(p,"backdropButton",bg);
    var panel=a.Graphic(t,"Panel");p.panel=SequentialContourAuthoring.Set(a,panel,"Panel",SequentialContourAuthoring.Points(a.R("Panel"),256,402,787,402,803,405,818,415,826,429,831,447,831,480,826,501,816,516,801,525,679,530,635,566,619,531,255,531,237,527,223,516,215,501,210,482,210,447,215,430,225,415,239,405));p.panel.raycastTarget=true;var button=Ensure<Button>(panel);button.targetGraphic=p.panel;button.transition=Selectable.Transition.None;Bind(p,"dismissButton",button);
    p.content=a.Text(t,"Content",new Rect(286,423,505,79),40);p.content.enableWordWrapping=true;p.content.transform.SetParent(panel,true);
    p.panels=new RectTransform[3];float[] heights={.82f,.5f,.22f};for(int i=0;i<3;i++){var marker=(RectTransform)Child(t,"Position"+i);marker.anchorMin=marker.anchorMax=new Vector2(.5f,heights[i]);marker.anchoredPosition=Vector2.zero;marker.sizeDelta=Vector2.zero;p.panels[i]=marker;}
    Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
   }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
  }
 }
}
