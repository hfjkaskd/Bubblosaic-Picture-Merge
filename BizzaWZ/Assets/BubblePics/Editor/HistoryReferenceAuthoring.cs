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
    public static class HistoryReferenceAuthoring
    {
        const string PagePath="Assets/BizzaWZ/Final/Real/UI/WithdrawHistory/WithdrawHistory.prefab";
        const string ItemPath="Assets/BizzaWZ/Final/Real/UI/WithdrawHistory/WithdrawHistoryItem.prefab";
        const string Root="Assets/BubblePics/Resources/HistoryReference20260928/";
        const string Resource="HistoryReference20260928/ApprovedSource";
        const float S=2360f/1846f;
        static TMP_FontAsset font;
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        struct Slice
        {
            public string name;public Rect rect;public Vector4 border;
            public Slice(string n,float x,float y,float w,float h,Vector4 b=default){name=n;rect=new Rect(x,y,w,h);border=b;}
        }
        static readonly Slice[] slices={
            new Slice("Backdrop",0,0,852,1846),new Slice("Panel",39,201,776,1547),
            new Slice("Title",148,113,559,69),new Slice("Close",724,68,86,87),
            new Slice("Card",64,232,724,446,new Vector4(60,60,60,60)),
            new Slice("PayPal",108,272,294,111),
            new Slice("Processing",529,290,225,72),new Slice("Completed",529,747,225,72),new Slice("Failed",583,1204,171,72),
            new Slice("AccountCaption",109,405,113,32),new Slice("AmountCaption",109,535,113,32),new Slice("DateCaption",468,535,68,32),
            new Slice("Divider",107,506,642,7),new Slice("ColumnDivider",421,536,7,92),
            new Slice("ReasonPlate",106,1564,645,114,new Vector4(25,25,25,25)),new Slice("ReasonCaption",135,1580,111,34),
            new Slice("Thumb",788,285,13,187,new Vector4(6,6,6,6))
        };

        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring.");
            Directory.CreateDirectory(Root);
            string source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/c688f7f43f37-07-提现记录.png"));
            if(!File.Exists(Root+"ApprovedSource.png"))File.Copy(source,Root+"ApprovedSource.png");
            AssetDatabase.Refresh();Import();font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/BaggageGo-Bold SDF.asset");
            AuthorItem();AuthorPage();AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/HistoryReference-20260928/authoring.txt")),"Saved history page and reusable record prefab. Native scroll, close, channel, account, amount, date and state bindings retained. "+DateTime.UtcNow.ToString("O"));
        }

        static void AuthorItem()
        {
            var go=PrefabUtility.LoadPrefabContents(ItemPath);
            try
            {
                var item=go.GetComponent<WithdrawHistoryItem>();var tr=go.transform;
                foreach(var text in go.GetComponentsInChildren<TMP_Text>(true))Strip(text);
                foreach(var layout in go.GetComponentsInChildren<LayoutGroup>(true))layout.enabled=false;
                foreach(Transform child in tr)if(child.name=="Infos")child.gameObject.SetActive(false);
                var rt=(RectTransform)tr;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=Vector2.zero;rt.sizeDelta=new Vector2(708,428)*S;
                Vertical(tr,0);var flow=tr.GetComponent<VerticalLayoutGroup>();flow.padding=new RectOffset(0,0,Mathf.RoundToInt(34*S),Mathf.RoundToInt(29*S));Fit(tr);
                var bg=Child(tr,"bg");Stretch(bg);((RectTransform)bg).offsetMin=new Vector2(-8,-12)*S;((RectTransform)bg).offsetMax=new Vector2(8,6)*S;Visual(bg,"Card");bg.GetComponent<Image>().type=Image.Type.Sliced;bg.GetComponent<Image>().pixelsPerUnitMultiplier=1/S;bg.GetComponent<Image>().raycastTarget=true;Ignore(bg);bg.SetAsFirstSibling();
                var header=Row(tr,"Header",110);
                item.withdrawImg.transform.SetParent(header,false);Local(item.withdrawImg.transform,36,0,294,111);item.withdrawImg.preserveAspect=true;item.withdrawImg.raycastTarget=false;item.withdrawImg.material=Mat("Logo");
                string configPath=Root+"PaymentConfig.asset";var data=new SerializedObject(item);var sourceConfig=(PaymentConfig)data.FindProperty("paymentConfig").objectReferenceValue;
                if(!File.Exists(configPath))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceConfig),configPath);
                var config=AssetDatabase.LoadAssetAtPath<PaymentConfig>(configPath);foreach(var payment in config.PaymentDatas)if(payment.payeeAccountType==E_PayeeAccountType.Paypal)payment.paymentIcon=sprites["PayPal"];EditorUtility.SetDirty(config);data.FindProperty("paymentConfig").objectReferenceValue=config;
                Badge(item.processingObj.transform,header,"Processing","ui_history_processing","Processing",new Rect(457,18,225,72),new Color(.29f,.02f,.06f));
                Badge(item.successObj.transform,header,"Completed","ui_history_success","Completed",new Rect(457,18,225,72),new Color(.03f,.32f,.03f));
                Badge(item.failObj.transform,header,"Failed","ui_history_fail","Failed",new Rect(511,18,171,72),new Color(.6f,.02f,.03f));
                Row(tr,"AfterHeader",20);
                var account=Row(tr,"AccountLabel",36);StaticCaption(account,"AccountCaption","history_account","Account",new Rect(37,3,113,32),27);
                NativeRow(tr,item.emailTxt,52,39.7f);item.emailTxt.rectTransform.pivot=new Vector2(0,1);item.emailTxt.rectTransform.localScale=new Vector3(.906f,1,1);item.emailTxt.margin=new Vector4(36/.906f,0,30,0)*S;
                NativeRow(tr,item.nameTxt,52,31);
                NativeRow(tr,item.cpfTxt,52,31);
                Row(tr,"AfterAccount",16);
                var divider=Row(tr,"DividerRow",4);var line=Child(divider,"Line");Local(line,35,-2,642,7);Visual(line,"Divider");
                Row(tr,"BeforeFooter",22);
                var footer=Row(tr,"Footer",105);
                StaticCaption(footer,"AmountCaption","history_amount","Amount",new Rect(37,3,113,32),27);
                StaticCaption(footer,"DateCaption","history_date","Date",new Rect(396,3,68,32),27);
                var column=Child(footer,"ColumnDivider");Local(column,349,4,7,92);Visual(column,"ColumnDivider");
                item.amountTxt.transform.SetParent(footer,false);Local(item.amountTxt.transform,36,30,291,74);Label(item.amountTxt,65);item.amountTxt.rectTransform.localScale=new Vector3(.87f,1,1);item.amountTxt.fontSharedMaterial=ValueMaterial();item.amountTxt.enableWordWrapping=false;
                item.timeTxt.transform.SetParent(footer,false);Local(item.timeTxt.transform,397,38,312,66);Label(item.timeTxt,50);item.timeTxt.rectTransform.localScale=new Vector3(.905f,1,1);item.timeTxt.fontSharedMaterial=ValueMaterial();item.timeTxt.enableWordWrapping=false;

                var reason=Child(tr,"ReasonSection");reason.SetAsLastSibling();Vertical(reason,0);reason.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(0,0,Mathf.RoundToInt(16*S),0);Fit(reason);
                var box=Child(reason,"ReasonBox");Vertical(box,5);var boxFlow=box.GetComponent<VerticalLayoutGroup>();boxFlow.padding=new RectOffset(Mathf.RoundToInt(28*S),Mathf.RoundToInt(28*S),Mathf.RoundToInt(14*S),Mathf.RoundToInt(14*S));Fit(box);
                // Width is authored independently from the column flow, while height follows text.
                var width=Ensure<LayoutElement>(box);width.preferredWidth=width.minWidth=642*S;width.flexibleWidth=0;reason.GetComponent<VerticalLayoutGroup>().childForceExpandWidth=false;
                var reasonBg=Child(box,"Background");Stretch(reasonBg);Visual(reasonBg,"ReasonPlate");reasonBg.GetComponent<Image>().type=Image.Type.Sliced;reasonBg.GetComponent<Image>().pixelsPerUnitMultiplier=1/S;Ignore(reasonBg);reasonBg.SetAsFirstSibling();
                var reasonLabel=Row(box,"ReasonLabel",30);StaticCaption(reasonLabel,"ReasonCaption","history_reason","Reason",new Rect(0,0,111,34),27);
                item.dueText.transform.SetParent(box,false);item.dueText.transform.SetAsLastSibling();item.dueText.gameObject.SetActive(true);Label(item.dueText,38);item.dueText.rectTransform.pivot=new Vector2(0,1);item.dueText.rectTransform.localScale=new Vector3(.864f,1,1);item.dueText.margin=new Vector4(3/.864f,0,0,0)*S;item.dueText.color=new Color(.7f,.01f,.02f);item.dueText.fontSharedMaterial=ValueMaterial();item.dueText.enableAutoSizing=false;item.dueText.enableWordWrapping=true;var dueLayout=Ensure<LayoutElement>(item.dueText);dueLayout.ignoreLayout=false;dueLayout.minHeight=49*S;dueLayout.preferredHeight=-1;dueLayout.flexibleHeight=0;
                data.FindProperty("reasonSection").objectReferenceValue=reason.gameObject;data.FindProperty("dateFormat").stringValue="dd MMM yyyy";data.FindProperty("currencySeparator").stringValue="";data.ApplyModifiedPropertiesWithoutUndo();
                item.nameTxt.gameObject.SetActive(false);item.cpfTxt.gameObject.SetActive(false);reason.gameObject.SetActive(false);item.successObj.SetActive(false);item.failObj.SetActive(false);item.processingObj.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(go,ItemPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
        }

        static void AuthorPage()
        {
            var go=PrefabUtility.LoadPrefabContents(PagePath);
            try
            {
                var page=go.GetComponent<WithdrawHistory>();var tr=go.transform;int buttons=go.GetComponentsInChildren<Button>(true).Length;
                foreach(string name in new[]{"PageBackdrop","OpaqueUnderlay"}){var old=tr.Find(name);if(old!=null)old.gameObject.SetActive(false);}
                var backdrop=Child(tr,"ReferenceBackdrop");Box(backdrop,new Rect(0,0,852,1846));Visual(backdrop,"Backdrop");backdrop.SetAsFirstSibling();
                tr.Find("PageMask (1)").GetComponent<Image>().color=Color.clear;
                var bg=tr.Find("BG (2)");Box(bg,SliceRect("Panel"));Visual(bg,"Panel");bg.GetComponent<Image>().raycastTarget=true;
                var title=(tr.Find("HistoryTitle")??bg.Find("Text (TMP)")).GetComponent<TMP_Text>();title.transform.SetParent(tr,false);title.name="HistoryTitle";Box(title.transform,SliceRect("Title"));Label(title,61,TextAlignmentOptions.Center);title.color=Color.white;title.fontSharedMaterial=TitleMaterial();Localized(title.transform,"history_title");Caption(tr,title,"Title","Withdrawal history",SliceRect("Title"),true);
                var oldDecoration=bg.Find("Image (2)");if(oldDecoration!=null)oldDecoration.gameObject.SetActive(false);
                var close=tr.Find("CloseBtn");Box(close,SliceRect("Close"));var closeImage=close.Find("Image");Stretch(closeImage);Visual(closeImage,"Close");
                var container=tr.Find("Content");Box(container,new Rect(62,228,740,1488));
                var scroll=container.Find("Scroll View").GetComponent<ScrollRect>();Stretch(scroll.transform);scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=90;scroll.inertia=true;
                var viewport=scroll.viewport;Stretch(viewport);viewport.offsetMax=new Vector2(-12*S,0);var mask=viewport.GetComponent<Mask>();if(mask!=null)UnityEngine.Object.DestroyImmediate(mask);if(viewport.GetComponent<RectMask2D>()==null)viewport.gameObject.AddComponent<RectMask2D>();var viewportImage=viewport.GetComponent<Image>();viewportImage.color=Color.clear;viewportImage.raycastTarget=true;
                var clear=page.root.GetComponent<ScrollViewClearChilds>();if(clear!=null)UnityEngine.Object.DestroyImmediate(clear);
                foreach(var existing in page.root.GetComponentsInChildren<WithdrawHistoryItem>(true))UnityEngine.Object.DestroyImmediate(existing.gameObject);
                page.item=AssetDatabase.LoadAssetAtPath<WithdrawHistoryItem>(ItemPath);
                var content=(RectTransform)page.root;content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;content.localScale=Vector3.one;Vertical(content,28.2f);var layout=content.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(Mathf.RoundToInt(10*S),Mathf.RoundToInt(10*S),Mathf.RoundToInt(10*S),Mathf.RoundToInt(10*S));Fit(content);scroll.content=content;
                var bar=Child(tr,"HistoryScrollbar");Box(bar,new Rect(788,285,13,1388));var scrollbar=Ensure<Scrollbar>(bar);scrollbar.direction=Scrollbar.Direction.BottomToTop;scrollbar.transition=Selectable.Transition.None;
                var area=Child(bar,"SlidingArea");Stretch(area);var thumb=Child(area,"Handle");Stretch(thumb);Visual(thumb,"Thumb");thumb.GetComponent<Image>().type=Image.Type.Sliced;thumb.GetComponent<Image>().pixelsPerUnitMultiplier=1/S;thumb.GetComponent<Image>().raycastTarget=true;scrollbar.handleRect=(RectTransform)thumb;scrollbar.targetGraphic=thumb.GetComponent<Image>();scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
                var empty=page.emptyHint;empty.transform.SetParent(tr,false);Box(empty.transform,new Rect(126,800,600,240));Label(empty.GetComponent<TMP_Text>(),38,TextAlignmentOptions.Center);Localized(empty.transform,"history_empty");
                var loading=Child(tr,"LoadingHint");Box(loading,new Rect(126,800,600,240));Label(Text(loading),38,TextAlignmentOptions.Center);Localized(loading,"history_loading");
                var error=Child(tr,"ErrorHint");Box(error,new Rect(126,800,600,240));Label(Text(error),38,TextAlignmentOptions.Center);Localized(error,"history_error");
                var data=new SerializedObject(page);data.FindProperty("loadingHint").objectReferenceValue=loading.gameObject;data.FindProperty("errorHint").objectReferenceValue=error.gameObject;data.FindProperty("scrollView").objectReferenceValue=scroll;data.ApplyModifiedPropertiesWithoutUndo();
                loading.gameObject.SetActive(false);error.gameObject.SetActive(false);empty.SetActive(false);close.SetAsLastSibling();
                if(buttons!=go.GetComponentsInChildren<Button>(true).Length)throw new InvalidOperationException("Standard Button count changed.");
                foreach(var button in go.GetComponentsInChildren<Button>(true))if(button.targetGraphic==null||button.onClick.GetPersistentEventCount()!=0)throw new InvalidOperationException("Invalid Button binding.");
                PrefabUtility.SaveAsPrefabAsset(go,PagePath);
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
        }

        static void Badge(Transform tag,Transform parent,string slice,string key,string text,Rect rect,Color color)
        {
            tag.SetParent(parent,false);Local(tag,rect.x,rect.y,rect.width,rect.height);var background=tag.Find("Image");background.gameObject.SetActive(true);Stretch(background);Visual(background,slice,slice+"Blank");
            var label=tag.Find("text").GetComponent<TMP_Text>();Stretch(label.transform);Label(label,35,TextAlignmentOptions.Center);label.color=color;Localized(label.transform,key);
            var caption=Child(tag,"ReferenceCaption");Stretch(caption);Visual(caption,slice);BindCaption(caption,label,text,Mat(slice));
        }
        static void NativeRow(Transform parent,TMP_Text text,float height,float size){text.transform.SetParent(parent,false);text.transform.SetAsLastSibling();Height(text.transform,height);Label(text,size);text.fontSharedMaterial=ValueMaterial();text.margin=new Vector4(36,0,30,0)*S;text.enableWordWrapping=false;}
        static void StaticCaption(Transform parent,string slice,string key,string value,Rect rect,float size)
        {
            var label=Text(Child(parent,"Native"+slice));Local(label.transform,rect.x,rect.y,Mathf.Max(rect.width,130),rect.height);Label(label,size);label.color=new Color(.35f,.43f,.67f);Localized(label.transform,key);Caption(parent,label,slice,value,rect,false);
        }
        static void Caption(Transform parent,TMP_Text text,string slice,string caption,Rect rect,bool screen)
        {var surface=Child(parent,"Reference"+slice);if(screen)Box(surface,rect);else Local(surface,rect.x,rect.y,rect.width,rect.height);Visual(surface,slice);BindCaption(surface,text,caption,Mat(slice));}
        static void BindCaption(Transform surface,TMP_Text text,string caption,Material material)
        {
            var group=Ensure<CanvasGroup>(text.transform);group.blocksRaycasts=false;group.interactable=false;
            var component=Ensure<ApprovedHudCaption>(surface);var data=new SerializedObject(component);data.FindProperty("_label").objectReferenceValue=text;data.FindProperty("_surface").objectReferenceValue=surface.GetComponent<Image>();data.FindProperty("_authoredCaption").stringValue=caption;data.FindProperty("_captionMaterial").objectReferenceValue=material;data.FindProperty("_translatedMaterial").objectReferenceValue=Mat("EmptyCaption");data.FindProperty("_captionGroup").objectReferenceValue=group;data.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Strip(TMP_Text label){var old=label.GetComponent<UILanguageLabel>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);var local=label.GetComponent<CoralLocalizedLabel>();if(local!=null)UnityEngine.Object.DestroyImmediate(local);}
        static void Localized(Transform t,string key){var old=t.GetComponent<UILanguageLabel>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);var label=Ensure<CoralLocalizedLabel>(t);var data=new SerializedObject(label);data.FindProperty("key").stringValue=key;data.ApplyModifiedPropertiesWithoutUndo();}
        static T Ensure<T>(Component t) where T:Component {var value=t.GetComponent<T>();if(value==null)value=t.gameObject.AddComponent<T>();return value;}
        static TMP_Text Text(Transform t){var value=t.GetComponent<TMP_Text>();if(value==null)value=t.gameObject.AddComponent<TextMeshProUGUI>();return value;}
        static void Label(TMP_Text text,float size,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft){text.font=font;text.fontSharedMaterial=font.material;text.fontStyle=FontStyles.Normal;text.fontSize=text.fontSizeMax=size*S;text.fontSizeMin=size*S*.5f;text.enableAutoSizing=true;text.alignment=align;text.color=new Color(.03f,.02f,.32f);text.margin=Vector4.zero;text.raycastTarget=false;text.enableVertexGradient=false;}
        static Rect SliceRect(string name){foreach(var s in slices)if(s.name==name)return s.rect;throw new ArgumentException(name);}
        static Transform Child(Transform parent,string name){var t=parent.Find(name);if(t==null){t=new GameObject(name,typeof(RectTransform)).transform;t.SetParent(parent,false);t.gameObject.layer=5;}return t;}
        static void Box(Transform t,Rect box){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(box.center.x-426,923-box.center.y)*S;r.sizeDelta=box.size*S;r.localScale=Vector3.one;}
        static void Local(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y)*S;r.sizeDelta=new Vector2(w,h)*S;r.localScale=Vector3.one;}
        static void Stretch(Transform t){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;}
        static Transform Row(Transform root,string name,float height){var t=Child(root,name);t.SetAsLastSibling();Height(t,height);return t;}
        static void Height(Transform t,float value){var e=Ensure<LayoutElement>(t);e.ignoreLayout=false;e.minHeight=e.preferredHeight=value*S;e.flexibleHeight=0;}
        static void Ignore(Transform t){var e=Ensure<LayoutElement>(t);e.ignoreLayout=true;}
        static void Vertical(Transform t,float spacing){var v=Ensure<VerticalLayoutGroup>(t);v.enabled=true;v.padding=new RectOffset();v.spacing=spacing*S;v.childControlWidth=v.childControlHeight=true;v.childForceExpandWidth=true;v.childForceExpandHeight=false;v.childAlignment=TextAnchor.UpperCenter;}
        static void Fit(Transform t){var f=Ensure<ContentSizeFitter>(t);f.enabled=true;f.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;f.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");
        static Material Material(string name,string shader){var path=Root+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find(shader);EditorUtility.SetDirty(m);return m;}
        static Material ValueMaterial(){var m=Mat("ValueText");if(m!=null)return m;m=Material("ValueText","TextMeshPro/Distance Field");m.CopyPropertiesFromMaterial(font.material);m.SetFloat("_FaceDilate",-.035f);return m;}
        static Material TitleMaterial(){var m=Material("TitleText","TextMeshPro/Distance Field");m.CopyPropertiesFromMaterial(font.material);m.SetColor("_OutlineColor",new Color(.03f,.39f,.78f));m.SetFloat("_OutlineWidth",.025f);m.EnableKeyword("OUTLINE_ON");return m;}
        static void Visual(Transform t,string slice,string material=null)
        {
            var image=Ensure<Image>(t);var loader=Ensure<CoralResourceSprite>(t);var data=new SerializedObject(loader);data.FindProperty("_resourcePath").stringValue=Resource;data.FindProperty("_spriteName").stringValue=slice;data.FindProperty("_image").objectReferenceValue=image;data.ApplyModifiedPropertiesWithoutUndo();image.sprite=null;image.material=Mat(material??slice);image.type=Image.Type.Simple;image.color=Color.white;image.enabled=true;image.raycastTarget=t.GetComponent<Button>()!=null;
        }
        static void Erase(Material material,float sampleX,params Rect[] slots){material.SetFloat("_SampleX",sampleX);for(int i=0;i<6;i++){Rect r=i<slots.Length?slots[i]:default;material.SetVector("_Erase"+i,new Vector4(r.x,r.y,r.width,r.height));}}
        static void Import()
        {
            string path=Root+"ApprovedSource.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(var platform in new[]{"Android","iPhone"}){var settings=importer.GetPlatformTextureSettings(platform);settings.overridden=true;settings.maxTextureSize=2048;settings.format=TextureImporterFormat.ASTC_4x4;importer.SetPlatformTextureSettings(settings);}importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var old=provider.GetSpriteRects();var rects=new List<SpriteRect>();var names=new List<SpriteNameFileIdPair>();
            foreach(var slice in slices){var id=GUID.Generate();foreach(var prior in old)if(prior.name==slice.name){id=prior.spriteID;break;}var r=slice.rect;rects.Add(new SpriteRect{name=slice.name,rect=new Rect(r.x,1846-r.y-r.height,r.width,r.height),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),border=slice.border,spriteID=id});names.Add(new SpriteNameFileIdPair(slice.name,id));var material=Material(slice.name,"BubblePics/UI/HistoryReferencePlate");Erase(material,-1);material.SetFloat("_SampleY",-1);material.SetVector("_VisibleRect",Vector4.zero);}
            Erase(Mat("Backdrop"),-1,new Rect(135,98,579,101),new Rect(714,59,104,108));Mat("Backdrop").SetFloat("_EraseFeather",6);
            Erase(Mat("Panel"),-1,new Rect(63,231,724,1487),new Rect(786,282,17,194));
            Mat("Panel").SetFloat("_SampleY",684);Mat("Panel").SetFloat("_EraseRadius",48);Mat("Panel").SetFloat("_EraseFeather",1);
            Erase(Mat("Card"),-1,new Rect(103,267,654,122),new Rect(104,399,648,91),new Rect(103,503,650,12),new Rect(103,530,650,102));
            Mat("Card").SetVector("_VisibleRect",new Vector4(64,232,724,446));Mat("Card").SetFloat("_Radius",48);Mat("Card").SetFloat("_Feather",6);
            Erase(Mat("ReasonPlate"),-1,new Rect(131,1578,600,80));
            Mat("ReasonPlate").SetVector("_VisibleRect",new Vector4(106,1564,645,114));Mat("ReasonPlate").SetFloat("_Radius",26);Mat("ReasonPlate").SetFloat("_Feather",2);
            foreach(string badge in new[]{"Processing","Completed","Failed"}){var blank=Material(badge+"Blank","BubblePics/UI/ApprovedReferencePlate");Rect r=SliceRect(badge);Erase(blank,r.x+16,new Rect(r.x+22,r.y+15,r.width-44,r.height-28));}
            var logo=Material("Logo","BubblePics/UI/ApprovedWithdrawFrame");logo.SetFloat("_MaskMode",6);logo.SetFloat("_EraseOn",0);
            var empty=Material("EmptyCaption","UI/Default");empty.SetColor("_Color",Color.clear);
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);provider.Apply();importer.SaveAndReimport();sprites.Clear();foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))if(asset is Sprite sprite)sprites[sprite.name]=sprite;
        }
    }
}
