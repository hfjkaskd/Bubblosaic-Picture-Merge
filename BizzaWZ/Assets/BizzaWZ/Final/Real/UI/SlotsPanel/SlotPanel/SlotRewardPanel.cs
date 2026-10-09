#if BIZZA_REAL_WITHDRAW
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotRewardPanel : MonoBehaviour
{
    public GameObject coinObj;
    public TMP_Text coinText;

    public GameObject dollarObj;
    public TMP_Text dollarText;

    public CurrencyInfo coinTarget;
    public Transform coinTargetPos;
    public CurrencyInfo dollarTarget;
    public Transform dollarTargetPos;

    public List<SlotRewardIcon> slotRewardIcons = new List<SlotRewardIcon>()
    {
        new SlotRewardIcon() { image = null, e_WzIconType = E_WzIconType.StackMoney },
        new SlotRewardIcon() { image = null, e_WzIconType = E_WzIconType.PileMoney },
        new SlotRewardIcon() { image = null, e_WzIconType = E_WzIconType.HundredMoney },
        new SlotRewardIcon() { image = null, e_WzIconType = E_WzIconType.GoldCoin },
        new SlotRewardIcon() { image = null, e_WzIconType = E_WzIconType.PileGold },
        new SlotRewardIcon() { image = null, e_WzIconType = E_WzIconType.PileWealth }
    };

    private float coinValue;
    private float dollarValue;
    private bool adReward;
    private bool claimed;

    public GameObject btnObj;
    public Image resultImage;
    [SerializeField] private BizzaButton collectButton;

    private void Awake()
    {
        if (collectButton != null) collectButton.onClick.AddListener(OnClickClose);
    }

    public void Init(float coin, float dollar, string resultType, bool fromAd = false)
    {
        dollar = WithdrawalUtil.NormalizeIndonesianReward(dollar);
        adReward = fromAd;
        claimed = false;
        coinObj.SetActive(coin > 0);
        coinValue = coin;
        dollarValue = dollar;

        dollarObj.SetActive(dollar > 0);
        var value = ItemUtils.Get(E_ItemType.Dollar).Count;
        #if BIZZA_REAL_WITHDRAW
        if (AccountModule.CountryType == AccountModule.E_CountryType.BR)
        {
            coinText.text = WithdrawalUtil.GetCustomizedIntByCountryType2(coin);
            dollarText.text = $"{LanguageUtils.GetText("CurrencyToken")}{WithdrawalUtil.GetCustomizedIntByCountryType2(dollar)}";
        }
        else
        {
            coinText.text = WithdrawalUtil.GetCustomizedValueByCountryType(coin);
            dollarText.text = $"{LanguageUtils.GetText("CurrencyToken")}{WithdrawalUtil.GetCustomizedFloatByCountryType(dollar)}";
        }
        #endif
        
        Image image = GetSlotRewardImage(resultType);
        if (image != null)
        {
            image.gameObject.SetActive(true);
            UIUtils.SetWzSprite(resultImage, resultType);
        }
        
    }

    public Image GetSlotRewardImage(string resultType)
    {
        foreach (var item in slotRewardIcons)
        {
            item.image.gameObject.SetActive(false);
        }

        foreach (var item in slotRewardIcons)
        {
            if (IconName(item.e_WzIconType) == resultType)
            {
                UIUtils.SetWzSprite(item.image, resultType);
                return item.image;
            }
        }
        return null;
    }
    private static string IconName(E_WzIconType value)
    {
        switch(value)
        {
            case E_WzIconType.StackMoney:return nameof(E_WzIconType.StackMoney);
            case E_WzIconType.PileMoney:return nameof(E_WzIconType.PileMoney);
            case E_WzIconType.HundredMoney:return nameof(E_WzIconType.HundredMoney);
            case E_WzIconType.GoldCoin:return nameof(E_WzIconType.GoldCoin);
            case E_WzIconType.PileGold:return nameof(E_WzIconType.PileGold);
            case E_WzIconType.PileWealth:return nameof(E_WzIconType.PileWealth);
            default:return string.Empty;
        }
    }

    [Obfuz.ObfuzIgnore]
    public void OnClickClose()
    {
        if (claimed) return;
        claimed = true;
        // 钞票飞过去
       // float money = WithdrawalUtil.GetDollarCountByReward();
        ItemUtils.AddItem(
                new ItemEntry()
                {
                    Type = E_ItemType.Dollar,
                    Count = dollarValue,
                },
                new AddItemParam()
                {
                    playAnim = true,
                    isAd = false,
                    startPos = btnObj.transform.position,
                    bUiPos = false,
                    source = E_AddItemSource.UsSlot,
                    target = dollarTargetPos,
                    animation = adReward ? RewardCollectAnimation.BurstCollect : RewardCollectAnimation.Legacy,
                    callbackOwner = dollarTarget,
                    addFinishAction = () => dollarTarget.Refresh()
                });
       // dollarTarget.Refresh();

//         LogUtil.Error($"coinValue: {coinValue}");
        if (coinValue > 0)
        {
            // 金币飞过去
            ItemUtils.AddItem(
                    new ItemEntry()
                    {
                        Type = E_ItemType.Gold,
                        Count = coinValue,
                    },
                    new AddItemParam()
                    {
                        playAnim = true,
                        isAd = false,
                        startPos = btnObj.transform.position,
                        bUiPos = false,
                        source = E_AddItemSource.UsSlot,
                        target = coinTargetPos,
                        animation = adReward ? RewardCollectAnimation.BurstCollect : RewardCollectAnimation.Legacy,
                        callbackOwner = coinTarget,
                        addFinishAction = () => coinTarget.Refresh()
                    });
            // coinTarget.Refresh();
        }
       gameObject.SetActive(false);
    }

}

#if BIZZA_REAL_WITHDRAW
[Serializable]
public struct SlotRewardIcon
{
    public Image image;
    public E_WzIconType e_WzIconType; 
}

#else

[Serializable]
public struct SlotRewardIcon
{
    public Image image;
    public E_WzIconType e_WzIconType; 
}

public enum E_WzIconType
{
    MoneyIcon, 
    PieceMoney,
    StackMoney,
    PileMoney,
    PileWealth,
    PileGold,
    HundredMoney,
    AbundanceWealth,
    MoneyEnhancement, // 恭喜获得界面，比例提升
    GoldCoin,
    BubbleCoin,
    BubbleMoney
}
#endif
#endif
