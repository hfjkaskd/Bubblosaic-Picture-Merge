using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async void ReviewFAQ()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/FAQPage-20260928"));Directory.CreateDirectory(folder);
            var report=new StringBuilder();int failures=0;string language=LanguageUtils.SelectedLanguage,locale=Localization.CurrentLocale;
            long lastWithdraw=0;
            void Check(bool pass,string name){report.AppendLine((pass?"PASS ":"FAIL ")+name);if(!pass)failures++;}
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null||SaveDataUtils.GameData==null)throw new InvalidOperationException("Formal InitWZ startup must finish first.");
                lastWithdraw=SaveDataUtils.GameData.lastWithdrawTime;
                CloseRuntime();LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                await UniTask.Delay(300,ignoreTimeScale:true);
                var page=(FAQPanel)await UIModule.Instance.OpenPage(UIPageIds.QFA);inspectedPage=page;await UniTask.Delay(350,ignoreTimeScale:true);
                if(page==null)throw new InvalidOperationException("FAQ page did not open after gameplay startup.");
                var scroll=page.GetComponentInChildren<ScrollRect>();var content=scroll.content;var close=Array.Find(page.GetComponentsInChildren<Button>(true),button=>button.name=="CloseBtn");
                Check(HitStandard(close),"Visible close control receives the standard Button raycast");
                Check(scroll.vertical&&!scroll.horizontal&&scroll.viewport.GetComponent<RectMask2D>()!=null,"Native vertical scrolling and viewport clipping are configured");
                Check(scroll.verticalNormalizedPosition>.999f,"Page opens at the first question");
                Check(close.gameObject.activeInHierarchy,"Close control is visible");
                for(int i=0;i<4;i++)
                {
                    var row=content.Find("QuickQuestion"+(i+1));var question=row.Find("Question").GetComponent<TMP_Text>();var answer=row.Find("Answer").GetComponent<TMP_Text>();
                    Check(question.text==FAQReferenceAuthoring.Questions[i]&&answer.text==FAQReferenceAuthoring.Answers[i],"Approved question and answer remain available as localized text "+(i+1));
                    report.AppendLine("SOURCE QuickQuestion"+(i+1)+" "+question.text+" | "+answer.text);
                    question.ForceMeshUpdate();Check(!question.isTextOverflowing,"Collapsed question fits its card "+(i+1));
                    Check(!answer.gameObject.activeSelf&&!row.GetComponent<FAQAccordionItem>().IsExpanded,"Quick answer starts collapsed "+(i+1));
                    Check(row.GetComponent<Button>()!=null&&row.GetComponent<Button>().targetGraphic!=null,"Quick card is clickable "+(i+1));
                }
                var original=page.GetComponentsInChildren<FAQDesc>(true);Check(original.Length==7,"All seven original detailed FAQ entries are retained");
                foreach(var desc in original)
                {
                    var expected=LanguageUtils.GetText(desc.key).GetReplaceDesc(page.titleColor).GetReplaceDesc(page.contentColor).GetReplaceDesc(page.highlightColor);
                    report.AppendLine("SOURCE "+desc.key+" "+expected.Replace("\n"," | "));
                    var row=desc.GetComponentInParent<FAQAccordionItem>(true);
                    bool duplicate=desc.key=="FAQPanel_Question1"||desc.key=="FAQPanel_Question3"||desc.key=="FAQPanel_Question4";
                    Check(row!=null&&!desc.gameObject.activeSelf&&row.gameObject.activeSelf!=duplicate&&row.GetComponent<Button>()!=null,
                        (duplicate?"Duplicate detailed topic is hidden ":"Detailed question starts collapsed and clickable ")+desc.key);
                    Check(expected.Contains(row.transform.Find("Question").GetComponent<TMP_Text>().text)&&expected.Contains(desc.tMP_Text.text),"Original detailed content remains available "+desc.key);
                }
                int visibleQuestions=0;foreach(var item in page.GetComponentsInChildren<FAQAccordionItem>(true))if(item.gameObject.activeInHierarchy)visibleQuestions++;
                Check(visibleQuestions==8,"Only eight distinct questions are visible");
                Check(scroll.verticalScrollbar.gameObject.activeInHierarchy,"Native scrollbar indicates additional detailed content");
                report.AppendLine("Scrollbar size reflects collapsed FAQ content: "+scroll.verticalScrollbar.size.ToString("F3"));
                await PayPalCapture(folder,"Reference-State.png");
                var firstQuick=content.Find("QuickQuestion1");var firstButton=firstQuick.GetComponent<Button>();
                Check(HitStandard(firstButton),"First FAQ card receives a standard Button raycast");
                PressStandard(firstButton);await UniTask.DelayFrame(3);
                Check(firstQuick.GetComponent<FAQAccordionItem>().IsExpanded&&firstQuick.Find("Answer").gameObject.activeSelf,"Tapping a question reveals its answer");
                Check(firstQuick.Find("Plate").GetComponent<Image>().color!=Color.white&&firstQuick.Find("ActiveAccent").gameObject.activeSelf&&firstQuick.Find("ActiveDivider").gameObject.activeSelf,"Expanded card has a distinct background and accents");
                await PayPalCapture(folder,"Expanded-Answer.png");
                PressStandard(firstButton);await UniTask.DelayFrame(3);
                Check(!firstQuick.GetComponent<FAQAccordionItem>().IsExpanded&&!firstQuick.Find("Answer").gameObject.activeSelf,"Tapping again hides its answer");
                Check(firstQuick.Find("Plate").GetComponent<Image>().color==Color.white&&!firstQuick.Find("ActiveAccent").gameObject.activeSelf,"Collapsed card returns to its neutral background");
                scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-8)});await UniTask.DelayFrame(3);Check(scroll.verticalNormalizedPosition<.999f,"Standard scroll input moves the list");
                scroll.StopMovement();scroll.verticalNormalizedPosition=.40f;await UniTask.DelayFrame(3);await PayPalCapture(folder,"Detailed-Information.png");
                scroll.verticalNormalizedPosition=0;await UniTask.DelayFrame(3);
                var last=content.Find("Detail-FAQPanel_Question7");var lastRect=PayPalRect((RectTransform)last);var viewRect=PayPalRect(scroll.viewport);
                Check(lastRect.yMax<=viewRect.yMax+1&&lastRect.yMax>viewRect.yMin,"The last original question is reachable");
                last.GetComponent<FAQAccordionItem>().Toggle();await UniTask.DelayFrame(3);
                Check(last.GetComponent<FAQAccordionItem>().IsExpanded&&last.GetComponentInChildren<FAQDesc>(true).gameObject.activeSelf,"Original detailed answer expands");
                Check(last.Find("Plate").GetComponent<Image>().color!=Color.white&&last.Find("ActiveAccent").gameObject.activeSelf,"Detailed card also changes its background on expansion");
                scroll.StopMovement();scroll.verticalNormalizedPosition=0;await UniTask.DelayFrame(3);
                await PayPalCapture(folder,"Scrolled-Bottom.png");
                PressStandard(close);await UniTask.Delay(150,ignoreTimeScale:true);inspectedPage=null;
                Check(!UIModule.Instance.PageIsOpen(UIPageIds.QFA),"Close Button dismisses FAQ");Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input returns after closing FAQ");
                page=(FAQPanel)await UIModule.Instance.OpenPage(UIPageIds.QFA);inspectedPage=page;await UniTask.Delay(200,ignoreTimeScale:true);scroll=page.GetComponentInChildren<ScrollRect>();content=scroll.content;
                Check(scroll.verticalNormalizedPosition>.999f,"Reopening resets the previous scroll position");
                foreach(var selection in new[]{("pt-BR","pt_BR","Portuguese.png"),("id-ID","id","Indonesian.png"),("ja-JP","ja","Japanese.png"),("ko-KR","ko","Korean.png")})
                {
                    LanguageUtils.SelectedLanguage=selection.Item1;Localization.SetLocale(selection.Item2);await UniTask.Delay(250,ignoreTimeScale:true);Canvas.ForceUpdateCanvases();scroll.StopMovement();scroll.verticalNormalizedPosition=1;
                    bool values=true,fits=true,glyphs=true;
                    for(int i=1;i<=4;i++)
                    {
                        var row=content.Find("QuickQuestion"+i);foreach(string name in new[]{"Question","Answer"})
                        {
                            var label=row.Find(name).GetComponent<TMP_Text>();label.ForceMeshUpdate();values&=label.text==Localization.Tr("faq_quick_"+(name=="Question"?"question_":"answer_")+i);fits&=!label.isTextOverflowing;
                            foreach(char c in label.text)if(!char.IsWhiteSpace(c))glyphs&=label.font.HasCharacter(c,true,true);
                        }
                    }
                    Check(values,"Live locale refresh updates question text in "+selection.Item2);Check(fits&&glyphs,"Translated questions fit and have font coverage in "+selection.Item2);
                    Check(content.Find("QuickQuestion1/Answer").GetComponent<TMP_Text>().font==AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/Resources/FAQReference20260928/BodyFont.asset"),"Locale refresh preserves the prefab-authored body font in "+selection.Item2);
                    var pageTitle=Array.Find(page.GetComponentsInChildren<TMP_Text>(true),label=>label.name=="FAQTitle");pageTitle.ForceMeshUpdate();Check(pageTitle.text==Localization.Tr("faq_title")&&!pageTitle.isTextOverflowing&&pageTitle.textInfo.lineCount==1,"Localized title stays on one line in "+selection.Item2);
                    bool detailed=true;foreach(var desc in page.GetComponentsInChildren<FAQDesc>(true)){var expected=LanguageUtils.GetText(desc.key).GetReplaceDesc(page.titleColor).GetReplaceDesc(page.contentColor).GetReplaceDesc(page.highlightColor);detailed&=expected.Contains(desc.tMP_Text.text)&&expected.Contains(desc.GetComponentInParent<FAQAccordionItem>(true).transform.Find("Question").GetComponent<TMP_Text>().text);}
                    Check(detailed,"Original detailed text also refreshes in "+selection.Item2);
                    bool unique=true;
                    for(int index=1;index<=7;index++)
                    {
                        bool shouldShow=index!=1&&index!=3&&index!=4;
                        unique&=content.Find("Detail-FAQPanel_Question"+index).gameObject.activeSelf==shouldShow;
                    }
                    Check(unique,"Only unique FAQ topics remain visible in "+selection.Item2);
                    await PayPalCapture(folder,selection.Item3);
                    if(selection.Item2=="pt_BR")
                    {
                        scroll.StopMovement();scroll.verticalNormalizedPosition=0;await UniTask.DelayFrame(3);
                        await PayPalCapture(folder,"Portuguese-Bottom.png");
                    }
                }
                PressStandard(Array.Find(page.GetComponentsInChildren<Button>(true),button=>button.name=="CloseBtn"));await UniTask.Delay(150,ignoreTimeScale:true);inspectedPage=null;
                Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input restores after translated page closes");
                Check(SaveDataUtils.GameData.lastWithdrawTime==lastWithdraw,"FAQ review did not submit or change any withdrawal");
                report.AppendLine("All images are unretouched Unity Play Mode Game view captures from the production FAQ prefab. Eight distinct questions are visible; duplicate detailed source entries remain in the prefab but are hidden.");
            }
            catch(Exception exception){Check(false,exception.ToString());Debug.LogException(exception);}
            finally{CloseRuntime();LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);report.AppendLine("Failures="+failures+" Completed="+DateTime.UtcNow.ToString("O"));File.WriteAllText(folder+"/inspection.txt",report.ToString());}
        }
    }
}
