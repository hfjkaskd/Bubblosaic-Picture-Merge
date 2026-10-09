using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UIPageIds
{
    public static readonly PageId WhiteWinPanel = "WhiteWinPanel";
}
namespace SnakeEscape.Recovered
{
    [DisallowMultipleComponent]
    public sealed class RecoveredVictoryPanel : UIPageBase<Action>
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform hero;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private Button nextButton;
        [SerializeField] private RecoveredVictoryPanelSkin skin;
        [SerializeField] private string completedTitle = "Level Complete";
        [SerializeField] private string completedBody = "Puzzle complete!";
        private Action nextAction;
        private bool advancing;
        public override bool PreserveRectTransformOnOpen => true;
        protected override void OnAwake()
        {
            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
        }
        protected override void OnOpen(Action onNext) { Show(BubblePics.Localization.Tr("ui_level_complete"), BubblePics.Localization.Tr("ui_puzzle_complete"), onNext); }
        public void Show(string title, string body, Action onNext)
        {
            nextAction = onNext; advancing = false;
            titleText.text = string.IsNullOrEmpty(title) ? completedTitle : title;
            bodyText.text = string.IsNullOrEmpty(body) ? completedBody : body;
            nextButton.interactable = true;
            gameObject.SetActive(true);
            if (canvasGroup != null) { canvasGroup.alpha = 1; canvasGroup.interactable = true; canvasGroup.blocksRaycasts = true; }
        }
        protected override void OnClose() { nextAction = null; }
        public void Hide()
        {
            nextAction = null;
            if (canvasGroup != null) { canvasGroup.alpha = 0; canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
            gameObject.SetActive(false);
        }
        private void OnNextClicked()
        {
            if (advancing) return;
            advancing = true; nextButton.interactable = false;
            Action callback = nextAction;
            if (UIManager.Instance != null && UIManager.Instance.GetPage(UIPageIds.WhiteWinPanel) == this) CloseSelf(); else Hide();
            callback?.Invoke();
            FlowModule.LoadGameLevel();
        }
    }
}
