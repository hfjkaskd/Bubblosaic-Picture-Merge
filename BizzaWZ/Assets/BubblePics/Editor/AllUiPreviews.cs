using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        // Disposable editor renders for layout iteration. These are never labelled gameplay captures.
        static void RenderPreviews()
        {
            RequireEdit();var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Folder,"plan.json")));
            string output=Path.Combine(Folder,"PrefabPreviews");Directory.CreateDirectory(output);
            foreach(var page in plan.items)RenderPreview(page,Path.Combine(output,page.id+".png"));
        }
        public static void PreviewWithdrawalService(string folder)
        {
            RequireEdit(); Directory.CreateDirectory(folder);
            foreach (int height in new[] {1920,2400})
            {
                RenderPreview(new Page {id="RealWithdrawPanel",prefab=WithdrawalServiceButtonAuthoring.Coins},
                    Path.Combine(folder,"coins-1080x"+height+".png"),1080,height);
                RenderPreview(new Page {id="FakeWithdrawPanel",prefab=WithdrawalServiceButtonAuthoring.Starter},
                    Path.Combine(folder,"starter-1080x"+height+".png"),1080,height);
            }
        }
        static void RenderPreview(Page page,string output,int width=1080,int height=2360)
        {
            var scene=EditorSceneManager.NewPreviewScene();RenderTexture rt=null;Texture2D pixels=null;
            try
            {
                var camObject=new GameObject("PreviewCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(camObject,scene);
                var camera=camObject.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=height*.5f;camera.nearClipPlane=.1f;camera.farClipPlane=3000;
                camera.transform.position=new Vector3(0,0,-1500);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.05f,.65f,1);camera.cullingMask=1<<5;
                var canvasObject=new GameObject("PreviewCanvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(canvasObject,scene);canvasObject.layer=5;
                var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
                var canvasRect=canvasObject.GetComponent<RectTransform>();canvasRect.sizeDelta=new Vector2(width,height);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(page.prefab),scene);go.transform.SetParent(canvasObject.transform,false);
                var root=go.GetComponent<RectTransform>();root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.sizeDelta=Vector2.zero;root.anchoredPosition=Vector2.zero;root.localScale=Vector3.one;
                foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
                foreach(var binder in go.GetComponentsInChildren<CoralResourceSprite>(true))
                {
                    var so=new SerializedObject(binder);var im=so.FindProperty("_image").objectReferenceValue as Image;
                    if(im!=null)im.sprite=CoralResourceSprite.Load(so.FindProperty("_resourcePath").stringValue,so.FindProperty("_spriteName").stringValue);
                }
                foreach(var binder in go.GetComponentsInChildren<WithdrawCloudBackdrop>(true))
                {
                    var so=new SerializedObject(binder);var im=so.FindProperty("targetImage").objectReferenceValue as Image;
                    if(im!=null){im.sprite=Resources.Load<Sprite>(so.FindProperty("resourcePath").stringValue);im.color=Color.white;}
                }
                foreach(var splash in go.GetComponentsInChildren<SplashPage>(true))
                {
                    var so=new SerializedObject(splash);var im=so.FindProperty("_backgroundImage").objectReferenceValue as Image;
                    if(im!=null)im.sprite=Resources.Load<Sprite>(so.FindProperty("_backgroundResourcePath").stringValue);
                }
                // OnEnable is not executed for ordinary behaviours in a preview scene.
                // Apply their serialized uniform-fit dimensions to this disposable render only.
                foreach(var fit in go.GetComponentsInChildren<PageContentFit>(true))
                {
                    var content=fit.Content; var viewport=(RectTransform)fit.transform;
                    if(content!=null&&content.rect.width>0&&content.rect.height>0)
                        content.localScale=Vector3.one*Mathf.Min(viewport.rect.width/content.rect.width,viewport.rect.height/content.rect.height);
                }
                Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(root);Canvas.ForceUpdateCanvases();
                foreach(var tmp in go.GetComponentsInChildren<TMP_Text>())tmp.ForceMeshUpdate();
                rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;camera.Render();
                var previous=RenderTexture.active;RenderTexture.active=rt;pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();RenderTexture.active=previous;
                File.WriteAllBytes(output,pixels.EncodeToPNG());
            }
            finally
            {
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
