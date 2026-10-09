#if BIZZA_REAL_WITHDRAW
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Localization = BubblePics.Localization;

public class WithdrawHintPanel : MonoBehaviour
{
    public TMP_Text hintText;
    [SerializeField] TMP_Text requirementText;
    [SerializeField] TMP_Text progressText;
    [SerializeField] Image progressFill;
    [SerializeField] BizzaButton closeBtn;
    bool levelRequirement;
    float currentValue,targetValue;

    void Awake() { closeBtn.onClick.AddListener(OnClickClose); }
    void OnEnable() { Localization.LocaleChanged += RefreshView; }
    void OnDisable() { Localization.LocaleChanged -= RefreshView; }

    public void Init(float remainingBonus,float minimumBonus)
    {
        levelRequirement=false;currentValue=Mathf.Max(0,minimumBonus-remainingBonus);targetValue=minimumBonus;
        gameObject.SetActive(true);RefreshView();
    }
    public void Init(int level,int currentLevel)
    {
        levelRequirement=true;currentValue=currentLevel;targetValue=level;
        gameObject.SetActive(true);RefreshView();
    }
    void RefreshView()
    {
        if(levelRequirement)
        {
            int level=Mathf.Max(0,Mathf.RoundToInt(targetValue));
            int gap=Mathf.Max(0,level-Mathf.RoundToInt(currentValue));
            requirementText.text=string.Format(Localization.Tr("ui_unlock_level"),level);
            hintText.text=string.Format(Localization.Tr(gap==1?"sequential_unlock_one":"sequential_unlock_many"),gap);
            progressText.text=Mathf.Clamp(Mathf.RoundToInt(currentValue),0,level)+" / "+level;
        }
        else
        {
            requirementText.text=FakeWithdrawPanel.FormatAmount(WithdrawalUtil.GetCustomizedFloatByCountryType(targetValue));
            progressText.text=WithdrawalUtil.GetCustomizedValueByCountryType(Mathf.Clamp(currentValue,0,Mathf.Max(0,targetValue)))+" / "+WithdrawalUtil.GetCustomizedValueByCountryType(targetValue);
            hintText.text=LanguageUtils.GetFormatText("WithdrawHintPanel_Hint",WithdrawalUtil.GetCustomizedValueByCountryType(targetValue),WithdrawalUtil.GetCustomizedValueByCountryType(Mathf.Max(0,targetValue-currentValue)));
        }
        progressFill.fillAmount=targetValue>0?Mathf.Clamp01(currentValue/targetValue):0;
    }
    public void OnClickClose() { gameObject.SetActive(false); }
}
#endif
