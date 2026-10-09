using System.IO;
using UnityEditor;
using UnityEngine;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class SequentialSwipeAuthoring
    {
        public const string PathName="Assets/BizzaWZ/Final/Framework/Runtime/Module/Teach/UITeach/UITeachFingerMovePage.prefab";
        public static void Apply()
        {
            var a=new ReferencePrefabTools("UI_TeachFinterMove",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/1a430b98c279-29-UI_TeachFinterMove.png")),849,1852);
            a.Slice("Hand",384,1103,107,125);a.Import();var go=PrefabUtility.LoadPrefabContents(PathName);
            try{var p=go.GetComponent<UITeachFingerMovePage>();Clear(go.transform);Stretch(go.transform);var hand=a.Graphic(go.transform,"Hand");SequentialContourAuthoring.Set(a,hand,"Hand",SequentialContourAuthoring.Read("SwipeHand"));p.finger=(RectTransform)hand;p.finger.pivot=new Vector2(.26f,.86f);p.finger.anchoredPosition=Vector2.zero;Validate(go.transform);PrefabUtility.SaveAsPrefabAsset(go,PathName);}finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}
