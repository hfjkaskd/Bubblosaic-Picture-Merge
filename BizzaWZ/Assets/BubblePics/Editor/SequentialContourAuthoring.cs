using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static class SequentialContourAuthoring
    {
        [Serializable] sealed class Trace { public Vector2[] points; }
        public static Vector2[] Read(string name)=>JsonUtility.FromJson<Trace>(System.IO.File.ReadAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../../ArtChanges/SequentialUI-20260928/Contours/"+name+".json")))).points;
        public static void ImportSpriteOutlines(ReferencePrefabTools a,string prefix,params string[] names)
        {
            string path=a.Root+"ApprovedSource.png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.Tight;settings.spriteExtrude=0;importer.SetTextureSettings(settings);importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var outline=provider.GetDataProvider<ISpriteOutlineDataProvider>();foreach(var rect in provider.GetSpriteRects())foreach(string name in names)if(rect.name==name){var points=Read(prefix+name);for(int i=0;i<points.Length;i++)points[i]=new Vector2((points[i].x-.5f)*rect.rect.width,(.5f-points[i].y)*rect.rect.height);outline.SetOutlines(rect.spriteID,new List<Vector2[]>{points});}provider.Apply();importer.SaveAndReimport();
        }
        public static ApprovedContourImage Set(ReferencePrefabTools a,Transform t,string slice,Vector2[] points)
        {
            var im=Set(t,points);a.Visual(t,slice);return im;
        }
        public static ApprovedContourImage Set(Transform t,Vector2[] points)
        {
            var old=t.GetComponent<Image>();if(old!=null&&!(old is ApprovedContourImage))UnityEngine.Object.DestroyImmediate(old);var im=t.GetComponent<ApprovedContourImage>();if(im==null)im=t.gameObject.AddComponent<ApprovedContourImage>();
            var indices=new List<int>();var remain=new List<int>();for(int i=0;i<points.Length;i++)remain.Add(i);float area=0;for(int i=0;i<points.Length;i++)area+=Cross(points[i],points[(i+1)%points.Length]);float sign=area>=0?1:-1;int safety=points.Length*points.Length;
            while(remain.Count>3&&safety-->0){bool cut=false;for(int i=0;i<remain.Count;i++){int x=remain[(i+remain.Count-1)%remain.Count],y=remain[i],z=remain[(i+1)%remain.Count];if(Cross(points[y]-points[x],points[z]-points[y])*sign<=0)continue;bool inside=false;foreach(int k in remain)if(k!=x&&k!=y&&k!=z&&In(points[k],points[x],points[y],points[z],sign)){inside=true;break;}if(inside)continue;indices.Add(x);indices.Add(y);indices.Add(z);remain.RemoveAt(i);cut=true;break;}if(!cut)throw new InvalidOperationException("Invalid contour "+t.name);}
            if(remain.Count==3)indices.AddRange(remain);var data=new SerializedObject(im);var vertices=data.FindProperty("contour");vertices.arraySize=points.Length;for(int i=0;i<points.Length;i++)vertices.GetArrayElementAtIndex(i).vector2Value=points[i];var tris=data.FindProperty("triangles");tris.arraySize=indices.Count;for(int i=0;i<indices.Count;i++)tris.GetArrayElementAtIndex(i).intValue=indices[i];data.ApplyModifiedPropertiesWithoutUndo();var button=t.GetComponent<Button>();if(button!=null){button.targetGraphic=im;im.raycastTarget=true;}return im;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool In(Vector2 p,Vector2 a,Vector2 b,Vector2 c,float sign)=>Cross(b-a,p-a)*sign>=0&&Cross(c-b,p-b)*sign>=0&&Cross(a-c,p-c)*sign>=0;
        public static Vector2[] Points(Rect bounds,params float[] xy){var result=new Vector2[xy.Length/2];for(int i=0;i<result.Length;i++)result[i]=new Vector2((xy[i*2]-bounds.x)/bounds.width,(xy[i*2+1]-bounds.y)/bounds.height);return result;}
    }
}
