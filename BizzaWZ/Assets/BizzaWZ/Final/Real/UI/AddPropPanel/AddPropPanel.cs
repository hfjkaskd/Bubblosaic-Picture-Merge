#if BIZZA_REAL_WITHDRAW
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UIPageIds
{
    public static readonly PageId AddPropPanel = "AddPropPanel";
}
public class AddPropPanel : UIPageBase<E_ItemType>
{
    public BizzaButton adBuyBtn;
    public BizzaButton closeBtn;
    public Image propIcon;
    public TMP_Text propName;
    public TMP_Text limitTxt;
    public Sprite busAwaySortPropIcon;
    public Sprite busAwayShufflePropIcon;
    [SerializeField] private GameObject authoredHintVisual;
    [SerializeField] private TMP_Text descriptionText;

    private E_ItemType _itemType;
    private PropConfigSO propConfigSO;
    private int openGeneration;
    private bool adPending;

    void Awake()
    {
        propConfigSO = PropConfigSO.Instance;
        adBuyBtn.onClick.AddListener(() =>
        {
            if (adPending) return;
            adPending = true;
            adBuyBtn.interactable = false;
            closeBtn.interactable = false;
            int generation = openGeneration;
            string placement = _itemType switch
            {
                E_ItemType.GameProp_1 => "GameProp_1",
                E_ItemType.GameProp_2 => "GameProp_2",
                E_ItemType.GameProp_3 => "GameProp_3",
                _ => "GameProp"
            };
            BizzaSdk.Ad.ShowRewardAd(placement, WithdrawalUtil.GetDollarCountByReward(), result =>
            {
                if (this == null || generation != openGeneration || !adPending) return;
                adPending = false;
                adBuyBtn.interactable = true;
                closeBtn.interactable = true;
                OnAdBuyFinish(result);
            });
            UIModule.Instance.m_curadvertistics--;
        });

        closeBtn.onClick.AddListener(() => { CloseSelf(); });
    }

    private void OnAdBuyFinish(Bizza.Sdk.ShowAdResult showAdResult)
    {
        bool success = showAdResult.success;
        if (success)
        {
            //NumbericalStatistics.AddPropTimesByEnum(_itemType);
            AddPropAndClose();
        }
    }

    private void AddPropAndClose()
    {
        ItemUtils.AddItem(_itemType, 1, true, true, propIcon.transform.position, false);
        CloseSelf();
    }

    protected override void OnOpen(E_ItemType itemType)
    {
        openGeneration++;
        adPending = false;
        adBuyBtn.interactable = closeBtn.interactable = true;
        _itemType = itemType;
        var config = propConfigSO.GetPropConfigInfo(itemType);
        if (config != null)
        {
            propIcon.sprite = config.propIcon;
        }
        propName.text = BubblePics.Localization.Tr(itemType switch
        {
            E_ItemType.GameProp_1 => "BUBBLE_TOOL_HINT",
            E_ItemType.GameProp_2 => "BUBBLE_TOOL_DROP",
            E_ItemType.GameProp_3 => "BUBBLE_TOOL_MAGNET",
            _ => throw new ArgumentOutOfRangeException(nameof(itemType))
        });
        if (authoredHintVisual != null)
        {
            authoredHintVisual.SetActive(itemType == E_ItemType.GameProp_1);
            propIcon.gameObject.SetActive(itemType != E_ItemType.GameProp_1);
            propName.text = BubblePics.Localization.Tr(itemType == E_ItemType.GameProp_1 ? "seq_need_hint" : "seq_more_tools");
        }
        if (descriptionText != null) descriptionText.text = BubblePics.Localization.Tr(itemType switch
        {
            E_ItemType.GameProp_1 => "seq_hint_description",
            E_ItemType.GameProp_2 => "seq_drop_description",
            _ => "seq_magnet_description"
        });
        var _propUseTimes = NumbericalStatistics._propUseTimes;
        var maxTimes = config.preLimitNum;
        _propUseTimes.TryGetValue(itemType, out var curTimes);
        limitTxt.gameObject.SetActive(maxTimes != int.MaxValue);
        limitTxt.text = curTimes + " / " + maxTimes;
    }

    protected override void OnClose()
    {
        openGeneration++;
        adPending = false;
    }
}
#endif
