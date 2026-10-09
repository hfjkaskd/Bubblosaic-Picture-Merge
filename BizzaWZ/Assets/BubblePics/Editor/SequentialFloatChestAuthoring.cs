using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
 public static class SequentialFloatChestAuthoring
 {
  public static void Apply()
  {
   var a=new ReferencePrefabTools("UIBizzaAAA",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/4e9d8fc3c357-24-UIBizzaAAA-simple.png")),849,1852);a.Slice("Chest",666,782,169,173).Slice("Video",768,885,62,63);a.Import();a.Mat("Chest").SetFloat("_BlueMatte",1);a.Round(a.Mat("Chest"),a.R("Chest"),81);a.Round(a.Mat("Video"),a.R("Video"),31);
   const string path="Assets/BizzaWZ/Final/Real/UI/GamePanel/UIBizzaAAA.prefab";var go=PrefabUtility.LoadPrefabContents(path);
   try{var p=go.GetComponent<UIBizzaAAA>();var t=go.transform;Clear(t);Stretch(t);var chest=a.Graphic(t,"Chest");var image=chest.GetComponent<Image>();image.raycastTarget=true;var b=Ensure<Button>(chest);b.targetGraphic=image;b.transition=Selectable.Transition.None;Bind(p,"btnFlowTreature",b);var video=a.Graphic(t,"Video");video.SetParent(chest,true);var text=a.Text(t,"RewardValue",new Rect(675,933,149,33),28);text.text="";text.transform.SetParent(chest,true);Bind(p,"txtRewardNum",text);Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
   }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
  }
 }
}
