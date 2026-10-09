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
    // Offline prefab authoring. Native controls, localized text and live data remain independent.
    public static class NewPlayerReferenceAuthoring
    {
        const string PagePath="Assets/BizzaWZ/Final/Real/UI/FakeWithdrawPanel/FakeWithdrawPanel.prefab";
        const string ItemPath="Assets/BizzaWZ/Final/Real/UI/FakeWithdrawPanel/WithdrawAmountItem.prefab";
        const string Root="Assets/BubblePics/Resources/NewPlayerReference20260928/";
        const string Resource="NewPlayerReference20260928/ApprovedSource";
        const float W=853,H=1844,SX=2360f*1080f/2340f/W,SY=2360f/H;
        static TMP_FontAsset font,amountFont;
        struct Slice
        {
            public string name;public Rect rect;public Vector4 border;
            public Slice(string n,float x,float y,float w,float h,Vector4 b=default){name=n;rect=new Rect(x,y,w,h);border=b;}
        }
        static readonly Slice[] slices={
            new Slice("Backdrop",0,0,W,H),new Slice("Panel",42,183,770,1485),
            new Slice("Title",249,57,349,70),new Slice("Subtitle",321,126,211,48),
            new Slice("Back",52,56,92,92),new Slice("History",612,56,90,92),new Slice("FAQ",710,56,91,92),
            new Slice("BalanceTitle",319,245,218,45),new Slice("Balance",85,306,683,157),
            new Slice("MethodTitle",86,489,493,48),new Slice("Payment",86,550,683,180,new Vector4(32,32,32,32)),
            new Slice("PayPal",241,575,375,124),new Slice("Check",673,607,72,76),
            new Slice("AmountTitle",86,765,285,44),
            new Slice("AmountNormal",433,823,336,123),new Slice("AmountSelected",84,823,338,123),
            new Slice("AmountCheck",341,850,62,66),new Slice("Lock",695,859,45,55),
            new Slice("ProgressPlate",87,1236,681,181),new Slice("ProgressTitle",110,1258,279,40),
            new Slice("ProgressFill",112,1309,630,43,new Vector4(21,21,21,21)),
            new Slice("Withdraw",104,1445,647,143),new Slice("Footer",271,1597,313,35),
            new Slice("Service",713,1678,111,114),new Slice("ServiceBase",52,56,92,92),new Slice("ServiceBubble",739,1708,60,58),new Slice("RedDot",788,1682,34,36)
        };
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            Directory.CreateDirectory(Root);
            if(!File.Exists(Root+"ApprovedSource.png"))File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/091591df730e-10-新人提现界面.png")),Root+"ApprovedSource.png");
            AssetDatabase.Refresh();Import();
            var source=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/Resources/TierReference20260928/TextFont.asset");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"TextFont.asset");
            if(font==null){font=UnityEngine.Object.Instantiate(source);font.name="NewPlayerText";AssetDatabase.CreateAsset(font,Root+"TextFont.asset");}
            amountFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/Resources/PayPalReference20260928/FredokaAmount-SDF.asset");
            AuthorItem();AuthorPayment();AuthorPage();AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/NewPlayerReference-20260928/authoring.txt")),"Saved new-player page, amount option and payout option. Original country mission assets retained. "+DateTime.UtcNow.ToString("O"));
        }
        static void AuthorItem()
        {
            var go=PrefabUtility.LoadPrefabContents(ItemPath);
            try
            {
                var item=go.GetComponent<WithdrawAmountItem>();Clear(go.transform);Local(go.transform,0,0,336,123);
                Visual(go.transform,"AmountNormal");var button=Ensure<WithdrawCloudButton>(go.transform);button.targetGraphic=go.GetComponent<Image>();button.transition=Selectable.Transition.None;go.GetComponent<Image>().raycastTarget=true;
                var owner=Ensure<BizzaButton>(go.transform);owner.scaleTarget=go.transform;owner.onClick=new UnityEvent();owner.onLongClick=new UnityEvent();item.btn=owner;
                var data=new SerializedObject(owner);data.FindProperty("standardButton").objectReferenceValue=button;data.ApplyModifiedPropertiesWithoutUndo();data=new SerializedObject(button);data.FindProperty("legacyOwner").objectReferenceValue=owner;data.ApplyModifiedPropertiesWithoutUndo();
                var selected=Child(go.transform,"Selected");Stretch(selected);Visual(selected,"AmountSelected");item.selectObj=selected.gameObject;
                var check=Child(selected,"Check");Local(check,257,27,62,66);Visual(check,"AmountCheck");
                item.amountTxt=Text(Child(go.transform,"Amount"));Local(item.amountTxt.transform,35,24,213,75);Label(item.amountTxt,43,TextAlignmentOptions.Center);
                var locked=Child(go.transform,"Locked");Local(locked,262,36,45,55);Visual(locked,"Lock");data=new SerializedObject(item);data.FindProperty("lockedObj").objectReferenceValue=locked.gameObject;data.ApplyModifiedPropertiesWithoutUndo();
                item.getObj=Child(go.transform,"StarterAvailable").gameObject;
                item.getedObj=Child(go.transform,"StarterClaimed").gameObject;Local(item.getedObj.transform,20,93,290,22);var claimed=Text(item.getedObj.transform);Label(claimed,20,TextAlignmentOptions.Center);Localized(claimed.transform,"tier_claimed");
                item.getedObj.SetActive(false);selected.gameObject.SetActive(false);
                Validate(go,1);PrefabUtility.SaveAsPrefabAsset(go,ItemPath);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void AuthorPayment()
        {
            var go=new GameObject("PayoutOption",typeof(RectTransform));go.layer=5;
            try
            {
                var option=go.AddComponent<NewPlayerPayoutOption>();Local(go.transform,0,0,683,180);Visual(go.transform,"Payment");Sliced(go.transform);go.GetComponent<Image>().raycastTarget=true;
                option.button=go.AddComponent<Button>();option.button.targetGraphic=go.GetComponent<Image>();option.button.transition=Selectable.Transition.None;
                option.paymentConfig=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BizzaWZ/Final/Real/UI/RealWithdrawalPanl/WithdrawWay.prefab").GetComponent<WithdrawWay>().paymentConfig;
                var paypal=Child(go.transform,"PayPal");LogoRect(paypal);Visual(paypal,"PayPal");paypal.GetComponent<Image>().preserveAspect=true;option.paypalLogo=paypal.gameObject;
                option.otherLogo=Ensure<Image>(Child(go.transform,"OtherLogo"));LogoRect(option.otherLogo.transform);option.otherLogo.preserveAspect=true;option.otherLogo.raycastTarget=false;option.otherLogo.material=Mat("BrandLogo");
                var check=Child(go.transform,"Selected");var r=(RectTransform)check;r.anchorMin=new Vector2(.859f,.5f);r.anchorMax=new Vector2(.965f,.5f);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(0,-3*SY);r.sizeDelta=new Vector2(0,76*SY);Visual(check,"Check");check.GetComponent<Image>().preserveAspect=true;option.selection=check.gameObject;
                Validate(go,1);PrefabUtility.SaveAsPrefabAsset(go,Root+"PayoutOption.prefab");
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        static void LogoRect(Transform t)
        {
            var r=(RectTransform)t;r.anchorMin=new Vector2(.227f,.5f);r.anchorMax=new Vector2(.777f,.5f);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(0,124*SY);
        }
        static void AuthorPage()
        {
            var go=PrefabUtility.LoadPrefabContents(PagePath);
            try
            {
                var page=go.GetComponent<FakeWithdrawPanel>();var tr=go.transform;
                // Preserve the existing tutorial hand and all mission/config references.
                var hand=page.fingerObj;hand.transform.SetParent(null,false);Clear(tr);hand.transform.SetParent(tr,false);hand.SetActive(false);Box(hand.transform,new Rect(570,1510,95,95));
                foreach(string name in new[]{"Backdrop","Panel"}){var t=Child(tr,name);Box(t,R(name));Visual(t,name);t.GetComponent<Image>().raycastTarget=true;}
                Heading(tr,"Title","newplayer_title","Withdrawal",new Rect(153,57,455,70),72,Color.white);
                var backdropCaption=new SerializedObject(Ensure<ApprovedHudCaption>(tr.Find("Backdrop")));var titleLabel=tr.Find("Title").GetComponent<TMP_Text>();backdropCaption.FindProperty("_label").objectReferenceValue=titleLabel;backdropCaption.FindProperty("_surface").objectReferenceValue=tr.Find("Backdrop").GetComponent<Image>();backdropCaption.FindProperty("_authoredCaption").stringValue="Withdrawal";backdropCaption.FindProperty("_captionMaterial").objectReferenceValue=Mat("AuthoredHeaderBackdrop");backdropCaption.FindProperty("_translatedMaterial").objectReferenceValue=Mat("Backdrop");backdropCaption.FindProperty("_captionGroup").objectReferenceValue=titleLabel.GetComponent<CanvasGroup>();backdropCaption.ApplyModifiedPropertiesWithoutUndo();
                Heading(tr,"Subtitle","newplayer_subtitle","New player",R("Subtitle"),32,new Color(0,.55f,.9f));
                var close=Action(tr,"Back",R("Back"),"Back",null,null,0,true);var history=Action(tr,"History",R("History"),"History",null,null,0,true);var faq=Action(tr,"FAQ",R("FAQ"),"FAQ",null,null,0,true);
                Heading(tr,"BalanceTitle","newplayer_balance","My balance",new Rect(110,245,633,49),42,new Color(.03f,.02f,.32f));
                var balance=Child(tr,"BalancePlate");Box(balance,R("Balance"));Visual(balance,"Balance");page.balanceTxt=Text(Child(tr,"BalanceValue"));Box(page.balanceTxt.transform,new Rect(143,320,568,132));Label(page.balanceTxt,98,TextAlignmentOptions.Midline);Money(page.balanceTxt);page.balanceTxt.characterSpacing=2;
                Heading(tr,"MethodTitle","newplayer_method","Select withdrawal method",new Rect(88,489,677,51),40,new Color(.03f,.02f,.32f));
                var payout=Child(tr,"PayoutOptions");Box(payout,R("Payment"));var h=Ensure<HorizontalLayoutGroup>(payout);h.spacing=16*SX;h.childControlHeight=h.childControlWidth=h.childForceExpandHeight=h.childForceExpandWidth=true;
                Heading(tr,"AmountTitle","newplayer_amount","Select amount",new Rect(88,765,677,50),40,new Color(.03f,.02f,.32f));
                var choices=Child(tr,"Amounts");Box(choices,new Rect(84,823,685,385));var grid=Ensure<GridLayoutGroup>(choices);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;grid.cellSize=new Vector2(336*SX,123*SY);grid.spacing=new Vector2(13*SX,7.5f*SY);page.root=choices;page.item=AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath).GetComponent<WithdrawAmountItem>();page.items.Clear();
                var progress=Child(tr,"ProgressPlate");Box(progress,R("ProgressPlate"));Visual(progress,"ProgressPlate");
                page.hintTxt=Text(Child(tr,"Requirement"));Box(page.hintTxt.transform,new Rect(112,1256,626,49));Label(page.hintTxt,35);page.hintTxt.fontSizeMin=21*SY;page.hintTxt.enableWordWrapping=true;Caption(tr,page.hintTxt,"ProgressTitle","Requirements met",R("ProgressTitle"),true);
                var track=Child(tr,"ProgressTrack");Box(track,R("ProgressFill"));Visual(track,"ProgressFill","EmptyProgress");Sliced(track);page.progressImg=Ensure<Image>(Child(track,"Fill"));Stretch(page.progressImg.transform);Visual(page.progressImg.transform,"ProgressFill");Sliced(page.progressImg.transform);Ensure<WithdrawCloudProgressFill>(page.progressImg);
                page.progressTxt=Text(Child(tr,"ProgressCount"));Box(page.progressTxt.transform,new Rect(270,1358,313,45));Label(page.progressTxt,35,TextAlignmentOptions.Center);
                page.withdrawBtn=Action(tr,"Withdraw",R("Withdraw"),"Withdraw","tier_withdraw","Withdraw",65,true);
                Heading(tr,"Footer","newplayer_footer","Choose an eligible amount",new Rect(88,1595,677,44),29,new Color(.28f,.43f,.7f));
                var service=Action(tr,"Service",R("Service"),"ServiceBase",null,null,0,true);var bubble=Child(service.transform,"ChatBubble");Local(bubble,26,30,60,58);Visual(bubble,"ServiceBubble");var component=Ensure<ServiceBtn>(service);component.bizzaButton=service;var red=Child(service.transform,"RedDot");Local(red,75,4,34,36);Visual(red,"RedDot");component.redDot=red.GetComponent<Image>();
                hand.transform.SetAsLastSibling();
                var data=new SerializedObject(page);data.FindProperty("closeBtn").objectReferenceValue=close;data.FindProperty("faqBtn").objectReferenceValue=faq;data.FindProperty("historyBtn").objectReferenceValue=history;data.FindProperty("payoutRoot").objectReferenceValue=payout;data.FindProperty("payoutOption").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"PayoutOption.prefab").GetComponent<NewPlayerPayoutOption>();data.ApplyModifiedPropertiesWithoutUndo();
                WithdrawalServiceButtonAuthoring.Configure(go);
                Validate(go,5);PrefabUtility.SaveAsPrefabAsset(go,PagePath);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
        }
        static void Heading(Transform parent,string slice,string key,string text,Rect box,float size,Color color)
        {
            var t=Text(Child(parent,slice));Box(t.transform,box);Label(t,size,slice=="MethodTitle"||slice=="AmountTitle"?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Center);t.color=color;Localized(t.transform,key);Caption(parent,t,slice,text,R(slice),true);
        }
        static void Money(TMP_Text t)
        {
            t.font=amountFont;var mat=Mat("AmountText");if(mat==null){mat=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/BubblePics/Resources/PayPalReference20260928/AmountText.mat"));AssetDatabase.CreateAsset(mat,Root+"AmountText.mat");}
            mat.SetFloat("_UnderlayOffsetY",-.3f);mat.SetFloat("_UnderlaySoftness",.32f);mat.SetFloat("_UnderlayDilate",.04f);mat.SetColor("_UnderlayColor",new Color(0,.16f,.25f,.55f));mat.SetFloat("_FaceDilate",.05f);mat.SetFloat("_OutlineWidth",.055f);EditorUtility.SetDirty(mat);
            t.fontSharedMaterial=mat;t.color=Color.white;t.enableVertexGradient=true;t.colorGradient=new VertexGradient(new Color(1,1,.86f),new Color(1,1,.86f),new Color(.59f,.93f,.18f),new Color(.59f,.93f,.18f));t.UpdateMeshPadding();
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
        static void UnusedTierAmount(TMP_Text t){t.font=amountFont;var m=Mat("AmountText");if(m==null){m=new Material(amountFont.material);AssetDatabase.CreateAsset(m,Root+"AmountText.mat");}m.SetFloat("_FaceDilate",-.06f);EditorUtility.SetDirty(m);t.fontSharedMaterial=m;t.color=new Color(.01f,.50f,.17f);}
        static TMP_Text Text(Transform t){var value=t.GetComponent<TMP_Text>();if(value==null)value=t.gameObject.AddComponent<TextMeshProUGUI>();return value;}
        static T Ensure<T>(Component c) where T:Component{var value=c.GetComponent<T>();if(value==null)value=c.gameObject.AddComponent<T>();return value;}
        static Transform Child(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);return go.transform;}
        static void Box(Transform t,Rect box){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2((box.center.x-W/2)*SX,(H/2-box.center.y)*SY);r.sizeDelta=new Vector2(box.width*SX,box.height*SY);r.localScale=Vector3.one;}
        static void Local(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x*SX,-y*SY);r.sizeDelta=new Vector2(w*SX,h*SY);r.localScale=Vector3.one;}
        static void Stretch(Transform t){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;}
        static void Height(Transform t,float h){var e=Ensure<LayoutElement>(t);e.minHeight=e.preferredHeight=h*SY;e.flexibleHeight=0;}
        static Rect R(string name){foreach(var s in slices)if(s.name==name)return s.rect;throw new ArgumentException(name);}
        static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>(Root+n+".mat");
        static Material Material(string name,string shader="BubblePics/UI/NewPlayerReferencePlate"){var m=Mat(name);if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,Root+name+".mat");}m.shader=Shader.Find(shader);EditorUtility.SetDirty(m);return m;}
        static void Visual(Transform t,string slice,string material=null){var im=Ensure<Image>(t);var data=new SerializedObject(Ensure<CoralResourceSprite>(t));data.FindProperty("_resourcePath").stringValue=Resource;data.FindProperty("_spriteName").stringValue=slice;data.FindProperty("_image").objectReferenceValue=im;data.ApplyModifiedPropertiesWithoutUndo();im.sprite=null;im.material=Mat(material??slice);im.color=Color.white;im.type=Image.Type.Simple;im.raycastTarget=false;}
        static void Sliced(Transform t){var im=t.GetComponent<Image>();im.type=Image.Type.Sliced;im.pixelsPerUnitMultiplier=1/SY;}
        static void Erase(Material m,params Rect[] rects){m.SetFloat("_InkOnly",0);m.SetVector("_MirrorPatch",Vector4.zero);m.SetVector("_TextureSize",new Vector4(W,H,0,0));m.SetVector("_SamplePoint",Vector4.zero);m.SetFloat("_SampleX",-1);m.SetFloat("_SampleY",-1);m.SetFloat("_EraseRadius",0);m.SetFloat("_EraseFeather",1);m.SetVector("_VisibleRect",Vector4.zero);for(int i=0;i<6;i++){var r=i<rects.Length?rects[i]:default;m.SetVector("_Erase"+i,new Vector4(r.x,r.y,r.width,r.height));}}
        static void Rounded(Material m,Rect r,float radius){m.SetVector("_VisibleRect",new Vector4(r.x,r.y,r.width,r.height));m.SetFloat("_Radius",radius);m.SetFloat("_Feather",1);}
        static void Import()
        {
            var path=Root+"ApprovedSource.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(string platform in new[]{"Android","iPhone"}){var p=importer.GetPlatformTextureSettings(platform);p.overridden=true;p.maxTextureSize=2048;p.format=TextureImporterFormat.ASTC_4x4;importer.SetPlatformTextureSettings(p);}importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var prior=provider.GetSpriteRects();var rects=new List<SpriteRect>();var names=new List<SpriteNameFileIdPair>();
            foreach(var s in slices){var id=GUID.Generate();foreach(var old in prior)if(old.name==s.name){id=old.spriteID;break;}rects.Add(new SpriteRect{name=s.name,rect=new Rect(s.rect.x,H-s.rect.y-s.rect.height,s.rect.width,s.rect.height),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),border=s.border,spriteID=id});names.Add(new SpriteNameFileIdPair(s.name,id));Erase(Material(s.name));}
            Erase(Mat("Backdrop"),new Rect(245,54,359,126),new Rect(711,1677,115,118));
            Erase(Material("AuthoredHeaderBackdrop"),new Rect(711,1677,115,118));
            Erase(Mat("Panel"),new Rect(69,227,715,1410));Mat("Panel").SetVector("_SamplePoint",new Vector4(426,480,1,0));Mat("Panel").SetFloat("_EraseRadius",55);Mat("Panel").SetFloat("_EraseFeather",2);
            Erase(Mat("Balance"),new Rect(300,337,254,95));Mat("Balance").SetFloat("_EraseFeather",6);
            Erase(Mat("Payment"),new Rect(230,576,517,130));Mat("Payment").SetFloat("_SampleX",140);
            Erase(Mat("AmountNormal"),new Rect(476,857,192,58),new Rect(695,854,49,61));Mat("AmountNormal").SetFloat("_SampleX",680);
            Erase(Mat("AmountSelected"),new Rect(158,854,252,64));Mat("AmountSelected").SetFloat("_SampleX",140);
            Erase(Mat("ProgressPlate"),new Rect(109,1254,632,151));Mat("ProgressPlate").SetFloat("_SampleX",751);
            var track=Material("EmptyProgress");Erase(track,R("ProgressFill"));track.SetVector("_SamplePoint",new Vector4(735,1290,1,0));Rounded(track,R("ProgressFill"),21);track.SetColor("_Color",new Color(.78f,.86f,.95f,1));
            var blank=Material("WithdrawBlank");Erase(blank,new Rect(250,1476,341,76));blank.SetFloat("_SampleX",180);
            Erase(Mat("ServiceBase"),new Rect(73,75,46,54));Mat("ServiceBase").SetVector("_SamplePoint",new Vector4(117,101,1,0));Mat("ServiceBubble").SetFloat("_InkOnly",1);Rounded(Mat("ServiceBase"),R("ServiceBase"),46);
            foreach(string name in new[]{"BalanceTitle","MethodTitle","AmountTitle","ProgressTitle","Footer","PayPal","Lock"})Mat(name).SetFloat("_InkOnly",1);
            Rounded(Mat("ProgressPlate"),R("ProgressPlate"),30);
            Rounded(Mat("Withdraw"),R("Withdraw"),68);Mat("Withdraw").SetFloat("_Feather",4);
            Rounded(Mat("WithdrawBlank"),R("Withdraw"),68);Mat("WithdrawBlank").SetFloat("_Feather",4);
            Rounded(Mat("Service"),new Rect(715,1683,105,106),52.5f);
            Rounded(Mat("RedDot"),R("RedDot"),17);
            var brand=Material("BrandLogo","BubblePics/UI/ApprovedWithdrawFrame");brand.SetFloat("_MaskMode",6);brand.SetFloat("_EraseOn",0);brand.SetVector("_SourceRect",new Vector4(0,0,1,1));
            var empty=Material("EmptyCaption","UI/Default");empty.SetColor("_Color",Color.clear);
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);provider.Apply();importer.SaveAndReimport();
        }
    }
}
