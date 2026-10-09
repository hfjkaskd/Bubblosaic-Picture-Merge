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
    // Offline prefab authoring. Runtime code only binds data, state and standard Button events.
    public static class TierReferenceAuthoring
    {
        const string PagePath="Assets/BizzaWZ/Final/Real/UI/WithdrawDanPanel/WithdrawDanPanel.prefab";
        const string ItemPath="Assets/BizzaWZ/Final/Real/UI/WithdrawDanPanel/WithdrawDanItem.prefab";
        const string Root="Assets/BubblePics/Resources/TierReference20260928/";
        const string Resource="TierReference20260928/ApprovedSource";
        const float W=853,H=1844,SX=2360f*1080f/2340f/W,SY=2360f/H;
        static TMP_FontAsset font,amountFont;
        struct Slice
        {
            public string name;public Rect rect;public Vector4 border;
            public Slice(string n,float x,float y,float w,float h,Vector4 b=default){name=n;rect=new Rect(x,y,w,h);border=b;}
        }
        static readonly Slice[] slices={
            new Slice("Backdrop",0,0,W,H),new Slice("Back",53,56,90,91),new Slice("History",614,56,88,92),new Slice("FAQ",713,56,90,92),
            new Slice("Title",303,57,250,84),new Slice("MainPanel",43,169,767,1228),new Slice("TargetPanel",43,1421,767,346),new Slice("BalanceCard",69,191,715,200),new Slice("BalanceTitle",313,201,228,45),
            new Slice("Card",61,401,731,324),new Slice("BronzeMedal",98,427,98,114),new Slice("SilverMedal",98,754,98,115),new Slice("GoldMedal",98,1082,98,114),
            new Slice("BronzeTitle",218,430,174,47),new Slice("SilverTitle",218,758,174,47),new Slice("GoldTitle",218,1086,174,47),
            new Slice("GreenFill",97,552,544,32,new Vector4(16,16,16,16)),new Slice("BlueFill",97,1209,452,32,new Vector4(16,16,16,16)),new Slice("Track",97,1209,544,32,new Vector4(16,16,16,16)),
            new Slice("Claimed",91,600,672,100),new Slice("Claim",91,928,672,102),new Slice("KeepPlaying",91,1257,672,102),
            new Slice("TargetTitle",75,1453,341,50),new Slice("TargetTrack",80,1525,621,42,new Vector4(21,21,21,21)),new Slice("TargetFill",80,1525,480,42,new Vector4(21,21,21,21)),
            new Slice("Withdraw",71,1634,712,115)
        };
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Root);
            if(!File.Exists(Root+"ApprovedSource.png"))File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/5222678c0b72-09-段位奖励与提现.png")),Root+"ApprovedSource.png");
            AssetDatabase.Refresh();Import();var sourceFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/BaggageGo-Bold SDF.asset");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"TextFont.asset");if(font==null){font=UnityEngine.Object.Instantiate(sourceFont);font.name="TierText";AssetDatabase.CreateAsset(font,Root+"TextFont.asset");}
            font.fallbackFontAssetTable=new List<TMP_FontAsset>(sourceFont.fallbackFontAssetTable){AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/Resources/FAQReference20260928/BodyFont.asset")};EditorUtility.SetDirty(font);
            amountFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/Resources/PayPalReference20260928/FredokaAmount-SDF.asset");
            AuthorItem();AuthorPage();AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/TierReference-20260928/authoring.txt")),"Saved tier page and reusable tier item prefabs, seven original mission/reward entries retained. "+DateTime.UtcNow.ToString("O"));
        }
        static void AuthorItem()
        {
            var go=PrefabUtility.LoadPrefabContents(ItemPath);
            try
            {
                var row=go.GetComponent<WithdrawDanItem>();var tr=go.transform;Clear(tr);Height(tr,308);
                var plate=Child(tr,"Card");Local(plate,-8,-8,731,324);Visual(plate,"Card");plate.GetComponent<Image>().raycastTarget=true;
                row.icon=Ensure<Image>(Child(tr,"FallbackMedal"));Local(row.icon.transform,29,18,98,114);row.icon.raycastTarget=false;row.icon.preserveAspect=true;
                row.danText=Text(Child(tr,"TierName"));Local(row.danText.transform,151,20,360,57);Label(row.danText,40);
                var titleGroup=Ensure<CanvasGroup>(row.danText);titleGroup.blocksRaycasts=false;titleGroup.interactable=false;
                var art=new List<GameObject>();string[] tiers={"Bronze","Silver","Gold"};
                for(int i=0;i<3;i++)
                {
                    var group=Child(tr,tiers[i]+"Art");Stretch(group);art.Add(group.gameObject);
                    var medal=Child(group,"Medal");Local(medal,29,18,98,114);Visual(medal,tiers[i]+"Medal");
                    Caption(group,row.danText,tiers[i]+"Title",tiers[i],new Rect(149,21,174,47),false);
                    group.gameObject.SetActive(i==0);
                }
                var money=Text(Child(tr,"Reward"));Local(money.transform,154,30,355,140);Label(money,68);Amount(money);row.moneyText1=row.moneyText2=row.moneyText3=money;
                row.hintText=Text(Child(tr,"Level"));Local(row.hintText.transform,469,24,218,49);Label(row.hintText,36,TextAlignmentOptions.MidlineRight);row.hintText.color=new Color(.01f,.22f,.67f);
                var progress=Child(tr,"Progress");Local(progress,28,143,544,32);Visual(progress,"Track");Sliced(progress);row.progressImage=Ensure<Image>(Child(progress,"Fill"));Stretch(row.progressImage.transform);Visual(row.progressImage.transform,"GreenFill");Sliced(row.progressImage.transform);Ensure<WithdrawCloudProgressFill>(row.progressImage);
                row.progressText=Text(Child(tr,"ProgressText"));Local(row.progressText.transform,580,137,106,45);Label(row.progressText,36,TextAlignmentOptions.MidlineRight);
                row.prepareStateBtn=Action(tr,"KeepPlaying",new Rect(22,191,672,102),"KeepPlaying","tier_keep_playing","Keep playing",44,false);
                row.claimStateBtn=Action(tr,"Claim",new Rect(22,191,672,102),"Claim","tier_claim","Claim",46,false);
                row.claimedStateBtn=Action(tr,"Claimed",new Rect(22,191,672,100),"Claimed","tier_claimed","Claimed",44,false);
                row.prepareStateObj=row.prepareStateBtn.gameObject;row.claimStateObj=row.claimStateBtn.gameObject;row.claimedStateObj=row.claimedStateBtn.gameObject;
                row.prepareStateObj.SetActive(true);row.claimStateObj.SetActive(false);row.claimedStateObj.SetActive(false);row.maxlevelTexts=new List<TMP_Text>();
                var data=new SerializedObject(row);var array=data.FindProperty("authoredTierArt");array.arraySize=3;for(int i=0;i<3;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=art[i];data.FindProperty("nativeTitleGroup").objectReferenceValue=titleGroup;data.FindProperty("artResource").stringValue=Resource;data.FindProperty("completeFill").stringValue="GreenFill";data.FindProperty("incompleteFill").stringValue="BlueFill";data.FindProperty("completeFillMaterial").objectReferenceValue=Mat("GreenFill");data.FindProperty("incompleteFillMaterial").objectReferenceValue=Mat("BlueFill");data.ApplyModifiedPropertiesWithoutUndo();
                Validate(go,3);PrefabUtility.SaveAsPrefabAsset(go,ItemPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void AuthorPage()
        {
            var go=PrefabUtility.LoadPrefabContents(PagePath);
            try
            {
                var page=go.GetComponent<WithdrawDanPanel>();var tr=go.transform;Clear(tr);
                var backdrop=Child(tr,"ReferenceBackdrop");Box(backdrop,new Rect(0,0,W,H));Visual(backdrop,"Backdrop");backdrop.GetComponent<Image>().raycastTarget=true;
                foreach(string panel in new[]{"MainPanel","TargetPanel"}){var surface=Child(tr,panel);Box(surface,R(panel));Visual(surface,panel);}
                var title=Text(Child(tr,"Title"));Box(title.transform,new Rect(168,57,426,84));Label(title,73,TextAlignmentOptions.Center);title.color=Color.white;Localized(title.transform,"tier_title");Caption(tr,title,"Title","My tier",R("Title"),true);
                page.closeBtn=Action(tr,"Back",R("Back"),"Back",null,null,0,true);
                page.historiyBtn=Action(tr,"History",R("History"),"History",null,null,0,true);
                page.faqBtn=Action(tr,"FAQ",R("FAQ"),"FAQ",null,null,0,true);
                var balance=Child(tr,"BalanceCard");Box(balance,R("BalanceCard"));Visual(balance,"BalanceCard");
                var balanceTitle=Text(Child(tr,"BalanceTitle"));Box(balanceTitle.transform,new Rect(190,201,473,48));Label(balanceTitle,40,TextAlignmentOptions.Center);Localized(balanceTitle.transform,"tier_balance");Caption(tr,balanceTitle,"BalanceTitle","Tier balance",R("BalanceTitle"),true);
                page.balanceTxt=Text(Child(tr,"Balance"));Box(page.balanceTxt.transform,new Rect(399,235.5f,354,150));Label(page.balanceTxt,79);Amount(page.balanceTxt);page.balanceTxt.characterSpacing=2;
                var scrollRoot=Child(tr,"TierScroll");Box(scrollRoot,new Rect(63,402,727,978));var scroll=Ensure<ScrollRect>(scrollRoot);scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=90;scroll.inertia=true;
                var viewport=Child(scrollRoot,"Viewport");Stretch(viewport);Ensure<Image>(viewport).color=Color.clear;viewport.GetComponent<Image>().raycastTarget=true;Ensure<RectMask2D>(viewport);scroll.viewport=(RectTransform)viewport;
                var content=Child(viewport,"Content");var rect=(RectTransform)content;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=Vector2.zero;
                var layout=Ensure<VerticalLayoutGroup>(content);layout.padding=new RectOffset(Mathf.RoundToInt(6*SX),Mathf.RoundToInt(6*SX),Mathf.RoundToInt(7*SY),Mathf.RoundToInt(7*SY));layout.spacing=20.5f*SY;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;layout.childAlignment=TextAnchor.UpperCenter;
                var fit=Ensure<ContentSizeFitter>(content);fit.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=rect;page.root=content;page.item=AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath).GetComponent<WithdrawDanItem>();
                var targetTitle=Text(Child(tr,"TargetTitle"));Box(targetTitle.transform,new Rect(76,1453,549,57));Label(targetTitle,42);Localized(targetTitle.transform,"tier_target");Caption(tr,targetTitle,"TargetTitle","Withdrawal target",R("TargetTitle"),true);
                var targetAmount=Text(Child(tr,"TargetAmount"));Box(targetAmount.transform,new Rect(606,1412,172,130));Label(targetAmount,65,TextAlignmentOptions.MidlineRight);Amount(targetAmount);targetAmount.characterSpacing=-4.5f;
                var track=Child(tr,"TargetProgress");Box(track,R("TargetTrack"));Visual(track,"TargetTrack");Sliced(track);page.progressImg=Ensure<Image>(Child(track,"Fill"));Stretch(page.progressImg.transform);Visual(page.progressImg.transform,"TargetFill");Sliced(page.progressImg.transform);Ensure<WithdrawCloudProgressFill>(page.progressImg);
                page.progressTxt=Text(Child(tr,"TargetPercent"));Box(page.progressTxt.transform,new Rect(708,1521,70,50));Label(page.progressTxt,31,TextAlignmentOptions.MidlineRight);
                page.hintTxt=Text(Child(tr,"TargetHint"));Box(page.hintTxt.transform,new Rect(80,1577,698,50));Label(page.hintTxt,34);page.hintTxt.enableWordWrapping=true;page.hintTxt.fontSizeMin=18*SY;
                page.withdrawBtn=Action(tr,"Withdraw",R("Withdraw"),"Withdraw","tier_withdraw","Withdraw",54,true);
                var data=new SerializedObject(page);data.FindProperty("tierScroll").objectReferenceValue=scroll;data.FindProperty("targetAmountText").objectReferenceValue=targetAmount;var keys=data.FindProperty("tierNameKeys");keys.arraySize=7;for(int i=0;i<7;i++)keys.GetArrayElementAtIndex(i).stringValue="tier_name_"+(i+1);data.ApplyModifiedPropertiesWithoutUndo();
                if(page.danLevelsUS.Count!=7||page.danLevelsID.Count!=7)throw new InvalidOperationException("Original seven-tier configuration missing.");Validate(go,4);PrefabUtility.SaveAsPrefabAsset(go,PagePath);
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static BizzaButton Action(Transform parent,string name,Rect rect,string slice,string key,string caption,float size,bool screen)
        {
            var t=Child(parent,name);if(screen)Box(t,rect);else Local(t,rect.x,rect.y,rect.width,rect.height);Visual(t,slice,key==null?slice:slice+"Blank");
            var owner=Ensure<BizzaButton>(t);owner.scaleTarget=t;owner.onClick=new UnityEvent();owner.onLongClick=new UnityEvent();owner.canDrag=!screen;
            var button=Ensure<WithdrawCloudButton>(t);button.targetGraphic=t.GetComponent<Image>();button.transition=Selectable.Transition.None;t.GetComponent<Image>().raycastTarget=true;
            var data=new SerializedObject(owner);data.FindProperty("standardButton").objectReferenceValue=button;data.ApplyModifiedPropertiesWithoutUndo();data=new SerializedObject(button);data.FindProperty("legacyOwner").objectReferenceValue=owner;data.ApplyModifiedPropertiesWithoutUndo();
            if(key!=null){var text=Text(Child(t,"Label"));Stretch(text.transform);Label(text,size,TextAlignmentOptions.Center);text.color=Color.white;Localized(text.transform,key);Caption(t,text,slice,caption,new Rect(0,0,rect.width,rect.height),false);}
            return owner;
        }
        static void Caption(Transform parent,TMP_Text label,string slice,string value,Rect rect,bool screen)
        {
            var surface=Child(parent,"Reference"+slice);if(screen)Box(surface,rect);else Local(surface,rect.x,rect.y,rect.width,rect.height);Visual(surface,slice);
            var group=Ensure<CanvasGroup>(label);group.blocksRaycasts=false;group.interactable=false;
            var data=new SerializedObject(Ensure<ApprovedHudCaption>(surface));data.FindProperty("_label").objectReferenceValue=label;data.FindProperty("_surface").objectReferenceValue=surface.GetComponent<Image>();data.FindProperty("_authoredCaption").stringValue=value;data.FindProperty("_captionMaterial").objectReferenceValue=Mat(slice);data.FindProperty("_translatedMaterial").objectReferenceValue=Mat("EmptyCaption");data.FindProperty("_captionGroup").objectReferenceValue=group;data.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Validate(GameObject go,int count){var buttons=go.GetComponentsInChildren<Button>(true);if(buttons.Length!=count)throw new InvalidOperationException("Standard Button count mismatch.");foreach(var b in buttons)if(b.targetGraphic==null||b.targetGraphic.gameObject!=b.gameObject||b.onClick.GetPersistentEventCount()!=0)throw new InvalidOperationException("Button must own its visual and use code-bound events.");}
        static void Clear(Transform t){var nested=t.GetComponentsInChildren<Transform>(true);foreach(var child in nested)if(child!=t&&PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))PrefabUtility.UnpackPrefabInstance(child.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);while(t.childCount>0)UnityEngine.Object.DestroyImmediate(t.GetChild(0).gameObject);}
        static void Localized(Transform t,string key){var data=new SerializedObject(Ensure<CoralLocalizedLabel>(t));data.FindProperty("key").stringValue=key;data.ApplyModifiedPropertiesWithoutUndo();}
        static void Label(TMP_Text t,float size,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft){Ensure<PreserveAuthoredFont>(t);t.font=font;t.fontSharedMaterial=font.material;t.fontStyle=FontStyles.Normal;t.fontSize=t.fontSizeMax=size*SY;t.fontSizeMin=size*SY*.55f;t.enableAutoSizing=true;t.alignment=alignment;t.color=new Color(.03f,.02f,.32f);t.enableWordWrapping=false;t.enableVertexGradient=false;t.margin=Vector4.zero;t.raycastTarget=false;}
        static void Amount(TMP_Text t){t.font=amountFont;var m=Mat("AmountText");if(m==null){m=new Material(amountFont.material);AssetDatabase.CreateAsset(m,Root+"AmountText.mat");}m.SetFloat("_FaceDilate",-.06f);EditorUtility.SetDirty(m);t.fontSharedMaterial=m;t.color=new Color(.01f,.50f,.17f);}
        static TMP_Text Text(Transform t){var value=t.GetComponent<TMP_Text>();if(value==null)value=t.gameObject.AddComponent<TextMeshProUGUI>();return value;}
        static T Ensure<T>(Component c) where T:Component{var value=c.GetComponent<T>();if(value==null)value=c.gameObject.AddComponent<T>();return value;}
        static Transform Child(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return go.transform;}
        static void Box(Transform t,Rect box){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2((box.center.x-W/2)*SX,(H/2-box.center.y)*SY);r.sizeDelta=new Vector2(box.width*SX,box.height*SY);r.localScale=Vector3.one;}
        static void Local(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x*SX,-y*SY);r.sizeDelta=new Vector2(w*SX,h*SY);r.localScale=Vector3.one;}
        static void Stretch(Transform t){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;}
        static void Height(Transform t,float h){var e=Ensure<LayoutElement>(t);e.minHeight=e.preferredHeight=h*SY;e.flexibleHeight=0;}
        static Rect R(string name){foreach(var s in slices)if(s.name==name)return s.rect;throw new ArgumentException(name);}
        static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>(Root+n+".mat");
        static Material Material(string name,string shader="BubblePics/UI/TierReferencePlate"){var m=Mat(name);if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,Root+name+".mat");}m.shader=Shader.Find(shader);EditorUtility.SetDirty(m);return m;}
        static void Visual(Transform t,string slice,string material=null){var im=Ensure<Image>(t);var data=new SerializedObject(Ensure<CoralResourceSprite>(t));data.FindProperty("_resourcePath").stringValue=Resource;data.FindProperty("_spriteName").stringValue=slice;data.FindProperty("_image").objectReferenceValue=im;data.ApplyModifiedPropertiesWithoutUndo();im.sprite=null;im.material=Mat(material??slice);im.color=Color.white;im.type=Image.Type.Simple;im.raycastTarget=false;}
        static void Sliced(Transform t){var im=t.GetComponent<Image>();im.type=Image.Type.Sliced;im.pixelsPerUnitMultiplier=1/SY;}
        static void Erase(Material m,params Rect[] rects){m.SetVector("_TextureSize",new Vector4(W,H,0,0));m.SetVector("_SamplePoint",Vector4.zero);m.SetFloat("_SampleX",-1);m.SetFloat("_SampleY",-1);m.SetFloat("_EraseRadius",0);m.SetFloat("_EraseFeather",1);m.SetVector("_VisibleRect",Vector4.zero);for(int i=0;i<6;i++){var r=i<rects.Length?rects[i]:default;m.SetVector("_Erase"+i,new Vector4(r.x,r.y,r.width,r.height));}}
        static void Rounded(Material m,Rect r,float radius){m.SetVector("_VisibleRect",new Vector4(r.x,r.y,r.width,r.height));m.SetFloat("_Radius",radius);m.SetFloat("_Feather",1);}
        static void Import()
        {
            var path=Root+"ApprovedSource.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(string platform in new[]{"Android","iPhone"}){var p=importer.GetPlatformTextureSettings(platform);p.overridden=true;p.maxTextureSize=2048;p.format=TextureImporterFormat.ASTC_4x4;importer.SetPlatformTextureSettings(p);}importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var prior=provider.GetSpriteRects();var rects=new List<SpriteRect>();var names=new List<SpriteNameFileIdPair>();
            foreach(var s in slices){var id=GUID.Generate();foreach(var old in prior)if(old.name==s.name){id=old.spriteID;break;}rects.Add(new SpriteRect{name=s.name,rect=new Rect(s.rect.x,H-s.rect.y-s.rect.height,s.rect.width,s.rect.height),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),border=s.border,spriteID=id});names.Add(new SpriteNameFileIdPair(s.name,id));Erase(Material(s.name));}
            Erase(Mat("Backdrop"),new Rect(300,52,260,91),new Rect(68,190,717,1187),new Rect(69,1445,716,306));Mat("Backdrop").SetFloat("_EraseFeather",2);
            Erase(Mat("MainPanel"),new Rect(67,189,719,1189));Mat("MainPanel").SetVector("_SamplePoint",new Vector4(426,400,1,0));Mat("MainPanel").SetFloat("_EraseRadius",34);Mat("MainPanel").SetFloat("_EraseFeather",4);
            Erase(Mat("TargetPanel"),new Rect(68,1442,718,310));Mat("TargetPanel").SetVector("_SamplePoint",new Vector4(426,1615,1,0));Mat("TargetPanel").SetFloat("_EraseRadius",30);Mat("TargetPanel").SetFloat("_EraseFeather",2);
            Erase(Mat("BalanceCard"),new Rect(310,202,235,43),new Rect(392,278,197,68));
            Erase(Mat("Card"),new Rect(96,425,666,280));Mat("Card").SetVector("_SamplePoint",new Vector4(426,545,1,0));Mat("Card").SetFloat("_EraseFeather",2);Rounded(Mat("Card"),R("Card"),43);
            Erase(Mat("Track"),new Rect(97,1209,544,32));Mat("Track").SetFloat("_SampleX",610);Rounded(Mat("Track"),R("Track"),16);
            Erase(Mat("TargetTrack"),R("TargetTrack"));Mat("TargetTrack").SetFloat("_SampleX",650);Rounded(Mat("TargetTrack"),R("TargetTrack"),21);
            foreach(string name in new[]{"Claimed","Claim","KeepPlaying","Withdraw"}){var r=R(name);var blank=Material(name+"Blank");Erase(blank,new Rect(r.x+130,r.y+23,r.width-260,r.height-41));blank.SetFloat("_SampleX",r.x+100);}
            var empty=Material("EmptyCaption","UI/Default");empty.SetColor("_Color",Color.clear);
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);provider.Apply();importer.SaveAndReimport();
        }
    }
}
