#if BIZZA_REAL_WITHDRAW
using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WithdrawHistoryItem : MonoBehaviour
{
    public TMP_Text nameTxt;
    public TMP_Text timeTxt;
    public TMP_Text emailTxt;
    public TMP_Text cpfTxt;
    public TMP_Text dueText;
    public GameObject successObj;
    public GameObject processingObj;
    public GameObject failObj;
    public Image withdrawImg;
    public TMP_Text amountTxt;

    [SerializeField] private PaymentConfig paymentConfig;
    [SerializeField] private GameObject reasonSection;
    [SerializeField] private string dateFormat = "dd MMM yyyy";
    [SerializeField] private string currencySeparator = "";

    public void Init(AccountModule.OceanShineWithdrawalRecord data)
    {
        timeTxt.text = FormatDate(data.Os_Dat);
        amountTxt.text = LanguageUtils.GetText("CurrencyToken") + currencySeparator
            + WithdrawalUtil.GetCustomizedValueByCountryType((float)data.Os_Prc);
        string account = AccountModule.CountryType == AccountModule.E_CountryType.BR
            ? data.Os_Re : data.Os_Ra;
        if (string.IsNullOrEmpty(account)) account = string.IsNullOrEmpty(data.Os_Re) ? data.Os_Ra : data.Os_Re;
        emailTxt.text = string.IsNullOrEmpty(account) ? "—" : account;
        emailTxt.gameObject.SetActive(true);
        bool showName = (AccountModule.CountryType == AccountModule.E_CountryType.BR
            || AccountModule.CountryType == AccountModule.E_CountryType.ID) && !string.IsNullOrEmpty(data.Os_Rn);
        bool showCpf = AccountModule.CountryType == AccountModule.E_CountryType.BR && !string.IsNullOrEmpty(data.Os_Cp);
        nameTxt.text = BubblePics.Localization.Tr("history_name") + ": " + data.Os_Rn;
        cpfTxt.text = BubblePics.Localization.Tr("history_identity") + ": " + data.Os_Cp;
        nameTxt.gameObject.SetActive(showName);
        cpfTxt.gameObject.SetActive(showCpf);
        withdrawImg.sprite = paymentConfig.GetSpriteByPayKey(data.Os_Pym);
        successObj.SetActive(data.Os_Sts == 3);
        processingObj.SetActive(data.Os_Sts == 1);
        bool failed = data.Os_Sts == 2 || data.Os_Sts > 3;
        failObj.SetActive(failed);
        reasonSection.SetActive(failed);
        dueText.gameObject.SetActive(failed);
        dueText.text = string.IsNullOrEmpty(data.Os_Tsm)
            ? BubblePics.Localization.Tr("history_reason_unavailable") : data.Os_Tsm;
        // Card height, optional fields and the reason block are controlled by prefab layouts.
    }

    private string FormatDate(string value)
    {
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime date)) return value ?? "";
        string locale = BubblePics.Localization.CurrentLocale;
        CultureInfo culture = locale == "pt_BR" ? CultureInfo.GetCultureInfo("pt-BR")
            : locale == "zh_CN" ? CultureInfo.GetCultureInfo("zh-CN") : CultureInfo.GetCultureInfo("en-US");
        return date.ToString(dateFormat, culture);
    }
}
#endif
