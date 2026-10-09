#if BIZZA_REAL_WITHDRAW
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Bizza.Sdk;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UIPageIds
{
    public static readonly PageId ExchangeRatePanel = "ExchangeRatePanel";
}


public class ExchangeRatePanel : UIPageBase<ExchangeRateInfo>
{
    public TMP_Text beforeBlanceText;
    public TMP_Text beforeClashText;
    public TMP_Text nowBlanceText;
    public TMP_Text nowClashText;

    public TMP_Text beforeDefiniteText;
    public TMP_Text nowDefiniteText;
    [SerializeField] private TMP_Text levelText;
    private ExchangeRateInfo currentInfo;

    public RectTransform root;

    [Header("按钮")]
    [SerializeField] private BizzaButton withdrawBtn;
    [SerializeField] private BizzaButton clickBtn;
    protected override void OnAwake()
    {

        withdrawBtn.onClick.AddListener(OnClickBtn);
        clickBtn.onClick.AddListener(CloseSelf);
    }

    protected override void OnOpen(ExchangeRateInfo info)
    {
        currentInfo=info;
        BubblePics.Localization.LocaleChanged += RefreshView;
        RefreshView();
    }

    private void RefreshView()
    {
        beforeBlanceText.text = string.Format(BubblePics.Localization.Tr("sequential_coin_count"),WithdrawalUtil.GetCustomizedValueByCountryType((float)currentInfo.beforeBlance));
        beforeClashText.text = $"≈ {LanguageUtils.GetText("CurrencyToken")}{WithdrawalUtil.GetCustomizedValueByCountryType((float)currentInfo.beforeClash)}";
        nowBlanceText.text = string.Format(BubblePics.Localization.Tr("sequential_coin_count"),WithdrawalUtil.GetCustomizedValueByCountryType((float)currentInfo.nowBlance));
        nowClashText.text = $"≈ {LanguageUtils.GetText("CurrencyToken")}{WithdrawalUtil.GetCustomizedValueByCountryType((float)currentInfo.nowClash)}";
        int level=ChannelConfig.Instance.real_CustomConfig.realWithdrawPassMode?SaveDataUtils.GameData.playerpassLevel:SaveDataUtils.GameData.playerSelectedLv;
        levelText.text=string.Format(BubblePics.Localization.Tr("ui_unlock_level"),level);
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
    }

    public void OnClickBtn()
    {
        CloseSelf();
        UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel).Forget();
    }

    protected override void OnClose()
    {
        BubblePics.Localization.LocaleChanged -= RefreshView;
    }
}

[Serializable]
public struct ExchangeRateInfo
{
    public double beforeBlance;
    public double beforeClash;
    public double nowBlance;
    public double nowClash;

    public double beforeRate;
    public double nowRate;

    public (string, string) ExchangeDefiniteRate()
    {
        string beforeInfo = "";
        string nowInfo = "";
        if (AccountModule.CountryType == AccountModule.E_CountryType.BR)
        {
            beforeInfo = $"100≈{LanguageUtils.GetText("CurrencyToken")}{beforeRate * 100}";
        }
        else if (AccountModule.CountryType == AccountModule.E_CountryType.ID)
        {
            beforeInfo = $"1000≈{LanguageUtils.GetText("CurrencyToken")}{beforeRate * 1000}";
        }
        else if (AccountModule.CountryType == AccountModule.E_CountryType.US)
        {
            beforeInfo = $"10000≈{LanguageUtils.GetText("CurrencyToken")}{beforeRate * 10000}";
        }

        if (AccountModule.CountryType == AccountModule.E_CountryType.BR)
        {
            nowInfo = $"100≈{LanguageUtils.GetText("CurrencyToken")}{nowRate * 100}";
        }
        else if (AccountModule.CountryType == AccountModule.E_CountryType.ID)
        {
            nowInfo = $"1000≈{LanguageUtils.GetText("CurrencyToken")}{nowRate * 1000}";
        }
        else if (AccountModule.CountryType == AccountModule.E_CountryType.US)
        {
            nowInfo = $"10000≈{LanguageUtils.GetText("CurrencyToken")}{nowRate * 10000}";
        }

        return (beforeInfo, nowInfo);
    }
}
#endif
