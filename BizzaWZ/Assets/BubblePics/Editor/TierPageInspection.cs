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
        static Rect TierRect(RectTransform tr)
        {
            var r=PayPalRect(tr);return new Rect(r.x*853/852,r.y*1844/1846,r.width*853/852,r.height*1844/1846);
        }
        static async void ReviewTier()
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation/TierPage-20260928"));Directory.CreateDirectory(folder);
            var report=new StringBuilder();int failures=0;string language=LanguageUtils.SelectedLanguage,locale=Localization.CurrentLocale;
            string claims=JsonUtility.ToJson(SaveDataUtils.WithDrawDanPanelData);float money=ItemUtils.GetItemCount(E_ItemType.WithDrawDanDollar);long lastWithdraw=SaveDataUtils.GameData.lastWithdrawTime;
            void Check(bool pass,string name){report.AppendLine((pass?"PASS ":"FAIL ")+name);if(!pass)failures++;}
            try
            {
                if(!EditorApplication.isPlaying||BizzaGameplayBridge.Page==null)throw new InvalidOperationException("Wait for production InitWZ startup.");
                CloseRuntime();LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");
                var page=(WithdrawDanPanel)await UIModule.Instance.OpenPage(UIPageIds.WithdrawDanPanel);inspectedPage=page;await UniTask.Delay(350,ignoreTimeScale:true);
                var scroll=page.GetComponentInChildren<ScrollRect>();var rows=page.root.GetComponentsInChildren<WithdrawDanItem>();
                Check(rows.Length==7,"All seven configured tiers are instantiated");
                Check(scroll.vertical&&!scroll.horizontal&&scroll.viewport.GetComponent<RectMask2D>()!=null,"Native vertical ScrollRect clips tier cards");
                Check(scroll.verticalNormalizedPosition>.999f,"Tier list opens at the first row");
                foreach(var b in new[]{page.closeBtn,page.historiyBtn,page.faqBtn,page.withdrawBtn})Check(HitStandard(b.GetComponent<Button>()),"Native Button receives raycast: "+b.name);
                foreach(var pair in new[]{("BalanceCard",new Rect(69,191,715,200)),("TierScroll",new Rect(63,402,727,978)),("Withdraw",new Rect(71,1634,712,115)),("TargetProgress",new Rect(80,1525,621,42))})
                {
                    var r=TierRect((RectTransform)page.transform.Find(pair.Item1));var e=pair.Item2;float d=Mathf.Max(Mathf.Abs(r.x-e.x),Mathf.Abs(r.y-e.y),Mathf.Abs(r.width-e.width),Mathf.Abs(r.height-e.height));Check(d<1,"Authored geometry "+pair.Item1+" "+r+" delta="+d.ToString("F2"));
                }
                var levels=AccountModule.CountryType==AccountModule.E_CountryType.ID?page.danLevelsID:page.danLevelsUS;
                for(int i=0;i<rows.Length;i++)
                {
                    var row=rows[i];string expected=WithdrawDanPanel.FormatTierMoney(ItemUtils.FormatCountFloat(new ItemEntry{Type=E_ItemType.WithDrawDanDollar,Count=levels[i].withdrawMoney}));
                    Check(row.moneyText1.text==expected&&row.hintText.text==string.Format(Localization.Tr("tier_level"),levels[i].missions[0].conditionData.targetValue),"Live reward and level use original configuration for tier "+(i+1));
                    Check((row.prepareStateObj.activeSelf?1:0)+(row.claimStateObj.activeSelf?1:0)+(row.claimedStateObj.activeSelf?1:0)==1,"Exactly one reward state visible for tier "+(i+1));
                    row.moneyText1.ForceMeshUpdate();row.hintText.ForceMeshUpdate();Check(!row.moneyText1.isTextOverflowing&&!row.hintText.isTextOverflowing,"Live amount and level fit tier "+(i+1));
                }
                Check(page.balanceTxt.text==WithdrawDanPanel.FormatTierMoney(money),"Live balance is formatted to two decimals");
                await PayPalCapture(folder,"Live-State.png");
                var balanceRect=TierRect(page.balanceTxt.rectTransform);var withdrawRect=TierRect((RectTransform)page.withdrawBtn.transform);
                scroll.OnScroll(new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-6)});await UniTask.DelayFrame(4);Check(scroll.verticalNormalizedPosition<.999f,"Standard scroll input moves the tier list");
                scroll.StopMovement();scroll.verticalNormalizedPosition=0;await UniTask.DelayFrame(4);
                var last=TierRect((RectTransform)rows[6].transform);var viewport=TierRect(scroll.viewport);Check(last.yMax<=viewport.yMax+1&&last.yMax>viewport.yMin,"Seventh tier is reachable in full");
                Check(TierRect(page.balanceTxt.rectTransform)==balanceRect&&TierRect((RectTransform)page.withdrawBtn.transform)==withdrawRect,"Balance and withdrawal action stay fixed while scrolling");await PayPalCapture(folder,"All-Seven-Tiers.png");
                scroll.verticalNormalizedPosition=1;await UniTask.DelayFrame(3);
                var mission=page.withdrawMissionSO.GetMissionByStateSafe(SaveDataUtils.WithDrawDanPanelData.curStageIndex);
                if(mission!=null&&!mission.IsCanWithdraw())
                {
                    PressStandard(page.withdrawBtn.GetComponent<Button>());await UniTask.Delay(100,ignoreTimeScale:true);
                    Check(JsonUtility.ToJson(SaveDataUtils.WithDrawDanPanelData)==claims&&ItemUtils.GetItemCount(E_ItemType.WithDrawDanDollar)==money,"Ineligible native withdrawal click does not advance or deduct rewards");
                }
                PressStandard(page.faqBtn.GetComponent<Button>());for(int i=0;i<50&&!UIModule.Instance.PageIsOpen(UIPageIds.QFA);i++)await UniTask.Delay(100,ignoreTimeScale:true);Check(UIModule.Instance.PageIsOpen(UIPageIds.QFA),"FAQ Button opens production FAQ");
                var faq=UIModule.Instance.GetPage<FAQPanel>();PressStandard(faq.transform.Find("CloseBtn").GetComponent<Button>());await UniTask.Delay(200,ignoreTimeScale:true);Check(!UIModule.Instance.PageIsOpen(UIPageIds.QFA),"FAQ close returns to tier page");
                PressStandard(page.historiyBtn.GetComponent<Button>());for(int i=0;i<100&&!UIModule.Instance.PageIsOpen(UIPageIds.WithdrawHistory);i++)await UniTask.Delay(100,ignoreTimeScale:true);Check(UIModule.Instance.PageIsOpen(UIPageIds.WithdrawHistory),"History Button opens production History");
                var history=UIModule.Instance.GetPage<WithdrawHistory>();PressStandard(history.transform.Find("CloseBtn").GetComponentInChildren<Button>());await UniTask.Delay(200,ignoreTimeScale:true);Check(!UIModule.Instance.PageIsOpen(UIPageIds.WithdrawHistory),"History close returns to tier page");
                // View-only fixtures use the same Init/data-binding methods. They never mutate save data or call reward/withdraw handlers.
                rows[0].Init(0,.5f,page.danSprites[0],"Bronze","Level 10","24",24,10,true,true,page);
                rows[1].Init(1,1f,page.danSprites[1],"Silver","Level 20","24",24,20,false,true,page);
                rows[2].Init(2,2f,page.danSprites[2],"Gold","Level 30","24",24,30,false,false,page);
                page.PresentBalance(1.5f);page.PresentTarget(new WithdrawMissionData{conditionData=new WithdrawConditionData{condition=E_WithdrawCondition.WithDrawDanDollar,targetValue=2}},1.5f);page.progressImg.fillAmount=.75f;page.progressTxt.text="75%";
                await UniTask.Delay(3500,ignoreTimeScale:true);
                Check(rows[0].claimedStateObj.activeSelf&&!rows[0].claimStateObj.activeSelf&&!rows[0].prepareStateObj.activeSelf,"Reference Bronze uses the claimed state only");
                Check(rows[1].claimStateObj.activeSelf&&HitStandard(rows[1].claimStateBtn.GetComponent<Button>()),"Reference Silver exposes its native claim Button");
                Check(rows[2].prepareStateObj.activeSelf&&HitStandard(rows[2].prepareStateBtn.GetComponent<Button>()),"Reference Gold exposes its native keep-playing Button");
                Check(rows[0].progressText.text=="10 / 10"&&rows[1].progressText.text=="20 / 20"&&rows[2].progressText.text=="24 / 30","Progress counts clamp at each tier target");
                Check(Mathf.Approximately(rows[2].progressImage.fillAmount,.8f)&&Mathf.Approximately(page.progressImg.fillAmount,.75f),"Progress lengths reflect 24/30 and 75% exactly");
                for(int i=0;i<3;i++){var r=TierRect((RectTransform)rows[i].transform);Check(Mathf.Abs(r.x-69)<1&&Mathf.Abs(r.y-(409+328.5f*i))<1&&Mathf.Abs(r.height-308)<1,"Reference tier card geometry "+(i+1)+" "+r);}
                await PayPalCapture(folder,"Reference-State.png");
                rows[2].Init(2,2,page.danSprites[2],"Gold","Level 0","0",0,0,false,false,page);Check(!float.IsNaN(rows[2].progressImage.fillAmount)&&rows[2].progressImage.fillAmount==0,"Zero target does not divide by zero");
                rows[2].Init(2,2,page.danSprites[2],"Gold","Level 30","0",0,30,true,false,page);Check(rows[2].claimedStateObj.activeSelf&&!rows[2].prepareStateObj.activeSelf,"Claimed state remains exclusive if progress data falls below its target");
                foreach(var selection in new[]{("pt-BR","pt_BR","Portuguese.png"),("id-ID","id","Indonesian.png"),("zh-CN","zh_CN","Chinese.png")})
                {
                    LanguageUtils.SelectedLanguage=selection.Item1;Localization.SetLocale(selection.Item2);await UniTask.Delay(250,ignoreTimeScale:true);Canvas.ForceUpdateCanvases();scroll.StopMovement();scroll.verticalNormalizedPosition=1;
                    bool fits=true,names=true,glyphs=true;for(int i=0;i<7;i++){names&=rows[i].danText.text==Localization.Tr("tier_name_"+(i+1));foreach(var label in new[]{rows[i].danText,rows[i].hintText,rows[i].moneyText1}){label.ForceMeshUpdate();fits&=!label.isTextOverflowing;foreach(char c in label.text)if(!char.IsWhiteSpace(c))glyphs&=label.font.HasCharacter(c,true,true);}}
                    Check(names&&fits,"All seven native tier names and values localize without clipping: "+selection.Item2);
                    var title=page.transform.Find("Title").GetComponent<TMP_Text>();title.ForceMeshUpdate();Check(title.text==Localization.Tr("tier_title")&&!title.isTextOverflowing&&title.GetComponent<CanvasGroup>().alpha==(Localization.CurrentLocale=="en"?0:1),"Localized title replaces approved English caption: "+selection.Item2);
                    page.hintTxt.ForceMeshUpdate();foreach(char c in page.hintTxt.text)if(!char.IsWhiteSpace(c))glyphs&=page.hintTxt.font.HasCharacter(c,true,true);Check(glyphs,"Localized tier text and currency glyphs are covered: "+selection.Item2);Check(!page.hintTxt.isTextOverflowing,"Live target instruction fits: "+selection.Item2);await PayPalCapture(folder,selection.Item3);
                }
                LanguageUtils.SelectedLanguage="en-US";Localization.SetLocale("en");await UniTask.DelayFrame(4);scroll.verticalNormalizedPosition=0;
                PressStandard(page.closeBtn.GetComponent<Button>());await UniTask.Delay(150,ignoreTimeScale:true);inspectedPage=null;Check(!UIModule.Instance.PageIsOpen(UIPageIds.WithdrawDanPanel),"Back Button closes the tier page");Check(BizzaGameplayBridge.Page.Input.InputEnabled&&!BizzaGameplayBridge.IsInputBlocked,"Gameplay input restores after closing");
                page=(WithdrawDanPanel)await UIModule.Instance.OpenPage(UIPageIds.WithdrawDanPanel);inspectedPage=page;await UniTask.Delay(250,ignoreTimeScale:true);Check(page.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition>.999f,"Reopening resets list to its first tier");
                Check(page.balanceTxt.text==WithdrawDanPanel.FormatTierMoney(money),"Reopening clears presentation fixture and uses live balance");
                Check(JsonUtility.ToJson(SaveDataUtils.WithDrawDanPanelData)==claims&&ItemUtils.GetItemCount(E_ItemType.WithDrawDanDollar)==money&&SaveDataUtils.GameData.lastWithdrawTime==lastWithdraw,"Inspection leaves reward state, balances and withdrawal history unchanged");
                report.AppendLine("Reference-State is an unretouched Unity Game view capture with presentation-only sample values matching the art. Live-State and translations use production configuration. No eligible claim, deduction, payout, save-data override or synthetic success response was executed. Native claim controls and state transitions were inspected; financial submission was not tested.");
            }
            catch(Exception e){Check(false,e.ToString());Debug.LogException(e);}
            finally{UIModule.Instance.ClosePage(UIPageIds.WithdrawHistory);UIModule.Instance.ClosePage(UIPageIds.QFA);CloseRuntime();LanguageUtils.SelectedLanguage=language;Localization.SetLocale(locale);report.AppendLine("Failures="+failures+" Completed="+DateTime.UtcNow.ToString("O"));File.WriteAllText(folder+"/inspection.txt",report.ToString());}
        }
    }
}
