using System;
using System.Collections.Generic;
using System.IO;
using AdvancedInputFieldPlugin;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
using Presentation=BubblePics.WithdrawalFormPresentation;

namespace BubblePics.EditorTools
{
    public static partial class FormPresentationAuthoring
    {
        const string FormPath="Assets/BizzaWZ/Final/Real/UI/WithdrawFillPanel/WithdrawFillPanel.prefab";
        static bool Pooled(Component target)
        {
            if(target==null)return false;
            if(target.GetComponentInParent<WithdrawWay>()!=null||target.GetComponent<TMP_SubMeshUI>()!=null)return true;
            for(var ancestor=target.transform.parent;ancestor!=null;ancestor=ancestor.parent)if(ancestor.name=="SelectPlatform")return true;
            return false;
        }
        static void ExcludePooled(Presentation.Preset p)
        {
            p.rects=Array.FindAll(p.rects,x=>x.target!=null&&!Pooled(x.target));p.images=Array.FindAll(p.images,x=>x.target!=null&&!Pooled(x.target));p.texts=Array.FindAll(p.texts,x=>x.target!=null&&!Pooled(x.target));p.behaviours=Array.FindAll(p.behaviours,x=>x.target!=null&&!Pooled(x.target));p.labels=Array.FindAll(p.labels,x=>x.target!=null&&!Pooled(x.target));p.groups=Array.FindAll(p.groups,x=>x.target!=null&&!Pooled(x.target));
        }
        static Presentation.Preset Capture(GameObject root,string name,string[] channels,WithdrawWay method,bool fixedLayout,GameObject[] visibility)
        {
            var r=new List<Presentation.RectState>();var images=new List<Presentation.ImageState>();var text=new List<Presentation.TextState>();var behaviours=new List<Presentation.EnabledState>();var labels=new List<Presentation.LabelState>();var objects=new List<Presentation.ActiveState>();var groups=new List<Presentation.GroupState>();
            foreach(var t in root.GetComponentsInChildren<RectTransform>(true))r.Add(new Presentation.RectState{target=t,authoredPath=AnimationUtility.CalculateTransformPath(t,root.transform),anchorMin=t.anchorMin,anchorMax=t.anchorMax,pivot=t.pivot,position=t.anchoredPosition,size=t.sizeDelta,scale=t.localScale});
            foreach(var i in root.GetComponentsInChildren<Image>(true))
            {
                var binder=i.GetComponent<CoralResourceSprite>();var state=new Presentation.ImageState{target=i,resource=binder,sprite=i.sprite,material=i.material,color=i.color,type=i.type,enabled=i.enabled,raycast=i.raycastTarget,preserveAspect=i.preserveAspect,pixelsPerUnit=i.pixelsPerUnitMultiplier};
                if(binder!=null){var data=new SerializedObject(binder);state.path=data.FindProperty("_resourcePath").stringValue;state.spriteName=data.FindProperty("_spriteName").stringValue;state.sprite=null;}images.Add(state);
            }
            foreach(var t in root.GetComponentsInChildren<TMP_Text>(true))text.Add(new Presentation.TextState{target=t,font=t.font,material=t.fontSharedMaterial,size=t.fontSize,min=t.fontSizeMin,max=t.fontSizeMax,autoSize=t.enableAutoSizing,wrap=t.enableWordWrapping,gradient=t.enableVertexGradient,color=t.color,gradientColors=t.colorGradient,alignment=t.alignment,margin=t.margin});
            foreach(var b in root.GetComponentsInChildren<Behaviour>(true))if(b is LayoutGroup||b is ContentSizeFitter||b is ApprovedHudCaption||b is UILanguageLabel||b is CoralLocalizedLabel)behaviours.Add(new Presentation.EnabledState{target=b,enabled=b.enabled});
            foreach(var label in root.GetComponentsInChildren<CoralLocalizedLabel>(true)){var data=new SerializedObject(label);labels.Add(new Presentation.LabelState{target=label,key=data.FindProperty("key").stringValue});}
            foreach(var group in root.GetComponentsInChildren<CanvasGroup>(true))groups.Add(new Presentation.GroupState{target=group,alpha=group.alpha});
            foreach(var item in visibility)if(item!=null)objects.Add(new Presentation.ActiveState{target=item,active=item.activeSelf});
            var preset=new Presentation.Preset{name=name,channels=channels,methodPrefab=method,fixedLayout=fixedLayout,rects=r.ToArray(),images=images.ToArray(),texts=text.ToArray(),behaviours=behaviours.ToArray(),labels=labels.ToArray(),objects=objects.ToArray(),groups=groups.ToArray()};ExcludePooled(preset);return preset;
        }
        static void StyleText(TMP_Text text,ReferencePrefabTools a,float size,TextAlignmentOptions alignment,Color? color=null)
        {
            Ensure<PreserveAuthoredFont>(text);text.font=a.Font;text.fontSharedMaterial=a.Font.material;text.fontSize=text.fontSizeMax=size*a.SY;text.fontSizeMin=size*a.SY*.6f;text.enableAutoSizing=true;text.enableWordWrapping=false;text.alignment=alignment;text.color=color??new Color(.025f,.02f,.32f);text.enableVertexGradient=false;text.margin=Vector4.zero;
        }
        static void Field(ReferencePrefabTools a,AdvancedInputField input,string slice,Rect box)
        {
            a.Local(input.transform,box);var background=input.transform.Find("Background");Stretch(background);a.Visual(background,slice);background.GetComponent<Image>().raycastTarget=true;
            var area=(RectTransform)input.transform.Find("TextArea");Stretch(area);area.offsetMin=new Vector2(28,10)*a.SY;area.offsetMax=new Vector2(-23,-10)*a.SY;
            string fontPath=a.Root+"FieldText.mat";var fontMaterial=AssetDatabase.LoadAssetAtPath<Material>(fontPath);if(fontMaterial==null){fontMaterial=new Material(a.Font.material);AssetDatabase.CreateAsset(fontMaterial,fontPath);}fontMaterial.CopyPropertiesFromMaterial(a.Font.material);fontMaterial.SetFloat("_FaceDilate",-.075f);EditorUtility.SetDirty(fontMaterial);
            foreach(var label in input.GetComponentsInChildren<TMP_Text>(true)){StyleText(label,a,36,TextAlignmentOptions.MidlineLeft,label.name=="Placeholder"?new Color(.58f,.58f,.73f):(Color?)null);label.fontSharedMaterial=fontMaterial;}
        }
        static void ProfileLabel(TMP_Text text,string key,Presentation.Preset original)
        {
            var old=text.GetComponent<UILanguageLabel>();if(old!=null)old.enabled=false;
            var label=text.GetComponent<CoralLocalizedLabel>();if(label==null){label=text.gameObject.AddComponent<CoralLocalizedLabel>();var list=new List<Presentation.EnabledState>(original.behaviours);list.Add(new Presentation.EnabledState{target=label,enabled=false});original.behaviours=list.ToArray();}
            label.enabled=true;label.SetKey(key);
        }
        static WithdrawWay MethodPrefab(ReferencePrefabTools a,WithdrawWay original,bool dana=false)
        {
            string path=a.Root+"PaymentWay.prefab";if(!File.Exists(path))AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original),path);var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var way=root.GetComponent<WithdrawWay>();a.Place(root.transform,new Rect(0,0,338,143));var baseImage=a.Visual(root.transform,"Method");baseImage.raycastTarget=true;root.GetComponent<Button>().targetGraphic=baseImage;
                var logo=way.payIcon.rectTransform;logo.anchorMin=new Vector2(.07f,.5f);logo.anchorMax=new Vector2(.77f,.5f);logo.pivot=new Vector2(.5f,.5f);logo.anchoredPosition=Vector2.zero;logo.sizeDelta=new Vector2(0,103*a.SY);way.payIcon.preserveAspect=true;way.payIcon.raycastTarget=false;
                Stretch(way.selectedObj.transform);var selected=a.Visual(way.selectedObj.transform,"SelectedMethod");selected.enabled=true;way.selectedObj.transform.SetAsFirstSibling();foreach(Transform child in way.selectedObj.transform)child.gameObject.SetActive(false);
                var data=new SerializedObject(way);data.FindProperty("styledIcon").objectReferenceValue=null;var styles=data.FindProperty("resourceIconStyles");styles.arraySize=2;for(int i=0;i<2;i++){var item=styles.GetArrayElementAtIndex(i);string logoName=dana?(i==0?"OvoLogo":"DanaLogo"):(i==0?"PixLogo":"PagBankLogo");item.FindPropertyRelative("channel").stringValue=dana?(i==0?UIWithdrawalPanel.ovoInfo:UIWithdrawalPanel.danaInfo):(i==0?UIWithdrawalPanel.pixInfo:UIWithdrawalPanel.pagBankInfo);item.FindPropertyRelative("resourcePath").stringValue=a.Resource;item.FindPropertyRelative("spriteName").stringValue=logoName;item.FindPropertyRelative("material").objectReferenceValue=a.Mat(logoName);}data.ApplyModifiedPropertiesWithoutUndo();Validate(root.transform);PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<WithdrawWay>();
        }
        public static void ApplyPix()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var a=new ReferencePrefabTools("PIX",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/43323a5d246d-03-账号填写-PIX.png")),852,1846);
            a.Slice("Backdrop",0,0,852,1846).Slice("Panel",40,199,772,1548,new Vector4(62,62,62,62)).Slice("Title",189,105,478,84).Slice("Close",726,70,91,91)
                .Slice("Amount",84,473,684,179).Slice("CPFInput",85,735,683,100).Slice("NameInput",85,913,683,101).Slice("KeyInput",85,1405,683,101)
                .Slice("Continue",84,1546,685,143).Slice("KeyEmail",84,1096,338,109).Slice("KeyCPF",433,1096,337,109).Slice("KeyPhone",84,1216,338,108).Slice("KeyRandom",433,1216,337,108)
                .Slice("Method",458,309,314,143).Slice("SelectedMethod",81,309,369,143).Slice("PixLogo",111,336,247,94).Slice("PagBankLogo",490,339,246,83)
                .Slice("MailIcon",119,1124,54,50).Slice("CpfIcon",468,1123,46,53).Slice("PhoneIcon",121,1244,49,52).Slice("RandomIcon",468,1244,48,50);
            a.Import();a.Erase(a.Mat("Backdrop"),new Rect(28,191,796,1570),new Rect(175,96,508,101),new Rect(716,58,111,117));a.Mat("Backdrop").SetFloat("_SampleX",840);a.Mat("Backdrop").SetFloat("_EraseFeather",8);
            a.Erase(a.Mat("Panel"),new Rect(76,251,700,1450));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(73,665,1,0));a.Mat("Panel").SetFloat("_EraseFeather",10);a.Round(a.Mat("Panel"),a.R("Panel"),60,1);
            a.Round(a.Mat("Close"),a.R("Close"),45,1);a.Erase(a.Mat("Amount"),new Rect(104,491,329,146));a.Mat("Amount").SetFloat("_SampleX",714);a.Round(a.Mat("Amount"),a.R("Amount"),32,1);
            foreach(string slice in new[]{"CPFInput","NameInput","KeyInput"}){var box=a.R(slice);a.Erase(a.Mat(slice),new Rect(box.x+25,box.y+21,box.width-52,57));a.Mat(slice).SetFloat("_SampleY",box.y+15);a.Round(a.Mat(slice),box,25,1);}
            a.Round(a.Mat("Continue"),a.R("Continue"),65,1);
            var blank=a.Material("ContinueBlank");a.Erase(blank,new Rect(281,1581,291,77));blank.SetFloat("_SampleX",217);a.Round(blank,a.R("Continue"),65,1);
            foreach(string slice in new[]{"KeyEmail","KeyCPF","KeyPhone","KeyRandom"}){var box=a.R(slice);a.Round(a.Mat(slice),box,26,1);}
            a.Erase(a.Mat("KeyCPF"),new Rect(456,1115,288,73));a.Mat("KeyCPF").SetFloat("_SampleY",1110);a.Round(a.Mat("KeyCPF"),a.R("KeyCPF"),26,1);
            a.Erase(a.Mat("KeyEmail"),new Rect(106,1115,67,76),new Rect(195,1124,130,65));a.Mat("KeyEmail").SetFloat("_SampleX",179);a.Round(a.Mat("KeyEmail"),a.R("KeyEmail"),26,1);
            foreach(string icon in new[]{"PixLogo","PagBankLogo","MailIcon","CpfIcon","PhoneIcon","RandomIcon"})a.Mat(icon).SetFloat("_WhiteMatte",1);a.Mat("Title").SetFloat("_BlueMatte",1);
            a.Erase(a.Mat("Method"),new Rect(481,329,264,98));a.Mat("Method").SetFloat("_SampleY",324);a.Round(a.Mat("Method"),a.R("Method"),27,1);
            a.Erase(a.Mat("SelectedMethod"),new Rect(101,329,260,108));a.Mat("SelectedMethod").SetFloat("_SampleX",365);a.Round(a.Mat("SelectedMethod"),a.R("SelectedMethod"),27,1);
            var go=PrefabUtility.LoadPrefabContents(FormPath);
            try
            {
                var model=go.GetComponent<UIWithdrawalPanel>();var tr=go.transform;var body=tr.Find("Root/FillRoot");var component=Ensure<Presentation>(tr);Bind(model,"presentation",component);
                // Only the designated common artwork changes visibility; platform and validation state remain owned by the controller.
                var pixArt=tr.Find("PixArtwork")??Child(tr,"PixArtwork");Stretch(pixArt);pixArt.SetAsFirstSibling();pixArt.SetSiblingIndex(tr.Find("Root").GetSiblingIndex()-1);pixArt.gameObject.SetActive(false);
                var visibility=new List<GameObject>{pixArt.gameObject,tr.Find("ReferenceBackdrop").gameObject};
                foreach(var caption in go.GetComponentsInChildren<ApprovedHudCaption>(true))visibility.Add(caption.gameObject);
                var original=component.presets!=null&&component.presets.Length>0?component.presets[0]:Capture(go,"Original",Array.Empty<string>(),model.withdrawWayItem,false,visibility.ToArray());ExcludePooled(original);original.Apply();
                var retainedObjects=new List<Presentation.ActiveState>();foreach(var state in original.objects)if(state.target!=null){if(state.target.name.StartsWith("Pix",StringComparison.Ordinal)&&state.target!=pixArt.gameObject)UnityEngine.Object.DestroyImmediate(state.target);else retainedObjects.Add(state);}original.objects=retainedObjects.ToArray();visibility.RemoveAll(x=>x==null);
                Clear(pixArt);pixArt.gameObject.SetActive(true);tr.Find("ReferenceBackdrop").gameObject.SetActive(false);var backdrop=a.Graphic(pixArt,"Backdrop");backdrop.GetComponent<Image>().material=null;backdrop.GetComponent<CoralResourceSprite>().SetSource("AllUI20260924/WithdrawBackdrop","");
                // PageBackdrop already fills the viewport. A second fixed-width copy creates visible side seams on wider screens.
                backdrop.gameObject.SetActive(false);
                foreach(var caption in go.GetComponentsInChildren<ApprovedHudCaption>(true))caption.gameObject.SetActive(false);
                foreach(var group in go.GetComponentsInChildren<CanvasGroup>(true))group.alpha=1;
                foreach(var layout in body.GetComponentsInChildren<LayoutGroup>(true))layout.enabled=false;foreach(var fitter in body.GetComponentsInChildren<ContentSizeFitter>(true))fitter.enabled=false;
                Stretch(tr.Find("Root"));Stretch(body);a.Place(model.BG,a.R("Panel"));a.Visual(model.BG,"Panel");model.BG.GetComponent<Image>().raycastTarget=true;
                var title=body.Find("Title").GetComponent<TMP_Text>();a.Place(title.transform,new Rect(166,92,520,99));StyleText(title,a,66,TextAlignmentOptions.Center,Color.white);title.GetComponent<CoralLocalizedLabel>().SetKey("withdraw_form_title");
                a.Caption(body,title,"Title","Conta de saque");var titleArt=body.Find("ReferenceTitle");titleArt.name="PixTitleCaption";visibility.Add(titleArt.gameObject);
                a.Place(body.Find("pageClose"),a.R("Close"));a.Visual(body.Find("pageClose"),"Close");body.Find("pageClose").GetComponent<Image>().raycastTarget=true;
                var methodTitle=a.Text(body,"PixMethodLabel",new Rect(87,254,680,55),38,TextAlignmentOptions.MidlineLeft);Localize(methodTitle,"sequential_method");visibility.Add(methodTitle.gameObject);
                a.Place(model.PlatformRoot.transform,new Rect(82,310,688,142));var ways=model.PlatformRoot.GetComponent<HorizontalLayoutGroup>();ways.enabled=true;ways.spacing=13*a.SX;ways.childControlWidth=ways.childControlHeight=true;ways.childForceExpandWidth=ways.childForceExpandHeight=true;
                a.Place(model.PlatformIconRoot.transform,new Rect(82,310,688,142));
                var amount=body.Find("AmountPlate");a.Place(amount,a.R("Amount"));a.Visual(amount,"Amount");a.Local(amount.Find("Label"),new Rect(26,19,625,48));StyleText(amount.Find("Label").GetComponent<TMP_Text>(),a,38,TextAlignmentOptions.MidlineLeft);amount.Find("Label").GetComponent<CoralLocalizedLabel>().SetKey("withdraw_confirm_amount");
                a.Local(model.balanceText.transform,new Rect(26,65,625,98));StyleText(model.balanceText,a,85,TextAlignmentOptions.MidlineLeft,new Color(0,.63f,.1f));
                Stretch(model.InputRoot);var info=body.Find("pageContent/InfoContent");Stretch(info);
                var cpf=info.Find("CPF");a.Place(cpf,new Rect(85,688,683,147));a.Local(cpf.Find("Text (TMP)"),new Rect(2,0,679,46));StyleText(cpf.Find("Text (TMP)").GetComponent<TMP_Text>(),a,37,TextAlignmentOptions.MidlineLeft);Field(a,model.CPFNumberInput,"CPFInput",new Rect(0,47,683,100));
                var name=info.Find("Name");a.Place(name,new Rect(85,867,683,147));a.Local(name.Find("Text (TMP)"),new Rect(2,0,679,46));StyleText(name.Find("Text (TMP)").GetComponent<TMP_Text>(),a,37,TextAlignmentOptions.MidlineLeft);Field(a,model.accountNameInput,"NameInput",new Rect(0,46,683,101));
                var document=info.Find("document");a.Place(document,new Rect(85,1045,683,461));a.Local(document.Find("Text (TMP)"),new Rect(2,0,679,47));StyleText(document.Find("Text (TMP)").GetComponent<TMP_Text>(),a,37,TextAlignmentOptions.MidlineLeft);
                ProfileLabel(cpf.Find("Text (TMP)").GetComponent<TMP_Text>(),"sequential_cpf_label",original);ProfileLabel(name.Find("Text (TMP)").GetComponent<TMP_Text>(),"sequential_full_name",original);ProfileLabel(document.Find("Text (TMP)").GetComponent<TMP_Text>(),"sequential_pix_type",original);
                var channelRoot=document.Find("SelectChannel ");a.Local(channelRoot,new Rect(0,51,683,228));
                for(int i=0;i<model.accountTypeDropdown.Count;i++)
                {
                    var option=model.accountTypeDropdown[i];a.Local(option.btn.transform,new Rect(i%2*349,i/2*120,337,109));a.Visual(option.btn.transform,"KeyCPF");option.btn.targetGraphic=option.btn.GetComponent<Image>();option.btn.GetComponent<Image>().raycastTarget=true;
                    Stretch(option.selectedIcon.transform);var select=a.Visual(option.selectedIcon.transform,"KeyEmail");select.enabled=true;option.selectedIcon.transform.SetAsFirstSibling();foreach(Transform child in option.selectedIcon.transform){child.gameObject.SetActive(false);visibility.Add(child.gameObject);}
                    var icon=Child(option.btn.transform,"PixKeyIcon"+i);a.Local(icon,new Rect(32,28,54,53));a.Visual(icon,new[]{"MailIcon","CpfIcon","PhoneIcon","RandomIcon"}[i]);icon.GetComponent<Image>().preserveAspect=true;visibility.Add(icon.gameObject);
                    // Text remains live and localized; static icons and card silhouettes come from the approved art.
                    var label=option.btn.GetComponentInChildren<TMP_Text>(true);a.Local(label.transform,new Rect(106,25,205,61));StyleText(label,a,32,TextAlignmentOptions.MidlineLeft);ProfileLabel(label,new[]{"sequential_key_email","sequential_key_cpf","sequential_key_phone","sequential_key_random"}[i],original);
                }
                var keyRoot=document.Find("document_New");a.Local(keyRoot,new Rect(0,314,683,147));var keyTitle=a.Text(body,"PixKeyLabel",new Rect(87,1357,679,47),37,TextAlignmentOptions.MidlineLeft);Localize(keyTitle,"sequential_pix_key");visibility.Add(keyTitle.gameObject);Field(a,model.accountIdentificationInput,"KeyInput",new Rect(0,46,683,101));
                foreach(var pair in new[]{(model.CPFNumberErrorTra,835f),(model.accountNameErrorTra,1014f),(model.accountIdentificationErrorTra,1507f)}){a.Place(pair.Item1,new Rect(87,pair.Item2,679,31));foreach(var label in pair.Item1.GetComponentsInChildren<TMP_Text>(true)){Stretch(label.transform);StyleText(label,a,24,TextAlignmentOptions.MidlineLeft,new Color(.9f,.07f,.08f));}}
                var action=body.Find("pageContent/BtnWithdrawal");a.Place(action,a.R("Continue"));a.Visual(action,"Continue","ContinueBlank");action.GetComponent<Image>().raycastTarget=true;var actionText=action.Find("Text (TMP)").GetComponent<TMP_Text>();Stretch(actionText.transform);StyleText(actionText,a,66,TextAlignmentOptions.Center,Color.white);
                a.Caption(body,actionText,"Continue","Continuar");var continueArt=body.Find("ReferenceContinue");continueArt.SetParent(action,true);continueArt.name="PixContinueCaption";visibility.Add(continueArt.gameObject);
                var authored=Capture(go,"PIX",new[]{UIWithdrawalPanel.pixInfo},MethodPrefab(a,original.methodPrefab),true,visibility.ToArray());
                if(original.prompts==null)original.prompts=new[]{new Presentation.PromptState{target=model.CPFNumberInput,original=model.CPFNumberInput.PlaceHolderText},new Presentation.PromptState{target=model.accountNameInput,original=model.accountNameInput.PlaceHolderText}};
                authored.prompts=new[]{new Presentation.PromptState{target=model.CPFNumberInput,key="sequential_cpf_prompt"},new Presentation.PromptState{target=model.accountNameInput,key="sequential_name_prompt"}};
                var defaultObjects=new List<Presentation.ActiveState>(original.objects);foreach(var extra in visibility)if(extra!=null&&extra.name.StartsWith("Pix",StringComparison.Ordinal)&&extra!=pixArt.gameObject)defaultObjects.Add(new Presentation.ActiveState{target=extra,active=false});original.objects=defaultObjects.ToArray();
                // PagBank uses the same identity inputs but has an email field instead of PIX type choices.
                a.Place(model.BG,new Rect(40,199,772,1238));a.Sliced(model.BG.GetComponent<Image>());keyTitle.gameObject.SetActive(false);
                var email=info.Find("EmailInfo");a.Place(email,new Rect(85,1045,683,147));a.Local(model.paypalMailTitle.transform,new Rect(2,0,679,46));StyleText(model.paypalMailTitle,a,37,TextAlignmentOptions.MidlineLeft);ProfileLabel(model.paypalMailTitle,"sequential_pagbank_account",original);Field(a,model.paypalMailInput,"NameInput",new Rect(0,46,683,101));
                a.Place(info.Find("PayPalEmailMessageSlot"),new Rect(85,1193,683,38));a.Place(action,new Rect(84,1240,685,143));Stretch(continueArt);
                var pagbank=Capture(go,"PagBank",new[]{UIWithdrawalPanel.pagBankInfo},authored.methodPrefab,true,visibility.ToArray());pagbank.prompts=authored.prompts;
                ConfigureSingleMethodLayout(model,authored);ConfigureSingleMethodLayout(model,pagbank);
                component.presets=new[]{original,authored,pagbank};original.Apply();Validate(tr);PrefabUtility.SaveAsPrefabAsset(go,FormPath);
            }finally{PrefabUtility.UnloadPrefabContents(go);}
            AssetDatabase.SaveAssets();
        }
    }
}

