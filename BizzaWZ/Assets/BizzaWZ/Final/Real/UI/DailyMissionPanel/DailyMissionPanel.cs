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

    [SerializeField] private BusinessPanelStatusView loadingStatus;
    [SerializeField] private GameObject[] dataWidgets;
    private enum ContentState { Closed, Loading, Ready, Failed }
    private ContentState contentState;
    private const float LoadTimeoutSeconds = 15f;
    private int requestVersion;
    private float loadStartedAt;
    private bool platformsReady;
    private bool adPending;
    private AccountModule.RoutineTaskLookAdMoneyResponse pendingMission;
#if UNITY_EDITOR
    internal static Action<bool, Action<FailHttpResponse<List<AccountModule.RoutineTaskLookAdMoneyResponse>>>> EditorRequestMission;
    internal static Action<Action<FailHttpResponse<AccountModule.OceanShineWithdrawalPageResponse>>> EditorRequestPlatforms;
#endif


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
        loadingStatus.Bind(OnRequestData, false);
    }
    
    protected override void OnOpen()
    {
        OnRequestData();
        SetLinster(true);
    }

    public void OnRequestData()
    {
        RequestContent(true);
    }

    public void OnrequestDailyMission()
    {
        RequestContent(true);
    }

    public void OnRequestDailyMission()
    {
        RequestContent(false);
    }

    private void RequestContent(bool update)
    {
        if (IsClosing || !gameObject.activeInHierarchy) return;
        int version = ++requestVersion;
        pendingMission = null;
        platformsReady = false;
        plats.Clear();
        loadStartedAt = Time.unscaledTime;
        SetContentState(ContentState.Loading);
        try
        {
            // Local loading feedback leaves the close button available.
            Action<FailHttpResponse<List<AccountModule.RoutineTaskLookAdMoneyResponse>>> missionReply = response =>
            {
                if (AcceptResponse(version)) OnResultCallback(response);
            };
#if UNITY_EDITOR
            if (EditorRequestMission != null) EditorRequestMission(update, missionReply);
            else
#endif
                AccountModule.Instance.Request_RoutineTaskLookAdMoneyRequest(update, missionReply, false);
            if (!AcceptResponse(version)) return;
            Action<FailHttpResponse<AccountModule.OceanShineWithdrawalPageResponse>> platformReply = response =>
            {
                if (AcceptResponse(version)) Refresh(response);
            };
#if UNITY_EDITOR
            if (EditorRequestPlatforms != null) EditorRequestPlatforms(platformReply);
            else
#endif
                AccountModule.Instance.Request_WithdrawalPageRequest(platformReply, block: false);
        }
        catch (Exception exception)
        {
            if (AcceptResponse(version)) SetContentState(ContentState.Failed);
            Debug.LogException(exception);
        }
    }

    private bool AcceptResponse(int version)
    {
        return this != null && version == requestVersion && !IsClosing &&
               gameObject.activeInHierarchy && contentState == ContentState.Loading;
    }

    private void SetContentState(ContentState state)
    {
        contentState = state;
        bool ready = state == ContentState.Ready;
        adPending = false;
        if (state == ContentState.Loading || state == ContentState.Failed)
            loadingStatus.Show(state == ContentState.Failed);
        else loadingStatus.Hide();
        foreach (var widget in dataWidgets) if (widget != null) widget.SetActive(ready);
        hintsTxt.gameObject.SetActive(ready);
        refreshTimeTxt.gameObject.SetActive(ready);
        goBtn.interactable = goBtn.enabled = ready;
        withdrawBtn.interactable = withdrawBtn.enabled = ready;
        claimedBtn.interactable = claimedBtn.enabled = ready;
        if (!ready)
        {
            GoObj.SetActive(false);
            WithdrawObj.SetActive(false);
            ClaimedObj.SetActive(false);
            claimedHint.gameObject.SetActive(false);
        }
    }

    private void TryShowContent()
    {
        if (pendingMission == null || !platformsReady) return;
        try
        {
            var data = pendingMission;
            int max = Math.Max(0, data.Os_An);
            int cur = Math.Clamp(data.Os_Ln, 0, max);
            SaveDataUtils.GameData.userLookDailyAdCountMax = max;
            SaveDataUtils.GameData.userLookDailyAdCount = cur;
            SetContentState(ContentState.Ready);
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
            timer = 0f;
            UpdateRemainingTime();
        }
        catch (Exception exception)
        {
            SetContentState(ContentState.Failed);
            Debug.LogException(exception);
        }
    }

    private string hintTxt;
    private string countTxt;
    private void OnResultCallback(FailHttpResponse<List<AccountModule.RoutineTaskLookAdMoneyResponse>> responses)
    {
        if (responses.success && responses.data != null && responses.data.Count > 0 && responses.data[0] != null &&
            responses.data[0].Os_Ss >= 0 && responses.data[0].Os_Ss <= 3)
        {
            pendingMission = responses.data[0];
            TryShowContent();
        }
        else
        {
            SetContentState(ContentState.Failed);
            LogLogger.LogVerbose(LogTag.DailyAD, "每日任务数据加载失败");
        }
    }

    private float timer;
    private void Update()
    {
        if (IsClosing) return;
        if (contentState == ContentState.Loading)
        {
            if (Time.unscaledTime - loadStartedAt >= LoadTimeoutSeconds)
                SetContentState(ContentState.Failed);
            return;
        }
        if (contentState != ContentState.Ready) return;
        timer += Time.unscaledDeltaTime;
        if (timer >= 1f)
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
        if (IsClosing || adPending) return;
        if (contentState == ContentState.Failed)
        {
            OnRequestData();
            return;
        }
        if (contentState != ContentState.Ready || !GoObj.activeSelf) return;
        adPending = true;
        goBtn.interactable = goBtn.enabled = false;
        int version = requestVersion;
        BizzaSdk.Ad.ShowRewardAd(E_AdPos.DailyMission.ToString(), 0, result =>
        {
            if (this != null && version == requestVersion && !IsClosing && gameObject.activeInHierarchy)
                OnGoResponse(result);
        }, ecpmLimit);
        UIModule.Instance.m_curadvertistics--;
        SaveDataUtils.GameData.btnDailyTaskClick++;
    }

    private void OnGoResponse(Bizza.Sdk.ShowAdResult showAdResult)
    {
        if (showAdResult.success)
        {
            LogLogger.LogVerbose(LogTag.DailyAD, "用户点击了观看每日任务");
            SaveDataUtils.GameData.userLookDailyAdCount++;
            RequestContent(true);
        }
        else
        {
            UIModule.Instance.ClosePage(UIPageIds.DailyMissionPanel);
        }
    }

    private void Refresh(FailHttpResponse<AccountModule.OceanShineWithdrawalPageResponse> response)
    {
        if (!response.success || response.data?.Os_Wwf == null || response.data.Os_Wwf.Count == 0 || response.data.Os_Wwf.Exists(platform => platform == null || string.IsNullOrEmpty(platform.Os_Cn)))
        {
            SetContentState(ContentState.Failed);
            LogLogger.LogVerbose(LogTag.DailyAD, "每日提现平台加载失败");
            return;
        }

        // Do not clear AccountModule's cached platform list when retrying.
        plats = new List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>(response.data.Os_Wwf);
        platformsReady = true;
        TryShowContent();
    }

    public void OnClickWithdrawStateBtn()
    {
        if (contentState != ContentState.Ready || IsClosing || !WithdrawObj.activeSelf) return;
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
        if (contentState != ContentState.Ready || IsClosing || !ClaimedObj.activeSelf) return;
        UIUtils.ShowTips(LanguageUtils.GetText("DailyMissionPage_Claimed"));

    }

    public void OnClickClose()
    {
        UIModule.Instance.ClosePage(this);
    }

    protected override void OnClose()
    {
        requestVersion++;
        pendingMission = null;
        SetContentState(ContentState.Closed);
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
