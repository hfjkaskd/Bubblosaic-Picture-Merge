#if BIZZA_REAL_WITHDRAW
using System;
using UnityEngine;
using UnityEngine.UI;

// All visuals and layout are authored in the option prefab. Runtime only binds a server option.
public sealed class NewPlayerPayoutOption : MonoBehaviour
{
    public Button button;
    public Image otherLogo;
    public GameObject paypalLogo;
    public GameObject selection;
    public PaymentConfig paymentConfig;
    public AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform Data { get; private set; }
    private Action<NewPlayerPayoutOption> selected;
    private void Awake() { button.onClick.AddListener(Select); }
    public void Bind(AccountModule.OceanShineWithdrawalPageResponse.WithdrawalPlatform data, Action<NewPlayerPayoutOption> onSelect)
    {
        Data=data;selected=onSelect;
        bool paypal=data.Os_Cn==UIWithdrawalPanel.paypalInfo;
        paypalLogo.SetActive(paypal);otherLogo.gameObject.SetActive(!paypal);
        if(!paypal)otherLogo.sprite=paymentConfig.GetSpriteByIconKey(data.Os_Cn);
        SetSelected(false);
    }
    public void SetSelected(bool value) { selection.SetActive(value); }
    private void Select() { selected?.Invoke(this); }
}
#endif
