using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Offline authoring utilities. The resulting prefab contains every static visual and native control.
    public sealed class ReferencePrefabTools
    {
        public readonly string Root,Resource;
        public readonly float W,H,SX,SY;
        public readonly TMP_FontAsset Font;
        readonly string source;
        readonly Dictionary<string,Rect> slices=new Dictionary<string,Rect>();
        readonly Dictionary<string,Vector4> borders=new Dictionary<string,Vector4>();
        public ReferencePrefabTools(string id,string sourcePath,float w,float h)
        {
            Root="Assets/BubblePics/Resources/SequentialUI20260928/"+id+"/";Resource="SequentialUI20260928/"+id+"/ApprovedSource";source=sourcePath;
            W=w;H=h;SX=2360f*1080/2340/w;SY=2360/h;
            Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/Resources/TierReference20260928/TextFont.asset");
        }
        public ReferencePrefabTools Slice(string n,float x,float y,float w,float h,Vector4 border=default){slices.Add(n,new Rect(x,y,w,h));borders.Add(n,border);return this;}
        public Rect R(string n)=>slices[n];
        public Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>(Root+n+".mat");
        public Material Material(string n,string shader="BubblePics/UI/SequentialReferencePlate")
        {
            var m=Mat(n);if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,Root+n+".mat");}m.shader=Shader.Find(shader);EditorUtility.SetDirty(m);return m;
        }
        public void Import()
        {
            Directory.CreateDirectory(Root);string path=Root+"ApprovedSource.png";if(!File.Exists(path))File.Copy(source,path);
            ResourceSheetPacking.RestoreForAuthoring(path,source);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var imp=(TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Multiple;imp.spritePixelsPerUnit=100;imp.mipmapEnabled=false;imp.npotScale=TextureImporterNPOTScale.None;imp.maxTextureSize=2048;imp.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(string platform in new[]{"Android","iPhone"}){var p=imp.GetPlatformTextureSettings(platform);p.overridden=true;p.maxTextureSize=2048;p.format=TextureImporterFormat.ASTC_4x4;imp.SetPlatformTextureSettings(p);}imp.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(imp);provider.InitSpriteEditorDataProvider();var prior=provider.GetSpriteRects();var rects=new List<SpriteRect>();var names=new List<SpriteNameFileIdPair>();
            foreach(var kv in slices){var id=GUID.Generate();foreach(var old in prior)if(old.name==kv.Key){id=old.spriteID;break;}Rect r=kv.Value;rects.Add(new SpriteRect{name=kv.Key,rect=new Rect(r.x,H-r.y-r.height,r.width,r.height),spriteID=id,pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,border=borders[kv.Key]});names.Add(new SpriteNameFileIdPair(kv.Key,id));Erase(Material(kv.Key));}
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);provider.Apply();imp.SaveAndReimport();
            var empty=Material("EmptyCaption","UI/Default");empty.SetColor("_Color",Color.clear);
        }
        public void Erase(Material m,params Rect[] boxes)
        {
            m.SetVector("_TextureSize",new Vector4(W,H,0,0));m.SetVector("_SamplePoint",Vector4.zero);m.SetFloat("_SampleX",-1);m.SetFloat("_SampleY",-1);m.SetFloat("_EraseRadius",0);m.SetFloat("_EraseFeather",2);m.SetVector("_VisibleRect",Vector4.zero);m.SetFloat("_InkOnly",0);m.SetVector("_MirrorPatch",Vector4.zero);m.SetColor("_Color",Color.white);
            m.SetFloat("_WhiteMatte",0);m.SetFloat("_BlueMatte",0);for(int i=0;i<6;i++){Rect r=i<boxes.Length?boxes[i]:default;m.SetVector("_Erase"+i,new Vector4(r.x,r.y,r.width,r.height));}EditorUtility.SetDirty(m);
        }
        public void Round(Material m,Rect r,float radius,float feather=1){m.SetVector("_VisibleRect",new Vector4(r.x,r.y,r.width,r.height));m.SetFloat("_Radius",radius);m.SetFloat("_Feather",feather);}
        public static T Ensure<T>(Component c) where T:Component{var v=c.GetComponent<T>();return v!=null?v:c.gameObject.AddComponent<T>();}
        public static Transform Child(Transform parent,string name){var g=new GameObject(name,typeof(RectTransform));g.layer=5;g.transform.SetParent(parent,false);return g.transform;}
        public static void Clear(Transform t){foreach(var c in t.GetComponentsInChildren<Transform>(true))if(c!=t&&PrefabUtility.IsAnyPrefabInstanceRoot(c.gameObject))PrefabUtility.UnpackPrefabInstance(c.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);while(t.childCount>0)UnityEngine.Object.DestroyImmediate(t.GetChild(0).gameObject);}
        public static void Stretch(Transform t){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;}
        public void Place(Transform t,Rect box){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2((box.center.x-W/2)*SX,(H/2-box.center.y)*SY);r.sizeDelta=new Vector2(box.width*SX,box.height*SY);r.localScale=Vector3.one;}
        public void Local(Transform t,Rect box){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(box.x*SX,-box.y*SY);r.sizeDelta=new Vector2(box.width*SX,box.height*SY);r.localScale=Vector3.one;}
        public Image Visual(Transform t,string slice,string material=null)
        {
            var im=Ensure<Image>(t);var data=new SerializedObject(Ensure<CoralResourceSprite>(t));data.FindProperty("_resourcePath").stringValue=Resource;data.FindProperty("_spriteName").stringValue=slice;data.FindProperty("_image").objectReferenceValue=im;data.ApplyModifiedPropertiesWithoutUndo();im.sprite=null;im.material=Mat(material??slice);im.color=Color.white;im.type=Image.Type.Simple;im.raycastTarget=false;return im;
        }
        public Transform Graphic(Transform parent,string name,string slice=null){var t=Child(parent,name);Place(t,R(slice??name));Visual(t,slice??name);return t;}
        public void Sliced(Image im){im.type=Image.Type.Sliced;im.pixelsPerUnitMultiplier=1/SY;}
        public TMP_Text Text(Transform parent,string name,Rect box,float size,TextAlignmentOptions align=TextAlignmentOptions.Center,Color? color=null)
        {
            var t=Child(parent,name);Place(t,box);var label=Ensure<TextMeshProUGUI>(t);Ensure<PreserveAuthoredFont>(label);label.font=Font;label.fontSharedMaterial=Font.material;label.fontSize=label.fontSizeMax=size*SY;label.fontSizeMin=size*SY*.55f;label.enableAutoSizing=true;label.alignment=align;label.color=color??new Color(.025f,.02f,.32f);label.raycastTarget=false;label.enableWordWrapping=false;label.margin=Vector4.zero;return label;
        }
        public static void Localize(TMP_Text t,string key){var data=new SerializedObject(Ensure<CoralLocalizedLabel>(t));data.FindProperty("key").stringValue=key;data.ApplyModifiedPropertiesWithoutUndo();}
        public void Caption(Transform parent,TMP_Text label,string slice,string caption,string translated="EmptyCaption")
        {
            var t=Graphic(parent,"Reference"+slice,slice);var group=Ensure<CanvasGroup>(label);group.blocksRaycasts=false;group.interactable=false;
            var data=new SerializedObject(Ensure<ApprovedHudCaption>(t));data.FindProperty("_label").objectReferenceValue=label;data.FindProperty("_surface").objectReferenceValue=t.GetComponent<Image>();data.FindProperty("_authoredCaption").stringValue=caption;data.FindProperty("_captionMaterial").objectReferenceValue=Mat(slice);data.FindProperty("_translatedMaterial").objectReferenceValue=Mat(translated);data.FindProperty("_captionGroup").objectReferenceValue=group;data.ApplyModifiedPropertiesWithoutUndo();
        }
        public BizzaButton Button(Transform parent,string name,string slice,Rect? box=null,string key=null,string caption=null,float size=50)
        {
            var t=Child(parent,name);Place(t,box??R(slice));var im=Visual(t,slice,key==null?slice:slice+"Blank");im.raycastTarget=true;
            var owner=Ensure<BizzaButton>(t);owner.scaleTarget=t;owner.onClick=new UnityEvent();owner.onLongClick=new UnityEvent();var button=Ensure<WithdrawCloudButton>(t);button.targetGraphic=im;button.transition=Selectable.Transition.None;
            var data=new SerializedObject(owner);data.FindProperty("standardButton").objectReferenceValue=button;data.ApplyModifiedPropertiesWithoutUndo();data=new SerializedObject(button);data.FindProperty("legacyOwner").objectReferenceValue=owner;data.ApplyModifiedPropertiesWithoutUndo();
            if(key!=null){var label=Text(parent,name+"Label",box??R(slice),size,color:Color.white);Localize(label,key);Caption(parent,label,slice,caption);label.transform.SetParent(t,true);parent.Find("Reference"+slice).SetParent(t,true);}
            return owner;
        }
        public Image Overlay(Transform parent,Color color){var t=Child(parent,"ModalOverlay");Stretch(t);var image=Ensure<Image>(t);image.color=color;image.raycastTarget=true;return image;}
        public static void Bind(UnityEngine.Object owner,string key,UnityEngine.Object value){var data=new SerializedObject(owner);data.FindProperty(key).objectReferenceValue=value;data.ApplyModifiedPropertiesWithoutUndo();}
        public static void Validate(Transform root){foreach(var b in root.GetComponentsInChildren<Button>(true))if(b.targetGraphic==null||b.targetGraphic.gameObject!=b.gameObject||b.onClick.GetPersistentEventCount()!=0)throw new InvalidOperationException("Native Button visual/event mismatch: "+b.name);}
    }
}
