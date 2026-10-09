using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Authoring only: source rectangles become normal prefab Images and Buttons.
    public static class UnityDialogFidelity
    {
        const string ResourcesRoot="Assets/BubblePics/Resources/DialogFidelity20260928/";
        static readonly string Folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/DialogFidelity-20260928"));
        [Serializable] public class Spec {public Page[] pages;}
        [Serializable] public class Page {public string id,asset,source;public Frame[] frames;public Edit[] edits;}
        [Serializable] public class Frame {public string name;public float[] rect,erase;public float radius,eraseRadius,feather=2,sample=-1,topArc,topInset;public string[] fill;}
        [Serializable] public class Edit {public string path,create,frame,color,text,key;public float[] rect;public float font;public int active=-1;public bool disableImage,whiteText,goldText,disableLayout,filled;}
        static readonly Color Ink=new Color(.02f,.03f,.28f,1);
        static TMP_FontAsset font;
        static Material outline;
        static Material goldOutline;

        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before editing prefabs.");
            var spec=JsonUtility.FromJson<Spec>(File.ReadAllText(Path.Combine(Folder,"spec.json")));
            Directory.CreateDirectory(ResourcesRoot);Directory.CreateDirectory(Path.Combine(Folder,"Backup"));
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/BaggageGo-Bold SDF.asset");
            string materialPath=ResourcesRoot+"ButtonText.mat";
            outline=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(outline==null){outline=new Material(font.material);AssetDatabase.CreateAsset(outline,materialPath);}
            outline.SetFloat("_OutlineWidth",.035f);outline.SetColor("_OutlineColor",new Color(.07f,.13f,.3f,1));outline.EnableKeyword("OUTLINE_ON");EditorUtility.SetDirty(outline);
            string goldPath=ResourcesRoot+"GoldText.mat";goldOutline=AssetDatabase.LoadAssetAtPath<Material>(goldPath);if(goldOutline==null){goldOutline=new Material(font.material);AssetDatabase.CreateAsset(goldOutline,goldPath);}goldOutline.SetFloat("_OutlineWidth",.055f);goldOutline.SetColor("_OutlineColor",new Color(.34f,.11f,.01f,1));goldOutline.EnableKeyword("OUTLINE_ON");goldOutline.SetFloat("_UnderlayOffsetY",-.13f);goldOutline.SetColor("_UnderlayColor",new Color(.46f,.14f,.01f,1));goldOutline.EnableKeyword("UNDERLAY_ON");EditorUtility.SetDirty(goldOutline);
            var report=new List<string>();
            foreach(var page in spec.pages)
            {
                Import(page);
                string backup=Path.Combine(Folder,"Backup",page.id+".prefab");if(!File.Exists(backup))File.Copy(page.asset,backup);
                var go=PrefabUtility.LoadPrefabContents(page.asset);
                try
                {
                    int count=go.GetComponentsInChildren<Button>(true).Length;
                    var before=new Dictionary<MonoBehaviour,List<KeyValuePair<string,UnityEngine.Object>>>();
                    foreach(var component in go.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if(component==null)throw new InvalidOperationException("Missing script: "+page.id);
                        if(component is Graphic||component is CoralResourceSprite||component is LayoutGroup||component is UILanguageLabel)continue;
                        var references=new List<KeyValuePair<string,UnityEngine.Object>>();var serialized=new SerializedObject(component);var p=serialized.GetIterator();
                        while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue!=null)references.Add(new KeyValuePair<string,UnityEngine.Object>(p.propertyPath,p.objectReferenceValue));
                        before.Add(component,references);
                    }
                    foreach(var e in page.edits)
                    {
                        Transform t=Find(go.transform,e.path);
                        if(!string.IsNullOrEmpty(e.create)){var parent=t;t=parent.Find(e.create);if(t==null){t=new GameObject(e.create,typeof(RectTransform)).transform;t.SetParent(parent,false);t.gameObject.layer=5;}}
                        if(e.active>=0)t.gameObject.SetActive(e.active!=0);
                        if(e.disableLayout){foreach(var l in t.GetComponents<LayoutGroup>())l.enabled=false;}
                        if(e.rect!=null)Place(go.transform,t,e.rect);
                        if(e.disableImage&&t.GetComponent<Image>()!=null)t.GetComponent<Image>().enabled=false;
                        if(!string.IsNullOrEmpty(e.frame))Visual(t,page.id,e.frame);
                        if(e.filled){var fill=t.GetComponent<Image>();fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;fill.fillOrigin=0;}
                        var text=t.GetComponent<TMP_Text>();
                        if(text==null&&(e.text!=null||e.key!=null)){text=t.gameObject.AddComponent<TextMeshProUGUI>();text.alignment=TextAlignmentOptions.Center;}
                        if(e.text!=null)text.text=e.text;
                        if(e.key!=null){var localized=t.GetComponent<CoralLocalizedLabel>();if(localized==null)localized=t.gameObject.AddComponent<CoralLocalizedLabel>();var labelData=new SerializedObject(localized);labelData.FindProperty("key").stringValue=e.key;labelData.ApplyModifiedPropertiesWithoutUndo();}
                        if(e.font>0&&text!=null){text.font=font;text.fontSize=e.font*2360/1852;text.fontSizeMax=text.fontSize;text.fontSizeMin=text.fontSize*.65f;text.enableAutoSizing=true;text.fontStyle=FontStyles.Normal;text.fontSharedMaterial=e.whiteText?outline:font.material;text.enableVertexGradient=false;text.color=e.whiteText?Color.white:Ink;text.raycastTarget=false;text.UpdateMeshPadding();}
                        if(!string.IsNullOrEmpty(e.color)&&ColorUtility.TryParseHtmlString(e.color,out Color tint)){var graphic=t.GetComponent<Graphic>();if(graphic!=null)graphic.color=tint;}
                        if(e.goldText&&text!=null){text.fontSharedMaterial=goldOutline;text.enableVertexGradient=true;text.color=Color.white;text.colorGradient=new VertexGradient(new Color(1,.97f,.36f),new Color(1,.97f,.36f),new Color(1,.51f,.04f),new Color(1,.51f,.04f));var textData=new SerializedObject(text);textData.FindProperty("m_fontColor32").colorValue=Color.white;textData.ApplyModifiedPropertiesWithoutUndo();text.UpdateMeshPadding();}
                    }
                    foreach(var pair in before){var serialized=new SerializedObject(pair.Key);foreach(var reference in pair.Value){var p=serialized.FindProperty(reference.Key);if(p==null||p.objectReferenceValue!=reference.Value)throw new InvalidOperationException("Functional reference changed: "+page.id+" / "+pair.Key.name+" / "+reference.Key);}}
                    if(go.GetComponentsInChildren<Button>(true).Length!=count)throw new InvalidOperationException("Button count changed: "+page.id);
                    foreach(var button in go.GetComponentsInChildren<Button>(true))if(button.targetGraphic==null||button.onClick.GetPersistentEventCount()>0)throw new InvalidOperationException("Invalid Button binding: "+page.id+" / "+button.name);
                    PrefabUtility.SaveAsPrefabAsset(go,page.asset);
                    report.Add("PASS "+page.id+" functional references preserved; standard Buttons="+count+"; edits="+page.edits.Length);
                }
                finally{PrefabUtility.UnloadPrefabContents(go);}
            }
            AssetDatabase.SaveAssets();File.WriteAllLines(Path.Combine(Folder,"applied.txt"),report);
        }

        static Transform Find(Transform root,string path)
        {
            if(string.IsNullOrEmpty(path))return root;
            foreach(string part in path.Split('/'))
            {
                string name=part;int index=0;int bracket=part.LastIndexOf('[');
                if(bracket>=0){index=int.Parse(part.Substring(bracket+1,part.Length-bracket-2));name=part.Substring(0,bracket);}
                Transform found=null;foreach(Transform child in root)if(child.name==name&&index--==0){found=child;break;}
                if(found==null)throw new InvalidOperationException("Missing node: "+path);root=found;
            }
            return root;
        }
        static void Place(Transform root,Transform t,float[] b)
        {
            const float s=2360f/1852f;
            var rect=(RectTransform)t;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.localScale=Vector3.one;
            Vector3 target=root.TransformPoint(new Vector3((b[0]+b[2]*.5f-424.5f)*s,(926-b[1]-b[3]*.5f)*s,0));
            Vector3 local=t.parent.InverseTransformPoint(target);
            rect.anchoredPosition=(Vector2)local-((RectTransform)t.parent).rect.center;rect.sizeDelta=new Vector2(b[2],b[3])*s;
        }
        static void Visual(Transform t,string page,string frame)
        {
            var image=t.GetComponent<Image>();if(image==null)image=t.gameObject.AddComponent<Image>();
            var binder=t.GetComponent<CoralResourceSprite>();if(binder==null)binder=t.gameObject.AddComponent<CoralResourceSprite>();
            var so=new SerializedObject(binder);so.FindProperty("_resourcePath").stringValue="DialogFidelity20260928/"+page;so.FindProperty("_spriteName").stringValue=frame;so.FindProperty("_image").objectReferenceValue=image;so.ApplyModifiedPropertiesWithoutUndo();
            bool filled=image.type==Image.Type.Filled;image.sprite=null;image.material=AssetDatabase.LoadAssetAtPath<Material>(ResourcesRoot+page+"-"+frame+".mat");image.type=filled?Image.Type.Filled:Image.Type.Simple;image.preserveAspect=false;image.color=Color.white;image.enabled=true;image.raycastTarget=t.GetComponent<Button>()!=null;
        }
        static void Import(Page page)
        {
            string path=ResourcesRoot+page.id+".png";if(!File.Exists(path))File.Copy(page.source,path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Multiple;ti.spritePixelsPerUnit=100;ti.mipmapEnabled=false;ti.isReadable=false;ti.npotScale=TextureImporterNPOTScale.None;ti.maxTextureSize=2048;ti.alphaIsTransparency=true;ti.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(string platform in new[]{"Android","iPhone"}){var ps=ti.GetPlatformTextureSettings(platform);ps.overridden=true;ps.maxTextureSize=2048;ps.format=TextureImporterFormat.ASTC_4x4;ti.SetPlatformTextureSettings(ps);}ti.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(ti);provider.InitSpriteEditorDataProvider();var old=provider.GetSpriteRects();var rects=new List<SpriteRect>();var names=new List<SpriteNameFileIdPair>();
            foreach(var frame in page.frames)
            {
                float[] b=frame.rect;GUID id=GUID.Generate();foreach(var previous in old)if(previous.name==frame.name){id=previous.spriteID;break;}
                rects.Add(new SpriteRect{name=frame.name,rect=new Rect(b[0],texture.height-b[1]-b[3],b[2],b[3]),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=id});names.Add(new SpriteNameFileIdPair(frame.name,id));
                string matPath=ResourcesRoot+page.id+"-"+frame.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(mat==null){mat=new Material(Shader.Find("BubblePics/UI/ApprovedDialogFrame"));AssetDatabase.CreateAsset(mat,matPath);}mat.shader=Shader.Find("BubblePics/UI/ApprovedDialogFrame");mat.SetFloat("_TopArc",frame.topArc);mat.SetFloat("_TopInset",frame.topInset);
                mat.SetVector("_SourceRect",new Vector4(b[0]/texture.width,(texture.height-b[1]-b[3])/texture.height,b[2]/texture.width,b[3]/texture.height));mat.SetVector("_SourceSize",new Vector4(b[2],b[3],0,0));mat.SetVector("_OuterRect",new Vector4(0,0,b[2],b[3]));mat.SetFloat("_OuterRadius",frame.radius);mat.SetFloat("_MaskMode",1);mat.SetVector("_CapEllipse",Vector4.zero);
                bool erase=frame.erase!=null;mat.SetFloat("_EraseOn",erase?1:0);mat.SetFloat("_FillSampleU",frame.sample);
                if(erase){mat.SetVector("_EraseRect",new Vector4(frame.erase[0],frame.erase[1],frame.erase[2],frame.erase[3]));mat.SetFloat("_EraseRadius",frame.eraseRadius);mat.SetFloat("_EraseFeather",frame.feather);for(int i=0;i<3;i++){ColorUtility.TryParseHtmlString(frame.fill[i],out Color fill);mat.SetColor(new[]{"_FillTop","_FillMiddle","_FillBottom"}[i],fill);}}
                EditorUtility.SetDirty(mat);
            }
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);provider.Apply();ti.SaveAndReimport();
        }
    }
}
