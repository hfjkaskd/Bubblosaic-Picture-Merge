using System;
using System.Collections.Generic;
using System.IO;
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
        public static void ApplyDana()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            var a=new ReferencePrefabTools("DANA",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/622b80edc604-04-账号填写-DANA.png")),852,1846);
            a.Slice("Panel",41,349,771,1128,new Vector4(70,70,70,70)).Slice("Title",220,172,421,137).Slice("Close",698,101,106,106)
                .Slice("Amount",98,490,655,137).Slice("Input",93,1029,667,134).Slice("Continue",96,1266,661,144)
                .Slice("Method",93,749,331,164).Slice("SelectedMethod",429,748,333,167).Slice("OvoLogo",125,782,224,94).Slice("DanaLogo",456,783,214,91);
            a.Import();a.Erase(a.Mat("Panel"),new Rect(90,414,674,1007));a.Mat("Panel").SetVector("_SamplePoint",new Vector4(82,677,1,0));a.Mat("Panel").SetFloat("_EraseFeather",7);a.Round(a.Mat("Panel"),a.R("Panel"),90,1);
            a.Mat("Title").SetFloat("_BlueMatte",1);a.Round(a.Mat("Close"),a.R("Close"),52,1);
            a.Erase(a.Mat("Amount"),new Rect(238,516,378,96));a.Mat("Amount").SetFloat("_SampleX",704);a.Mat("Amount").SetFloat("_EraseFeather",6);a.Round(a.Mat("Amount"),a.R("Amount"),33,1);
            a.Erase(a.Mat("Input"),new Rect(121,1054,590,90));a.Mat("Input").SetFloat("_SampleY",1045);a.Round(a.Mat("Input"),a.R("Input"),35,1);
            a.Round(a.Mat("Continue"),a.R("Continue"),67,1);var blank=a.Material("ContinueBlank");a.Erase(blank,new Rect(299,1308,253,70));blank.SetFloat("_SampleX",226);a.Round(blank,a.R("Continue"),67,1);
            a.Erase(a.Mat("Method"),new Rect(115,775,266,110));a.Mat("Method").SetFloat("_SampleY",767);a.Round(a.Mat("Method"),a.R("Method"),31,1);
            a.Erase(a.Mat("SelectedMethod"),new Rect(447,774,226,114));a.Mat("SelectedMethod").SetVector("_SamplePoint",new Vector4(654,886,1,0));a.Mat("SelectedMethod").SetFloat("_EraseFeather",10);a.Round(a.Mat("SelectedMethod"),a.R("SelectedMethod"),31,1);
            foreach(string icon in new[]{"OvoLogo","DanaLogo"})a.Mat(icon).SetFloat("_WhiteMatte",1);
            var go=PrefabUtility.LoadPrefabContents(FormPath);
            try
            {
                var model=go.GetComponent<UIWithdrawalPanel>();var tr=go.transform;var body=tr.Find("Root/FillRoot");var p=go.GetComponent<Presentation>();
                var retained=new List<Presentation.Preset>();foreach(var preset in p.presets)if(preset.name!="DANA"&&preset.name!="OVO")retained.Add(preset);var original=retained[0];original.Apply();
                var old=tr.Find("DanaArtwork");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
                foreach(string generated in new[]{"DanaMethod","DanaPhoneHelp"}){var prior=body.Find(generated);if(prior!=null)UnityEngine.Object.DestroyImmediate(prior.gameObject);}var priorAction=body.Find("pageContent/BtnWithdrawal/DanaContinueCaption");if(priorAction!=null)UnityEngine.Object.DestroyImmediate(priorAction.gameObject);
                foreach(var preset in retained){preset.objects=Array.FindAll(preset.objects,x=>x.target!=null);preset.rects=Array.FindAll(preset.rects,x=>x.target!=null);preset.images=Array.FindAll(preset.images,x=>x.target!=null);preset.texts=Array.FindAll(preset.texts,x=>x.target!=null);preset.behaviours=Array.FindAll(preset.behaviours,x=>x.target!=null);preset.labels=Array.FindAll(preset.labels,x=>x.target!=null);preset.groups=Array.FindAll(preset.groups,x=>x.target!=null);}
                var art=Child(tr,"DanaArtwork");Stretch(art);art.SetAsFirstSibling();art.SetSiblingIndex(tr.Find("Root").GetSiblingIndex()-1);
                var backdrop=Child(art,"Backdrop");Stretch(backdrop);var img=Ensure<Image>(backdrop);img.raycastTarget=false;var binder=Ensure<CoralResourceSprite>(backdrop);var binding=new SerializedObject(binder);binding.FindProperty("_image").objectReferenceValue=img;binding.ApplyModifiedPropertiesWithoutUndo();binder.SetSource("AllUI20260924/WithdrawBackdrop","");
                var visibility=new List<GameObject>{art.gameObject,tr.Find("ReferenceBackdrop").gameObject,tr.Find("PixArtwork").gameObject};
                foreach(var state in original.objects)if(state.target!=null&&!visibility.Contains(state.target))visibility.Add(state.target);
                foreach(var item in visibility)if(item!=art.gameObject)item.SetActive(false);
                foreach(var c in go.GetComponentsInChildren<ApprovedHudCaption>(true)){c.gameObject.SetActive(false);if(!visibility.Contains(c.gameObject))visibility.Add(c.gameObject);}
                foreach(var g in go.GetComponentsInChildren<CanvasGroup>(true))g.alpha=1;
                foreach(var l in body.GetComponentsInChildren<LayoutGroup>(true))l.enabled=false;foreach(var f in body.GetComponentsInChildren<ContentSizeFitter>(true))f.enabled=false;
                Stretch(tr.Find("Root"));Stretch(body);a.Place(model.BG,new Rect(41,349,771,1310));a.Sliced(a.Visual(model.BG,"Panel"));model.BG.GetComponent<Image>().raycastTarget=true;
                var title=body.Find("Title").GetComponent<TMP_Text>();a.Place(title.transform,new Rect(165,169,522,140));StyleText(title,a,68,TextAlignmentOptions.Center,Color.white);title.enableWordWrapping=true;title.GetComponent<CoralLocalizedLabel>().SetKey("withdraw_form_title");a.Caption(art,title,"Title","Withdrawal account");
                a.Place(body.Find("pageClose"),a.R("Close"));a.Visual(body.Find("pageClose"),"Close");body.Find("pageClose").GetComponent<Image>().raycastTarget=true;
                var amount=body.Find("AmountPlate");a.Place(amount,a.R("Amount"));a.Visual(amount,"Amount");a.Local(amount.Find("Label"),new Rect(10,-67,620,57));StyleText(amount.Find("Label").GetComponent<TMP_Text>(),a,39,TextAlignmentOptions.MidlineLeft);amount.Find("Label").GetComponent<CoralLocalizedLabel>().SetKey("sequential_amount_to_withdraw");
                Stretch(model.balanceText.transform);StyleText(model.balanceText,a,79,TextAlignmentOptions.Center,new Color(.015f,.08f,.6f));
                var method=a.Text(body,"DanaMethod",new Rect(100,681,663,60),39,TextAlignmentOptions.MidlineLeft);Localize(method,"sequential_method");visibility.Add(method.gameObject);
                a.Place(model.PlatformRoot.transform,new Rect(94,750,665,160));var row=model.PlatformRoot.GetComponent<HorizontalLayoutGroup>();row.enabled=true;row.spacing=10*a.SX;row.childControlWidth=row.childControlHeight=true;row.childForceExpandWidth=row.childForceExpandHeight=true;a.Place(model.PlatformIconRoot.transform,new Rect(94,750,665,160));
                var info=body.Find("pageContent/InfoContent");Stretch(info);Stretch(model.InputRoot);
                var name=info.Find("Name");a.Place(name,new Rect(94,951,665,174));a.Local(name.Find("Text (TMP)"),new Rect(6,0,653,50));StyleText(name.Find("Text (TMP)").GetComponent<TMP_Text>(),a,36,TextAlignmentOptions.MidlineLeft);ProfileLabel(name.Find("Text (TMP)").GetComponent<TMP_Text>(),"sequential_full_name",original);Field(a,model.accountNameInput,"Input",new Rect(0,53,665,118));
                var phone=info.Find("OVO_New");a.Place(phone,new Rect(94,1164,665,174));var phoneTitle=phone.GetComponentInChildren<TMP_Text>(true);a.Local(phoneTitle.transform,new Rect(6,0,653,50));StyleText(phoneTitle,a,36,TextAlignmentOptions.MidlineLeft);ProfileLabel(phoneTitle,"sequential_dana_account",original);Field(a,model.accPhoneMailInput,"Input",new Rect(0,53,665,118));
                foreach(var pair in new[]{(model.accountNameErrorTra,1127f),(model.accPhoneMailErrorTra,1339f)}){a.Place(pair.Item1,new Rect(100,pair.Item2,652,34));foreach(var text in pair.Item1.GetComponentsInChildren<TMP_Text>(true)){Stretch(text.transform);StyleText(text,a,24,TextAlignmentOptions.MidlineLeft,new Color(.9f,.07f,.08f));}}
                var help=a.Text(body,"DanaPhoneHelp",new Rect(101,1378,652,48),29,TextAlignmentOptions.MidlineLeft,new Color(.38f,.55f,.74f));Localize(help,"sequential_dana_phone_help");visibility.Add(help.gameObject);
                var action=body.Find("pageContent/BtnWithdrawal");a.Place(action,new Rect(96,1460,661,144));a.Visual(action,"Continue","ContinueBlank");action.GetComponent<Image>().raycastTarget=true;var actionText=action.Find("Text (TMP)").GetComponent<TMP_Text>();Stretch(actionText.transform);StyleText(actionText,a,64,TextAlignmentOptions.Center,Color.white);a.Caption(action,actionText,"Continue","Continue");Stretch(action.Find("ReferenceContinue"));visibility.Add(action.Find("ReferenceContinue").gameObject);
                var methodPrefab=MethodPrefab(a,original.methodPrefab,true);var dana=Capture(go,"DANA",new[]{UIWithdrawalPanel.danaInfo},methodPrefab,true,visibility.ToArray());
                dana.prompts=new[]{new Presentation.PromptState{target=model.accountNameInput,key="sequential_name_prompt"},new Presentation.PromptState{target=model.accPhoneMailInput,key="sequential_id_phone_prompt"}};
                phoneTitle.GetComponent<CoralLocalizedLabel>().SetKey("sequential_ovo_account");var ovo=Capture(go,"OVO",new[]{UIWithdrawalPanel.ovoInfo},methodPrefab,true,visibility.ToArray());ovo.prompts=dana.prompts;
                action.Find("ReferenceContinue").name="DanaContinueCaption";
                foreach(var preset in retained){var objects=new List<Presentation.ActiveState>(preset.objects);foreach(var extra in new[]{art.gameObject,method.gameObject,help.gameObject,action.Find("DanaContinueCaption").gameObject})objects.Add(new Presentation.ActiveState{target=extra,active=false});preset.objects=objects.ToArray();}
                retained.Add(dana);retained.Add(ovo);p.presets=retained.ToArray();original.Apply();Validate(tr);PrefabUtility.SaveAsPrefabAsset(go,FormPath);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
    }
}
