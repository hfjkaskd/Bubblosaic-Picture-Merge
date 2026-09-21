using UnityEngine;
using UnityEngine.UI;

/// <summary>Standard Button interaction with the existing withdrawal callbacks and state.</summary>
[DisallowMultipleComponent]
public sealed class WithdrawCloudButton : Button
{
    [SerializeField] private BizzaButton legacyOwner;

    protected override void Awake()
    {
        base.Awake();
        onClick.AddListener(ForwardClick);
    }

    public override bool IsInteractable()
    {
        return base.IsInteractable() && legacyOwner != null &&
               legacyOwner.isActiveAndEnabled && legacyOwner.interactable;
    }

    private void ForwardClick()
    {
        if (!IsInteractable()) return;
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX("SFX_Click");
        legacyOwner.onClick.Invoke();
    }

    protected override void OnDestroy()
    {
        onClick.RemoveListener(ForwardClick);
        base.OnDestroy();
    }
}
