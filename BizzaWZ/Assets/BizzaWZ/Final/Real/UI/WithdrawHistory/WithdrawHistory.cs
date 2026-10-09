#if BIZZA_REAL_WITHDRAW
using System;
using System.Collections;
using System.Collections.Generic;
using Bizza;
using UnityEngine;
using UnityEngine.UI;

public partial class UIPageIds
{
    public static readonly PageId WithdrawHistory = "WithdrawHistory";
}


public class WithdrawHistory : UIPageBase
{
    public WithdrawHistoryItem item;
    public Transform root;
    public GameObject emptyHint;

    private readonly List<WithdrawHistoryItem> items = new List<WithdrawHistoryItem>();
    public IReadOnlyList<WithdrawHistoryItem> Rows => items;
    public bool IsRefreshing { get; private set; }
    [SerializeField] private GameObject loadingHint;
    [SerializeField] private GameObject errorHint;
    [SerializeField] private ScrollRect scrollView;
    private int requestVersion;

    public RectTransform rectTransform;

    [SerializeField] private BizzaButton closeButton;
    protected override void OnAwake()
    {
        base.OnAwake();
        closeButton.onClick.AddListener(() => { CloseSelf(); });
    }

    protected override void OnOpen()
    {
        OnRefresh();
    }

    private void OnRefresh()
    {
        int version = ++requestVersion;
        IsRefreshing = true;
        SetRecords(null);
        emptyHint.SetActive(false);
        if (loadingHint != null) loadingHint.SetActive(true);
        if (errorHint != null) errorHint.SetActive(false);
        AccountModule.Instance.Request_WithdrawalRecordRequest(response =>
        {
            if (this && version == requestVersion) ApplyResponse(response);
        });
    }

    public void ApplyResponse(FailHttpResponse<List<AccountModule.OceanShineWithdrawalRecord>> response)
    {
        IsRefreshing = false;
        if (loadingHint != null) loadingHint.SetActive(false);
        if (errorHint != null) errorHint.SetActive(!response.success);
        SetRecords(response.success ? response.data : null);
        if (!response.success) emptyHint.SetActive(false);
    }

    public void SetRecords(IReadOnlyList<AccountModule.OceanShineWithdrawalRecord> records)
    {
        int count = records == null ? 0 : records.Count;
        items.SetCmptListCount(item, root, count);
        for (int i = 0; i < count; i++) items[i].Init(records[i]);
        emptyHint.SetActive(count == 0);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        if (scrollView != null)
        {
            scrollView.StopMovement();
            scrollView.verticalNormalizedPosition = 1f;
        }
    }

    protected override void OnClose()
    {
        requestVersion++;
        IsRefreshing = false;
        emptyHint.SetActive(false);
    }

}
#endif
