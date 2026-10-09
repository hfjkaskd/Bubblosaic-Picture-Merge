using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
 public static class SequentialFocusAuthoring
 {
  public const string PathName="Assets/BizzaWZ/Final/Framework/Runtime/Module/Teach/UITeach/UITeachMaskFocusPage.prefab";
  public static void Apply()
  {
   var a=new ReferencePrefabTools("UITeachMaskFocusPage",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/ae34988931db-19-UITeachMaskFocusPage-simple.png")),849,1852);a.Slice("Hand",82,1573,117,171);a.Import();var halo=a.Material("Halo","BubblePics/UI/TutorialFocusHalo");halo.SetColor("_Color",new Color(1,.72f,.1f));halo.SetFloat("_Radius",.34f);
   var go=PrefabUtility.LoadPrefabContents(PathName);try{var p=go.GetComponent<UITeachMaskFocusPage>();var root=go.transform;foreach(var g in root.GetComponentsInChildren<Graphic>(true)){g.raycastTarget=false;if(g.name=="Frame"||g.name=="Mask"||g.name=="CoralFocusOutline"||g.name=="CoralFinger")g.enabled=false;}foreach(var s in root.GetComponentsInChildren<Spine.Unity.SkeletonGraphic>(true))s.gameObject.SetActive(false);
    var old=p.maskParent.Find("SequentialHand");if(old!=null)Object.DestroyImmediate(old.gameObject);old=p.maskParent.Find("SequentialHalo");if(old!=null)Object.DestroyImmediate(old.gameObject);
    old=p.maskParent.Find("SequentialCorners");if(old!=null)Object.DestroyImmediate(old.gameObject);var cm=a.Material("Corners","BubblePics/UI/TutorialFocusHalo");cm.SetColor("_Color",Color.white);cm.SetFloat("_DimCorners",1);cm.SetFloat("_CornerRadius",.27f);var corners=Child(p.maskParent,"SequentialCorners");Stretch(corners);var ci=Ensure<Image>(corners);ci.raycastTarget=false;ci.material=cm;Bind(root.GetComponent<TutorialFocusBackdrop>(),"corners",ci);
    var rim=Child(p.maskParent,"SequentialHalo");Stretch(rim);((RectTransform)rim).sizeDelta=new Vector2(60,60);var im=Ensure<Image>(rim);im.material=halo;im.raycastTarget=false;
    var hand=a.Graphic(p.maskParent,"SequentialHand","Hand");SequentialContourAuthoring.Set(a,hand,"Hand",SequentialContourAuthoring.Read("FocusHand"));var hr=(RectTransform)hand;hr.anchorMin=hr.anchorMax=new Vector2(0,.5f);hr.pivot=new Vector2(.8f,.9f);hr.anchoredPosition=new Vector2(0,30);hr.localRotation=Quaternion.Euler(0,0,-35);p.block.color=new Color(.015f,.12f,.22f,.65f);Validate(root);PrefabUtility.SaveAsPrefabAsset(go,PathName);
   }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
  }
 }
}
