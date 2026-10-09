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
    // All geometry, typography and artwork are saved into the production prefab.
    public static class ConfirmReferenceAuthoring
    {
        const string PrefabPath = "Assets/BizzaWZ/Final/Real/UI/UIWithdrawalConfirmPanel/UIWithdrawalConfirmPanel.prefab";
        const string Root = "Assets/BubblePics/Resources/ConfirmReference20260928/";
        const string Resource = "ConfirmReference20260928/ApprovedSource";
        const float S = 2360f / 1846f;
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static TMP_FontAsset font;

        struct Slice
        {
            public string name;
            public Rect rect;
            public float radius;
            public Vector4 erase;
            public Slice(string name, float x, float y, float width, float height, float radius = 0, Vector4 erase = default)
            { this.name=name; rect=new Rect(x,y,width,height); this.radius=radius; this.erase=erase; }
        }

        static readonly Slice[] Slices = {
            new Slice("Backdrop",0,0,852,1846),
            new Slice("Panel",40,335,771,1175,62,new Vector4(0,0,751,1155)),
            new Slice("Close",700,369,89,91,44),
            new Slice("TitleCaption",145,458,565,66),
            new Slice("Payment",194,565,464,177,36,new Vector4(0,0,444,157)),
            new Slice("PayPal",248,596,357,114),
            new Slice("AmountCaption",95,784,206,43),
            new Slice("Amount",95,838,661,145,28,new Vector4(0,0,644,128)),
            new Slice("AccountCaption",95,1020,340,47),
            new Slice("Account",95,1074,661,126,27,new Vector4(0,0,644,110)),
            new Slice("HintCaption",144,1240,568,42),
            new Slice("Submit",92,1322,666,140,64,new Vector4(0,0,510,102)),
            new Slice("SubmitCaption",92,1322,666,140,64)
        };

        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring.");
            string audit=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/ConfirmReference-20260928"));
            Directory.CreateDirectory(audit);Directory.CreateDirectory(Root);
            if(!File.Exists(audit+"/UIWithdrawalConfirmPanel.before.prefab"))File.Copy(PrefabPath,audit+"/UIWithdrawalConfirmPanel.before.prefab");
            string source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/54466ffa3f0a-05-确认提现账号.png"));
            if(!File.Exists(Root+"ApprovedSource.png"))File.Copy(source,Root+"ApprovedSource.png");
            AssetDatabase.Refresh();Import();
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/BaggageGo-Bold SDF.asset");
            var go=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var model=go.GetComponent<UIWithdrawalConfirmPanel>();
                var bindings=new[]{model.CPF_CNPJText,model.NameText,model.EmailText,model.PaymentValueText};
                int buttons=go.GetComponentsInChildren<Button>(true).Length;
                var tr=go.transform;
                Transform Find(string path)=>tr.Find(path)??tr.Find("ReferencePanel/"+path)??tr.Find("ReferencePanel/SubmitRow/"+path)??tr.Find("ReferencePanel/HintRow/"+path);
                var bg=Find("BG");var title=Find("Title");var submit=Find("BtnOk");var close=Find("comfirmClose");var details=Find("txtContext");
                var paymentPlate=Find("CloudPaymentPlate")??tr.Find("ReferencePanel/PaymentRow/CloudPaymentPlate");
                var hint=Find("Text (TMP)");
                var backdrop=Child(tr,"ReferenceBackdrop");Rect(backdrop,0,0,852,1846);Visual(backdrop,"Backdrop");backdrop.SetSiblingIndex(0);
                var mask=tr.Find("PageMask").GetComponent<Image>();mask.color=Color.clear;
                var panel=Child(tr,"ReferencePanel");Rect(panel,-.5f,.5f,771,1175);
                Vertical(panel,0);var layout=panel.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(0,0,Mathf.RoundToInt(115*S),Mathf.RoundToInt(48*S));Fit(panel);
                bg.SetParent(panel,false);Stretch(bg);Visual(bg,"Panel");bg.GetComponent<Image>().raycastTarget=true;Ignore(bg);bg.SetAsFirstSibling();

                Row(panel,title,75);Stretch(title.GetChild(0));Label(title.GetChild(0).GetComponent<TMP_Text>(),62);Localize(title.GetChild(0),"withdraw_confirm_title");
                Caption(title,title.GetChild(0).GetComponent<TMP_Text>(),"TitleCaption","Confirm withdrawal",new Rect(105,8,565,66));
                Spacer(panel,"AfterTitle",40);
                var paymentRow=Child(panel,"PaymentRow");Row(panel,paymentRow,177);
                paymentPlate.SetParent(paymentRow,false);Rect(paymentPlate,0,0,464,177);Visual(paymentPlate,"Payment");
                model.paymentImage.transform.SetParent(paymentRow,false);Rect(model.paymentImage.transform,0,0,357,114);model.paymentImage.material=Mat("PayPal");model.paymentImage.preserveAspect=true;model.paymentImage.raycastTarget=false;
                string configPath=Root+"PaymentConfig.asset";
                if(!File.Exists(configPath))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(model.paymentList),configPath);
                var config=AssetDatabase.LoadAssetAtPath<PaymentConfig>(configPath);
                foreach(var data in config.PaymentDatas)if(data.payeeAccountType==E_PayeeAccountType.Paypal)data.paymentIcon=Sprites["PayPal"];
                EditorUtility.SetDirty(config);model.paymentList=config;

                Spacer(panel,"AfterPayment",40);
                var amount=Child(panel,"AmountRow");Row(panel,amount,201);
                var amountLabel=Child(amount,"Label");Local(amountLabel,55,0,661,56);var amountText=Text(amountLabel);Label(amountText,41,TextAlignmentOptions.MidlineLeft);Localize(amountLabel,"withdraw_confirm_amount");
                Caption(amount,amountText,"AmountCaption","Amount",new Rect(55,2,206,43));
                var amountPlate=Child(amount,"Plate");Local(amountPlate,55,56,661,145);Visual(amountPlate,"Amount");
                model.PaymentValueText.transform.SetParent(amountPlate,false);Stretch(model.PaymentValueText.transform);Label(model.PaymentValueText,95,TextAlignmentOptions.Midline);model.PaymentValueText.fontSharedMaterial=ValueMaterial();model.PaymentValueText.transform.localScale=new Vector3(1,1.1f,1);model.PaymentValueText.margin=new Vector4(16,0,16,0)*S;

                Spacer(panel,"BeforeDetails",35);
                details.SetParent(panel,false);details.SetAsLastSibling();Vertical(details,22);Fit(details);
                ConfigureDetail(model.EmailObj.transform,model.EmailitleText,model.EmailText,"AccountText",true);
                ConfigureDetail(model.NameObj.transform,model.NameTitleText,model.NameText,"Name_input",false);
                ConfigureDetail(model.CPFObj.transform,model.CPFTitleText,model.CPF_CNPJText,"account_input",false);
                var localized=Localize(model.EmailitleText.transform,"withdraw_form_email");
                Caption(model.EmailObj.transform,model.EmailitleText,"AccountCaption","PayPal email",new Rect(55,2,340,47));
                var serialized=new SerializedObject(model);serialized.FindProperty("accountCaption").objectReferenceValue=localized;serialized.ApplyModifiedPropertiesWithoutUndo();

                Spacer(panel,"BeforeHint",39);var hintRow=Child(panel,"HintRow");Row(panel,hintRow,47);hint.SetParent(hintRow,false);Stretch(hint);var hintText=hint.GetComponent<TMP_Text>();Label(hintText,33);hintText.color=new Color(.28f,.35f,.68f);Localize(hint,"withdraw_confirm_hint");
                Caption(hintRow,hintText,"HintCaption","Check your details before submitting.",new Rect(104,3,568,42));
                Spacer(panel,"BeforeSubmit",37);
                var submitRow=Child(panel,"SubmitRow");Row(panel,submitRow,140);submit.SetParent(submitRow,false);Rect(submit,0,0,666,140);Visual(submit,"Submit");
                var submitText=submit.Find("Text (TMP)").GetComponent<TMP_Text>();Stretch(submitText.transform);Label(submitText,67);submitText.color=Color.white;submitText.fontSharedMaterial=ButtonMaterial();Localize(submitText.transform,"withdraw_confirm_submit");
                Caption(submit,submitText,"SubmitCaption","Submit",new Rect(0,0,666,140));

                close.SetParent(panel,false);Ignore(close);var closeRect=(RectTransform)close;closeRect.anchorMin=closeRect.anchorMax=Vector2.one;closeRect.pivot=new Vector2(.5f,.5f);closeRect.localScale=Vector3.one;closeRect.sizeDelta=new Vector2(89,91)*S;closeRect.anchoredPosition=new Vector2(-66.5f,-79.5f)*S;Visual(close,"Close");close.SetAsLastSibling();
                if(buttons!=go.GetComponentsInChildren<Button>(true).Length)throw new InvalidOperationException("Button count changed.");
                if(bindings[0]!=model.CPF_CNPJText||bindings[1]!=model.NameText||bindings[2]!=model.EmailText||bindings[3]!=model.PaymentValueText)throw new InvalidOperationException("Dynamic data binding changed.");
                foreach(var button in go.GetComponentsInChildren<Button>(true))if(button.targetGraphic==null||button.onClick.GetPersistentEventCount()!=0)throw new InvalidOperationException("Invalid standard Button.");
                PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
                File.WriteAllText(audit+"/authoring.txt","Saved confirmation prefab. Preserved "+buttons+" standard Buttons and all account/amount bindings. "+DateTime.UtcNow.ToString("O"));
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
            AssetDatabase.SaveAssets();
        }

        static void ConfigureDetail(Transform row,TMP_Text label,TMP_Text value,string plateName,bool email)
        {
            Height(row,181);Local(label.transform,55,0,661,56);Label(label,41,TextAlignmentOptions.MidlineLeft);
            var plate=row.Find(plateName);Local(plate,55,56,661,126);Visual(plate,"Account");
            Stretch(value.transform);Label(value,email?46.25f:40,TextAlignmentOptions.Midline);value.fontSharedMaterial=ValueMaterial();value.margin=new Vector4(20,0,20,0)*S;
            var old=value.GetComponent<UILanguageLabel>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
        }
        static TMP_Text Text(Transform t){var text=t.GetComponent<TMP_Text>();return text!=null?text:t.gameObject.AddComponent<TextMeshProUGUI>();}
        static CoralLocalizedLabel Localize(Transform t,string key)
        {
            var old=t.GetComponent<UILanguageLabel>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            var label=t.GetComponent<CoralLocalizedLabel>();if(label==null)label=t.gameObject.AddComponent<CoralLocalizedLabel>();
            var serialized=new SerializedObject(label);serialized.FindProperty("key").stringValue=key;serialized.ApplyModifiedPropertiesWithoutUndo();return label;
        }
        static void Caption(Transform parent,TMP_Text label,string slice,string caption,Rect rect)
        {
            // The artwork is a child of the same visual control; no detached click area is introduced.

            var surface=Child(parent,"Reference"+slice);Local(surface,rect.x,rect.y,rect.width,rect.height);Ignore(surface);Visual(surface,slice);
            var group=label.GetComponent<CanvasGroup>();if(group==null)group=label.gameObject.AddComponent<CanvasGroup>();group.interactable=false;group.blocksRaycasts=false;
            // Caption art must not inherit the hidden native-text CanvasGroup.
            if(surface.IsChildOf(label.transform))throw new InvalidOperationException("Caption artwork cannot be inside hidden live text.");
            var component=surface.GetComponent<ApprovedHudCaption>();if(component==null)component=surface.gameObject.AddComponent<ApprovedHudCaption>();
            var data=new SerializedObject(component);data.FindProperty("_label").objectReferenceValue=label;data.FindProperty("_surface").objectReferenceValue=surface.GetComponent<Image>();data.FindProperty("_authoredCaption").stringValue=caption;data.FindProperty("_captionMaterial").objectReferenceValue=Mat(slice);data.FindProperty("_translatedMaterial").objectReferenceValue=Mat("EmptyCaption");data.FindProperty("_captionGroup").objectReferenceValue=group;data.ApplyModifiedPropertiesWithoutUndo();
        }
        static Material ButtonMaterial()
        {
            var path=Root+"SubmitText.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(font.material);AssetDatabase.CreateAsset(m,path);}m.CopyPropertiesFromMaterial(font.material);m.SetColor("_OutlineColor",new Color(.02f,.32f,.13f));m.SetFloat("_OutlineWidth",.025f);m.EnableKeyword("OUTLINE_ON");m.SetColor("_UnderlayColor",new Color(.02f,.30f,.12f));m.SetFloat("_UnderlayOffsetY",-.15f);m.EnableKeyword("UNDERLAY_ON");EditorUtility.SetDirty(m);return m;
        }
        static Material ValueMaterial()
        {
            var path=Root+"ValueText.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(font.material);AssetDatabase.CreateAsset(material,path);}material.CopyPropertiesFromMaterial(font.material);material.SetFloat("_FaceDilate",-.022f);EditorUtility.SetDirty(material);return material;
        }
        static void Label(TMP_Text text,float size,TextAlignmentOptions alignment=TextAlignmentOptions.Center)
        {text.font=font;text.fontSharedMaterial=font.material;text.fontStyle=FontStyles.Normal;text.fontSize=size*S;text.fontSizeMax=size*S;text.fontSizeMin=size*S*.55f;text.enableAutoSizing=true;text.alignment=alignment;text.color=new Color(.03f,.02f,.32f);text.enableVertexGradient=false;text.raycastTarget=false;text.margin=Vector4.zero;}
        static Transform Child(Transform parent,string name){var t=parent.Find(name);if(t==null){t=new GameObject(name,typeof(RectTransform)).transform;t.SetParent(parent,false);t.gameObject.layer=5;}return t;}
        static void Rect(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(x,y)*S;r.sizeDelta=new Vector2(w,h)*S;r.localScale=Vector3.one;}
        static void Local(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y)*S;r.sizeDelta=new Vector2(w,h)*S;r.localScale=Vector3.one;}
        static void Stretch(Transform t){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;}
        static void Height(Transform t,float height){var e=t.GetComponent<LayoutElement>();if(e==null)e=t.gameObject.AddComponent<LayoutElement>();e.ignoreLayout=false;e.minHeight=e.preferredHeight=height*S;e.flexibleHeight=0;}
        static void Row(Transform panel,Transform row,float height){row.SetParent(panel,false);Height(row,height);row.SetAsLastSibling();}
        static void Spacer(Transform panel,string name,float height)=>Row(panel,Child(panel,name),height);
        static void Ignore(Transform t){var e=t.GetComponent<LayoutElement>();if(e==null)e=t.gameObject.AddComponent<LayoutElement>();e.ignoreLayout=true;}
        static void Vertical(Transform t,float spacing){var v=t.GetComponent<VerticalLayoutGroup>();if(v==null)v=t.gameObject.AddComponent<VerticalLayoutGroup>();v.padding=new RectOffset();v.spacing=spacing*S;v.childControlWidth=v.childControlHeight=true;v.childForceExpandWidth=true;v.childForceExpandHeight=false;v.childAlignment=TextAnchor.UpperCenter;}
        static void Fit(Transform t){var f=t.GetComponent<ContentSizeFitter>();if(f==null)f=t.gameObject.AddComponent<ContentSizeFitter>();f.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;f.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");
        static void Visual(Transform t,string name)
        {
            var image=t.GetComponent<Image>();if(image==null)image=t.gameObject.AddComponent<Image>();var loader=t.GetComponent<CoralResourceSprite>();if(loader==null)loader=t.gameObject.AddComponent<CoralResourceSprite>();
            var data=new SerializedObject(loader);data.FindProperty("_resourcePath").stringValue=Resource;data.FindProperty("_spriteName").stringValue=name;data.FindProperty("_image").objectReferenceValue=image;data.ApplyModifiedPropertiesWithoutUndo();image.sprite=null;image.material=Mat(name);image.type=Image.Type.Simple;image.color=Color.white;image.enabled=true;image.raycastTarget=t.GetComponent<Button>()!=null;
        }

        static void Import()
        {
            string path=Root+"ApprovedSource.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(var platform in new[]{"Android","iPhone"}){var settings=importer.GetPlatformTextureSettings(platform);settings.overridden=true;settings.maxTextureSize=2048;settings.format=TextureImporterFormat.ASTC_4x4;importer.SetPlatformTextureSettings(settings);}importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var old=provider.GetSpriteRects();var rects=new List<SpriteRect>();var names=new List<SpriteNameFileIdPair>();
            foreach(var slice in Slices)
            {
                var id=GUID.Generate();foreach(var prior in old)if(prior.name==slice.name){id=prior.spriteID;break;}
                var b=slice.rect;rects.Add(new SpriteRect{name=slice.name,rect=new Rect(b.x,1846-b.y-b.height,b.width,b.height),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=id});names.Add(new SpriteNameFileIdPair(slice.name,id));
                string materialPath=Root+slice.name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(material==null){material=new Material(Shader.Find("BubblePics/UI/ApprovedWithdrawFrame"));AssetDatabase.CreateAsset(material,materialPath);}
                if(slice.name=="Backdrop")
                {material.shader=Shader.Find("BubblePics/UI/ApprovedBackgroundCutout");material.SetVector("_SourceSize",new Vector4(852,1846));material.SetVector("_Cutout",new Vector4(-.5f,.5f,771,1175));material.SetFloat("_Radius",62);}
                else
                {
                    material.SetVector("_SourceRect",new Vector4(b.x/852,(1846-b.y-b.height)/1846,b.width/852,b.height/1846));material.SetVector("_SourceSize",new Vector4(b.width,b.height));material.SetVector("_OuterRect",new Vector4(0,0,b.width,b.height));material.SetFloat("_OuterRadius",slice.radius);material.SetFloat("_MaskMode",slice.radius>0?1:6);material.SetVector("_CapEllipse",Vector4.zero);material.SetFloat("_EraseOn",slice.erase.z>0?1:0);material.SetVector("_EraseRect",slice.erase);material.SetFloat("_EraseRadius",Mathf.Max(0,slice.radius-9));material.SetFloat("_EraseFeather",2);material.SetFloat("_FillSampleU",-1);
                    ColorUtility.TryParseHtmlString(slice.name=="Amount"||slice.name=="Account"?"#E4F2FE":"#F9FDFF",out var color);material.SetColor("_FillTop",color);material.SetColor("_FillMiddle",color);material.SetColor("_FillBottom",color);
                    if(slice.name=="Panel"){ColorUtility.TryParseHtmlString("#FCFEFF",out var top);ColorUtility.TryParseHtmlString("#F7FCFE",out var middle);ColorUtility.TryParseHtmlString("#E5F3FC",out var bottom);material.SetColor("_FillTop",top);material.SetColor("_FillMiddle",middle);material.SetColor("_FillBottom",bottom);}
                    if(slice.name=="Payment"||slice.name=="Amount"||slice.name=="Account"||slice.name=="Submit")material.SetFloat("_FillSampleU",.08f);
                }
                EditorUtility.SetDirty(material);
            }
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);provider.Apply();importer.SaveAndReimport();Sprites.Clear();foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(path))if(obj is Sprite sprite)Sprites[sprite.name]=sprite;
            string emptyPath=Root+"EmptyCaption.mat";var empty=AssetDatabase.LoadAssetAtPath<Material>(emptyPath);if(empty==null){empty=new Material(Shader.Find("UI/Default"));AssetDatabase.CreateAsset(empty,emptyPath);}empty.SetColor("_Color",Color.clear);EditorUtility.SetDirty(empty);
        }
    }
}
