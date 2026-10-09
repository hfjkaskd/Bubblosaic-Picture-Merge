#if BIZZA_REAL_WITHDRAW
using System;
using System.Collections;
using System.Collections.Generic;
using Bizza.Sdk;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public partial class UIPageIds
{
    public static readonly PageId DailyMissionPanel = "DailyMissionPanel";
}

 
public class DailyMissionPanel : UIPageBase
{
    public TMP_Text hintsTxt;
    [SerializeField] private TMP_Text rewardAmountText;
    [SerializeField] private TMP_Text taskProgressText;
    [SerializeField] private Image taskProgressFill;
    public TMP_Text refreshTimeTxt;
    // public TMP_Text adsCountTxt;

    public TMP_Text claimedHint;

    public GameObject GoObj;
    public GameObject WithdrawObj;
    public GameObject ClaimedObj;


    private List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform> plats = new();

    public static bool isTestWithdraw = false;
    private float ecpmLimit = 1.5f;

    private void SetLinster(bool enable)
    {
        BizzaEventSystem.Set(EventDefine.WithDraw.RefreshDailyMissionPage, OnrequestDailyMission, enable);
        BizzaEventSystem.Set(EventDefine.WithDraw.RefreshDailyMissionPageFalse, OnRequestDailyMission, enable);
    }


     [Header("按钮")]
    [SerializeField] private BizzaButton closeBtn;
    [SerializeField] private BizzaButton goBtn;
    [SerializeField] private BizzaButton withdrawBtn;
    [SerializeField] private BizzaButton claimedBtn;
    protected override void OnAwake()
    {
        closeBtn.onClick.AddListener(OnClickClose);
        goBtn.onClick.AddListener(OnClickGoStateBtn);
        claimedBtn.onClick.AddListener(OnClickWithdrawedStateBtn);
        withdrawBtn.onClick.AddListener(OnClickWithdrawStateBtn);
    }
    
    protected override void OnOpen()
    {
        OnRequestData();
        SetLinster(true);
    }

    public void OnRequestData()
    {
        // AccountModule.Instance.
        AccountModule.Instance.Request_RoutineTaskLookAdMoneyRequest(true, OnResultCallback, false);
        AccountModule.Instance.Request_WithdrawalPageRequest(Refresh); // 刷新平台
    }

    public void OnrequestDailyMission()
    {
        AccountModule.Instance.Request_RoutineTaskLookAdMoneyRequest(true, OnResultCallback, true);
    }

    public void OnRequestDailyMission()
    {
        AccountModule.Instance.Request_RoutineTaskLookAdMoneyRequest(false, OnResultCallback, true);
    }

    private string hintTxt;
    private string countTxt;
    private void OnResultCallback(FailHttpResponse<List<AccountModule.RoutineTaskLookAdMoneyResponse>> responses)
    {
        if (!this) return;

        if (responses.success && responses.data != null && responses.data.Count > 0)
        {
            int cur = SaveDataUtils.GameData.userLookDailyAdCount;
            int max = SaveDataUtils.GameData.userLookDailyAdCountMax;

            SaveDataUtils.GameData.userLookDailyAdCount = Math.Clamp(cur, 0, max);
            var data = responses.data[0];
            hintTxt = 
                LanguageUtils.GetFormatText(
                    "DailyWithdrawMissionPanel_Hint",
                    data.Os_An,
                    $"{LanguageUtils.GetText("CurrencyToken") + WithdrawalUtil.GetCustomizedValueByCountryType((float)data.Os_My)}"
                );
            countTxt = $" (<color=#9039D8>{cur}/{max}</color>)";
             hintsTxt.text = hintTxt + countTxt;
            if(rewardAmountText!=null)rewardAmountText.text=LanguageUtils.GetText("CurrencyToken")+WithdrawalUtil.GetCustomizedValueByCountryType((float)data.Os_My);
            RefreshProgress(cur,max);

            LogLogger.LogVerbose(LogTag.DailyAD, $"每日任务界面刷新 ： " +
                                             $"{cur}/{max}");
            // adsCountTxt.text = $"{cur}/{max}";

            LogLogger.LogVerbose(LogTag.DailyAD, "data.Os_Ss " + data.Os_Ss);
            GoObj.SetActive(data.Os_Ss == 1 || data.Os_Ss == 0);
            WithdrawObj.SetActive(data.Os_Ss == 2);
            ClaimedObj.SetActive(data.Os_Ss == 3);
            claimedHint.gameObject.SetActive(data.Os_Ss == 3);
        }
        else
        {
            UIModule.Instance.ClosePage(UIPageIds.DailyMissionPanel);
            LogLogger.LogVerbose(LogTag.DailyAD, $"获取服务器数据 OceanShineRoutineTaskLookAdMoneyResponse 失败 {responses}");
        }
    }

    private float timer;
    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= 1f) // 每秒更新一次
        {
            timer = 0f;
            UpdateRemainingTime();
        }
    }
    private void RefreshProgress(int current,int total)
    {
        if(taskProgressText!=null)taskProgressText.text=current+" / "+total;
        if(taskProgressFill!=null)taskProgressFill.fillAmount=total>0?Mathf.Clamp01((float)current/total):0;
    }

    private void UpdateRemainingTime()
    {
        DateTime now = DateTime.Now;
        DateTime tomorrow = now.Date.AddDays(1); // 明天 00:00
        TimeSpan remain = tomorrow - now;
        refreshTimeTxt.text = string.Format(BubblePics.Localization.Tr("seq_daily_reset"), $"{remain.Hours:D2}:{remain.Minutes:D2}:{remain.Seconds:D2}");
    }

    public void OnClickGoStateBtn()
    {
        BizzaSdk.Ad.ShowRewardAd(nameof(E_AdPos.DailyMission), 0, OnGoResponse, ecpmLimit);
        UIModule.Instance.m_curadvertistics--;
        SaveDataUtils.GameData.btnDailyTaskClick++;
    }

    private void OnGoResponse(Bizza.Sdk.ShowAdResult showAdResult)
    {
         bool success = showAdResult.success;
        if (success)
        {
            LogLogger.LogVerbose(LogTag.DailyAD, "用户点击了观看每日任务");
            SaveDataUtils.GameData.userLookDailyAdCount++;
            int lookAdCount = SaveDataUtils.GameData.userLookDailyAdCount;
            int lookMax = SaveDataUtils.GameData.userLookDailyAdCountMax;
            bool black = lookAdCount + 1 >= lookMax;
            LogLogger.LogVerbose(LogTag.DailyAD, $"每日任---务界面刷新 ： " + $"{lookAdCount}/{lookMax}");
            countTxt = $"<color=#9039D8>{lookAdCount}/{lookMax}</color>";
            hintsTxt.text = hintTxt + countTxt;
            RefreshProgress(lookAdCount,lookMax);
            AccountModule.Instance.Request_RoutineTaskLookAdMoneyRequest(true, OnResultCallback, black);
        }
        else
        {
            //  UIUtils.ShowTips(LanguageUtils.GetText("DailyMissionPanel_NoAd"));
            UIModule.Instance.ClosePage(UIPageIds.DailyMissionPanel);
            LogLogger.LogVerbose(LogTag.DailyAD, $"获取服务器数据 OceanShineRoutineTaskLookAdMoneyResponse 失败 ");
        }
    }

    private void Refresh(FailHttpResponse<AccountModule.OceanShineWithdrawalPageResponse> response)
    {
        if (!this)
        {
            return;
        }
        if (!response.success || response.data == null)
        {
            LogLogger.LogVerbose(LogTag.DailyAD, "每日提现任务 ：为空 ");
            CloseSelf();
            return;
        }

        LogLogger.LogVerbose(LogTag.DailyAD, "每日任务 ： " + response.data.Os_Wwf);
        plats = response.data.Os_Wwf;
    }

    public void OnClickWithdrawStateBtn()
    {
        bool isSelectPlatform = false;
        AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform plat = null;
        foreach (var _plat in plats)
        {
            if (AccountModule.CountryType == AccountModule.E_CountryType.US
                && _plat.Os_Cn.Equals(UIWithdrawalPanel.paypalInfo))
            {
                plat = _plat;
                break;
            }
            else if (AccountModule.CountryType == AccountModule.E_CountryType.BR
                     && _plat.Os_Cn.Equals(UIWithdrawalPanel.pagBankInfo))
            {
                plat = _plat;
                break;
            }
            else if (AccountModule.CountryType == AccountModule.E_CountryType.ID
                     && _plat.Os_Cn.Equals(UIWithdrawalPanel.danaInfo))
            {
                isSelectPlatform = true;
                plat = _plat;
                break;
            }
        }

        UIModule.Instance.OpenPage<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform, List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>, E_WithdrawType, Action, bool>
            (UIPageIds.UIWithdrawalPanel, plat, plats, E_WithdrawType.DailyMission, null, isSelectPlatform).Forget();
        SaveDataUtils.GameData.btnWithdrawClick++;
    }

    public void OnClickWithdrawedStateBtn()
    {
        UIUtils.ShowTips(LanguageUtils.GetText("DailyMissionPage_Claimed"));

    }

    public void OnClickClose()
    {
        UIModule.Instance.ClosePage(this);
    }

    protected override void OnClose()
    {
        SetLinster(false);
    }

    //编辑器下测试每日提现
    public void EditorTestDailyMission()
    {
        StartCoroutine(_EditorTestDailyMission());
    }

    private IEnumerator _EditorTestDailyMission()
    {
        int max = SaveDataUtils.GameData.userLookDailyAdCountMax;
        var cfg = ChannelConfig.Instance;
        var old = cfg.real_CustomConfig;
        for (int i = SaveDataUtils.GameData.userLookDailyAdCount; i <= max; i++)
        {
            cfg.real_CustomConfig.testECPM1000 = true;
            cfg.real_CustomConfig.TestECPMValue = Random.Range(200f, 250f);
            OnClickGoStateBtn();

            yield return new WaitForSeconds(5f);
            var btnGo = GameObject.Find("Rewarded(Clone)/Panel/MaxRewardedCloseButton");
            if (btnGo != null)
            {
                var btn = btnGo.GetComponent<Button>();
                btn.onClick?.Invoke();
            }
            yield return new WaitForSeconds(10f);
        }

        cfg.real_CustomConfig = old;
        Debug.LogError("============每日任务广告观看完成============");
    }
}

public static partial class EventDefine
{
    public static partial class WithDraw
    {
        public static GameEvent RefreshDailyMissionPage = new();
        public static GameEvent RefreshDailyMissionPageFalse = new();
    }
}
#endif
