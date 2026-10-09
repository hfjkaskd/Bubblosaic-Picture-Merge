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
    // Offline prefab authoring only. Runtime uses these serialized controls and bindings.
    public static class PendingReferenceAuthoring
    {
        const string PrefabPath="Assets/BizzaWZ/Final/Real/UI/UIWithdrawalPendingPanel/UIWithdrawalPendingPanel.prefab";
        const string Root="Assets/BubblePics/Resources/PendingReference20260928/";
        const string Resource="PendingReference20260928/ApprovedSource";
        const float S=2360f/1846f;
        static TMP_FontAsset font;
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        struct Slice
        {
            public string name;public Rect rect;
            public Slice(string name,float x,float y,float w,float h){this.name=name;rect=new Rect(x,y,w,h);}
        }
        static readonly Slice[] slices={
            new Slice("Backdrop",0,0,852,1846),
            new Slice("Panel",39,313,776,1239),
            new Slice("TitleCaption",139,394,580,69),
            new Slice("Hourglass",299,488,255,249),
            new Slice("BodyCaption",198,743,456,89),
            new Slice("Payment",94,864,667,155),
            new Slice("PayPal",155,900,269,80),
            new Slice("ReviewPlate",91,1041,673,280),
            new Slice("StatusCaption",242,1076,373,49),
            new Slice("ReviewBar",145,1138,565,47),
            new Slice("ProgressFill",147,1141,363,42),
            new Slice("HistoryCaption",229,1209,398,79),
            new Slice("Confirm",83,1349,689,155)
        };

        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring.");
            Directory.CreateDirectory(Root);
            string source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/d9c17ad64882-06-提现待审核.png"));
            if(!File.Exists(Root+"ApprovedSource.png"))File.Copy(source,Root+"ApprovedSource.png");
            AssetDatabase.Refresh();Import();
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/BaggageGo-Bold SDF.asset");
            var go=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var page=go.GetComponent<UIWithdrawalPendingPanel>();var tr=go.transform;
                var amount=page.amountText;var image=page.paymentImage;
                int buttons=go.GetComponentsInChildren<Button>(true).Length;
                foreach(string oldLayer in new[]{"PageBackdrop","OpaqueUnderlay"}){var layer=tr.Find(oldLayer);if(layer!=null)layer.gameObject.SetActive(false);}
                var backdrop=Child(tr,"ReferenceBackdrop");Box(backdrop,new Rect(0,0,852,1846));Visual(backdrop,"Backdrop");backdrop.SetAsFirstSibling();
                tr.Find("PageMask").GetComponent<Image>().color=Color.clear;
                var bg=tr.Find("BG");Box(bg,FindSlice("Panel"));Visual(bg,"Panel");bg.GetComponent<Image>().raycastTarget=true;bg.SetSiblingIndex(2);
                StripLocalization(page.titleText);Box(page.titleText.transform,FindSlice("TitleCaption"));Label(page.titleText,59);Caption(tr,page.titleText,"TitleCaption","Solicitação enviada!");
                var hourglass=Child(tr,"Hourglass");Box(hourglass,FindSlice("Hourglass"));Visual(hourglass,"Hourglass");
                StripLocalization(page.hintText);Box(page.hintText.transform,new Rect(160,741,532,96));Label(page.hintText,39);
                Caption(tr,page.hintText,"BodyCaption","Sua solicitação de saque\nfoi enviada com sucesso.");

                var payment=tr.Find("CloudPaymentPlate");Box(payment,FindSlice("Payment"));Visual(payment,"Payment");payment.SetSiblingIndex(3);
                Box(image.transform,FindSlice("PayPal"));image.preserveAspect=true;image.raycastTarget=false;image.material=Mat("Logo");
                string configPath=Root+"PaymentConfig.asset";
                if(!File.Exists(configPath))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(page.paymentList),configPath);
                var config=AssetDatabase.LoadAssetAtPath<PaymentConfig>(configPath);
                foreach(var entry in config.PaymentDatas)if(entry.payeeAccountType==E_PayeeAccountType.Paypal)entry.paymentIcon=sprites["PayPal"];
                EditorUtility.SetDirty(config);page.paymentList=config;
                Box(amount.transform,new Rect(493,896.5f,232,89));Label(amount,79.3f);amount.fontSharedMaterial=ValueMaterial();amount.transform.localScale=new Vector3(1,1.03f,1);amount.enableWordWrapping=false;

                var plate=Child(tr,"ReviewPlate");Box(plate,FindSlice("ReviewPlate"));Visual(plate,"ReviewPlate");plate.SetSiblingIndex(4);
                var status=Text(Child(tr,"ReviewStatus"));Box(status.transform,FindSlice("StatusCaption"));Label(status,39);Caption(tr,status,"StatusCaption","Aguardando análise");
                var history=Text(Child(tr,"HistoryHint"));Box(history.transform,new Rect(192,1209,471,80));Label(history,33);history.color=new Color(.29f,.46f,.69f);
                Caption(tr,history,"HistoryCaption","Você pode verificar o status\nno seu Histórico.");

                var progress=page.progressRoot.transform;Box(progress,FindSlice("ReviewBar"));
                var track=progress.Find("ProgressBg");Stretch(track);Visual(track,"ReviewBar","ProgressTrack");
                Stretch(page.progressFill.transform);Visual(page.progressFill.transform,"ProgressFill");page.progressFill.type=Image.Type.Sliced;
                page.progressFill.pixelsPerUnitMultiplier=S;page.progressFill.fillAmount=0;page.progressFill.raycastTarget=false;
                page.progressText.gameObject.SetActive(false);
                var result=page.resultRoot.transform;Stretch(result);
                var submitted=Child(result,"SubmittedReview");Box(submitted,FindSlice("ReviewBar"));Visual(submitted,"ReviewBar");
                Box(page.resultIcon.transform,new Rect(330,514,192,192));
                var resultLoader=page.resultIcon.GetComponent<CoralResourceSprite>();if(resultLoader!=null)UnityEngine.Object.DestroyImmediate(resultLoader);
                page.resultIcon.material=null;page.resultIcon.raycastTarget=false;page.resultIcon.preserveAspect=true;

                var button=tr.Find("BtnConfirm");Box(button,FindSlice("Confirm"));Visual(button,"Confirm","ConfirmBlank");
                var buttonGroup=button.GetComponent<CanvasGroup>();if(buttonGroup==null)buttonGroup=button.gameObject.AddComponent<CanvasGroup>();
                StripLocalization(page.confirmText);Stretch(page.confirmText.transform);Label(page.confirmText,68);page.confirmText.color=Color.white;page.confirmText.fontSharedMaterial=ButtonMaterial();
                // Local button art inherits the same disabled/tap CanvasGroup as its Button.
                var buttonCaption=Child(button,"ReferenceConfirmCaption");Stretch(buttonCaption);Visual(buttonCaption,"Confirm");BindCaption(buttonCaption,page.confirmText,"Confirmar",Mat("Confirm"));
                tr.Find("BtnClose").gameObject.SetActive(false);
                var data=new SerializedObject(page);
                data.FindProperty("waitingIcon").objectReferenceValue=hourglass.gameObject;
                data.FindProperty("submittedReview").objectReferenceValue=submitted.gameObject;
                data.FindProperty("reviewStatusText").objectReferenceValue=status;
                data.FindProperty("historyHintText").objectReferenceValue=history;
                data.FindProperty("confirmVisual").objectReferenceValue=buttonGroup;
                data.ApplyModifiedPropertiesWithoutUndo();
                if(page.amountText!=amount||page.paymentImage!=image||buttons!=go.GetComponentsInChildren<Button>(true).Length)throw new InvalidOperationException("Runtime binding or Button count changed.");
                foreach(var b in go.GetComponentsInChildren<Button>(true))if(b.targetGraphic==null||b.onClick.GetPersistentEventCount()!=0)throw new InvalidOperationException("Invalid Button binding.");
                PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
                File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/PendingReference-20260928/authoring.txt")),"Saved pending page prefab and retained "+buttons+" standard Buttons, amount/channel binding and result callbacks. "+DateTime.UtcNow.ToString("O"));
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
            AssetDatabase.SaveAssets();
        }

        static Rect FindSlice(string name){foreach(var slice in slices)if(slice.name==name)return slice.rect;throw new ArgumentException(name);}
        static Transform Child(Transform parent,string name){var t=parent.Find(name);if(t==null){t=new GameObject(name,typeof(RectTransform)).transform;t.SetParent(parent,false);t.gameObject.layer=5;}return t;}
        static void Box(Transform t,Rect box){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(box.center.x-426,923-box.center.y)*S;r.sizeDelta=box.size*S;r.localScale=Vector3.one;}
        static void Stretch(Transform t){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;}
        static TMP_Text Text(Transform t){var label=t.GetComponent<TMP_Text>();return label!=null?label:t.gameObject.AddComponent<TextMeshProUGUI>();}
        static void StripLocalization(TMP_Text label){var old=label.GetComponent<UILanguageLabel>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);var local=label.GetComponent<CoralLocalizedLabel>();if(local!=null)UnityEngine.Object.DestroyImmediate(local);}
        static void Label(TMP_Text label,float size){label.font=font;label.fontSharedMaterial=font.material;label.fontStyle=FontStyles.Normal;label.fontSize=label.fontSizeMax=size*S;label.fontSizeMin=size*S*.52f;label.enableAutoSizing=true;label.alignment=TextAlignmentOptions.Midline;label.color=new Color(.03f,.02f,.32f);label.raycastTarget=false;label.margin=Vector4.zero;label.enableVertexGradient=false;}
        static void Caption(Transform root,TMP_Text label,string slice,string caption){var surface=Child(root,"Reference"+slice);Box(surface,FindSlice(slice));Visual(surface,slice);BindCaption(surface,label,caption,Mat(slice));}
        static void BindCaption(Transform surface,TMP_Text label,string caption,Material material)
        {
            var group=label.GetComponent<CanvasGroup>();if(group==null)group=label.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            var component=surface.GetComponent<ApprovedHudCaption>();if(component==null)component=surface.gameObject.AddComponent<ApprovedHudCaption>();
            var data=new SerializedObject(component);data.FindProperty("_label").objectReferenceValue=label;data.FindProperty("_surface").objectReferenceValue=surface.GetComponent<Image>();data.FindProperty("_authoredCaption").stringValue=caption;data.FindProperty("_captionMaterial").objectReferenceValue=material;data.FindProperty("_translatedMaterial").objectReferenceValue=Mat("EmptyCaption");data.FindProperty("_captionGroup").objectReferenceValue=group;data.ApplyModifiedPropertiesWithoutUndo();
        }
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");
        static void Visual(Transform t,string name,string material=null)
        {
            var image=t.GetComponent<Image>();if(image==null)image=t.gameObject.AddComponent<Image>();var loader=t.GetComponent<CoralResourceSprite>();if(loader==null)loader=t.gameObject.AddComponent<CoralResourceSprite>();
            var data=new SerializedObject(loader);data.FindProperty("_resourcePath").stringValue=Resource;data.FindProperty("_spriteName").stringValue=name;data.FindProperty("_image").objectReferenceValue=image;data.ApplyModifiedPropertiesWithoutUndo();
            image.sprite=null;image.material=Mat(material??name);image.type=Image.Type.Simple;image.color=Color.white;image.raycastTarget=t.GetComponent<Button>()!=null;image.enabled=true;
        }
        static Material NewMaterial(string name,string shader){var path=Root+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find(shader);EditorUtility.SetDirty(m);return m;}
        static Material ValueMaterial(){var m=NewMaterial("ValueText","TextMeshPro/Distance Field");m.CopyPropertiesFromMaterial(font.material);m.SetFloat("_FaceDilate",-.022f);return m;}
        static Material ButtonMaterial(){var m=NewMaterial("ButtonText","TextMeshPro/Distance Field");m.CopyPropertiesFromMaterial(font.material);m.SetColor("_OutlineColor",new Color(.02f,.32f,.13f));m.SetFloat("_OutlineWidth",.025f);m.EnableKeyword("OUTLINE_ON");m.SetColor("_UnderlayColor",new Color(.02f,.30f,.12f));m.SetFloat("_UnderlayOffsetY",-.15f);m.EnableKeyword("UNDERLAY_ON");return m;}
        static void Erase(Material material,float sampleX,float sampleY,params Rect[] slots)
        {
            material.SetFloat("_SampleX",sampleX);material.SetFloat("_SampleY",sampleY);
            for(int i=0;i<6;i++){Rect r=i<slots.Length?slots[i]:default;material.SetVector("_Erase"+i,new Vector4(r.x,r.y,r.width,r.height));}
        }
        static void Import()
        {
            string path=Root+"ApprovedSource.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(var platform in new[]{"Android","iPhone"}){var p=importer.GetPlatformTextureSettings(platform);p.overridden=true;p.maxTextureSize=2048;p.format=TextureImporterFormat.ASTC_4x4;importer.SetPlatformTextureSettings(p);}importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var old=provider.GetSpriteRects();var rects=new List<SpriteRect>();var names=new List<SpriteNameFileIdPair>();
            foreach(var slice in slices)
            {
                var id=GUID.Generate();foreach(var prior in old)if(prior.name==slice.name){id=prior.spriteID;break;}
                var r=slice.rect;rects.Add(new SpriteRect{name=slice.name,rect=new Rect(r.x,1846-r.y-r.height,r.width,r.height),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),spriteID=id,border=slice.name=="ProgressFill"?new Vector4(21,0,21,0):Vector4.zero});names.Add(new SpriteNameFileIdPair(slice.name,id));
                var material=NewMaterial(slice.name,"BubblePics/UI/ApprovedReferencePlate");Erase(material,-1,1334);
            }
            var backdrop=Mat("Backdrop");backdrop.shader=Shader.Find("BubblePics/UI/ApprovedBackgroundCutout");backdrop.SetVector("_SourceSize",new Vector4(852,1846));backdrop.SetVector("_Cutout",new Vector4(1,-9.5f,770,1233));backdrop.SetFloat("_Radius",0);
            Erase(Mat("Panel"),-1,1334,FindSlice("TitleCaption"),FindSlice("Hourglass"),FindSlice("BodyCaption"),FindSlice("Payment"),FindSlice("ReviewPlate"),FindSlice("Confirm"));
            Erase(Mat("Payment"),124,0,new Rect(151,896,278,89),new Rect(510,897,205,88));
            Erase(Mat("ReviewPlate"),-1,1199,FindSlice("StatusCaption"),FindSlice("ReviewBar"),FindSlice("HistoryCaption"));
            var track=NewMaterial("ProgressTrack","BubblePics/UI/ApprovedReferencePlate");Erase(track,650,0,new Rect(144,1138,372,47));
            var blank=NewMaterial("ConfirmBlank","BubblePics/UI/ApprovedReferencePlate");Erase(blank,198,0,new Rect(266,1385,333,85));
            var empty=NewMaterial("EmptyCaption","UI/Default");empty.SetColor("_Color",Color.clear);
            var logo=NewMaterial("Logo","BubblePics/UI/ApprovedWithdrawFrame");logo.SetFloat("_EraseOn",0);logo.SetFloat("_MaskMode",6);
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);provider.Apply();importer.SaveAndReimport();
            sprites.Clear();foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(path))if(obj is Sprite sprite)sprites[sprite.name]=sprite;
        }
    }
}
