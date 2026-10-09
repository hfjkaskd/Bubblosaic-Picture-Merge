using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Disposable Unity renderer checks, not gameplay screenshots.
    public static class UiBorderCalibrationPreview
    {
        public static void Render()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Requires Edit Mode.");
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/BorderCalibration-20260927"));
            var scene=EditorSceneManager.NewPreviewScene();RenderTexture texture=null;Texture2D pixels=null;
            try
            {
                const int width=1500,height=1800;
                var cameraObject=new GameObject("InspectionCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=height*.5f;camera.transform.position=new Vector3(0,0,-1000);camera.nearClipPlane=.1f;camera.farClipPlane=2000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.48f,.77f);camera.cullingMask=1<<5;
                var canvasObject=new GameObject("InspectionCanvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(canvasObject,scene);canvasObject.layer=5;
                var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
                ((RectTransform)canvas.transform).sizeDelta=new Vector2(width,height);
                string[] names={"Status","Currency","Conversion","Withdraw","Tray","Tool","Locked","Dialog","Orange"};
                Vector2[] sizes={new Vector2(342,208),new Vector2(350,100),new Vector2(146,78),new Vector2(146,78),new Vector2(600,220),new Vector2(176,176),new Vector2(176,176),new Vector2(620,220),new Vector2(340,120)};
                float[] ys={750,580,460,350,160,-70,-260,-490,-690};
                for(int row=0;row<names.Length;row++)
                {
                    string resource=row<7?"CoralV3/CoralUIAtlas":"AllUI20260924/SharedControls";
                    Sprite sprite=null;foreach(var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/BubblePics/Resources/"+resource+".png"))if(asset is Sprite candidate&&candidate.name==names[row]){sprite=candidate;break;}
                    if(sprite==null)throw new InvalidOperationException(names[row]);
                    for(int column=0;column<2;column++)
                    {
                        var node=new GameObject(names[row]+column,typeof(RectTransform),typeof(Image));node.layer=5;node.transform.SetParent(canvas.transform,false);
                        var rect=(RectTransform)node.transform;rect.sizeDelta=sizes[row];rect.anchoredPosition=new Vector2(column==0?-375:375,ys[row]);
                        var image=node.GetComponent<Image>();image.sprite=sprite;image.raycastTarget=false;image.type=row<7?Image.Type.Simple:Image.Type.Sliced;image.pixelsPerUnitMultiplier=row<7?1:row==7?.55f:.7f;
                        if(column==1){if(row<7){UiBorderCalibration.ConfigureImage(image,resource,names[row]);foreach(var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/BubblePics/Resources/CoralV3/CoralThinFrames.png"))if(asset is Sprite thin&&thin.name==names[row]){image.sprite=thin;break;}}else image.pixelsPerUnitMultiplier=1;}
                    }
                }
                Canvas.ForceUpdateCanvases();texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=texture;camera.Render();
                var previous=RenderTexture.active;RenderTexture.active=texture;pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();RenderTexture.active=previous;
                File.WriteAllBytes(Path.Combine(folder,"unity-border-render.png"),pixels.EncodeToPNG());
                File.WriteAllText(Path.Combine(folder,"preview-result.txt"),"PASS Unity Image rendering: before | after. Not a gameplay screenshot. "+DateTime.UtcNow.ToString("O"));
            }
            finally { if(texture!=null){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
