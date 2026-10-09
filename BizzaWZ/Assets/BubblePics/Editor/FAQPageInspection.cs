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
            long lastWithdraw=SaveDataUtils.GameData.lastWithdrawTime;
            void Check(bool pass,string name){report.AppendLine((pass?"PASS ":"FAIL ")+name);if(!pass)failures++;}
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null)throw new InvalidOperationException("Formal InitWZ startup must finish first.");
                CloseRuntime();LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                var page=(FAQPanel)await UIModule.Instance.OpenPage(UIPageIds.QFA);inspectedPage=page;await UniTask.Delay(350,ignoreTimeScale:true);
                var scroll=page.GetComponentInChildren<ScrollRect>();var content=scroll.content;var close=page.transform.Find("CloseBtn").GetComponent<Button>();
                Check(HitStandard(close),"Visible close control receives the standard Button raycast");
                Check(scroll.vertical&&!scroll.horizontal&&scroll.viewport.GetComponent<RectMask2D>()!=null,"Native vertical scrolling and viewport clipping are configured");
                Check(scroll.verticalNormalizedPosition>.999f,"Page opens at the first question");
                foreach(var pair in new[]{("BG (2)",new Rect(37,213,780,1533)),("ReferenceTitle",new Rect(341,102,175,85)),("CloseBtn",new Rect(701,91,103,104))})
                {
                    Rect r=PayPalRect((RectTransform)page.transform.Find(pair.Item1)),e=pair.Item2;float difference=Mathf.Max(Mathf.Abs(r.x-e.x),Mathf.Abs(r.y-e.y),Mathf.Abs(r.width-e.width),Mathf.Abs(r.height-e.height));
                    Check(difference<1,"Reference geometry "+pair.Item1+" "+r+" difference="+difference.ToString("F2"));
                }
                for(int i=0;i<4;i++)
                {
                    var row=content.Find("QuickQuestion"+(i+1));var question=row.Find("Question").GetComponent<TMP_Text>();var answer=row.Find("Answer").GetComponent<TMP_Text>();
                    Check(question.text==FAQReferenceAuthoring.Questions[i]&&answer.text==FAQReferenceAuthoring.Answers[i],"Approved question and answer remain available as localized text "+(i+1));
                    question.ForceMeshUpdate();answer.ForceMeshUpdate();Check(!question.isTextOverflowing&&!answer.isTextOverflowing,"Quick question and answer fit their card "+(i+1));
                    Check(question.GetComponent<CanvasGroup>().alpha==0&&ReleaseArtInspection.Caption(row.Find("ReferenceQuestion"+(i+1)).GetComponent<Image>(),true),"English caption uses its authored visual "+(i+1));
                    var r=PayPalRect((RectTransform)row);Check(Mathf.Abs(r.x-78)<1&&Mathf.Abs(r.y-(304+326*i))<1&&Mathf.Abs(r.height-244)<1,"Question card geometry "+(i+1)+" "+r);
                }
                var original=page.GetComponentsInChildren<FAQDesc>(true);Check(original.Length==7,"All seven original detailed FAQ entries are retained");
                foreach(var desc in original)
                {
                    var expected=LanguageUtils.GetText(desc.key).GetReplaceDesc(page.titleColor).GetReplaceDesc(page.contentColor).GetReplaceDesc(page.highlightColor);
                    Check(desc.tMP_Text.text==expected,"Original detailed content preserved "+desc.key);
                    desc.tMP_Text.ForceMeshUpdate();Check(!desc.tMP_Text.isTextOverflowing,"Detailed paragraph expands without truncation "+desc.key);
                }
                Check(scroll.verticalScrollbar.gameObject.activeInHierarchy,"Native scrollbar indicates additional detailed content");
                report.AppendLine("Scrollbar size reflects actual retained content: "+scroll.verticalScrollbar.size.ToString("F3")+". It is not a decorative fixed-size bar.");
                await PayPalCapture(folder,"Reference-State.png");
                scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-8)});await UniTask.DelayFrame(3);Check(scroll.verticalNormalizedPosition<.999f,"Standard scroll input moves the list");
                scroll.StopMovement();scroll.verticalNormalizedPosition=.40f;await UniTask.DelayFrame(3);await PayPalCapture(folder,"Detailed-Information.png");
                scroll.verticalNormalizedPosition=0;await UniTask.DelayFrame(3);
                var last=content.Find("Detail-FAQPanel_Question7");var lastRect=PayPalRect((RectTransform)last);var viewRect=PayPalRect(scroll.viewport);
                Check(lastRect.yMax<=viewRect.yMax+1&&lastRect.yMax>viewRect.yMin,"The last original question is reachable and fully visible");await PayPalCapture(folder,"Scrolled-Bottom.png");
                PressStandard(close);await UniTask.Delay(150,ignoreTimeScale:true);inspectedPage=null;
                Check(!UIModule.Instance.PageIsOpen(UIPageIds.QFA),"Close Button dismisses FAQ");Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input returns after closing FAQ");
                page=(FAQPanel)await UIModule.Instance.OpenPage(UIPageIds.QFA);inspectedPage=page;await UniTask.Delay(200,ignoreTimeScale:true);scroll=page.GetComponentInChildren<ScrollRect>();content=scroll.content;
                Check(scroll.verticalNormalizedPosition>.999f,"Reopening resets the previous scroll position");
                foreach(var selection in new[]{("pt-BR","pt_BR","Portuguese.png"),("id-ID","id","Indonesian.png"),("ja-JP","ja","Japanese.png"),("ko-KR","ko","Korean.png")})
                {
                    LanguageUtils.SelectedLanguage=selection.Item1;Localization.SetLocale(selection.Item2);await UniTask.Delay(250,ignoreTimeScale:true);Canvas.ForceUpdateCanvases();scroll.StopMovement();scroll.verticalNormalizedPosition=1;
                    bool values=true,fits=true,glyphs=true,captions=true;
                    for(int i=1;i<=4;i++)
                    {
                        var row=content.Find("QuickQuestion"+i);foreach(string name in new[]{"Question","Answer"})
                        {
                            var label=row.Find(name).GetComponent<TMP_Text>();label.ForceMeshUpdate();values&=label.text==Localization.Tr("faq_quick_"+(name=="Question"?"question_":"answer_")+i);fits&=!label.isTextOverflowing;
                            foreach(char c in label.text)if(!char.IsWhiteSpace(c))glyphs&=label.font.HasCharacter(c,true,true);
                            bool authored=label.text==(name=="Question"?FAQReferenceAuthoring.Questions[i-1]:FAQReferenceAuthoring.Answers[i-1]);
                            captions&=label.GetComponent<CanvasGroup>().alpha==(authored?0:1)&&ReleaseArtInspection.Caption(row.Find("Reference"+name+i).GetComponent<Image>(),authored);
                        }
                    }
                    Check(values&&captions,"Live locale refresh replaces reference captions in "+selection.Item2);Check(fits&&glyphs,"Translated questions fit and have font coverage in "+selection.Item2);
                    Check(content.Find("QuickQuestion1/Answer").GetComponent<TMP_Text>().font==AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BubblePics/Resources/FAQReference20260928/BodyFont.asset"),"Locale refresh preserves the prefab-authored body font in "+selection.Item2);
                    var pageTitle=page.transform.Find("FAQTitle").GetComponent<TMP_Text>();pageTitle.ForceMeshUpdate();Check(pageTitle.text==Localization.Tr("faq_title")&&!pageTitle.isTextOverflowing&&pageTitle.textInfo.lineCount==1,"Localized title stays on one line in "+selection.Item2);
                    bool detailed=true;foreach(var desc in page.GetComponentsInChildren<FAQDesc>())detailed&=desc.tMP_Text.text==LanguageUtils.GetText(desc.key).GetReplaceDesc(page.titleColor).GetReplaceDesc(page.contentColor).GetReplaceDesc(page.highlightColor);
                    Check(detailed,"Original detailed text also refreshes in "+selection.Item2);await PayPalCapture(folder,selection.Item3);
                }
                PressStandard(page.transform.Find("CloseBtn").GetComponent<Button>());await UniTask.Delay(150,ignoreTimeScale:true);inspectedPage=null;
                Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input restores after translated page closes");
                Check(SaveDataUtils.GameData.lastWithdrawTime==lastWithdraw,"FAQ review did not submit or change any withdrawal");
                report.AppendLine("All images are unretouched Unity Play Mode Game view captures from the production FAQ prefab. No fixture balance, financial request or simulated response was used. First four FAQ summaries match the approved design; all seven original detailed entries remain below them.");
            }
            catch(Exception exception){Check(false,exception.ToString());Debug.LogException(exception);}
            finally{CloseRuntime();LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);report.AppendLine("Failures="+failures+" Completed="+DateTime.UtcNow.ToString("O"));File.WriteAllText(folder+"/inspection.txt",report.ToString());}
        }
    }
}
