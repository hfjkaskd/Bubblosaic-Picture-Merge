#if BIZZA_REAL_WITHDRAW
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Authored in each game's prefab; retains its fonts, colors and button artwork.
public sealed class BusinessPanelStatusView : MonoBehaviour
{
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button retryButton;
    [SerializeField] private TMP_Text retryLabel;
    [SerializeField] private DailyMissionLoadingSpinner spinner;
    private bool failed;
    private bool history;
    private Action retry;

    public void Bind(Action onRetry, bool isHistory)
    {
        retry = onRetry;
        history = isHistory;
        retryButton.onClick.RemoveListener(Retry);
        retryButton.onClick.AddListener(Retry);
    }
    private void Retry() { if (failed) retry?.Invoke(); }
    public void Show(bool hasFailed)
    {
        failed = hasFailed;
        gameObject.SetActive(true);
        spinner.gameObject.SetActive(!failed);
        retryButton.gameObject.SetActive(failed);
        RefreshText();
    }
    public void Hide() => gameObject.SetActive(false);
    private void OnEnable() => BizzaEventSystem.Set(EventDefine.Frame.LanguageChange, RefreshText, true);
    private void OnDisable() => BizzaEventSystem.Set(EventDefine.Frame.LanguageChange, RefreshText, false);
    private void RefreshText()
    {
        message.text = !failed
            ? BusinessPanelLoadingText.Select("Loading...", "Carregando...", "Memuat...")
            : history
                ? BusinessPanelLoadingText.Select("Couldn't load withdrawal history.\nPlease try again.", "Não foi possível carregar o histórico.\nTente novamente.", "Gagal memuat riwayat penarikan.\nSilakan coba lagi.")
                : BusinessPanelLoadingText.Select("Couldn't load rewards.\nPlease try again.", "Não foi possível carregar.\nTente novamente.", "Gagal memuat hadiah.\nSilakan coba lagi.");
        retryLabel.text = BusinessPanelLoadingText.Select("Try again", "Tentar novamente", "Coba lagi");
    }
    private void Update()
    {
        if (!failed) spinner.rectTransform.Rotate(0f, 0f, -240f * Time.unscaledDeltaTime);
    }
}
#endif
