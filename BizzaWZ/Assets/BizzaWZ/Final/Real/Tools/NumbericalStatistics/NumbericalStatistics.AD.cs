#if BIZZA_REAL_WITHDRAW
using System;
using System.Collections;
using System.Collections.Generic;
using Bizza.Sdk;
using Cysharp.Threading.Tasks;
using UnityEngine;

public partial class NumbericalStatistics
{
    public static int currentLevel => SaveDataUtils.GameData.playerSelectedLv; // 当前关卡
    public static int CloseGetRewardCount // 关闭多少次恭喜获得界面出现广告
    {
        get
        {
            return AdStatisticsConfigMiddleware.Get(currentLevel).CloseGetRewardCount;
        }
    }
    public static int CloseGetRewardNum = 0; // 当前关闭恭喜获得界面次数进度

    public static int ShowGetRewardCount  // 旧版次数配置，当前弹窗使用时间间隔
    {
        get
        {
            return AdStatisticsConfigMiddleware.Get(currentLevel).ShowGetRewardCount;
        }
    }
    public static int ShowGetRewardNum = 0; // 当前出现恭喜获得界面的进度

    public static int ShowDollarCount  // 多少次出现假钞获得界面
    {
        get
        {
            return AdStatisticsConfigMiddleware.Get(currentLevel).ShowDollarCount;
        }
    }

    /// <summary>
    /// 完整拼图收集后，达到时间间隔才打开恭喜获得界面。
    /// </summary>
    public static bool CheckShowGetReward(Vector3 pos)
    {
        bool show = RewardPopupTiming.TryReserve();
        LogLogger.LogVerbose(LogTag.ADNumericalStatistics,
            $"打开恭喜获得界面 - 间隔:{RewardPopupTiming.IntervalSeconds}秒,是否显示:{show}");
        if (!show)
        {
            return false;
        }
        ShowGetRewardNum = 0;
        Real_GetRewardPanelUtil.OpenGetRewardPanel(DoubleGetRewardPanel.E_UseScene.MatchReward);
        return true;
    }

    /// <summary>
    /// 关闭恭喜获得界面一定次数会强  弹插屏
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="money"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static bool CheckCloseGetReward(E_AdPos pos, float money, Action<Bizza.Sdk.ShowAdResult> action)
    {
        if (InterstitialProtection.IsBlocked)
        {
            CloseGetRewardNum = 0;
            return false;
        }
        bool show = AdvanceRewardCloseCounter(CloseGetRewardCount);
        LogLogger.LogVerbose(LogTag.ADNumericalStatistics, $"关闭界面弹插屏 - 进度:{CloseGetRewardNum},最大次数:{CloseGetRewardCount},是否显示:{show}");
        if (!show)
        {
            return false;
        }
        UIModule.Instance.RecoverAdvertistics();
        BizzaSdk.Ad.ShowInterAd(pos.ToString(), money, action, true);
        UIModule.Instance.m_curadvertistics--;
        return true;
    }

    public static bool AdvanceRewardCloseCounter(int threshold)
    {
        CloseGetRewardNum++;
        if (CloseGetRewardNum < System.Math.Max(1, threshold)) return false;
        CloseGetRewardNum = 0;
        return true;
    }

    /// <summary>
    /// 完整拼图收集时发放Dollar
    /// </summary>
    /// <returns></returns>
    public static bool CheckGetDollar(Vector3 pos)
    {
        var showDollarCount = ShowDollarCount;
        bool show = !ChannelConfig.Instance.real_CustomConfig.singleCurrencyMode;
        LogLogger.LogVerbose(LogTag.ADNumericalStatistics, $"出现Dollar界面 - 进度:{ShowGetRewardNum},最大次数:{showDollarCount},是否显示:{show}");
        if (!show) return false;

        float moneyValue = WithdrawalUtil.GetDollarCountBtFree();
        ItemEntry dollar = new()
        {
            Type = E_ItemType.Dollar,
            Count = WithdrawalUtil.GetCustomizedFloatByCountryType(moneyValue) / 5
        };

        // UIUtils.ShowTips(dollar, default, (pos1, pos2) =>
        // {

        // });
        ItemUtils.AddItem(dollar, new AddItemParam
        {
            playAnim = true,
            isAd = false,
            startPos = pos,
            bUiPos = false,
            source = DoubleGetRewardPanel.GetItemSource(DoubleGetRewardPanel.E_UseScene.Ad),
        });
        return true;
    }
}
#endif
