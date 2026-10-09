using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewRewardPresentationCore(string folder,Action<bool,string> check)
        {
            float coins=ItemUtils.GetItemCount(E_ItemType.Gold),cash=ItemUtils.GetItemCount(E_ItemType.Dollar);
            int videoCount=SaveDataUtils.GameData.todayAdTimes;
            LanguageUtils.SelectedLanguage="pt-BR";Localization.SetLocale("pt_BR");
            async UniTask<GetRewardPanel> OpenView()
            {
                var panel=(GetRewardPanel)await UIModule.Instance.OpenPage(UIPageIds.GetRewardPanel,
                    new ItemEntry{Type=E_ItemType.Gold,Count=1000},new ItemEntry{Type=E_ItemType.Dollar,Count=23.82f},
                    DoubleGetRewardPanel.E_UseScene.DailyTask,(Action<bool>)null);
                inspectedPage=panel;return panel;
            }
            var p=await OpenView();
            float delay=new SerializedObject(p).FindProperty("normalCollectRevealDelay").floatValue;
            check(Mathf.Approximately(delay,3),"Normal collect delay is authored as 3 seconds in the prefab");
            check(!p.closeBtn.gameObject.activeSelf&&!p.closeBtn.GetComponent<Button>().IsActive(),
                "Blue action and its native Button are hidden immediately on opening");
            await UniTask.Delay(800,ignoreTimeScale:true);
            check(HitStandard(p.claimBtn.GetComponent<Button>()),"Ad action retains its original short accidental-click guard");
            check(!p.closeBtn.gameObject.activeSelf,"Blue action remains hidden after the ad action becomes available");
            await PayPalCapture(folder,"Before-3s.png");
            await UniTask.WaitUntil(()=>p.closeBtn.gameObject.activeSelf).Timeout(TimeSpan.FromSeconds(delay+3));
            check(HitStandard(p.closeBtn.GetComponent<Button>()),"Blue action is visible and raycastable after its delay");
            check(p.claimBtn.GetComponent<Button>().onClick.GetPersistentEventCount()==0&&
                p.closeBtn.GetComponent<Button>().onClick.GetPersistentEventCount()==0,"Button events remain code-bound");
            foreach(var path in new[]{"Coins","CashReward/Cash"})
            {
                var material=p.transform.Find(path).GetComponent<Image>().material;
                check(material.shader.name=="UI/Default",
                    "Amount background uses repaired baked artwork without runtime erase material / "+path);
            }
            await PayPalCapture(folder,"After-3s.png");
            CloseRuntime();await UniTask.DelayFrame(3);
            p=await OpenView();
            check(!p.closeBtn.gameObject.activeSelf,"Reopening resets the delay and hides the previously visible button");
            await UniTask.Delay(500,ignoreTimeScale:true);
            CloseRuntime();await UniTask.Delay((int)(delay*1000)+100,ignoreTimeScale:true);
            check(!UIModule.Instance.PageIsOpen(UIPageIds.GetRewardPanel),"Closing during the delay does not reopen the page");
            check(Mathf.Approximately(coins,ItemUtils.GetItemCount(E_ItemType.Gold))&&Mathf.Approximately(cash,ItemUtils.GetItemCount(E_ItemType.Dollar)),
                "View-only review leaves currency balances unchanged");
            check(videoCount==SaveDataUtils.GameData.todayAdTimes,"View-only review leaves ad count unchanged");
        }

        static async UniTask ReviewRewardDisplayCore(string folder,Action<bool,string> check)
        {
            float coins=ItemUtils.GetItemCount(E_ItemType.Gold),cash=ItemUtils.GetItemCount(E_ItemType.Dollar);
            int adCount=SaveDataUtils.GameData.todayAdTimes;
            var p=(GetRewardPanel)await UIModule.Instance.OpenPage(UIPageIds.GetRewardPanel,
                new ItemEntry{Type=E_ItemType.Gold,Count=1000},new ItemEntry{Type=E_ItemType.Dollar,Count=12.60f},
                DoubleGetRewardPanel.E_UseScene.DailyTask,(Action<bool>)null);
            inspectedPage=p;
            await UniTask.Delay(500,ignoreTimeScale:true);
            check(!p.closeBtn.gameObject.activeSelf,"Blue collect action starts hidden");
            await UniTask.Delay(3200,ignoreTimeScale:true);
            Canvas.ForceUpdateCanvases();
            var bounds=PayPalRect((RectTransform)p.transform.Find("Backdrop"));
            check(bounds.xMin<=1&&bounds.xMax>=851&&bounds.yMin<=1&&bounds.yMax>=1845,
                "Reward background covers all four viewport edges / "+Screen.width+"x"+Screen.height+" / "+bounds);
            foreach(var b in new[]{p.claimBtn,p.closeBtn})
            {
                check(HitStandard(b.GetComponent<Button>()),"Native action remains reachable / "+b.name);
                check(b.GetComponent<Button>().onClick.GetPersistentEventCount()==0,"Events remain code-bound / "+b.name);
            }
            foreach(var path in new[]{"Coins","CashReward/Cash"})
            {
                var image=p.transform.Find(path).GetComponent<Image>();
                check(image.preserveAspect&&image.sprite!=null,"Reward icon retains circular proportions / "+path);
                check(image.material.shader.name=="UI/Default","No live erasure material / "+path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(image.sprite));
                check(importer.GetPlatformTextureSettings("Android").format==TextureImporterFormat.ASTC_4x4,
                    "Android reward atlas uses ASTC 4x4 / "+path);
            }
            foreach(var text in p.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();check(!text.isTextOverflowing,"Text fits / "+text.name);
            }
            await PayPalCapture(folder,"Unity-"+Screen.width+"x"+Screen.height+".png");
            CloseRuntime();
            check(coins==ItemUtils.GetItemCount(E_ItemType.Gold)&&cash==ItemUtils.GetItemCount(E_ItemType.Dollar),"Review does not award currency");
            check(adCount==SaveDataUtils.GameData.todayAdTimes,"Review does not change ad count");
        }

        static async UniTask ReviewRewardCore(string folder,Action<bool,string> check)
        {
            // DailyTask is a view-only entry here. WinPanel would report a level and can grant on close.
            float coins=ItemUtils.GetItemCount(E_ItemType.Gold),cash=ItemUtils.GetItemCount(E_ItemType.Dollar);
            int videoCount=SaveDataUtils.GameData.todayAdTimes;
            foreach(var language in new[]{("pt-BR","pt_BR"),("en-US","en"),("id-ID","id"),("zh-CN","zh_CN")})
            {
                LanguageUtils.SelectedLanguage=language.Item1;Localization.SetLocale(language.Item2);
                var p=(GetRewardPanel)await UIModule.Instance.OpenPage(UIPageIds.GetRewardPanel,
                    new ItemEntry{Type=E_ItemType.Gold,Count=1000},new ItemEntry{Type=E_ItemType.Dollar,Count=9.34f},
                    DoubleGetRewardPanel.E_UseScene.DailyTask,(Action<bool>)null);
                inspectedPage=p;
                await UniTask.Delay((int)(new SerializedObject(p).FindProperty("normalCollectRevealDelay").floatValue*1000)+1100,ignoreTimeScale:true);
                var ad=p.GetComponentInChildren<WathAdProgress>(true);
                var real=p.GetComponentInChildren<Real_AdWatchProgress>(true);
                check(ad!=null&&ad.startText&&ad.endText&&ad.hintText&&ad.progressText&&ad.progressBar,
                    "Original video bonus progress and all five bindings restored / "+language.Item2);
                check(real!=null&&real.payCfg&&real.progressImg&&real.paymentImg&&real.hintTxt,
                    "Single-currency withdrawal component and bindings restored / "+language.Item2);
                check(p.MaxDollarTip.activeInHierarchy&&p.MaxDollarTip.GetComponentInChildren<TMP_Text>()!=null,
                    "MAX is visible artwork and text, not an empty placeholder / "+language.Item2);
                check(p.levelTips==p.LevelObj,"Level tip retains original level badge binding");
                check(p.itemBTxt.text.StartsWith(LanguageUtils.GetText("CurrencyToken")),"Reward cash includes its currency / "+language.Item2);
                bool single=Bizza.Sdk.ChannelConfig.Instance.real_CustomConfig.singleCurrencyMode;
                check(p.transform.Find("CashReward").gameObject.activeSelf==!single,"Cash reward honors original currency mode");
                check(new SerializedObject(real.GetComponent<SingleCurrencyMode>()).FindProperty("isReverse").boolValue,
                    "Real withdrawal progress retains original single-currency selection");
                check(!new SerializedObject(p.progress.GetComponent<SingleCurrencyMode>()).FindProperty("isReverse").boolValue,
                    "Amount withdrawal progress retains original dual-currency selection");
                check(single||!real.gameObject.activeInHierarchy,"Inactive currency mode does not overlap withdrawal panels");
                check(!new SerializedObject(p.progress).FindProperty("percentOnly").boolValue,
                    "Withdrawal condition is not replaced by percent only");
                if(p.progress.gameObject.activeInHierarchy)
                {
                    check(!string.IsNullOrEmpty(p.progress.progressTxt.text)&&p.progress.progressTxt.text!=p.progress.percentageText.text,
                        "Withdrawal requirement and percent are displayed separately / "+language.Item2);
                    check(p.progress.progressTxt.text.Contains(LanguageUtils.GetText("CurrencyToken")),
                        "Withdrawal requirement contains a currency amount / "+language.Item2);
                }
                if(!string.IsNullOrEmpty(AccountModule.Instance.OceanShineAppOtherConfigResponse))
                {
                    check(ad.gameObject.activeInHierarchy,"Video bonus visible when production configuration is available");
                    check(ad.progressText.text.StartsWith(videoCount+"/"),"Video count comes from the original saved count");
                    check(ad.startText.text.StartsWith("+")&&ad.endText.text.StartsWith("+"),"Current and next bonus rates both displayed");
                    check(ad.progressBar.fillAmount>=0&&ad.progressBar.fillAmount<=1,"Video progress is finite and clamped");
                }
                check(HitStandard(p.claimBtn.GetComponent<Button>())&&HitStandard(p.closeBtn.GetComponent<Button>()),
                    "Both original reward actions retain reachable native Buttons / "+language.Item2);
                Canvas.ForceUpdateCanvases();
                foreach(var text in p.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();check(!text.isTextOverflowing,"Text fits / "+language.Item2+" / "+text.name);
                }
                await PayPalCapture(folder,language.Item2+".png");
                if(language.Item2=="pt_BR")await PayPalCapture(folder,"Live-State.png");
                CloseRuntime();await UniTask.DelayFrame(4);
            }
            check(!UIModule.Instance.PageIsOpen(UIPageIds.GetRewardPanel),"View-only page closes without reward callback");
            check(Mathf.Approximately(coins,ItemUtils.GetItemCount(E_ItemType.Gold))&&Mathf.Approximately(cash,ItemUtils.GetItemCount(E_ItemType.Dollar)),
                "Review does not grant currency or change balances");
            check(videoCount==SaveDataUtils.GameData.todayAdTimes,"Review does not change ad count");
        }
    }
}
