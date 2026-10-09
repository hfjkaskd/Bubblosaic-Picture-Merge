using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static class HudFidelityAuthoring
    {
        const string Widget = "Assets/BizzaWZ/Final/Real/UI/GamePanel/Resources/Resources/GameUiWidget.prefab";
        const string Top = "Assets/BubblePics/RuntimePrefabs/UI/TopGameBar.prefab";
        const string Tool = "Assets/BubblePics/Resources/Prefabs/FrameworkBubbleProp.prefab";
        const string Toolbar = "Assets/BubblePics/RuntimePrefabs/UI/BubbleToolbar.prefab";
        const string Frames = "Assets/BubblePics/Resources/CoralV3/HudFidelityFrames.png";
        static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../ArtChanges/HudFidelity-20260928"));
        static TMP_FontAsset font;
        static Material outline;
        static readonly StringBuilder report = new StringBuilder();

        public static void ApplyAndBuild()
        {
            Apply();
            BizzaAndroidBuildInspection.BuildApk(Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/HudFidelity-20260928")));
        }

        public static void Apply()
        {
            Directory.CreateDirectory(Folder);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/Resources/AllUI20260924/Fonts/LilitaOne-SDF.asset");
            outline = AssetDatabase.LoadAssetAtPath<Material>("Assets/BubblePics/Resources/AllUI20260924/Fonts/LilitaOne-Outline.mat");
            if (font == null || outline == null) throw new InvalidOperationException("Missing authored HUD typeface.");
            ImportFrames();
            Edit(Widget, ConfigureWidget);
            Edit(Top, ConfigureStatus);
            Edit(Tool, ConfigureTool);
            Edit(Toolbar, root => Rect(root.transform,"HudMount/Toolbar/BarBg",0,-131,600,236));
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Folder,"applied.txt"),report.ToString());
        }

        static void ImportFrames()
        {
            File.Copy("Assets/BubblePics/Resources/CoralV3/CoralUIAtlas.png",Frames,true);
            AssetDatabase.ImportAsset(Frames,ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Frames);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp;
            foreach (string platform in new[]{"Android","iPhone"})
            {
                var settings=importer.GetPlatformTextureSettings(platform);
                settings.overridden=true;settings.maxTextureSize=2048;settings.format=TextureImporterFormat.ASTC_4x4;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            var factory = new SpriteDataProviderFactories();factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var old = provider.GetSpriteRects();
            var rects = new[]{
                Frame("Currency",367,117,356,122,new Vector4(55,50,55,50)),
                Frame("Conversion",739,122,234,115,Vector4.zero),
                Frame("Withdraw",985,122,253,115,Vector4.zero)
            };
            var pairs = new SpriteNameFileIdPair[rects.Length];
            for(int i=0;i<rects.Length;i++)
            {
                foreach(var prior in old) if(prior.name==rects[i].name) rects[i].spriteID=prior.spriteID;
                pairs[i]=new SpriteNameFileIdPair(rects[i].name,rects[i].spriteID);
            }
            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);
            provider.Apply();importer.SaveAndReimport();
        }

        static SpriteRect Frame(string name,float x,float top,float w,float h,Vector4 border)
        {
            return new SpriteRect{name=name,rect=new Rect(x,1254-top-h,w,h),border=border,
                pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=GUID.Generate()};
        }

        static void Edit(string path,Action<GameObject> edit)
        {
            string backup=Path.Combine(Folder,"Backup/BizzaWZ",path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if(!File.Exists(backup)) File.Copy(path,backup);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                int before=root.GetComponentsInChildren<Button>(true).Length;
                edit(root);
                int after=root.GetComponentsInChildren<Button>(true).Length;
                if(before!=after) throw new InvalidOperationException("Button count changed: "+path);
                foreach(var button in root.GetComponentsInChildren<Button>(true))
                {
                    if(button.targetGraphic==null) throw new InvalidOperationException("Button graphic missing: "+button.name);
                    if(button.onClick.GetPersistentEventCount()!=0) throw new InvalidOperationException("Persistent event binding: "+button.name);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
                report.AppendLine(path+" | buttons="+after+" | persistent events=0 | "+DateTime.UtcNow.ToString("O"));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static RectTransform Rect(Transform root,string path,float x,float y,float w,float h)
        {
            var rt=(RectTransform)root.Find(path);
            if(rt==null) throw new InvalidOperationException("Missing path: "+path);
            rt.anchoredPosition=new Vector2(x,y);rt.sizeDelta=new Vector2(w,h);
            return rt;
        }

        static void Label(TMP_Text label,float size,bool outlined=false,bool fit=false)
        {
            label.font=font;label.fontSharedMaterial=outlined?outline:font.material;
            label.fontStyle=FontStyles.Normal;label.fontWeight=FontWeight.Regular;
            label.enableAutoSizing=fit;label.fontSize=size;label.fontSizeMax=size;label.fontSizeMin=size*.72f;
            label.enableWordWrapping=false;label.overflowMode=TextOverflowModes.Overflow;
            label.margin=Vector4.zero;label.alignment=TextAlignmentOptions.Center;
            label.enableVertexGradient=false;label.characterSpacing=0;
            label.UpdateMeshPadding();
        }

        static void BindFrame(Transform node,string sprite,bool sliced=false)
        {
            var binder=node.GetComponent<CoralResourceSprite>();
            var data=new SerializedObject(binder);
            data.FindProperty("_resourcePath").stringValue="CoralV3/HudFidelityFrames";
            data.FindProperty("_spriteName").stringValue=sprite;data.ApplyModifiedPropertiesWithoutUndo();
            var image=node.GetComponent<Image>();image.sprite=null;image.type=sliced?Image.Type.Sliced:Image.Type.Simple;
            image.preserveAspect=false;image.pixelsPerUnitMultiplier=sliced?1.5f:1;
        }

        static void ConfigureWidget(GameObject root)
        {
            var currency=root.transform.Find("GameplayHudHeader/CurrencyBar");
            Label(currency.Find("Image/Text (TMP)").GetComponent<TMP_Text>(),60);
            foreach(bool coin in new[]{true,false})
            {
                string prefix="CurrentGroup/"+(coin?"GoldGroup/RealBtn":"DollarGroup/FakeBtn");
                var button=currency.Find(prefix);
                Rect(currency,prefix,0,0,350,100);BindFrame(button,"Currency",true);
                string box=coin?"CoinBox":"DollarBox";
                Rect(button,box,-66,0,176,100);
                var icon=Rect(button,box+"/Icon",-62,0,76,76).GetComponent<Image>();icon.preserveAspect=true;
                var iconData=new SerializedObject(icon.GetComponent<WzIconAmend>());
                iconData.FindProperty("skinAtlasResource").stringValue="CoralV3/CoralUIAtlas";
                iconData.FindProperty("skinSpriteName").stringValue=coin?"Coin":"Cash";
                iconData.FindProperty("singleCurrencySkinSpriteName").stringValue="Cash";
                iconData.ApplyModifiedPropertiesWithoutUndo();
                var amount=Rect(button,box+"/"+(coin?"GoldText":"DollarText"),21,0,92,64).GetComponent<TMP_Text>();
                Label(amount,38,false,true);
                var action=Rect(button,"ButtonView",coin?88:86,0,coin?148:168,76);
                BindFrame(action,coin?"Conversion":"Withdraw");
                var actionLabel=Rect(action,"Text (TMP)",coin?15:0,0,coin?109:157,58).GetComponent<TMP_Text>();
                Label(actionLabel,30,true,true);
                if(coin)
                {
                    var approximation=Rect(action,"Text (TMP) (1)",-54,0,24,54).GetComponent<TMP_Text>();
                    Label(approximation,22,true);
                }
            }
            var entry=root.transform.Find("FooterMount/EntryRow");
            ((RectTransform)entry).anchoredPosition=new Vector2(0,-151);
            Rect(entry,"SlotEnter",120,0,200,222);
            Rect(entry,"SlotEnter/Content/Progress",0,-65,148,43);
            Rect(entry,"DailyMissionItem/Badge",0,0,200,214);
            var progress=Rect(entry,"SlotEnter/Content/Progress/Text (TMP)",0,0,150,47).GetComponent<TMP_Text>();
            Label(progress,35,true);
            var reward=Rect(entry,"DailyMissionItem/Badge/Text (TMP)",0,-62,153,48).GetComponent<TMP_Text>();
            Label(reward,36,true,true);
        }

        static void ConfigureStatus(GameObject root)
        {
            foreach(string side in new[]{"TargetPanel","MovesPanel"})
            {
                string path="HudMount/TopGameBar/Panels/"+side+"/WhiteCard";
                Label(Rect(root.transform,path+"/Banner",0,38,287,52).GetComponent<TMP_Text>(),40,false,true);
                Label(Rect(root.transform,path+"/Num",0,-22,284,104).GetComponent<TMP_Text>(),80);
            }
        }

        static void ConfigureTool(GameObject root)
        {
            var data=new SerializedObject(root.GetComponent<ToolButton>());
            var count=(TMP_Text)data.FindProperty("_countBadge").objectReferenceValue;
            var ad=(TMP_Text)data.FindProperty("_adBadge").objectReferenceValue;
            var lv=(TMP_Text)data.FindProperty("_lvLabel").objectReferenceValue;
            Label(count,48,true,true);Label(ad,31,true);Label(lv,35,true);
            lv.rectTransform.anchoredPosition=new Vector2(0,-42);
            lv.rectTransform.sizeDelta=new Vector2(142,43);
            var parent=lv.transform.parent;
            var plate=parent.Find("LevelPlate");
            if(plate==null) plate=new GameObject("LevelPlate",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).transform;
            plate.SetParent(parent,false);plate.SetAsFirstSibling();
            var rt=(RectTransform)plate;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);
            rt.anchoredPosition=new Vector2(0,-42);rt.sizeDelta=new Vector2(132,42);
            var image=plate.GetComponent<Image>();image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=.15f;
            image.color=new Color(.38f,.41f,.45f,.72f);image.raycastTarget=false;
        }
    }
}
