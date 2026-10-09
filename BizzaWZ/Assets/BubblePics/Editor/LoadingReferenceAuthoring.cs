using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;

namespace BubblePics.EditorTools
{
    public static class LoadingReferenceAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("LoadingPanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/0777fe746699-05-LoadingPanel-simple.png")),849,1852);
            a.Slice("Background",0,0,849,1852).Slice("Track",81,1500,687,110).Slice("Fill",103,1520,465,75,new Vector4(39,18,37,18)).Slice("Percent",365,1530,117,52).Slice("Loading",294,1625,259,54);
            a.Import();a.Erase(a.Mat("Track"),new Rect(101,1516,647,82));a.Mat("Track").SetFloat("_SampleX",671);a.Mat("Track").SetFloat("_EraseRadius",39);a.Mat("Track").SetFloat("_EraseFeather",.5f);a.Mat("Percent").SetFloat("_WarmMatte",1);
            a.Erase(a.Mat("Fill"),new Rect(365,1530,117,52));a.Mat("Fill").SetFloat("_SampleX",314);a.Round(a.Mat("Fill"),a.R("Fill"),36,1);
            var translated=a.Material("TranslatedBackground");a.Erase(translated,new Rect(282,1618,291,67));translated.SetFloat("_SampleY",1697);translated.SetFloat("_EraseFeather",8);
            const string path="Assets/BizzaWZ/Common/MenuSystem/Common/LoadingPanel/LoadingPanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page=go.GetComponent<LoadingPanel>();var visual=go.GetComponentInChildren<SplashPage>(true);var instance=PrefabUtility.GetOutermostPrefabInstanceRoot(visual.gameObject);if(instance!=null)PrefabUtility.UnpackPrefabInstance(instance,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var data=new SerializedObject(visual);var root=(RectTransform)data.FindProperty("_root").objectReferenceValue;var fill=(RectTransform)data.FindProperty("_progressFill").objectReferenceValue;fill.SetParent(root,false);
                var previous=root.Find("ReferenceLoading");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
                foreach(Transform child in root)child.gameObject.SetActive(false);
                var art=Child(root,"ReferenceLoading");Stretch(art);var background=a.Graphic(art,"Background");a.Graphic(art,"Track");
                fill.SetParent(art,false);fill.gameObject.SetActive(true);Clear(fill);var raw=fill.GetComponent<RawImage>();if(raw!=null)raw.enabled=false;a.Place(fill,new Rect(103,1520,642,75));fill.pivot=new Vector2(0,.5f);fill.anchoredPosition-=new Vector2(321*a.SX,0);
                var fg=Child(fill,"Artwork");Stretch(fg);var image=a.Visual(fg,"Fill");a.Sliced(image);
                var percent=a.Text(art,"Percent",new Rect(314,1520,222,75),56,TextAlignmentOptions.Midline,Color.white);percent.text="0%";percent.enableAutoSizing=false;
                var fontMaterial=a.Material("PercentFont","TextMeshPro/Distance Field");fontMaterial.CopyPropertiesFromMaterial(a.Font.material);
                fontMaterial.SetColor("_FaceColor",Color.white);fontMaterial.SetFloat("_FaceDilate",0);
                fontMaterial.SetColor("_OutlineColor",new Color(.015f,.02f,.31f));fontMaterial.SetFloat("_OutlineWidth",.035f);fontMaterial.SetFloat("_OutlineSoftness",0);
                EditorUtility.SetDirty(fontMaterial);percent.fontSharedMaterial=fontMaterial;percent.UpdateMeshPadding();
                var loading=a.Text(art,"LoadingLabel",new Rect(274,1622,301,68),49,color:new Color(.005f,.19f,.5f));Localize(loading,"LOADING");var group=Ensure<CanvasGroup>(loading);group.blocksRaycasts=false;var caption=new SerializedObject(Ensure<ApprovedHudCaption>(background));caption.FindProperty("_label").objectReferenceValue=loading;caption.FindProperty("_surface").objectReferenceValue=background.GetComponent<Image>();caption.FindProperty("_authoredCaption").stringValue="Loading…";caption.FindProperty("_captionMaterial").objectReferenceValue=a.Mat("Background");caption.FindProperty("_translatedMaterial").objectReferenceValue=translated;caption.FindProperty("_captionGroup").objectReferenceValue=group;caption.ApplyModifiedPropertiesWithoutUndo();
                data.Update();data.FindProperty("_backgroundResourcePath").stringValue="";data.FindProperty("_animatedDecorations").boolValue=false;data.FindProperty("_progressWidth").floatValue=642*a.SX;data.FindProperty("_progressHeight").floatValue=75*a.SY;data.FindProperty("_progressLabel").objectReferenceValue=percent;data.ApplyModifiedPropertiesWithoutUndo();
                page.CameraObj=null;page.busRect=null;Validate(go.transform);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
            LoadingBrandAuthoring.Apply();
        }
    }
}
