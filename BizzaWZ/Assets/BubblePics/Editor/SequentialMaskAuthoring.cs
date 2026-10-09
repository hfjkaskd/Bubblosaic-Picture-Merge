using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
 public static class SequentialMaskAuthoring
 {
  public static void Apply()
  {
   var a=new ReferencePrefabTools("UI_TeachMask",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/219b99cd8f5f-31-UI_TeachMask.png")),849,1852);a.Slice("Hand",705,158,125,145);a.Import();var halo=a.Material("Halo","BubblePics/UI/TutorialFocusHalo");halo.SetColor("_Color",new Color(1,.72f,.1f));halo.SetFloat("_Radius",.38f);
   const string path="Assets/BizzaWZ/Final/Framework/Runtime/Module/Teach/UITeach/UITeachMaskPage.prefab";var go=PrefabUtility.LoadPrefabContents(path);
   try{var p=go.GetComponent<UITeachMaskPage>();var t=go.transform;Clear(t);Stretch(t);var backdrop=Ensure<TutorialFocusBackdrop>(t);Bind(backdrop,"viewport",t);var buttons=new Button[4];string[] names={"top","bottom","left","right"};
    for(int i=0;i<4;i++){var dim=Child(t,"Dim"+i);var rt=(RectTransform)dim;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);var im=Ensure<Image>(dim);im.color=new Color(.015f,.12f,.22f,.65f);var b=Ensure<Button>(dim);b.targetGraphic=im;b.transition=Selectable.Transition.None;buttons[i]=b;Bind(backdrop,names[i],im);if(i==0)p.block=im;}
    var focus=Child(t,"Focus");p.maskParent=(RectTransform)focus;p.maskParent.anchorMin=p.maskParent.anchorMax=Vector2.zero;p.maskParent.pivot=new Vector2(.5f,.5f);p.maskParent.sizeDelta=new Vector2(120,120);Bind(backdrop,"focus",focus);
    var cm=a.Material("Corners","BubblePics/UI/TutorialFocusHalo");cm.SetColor("_Color",Color.white);cm.SetFloat("_DimCorners",1);cm.SetFloat("_CornerRadius",.5f);var oldMask=Child(focus,"ShapeCorners");Stretch(oldMask);p.imgMask=Ensure<Image>(oldMask);p.imgMask.material=cm;p.imgMask.raycastTarget=false;Bind(backdrop,"corners",p.imgMask);
    var rim=Child(focus,"Halo");Stretch(rim);((RectTransform)rim).sizeDelta=new Vector2(60,60);var ring=Ensure<Image>(rim);ring.material=halo;ring.raycastTarget=false;
    var hand=a.Graphic(focus,"Hand");p.finger=SequentialContourAuthoring.Set(a,hand,"Hand",SequentialContourAuthoring.Read("MaskHand"));var hr=(RectTransform)hand;hr.anchorMin=hr.anchorMax=new Vector2(1,0);hr.pivot=new Vector2(.12f,.95f);hr.anchoredPosition=Vector2.zero;
    var data=new SerializedObject(p);var array=data.FindProperty("backgroundButtons");array.arraySize=4;for(int i=0;i<4;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=buttons[i];data.ApplyModifiedPropertiesWithoutUndo();Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
   }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
  }
 }
}
