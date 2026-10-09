using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Record = AccountModule.OceanShineWithdrawalRecord;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async void ReviewHistory()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/HistoryPage-20260928"));
            Directory.CreateDirectory(folder);var report=new StringBuilder();int failures=0;
            string language=LanguageUtils.SelectedLanguage,locale=Localization.CurrentLocale;
            var country=AccountModule.CountryType;long lastWithdraw=SaveDataUtils.GameData.lastWithdrawTime;
            void Check(bool value,string name){report.AppendLine((value?"PASS ":"FAIL ")+name);if(!value)failures++;}
            int Active(WithdrawHistory page){int count=0;foreach(var row in page.Rows)if(row.gameObject.activeSelf)count++;return count;}
            void Records(WithdrawHistory page,List<Record> records)=>page.ApplyResponse(new FailHttpResponse<List<Record>>{success=true,data=records});
            Record Sample(int i)=>new Record{Os_Id=1000+i,Os_Pym="paypal",Os_Ra="player@example.com",Os_Re="player@example.com",Os_Rn="",Os_Cp="",Os_Prc=i==0?2.5:i==1?.25:.10,Os_Dat=new DateTime(2026,9,23).AddDays(-i).ToString("yyyy-MM-dd"),Os_Sts=i==0?1:i==2?4:3,Os_Tsm="Check account details"};
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null)throw new InvalidOperationException("Formal InitWZ startup must finish first.");
                CloseRuntime();var page=(WithdrawHistory)await UIModule.Instance.OpenPage(UIPageIds.WithdrawHistory);inspectedPage=page;
                Check(page.IsRefreshing&&page.transform.Find("LoadingHint").gameObject.activeSelf&&!page.emptyHint.activeSelf,"Loading does not show an empty-history message");
                for(int i=0;i<150&&page.IsRefreshing;i++)await UniTask.Delay(100,ignoreTimeScale:true);
                if(page.IsRefreshing)throw new TimeoutException("Read-only history request has not completed; no fixture may replace a pending response.");
                await UniTask.Delay(250,ignoreTimeScale:true);await PayPalCapture(folder,"Live-CurrentLocale.png");
                Check(!page.IsRefreshing&&!page.transform.Find("LoadingHint").gameObject.activeSelf,"Read-only response ends the loading state");
                LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");AccountModule.CountryType=AccountModule.E_CountryType.US;
                var samples=new List<Record>();for(int i=0;i<24;i++)samples.Add(Sample(i));Records(page,samples);await UniTask.Delay(400,ignoreTimeScale:true);
                var scroll=page.GetComponentInChildren<ScrollRect>();var first=page.Rows[0];
                Check(Active(page)==24,"All 24 fixture records bind without changing account records");
                Check(first.emailTxt.text=="player@example.com"&&first.amountTxt.text=="$2.50"&&first.timeTxt.text=="23 Sep 2026","First row preserves live account, amount and localized date");
                Check(first.processingObj.activeSelf&&!first.successObj.activeSelf&&!first.failObj.activeSelf,"Processing record shows exactly its status");
                Check(page.Rows[1].successObj.activeSelf&&!page.Rows[1].processingObj.activeSelf&&!page.Rows[1].failObj.activeSelf,"Completed record shows exactly its status");
                Check(page.Rows[2].failObj.activeSelf&&page.Rows[2].dueText.text=="Check account details"&&page.Rows[2].dueText.gameObject.activeInHierarchy,"Failed record shows status and server reason");
                Check(!first.nameTxt.gameObject.activeSelf&&!first.cpfTxt.gameObject.activeSelf,"Unavailable identity fields do not leave gaps");
                Check(ReleaseArtInspection.NamedSprite(first.withdrawImg.sprite,"PayPal"),"Channel uses the configured PayPal artwork");
                foreach(var pair in new[]{("BG (2)",new Rect(39,201,776,1547)),("HistoryTitle",new Rect(148,113,559,69)),("CloseBtn",new Rect(724,68,86,87))})
                {
                    Rect r=PayPalRect((RectTransform)page.transform.Find(pair.Item1)),e=pair.Item2;
                    float difference=Mathf.Max(Mathf.Abs(r.x-e.x),Mathf.Abs(r.y-e.y),Mathf.Abs(r.width-e.width),Mathf.Abs(r.height-e.height));
                    Check(difference<1,"Reference geometry "+pair.Item1+" "+r+" difference="+difference.ToString("F2"));
                }
                for(int i=0;i<3;i++)
                {
                    var row=page.Rows[i];Rect r=PayPalRect((RectTransform)row.transform);report.AppendLine("ROW "+i+" "+r);
                    row.emailTxt.ForceMeshUpdate();row.timeTxt.ForceMeshUpdate();row.amountTxt.ForceMeshUpdate();
                    Check(!row.emailTxt.isTextOverflowing&&!row.timeTxt.isTextOverflowing&&!row.amountTxt.isTextOverflowing,"Primary live fields fit row "+i);
                }
                Check(scroll.vertical&&scroll.verticalScrollbar!=null&&scroll.verticalScrollbar.gameObject.activeInHierarchy,"Native scroll and scrollbar enabled for multiple records");
                Check(scroll.viewport.GetComponent<RectMask2D>()!=null,"Record list is clipped by a native UI viewport");
                var close=page.transform.Find("CloseBtn").GetComponentInChildren<Button>();Check(HitStandard(close),"Visible close Button receives the native raycast");
                await PayPalCapture(folder,"Reference-State.png");
                scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-8)});await UniTask.DelayFrame(3);
                Check(scroll.verticalNormalizedPosition<.999f,"Standard scroll input moves the list");
                scroll.StopMovement();scroll.verticalNormalizedPosition=0;await UniTask.DelayFrame(3);
                var lastRect=PayPalRect((RectTransform)page.Rows[23].transform);var viewportRect=PayPalRect(scroll.viewport);
                Check(lastRect.yMax<=viewportRect.yMax+2&&lastRect.yMin<viewportRect.yMax,"Last record is reachable without clipping its bottom");
                await PayPalCapture(folder,"Scrolled-Bottom.png");

                Records(page,new List<Record>());await UniTask.DelayFrame(4);
                Check(Active(page)==0&&page.emptyHint.activeSelf&&!page.transform.Find("ErrorHint").gameObject.activeSelf,"Successful empty response hides all pooled records and shows only empty state");
                Check(!scroll.verticalScrollbar.gameObject.activeSelf,"Empty list hides the scrollbar");await PayPalCapture(folder,"Empty.png");
                page.ApplyResponse(new FailHttpResponse<List<Record>>{success=false});await UniTask.DelayFrame(4);
                Check(Active(page)==0&&!page.emptyHint.activeSelf&&page.transform.Find("ErrorHint").gameObject.activeSelf,"Request failure is not misreported as no records");await PayPalCapture(folder,"Load-Error.png");

                LanguageUtils.SelectedLanguage="pt-BR";Localization.SetLocale("pt_BR");AccountModule.CountryType=AccountModule.E_CountryType.BR;
                var longRecord=Sample(0);longRecord.Os_Pym="pix";longRecord.Os_Ra="000.000.000-00";longRecord.Os_Re="long.player.account@example.com";longRecord.Os_Rn="Conta de teste";longRecord.Os_Cp="000.000.000-00";longRecord.Os_Sts=4;
                longRecord.Os_Tsm="Confira os dados da conta e o nome do titular. A solicitação não pôde ser concluída porque os dados informados não correspondem à conta selecionada.";
                var small=new List<Record>{longRecord,Sample(1)};Records(page,small);await UniTask.Delay(300,ignoreTimeScale:true);
                Check(Active(page)==2&&page.Rows[0]==first,"Refreshing a shorter list reuses rows and hides the remainder");
                Check(first.nameTxt.gameObject.activeInHierarchy&&first.cpfTxt.gameObject.activeInHierarchy&&first.emailTxt.text==longRecord.Os_Re,"Brazilian name, identity and email remain available");
                Check(first.withdrawImg.sprite!=null&&first.withdrawImg.sprite.name!="PayPal","PIX logo replaces PayPal through the existing payment configuration");
                first.dueText.ForceMeshUpdate();var reasonRect=PayPalRect((RectTransform)first.transform.Find("ReasonSection"));var rowRect=PayPalRect((RectTransform)first.transform);var nextRect=PayPalRect((RectTransform)page.Rows[1].transform);
                Check(first.dueText.text==longRecord.Os_Tsm&&!first.dueText.isTextOverflowing,"Full long failure reason wraps without truncation");
                Check(reasonRect.yMax<=rowRect.yMax+1&&nextRect.yMin>rowRect.yMax,"Long reason expands its card without overlapping the next record");
                Check(first.timeTxt.text=="23 set 2026"||first.timeTxt.text=="23 set. 2026","Date follows Portuguese locale");
                Check(page.transform.Find("HistoryTitle").GetComponent<TMPro.TMP_Text>().text=="Histórico de saques","History title follows Portuguese locale");
                await PayPalCapture(folder,"PIX-Long-Reason.png");
                AccountModule.CountryType=AccountModule.E_CountryType.ID;LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                longRecord.Os_Pym="dana";longRecord.Os_Ra="081234567890";longRecord.Os_Dat="2026-09-23 15:42:03";longRecord.Os_Tsm="";longRecord.Os_Sts=2;Records(page,new List<Record>{longRecord});await UniTask.DelayFrame(4);
                Check(first.emailTxt.text==longRecord.Os_Ra&&first.nameTxt.gameObject.activeSelf&&!first.cpfTxt.gameObject.activeSelf,"Indonesian account and name remain visible without CPF");
                Check(first.timeTxt.text==longRecord.Os_Dat,"Unrecognized server date format is preserved");
                Check(first.failObj.activeSelf&&first.dueText.text==Localization.Tr("history_reason_unavailable"),"Rejected record retains failed state and missing-reason message");
                Check(first.withdrawImg.sprite!=null,"DANA payment logo resolves");await PayPalCapture(folder,"DANA-Rejected.png");
                PressStandard(close);await UniTask.Delay(200,ignoreTimeScale:true);inspectedPage=null;
                Check(!UIModule.Instance.PageIsOpen(UIPageIds.WithdrawHistory),"Native close dismisses the history page");
                Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input restores after closing");
                Check(SaveDataUtils.GameData.lastWithdrawTime==lastWithdraw,"No withdrawal request or saved withdrawal timestamp was changed");
                report.AppendLine("All PNGs are raw Unity Play Mode Game view captures. Live-CurrentLocale uses the real read-only history response. Other captures use explicitly local UI fixture records through the production response/data-binding path. No account records, balances, or withdrawal requests were modified.");
            }
            catch(Exception exception){Check(false,exception.ToString());Debug.LogException(exception);}
            finally
            {
                CloseRuntime();LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);AccountModule.CountryType=country;
                report.AppendLine("Failures="+failures+" Completed="+DateTime.UtcNow.ToString("O"));File.WriteAllText(folder+"/inspection.txt",report.ToString());
            }
        }
    }
}
