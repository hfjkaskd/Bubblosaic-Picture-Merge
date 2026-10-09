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
    // Editor authoring only. Rectangles are measured in the approved 852 x 1846 image.
    public static class PayPalReferenceAuthoring
    {
        const string FormPath="Assets/BizzaWZ/Final/Real/UI/WithdrawFillPanel/WithdrawFillPanel.prefab";
        const string Root="Assets/BubblePics/Resources/PayPalReference20260928/";
        const string Resource="PayPalReference20260928/ApprovedSource";
        const float S=2360f/1846f;
        static readonly string Audit=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/PayPalReference-20260928"));
        static TMP_FontAsset font;
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        struct Slice
        {
            public string name;public Rect rect;public float radius;public Vector4 erase;
            public Slice(string n,float x,float y,float w,float h,float r=0,Vector4 e=default){name=n;rect=new Rect(x,y,w,h);radius=r;erase=e;}
        }
        static readonly Slice[] slices={
            new Slice("Backdrop",0,0,852,1846),
            new Slice("TitleCaption",129,372,590,72),
            new Slice("AmountLabelCaption",246,764,366,46),
            new Slice("EmailLabelCaption",70,1002,280,45),
            new Slice("EmailHelpCaption",70,1204,440,42),
            new Slice("Top",0,0,852,309),new Slice("Bottom",0,1496,852,350),
            new Slice("Left",0,309,27,1187),new Slice("Right",826,309,26,1187),
            new Slice("Panel",27,309,799,1187,64,new Vector4(0,0,781,1170)),
            new Slice("Close",718,331,89,89,44),
            new Slice("Payment",67,501,718,205,34,new Vector4(0,0,699,185)),
            new Slice("PayPal",136,529,435,150),new Slice("Check",684,565,82,83,41),
            new Slice("Amount",67,736,718,218,35,new Vector4(0,0,718,218)),
            new Slice("Input",67,1053,718,132,34,new Vector4(0,0,697,112)),
            new Slice("Continue",67,1292,718,159,76,new Vector4(0,-1,540,102)),
            new Slice("ContinueCaption",67,1292,718,159,76)
        };
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode.");
            Directory.CreateDirectory(Audit);Directory.CreateDirectory(Root);
            if(!File.Exists(Audit+"/WithdrawFillPanel.before.prefab"))File.Copy(FormPath,Audit+"/WithdrawFillPanel.before.prefab");
            string source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/39d74e6c95e3-02-账号填写-PayPal.png"));
            if(!File.Exists(Root+"ApprovedSource.png"))File.Copy(source,Root+"ApprovedSource.png");
            AssetDatabase.Refresh();Import();
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/BaggageGo-Bold SDF.asset");
            string wayPath=Root+"PaymentWay.prefab";
            if(!File.Exists(wayPath))AssetDatabase.CopyAsset("Assets/BizzaWZ/Final/Real/UI/RealWithdrawalPanl/WithdrawWay.prefab",wayPath);
            var way=PrefabUtility.LoadPrefabContents(wayPath);
            try
            {
                var model=way.GetComponent<WithdrawWay>();
                string configPath=Root+"PaymentConfig.asset";
                if(!File.Exists(configPath))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(model.paymentConfig),configPath);
                var config=AssetDatabase.LoadAssetAtPath<PaymentConfig>(configPath);
                foreach(var payment in config.PaymentDatas)if(payment.payeeAccountType==E_PayeeAccountType.Paypal)payment.paymentIcon=sprites["PayPal"];
                EditorUtility.SetDirty(config);model.paymentConfig=config;StylePaymentIcon(model);
                Rect(way.transform,0,0,718,205);Visual(way.transform,"Payment");
                Rect(way.transform.Find("Frame"),-65,0,435,150);var logo=model.payIcon;logo.sprite=null;logo.type=Image.Type.Simple;logo.preserveAspect=true;logo.material=Mat("PayPal");logo.raycastTarget=false;ProportionalWidth(logo.rectTransform,.098f,.704f,150);
                Stretch(way.transform.Find("Select"));way.transform.Find("Select").GetComponent<Image>().enabled=false;
                var check=way.transform.Find("Select/CloudSelectionCheck");Rect(check,299,0,82,83);Visual(check,"Check");ProportionalWidth((RectTransform)check,.861f,.975f,83);check.GetComponent<Image>().preserveAspect=true;
                PrefabUtility.SaveAsPrefabAsset(way,wayPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(way);}
            var go=PrefabUtility.LoadPrefabContents(FormPath);
            try
            {
                var model=go.GetComponent<UIWithdrawalPanel>();int originalButtons=go.GetComponentsInChildren<Button>(true).Length;
                var email=model.paypalMailInput;var inputRefs=go.GetComponentsInChildren<AdvancedInputFieldPlugin.AdvancedInputField>(true);
                var tr=go.transform;Stretch(tr.Find("Root"));Stretch(tr.Find("Root/FillRoot"));
                var body=tr.Find("Root/FillRoot");
                foreach(var item in new[]{"Top","Bottom","Left","Right"}){var old=tr.Find("ReferenceBackdrop"+item);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);}
                var backdrop=EnsureChild(tr,"ReferenceBackdrop");Place(tr,backdrop,new Rect(0,0,852,1846));Visual(backdrop,"Backdrop");backdrop.SetSiblingIndex(tr.Find("Root").GetSiblingIndex());
                var bg=body.Find("BG");Place(tr,bg,new Rect(27,309,799,1187));Visual(bg,"Panel");bg.GetComponent<Image>().raycastTarget=true;SetTopPivot((RectTransform)bg);
                Place(tr,body.Find("Title"),new Rect(109,369,616,87));Label(body.Find("Title").GetComponent<TMP_Text>(),60);Localize(body.Find("Title"),"withdraw_form_title");
                CaptionGraphic(tr,body,body.Find("Title").GetComponent<TMP_Text>(),"TitleCaption","Withdrawal account");
                Place(tr,body.Find("pageClose"),new Rect(718,331,89,89));Visual(body.Find("pageClose"),"Close");
                Place(tr,body.Find("SelectPlatform"),new Rect(67,501,718,205));
                var wayRoot=body.Find("SelectPlatform");
                foreach(var layout in wayRoot.GetComponents<LayoutGroup>())if(!(layout is HorizontalLayoutGroup))UnityEngine.Object.DestroyImmediate(layout);
                var ways=wayRoot.GetComponent<HorizontalLayoutGroup>()??wayRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
                ways.spacing=15*S;ways.padding=new RectOffset();ways.childControlWidth=ways.childControlHeight=true;ways.childForceExpandWidth=ways.childForceExpandHeight=true;
                model.withdrawWayItem=AssetDatabase.LoadAssetAtPath<GameObject>(wayPath).GetComponent<WithdrawWay>();
                // Initial template rows also need the authored geometry before pool reuse.
                foreach(var initial in body.Find("SelectPlatform").GetComponentsInChildren<WithdrawWay>(true))
                {
                    initial.paymentConfig=model.withdrawWayItem.paymentConfig;StylePaymentIcon(initial);Rect(initial.transform,0,0,718,205);Visual(initial.transform,"Payment");
                    Rect(initial.payIcon.transform,-65,0,435,150);initial.payIcon.type=Image.Type.Simple;initial.payIcon.material=Mat("PayPal");initial.payIcon.preserveAspect=true;initial.payIcon.raycastTarget=false;ProportionalWidth(initial.payIcon.rectTransform,.098f,.704f,150);
                    Stretch(initial.selectedObj.transform);initial.selectedObj.GetComponent<Image>().enabled=false;
                    var check=initial.selectedObj.transform.Find("CloudSelectionCheck");Rect(check,299,0,82,83);Visual(check,"Check");ProportionalWidth((RectTransform)check,.861f,.975f,83);check.GetComponent<Image>().preserveAspect=true;
                }
                Place(tr,body.Find("Balence"),new Rect(67,501,718,205));Visual(body.Find("Balence"),"Payment");
                Rect(body.Find("Balence/Icon"),-65,0,435,150);model.paymentList=model.withdrawWayItem.paymentConfig;model.paymentImage.preserveAspect=true;
                var amount=body.Find("AmountPlate");Place(tr,amount,new Rect(67,736,718,218));Visual(amount,"Amount");
                Rect(amount.Find("Label"),0,58,670,57);Label(amount.Find("Label").GetComponent<TMP_Text>(),39);Localize(amount.Find("Label"),"withdraw_form_amount");
                CaptionGraphic(tr,amount,amount.Find("Label").GetComponent<TMP_Text>(),"AmountLabelCaption","Amount to withdraw");
                Rect(amount.Find("Amount"),0,-25,660,180);Label(model.balanceText,124,TextAlignmentOptions.Midline);var amountFont=AmountFont();model.balanceText.font=amountFont;var amountMaterial=FontMaterial("AmountText",new Color(.035f,.40f,.055f),.085f,amountFont);amountMaterial.shader=Shader.Find("TextMeshPro/Distance Field");amountMaterial.EnableKeyword("BEVEL_ON");amountMaterial.SetFloat("_Bevel",.3f);amountMaterial.SetFloat("_BevelWidth",.18f);amountMaterial.SetFloat("_BevelRoundness",.6f);amountMaterial.SetFloat("_LightAngle",3.9f);amountMaterial.SetFloat("_SpecularPower",.8f);amountMaterial.SetFloat("_Diffuse",0);amountMaterial.SetFloat("_Ambient",1);amountMaterial.SetFloat("_UnderlayOffsetY",-.48f);amountMaterial.SetFloat("_UnderlayDilate",.07f);amountMaterial.SetFloat("_UnderlaySoftness",.08f);amountMaterial.SetFloat("_FaceDilate",.08f);EditorUtility.SetDirty(amountMaterial);model.balanceText.fontSharedMaterial=amountMaterial;model.balanceText.color=Color.white;model.balanceText.enableVertexGradient=true;model.balanceText.colorGradient=new VertexGradient(new Color(.98f,1,.72f),new Color(.98f,1,.72f),new Color(.51f,.94f,.14f),new Color(.51f,.94f,.14f));model.balanceText.UpdateMeshPadding();
                var content=body.Find("pageContent");Place(tr,content,new Rect(67,994,718,457));SetTopPivot((RectTransform)content);Vertical(content,40);Fit(content);
                var info=content.Find("InfoContent");Rect(info,0,0,718,258);Vertical(info,0);Fit(info);foreach(Transform child in info){if(child.name!="EmailInfo"&&child.name!="PayPalEmailMessageSlot"&&child.GetComponent<LayoutElement>()==null)Height(child,((RectTransform)child).sizeDelta.y/S);}
                var emailGroup=info.Find("EmailInfo");Height(emailGroup,191);StretchWidth(emailGroup);
                Local(emailGroup.Find("Title"),0,0,718,59);Label(model.paypalMailTitle,40,TextAlignmentOptions.MidlineLeft);Localize(model.paypalMailTitle.transform,"withdraw_form_email");
                CaptionGraphic(tr,emailGroup,model.paypalMailTitle,"EmailLabelCaption","PayPal email",new Rect(3,8,280,45));
                Local(email.transform,0,59,718,132);ConfigureInput(email.transform);
                var slot=EnsureChild(info,"PayPalEmailMessageSlot");Height(slot,67);slot.SetSiblingIndex(emailGroup.GetSiblingIndex()+1);
                var error=model.paypalMailErrorTra;error.SetParent(slot,false);Stretch(error);var errorLabel=model.paypalMailError;Stretch(errorLabel.transform);Label(errorLabel,31,TextAlignmentOptions.MidlineLeft);errorLabel.color=new Color(.95f,.03f,.03f);errorLabel.margin=new Vector4(4*S,0,0,0);
                var helpContainer=EnsureChild(slot,"EmailHelpContainer");Stretch(helpContainer);var help=slot.Find("EmailHelp")??EnsureChild(helpContainer,"EmailHelp");help.SetParent(helpContainer,false);Stretch(help);var helpText=help.GetComponent<TMP_Text>()??help.gameObject.AddComponent<TextMeshProUGUI>();Label(helpText,31,TextAlignmentOptions.MidlineLeft);helpText.color=errorLabel.color;helpText.margin=errorLabel.margin;Localize(help,"withdraw_form_email_hint");
                CaptionGraphic(tr,helpContainer,helpText,"EmailHelpCaption","Check your email address",new Rect(3,19,440,42));
                var serialized=new SerializedObject(model);serialized.FindProperty("paypalEmailHelp").objectReferenceValue=helpContainer.gameObject;serialized.ApplyModifiedPropertiesWithoutUndo();
                if(!model.PaypalList.Contains(slot.gameObject))model.PaypalList.Add(slot.gameObject);if(!model.PagBankList.Contains(slot.gameObject))model.PagBankList.Add(slot.gameObject);
                var action=content.Find("BtnWithdrawal");Height(action,159);Visual(action,"Continue");Stretch(action.Find("Text (TMP)"));var caption=action.Find("Text (TMP)").GetComponent<TMP_Text>();Label(caption,66);caption.color=Color.white;caption.fontSharedMaterial=FontMaterial("ButtonText",new Color(.04f,.36f,.12f),.027f);Localize(caption.transform,"continue");
                var oldCaption=action.GetComponent<ApprovedHudCaption>();if(oldCaption!=null)UnityEngine.Object.DestroyImmediate(oldCaption);var captionImage=EnsureChild(action,"ReferenceContinueCaption");Stretch(captionImage);Visual(captionImage,"ContinueCaption");BindCaption(captionImage,caption,captionImage.GetComponent<Image>(),"Continue",Mat("ContinueCaption"),Mat("EmptyCaption"));
                model.defaultHeight=730*S;
                if(model.paypalMailInput!=email||go.GetComponentsInChildren<AdvancedInputFieldPlugin.AdvancedInputField>(true).Length!=inputRefs.Length)throw new InvalidOperationException("Input references changed.");
                if(go.GetComponentsInChildren<Button>(true).Length!=originalButtons)throw new InvalidOperationException("Button count changed.");
                foreach(var b in go.GetComponentsInChildren<Button>(true))if(b.targetGraphic==null||b.onClick.GetPersistentEventCount()!=0)throw new InvalidOperationException("Invalid Button: "+b.name);
                PrefabUtility.SaveAsPrefabAsset(go,FormPath);
                File.WriteAllText(Audit+"/authoring.txt","Authored PayPal form geometry from 852x1846 reference. Original input references and "+originalButtons+" standard Buttons preserved. "+DateTime.UtcNow.ToString("O"));
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
            AssetDatabase.SaveAssets();
        }
        static void StylePaymentIcon(WithdrawWay way)
        {var data=new SerializedObject(way);data.FindProperty("styledIcon").objectReferenceValue=sprites["PayPal"];data.FindProperty("styledIconMaterial").objectReferenceValue=Mat("PayPal");data.ApplyModifiedPropertiesWithoutUndo();}
        static void ConfigureInput(Transform t)
        {
            var bg=t.Find("Background");Stretch(bg);Visual(bg,"Input");bg.GetComponent<Image>().raycastTarget=true;
            var area=t.Find("TextArea");Stretch(area);((RectTransform)area).offsetMin=new Vector2(41,13)*S;((RectTransform)area).offsetMax=new Vector2(-22,-13)*S;
            var input=t.GetComponent<AdvancedInputFieldPlugin.AdvancedInputField>();input.CaretColor=new Color(.05f,.50f,1);var inputMaterial=FontMaterial("InputText",Color.clear,0);inputMaterial.DisableKeyword("UNDERLAY_ON");inputMaterial.SetFloat("_FaceDilate",-.052f);EditorUtility.SetDirty(inputMaterial);foreach(var label in t.GetComponentsInChildren<TMP_Text>(true)){Label(label,42,TextAlignmentOptions.MidlineLeft);label.fontSharedMaterial=inputMaterial;}
        }
        static void Localize(Transform t,string key)
        {
            var old=t.GetComponent<UILanguageLabel>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            var label=t.GetComponent<CoralLocalizedLabel>()??t.gameObject.AddComponent<CoralLocalizedLabel>();var data=new SerializedObject(label);data.FindProperty("key").stringValue=key;data.ApplyModifiedPropertiesWithoutUndo();
        }
        static void CaptionGraphic(Transform root,Transform parent,TMP_Text label,string slice,string text,Rect? localRect=null)
        {
            var surface=EnsureChild(parent,"Reference"+slice);if(localRect.HasValue){var r=localRect.Value;Local(surface,r.x,r.y,r.width,r.height);}else Place(root,surface,Array.Find(slices,x=>x.name==slice).rect);Visual(surface,slice);
            var layout=surface.GetComponent<LayoutElement>()??surface.gameObject.AddComponent<LayoutElement>();layout.ignoreLayout=true;
            // Keep a caption surface outside its text CanvasGroup so only the live glyphs are suppressed.
            if(parent==label.transform){surface.SetParent(parent.parent,false);Place(root,surface,Array.Find(slices,x=>x.name==slice).rect);}
            BindCaption(surface,label,surface.GetComponent<Image>(),text,Mat(slice),Mat("EmptyCaption"));
        }
        static void BindCaption(Transform owner,TMP_Text label,Image image,string text,Material normal,Material translated)
        {
            var group=label.GetComponent<CanvasGroup>();if(group==null)group=label.gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
            var component=owner.GetComponent<ApprovedHudCaption>()??owner.gameObject.AddComponent<ApprovedHudCaption>();var data=new SerializedObject(component);data.FindProperty("_label").objectReferenceValue=label;data.FindProperty("_surface").objectReferenceValue=image;data.FindProperty("_authoredCaption").stringValue=text;data.FindProperty("_captionMaterial").objectReferenceValue=normal;data.FindProperty("_translatedMaterial").objectReferenceValue=translated;data.FindProperty("_captionGroup").objectReferenceValue=group;data.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Label(TMP_Text t,float size,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {t.font=font;t.fontSharedMaterial=font.material;t.fontStyle=FontStyles.Normal;t.fontSize=size*S;t.fontSizeMin=size*S*.7f;t.fontSizeMax=size*S;t.enableAutoSizing=true;t.alignment=align;t.color=new Color(.03f,.02f,.32f);t.enableVertexGradient=false;t.raycastTarget=false;t.margin=Vector4.zero;t.UpdateMeshPadding();}
        static Material FontMaterial(string name,Color color,float width,TMP_FontAsset custom=null)
        {var source=custom!=null?custom:font;string path=Root+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(source.material);AssetDatabase.CreateAsset(m,path);}m.shader=source.material.shader;m.CopyPropertiesFromMaterial(source.material);m.SetFloat("_OutlineWidth",width);m.SetColor("_OutlineColor",color);m.EnableKeyword("OUTLINE_ON");m.SetColor("_UnderlayColor",color);m.SetFloat("_UnderlayOffsetY",-.12f);m.SetFloat("_UnderlaySoftness",.08f);m.EnableKeyword("UNDERLAY_ON");EditorUtility.SetDirty(m);return m;}
        static TMP_FontAsset AmountFont()
        {
            string path=Root+"FredokaAmount-SDF.asset";var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(asset!=null)return asset;
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/BubblePics/RuntimeFonts/PayPalReference/Fredoka-Bold.ttf");
            asset=TMP_FontAsset.CreateFontAsset(source,120,16,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic);
            asset.name="FredokaAmount-SDF";asset.TryAddCharacters("$0123456789.,≈Rpr€£¥₫฿₱₩₹₽ ");asset.atlasPopulationMode=AtlasPopulationMode.Static;asset.fallbackFontAssetTable=new List<TMP_FontAsset>{font};
            AssetDatabase.CreateAsset(asset,path);AssetDatabase.AddObjectToAsset(asset.material,asset);foreach(var atlas in asset.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,asset);EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();return asset;
        }
        static void Height(Transform t,float height){var e=t.GetComponent<LayoutElement>()??t.gameObject.AddComponent<LayoutElement>();e.preferredHeight=e.minHeight=height*S;e.flexibleHeight=0;}
        static void Vertical(Transform t,float spacing){var v=t.GetComponent<VerticalLayoutGroup>()??t.gameObject.AddComponent<VerticalLayoutGroup>();v.enabled=true;v.padding=new RectOffset();v.spacing=spacing*S;v.childControlWidth=v.childControlHeight=true;v.childForceExpandWidth=true;v.childForceExpandHeight=false;v.childAlignment=TextAnchor.UpperCenter;}
        static void Fit(Transform t){var f=t.GetComponent<ContentSizeFitter>()??t.gameObject.AddComponent<ContentSizeFitter>();f.enabled=true;f.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;f.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        static Transform EnsureChild(Transform parent,string name){var t=parent.Find(name);if(t==null){t=new GameObject(name,typeof(RectTransform)).transform;t.SetParent(parent,false);t.gameObject.layer=5;}return t;}
        static void Rect(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;r.anchoredPosition=new Vector2(x,y)*S;r.sizeDelta=new Vector2(w,h)*S;}
        static void Place(Transform root,Transform t,Rect b){Rect(t,0,0,b.width,b.height);var local=t.parent.InverseTransformPoint(root.TransformPoint(new Vector3((b.center.x-426)*S,(923-b.center.y)*S,0)));((RectTransform)t).anchoredPosition=(Vector2)local-((RectTransform)t.parent).rect.center;}
        static void Local(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.localScale=Vector3.one;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y)*S;r.sizeDelta=new Vector2(w,h)*S;}
        static void SetTopPivot(RectTransform r){Vector3 top=r.TransformPoint(new Vector3(0,r.rect.yMax));r.pivot=new Vector2(.5f,1);r.position=top;}
        static void Stretch(Transform t){var r=(RectTransform)t;r.localScale=Vector3.one;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.offsetMin=r.offsetMax=Vector2.zero;}
        static void StretchWidth(Transform t){var r=(RectTransform)t;r.anchorMin=new Vector2(0,1);r.anchorMax=new Vector2(1,1);r.pivot=new Vector2(.5f,1);r.sizeDelta=new Vector2(0,r.sizeDelta.y);}
        static void ProportionalWidth(RectTransform r,float left,float right,float height){r.anchorMin=new Vector2(left,.5f);r.anchorMax=new Vector2(right,.5f);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=Vector2.zero;r.sizeDelta=new Vector2(0,height*S);}
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");
        static void Visual(Transform t,string name)
        {var im=t.GetComponent<Image>()??t.gameObject.AddComponent<Image>();var loader=t.GetComponent<CoralResourceSprite>()??t.gameObject.AddComponent<CoralResourceSprite>();var data=new SerializedObject(loader);data.FindProperty("_resourcePath").stringValue=Resource;data.FindProperty("_spriteName").stringValue=name;data.FindProperty("_image").objectReferenceValue=im;data.ApplyModifiedPropertiesWithoutUndo();im.sprite=null;im.material=Mat(name);im.type=Image.Type.Simple;im.preserveAspect=false;im.color=Color.white;im.enabled=true;im.raycastTarget=t.GetComponent<Button>()!=null;}
        static void Import()
        {
            string path=Root+"ApprovedSource.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Multiple;ti.spritePixelsPerUnit=100;ti.mipmapEnabled=false;ti.npotScale=TextureImporterNPOTScale.None;ti.maxTextureSize=2048;ti.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(string platform in new[]{"Android","iPhone"}){var ps=ti.GetPlatformTextureSettings(platform);ps.overridden=true;ps.maxTextureSize=2048;ps.format=TextureImporterFormat.ASTC_4x4;ti.SetPlatformTextureSettings(ps);}ti.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(ti);provider.InitSpriteEditorDataProvider();var old=provider.GetSpriteRects();var rects=new List<SpriteRect>();var names=new List<SpriteNameFileIdPair>();
            foreach(var slice in slices)
            {
                GUID id=GUID.Generate();foreach(var previous in old)if(previous.name==slice.name){id=previous.spriteID;break;}
                Rect b=slice.rect;rects.Add(new SpriteRect{name=slice.name,rect=new Rect(b.x,1846-b.y-b.height,b.width,b.height),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=id});names.Add(new SpriteNameFileIdPair(slice.name,id));
                string mpath=Root+slice.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mpath);if(mat==null){mat=new Material(Shader.Find("BubblePics/UI/ApprovedWithdrawFrame"));AssetDatabase.CreateAsset(mat,mpath);}
                mat.SetVector("_SourceRect",new Vector4(b.x/852,(1846-b.y-b.height)/1846,b.width/852,b.height/1846));mat.SetVector("_SourceSize",new Vector4(b.width,b.height,0,0));mat.SetVector("_OuterRect",new Vector4(0,0,b.width,b.height));mat.SetFloat("_OuterRadius",slice.radius);mat.SetFloat("_MaskMode",slice.radius>0?1:0);mat.SetVector("_CapEllipse",Vector4.zero);mat.SetFloat("_EraseOn",slice.erase.z>0?1:0);mat.SetVector("_EraseRect",slice.erase);mat.SetFloat("_EraseRadius",slice.name=="Continue"?45:Mathf.Max(0,slice.radius-10));mat.SetFloat("_EraseFeather",2);mat.SetFloat("_FillSampleU",-1);
                string[] colors=slice.name=="Continue"?new[]{"#74F833","#35D515","#12B90D"}:slice.name=="Amount"?new[]{"#E5F2FF","#E5F2FF","#E5F2FF"}:new[]{"#F7FCFF","#F8FCFF","#F5FBFE"};for(int i=0;i<3;i++){ColorUtility.TryParseHtmlString(colors[i],out var c);mat.SetColor(new[]{"_FillTop","_FillMiddle","_FillBottom"}[i],c);}
                if(slice.name=="PayPal"){mat.SetFloat("_MaskMode",4);mat.SetFloat("_ChromaLow",.15f);mat.SetFloat("_ChromaHigh",.30f);}
                if(slice.name.EndsWith("Caption")&&slice.name!="ContinueCaption")mat.SetFloat("_MaskMode",6);
                if(slice.name=="Payment")mat.SetFloat("_FillSampleU",.07f);if(slice.name=="Amount")mat.SetFloat("_FillSampleU",.10f);if(slice.name=="Continue")mat.SetFloat("_FillSampleU",.10f);if(slice.name=="Input")mat.SetFloat("_FillSampleU",.035f);
                if(slice.name=="Backdrop")mat.shader=Shader.Find("BubblePics/UI/ApprovedBackgroundCutout");
                EditorUtility.SetDirty(mat);
            }
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);provider.Apply();ti.SaveAndReimport();sprites.Clear();foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))if(asset is Sprite sprite)sprites[sprite.name]=sprite;
            string emptyPath=Root+"EmptyCaption.mat";var empty=AssetDatabase.LoadAssetAtPath<Material>(emptyPath);if(empty==null){empty=new Material(Shader.Find("UI/Default"));AssetDatabase.CreateAsset(empty,emptyPath);}empty.SetColor("_Color",Color.clear);EditorUtility.SetDirty(empty);
        }
    }
}
