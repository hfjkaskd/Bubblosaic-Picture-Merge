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
    // Authoring only. The resulting prefabs use the existing Android data and event paths.
    public static class WithdrawReferenceAuthoring
    {
        const string Resource="WithdrawReference20260928/ApprovedSource";
        const string Source="Assets/BubblePics/Resources/"+Resource+".png";
        const string MaterialRoot="Assets/BubblePics/Resources/WithdrawReference20260928/Materials/";
        const string PrefabRoot="Assets/BizzaWZ/Final/Real/UI/RealWithdrawalPanl/";
        const string SkinRoot="Assets/BizzaWZ/Final/Real/UI/WithdrawReferenceSkin/Prefabs/";
        const float S=2360f/1846f;
        static readonly string Folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/WithdrawReference-20260928"));
        static readonly string Output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/WithdrawReference-20260928"));
        [Serializable] public class Frame { public string name;public float[] rect,erase;public float radius,eraseRadius,eraseFeather=1,fillSampleU=-1,mode=1;public string[] fill; }
        [Serializable] public class Spec { public Frame[] frames; }
        static readonly Dictionary<string,Frame> frames=new Dictionary<string,Frame>();
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        static TMP_FontAsset font;
        static Material greenOutline,whiteOutline;
        static readonly Color Ink=new Color(.025f,.018f,.30f,1);

        public static void ApplyAndBuild(){Apply();BizzaAndroidBuildInspection.BuildApk(Output);}
        public static void Apply()
        {
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output);Directory.CreateDirectory(MaterialRoot);AssetDatabase.Refresh();
            frames.Clear();foreach(var f in JsonUtility.FromJson<Spec>(File.ReadAllText(Path.Combine(Folder,"controls.json"))).frames)frames.Add(f.name,f);
            Import();
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/BaggageGo-Bold SDF.asset");
            greenOutline=FontMaterial("AmountText",new Color(.04f,.35f,.025f,1),.018f);
            whiteOutline=FontMaterial("ButtonText",new Color(.01f,.32f,.07f,1),.025f);
            Edit("WithdrawWay.prefab",ConfigureWay);Edit("WithdrawLevelItem.prefab",ConfigureTier);Edit("RealWithdrawPanel.prefab",ConfigurePage);
            AssetDatabase.SaveAssets();
            ValidateLists();
            File.WriteAllText(Path.Combine(Folder,"applied.txt"),"Withdrawal-only prefabs authored from approved 852x1846 source regions. Data bindings and Button callbacks retained. "+DateTime.UtcNow.ToString("O"));
        }
        static Material FontMaterial(string name,Color color,float width)
        {
            string path=MaterialRoot+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(font.material);AssetDatabase.CreateAsset(m,path);}
            m.CopyPropertiesFromMaterial(font.material);m.SetFloat("_OutlineWidth",width);m.SetColor("_OutlineColor",color);m.EnableKeyword("OUTLINE_ON");m.SetColor("_UnderlayColor",color);m.SetFloat("_UnderlayOffsetY",-.09f);m.SetFloat("_UnderlaySoftness",.08f);m.EnableKeyword("UNDERLAY_ON");EditorUtility.SetDirty(m);return m;
        }
        static void Import()
        {
            AssetDatabase.ImportAsset(Source,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(Source);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=100;
            importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
            importer.maxTextureSize=2048;importer.alphaIsTransparency=true;
            foreach(string platform in new[]{"Android","iPhone"}){var ps=importer.GetPlatformTextureSettings(platform);ps.overridden=true;ps.maxTextureSize=2048;ps.format=TextureImporterFormat.ASTC_4x4;importer.SetPlatformTextureSettings(ps);}
            importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
            var previous=provider.GetSpriteRects();var rects=new List<SpriteRect>();var pairs=new List<SpriteNameFileIdPair>();
            foreach(var f in frames.Values)
            {
                var id=GUID.Generate();foreach(var old in previous)if(old.name==f.name){id=old.spriteID;break;}
                rects.Add(new SpriteRect{name=f.name,rect=new Rect(f.rect[0],1846-f.rect[1]-f.rect[3],f.rect[2],f.rect[3]),pivot=new Vector2(.5f,.5f),alignment=SpriteAlignment.Center,spriteID=id});pairs.Add(new SpriteNameFileIdPair(f.name,id));
                string path=MaterialRoot+f.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(Shader.Find("BubblePics/UI/ApprovedWithdrawFrame"));AssetDatabase.CreateAsset(mat,path);}
                mat.SetVector("_SourceRect",new Vector4(f.rect[0]/852f,(1846-f.rect[1]-f.rect[3])/1846f,f.rect[2]/852f,f.rect[3]/1846f));
                mat.SetVector("_SourceSize",new Vector4(f.rect[2],f.rect[3],0,0));mat.SetVector("_OuterRect",new Vector4(0,0,f.rect[2],f.rect[3]));mat.SetVector("_CapEllipse",Vector4.zero);
                mat.SetFloat("_OuterRadius",f.radius);mat.SetFloat("_MaskMode",f.mode);bool erase=f.erase!=null&&f.erase.Length==4;mat.SetFloat("_EraseOn",erase?1:0);
                if(erase){mat.SetVector("_EraseRect",new Vector4(f.erase[0],f.erase[1],f.erase[2],f.erase[3]));mat.SetFloat("_EraseRadius",f.eraseRadius);mat.SetFloat("_EraseFeather",f.eraseFeather);mat.SetFloat("_FillSampleU",f.fillSampleU);for(int i=0;i<3;i++){ColorUtility.TryParseHtmlString(f.fill[i],out var c);mat.SetColor(new[]{"_FillTop","_FillMiddle","_FillBottom"}[i],c);}}
                EditorUtility.SetDirty(mat);
            }
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(pairs);provider.Apply();importer.SaveAndReimport();
            sprites.Clear();foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(Source))if(obj is Sprite sprite)sprites[sprite.name]=sprite;
        }
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot+name+".mat");
        static void Edit(string filename,Action<GameObject> apply)
        {
            string path=(filename=="RealWithdrawPanel.prefab"?PrefabRoot:SkinRoot)+filename;string backup=Path.Combine(Folder,"Backup",filename);Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!File.Exists(backup))File.Copy(path,backup);
            var root=PrefabUtility.LoadPrefabContents(path);
            try{int count=root.GetComponentsInChildren<Button>(true).Length;apply(root);if(root.GetComponentsInChildren<Button>(true).Length!=count)throw new InvalidOperationException("Button count changed: "+path);foreach(var b in root.GetComponentsInChildren<Button>(true))if(b.targetGraphic==null||b.onClick.GetPersistentEventCount()!=0)throw new InvalidOperationException("Invalid Button: "+b.name);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static RectTransform R(Transform t,float x,float y,float w,float h)
        {
            var r=(RectTransform)t;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;r.anchoredPosition=new Vector2(x*S,y*S);r.sizeDelta=new Vector2(w*S,h*S);return r;
        }
        static RectTransform R(Transform root,string path,float x,float y,float w,float h)=>R(root.Find(path),x,y,w,h);
        static void Stretch(Transform t)
        {
            var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;r.offsetMin=r.offsetMax=Vector2.zero;
        }
        static T Ensure<T>(Transform t) where T:Component {var c=t.GetComponent<T>();return c?c:t.gameObject.AddComponent<T>();}
        static void FixedHeight(Transform t,float height,float width)
        {
            var e=Ensure<LayoutElement>(t);e.ignoreLayout=false;e.minHeight=e.preferredHeight=height*S;e.flexibleHeight=0;e.preferredWidth=width*S;e.flexibleWidth=0;
        }
        static VerticalLayoutGroup Vertical(Transform t,float spacing,bool width=true)
        {
            var v=Ensure<VerticalLayoutGroup>(t);v.enabled=true;v.padding=new RectOffset();v.spacing=spacing*S;v.childAlignment=TextAnchor.UpperCenter;v.childControlWidth=width;v.childControlHeight=true;v.childForceExpandWidth=false;v.childForceExpandHeight=false;
            return v;
        }
        static void Fit(Transform t){var f=Ensure<ContentSizeFitter>(t);f.enabled=true;f.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;f.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        static void Visual(Transform t,string name)
        {
            var image=Ensure<Image>(t);var binder=Ensure<CoralResourceSprite>(t);var data=new SerializedObject(binder);
            data.FindProperty("_resourcePath").stringValue=Resource;data.FindProperty("_spriteName").stringValue=name;data.FindProperty("_image").objectReferenceValue=image;data.ApplyModifiedPropertiesWithoutUndo();
            image.enabled=true;image.sprite=null;image.material=Mat(name);image.type=Image.Type.Simple;image.preserveAspect=false;image.color=Color.white;
        }
        static void Label(TMP_Text t,float size,TextAlignmentOptions alignment=TextAlignmentOptions.Center,bool fit=true)
        {
            t.font=font;t.fontSharedMaterial=font.material;t.fontStyle=FontStyles.Normal;t.fontWeight=FontWeight.Regular;t.fontSize=size*S;t.enableAutoSizing=fit;t.fontSizeMin=size*S*.7f;t.fontSizeMax=size*S;
            t.enableWordWrapping=false;t.overflowMode=TextOverflowModes.Overflow;t.alignment=alignment;t.margin=Vector4.zero;t.characterSpacing=0;t.color=Ink;t.alpha=1;t.raycastTarget=false;t.UpdateMeshPadding();
        }
        static TMP_Text Label(Transform root,string path,float x,float y,float w,float h,float size,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {var t=R(root,path,x,y,w,h).GetComponent<TMP_Text>();Label(t,size,align);return t;}
        static void ConfigureWay(GameObject root)
        {
            R(root.transform,0,0,333,140);Visual(root.transform,"Payment");
            R(root.transform,"Select",0,0,333,140);Visual(root.transform.Find("Select"),"PaymentSelected");root.transform.Find("Select").SetAsFirstSibling();
            R(root.transform,"Frame",-30,0,236,97);var logo=root.transform.Find("Frame").GetComponent<Image>();logo.preserveAspect=true;logo.raycastTarget=false;
            string logoMatPath=MaterialRoot+"PaymentLogo.mat";var logoMat=AssetDatabase.LoadAssetAtPath<Material>(logoMatPath);if(logoMat==null){logoMat=new Material(Shader.Find("BubblePics/UI/ApprovedWithdrawFrame"));AssetDatabase.CreateAsset(logoMat,logoMatPath);}logoMat.SetFloat("_MaskMode",6);logoMat.SetFloat("_EraseOn",0);EditorUtility.SetDirty(logoMat);logo.material=logoMat;
            R(root.transform,"Select/CloudSelectionCheck",124,0,55,56);Visual(root.transform.Find("Select/CloudSelectionCheck"),"GreenCheck");
            root.transform.Find("Select").GetComponent<Image>().raycastTarget=false;root.transform.Find("Select/CloudSelectionCheck").GetComponent<Image>().raycastTarget=false;
        }
        static void ConfigureTier(GameObject root)
        {
            R(root.transform,0,0,380,176);R(root.transform,"bg",0,0,380,176);Visual(root.transform.Find("bg"),"Card");
            foreach(string node in new[]{"Shadow","SelectShadow"}){R(root.transform,node,0,0,380,176);root.transform.Find(node).GetComponent<Image>().enabled=false;}
            R(root.transform,"SelectObj",0,0,388,179);Visual(root.transform.Find("SelectObj"),"CardSelected");
            R(root.transform,"SelectObj/CloudSelectionCheck",145,-32,60,61);Visual(root.transform.Find("SelectObj/CloudSelectionCheck"),"BlueCheck");
            R(root.transform,"Shadow/CloudLock",148,-34,34,41);Visual(root.transform.Find("Shadow/CloudLock"),"Lock");
            R(root.transform,"Level",-50,44,220,45);Label(root.transform,"Level/Level",0,0,220,45,30,TextAlignmentOptions.Left);
            Label(root.transform,"BalanceText",34,-26,160,64,48,TextAlignmentOptions.Left);
            R(root.transform,"Rate",119,46,104,49);Visual(root.transform.Find("Rate"),"Multiplier");Label(root.transform,"Rate/RateText",0,0,93,43,28);
            var rate=root.transform.Find("Rate");var baseBadge=rate.Find("BaseRateBadge");if(baseBadge==null){baseBadge=new GameObject("BaseRateBadge",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).transform;baseBadge.SetParent(rate,false);}Stretch(baseBadge);Visual(baseBadge,"BaseMultiplier");baseBadge.SetAsFirstSibling();baseBadge.gameObject.SetActive(false);
            var tierData=new SerializedObject(root.GetComponent<WithdrawLevelItem>());tierData.FindProperty("boostedRateBadge").objectReferenceValue=rate.GetComponent<Image>();tierData.FindProperty("baseRateBadge").objectReferenceValue=baseBadge.gameObject;tierData.ApplyModifiedPropertiesWithoutUndo();
            R(root.transform,"CashIllustration",-116,-27,98,84);Visual(root.transform.Find("CashIllustration"),"Cash");
            root.GetComponent<WithdrawLevelItem>().images=Array.Empty<Image>();
            foreach(var image in root.GetComponentsInChildren<Image>(true))image.raycastTarget=image.transform.name=="bg";
        }
        static void ConfigurePage(GameObject root)
        {
            var tr=root.transform;var content=tr.Find("Content");Stretch(content);((RectTransform)content).offsetMax=new Vector2(0,-160*S);
            var body=tr.Find("Content/Scroll View/Viewport/Content");R(body,0,0,774,0);var br=(RectTransform)body;br.anchorMin=br.anchorMax=new Vector2(.5f,1);br.pivot=new Vector2(.5f,1);br.anchoredPosition=Vector2.zero;
            var bodyLayout=Vertical(body,19);bodyLayout.padding.top=Mathf.RoundToInt(8*S);bodyLayout.padding.bottom=Mathf.RoundToInt(22*S);Fit(body);
            var info=body.Find("WithdrawInfo");R(info,0,0,773,877);var vl=Vertical(info,0);vl.padding=new RectOffset(Mathf.RoundToInt(46*S),Mathf.RoundToInt(46*S),Mathf.RoundToInt(50*S),Mathf.RoundToInt(38*S));Fit(info);
            var infoElement=Ensure<LayoutElement>(info);infoElement.preferredWidth=773*S;infoElement.minHeight=877*S;infoElement.preferredHeight=-1;infoElement.flexibleHeight=0;
            Stretch(info.Find("bg"));Visual(info.Find("bg"),"Panel");info.Find("bg").GetComponent<Image>().raycastTarget=false;Ensure<LayoutElement>(info.Find("bg")).ignoreLayout=true;
            var coin=info.Find("CoinInfo");FixedHeight(coin,264,681);
            R(coin,"CurrentCount",0,105,430,55);
            Label(coin,"CurrentCount/MyBalance",-98,0,220,55,34,TextAlignmentOptions.Right);
            R(coin,"CurrentCount/Image",45,0,45,46);
            Label(coin,"CurrentCount/Balance_Text",118,0,90,55,34,TextAlignmentOptions.Left);
            R(coin,"RateCount",239,53,200,43);R(coin,"RateCount/Image",-68,0,40,42);Label(coin,"RateCount/RateNum",28,0,150,43,25,TextAlignmentOptions.Left);
            Label(coin,"PassLevelHint",-184,53,310,43,25,TextAlignmentOptions.Left);
            R(coin,"RealCurrent",0,-49,683,157);Visual(coin.Find("RealCurrent"),"Amount");var amount=Label(coin,"RealCurrent/Text (TMP)",0,0,636,126,88);amount.fontSharedMaterial=greenOutline;amount.color=Color.white;amount.enableVertexGradient=true;amount.colorGradient=new VertexGradient(new Color(.90f,1,.55f),new Color(.90f,1,.55f),new Color(.48f,.97f,.12f),new Color(.48f,.97f,.12f));amount.UpdateMeshPadding();
            var amountData=new SerializedObject(amount);amountData.FindProperty("m_fontColor32").colorValue=Color.white;amountData.ApplyModifiedPropertiesWithoutUndo();
            var mode=info.Find("WithdrawMode");R(mode,0,0,681,234);var modeLayout=Vertical(mode,0);modeLayout.padding.bottom=Mathf.RoundToInt(11*S);Fit(mode);
            var title=mode.Find("WithdrawWayTitle");FixedHeight(title,83,681);Label(title.GetComponent<TMP_Text>(),34,TextAlignmentOptions.MidlineLeft);
            var ways=mode.Find("Content");R(ways,0,0,681,140);var grid=ways.GetComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;grid.cellSize=new Vector2(333,140)*S;grid.spacing=new Vector2(15,12)*S;grid.padding=new RectOffset();grid.childAlignment=TextAnchor.UpperLeft;Fit(ways);Ensure<LayoutElement>(ways).minHeight=140*S;
            var hint=info.Find("BlanaceHint")??info.Find("ReferenceHintBackground/BlanaceHint");var hintLabel=hint.GetComponent<TMP_Text>();Label(hintLabel,29,TextAlignmentOptions.MidlineLeft);hintLabel.margin=new Vector4(23*S,0,14*S,0);hintLabel.enableWordWrapping=true;hintLabel.fontSizeMin=18*S;hintLabel.color=new Color(.26f,.27f,.65f,1);
            var hintBack=info.Find("ReferenceHintBackground");if(hintBack==null){int index=hint.GetSiblingIndex();hintBack=new GameObject("ReferenceHintBackground",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(LayoutElement)).transform;hintBack.SetParent(info,false);hintBack.SetSiblingIndex(index);hint.SetParent(hintBack,false);}R(hintBack,0,0,681,72);Visual(hintBack,"Hint");FixedHeight(hintBack,72,681);hintBack.GetComponent<Image>().raycastTarget=false;Stretch(hint);
            var withdraw=info.Find("WithdrawBtn");FixedHeight(withdraw,168,681);R(withdraw,"Btn",0,-7.5f,617,137);Visual(withdraw.Find("Btn"),"Withdraw");var buttonLabel=Label(withdraw,"Btn/Text (TMP)",0,0,560,116,69);buttonLabel.color=Color.white;buttonLabel.fontSharedMaterial=whiteOutline;buttonLabel.UpdateMeshPadding();
            var caption=Ensure<ApprovedHudCaption>(withdraw.Find("Btn"));var captionData=new SerializedObject(caption);captionData.FindProperty("_label").objectReferenceValue=buttonLabel;captionData.FindProperty("_surface").objectReferenceValue=withdraw.Find("Btn").GetComponent<Image>();captionData.FindProperty("_authoredCaption").stringValue="Retirar";captionData.FindProperty("_captionMaterial").objectReferenceValue=Mat("WithdrawCaption");captionData.FindProperty("_translatedMaterial").objectReferenceValue=Mat("Withdraw");captionData.ApplyModifiedPropertiesWithoutUndo();
            var footer=info.Find("MoreWithdraw_Hint");FixedHeight(footer,51,681);Label(footer.GetComponent<TMP_Text>(),28);footer.GetComponent<TMP_Text>().color=new Color(.28f,.35f,.71f,1);
            var levels=body.Find("WithdrawLevel");R(levels,0,0,774,752);Vertical(levels,0);Fit(levels);var le=Ensure<LayoutElement>(levels);le.preferredWidth=774*S;le.minHeight=752*S;le.preferredHeight=-1;le.flexibleHeight=0;
            var tiers=levels.Find("Content");R(tiers,0,0,774,558);var tg=tiers.GetComponent<GridLayoutGroup>();tg.constraint=GridLayoutGroup.Constraint.FixedColumnCount;tg.constraintCount=2;tg.cellSize=new Vector2(380,176)*S;tg.spacing=new Vector2(14,16)*S;tg.padding=new RectOffset();tg.childAlignment=TextAnchor.UpperLeft;Fit(tiers);Ensure<LayoutElement>(tiers).minHeight=558*S;
            var progress=levels.Find("ProgressInfo");R(progress,0,0,774,194);FixedHeight(progress,194,774);
            R(progress,"Progress",0,24,740,44);Label(progress,"Progress/progressValue",0,0,700,44,25);Label(progress,"progressHint",0,-29,730,55,25);
            Label(progress,"CompleteHint",0,-7,740,66,25);
            var header=R(tr,"Title",0,-80,852,160);header.anchorMin=header.anchorMax=new Vector2(.5f,1);
            var pageTitle=Label(header,"Title",0,-16,460,91,72);pageTitle.color=Color.white;pageTitle.fontSharedMaterial=FontMaterial("PageTitle",new Color(.10f,.18f,.56f,1),.025f);pageTitle.UpdateMeshPadding();
            HeaderButton(header,"CloseBtn",-327,-20,"Back");HeaderButton(header,"HistoryBtn",233,-20,"History");HeaderButton(header,"FQA",330,-20,"Help");
            WithdrawalServiceButtonAuthoring.Configure(root);
            var page=root.GetComponent<RealWithdrawPanel>();page.normalSprite=page.canWithdrawSprite=sprites["Withdraw"];
            page.withdrawWayItem=AssetDatabase.LoadAssetAtPath<GameObject>(SkinRoot+"WithdrawWay.prefab").GetComponent<WithdrawWay>();
            page.withdrawLevelItem=AssetDatabase.LoadAssetAtPath<GameObject>(SkinRoot+"WithdrawLevelItem.prefab").GetComponent<WithdrawLevelItem>();
            page.withdrawValueKeyColor="#149837FF";page.withdrawChannelKeyColor="#0589B5FF";
            var pageData=new SerializedObject(page);pageData.FindProperty("progressUsesPrefabLayout").boolValue=true;pageData.ApplyModifiedPropertiesWithoutUndo();
            WithdrawLevelProgressAuthoring.Configure(page);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)body);
            File.WriteAllText(Path.Combine(Folder,"geometry.txt"),"Main panel "+((RectTransform)info).rect+"; payment grid "+((RectTransform)ways).rect+"; tiers "+((RectTransform)tiers).rect);
        }
        static void HeaderButton(Transform title,string path,float x,float y,string sprite)
        {var button=R(title,path,x,y,92,94);R(button,"Image",0,0,92,94);Visual(button.Find("Image"),sprite);}

        static void ValidateLists()
        {
            var page=PrefabUtility.LoadPrefabContents(PrefabRoot+"RealWithdrawPanel.prefab");
            var results=new List<string>();
            try
            {
                var model=page.GetComponent<RealWithdrawPanel>();
                foreach(int count in new[]{1,2,4})
                {
                    foreach(Transform child in model.withdrawWayRoot)child.gameObject.SetActive(false);
                    var copies=new List<GameObject>();for(int i=0;i<count;i++){var item=(GameObject)PrefabUtility.InstantiatePrefab(model.withdrawWayItem.gameObject,model.withdrawWayRoot);copies.Add(item);}
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)model.withdrawWayRoot.parent.parent.parent);
                    var a=(RectTransform)copies[0].transform;if(count>1){var b=(RectTransform)copies[1].transform;if(Mathf.Abs(a.anchoredPosition.y-b.anchoredPosition.y)>1||b.anchoredPosition.x<=a.anchoredPosition.x)throw new InvalidOperationException("Payment columns failed");}
                    results.Add("PASS: "+count+" payment entries, two visible columns; standard Buttons and configured logo binding.");foreach(var item in copies)UnityEngine.Object.DestroyImmediate(item);
                }
                foreach(int count in new[]{4,6,10})
                {
                    foreach(Transform child in model.withdrawLevelRoot)child.gameObject.SetActive(false);
                    var copies=new List<GameObject>();for(int i=0;i<count;i++)copies.Add((GameObject)PrefabUtility.InstantiatePrefab(model.withdrawLevelItem.gameObject,model.withdrawLevelRoot));
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)model.withdrawLevelRoot.parent.parent);
                    var first=(RectTransform)copies[0].transform;var last=(RectTransform)copies[count-1].transform;if(last.anchoredPosition.y>=first.anchoredPosition.y)throw new InvalidOperationException("Tier rows failed");
                    results.Add("PASS: "+count+" tier entries wrap to two columns and expand the ScrollRect content.");foreach(var item in copies)UnityEngine.Object.DestroyImmediate(item);
                }
                File.WriteAllLines(Path.Combine(Folder,"layout-validation.txt"),results);
            }
            finally{PrefabUtility.UnloadPrefabContents(page);}
        }
    }
}
