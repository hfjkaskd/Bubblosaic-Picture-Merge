#if BIZZA_REAL_WITHDRAW
using System;
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
    public RectTransform rectTransform;
    [SerializeField] private BizzaButton closeButton;
    [SerializeField] private GameObject loadingHint;
    [SerializeField] private GameObject errorHint;
    [SerializeField] private ScrollRect scrollView;
    [SerializeField] private BusinessPanelStatusView loadingStatus;
    private readonly List<WithdrawHistoryItem> items = new();
    public IReadOnlyList<WithdrawHistoryItem> Rows => items;
    public bool IsRefreshing => contentState == ContentState.Loading;
    private enum ContentState { Closed, Loading, Ready, Empty, Failed }
    private ContentState contentState;
    private const float LoadTimeoutSeconds = 15f;
    private int requestVersion;
    private float loadStartedAt;
    private CanvasGroup listGroup;
#if UNITY_EDITOR
    internal static Action<Action<FailHttpResponse<List<AccountModule.OceanShineWithdrawalRecord>>>> EditorRequestHistory;
#endif

    protected override void OnAwake()
    {
        base.OnAwake();
        closeButton.onClick.AddListener(CloseSelf);
        // The prefab no longer has ScrollViewClearChilds; this page owns the row pool.
        items.AddRange(root.GetComponentsInChildren<WithdrawHistoryItem>(true));
        foreach (var row in items) row.gameObject.SetActive(false);
        if (scrollView == null) scrollView = root.GetComponentInParent<ScrollRect>();
        listGroup = scrollView.GetComponent<CanvasGroup>();
        loadingStatus.Bind(OnRefresh, true);
        SetContentState(ContentState.Closed);
    }

    protected override void OnOpen() => OnRefresh();

    private void OnRefresh()
    {
        if (IsClosing || !gameObject.activeInHierarchy) return;
        int version = ++requestVersion;
        loadStartedAt = Time.unscaledTime;
        scrollView.StopMovement();
        SetContentState(ContentState.Loading);
        try
        {
            Action<FailHttpResponse<List<AccountModule.OceanShineWithdrawalRecord>>> reply = response =>
            {
                if (AcceptResponse(version)) ApplyResponse(response);
            };
#if UNITY_EDITOR
            if (EditorRequestHistory != null) EditorRequestHistory(reply);
            else
#endif
                AccountModule.Instance.Request_WithdrawalRecordRequest(reply);
        }
        catch (Exception exception)
        {
            if (AcceptResponse(version)) SetContentState(ContentState.Failed);
            Debug.LogException(exception);
        }
    }

    private bool AcceptResponse(int version) => this != null && version == requestVersion &&
        !IsClosing && gameObject.activeInHierarchy && contentState == ContentState.Loading;

    public void ApplyResponse(FailHttpResponse<List<AccountModule.OceanShineWithdrawalRecord>> response)
    {
        if (!AcceptResponse(requestVersion)) return;
        if (!response.success || response.errorCode != "200" ||
            (response.data != null && response.data.Exists(record => record == null)))
        {
            SetContentState(ContentState.Failed);
            return;
        }
        try
        {
            SetRecords(response.data);
            SetContentState(response.data == null || response.data.Count == 0 ? ContentState.Empty : ContentState.Ready);
        }
        catch (Exception exception)
        {
            SetContentState(ContentState.Failed);
            Debug.LogException(exception);
        }
    }

    public void SetRecords(IReadOnlyList<AccountModule.OceanShineWithdrawalRecord> records)
    {
        int count = records == null ? 0 : records.Count;
        items.RemoveAll(row => row == null);
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

    private void SetContentState(ContentState state)
    {
        contentState = state;
        bool ready = state == ContentState.Ready;
        listGroup.alpha = ready ? 1f : 0f;
        listGroup.interactable = listGroup.blocksRaycasts = ready;
        emptyHint.SetActive(state == ContentState.Empty);
        if (loadingHint != null) loadingHint.SetActive(false);
        if (errorHint != null) errorHint.SetActive(false);
        if (state == ContentState.Loading || state == ContentState.Failed)
            loadingStatus.Show(state == ContentState.Failed);
        else loadingStatus.Hide();
    }

    private void Update()
    {
        if (!IsClosing && IsRefreshing && Time.unscaledTime - loadStartedAt >= LoadTimeoutSeconds)
            SetContentState(ContentState.Failed);
    }

    protected override void OnClose()
    {
        requestVersion++;
        SetContentState(ContentState.Closed);
    }
}
#endif
