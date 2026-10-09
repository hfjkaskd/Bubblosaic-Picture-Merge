using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BubblePics.EditorTools.ReferencePrefabTools;
namespace BubblePics.EditorTools
{
    public static class SequentialServiceAuthoring
    {
        public static void Apply()
        {
            var a=new ReferencePrefabTools("ServicePanel",Path.GetFullPath(Path.Combine(Application.dataPath,"../../_ui_audit/all-ui-review-20260924/art/104735c53400-12-ServicePanel-simple.png")),849,1852);
            a.Slice("Background",0,0,849,1852).Slice("Header",208,34,427,113).Slice("Back",17,66,105,108).Slice("History",617,67,107,121).Slice("FAQ",729,67,105,121)
                .Slice("Quick",234,1399,383,102).Slice("InputBar",22,1540,807,146).Slice("Input",44,1563,629,94).Slice("Clear",604,1588,43,43).Slice("Send",687,1551,121,121)
                .Slice("IssueAvatar",62,339,148,151).Slice("PlayerAvatar",649,555,141,148)
                .Slice("IssueBubble",210,340,430,155,new Vector4(62,43,48,46)).Slice("PlayerBubble",215,555,438,162,new Vector4(45,43,60,46));
            a.Import();a.Erase(a.Mat("Background"),new Rect(55,330,603,175),new Rect(207,547,590,180),new Rect(55,777,598,180),default,new Rect(18,1536,816,153));a.Mat("Background").SetFloat("_SampleX",799);a.Mat("Background").SetFloat("_TailSampleX",3);a.Mat("Background").SetFloat("_EraseFeather",8);
            // The editable controls redraw on the original base, with only their captions cleared.
            var header=a.Material("HeaderBlank");a.Erase(header,new Rect(277,58,291,63));header.SetFloat("_SampleX",580);header.SetFloat("_EraseFeather",3);
            var quick=a.Material("QuickBlank");a.Erase(quick,new Rect(355,1427,244,52));quick.SetFloat("_SampleX",342);a.Round(quick,a.R("Quick"),48);a.Round(a.Mat("Quick"),a.R("Quick"),48);
            a.Erase(a.Mat("Input"),new Rect(77,1588,510,49),new Rect(604,1587,43,44));a.Mat("Input").SetFloat("_SampleX",580);a.Round(a.Mat("Input"),a.R("Input"),44);
            a.Erase(a.Mat("InputBar"),new Rect(39,1559,641,100),new Rect(681,1548,132,128));a.Mat("InputBar").SetVector("_SamplePoint",new Vector4(678,1570,1,0));a.Round(a.Mat("InputBar"),a.R("InputBar"),65);
            a.Erase(a.Mat("IssueBubble"),new Rect(264,385,328,89));a.Mat("IssueBubble").SetFloat("_SampleX",612);a.Mat("IssueBubble").SetFloat("_EraseFeather",4);
            a.Erase(a.Mat("PlayerBubble"),new Rect(247,587,365,116));a.Mat("PlayerBubble").SetVector("_SamplePoint",new Vector4(590,660,1,0));a.Mat("PlayerBubble").SetFloat("_EraseFeather",8);a.Round(a.Mat("IssueBubble"),a.R("IssueBubble"),43);a.Round(a.Mat("PlayerBubble"),a.R("PlayerBubble"),43);
            foreach(string n in new[]{"Back","Send","Clear","IssueAvatar","PlayerAvatar"}){Rect r=a.R(n);a.Round(a.Mat(n),r,Mathf.Min(r.width,r.height)/2);}
            const string path="Assets/BizzaWZ/Final/Real/UI/ServicePanel/ServicePanel.prefab";var go=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var p=go.GetComponent<ServicePanel>();var t=go.transform;foreach(var pathToRemove in new[]{"SequentialQuick","Title/SequentialHeader","Title/SequentialTitle","Title/ReferenceHeader"}){var old=t.Find(pathToRemove);if(old!=null)Object.DestroyImmediate(old.gameObject);}foreach(var name in new[]{"IssueAvatar","PlayerAvatar"}){var old=p.chatElementPrefab.chatInfoRoot.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);}Stretch(t);var bg=t.Find("BG");Stretch(bg);a.Visual(bg,"Background");bg.SetAsFirstSibling();var title=t.Find("Title");Stretch(title);title.Find("bg").gameObject.SetActive(false);
                foreach(var old in t.GetComponentsInChildren<Transform>(true))if(old!=null&&(old.name=="PageBackdrop"||old.name=="ChatPanel"||old.name=="WoodHeading"||old.name=="OpaqueUnderlay"))Object.DestroyImmediate(old.gameObject);bg.GetComponent<Image>().enabled=true;
                if(PrefabUtility.IsAnyPrefabInstanceRoot(p.chatElementPrefab.gameObject))PrefabUtility.UnpackPrefabInstance(p.chatElementPrefab.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var oldTitle=title.Find("Title");oldTitle.gameObject.SetActive(false);var h=a.Graphic(title,"SequentialHeader","Header");h.GetComponent<Image>().material=header;var label=a.Text(title,"SequentialTitle",new Rect(271,53,305,78),58,color:Color.white);Localize(label,"seq_feedback");a.Caption(title,label,"Header","Feedback");
                StyleButton(a,title.Find("CloseBtn"),"Back");StyleButton(a,title.Find("HistoryBtn"),"History");StyleButton(a,title.Find("FQA"),"FAQ");
                Stretch(t.Find("Content"));a.Place(p.scrollRect.transform,new Rect(33,329,782,1050));Stretch(p.scrollRect.viewport);var mask=p.scrollRect.viewport.GetComponent<Mask>();if(mask!=null)mask.showMaskGraphic=false;
                var v=(RectTransform)p.contentRoot;v.anchorMin=new Vector2(0,1);v.anchorMax=Vector2.one;v.pivot=new Vector2(.5f,1);v.anchoredPosition=Vector2.zero;v.sizeDelta=Vector2.zero;
                var list=Ensure<VerticalLayoutGroup>(v);list.childControlWidth=true;list.childControlHeight=false;list.childForceExpandWidth=true;list.childForceExpandHeight=false;list.spacing=62*a.SY;list.padding=new RectOffset(0,0,12,12);
                var input=t.Find("Content/InputNode");a.Place(input,new Rect(22,1540,807,146));var ir=(RectTransform)input;ir.anchorMin=ir.anchorMax=new Vector2(.5f,0);ir.anchoredPosition=new Vector2(ir.anchoredPosition.x,(1852-1613)*a.SY);var ib=input.Find("bg");Stretch(ib);a.Visual(ib,"InputBar");
                a.Local(p.selectQuestionButton.transform,new Rect(22,23,629,94));a.Visual(p.selectQuestionButton.transform,"Input").raycastTarget=true;p.selectQuestionButton.GetComponent<Button>().targetGraphic=p.selectQuestionButton.GetComponent<Image>();var chevron=p.selectQuestionButton.transform.Find("Chevron");if(chevron!=null)chevron.gameObject.SetActive(false);StyleText(a,p.selectQuestionText,32);Stretch(p.selectQuestionText.transform);p.selectQuestionText.margin=new Vector4(35*a.SX,0,75*a.SX,0);p.selectQuestionText.alignment=TextAlignmentOptions.MidlineLeft;
                a.Local(p.inputText.transform,new Rect(22,23,629,94));a.Visual(p.inputText.transform,"Input").raycastTarget=true;foreach(var tx in p.inputText.GetComponentsInChildren<TMP_Text>(true)){StyleText(a,tx,31);if(tx.name=="Placeholder")tx.color=new Color(.5f,.61f,.71f);}var area=(RectTransform)p.inputText.transform.Find("TextArea");area.offsetMin=new Vector2(35*a.SX,0);area.offsetMax=new Vector2(-75*a.SX,0);
                var clear=input.Find("ClearBtn");StyleButton(a,clear,"Clear");a.Local(clear,new Rect(582,48,43,43));
                foreach(var n in new[]{"CanSendBtn","NotCanSendBtn"}){var b=input.Find(n);StyleButton(a,b,"Send");a.Local(b,new Rect(665,11,121,121));}
                var quickButton=a.Button(t,"SequentialQuick","Quick",key:"seq_quick_replies",caption:"Quick replies",size:35);var quickLabel=quickButton.transform.Find("SequentialQuickLabel").GetComponent<TMP_Text>();Stretch(quickLabel.transform);quickLabel.margin=new Vector4(108*a.SX,0,17*a.SX,0);Bind(p,"quickReplyBtn",quickButton);
                var chat=p.chatElementPrefab;StyleText(a,chat.chatTxt,33);StyleText(a,chat.timeTxt,22);chat.timeTxt.transform.SetParent(chat.chatInfoRoot,false);var data=new SerializedObject(chat);
                Set(data,"maxBubbleWidth",430*a.SX);Set(data,"minimumBubbleWidth",430*a.SX);Set(data,"minimumBubbleHeight",153*a.SY);Set(data,"outerHorizontalPadding",181*a.SX);Set(data,"bubbleHorizontalPadding",53*a.SX);Set(data,"bubbleVerticalPadding",24*a.SY);Set(data,"bubbleTimeSpacing",8*a.SY);data.FindProperty("timeInsideBubble").boolValue=true;data.FindProperty("compactTime").boolValue=true;
                foreach(var n in new[]{"issueBubbleColor","playerBubbleColor"})data.FindProperty(n).colorValue=Color.white;foreach(var n in new[]{"issueTextColor","playerTextColor"})data.FindProperty(n).colorValue=new Color(.025f,.02f,.32f);data.FindProperty("timeTextColor").colorValue=new Color(.36f,.51f,.61f);data.ApplyModifiedPropertiesWithoutUndo();
                foreach(var pair in new[]{new[]{"Issue_bg","IssueBubble"},new[]{"Player_bg","PlayerBubble"}}){var im=a.Visual(chat.chatInfoRoot.Find(pair[0]),pair[1]);a.Sliced(im);}
                foreach(var n in new[]{"Issue","Player"}){var avatar=Child(chat.chatInfoRoot,n+"Avatar");var art=a.Visual(avatar,n+"Avatar");var r=(RectTransform)avatar;bool issue=n=="Issue";r.anchorMin=r.anchorMax=new Vector2(issue?0:1,1);r.pivot=new Vector2(issue?1:0,1);r.sizeDelta=new Vector2(148*a.SX,151*a.SY);r.anchoredPosition=new Vector2(issue?-4*a.SX:4*a.SX,0);Bind(chat,issue?"issueAvatar":"playerAvatar",avatar.gameObject);}
                Validate(t);PrefabUtility.SaveAsPrefabAsset(go,path);
            }finally{PrefabUtility.UnloadPrefabContents(go);}AssetDatabase.SaveAssets();
        }
        static void Set(SerializedObject o,string n,float value){o.FindProperty(n).floatValue=value;}
        static void StyleText(ReferencePrefabTools a,TMP_Text text,float size){Ensure<PreserveAuthoredFont>(text);text.font=a.Font;text.fontSharedMaterial=a.Font.material;text.fontSize=text.fontSizeMax=size*a.SY;text.fontSizeMin=size*a.SY*.6f;text.enableAutoSizing=false;text.color=new Color(.025f,.02f,.32f);text.raycastTarget=false;}
        static void StyleButton(ReferencePrefabTools a,Transform t,string slice){t.gameObject.SetActive(true);foreach(Transform child in t)child.gameObject.SetActive(false);a.Place(t,a.R(slice));var im=a.Visual(t,slice);im.raycastTarget=true;t.GetComponent<Button>().targetGraphic=im;t.GetComponent<BizzaButton>().scaleTarget=t;}
    }
}
