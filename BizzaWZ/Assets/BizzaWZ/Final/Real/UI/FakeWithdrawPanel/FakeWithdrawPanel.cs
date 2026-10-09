#if BIZZA_REAL_WITHDRAW
using System;
using System.Collections;
using System.Collections.Generic;
using Bizza;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Obfuz;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using static AccountModule;
using Localization = BubblePics.Localization;

public partial class UIPageIds
{
    public static readonly PageId FakeWithdrawPanel = "FakeWithdrawPanel";
}


public class FakeWithdrawPanel : UIPageBase
{

    public List<WithDrawMissionSO> USWithdrawMissionSOList;
    public List<WithDrawMissionSO> BRWithdrawMissionSOList;
    public List<WithDrawMissionSO> IDWithdrawMissionSOList;
    public bool isReward
    {
        get => SaveDataUtils.GameData.fakeWithdrawPanelFirstWithdraw;
        set => SaveDataUtils.GameData.fakeWithdrawPanelFirstWithdraw = value;
    }
    public List<WithDrawMissionSO> withdrawMissionSOList
    {
        get
        {
            if (AccountModule.CountryType == E_CountryType.US)
            {
                return USWithdrawMissionSOList;
            }
            else if (AccountModule.CountryType == E_CountryType.BR)
            {
                return BRWithdrawMissionSOList;
            }
            else if (AccountModule.CountryType == E_CountryType.ID)
            {
                return IDWithdrawMissionSOList;
            }
            else
            {
                Debug.LogError($"未实现提现功能的国家类型：{AccountModule.CountryType}");
                return new List<WithDrawMissionSO>();
            }
        }
    }

    public BizzaButton withdrawBtn;
    [SerializeField] private BizzaButton faqBtn;
    [SerializeField] private BizzaButton closeBtn;
    [SerializeField] private BizzaButton historyBtn;

    public TMP_Text balanceTxt;

    public WithdrawAmountItem item;
    public Transform root;
    public List<WithdrawAmountItem> items = new List<WithdrawAmountItem>();
    public List<float> amountList = new List<float>();
    public Image progressImg;
    public TMP_Text progressTxt;

    public TMP_Text hintTxt;
    public int curSelectIndex = 0;

    public GameObject fingerObj;

    [SerializeField] private NewPlayerPayoutOption payoutOption;
    [SerializeField] private Transform payoutRoot;
    private readonly List<NewPlayerPayoutOption> payoutOptions = new List<NewPlayerPayoutOption>();
    private AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform selectedPlatform;

    private bool HasStarterWithdraw => AccountModule.CountryType == E_CountryType.BR
                                       || AccountModule.CountryType == E_CountryType.ID;

    // US 移除新手档后，仍沿用原存档索引，避免已有金额档位的任务进度错位。
    private int CurrentStageIndex => curSelectIndex + (AccountModule.CountryType == E_CountryType.US ? 1 : 0);

    private bool EnsureSelection()
    {
        var missions = withdrawMissionSOList;
        if (missions == null || missions.Count == 0) return false;
        curSelectIndex = Mathf.Clamp(curSelectIndex, 0, missions.Count - 1);
        return true;
    }

    protected override void OnAwake()
    {
        base.OnAwake();
        withdrawBtn.onClick.AddListener(OnClickWithdrawBtn);
        faqBtn.onClick.AddListener(() => { OnClickFQABtn(); });
        closeBtn.onClick.AddListener(() => { CloseSelf(); });
        historyBtn.onClick.AddListener(() => { OnClickWithdrawHistory(); });
    }

    protected override void OnOpen()
    {
        plats?.Clear();
        selectedPlatform = null;
        curSelectIndex = HasStarterWithdraw && isReward ? 1 : 0;
        if (!EnsureSelection())
        {
            CloseSelf();
            return;
        }
        items.SetCmptListCount(item, root, withdrawMissionSOList.Count);
        BizzaEventSystem.On(EventDefine.Item.ItemChangedWithData, OnRefresh);
        BizzaEventSystem.On(EventDefine.Item.ItemChangedWithData, OnRewardProgressChanged);
        AccountModule.Instance.Request_WithdrawalPageRequest(Refresh);
        fingerObj.gameObject.SetActive(false);
        SetLinster(true);
        Localization.LocaleChanged += OnLocaleChanged;
    }

    private List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform> plats;

    private void Refresh(FailHttpResponse<AccountModule.OceanShineWithdrawalPageResponse> response)
    {
        if (!response.success || response.data == null)
        {
            CloseSelf();
            return;
        }

        plats = response.data.Os_Wwf;
        PresentPlatforms(plats);

        curSelectIndex = HasStarterWithdraw && isReward ? 1 : 0;
        if (!EnsureSelection())
        {
            CloseSelf();
            return;
        }

        var missions = withdrawMissionSOList;
        items.SetCmptListCount(item, root, missions.Count);
        // 对象池只隐藏多余元素，不能使用 items.Count 索引任务列表。
        for (int i = 0; i < missions.Count; i++)
        {
            bool isStarterItem = HasStarterWithdraw && i == 0;
            bool canGet = isStarterItem && !isReward;
            var itemEntry = new ItemEntry()
            {
                Type = E_ItemType.Dollar,
                Count = missions[i].withdrawMoney,
            };
            string count = FormatAmount(ItemUtils.FormatCountFloat(itemEntry));
            items[i].Init(this, i, count, canGet, isStarterItem);
        }

        SetSelectIndex(curSelectIndex);
        OnRefresh();
        UpdateProgress(false);
    }

    public void OnRefresh()
    {
        if (!EnsureSelection()) return;
        var count = ItemUtils.GetItemCount(E_ItemType.Dollar);
        PresentBalance(WithdrawalUtil.GetCustomizedFloatByCountryType(count));
        var so = withdrawMissionSOList[curSelectIndex];
        int state = GetCurState();
        WithdrawMissionData curMission = so.GetMissionByStateSafe(state);

        hintTxt.text = Localization.Tr("newplayer_requirements_met");
        if (curMission != null && !curMission.IsCanWithdraw())
        {
            float value = curMission.GetValueOfCondition();
            hintTxt.text = curMission.GetWithdrawDesc(WithdrawalUtil.GetCustomizedFloatByCountryType(value));
        }

        for (int i = 0; i < withdrawMissionSOList.Count && i < items.Count; i++)
        {
            bool isStarterItem = HasStarterWithdraw && i == 0;
            items[i].Refresh(isStarterItem && !isReward, isStarterItem);
        }
    }

    public void UpdateProgress(bool isAnim = false)
    {
        float progress = GetCurProgress();
        progress = Mathf.Clamp01(progress);
        if (progressImg == null || progressTxt == null) return; // ✅ 同时防两者

        if (isAnim)
        {
            progressImg.DOFillAmount(progress, 0.4f).SetEase(Ease.OutSine);
        }
        else
        {
            progressImg.fillAmount = progress;
        }
        var mission=withdrawMissionSOList[curSelectIndex].GetMissionByStateSafe(GetCurState());
        if(mission==null)progressTxt.text="1 / 1";
        else
        {
            float target=mission.conditionData.targetValue;
            if(mission.conditionData.condition==E_WithdrawCondition.Money)
                target=ItemUtils.FormatCountFloat(new ItemEntry{Type=E_ItemType.Dollar,Count=target});
            float value=Mathf.Clamp(mission.GetValueOfCondition(),0,Mathf.Max(0,target));
            progressTxt.text=value.ToString("0.##")+" / "+target.ToString("0.##");
        }
    }

    private float GetCurProgress()
    {
        if (!EnsureSelection()) return 0;
        var so = withdrawMissionSOList[curSelectIndex];
        int state = GetCurState();
        WithdrawMissionData curMission = so.GetMissionByStateSafe(state);

        if (curMission == null)
        {
            LogLogger.LogInfo("新手引导 已经完成所有任务");
            return 1;
        }
        float targetValue = 0;
        if (curMission.conditionData.condition == E_WithdrawCondition.Money)
        {
            var itemEntry = new ItemEntry()
            {
                Type = E_ItemType.Dollar,
                Count = curMission.conditionData.targetValue,
            };
            targetValue = ItemUtils.FormatCountFloat(itemEntry);
        }
        else
        {
            targetValue = curMission.conditionData.targetValue;
        }

        float curVlaue = curMission.GetValueOfCondition();
        float progress = targetValue > 0 ? curVlaue / targetValue : 0;
        return progress;
    }

    public void SetSelectIndex(int index)
    {
        curSelectIndex = index;
        if (!EnsureSelection()) return;
        for (int i = 0; i < items.Count; i++)
        {
            items[i].SetSelectState(i == curSelectIndex);
        }

        UpdateProgress();
    }

    private void SetLinster(bool enable)
    {
        BizzaEventSystem.Set(EventDefine.WithDraw.FingerShow, SetFinger, enable);
        // BizzaEventSystem.Set(EventDefine.WithDraw.RefreshRealPage, OnRefresh, enable);
    }

    private void SetFinger(bool isShow)
    {
        if (this == null) return;
        fingerObj?.SetActive(isShow);
    }

    protected override void OnClose()
    {
        BizzaEventSystem.Off(EventDefine.Item.ItemChangedWithData, OnRefresh);
        BizzaEventSystem.Off(EventDefine.Item.ItemChangedWithData, OnRewardProgressChanged);
        SetLinster(false);
        Localization.LocaleChanged -= OnLocaleChanged;
        progressImg?.DOKill();
    }
    void OnDestroy()
    {
        BizzaEventSystem.Off(EventDefine.Item.ItemChangedWithData, OnRefresh);
        BizzaEventSystem.Off(EventDefine.Item.ItemChangedWithData, OnRewardProgressChanged);
        Localization.LocaleChanged -= OnLocaleChanged;
        SetLinster(false);
    }

    private void OnRewardProgressChanged()
    {
        if (isActiveAndEnabled) UpdateProgress();
    }

    [ObfuzIgnore(ObfuzScope.MethodName)]
    public void OnClickWithdrawBtn()
    {
        if (!EnsureSelection()) return;
        if (plats == null || plats.Count == 0)
        {
            LogLogger.LogVerbose(BaseConst.LOG_Game, "没有提现平台");
            return;
        }
        SaveDataUtils.GameData.btnWithdrawClick++;
        bool canNewPlayerGetReward = HasStarterWithdraw && !isReward && curSelectIndex == 0;
        if (canNewPlayerGetReward)
        {
            OnWithdrawAction();
            return;
        }

        float curProgress = GetCurProgress();
        if (curProgress < 1)
        {
            //  // LogUtil.Verbose(BaseConst.LOG_Game,"当前任务未完成");
            UIUtils.ShowLanguageTips("WithdrawDanPanel_UnableClaim");
            return;
        }

        if (HasStarterWithdraw && curSelectIndex == 0)
        {
            return;
        }

        TryAdvanceStage();
    }

    private void OnWithdrawAction()
    {
        if (!HasStarterWithdraw || isReward || curSelectIndex != 0) return;

        bool isSelectPlatform = AccountModule.CountryType == E_CountryType.ID;
        var plat = selectedPlatform;

        if (plat == null)
        {
            LogLogger.LogVerbose(BaseConst.LOG_Game, "没有找到平台");
        }

        UIModule.Instance.OpenPage<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform,
            List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform>, E_WithdrawType, Action, bool>
            (UIPageIds.UIWithdrawalPanel, plat, plats, E_WithdrawType.Fake, OnStarterWithdrawComplete, isSelectPlatform).Forget();
    }

    private void OnStarterWithdrawComplete()
    {
        if (!HasStarterWithdraw || !EnsureSelection()) return;

        float amount = withdrawMissionSOList[0].withdrawMoney;
        isReward = true;
        SaveDataUtils.Save();
        SetSelectIndex(1);
        ItemUtils.TryReduceItemFloat(E_ItemType.Dollar, amount);
        LogLogger.LogInfo($"提现成功，提现金额：{amount}");
        LogLogger.LogInfo($"提现成功后，剩余提现金额：{ItemUtils.GetItemCount(E_ItemType.Dollar)}");
        OnRefresh();
        UpdateProgress();
    }

    private void TryAdvanceStage()
    {
        if (!EnsureSelection()) return;
        var so = withdrawMissionSOList[curSelectIndex];
        int stage = GetCurState();
        var mission = so.GetMissionByStateSafe(stage);

        if (mission == null)
        {
            LogLogger.LogInfo("该金额所有阶段已完成");
            return;
        }

        if (!mission.IsCanWithdraw())
        {
            UIUtils.ShowLanguageTips("WithdrawDanPanel_UnableClaim");
            return;
        }
        if (stage == 0)
        {
            ItemUtils.TryReduceItemFloat(E_ItemType.Dollar, so.withdrawMoney);
        }

        SaveDataUtils.FakeWithDrawPanelData.SetStage(CurrentStageIndex, stage + 1);
        LogLogger.LogInfo($"金额 index={curSelectIndex} 进入下一阶段：{stage + 1}");
        UIUtils.ShowLanguageTips("FakeWithdrawPanel_NextStage");
        OnRefresh();
        UpdateProgress();
    }

    [ObfuzIgnore(ObfuzScope.MethodName)]
    public void OnClickWithdrawHistory()
    {
        SetFinger(false);
        UIModule.Instance.OpenPage(UIPageIds.WithdrawHistory).Forget();
    }

    [ObfuzIgnore(ObfuzScope.MethodName)]
    public void OnClickFQABtn()
    {
        UIModule.Instance.OpenPage(UIPageIds.QFA).Forget();
    }
    private int GetCurState()
    {
        return SaveDataUtils.FakeWithDrawPanelData.curStageList[CurrentStageIndex];
    }

    public static string FormatAmount(float value) => WithdrawDanPanel.FormatTierMoney(value);
    public void PresentBalance(float value) { balanceTxt.text=FormatAmount(value); }
    public void PresentPlatforms(List<AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform> platforms)
    {
        if(payoutOption==null||payoutRoot==null)return;
        payoutOptions.SetCmptListCount(payoutOption,payoutRoot,platforms==null?0:platforms.Count);
        if(platforms==null||platforms.Count==0){selectedPlatform=null;return;}
        string preferred=AccountModule.CountryType==E_CountryType.BR?UIWithdrawalPanel.pagBankInfo:AccountModule.CountryType==E_CountryType.ID?UIWithdrawalPanel.danaInfo:UIWithdrawalPanel.paypalInfo;
        int selected=0;
        for(int i=0;i<platforms.Count;i++)
        {
            payoutOptions[i].Bind(platforms[i],SelectPlatform);
            if(platforms[i].Os_Cn==preferred)selected=i;
        }
        SelectPlatform(payoutOptions[selected]);
    }
    private void SelectPlatform(NewPlayerPayoutOption option)
    {
        selectedPlatform=option.Data;
        foreach(var item in payoutOptions)item.SetSelected(item==option);
    }
    public bool IsAmountEligible(int index)
    {
        if(index<0||index>=withdrawMissionSOList.Count)return false;
        if(HasStarterWithdraw&&index==0)return !isReward;
        int stageIndex=index+(AccountModule.CountryType==E_CountryType.US?1:0);
        var mission=withdrawMissionSOList[index].GetMissionByStateSafe(SaveDataUtils.FakeWithDrawPanelData.curStageList[stageIndex]);
        return mission==null||mission.IsCanWithdraw();
    }
    private void OnLocaleChanged()
    {
        if(!isActiveAndEnabled||!EnsureSelection())return;
        for(int i=0;i<withdrawMissionSOList.Count&&i<items.Count;i++)
            items[i].amountTxt.text=FormatAmount(ItemUtils.FormatCountFloat(new ItemEntry{Type=E_ItemType.Dollar,Count=withdrawMissionSOList[i].withdrawMoney}));
        OnRefresh();UpdateProgress();
    }

}
#endif
