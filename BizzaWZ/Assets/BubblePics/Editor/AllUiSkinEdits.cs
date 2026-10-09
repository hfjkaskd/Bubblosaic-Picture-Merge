using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        const string ArtRoot="Assets/BubblePics/Resources/AllUI20260924/";
        [Serializable] public class ArtSpec { public TextureSpec[] textures; }
        [Serializable] public class TextureSpec { public string file; public Slice[] slices; }
        [Serializable] public class Slice { public string name; public SliceRect rect; public Vector4 border; }
        [Serializable] public class SliceRect { public float x,y,width,height; }
        [Serializable] public class SkinSpec { public PrefabSpec[] prefabs; }
        [Serializable] public class PrefabSpec { public string asset; public NodeEdit[] edits; }
        [Serializable] public class NodeEdit
        {
            public string path,create,parent,sprite,resource,color,text,font;
            public string[] add;
            public bool rect,stretch,scale,sliced,firstSibling,replaceRaw;
            public Vector2 position,size,anchor=new Vector2(.5f,.5f),pivot=new Vector2(.5f,.5f);
            public Vector3 localScale=Vector3.one;
            public int active=-1,imageEnabled=-1,raycast=-1,sibling=-1;
            public float fontSize;
            public FieldEdit[] fields;
        }
        [Serializable] public class FieldEdit { public string component,property,type,text,asset,path,sprite; public float number; public bool boolean; public Vector2 vector; public Color color=Color.white; }
        static void Backup(string path)
        {
            var backup=Path.Combine(Folder,"Backup",path);Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if(!File.Exists(backup))File.Copy(path,backup);
        }
        static void RequireEdit(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Authoring requires Edit Mode.");}
        static void ImportArt()
        {
            RequireEdit();var spec=JsonUtility.FromJson<ArtSpec>(File.ReadAllText(Path.Combine(Folder,"art.json")));
            foreach(var item in spec.textures)
            {
                string path=ArtRoot+item.file;AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var ti=(TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=item.slices!=null&&item.slices.Length>0?SpriteImportMode.Multiple:SpriteImportMode.Single;
                ti.spritePixelsPerUnit=100;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.isReadable=false;
                ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;
                var settings=new TextureImporterSettings();ti.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;ti.SetTextureSettings(settings);
                foreach(string platform in new[]{"Android","iPhone"}){var ps=ti.GetPlatformTextureSettings(platform);ps.overridden=true;ps.maxTextureSize=2048;ps.format=TextureImporterFormat.ASTC_4x4;ti.SetPlatformTextureSettings(ps);}
                ti.SaveAndReimport();
                if(item.slices==null||item.slices.Length==0)continue;
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(ti);provider.InitSpriteEditorDataProvider();
                var existing=provider.GetSpriteRects();var rects=new List<SpriteRect>();var pairs=new List<SpriteNameFileIdPair>();
                foreach(var s in item.slices)
                {
                    if(s.rect==null||s.rect.width<=0||s.rect.height<=0)throw new InvalidOperationException("Invalid sprite rectangle: "+item.file+"/"+s.name);
                    GUID id=GUID.Generate();foreach(var old in existing)if(old.name==s.name){id=old.spriteID;break;}
                    rects.Add(new SpriteRect{name=s.name,rect=new Rect(s.rect.x,texture.height-s.rect.y-s.rect.height,s.rect.width,s.rect.height),border=s.border,alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),spriteID=id});
                    pairs.Add(new SpriteNameFileIdPair(s.name,id));
                }
                provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);provider.Apply();ti.SaveAndReimport();
            }
            const string fontPath=ArtRoot+"Fonts/LilitaOne-SDF.asset";
            if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath)==null)
            {
                AssetDatabase.ImportAsset(ArtRoot+"Fonts/LilitaOne-Regular.ttf");
                var source=AssetDatabase.LoadAssetAtPath<Font>(ArtRoot+"Fonts/LilitaOne-Regular.ttf");
                var font=TMP_FontAsset.CreateFontAsset(source,80,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic);
                var glyphs=new StringBuilder();for(int i=32;i<592;i++)glyphs.Append((char)i);glyphs.Append("≈€₹→✓×");font.TryAddCharacters(glyphs.ToString());
                font.name="LilitaOne-SDF";font.atlasPopulationMode=AtlasPopulationMode.Static;
                font.fallbackFontAssetTable=new List<TMP_FontAsset>{AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BizzaWZ/Common/Framework/Res/Fonts/MainFont_Simple.asset")};
                AssetDatabase.CreateAsset(font,fontPath);AssetDatabase.AddObjectToAsset(font.material,font);foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
            }
        }
        static Transform Locate(Transform root,string path)
        {
            var found=TryLocate(root,path);if(found==null)throw new InvalidOperationException("Missing prefab path: "+path);return found;
        }
        static Transform TryLocate(Transform root,string path)
        {
            if(string.IsNullOrEmpty(path))return root;
            var counts=new Dictionary<string,int>();
            foreach(Transform child in root)
            {
                counts.TryGetValue(child.name,out int index);counts[child.name]=index+1;
                string segment=child.name+(index==0?"":"["+index+"]");
                if(path==segment)return child;
                if(path.StartsWith(segment+"/",StringComparison.Ordinal))
                {
                    var found=TryLocate(child,path.Substring(segment.Length+1));if(found!=null)return found;
                }
                if(index>0&&path.StartsWith(child.name+"/",StringComparison.Ordinal))
                {
                    var found=TryLocate(child,path.Substring(child.name.Length+1));if(found!=null)return found;
                }
            }
            return null;
        }
        static Sprite SpriteAsset(string resource,string name)
        {
            string path="Assets/BubblePics/Resources/"+resource+".png";
            foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(path))if(obj is Sprite sprite&&(string.IsNullOrEmpty(name)||sprite.name==name))return sprite;
            throw new InvalidOperationException("Missing sprite "+path+" : "+name);
        }
        static void Art(Transform node,string resource,string name,bool sliced)
        {
            resource=UiBorderCalibration.FrameResource(resource,name);
            var im=node.GetComponent<Image>();if(im==null)im=node.gameObject.AddComponent<Image>();
            var old=node.GetComponent<CoralResourceSprite>();
            if(old==null)old=node.gameObject.AddComponent<CoralResourceSprite>();
            var so=new SerializedObject(old);so.FindProperty("_resourcePath").stringValue=resource;so.FindProperty("_spriteName").stringValue=name??"";
            so.FindProperty("_image").objectReferenceValue=im;so.ApplyModifiedPropertiesWithoutUndo();
            var preview=SpriteAsset(resource,name); // Validate at authoring; runtime loads the same resource.
            bool filled=im.type==Image.Type.Filled;
            im.sprite=null;im.color=Color.white;im.type=filled?Image.Type.Filled:sliced?Image.Type.Sliced:Image.Type.Simple;im.preserveAspect=false;
            im.pixelsPerUnitMultiplier=1f;
            UiBorderCalibration.ConfigureImage(im,resource,name);
            if(name=="Selected")im.fillCenter=false;
        }
        static void ApplySkin()
        {
            RequireEdit();var spec=JsonUtility.FromJson<SkinSpec>(File.ReadAllText(Path.Combine(Folder,"layout.json")));
            var report=new StringBuilder();
            foreach(var prefab in spec.prefabs)
            {
                Backup(prefab.asset);var root=PrefabUtility.LoadPrefabContents(prefab.asset);
                try
                {
                    // Both legacy reward decorations share a name. Restore their
                    // original order so repeated authoring resolves the same node.
                    var rewardPage=root.GetComponent<GetRewardPanel>();
                    if(rewardPage!=null&&rewardPage.LevelObj!=null)rewardPage.LevelObj.transform.SetAsFirstSibling();
                    foreach(var e in prefab.edits)
                    {
                        Transform node;
                        if(!string.IsNullOrEmpty(e.create))
                        {
                            var parent=Locate(root.transform,e.parent);node=parent.Find(e.create);
                            if(node==null){node=new GameObject(e.create,typeof(RectTransform)).transform;node.SetParent(parent,false);node.gameObject.layer=parent.gameObject.layer;}
                        }
                        else node=Locate(root.transform,e.path);
                        if(e.add!=null)foreach(string component in e.add)
                        {
                            switch(component)
                            {
                                case "LayoutElement":if(node.GetComponent<LayoutElement>()==null)node.gameObject.AddComponent<LayoutElement>();break;
                                case "VerticalLayoutGroup":if(node.GetComponent<VerticalLayoutGroup>()==null)node.gameObject.AddComponent<VerticalLayoutGroup>();break;
                                case "ContentSizeFitter":if(node.GetComponent<ContentSizeFitter>()==null)node.gameObject.AddComponent<ContentSizeFitter>();break;
                                case "Image":if(node.GetComponent<Image>()==null)node.gameObject.AddComponent<Image>();break;
                                case "CanvasGroup":if(node.GetComponent<CanvasGroup>()==null)node.gameObject.AddComponent<CanvasGroup>();break;
                                case "Canvas":if(node.GetComponent<Canvas>()==null)node.gameObject.AddComponent<Canvas>();break;
                                case "UILanguageLabel":if(node.GetComponent<UILanguageLabel>()==null)node.gameObject.AddComponent<UILanguageLabel>();break;
                                case "CoralLocalizedLabel":if(node.GetComponent<CoralLocalizedLabel>()==null)node.gameObject.AddComponent<CoralLocalizedLabel>();break;
                            }
                        }
                        if(e.firstSibling)node.SetAsFirstSibling();
                        if(e.sibling>=0)node.SetSiblingIndex(e.sibling);
                        if(e.active>=0)node.gameObject.SetActive(e.active!=0);
                        if(e.scale)node.localScale=e.localScale;
                        if(e.rect)
                        {
                            var rt=(RectTransform)node;rt.anchorMin=e.stretch?Vector2.zero:e.anchor;rt.anchorMax=e.stretch?Vector2.one:e.anchor;
                            rt.pivot=e.pivot;rt.anchoredPosition=e.position;rt.sizeDelta=e.size;
                        }
                        if(e.replaceRaw)
                        {
                            var raw=node.GetComponent<RawImage>();if(raw!=null)UnityEngine.Object.DestroyImmediate(raw);
                        }
                        if(!string.IsNullOrEmpty(e.resource)||!string.IsNullOrEmpty(e.sprite))Art(node,e.resource??"AllUI20260924/SharedControls",e.sprite,e.sliced);
                        if(e.replaceRaw){var button=node.GetComponent<Button>();if(button!=null)button.targetGraphic=node.GetComponent<Image>();}
                        var graphic=node.GetComponent<Graphic>();
                        if(graphic!=null)
                        {
                            if(e.imageEnabled>=0)graphic.enabled=e.imageEnabled!=0;
                            if(e.raycast>=0)graphic.raycastTarget=e.raycast!=0;
                            if(!string.IsNullOrEmpty(e.color)&&ColorUtility.TryParseHtmlString(e.color,out Color c))graphic.color=c;
                        }
                        var tmp=node.GetComponent<TMP_Text>();
                        if(tmp==null&&e.text!=null)tmp=node.gameObject.AddComponent<TextMeshProUGUI>();
                        if(tmp!=null)
                        {
                            if(e.text!=null){tmp.text=e.text;tmp.alignment=TextAlignmentOptions.Center;}
                            if(!string.IsNullOrEmpty(e.font)){var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(e.font);if(font==null)throw new FileNotFoundException(e.font);tmp.font=font;tmp.fontSharedMaterial=font.material;tmp.fontStyle=FontStyles.Normal;tmp.fontWeight=FontWeight.Regular;}
                            if(e.fontSize>0){tmp.fontSize=e.fontSize;tmp.enableAutoSizing=true;tmp.fontSizeMax=e.fontSize;tmp.fontSizeMin=e.fontSize*.65f;}
                            if(!string.IsNullOrEmpty(e.color)&&ColorUtility.TryParseHtmlString(e.color,out Color c)){tmp.color=c;tmp.enableVertexGradient=false;}
                            tmp.raycastTarget=false;
                        }
                    }
                    foreach(var e in prefab.edits)if(e.fields!=null)
                    {
                        var node=string.IsNullOrEmpty(e.create)?Locate(root.transform,e.path):Locate(root.transform,e.parent).Find(e.create);
                        foreach(var field in e.fields)SetSkinField(root.transform,node,field);
                    }
                    foreach(var legacy in root.GetComponentsInChildren<BizzaButton>(true))
                    {
                        var owner=new SerializedObject(legacy);var binding=owner.FindProperty("standardButton");
                        if(binding==null||binding.objectReferenceValue!=null)continue;
                        var graphic=legacy.GetComponent<Graphic>();
                        if(graphic==null)graphic=legacy.GetComponentInChildren<Graphic>(true);
                        if(graphic==null)continue;
                        var button=graphic.GetComponent<WithdrawCloudButton>();if(button==null)button=graphic.gameObject.AddComponent<WithdrawCloudButton>();
                        button.targetGraphic=graphic;graphic.raycastTarget=true;
                        var buttonData=new SerializedObject(button);buttonData.FindProperty("legacyOwner").objectReferenceValue=legacy;buttonData.ApplyModifiedPropertiesWithoutUndo();
                        binding.objectReferenceValue=button;owner.ApplyModifiedPropertiesWithoutUndo();
                    }
                    var bodyFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/NotoSans-Bold SDF.asset");
                    string outlinePath=ArtRoot+"Fonts/LilitaOne-Outline.mat";
                    var outline=AssetDatabase.LoadAssetAtPath<Material>(outlinePath);
                    if(outline==null){outline=new Material(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ArtRoot+"Fonts/LilitaOne-SDF.asset").material);outline.name="LilitaOne-Outline";outline.SetFloat("_OutlineWidth",.09f);outline.SetColor("_OutlineColor",new Color(.015f,.19f,.38f,1f));AssetDatabase.CreateAsset(outline,outlinePath);}
                    outline.EnableKeyword("OUTLINE_ON");EditorUtility.SetDirty(outline);
                    foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if(label.fontSize<=43&&bodyFont!=null){label.font=bodyFont;label.fontSharedMaterial=bodyFont.material;}
                        else if(((label.color.r>.94f&&label.color.g>.94f&&label.color.b>.94f)||(label.color.g>.9f&&label.color.r>.4f&&label.color.b<.4f))&&label.font!=null&&label.font.name=="LilitaOne-SDF"){label.fontSharedMaterial=outline;label.UpdateMeshPadding();}
                    }
                    if(prefab.asset.EndsWith("/UITeachMaskFocusPage.prefab"))ConfigureFocus(root);
                    if(prefab.asset.EndsWith("/TransitionBlock.prefab"))ConfigureTransition(root);
                    if(prefab.asset.EndsWith("/UITeachMaskPage.prefab"))
                    {
                        ConfigureMaskBackdrop(root);
                        foreach(var button in root.GetComponentsInChildren<Button>(true))
                            if(button.name=="FakeButtonTwo"&&button.targetGraphic==null)UnityEngine.Object.DestroyImmediate(button); // Unused legacy component; no callbacks or serialized references.
                    }
                    PrefabUtility.SaveAsPrefabAsset(root,prefab.asset);report.AppendLine("APPLIED "+prefab.asset+" edits="+prefab.edits.Length);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();File.WriteAllText(Path.Combine(Folder,"applied.txt"),report.ToString());Audit();
        }
        static void ConfigureTransition(GameObject root)
        {
            var animation=root.GetComponent<Animation>();
            foreach(string name in new[]{"open","close"})
            {
                var clip=animation.GetClip(name);if(clip==null)continue;
                Backup(AssetDatabase.GetAssetPath(clip));clip.ClearCurves();
                bool opening=name=="open";float from=opening?0f:1f,to=opening?1f:0f;
                clip.SetCurve("Content",typeof(CanvasGroup),"m_Alpha",AnimationCurve.EaseInOut(0,from,.65f,to));
                clip.SetCurve("Content/BG",typeof(Image),"m_Color.a",opening?new AnimationCurve(new Keyframe(0,0),new Keyframe(.45f,0),new Keyframe(.65f,1)):new AnimationCurve(new Keyframe(0,1),new Keyframe(.2f,0),new Keyframe(.65f,0)));
                foreach(string axis in new[]{"x","y","z"})clip.SetCurve("Content/Hole",typeof(Transform),"localScale."+axis,AnimationCurve.EaseInOut(0,opening?1.35f:1f,.65f,opening?1f:1.35f));
                EditorUtility.SetDirty(clip);
            }
        }
        static void ConfigureFocus(GameObject root)
        {
            foreach(var node in root.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
            var legacy=root.GetComponent<UITeachMaskFocusPage>();
            if(legacy.maskParent.Find("CoralFinger")==null)
            {
                var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BizzaWZ/Final/Framework/Runtime/Module/Teach/UITeach/UITeachMaskPage.prefab").transform.Find("MaskParent/Finger");
                var finger=UnityEngine.Object.Instantiate(template.gameObject,legacy.maskParent,false);finger.name="CoralFinger";finger.SetActive(true);((RectTransform)finger.transform).anchoredPosition=new Vector2(-45,-80);
            }
            var oldMask=root.transform.Find("MaskParent/MaskClick/Mask");if(oldMask!=null)oldMask.gameObject.SetActive(false);
            var controller=root.GetComponent<TutorialFocusBackdrop>();if(controller==null)controller=root.AddComponent<TutorialFocusBackdrop>();
            var so=new SerializedObject(controller);so.FindProperty("viewport").objectReferenceValue=root.GetComponent<RectTransform>();so.FindProperty("focus").objectReferenceValue=legacy.maskParent;
            Image[] parts=new Image[4];parts[0]=legacy.block;
            for(int i=1;i<4;i++)
            {
                string name="CoralDim"+i;var existing=root.transform.Find(name);
                if(existing==null){existing=new GameObject(name,typeof(RectTransform),typeof(Image)).transform;existing.SetParent(root.transform,false);existing.gameObject.layer=5;}
                parts[i]=existing.GetComponent<Image>();
            }
            string[] fields={"top","bottom","left","right"};
            for(int i=0;i<4;i++)
            {
                var image=parts[i];image.transform.SetParent(root.transform,false);image.transform.SetAsFirstSibling();image.raycastTarget=false;image.color=legacy.block.color;
                image.rectTransform.anchorMin=image.rectTransform.anchorMax=new Vector2(.5f,.5f);image.rectTransform.pivot=new Vector2(.5f,.5f);
                so.FindProperty(fields[i]).objectReferenceValue=image;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void ConfigureMaskBackdrop(GameObject root)
        {
            var legacy=root.GetComponent<UITeachMaskPage>();
            legacy.imgMask.gameObject.SetActive(false);
            var controller=root.GetComponent<TutorialFocusBackdrop>();if(controller==null)controller=root.AddComponent<TutorialFocusBackdrop>();
            var so=new SerializedObject(controller);so.FindProperty("viewport").objectReferenceValue=root.GetComponent<RectTransform>();so.FindProperty("focus").objectReferenceValue=legacy.maskParent;
            Image[] parts=new Image[4];parts[0]=legacy.block;
            string[] fields={"top","bottom","left","right"};
            for(int k=0;k<4;k++)
            {
                if(k>0){string name="CoralDim"+k;var node=root.transform.Find(name);if(node==null){node=new GameObject(name,typeof(RectTransform),typeof(Image)).transform;node.SetParent(root.transform,false);node.gameObject.layer=5;}parts[k]=node.GetComponent<Image>();}
                var im=parts[k];im.transform.SetParent(root.transform,false);im.transform.SetAsFirstSibling();im.raycastTarget=false;im.color=legacy.block.color;im.rectTransform.anchorMin=im.rectTransform.anchorMax=new Vector2(.5f,.5f);im.rectTransform.pivot=new Vector2(.5f,.5f);so.FindProperty(fields[k]).objectReferenceValue=im;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void SetSkinField(Transform root,Transform node,FieldEdit field)
        {
            Component component=null;foreach(var c in node.GetComponents<Component>())if(c!=null&&(c.GetType().Name==field.component||c.GetType().FullName==field.component)){component=c;break;}
            if(component==null)throw new InvalidOperationException(node.name+" missing "+field.component);
            var so=new SerializedObject(component);var p=so.FindProperty(field.property);if(p==null)throw new InvalidOperationException(field.component+" missing field "+field.property);
            switch(field.type)
            {
                case "string":p.stringValue=field.text;break;
                case "bool":p.boolValue=field.boolean;break;
                case "int":p.intValue=(int)field.number;break;
                case "float":p.floatValue=field.number;break;
                case "vector":p.vector2Value=field.vector;break;
                case "vector4":p.vector4Value=new Vector4(field.color.r,field.color.g,field.color.b,field.color.a);break;
                case "color":p.colorValue=field.color;break;
                case "sprite":p.objectReferenceValue=SpriteAsset(field.asset,field.sprite);break;
                case "asset":p.objectReferenceValue=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(field.asset);break;
                case "null":p.objectReferenceValue=null;break;
                case "reference":
                    var target=Locate(root,field.path);p.objectReferenceValue=field.text=="GameObject"?target.gameObject:target.GetComponent(field.text);break;
                default:throw new InvalidOperationException("Unsupported field edit "+field.type);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Audit()
        {
            RequireEdit();var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Path.Combine(Folder,"plan.json")));
            var report=new StringBuilder();int missing=0,events=0,invalidButtons=0;
            foreach(var page in plan.items)
            {
                var root=AssetDatabase.LoadAssetAtPath<GameObject>(page.prefab);int buttons=0;
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {
                    foreach(var c in t.GetComponents<Component>())if(c==null){missing++;report.AppendLine("FAIL missing script "+page.id+"/"+t.name);}
                    foreach(var btn in t.GetComponents<Button>())
                    {
                        buttons++;events+=btn.onClick.GetPersistentEventCount();
                        if(btn.targetGraphic==null){invalidButtons++;report.AppendLine("FAIL Button target "+page.id+"/"+t.name);}
                    }
                }
                report.AppendLine("PAGE "+page.id+" buttons="+buttons);
            }
            report.AppendLine("MissingScripts="+missing+" PersistentEvents="+events+" MissingButtonGraphics="+invalidButtons);
            File.WriteAllText(Path.Combine(Folder,"prefab-audit.txt"),report.ToString());
        }
    }
}
