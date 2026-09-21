using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Fixed, prefab-authored presentation for bubble_tutorial.tscn. Runtime
    /// code supplies only the target bubble positions and translated text.
    /// </summary>
    public sealed class BubbleTutorialView : MonoBehaviour
    {
        const int HAND_SORTING_ORDER = 200;

        [SerializeField] Transform _handMount;
        [SerializeField] SpineLite.SpineSprite _hand;
        [SerializeField] RectTransform _toastContainer;
        [SerializeField] Image _toastBackground;
        [SerializeField] TMP_Text _toastLabel;
        [SerializeField] CanvasGroup _toastLabelGroup;
        [SerializeField] TutorialAmbientBubbles _ambientBubbles;

        public Transform HandTransform => _hand != null ? _hand.transform : _handMount;
        public SpineLite.SpineSprite Hand => _hand;
        public RectTransform ToastContainer => _toastContainer;
        public Image ToastBackground => _toastBackground;
        public TMP_Text ToastLabel => _toastLabel;
        public CanvasGroup ToastLabelGroup => _toastLabelGroup;
        public TutorialAmbientBubbles AmbientBubbles => _ambientBubbles;

        public bool HasRequiredReferences =>
            _handMount != null &&
            _hand != null &&
            _toastContainer != null &&
            _toastBackground != null &&
            _toastLabel != null &&
            _toastLabelGroup != null &&
            _ambientBubbles != null;

        public bool InitializeRuntime()
        {
            if (!HasRequiredReferences)
            {
                Debug.LogError("BubbleTutorial.prefab is missing one or more authored references.");
                return false;
            }

            if (_hand.Data == null)
                _hand.Load("hand");
            _hand.SortingOrder = HAND_SORTING_ORDER;
            _hand.Tint = new Color(1f, 1f, 1f, 0f);
            _hand.gameObject.SetActive(false);

            _toastContainer.gameObject.SetActive(false);
            _toastContainer.localScale = Vector3.one;
            _toastBackground.rectTransform.localScale = Vector3.one;
            _toastLabelGroup.alpha = 1f;
            _ambientBubbles.InitializeRuntime();
            return true;
        }

        public void LayoutToast()
        {
            if (_toastContainer == null) return;
            var width = App.I != null && App.I.HudRoot != null
                ? App.I.HudRoot.rect.width
                : App.DesignW;
            var scale = width > 0f ? width / App.DesignW : 1f;
            _toastContainer.anchoredPosition = Vector2.zero;
            _toastContainer.localScale = new Vector3(scale, scale, 1f);
        }

#if UNITY_EDITOR
        public void ConfigurePrefabAuthoring(
            Transform handMount,
            SpineLite.SpineSprite hand,
            RectTransform toastContainer,
            Image toastBackground,
            TMP_Text toastLabel,
            CanvasGroup toastLabelGroup,
            TutorialAmbientBubbles ambientBubbles)
        {
            _handMount = handMount;
            _hand = hand;
            _toastContainer = toastContainer;
            _toastBackground = toastBackground;
            _toastLabel = toastLabel;
            _toastLabelGroup = toastLabelGroup;
            _ambientBubbles = ambientBubbles;
        }
#endif
    }
}
