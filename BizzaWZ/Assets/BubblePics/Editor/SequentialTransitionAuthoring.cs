using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
 public static class SequentialTransitionAuthoring
 {
  public static void Apply()
  {
   var a=new ReferencePrefabTools("TransitionBlock",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/4a79538ff7a2-22-TransitionBlock-simple.png")),849,1852);a.Slice("Cover",0,0,849,1852).Slice("Water",466,20,211,136);a.Import();
   var inner=SequentialContourAuthoring.Points(a.R("Cover"),228,491,249,485,284,487,327,500,359,520,385,533,432,529,475,511,525,497,565,498,594,509,607,524,590,548,598,560,631,571,681,567,700,572,730,594,746,626,752,660,762,691,767,735,789,774,785,812,761,832,741,852,733,891,741,927,765,943,774,963,757,982,747,1014,735,1040,696,1064,665,1109,640,1141,626,1177,608,1200,580,1216,562,1238,525,1242,485,1244,466,1234,450,1215,422,1208,401,1223,386,1243,368,1236,348,1216,344,1196,329,1161,322,1137,305,1131,272,1148,257,1127,229,1121,199,1118,192,1090,172,1070,147,1060,97,1047,70,1015,53,977,53,943,70,915,78,904,85,870,79,799,95,747,101,708,103,656,139,632,165,603,176,567,190,530);
   for(int i=0;i<inner.Length;i++){var pt=inner[i];if(pt.x<.25f&&pt.y>.32f&&pt.y<.57f)pt.x-=.032f;if(pt.x>.81f&&pt.y>.34f&&pt.y<.60f)pt.x+=.046f;if(pt.x<.39f&&pt.y>.57f)pt.y+=.026f;if(pt.x>.70f&&pt.y>.58f&&pt.y<.67f)pt.x+=.055f;inner[i]=pt;}
   var smooth=new List<Vector2>();for(int i=0;i<inner.Length;i++){Vector2 p0=inner[(i+inner.Length-1)%inner.Length],p1=inner[i],p2=inner[(i+1)%inner.Length],p3=inner[(i+2)%inner.Length];for(int k=0;k<5;k++){float u=k/5f;smooth.Add(.5f*((2*p1)+(-p0+p2)*u+(2*p0-5*p1+4*p2-p3)*u*u+(-p0+3*p1-3*p2+p3)*u*u*u));}}inner=smooth.ToArray();
   var vertices=new List<Vector2>(inner);var sides=new int[inner.Length];var center=new Vector2(.5f,.47f);
   for(int i=0;i<inner.Length;i++){Vector2 d=inner[i]-center;float tx=d.x>=0?(1-center.x)/d.x:-center.x/d.x,ty=d.y>=0?(1-center.y)/d.y:-center.y/d.y;float k=Mathf.Min(tx,ty);vertices.Add(center+d*k);sides[i]=tx<ty?(d.x>0?1:3):(d.y>0?2:0);}
   vertices.AddRange(new[]{new Vector2(1,0),Vector2.one,new Vector2(0,1),Vector2.zero});var indices=new List<int>();int n=inner.Length;
   for(int i=0;i<n;i++){int j=(i+1)%n;indices.AddRange(new[]{i,j,n+j,i,n+j,n+i});if(sides[i]!=sides[j])indices.AddRange(new[]{n+i,2*n+sides[i],n+j});}
   const string path="Assets/BizzaWZ/Common/MenuSystem/Common/Block/TransitionBlock.prefab";var go=PrefabUtility.LoadPrefabContents(path);
   try{var p=go.GetComponent<TransitionBlock>();Clear(go.transform);Stretch(go.transform);var fill=a.Graphic(go.transform,"ClosureFill","Water");Stretch(fill);Ensure<CanvasGroup>(fill).alpha=0;var frame=Child(go.transform,"Frame");Stretch(frame);var image=Ensure<TransitionCoverImage>(frame);var sprite=new SerializedObject(Ensure<CoralResourceSprite>(frame));sprite.FindProperty("_resourcePath").stringValue=a.Resource;sprite.FindProperty("_spriteName").stringValue="Cover";sprite.FindProperty("_image").objectReferenceValue=image;sprite.ApplyModifiedPropertiesWithoutUndo();image.raycastTarget=true;
    var data=new SerializedObject(image);var v=data.FindProperty("vertices");v.arraySize=vertices.Count;for(int i=0;i<vertices.Count;i++)v.GetArrayElementAtIndex(i).vector2Value=vertices[i];var tr=data.FindProperty("triangles");tr.arraySize=indices.Count;for(int i=0;i<indices.Count;i++)tr.GetArrayElementAtIndex(i).intValue=indices[i];data.FindProperty("innerCount").intValue=n;data.FindProperty("center").vector2Value=center;data.ApplyModifiedPropertiesWithoutUndo();Ensure<CanvasGroup>(frame);
    var open=Clip(a.Root+"Open.anim",true);var close=Clip(a.Root+"Close.anim",false);p.animation=Ensure<Animation>(go.transform);p.animation.playAutomatically=false;var anim=new SerializedObject(p.animation);var clips=anim.FindProperty("m_Animations");clips.arraySize=2;clips.GetArrayElementAtIndex(0).objectReferenceValue=open;clips.GetArrayElementAtIndex(1).objectReferenceValue=close;anim.ApplyModifiedPropertiesWithoutUndo();p.openAnim="Open";p.closeAnim="Close";PrefabUtility.SaveAsPrefabAsset(go,path);
   }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
  }
  static AnimationClip Clip(string path,bool opening)
  {
   var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}clip.ClearCurves();clip.legacy=true;clip.wrapMode=WrapMode.Once;
   clip.SetCurve("ClosureFill",typeof(CanvasGroup),"m_Alpha",opening?new AnimationCurve(new Keyframe(0,0),new Keyframe(.65f,0),new Keyframe(.95f,1)):AnimationCurve.EaseInOut(0,1,.5f,0));
   clip.SetCurve("Frame",typeof(CanvasGroup),"m_Alpha",opening?new AnimationCurve(new Keyframe(0,0),new Keyframe(.2f,1),new Keyframe(.95f,1)):new AnimationCurve(new Keyframe(0,1),new Keyframe(.5f,1),new Keyframe(.8f,0)));EditorUtility.SetDirty(clip);return clip;
  }
 }
}
