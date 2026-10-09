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
    // Offline authoring only: all static objects and geometry are saved into the page prefab.
    public static class FAQReferenceAuthoring
    {
        const string PrefabPath="Assets/BizzaWZ/Final/Real/UI/FAQPanel/FAQPanel.prefab";
        const string Root="Assets/BubblePics/Resources/FAQReference20260928/";
        const string Resource="FAQReference20260928/ApprovedSource";
        const float S=2360f/1846f;
        static TMP_FontAsset headingFont,bodyFont;
        struct Slice
        {
            public string name;public Rect rect;public Vector4 border;
            public Slice(string n,float x,float y,float w,float h,Vector4 b=default){name=n;rect=new Rect(x,y,w,h);border=b;}
        }
        static readonly Slice[] slices={
            new Slice("Backdrop",0,0,852,1846),new Slice("Panel",37,213,780,1533),
            new Slice("Title",341,102,175,85),new Slice("Close",701,91,103,104),
            new Slice("Card",77,303,698,247,new Vector4(45,45,45,45)),new Slice("QuestionIcon",104,342,119,124),
            new Slice("Question1",249,347,496,61),new Slice("Answer1",251,418,502,94),
            new Slice("Question2",249,674,496,61),new Slice("Answer2",251,743,502,94),
            new Slice("Question3",249,999,496,61),new Slice("Answer3",251,1069,502,94),
            new Slice("Question4",249,1325,496,61),new Slice("Answer4",251,1395,502,94),
            new Slice("Divider",92,587,670,9),new Slice("Thumb",783,292,19,198,new Vector4(8,8,8,8))
        };
        public static readonly string[] Questions={"How do I withdraw?","Where is my request?","Why did a request fail?","How do tiers work?"};
        public static readonly string[] Answers={"Choose a payout method and\nan eligible amount.","Open History to check its\nreview status.","Check your account details\nand the reason in History.","Play more levels to unlock\nthe next tier."};

        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring.");
            Directory.CreateDirectory(Root);
            string source=Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/82abfccdde27-08-提现常见问题.png"));
            if(!File.Exists(Root+"ApprovedSource.png"))File.Copy(source,Root+"ApprovedSource.png");
            AssetDatabase.Refresh();Import();
            headingFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/BaggageGo-Bold SDF.asset");
            var originalBodyFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            bodyFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"BodyFont.asset");
            if(bodyFont==null){bodyFont=UnityEngine.Object.Instantiate(originalBodyFont);bodyFont.name="FAQBody";AssetDatabase.CreateAsset(bodyFont,Root+"BodyFont.asset");}
            // Reuse existing atlases; the page-specific fallback list includes the small release-language supplement.
            bodyFont.fallbackFontAssetTable=new List<TMP_FontAsset>(originalBodyFont.fallbackFontAssetTable){headingFont,AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/RuntimeFonts/TMP/ReleaseLanguageSupplement SDF.asset")};EditorUtility.SetDirty(bodyFont);
            var go=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var page=go.GetComponent<FAQPanel>();var tr=go.transform;int buttons=go.GetComponentsInChildren<Button>(true).Length;
                var nested=PrefabUtility.GetNearestPrefabInstanceRoot(tr.Find("BG (2)").gameObject);if(nested!=null)PrefabUtility.UnpackPrefabInstance(nested,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                foreach(string name in new[]{"PageBackdrop","OpaqueUnderlay"}){var old=tr.Find(name);if(old!=null)old.gameObject.SetActive(false);}
                var backdrop=Child(tr,"ReferenceBackdrop");Box(backdrop,SliceRect("Backdrop"));Visual(backdrop,"Backdrop");backdrop.SetAsFirstSibling();
                tr.Find("PageMask (1)").GetComponent<Image>().color=Color.clear;
                var bg=tr.Find("BG (2)");Box(bg,SliceRect("Panel"));Visual(bg,"Panel");bg.GetComponent<Image>().raycastTarget=true;
                var decoration=bg.Find("Image (2)");if(decoration!=null)decoration.gameObject.SetActive(false);
                var title=tr.Find("FAQTitle");if(title==null)title=bg.Find("FAQTitle");if(title==null)title=bg.Find("Text (TMP)");title.SetParent(tr,false);title.name="FAQTitle";Box(title,new Rect(178.5f,102,500,85));Label(Text(title),80,true,TextAlignmentOptions.Center);Text(title).enableWordWrapping=false;Text(title).color=Color.white;Localized(title,"faq_title");Caption(tr,Text(title),"Title","FAQ",SliceRect("Title"),true);
                var close=tr.Find("CloseBtn");if(close==null)close=bg.Find("CloseBtn");close.SetParent(tr,false);Box(close,SliceRect("Close"));Visual(close,"Close");
                var holder=tr.Find("Content (1)");Box(holder,new Rect(65,282,714,1410));
                var scroll=holder.Find("Scroll View").GetComponent<ScrollRect>();Stretch(scroll.transform);scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.inertia=true;scroll.scrollSensitivity=90;
                var viewport=scroll.viewport;Stretch(viewport);var mask=viewport.GetComponent<Mask>();if(mask!=null)UnityEngine.Object.DestroyImmediate(mask);Ensure<RectMask2D>(viewport);viewport.GetComponent<Image>().color=Color.clear;viewport.GetComponent<Image>().raycastTarget=true;
                var content=scroll.content;content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;content.localScale=Vector3.one;
                Vertical(content,0);var flow=content.GetComponent<VerticalLayoutGroup>();flow.padding=new RectOffset(Mathf.RoundToInt(13*S),Mathf.RoundToInt(5*S),Mathf.RoundToInt(22*S),Mathf.RoundToInt(32*S));Fit(content);
                for(int i=0;i<4;i++)
                {
                    var row=Child(content,"QuickQuestion"+(i+1));row.SetAsLastSibling();Height(row,244);
                    var plate=Child(row,"Plate");Stretch(plate);Visual(plate,"Card");plate.GetComponent<Image>().type=Image.Type.Sliced;plate.GetComponent<Image>().pixelsPerUnitMultiplier=1/S;plate.GetComponent<Image>().raycastTarget=true;plate.SetAsFirstSibling();
                    var icon=Child(row,"QuestionIcon");Local(icon,26,38,119,124);Visual(icon,"QuestionIcon");
                    var question=Text(Child(row,"Question"));Local(question.transform,175,44,492,66);Label(question,45,true,TextAlignmentOptions.MidlineLeft);Localized(question.transform,"faq_quick_question_"+(i+1));
                    var answer=Text(Child(row,"Answer"));Local(answer.transform,175,113,494,99);Label(answer,37,false,TextAlignmentOptions.TopLeft);Localized(answer.transform,"faq_quick_answer_"+(i+1));
                    var qRect=SliceRect("Question"+(i+1));var aRect=SliceRect("Answer"+(i+1));
                    float originalTop=i==0?304:i==1?629:i==2?955:1282;
                    Caption(row,question,"Question"+(i+1),Questions[i],new Rect(qRect.x-78,qRect.y-originalTop,qRect.width,qRect.height),false);
                    Caption(row,answer,"Answer"+(i+1),Answers[i],new Rect(aRect.x-78,aRect.y-originalTop,aRect.width,aRect.height),false);
                    if(i<3){var separator=Child(content,"Separator"+(i+1));separator.SetAsLastSibling();Height(separator,82);var line=Child(separator,"Line");Local(line,14,39,670,9);Visual(line,"Divider");}
                }
                var space=Child(content,"BeforeDetails");space.SetAsLastSibling();Height(space,220);
                var detailTitle=Text(Child(content,"DetailsTitle"));detailTitle.transform.SetAsLastSibling();Height(detailTitle.transform,80);Label(detailTitle,43,true,TextAlignmentOptions.MidlineLeft);Localized(detailTitle.transform,"faq_more_details");detailTitle.margin=new Vector4(30,0,25,0)*S;
                var original=page.GetComponentsInChildren<FAQDesc>(true);
                foreach(var desc in original)
                {
                    var section=Child(content,"Detail-"+desc.key);section.SetAsLastSibling();Vertical(section,0);section.GetComponent<VerticalLayoutGroup>().padding=new RectOffset(Mathf.RoundToInt(32*S),Mathf.RoundToInt(32*S),Mathf.RoundToInt(30*S),Mathf.RoundToInt(30*S));Fit(section);
                    var plate=Child(section,"Plate");Stretch(plate);Visual(plate,"Card");plate.GetComponent<Image>().type=Image.Type.Sliced;plate.GetComponent<Image>().pixelsPerUnitMultiplier=1/S;plate.GetComponent<Image>().raycastTarget=true;Ensure<LayoutElement>(plate).ignoreLayout=true;plate.SetAsFirstSibling();
                    desc.transform.SetParent(section,false);desc.transform.SetAsLastSibling();desc.transform.localScale=Vector3.one;
                    foreach(Transform decorationChild in desc.transform)decorationChild.gameObject.SetActive(false);
                    var oldLayout=desc.GetComponent<LayoutElement>();if(oldLayout!=null)UnityEngine.Object.DestroyImmediate(oldLayout);
                    var fitter=desc.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;
                    Label(desc.tMP_Text,37,false,TextAlignmentOptions.TopLeft);desc.tMP_Text.enableAutoSizing=false;desc.tMP_Text.enableWordWrapping=true;desc.tMP_Text.margin=Vector4.zero;desc.tMP_Text.lineSpacing=8;
                    var gap=Child(content,"After-"+desc.key);gap.SetAsLastSibling();Height(gap,32);
                }
                page.titleColor.replaceValue="#080454";page.contentColor.replaceValue="#162652";page.highlightColor.replaceValue="#006ED6";
                var bar=Child(tr,"FAQScrollbar");Box(bar,new Rect(783,292,19,1372));var scrollbar=Ensure<Scrollbar>(bar);scrollbar.direction=Scrollbar.Direction.BottomToTop;scrollbar.transition=Selectable.Transition.None;
                var area=Child(bar,"SlidingArea");Stretch(area);var thumb=Child(area,"Handle");Stretch(thumb);Visual(thumb,"Thumb");thumb.GetComponent<Image>().type=Image.Type.Sliced;thumb.GetComponent<Image>().pixelsPerUnitMultiplier=1/S;thumb.GetComponent<Image>().raycastTarget=true;scrollbar.handleRect=(RectTransform)thumb;scrollbar.targetGraphic=thumb.GetComponent<Image>();scroll.verticalScrollbar=scrollbar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
                var data=new SerializedObject(page);data.FindProperty("scrollView").objectReferenceValue=scroll;data.ApplyModifiedPropertiesWithoutUndo();close.SetAsLastSibling();
                if(original.Length!=7)throw new InvalidOperationException("Expected all seven original FAQ descriptions.");
                if(buttons!=go.GetComponentsInChildren<Button>(true).Length)throw new InvalidOperationException("Standard Button count changed.");
                foreach(var button in go.GetComponentsInChildren<Button>(true))if(button.targetGraphic==null||button.onClick.GetPersistentEventCount()!=0)throw new InvalidOperationException("Invalid standard Button binding.");
                PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(go);}
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"../../ArtChanges/FAQReference-20260928/authoring.txt")),"Saved FAQ prefab; retained seven original descriptions, standard close Button and native scrolling. "+DateTime.UtcNow.ToString("O"));
        }

        static void Caption(Transform parent,TMP_Text label,string slice,string value,Rect rect,bool screen)
        {
            var surface=Child(parent,"Reference"+slice);if(screen)Box(surface,rect);else Local(surface,rect.x,rect.y,rect.width,rect.height);Visual(surface,slice);
            var group=Ensure<CanvasGroup>(label);group.blocksRaycasts=false;group.interactable=false;
            var data=new SerializedObject(Ensure<ApprovedHudCaption>(surface));data.FindProperty("_label").objectReferenceValue=label;data.FindProperty("_surface").objectReferenceValue=surface.GetComponent<Image>();data.FindProperty("_authoredCaption").stringValue=value;data.FindProperty("_captionMaterial").objectReferenceValue=Mat(slice);data.FindProperty("_translatedMaterial").objectReferenceValue=Mat("EmptyCaption");data.FindProperty("_captionGroup").objectReferenceValue=group;data.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Localized(Transform tr,string key){var old=tr.GetComponent<UILanguageLabel>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);var data=new SerializedObject(Ensure<CoralLocalizedLabel>(tr));data.FindProperty("key").stringValue=key;data.ApplyModifiedPropertiesWithoutUndo();}
        static void Label(TMP_Text label,float size,bool bold,TextAlignmentOptions alignment){Ensure<PreserveAuthoredFont>(label);label.font=bold?headingFont:bodyFont;label.fontSharedMaterial=label.font.material;label.fontStyle=FontStyles.Normal;label.fontSize=label.fontSizeMax=size*S;label.fontSizeMin=size*S*.65f;label.enableAutoSizing=true;label.alignment=alignment;label.color=new Color(.03f,.02f,.32f);label.enableVertexGradient=false;label.enableWordWrapping=true;label.raycastTarget=false;label.margin=Vector4.zero;}
        static TMP_Text Text(Transform t){var text=t.GetComponent<TMP_Text>();if(text==null)text=t.gameObject.AddComponent<TextMeshProUGUI>();return text;}
        static T Ensure<T>(Component c) where T:Component {var value=c.GetComponent<T>();if(value==null)value=c.gameObject.AddComponent<T>();return value;}
        static Transform Child(Transform p,string n){var t=p.Find(n);if(t==null){t=new GameObject(n,typeof(RectTransform)).transform;t.SetParent(p,false);t.gameObject.layer=5;}return t;}
        static void Box(Transform t,Rect box){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(box.center.x-426,923-box.center.y)*S;r.sizeDelta=box.size*S;r.localScale=Vector3.one;}
        static void Local(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y)*S;r.sizeDelta=new Vector2(w,h)*S;r.localScale=Vector3.one;}
        static void Stretch(Transform t){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.pivot=new Vector2(.5f,.5f);r.localScale=Vector3.one;}
        static void Height(Transform t,float h){var e=Ensure<LayoutElement>(t);e.ignoreLayout=false;e.minHeight=e.preferredHeight=h*S;e.flexibleHeight=0;}
        static void Vertical(Transform t,float spacing){var v=Ensure<VerticalLayoutGroup>(t);v.enabled=true;v.spacing=spacing*S;v.padding=new RectOffset();v.childControlWidth=v.childControlHeight=true;v.childForceExpandWidth=true;v.childForceExpandHeight=false;v.childAlignment=TextAnchor.UpperLeft;}
        static void Fit(Transform t){var f=Ensure<ContentSizeFitter>(t);f.enabled=true;f.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;f.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        static Rect SliceRect(string name){foreach(var s in slices)if(s.name==name)return s.rect;throw new ArgumentException(name);}
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Root+name+".mat");
        static Material Material(string name,string shader){var path=Root+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find(shader);EditorUtility.SetDirty(m);return m;}
        static void Visual(Transform t,string slice)
        {
            var im=Ensure<Image>(t);var data=new SerializedObject(Ensure<CoralResourceSprite>(t));data.FindProperty("_resourcePath").stringValue=Resource;data.FindProperty("_spriteName").stringValue=slice;data.FindProperty("_image").objectReferenceValue=im;data.ApplyModifiedPropertiesWithoutUndo();im.sprite=null;im.material=Mat(slice);im.color=Color.white;im.type=Image.Type.Simple;im.enabled=true;im.raycastTarget=t.GetComponent<Button>()!=null;
        }
        static void Erase(Material m,params Rect[] rects){m.SetFloat("_SampleX",-1);m.SetFloat("_SampleY",-1);m.SetFloat("_EraseRadius",0);m.SetFloat("_EraseFeather",.5f);m.SetVector("_VisibleRect",Vector4.zero);for(int i=0;i<6;i++){var r=i<rects.Length?rects[i]:default;m.SetVector("_Erase"+i,new Vector4(r.x,r.y,r.width,r.height));}}
        static void Import()
        {
            string path=Root+"ApprovedSource.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            foreach(string platform in new[]{"Android","iPhone"}){var setting=importer.GetPlatformTextureSettings(platform);setting.overridden=true;setting.maxTextureSize=2048;setting.format=TextureImporterFormat.ASTC_4x4;importer.SetPlatformTextureSettings(setting);}importer.SaveAndReimport();
            var factory=new SpriteDataProviderFactories();factory.Init();var provider=factory.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();var prior=provider.GetSpriteRects();var rects=new List<SpriteRect>();var names=new List<SpriteNameFileIdPair>();
            foreach(var s in slices){var id=GUID.Generate();foreach(var old in prior)if(old.name==s.name){id=old.spriteID;break;}rects.Add(new SpriteRect{name=s.name,rect=new Rect(s.rect.x,1846-s.rect.y-s.rect.height,s.rect.width,s.rect.height),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),border=s.border,spriteID=id});names.Add(new SpriteNameFileIdPair(s.name,id));Erase(Material(s.name,"BubblePics/UI/HistoryReferencePlate"));}
            Erase(Mat("Backdrop"),new Rect(330,97,194,99),new Rect(694,84,117,117));Mat("Backdrop").SetFloat("_EraseFeather",5);
            Erase(Mat("Panel"),new Rect(74,298,704,1233),new Rect(91,582,670,17),new Rect(91,909,670,17),new Rect(91,1236,670,17),new Rect(783,292,19,198));Mat("Panel").SetFloat("_SampleY",1560);Mat("Panel").SetFloat("_EraseRadius",15);
            Erase(Mat("Card"),new Rect(98,333,658,184));Mat("Card").SetVector("_VisibleRect",new Vector4(77,303,698,247));Mat("Card").SetFloat("_Radius",40);Mat("Card").SetFloat("_Feather",2);
            var blank=Material("EmptyCaption","UI/Default");blank.SetColor("_Color",Color.clear);
            provider.SetSpriteRects(rects.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(names);provider.Apply();importer.SaveAndReimport();
        }
    }
}
