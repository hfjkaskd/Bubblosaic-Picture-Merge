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
    public static class ReferenceHudAuthoring
    {
        const string Resource="CoralV3/ApprovedHudSource";
        const string Source="Assets/BubblePics/Resources/CoralV3/ApprovedHudSource.png";
        const string MaterialRoot="Assets/BubblePics/Resources/CoralV3/ReferenceMaterials/";
        const string Widget="Assets/BizzaWZ/Final/Real/UI/GamePanel/Resources/Resources/GameUiWidget.prefab";
        const string Tool="Assets/BubblePics/Resources/Prefabs/FrameworkBubbleProp.prefab";
        const string Top="Assets/BubblePics/RuntimePrefabs/UI/TopGameBar.prefab";
        const string Toolbar="Assets/BubblePics/RuntimePrefabs/UI/BubbleToolbar.prefab";
        const string World="Assets/BubblePics/RuntimePrefabs/Game/BubbleWorld.prefab";
        const string Background="Assets/BubblePics/Resources/CoralV3/CoralBackgroundReference.png";
        const float Scale=1080f/941f;
        static readonly string Folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/ReferenceHud-20260928"));
        static readonly string Output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/ReferenceHud-20260928"));
        [Serializable] public class Frame { public string name;public float[] rect,erase,outer,cap,polygon;public float radius,eraseRadius,eraseFeather=1,fillSampleU=-1;public string[] fill; }
        [Serializable] public class Spec { public Frame[] frames; }
        static readonly Dictionary<string,Frame> frames=new Dictionary<string,Frame>();
        static TMP_FontAsset font;
        static Material outline;

        public static void ApplyAndBuild()
        {
            Apply();
            BizzaAndroidBuildInspection.BuildApk(Output);
        }

        public static void Apply()
        {
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);
            Directory.CreateDirectory(MaterialRoot);AssetDatabase.Refresh();
            frames.Clear();foreach(var f in JsonUtility.FromJson<Spec>(File.ReadAllText(Path.Combine(Folder,"controls.json"))).frames)frames.Add(f.name,f);
            Import();
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/BaggageGo-Bold SDF.asset");
            string fontMaterial=MaterialRoot+"BaggageHudOutline.mat";
            outline=AssetDatabase.LoadAssetAtPath<Material>(fontMaterial);
            if(outline==null){outline=new Material(font.material);AssetDatabase.CreateAsset(outline,fontMaterial);}
            outline.SetFloat("_OutlineWidth",.025f);outline.SetColor("_OutlineColor",new Color(.03f,.18f,.4f,1));outline.EnableKeyword("OUTLINE_ON");EditorUtility.SetDirty(outline);
            Edit(Widget,ConfigureWidget);Edit(Toolbar,ConfigureToolbar);Edit(Tool,ConfigureTool);Edit(Top,ConfigureStatus);
            ImportBackground();Edit(World,ConfigureBackground);
            string appearancePath="Assets/BubblePics/Resources/CoralV3/ToolAppearance.asset";Backup(appearancePath);
            var appearance=AssetDatabase.LoadAssetAtPath<ToolAppearance>(appearancePath);
            appearance.BackgroundAtlasResource=Resource;appearance.NormalSprite="ToolNormal";appearance.LockedSprite="ToolLocked";
            appearance.NormalBackgroundMaterial=Material("ToolNormal");appearance.LockedBackgroundMaterial=Material("ToolLocked");EditorUtility.SetDirty(appearance);
            AssetDatabase.SaveAssets();
            ValidateToolStates();
            File.WriteAllText(Path.Combine(Folder,"applied.txt"),"Applied four HUD prefabs, the gameplay background, source-image regions, and tool state materials. "+DateTime.UtcNow.ToString("O"));
        }

        static void ImportBackground()
        {
            AssetDatabase.ImportAsset(Background,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(Background);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=1;importer.npotScale=TextureImporterNPOTScale.None;
            importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.maxTextureSize=2048;
            foreach(string platform in new[]{"Android","iPhone"}){var ps=importer.GetPlatformTextureSettings(platform);ps.overridden=true;ps.maxTextureSize=2048;ps.format=TextureImporterFormat.ASTC_4x4;importer.SetPlatformTextureSettings(ps);}
            importer.SaveAndReimport();
        }
        static void ConfigureBackground(GameObject root)
        {
            foreach(var binder in root.GetComponentsInChildren<CoralResourceSprite>(true))
            {
                var data=new SerializedObject(binder);var resource=data.FindProperty("_resourcePath");
                if(resource.stringValue!="CoralV3/CoralBackground" && resource.stringValue!="CoralV3/CoralBackgroundReference")continue;
                resource.stringValue="CoralV3/CoralBackgroundReference";data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void Import()
        {
            AssetDatabase.ImportAsset(Source,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(Source);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit=100;importer.npotScale=TextureImporterNPOTScale.None;
            importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.maxTextureSize=2048;importer.alphaIsTransparency=true;
            foreach(string platform in new[]{"Android","iPhone"}){var ps=importer.GetPlatformTextureSettings(platform);ps.overridden=true;ps.maxTextureSize=2048;ps.format=TextureImporterFormat.ASTC_4x4;importer.SetPlatformTextureSettings(ps);}
            importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();
            var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var previous=provider.GetSpriteRects();var rects=new List<SpriteRect>();var pairs=new List<SpriteNameFileIdPair>();
            foreach(var f in frames.Values)
            {
                var id=GUID.Generate();foreach(var old in previous)if(old.name==f.name){id=old.spriteID;break;}
                rects.Add(new SpriteRect{name=f.name,rect=new Rect(f.rect[0],1672-f.rect[1]-f.rect[3],f.rect[2],f.rect[3]),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=id});pairs.Add(new SpriteNameFileIdPair(f.name,id));
                string path=MaterialRoot+f.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(Shader.Find("BubblePics/UI/ApprovedHudFrame"));AssetDatabase.CreateAsset(mat,path);}
                mat.SetVector("_SourceRect",new Vector4(f.rect[0]/941f,(1672-f.rect[1]-f.rect[3])/1672f,f.rect[2]/941f,f.rect[3]/1672f));
                mat.SetVector("_SourceSize",new Vector4(f.rect[2],f.rect[3],0,0));
                mat.SetVector("_OuterRect",f.outer!=null&&f.outer.Length==4?V(f.outer):new Vector4(0,0,f.rect[2],f.rect[3]));
                mat.SetVector("_CapEllipse",f.cap!=null&&f.cap.Length==4?V(f.cap):Vector4.zero);
                mat.SetFloat("_OuterRadius",f.radius);mat.SetFloat("_MaskMode",1);
                // Convex outline follows the cash-note bundle; preserve the white artwork within it.
                if(f.polygon!=null && f.polygon.Length==12)
                {
                    mat.SetFloat("_MaskMode",2);for(int i=0;i<6;i++)mat.SetVector("_P"+i,new Vector4(f.polygon[i*2],f.polygon[i*2+1],0,0));
                }
                bool erase=f.erase!=null&&f.erase.Length==4;mat.SetFloat("_EraseOn",erase?1:0);
                if(erase){mat.SetVector("_EraseRect",V(f.erase));mat.SetFloat("_EraseRadius",f.eraseRadius);mat.SetFloat("_EraseFeather",f.eraseFeather);mat.SetFloat("_FillSampleU",f.fillSampleU);SetColor(mat,"_FillTop",f.fill[0]);SetColor(mat,"_FillMiddle",f.fill[1]);SetColor(mat,"_FillBottom",f.fill[2]);}
                EditorUtility.SetDirty(mat);
            }
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);provider.Apply();importer.SaveAndReimport();
        }

        static Vector4 V(float[] f)=>new Vector4(f[0],f[1],f[2],f[3]);
        static void SetColor(Material mat,string field,string html){ColorUtility.TryParseHtmlString(html,out var color);mat.SetColor(field,color);}
        static Material Material(string name)=>AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot+name+".mat");
        static void Backup(string path){string dest=Path.Combine(Folder,"Backup/BizzaWZ",path);Directory.CreateDirectory(Path.GetDirectoryName(dest));if(!File.Exists(dest))File.Copy(path,dest);}
        static void Edit(string path,Action<GameObject> apply)
        {
            Backup(path);var root=PrefabUtility.LoadPrefabContents(path);
            try{int count=root.GetComponentsInChildren<Button>(true).Length;apply(root);if(root.GetComponentsInChildren<Button>(true).Length!=count)throw new InvalidOperationException("Button count changed: "+path);foreach(var b in root.GetComponentsInChildren<Button>(true))if(b.targetGraphic==null||b.onClick.GetPersistentEventCount()!=0)throw new InvalidOperationException("Invalid Button: "+b.name);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static RectTransform Rect(Transform root,string path,float x,float y,float w,float h){var rt=(RectTransform)root.Find(path);if(rt==null)throw new InvalidOperationException(path);rt.anchoredPosition=new Vector2(x,y);rt.sizeDelta=new Vector2(w,h);return rt;}
        static void Text(TMP_Text label,float size,bool outlined=false,bool fit=false)
        {
            label.font=font;label.fontSharedMaterial=outlined?outline:font.material;label.fontStyle=FontStyles.Normal;label.fontWeight=FontWeight.Regular;
            label.fontSize=size;label.enableAutoSizing=fit;label.fontSizeMax=size;label.fontSizeMin=size*.72f;label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Overflow;
            label.alignment=TextAlignmentOptions.Center;label.margin=Vector4.zero;label.characterSpacing=0;label.UpdateMeshPadding();
        }
        static void Visual(Transform node,string name,bool resize=true)
        {
            var image=node.GetComponent<Image>();var binder=node.GetComponent<CoralResourceSprite>();if(binder==null)binder=node.gameObject.AddComponent<CoralResourceSprite>();
            var data=new SerializedObject(binder);data.FindProperty("_resourcePath").stringValue=Resource;data.FindProperty("_spriteName").stringValue=name;data.FindProperty("_image").objectReferenceValue=image;data.ApplyModifiedPropertiesWithoutUndo();
            image.sprite=null;image.material=Material(name);image.type=Image.Type.Simple;image.preserveAspect=false;image.color=Color.white;
            if(resize){var f=frames[name];image.rectTransform.sizeDelta=new Vector2(f.rect[2]*Scale,f.rect[3]*Scale);}
        }
        static void ConfigureWidget(GameObject root)
        {
            var header=root.transform.Find("GameplayHudHeader");((RectTransform)header).anchoredPosition=new Vector2(0,-72);
            var currency=header.Find("CurrencyBar");
            Visual(currency.Find("Image"),"Level");Text(currency.Find("Image/Text (TMP)").GetComponent<TMP_Text>(),70);
            Visual(currency.Find("PauseButton"),"Settings");
            var group=(RectTransform)currency.Find("CurrentGroup");group.anchoredPosition=new Vector2(0,-28);group.GetComponent<HorizontalLayoutGroup>().spacing=19.5f;
            foreach(bool coin in new[]{true,false})
            {
                var button=group.Find(coin?"GoldGroup/RealBtn":"DollarGroup/FakeBtn");Visual(button,"Currency");
                string box=coin?"CoinBox":"DollarBox";Rect(button,box,-64,0,176,100);
                var icon=Rect(button,box+"/Icon",-80,0,(coin?65:59)*Scale,(coin?66:69)*Scale).GetComponent<Image>();icon.material=Material(coin?"Coin":"Cash");
                var data=new SerializedObject(icon.GetComponent<WzIconAmend>());data.FindProperty("skinAtlasResource").stringValue=Resource;data.FindProperty("skinSpriteName").stringValue=coin?"Coin":"Cash";data.FindProperty("singleCurrencySkinSpriteName").stringValue="Cash";data.FindProperty("skinMaterial").objectReferenceValue=Material(coin?"Coin":"Cash");data.FindProperty("singleCurrencySkinMaterial").objectReferenceValue=Material("Cash");data.ApplyModifiedPropertiesWithoutUndo();
                Text(Rect(button,box+"/"+(coin?"GoldText":"DollarText"),coin?25:13,0,coin?96:88,64).GetComponent<TMP_Text>(),38,false,true);
                var action=button.Find("ButtonView");Visual(action,coin?"Conversion":"Withdraw");((RectTransform)action).anchoredPosition=new Vector2((coin?72.5f:68)*Scale+20,0);
                var actionLabel=Rect(action,"Text (TMP)",coin?17:0,0,coin?106:154,55).GetComponent<TMP_Text>();
                Text(actionLabel,30,true,true);
                if(!coin)Caption(action,actionLabel,"Withdraw","WithdrawCaption","Withdraw");
                if(coin)Text(Rect(action,"Text (TMP) (1)",-51,0,24,52).GetComponent<TMP_Text>(),24,true);
            }
            var entry=(RectTransform)root.transform.Find("FooterMount/EntryRow");entry.anchoredPosition=new Vector2(0,-146);
            var slot=entry.Find("SlotEnter");Visual(slot,"Slots",false);((RectTransform)slot).sizeDelta=new Vector2(150*Scale/.9f,164*Scale/.9f);
            Rect(slot,"Content/Progress",0,-49*Scale/.9f,148,43);Text(Rect(slot,"Content/Progress/Text (TMP)",0,0,150,48).GetComponent<TMP_Text>(),39,true);
            SlotEntryProgressAuthoring.Configure(slot);
            var crown=entry.Find("DailyMissionItem/Badge");Visual(crown,"Crown");Text(Rect(crown,"Text (TMP)",0,-51*Scale,155,50).GetComponent<TMP_Text>(),39,true,true);
        }
        static void ConfigureToolbar(GameObject root)
        {
            var bg=Rect(root.transform,"HudMount/Toolbar/BarBg",0,-130,527*Scale,194*Scale);Visual(bg,"Tray");bg.GetComponent<HorizontalLayoutGroup>().spacing=6;
        }
        static void ConfigureTool(GameObject root)
        {
            var data=new SerializedObject(root.GetComponent<ToolButton>());
            var bg=(Image)data.FindProperty("_bg").objectReferenceValue;Visual(bg.transform,"ToolNormal");
            var count=(TMP_Text)data.FindProperty("_countBadge").objectReferenceValue;var ad=(TMP_Text)data.FindProperty("_adBadge").objectReferenceValue;var lv=(TMP_Text)data.FindProperty("_lvLabel").objectReferenceValue;
            Text(count,48,true,true);Text(ad,31,true);Text(lv,35,true);lv.rectTransform.anchoredPosition=new Vector2(0,-42*Scale);lv.rectTransform.sizeDelta=new Vector2(138,44);
            var plate=lv.transform.parent.Find("LevelPlate");if(plate!=null)plate.gameObject.SetActive(false);
        }
        static void ConfigureStatus(GameObject root)
        {
            foreach(string side in new[]{"TargetPanel","MovesPanel"})
            {
                bool target=side=="TargetPanel";
                var card=root.transform.Find("HudMount/TopGameBar/Panels/"+side+"/WhiteCard");Visual(card,target?"Status":"MovesStatus");
                var title=Rect(card,"Banner",0,38,290,52).GetComponent<TMP_Text>();
                Text(title,36,false,true);Text(Rect(card,"Num",0,-22,284,104).GetComponent<TMP_Text>(),80);
                Caption(card,title,target?"Target":"Moves",target?"TargetCaption":"MovesCaption",target?"Status":"MovesStatus");
            }
        }

        static void Caption(Transform surface,TMP_Text label,string value,string authored,string translated)
        {
            var caption=surface.GetComponent<ApprovedHudCaption>();
            if(caption==null)caption=surface.gameObject.AddComponent<ApprovedHudCaption>();
            var data=new SerializedObject(caption);
            data.FindProperty("_label").objectReferenceValue=label;data.FindProperty("_surface").objectReferenceValue=surface.GetComponent<Image>();
            data.FindProperty("_authoredCaption").stringValue=value;data.FindProperty("_captionMaterial").objectReferenceValue=Material(authored);
            data.FindProperty("_translatedMaterial").objectReferenceValue=Material(translated);data.ApplyModifiedPropertiesWithoutUndo();
            string previous=label.text;
            label.text=value;caption.RefreshCaption();
            if(label.alpha!=0 || surface.GetComponent<Image>().material!=Material(authored))throw new InvalidOperationException("Authored caption failed: "+value);
            label.text="Texto traduzido";caption.RefreshCaption();
            if(label.alpha!=1 || surface.GetComponent<Image>().material!=Material(translated))throw new InvalidOperationException("Translated caption failed: "+value);
            label.text=previous;caption.RefreshCaption();
        }

        static void ValidateToolStates()
        {
            var root=PrefabUtility.LoadPrefabContents(Tool);
            try
            {
                var button=root.GetComponent<ToolButton>();
                var serialized=new SerializedObject(button);
                var bg=(Image)serialized.FindProperty("_bg").objectReferenceValue;
                var icon=(Image)serialized.FindProperty("_icon").objectReferenceValue;
                var count=(TMP_Text)serialized.FindProperty("_countBadge").objectReferenceValue;
                var ad=(TMP_Text)serialized.FindProperty("_adBadge").objectReferenceValue;
                var lv=(TMP_Text)serialized.FindProperty("_lvLabel").objectReferenceValue;
                int checks=0;
                foreach(var def in ToolDef.All)
                {
                    button.InitializePrefabRuntime(def);
                    var states=new[]{ToolState.Locked,ToolState.Free,ToolState.Count,ToolState.Ad};
                    foreach(var state in states)
                    {
                        bool locked=state==ToolState.Locked;
                        button.Refresh(locked?def.UnlockLevel-1:def.UnlockLevel,state==ToolState.Count?4:0,state==ToolState.Free,true,true);
                        if(button.State!=state || bg.sprite==null || bg.sprite.name!=(locked?"ToolLocked":"ToolNormal") || bg.material!=Material(locked?"ToolLocked":"ToolNormal"))
                            throw new InvalidOperationException("Tool state appearance mismatch: "+def.Id);
                        if(icon.gameObject.activeSelf==locked || lv.transform.parent.gameObject.activeSelf!=locked)
                            throw new InvalidOperationException("Tool icon/lock visibility mismatch: "+def.Id);
                        if(count.font!=font || ad.font!=font || lv.font!=font || lv.fontSharedMaterial!=outline)
                            throw new InvalidOperationException("Runtime overwrote authored tool font: "+def.Id);
                        checks++;
                    }
                }
                File.WriteAllText(Path.Combine(Output,"tool-state-checks.txt"),"PASS: "+checks+" prefab state transitions. Three tools x Locked/Free/Count/Ad. Sprite/material switching, icon/lock visibility and authored fonts retained. This is an EditMode component check, not an Android interaction capture.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
