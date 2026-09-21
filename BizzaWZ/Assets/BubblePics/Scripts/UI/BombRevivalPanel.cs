using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Port of bomb_revival_panel.tscn. Bomb expiry cannot be revived with
    /// moves, so both actions restart the current level like the reference.
    /// </summary>
    public sealed class BombRevivalPanel : MonoBehaviour
    {
        const float APPEAR_DURATION = 0.28f;

        public BubblePage Page { get; set; }
        public bool Visible { get; private set; }

        [SerializeField] RectTransform _root;
        [SerializeField] Image _dim;
        [SerializeField] RectTransform _dialog;
        [SerializeField] Image _card;
        [SerializeField] Image _slot;
        [SerializeField] Image _revival;
        [SerializeField] TMP_Text _titleBack;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _subtitle;
        [SerializeField] CanvasGroup _dimGroup;
        [SerializeField] CanvasGroup _dialogGroup;
        [SerializeField] CommonButton _restartButton;
        [SerializeField] PressButton _closeButton;

        public RectTransform Root => _root;
        public bool HasAuthoredHierarchy => HasPrefabReferences();

        public void Build(Transform parent)
        {
            if (HasPrefabReferences())
            {
                InitializePrefabRuntime(Page, parent);
                HidePanel();
                return;
            }

            _root = UiFactory.FullStretch((RectTransform)parent, "BombRevivalPanel");

            _dim = UiFactory.Rect(_root, "Dim", new Color(0f, 0f, 0f, 0.8f), 0f, 0f);
            _dim.rectTransform.anchorMin = Vector2.zero;
            _dim.rectTransform.anchorMax = Vector2.one;
            _dim.rectTransform.offsetMin = Vector2.zero;
            _dim.rectTransform.offsetMax = Vector2.zero;
            _dim.raycastTarget = true;
            _dimGroup = _dim.gameObject.AddComponent<CanvasGroup>();

            _dialog = UiFactory.Node(_root, "Dialog");
            _dialog.sizeDelta = new Vector2(1080f, 1222f);
            _dialogGroup = _dialog.gameObject.AddComponent<CanvasGroup>();

            _card = UiFactory.Img(
                _dialog,
                "Card",
                "Art/Sprites/Setting/dialog_card_bg_blue_9",
                1080f,
                1222f);
            _card.sprite = AssetLib.Sprite9Design(
                "Art/Sprites/Setting/dialog_card_bg_blue_9",
                270f,
                332f,
                270f,
                188f);
            _card.type = Image.Type.Sliced;

            _titleBack = UiFactory.Label(
                _dialog,
                "TitleBack",
                Localization.Tr("continue"),
                96,
                new Color32(0xB6, 0xD7, 0xF7, 0xFF),
                AssetLib.UiFont,
                TextAnchor.MiddleCenter,
                728f,
                172f);
            _titleBack.rectTransform.anchorMin = _titleBack.rectTransform.anchorMax =
                new Vector2(0.5f, 1f);
            _titleBack.rectTransform.anchoredPosition = new Vector2(0f, -133f);
            TmpTextStyle.ApplyOutline(
                _titleBack,
                new Color32(0x00, 0x31, 0x5C, 0xFF),
                32f);

            _title = UiFactory.Label(
                _dialog,
                "Title",
                Localization.Tr("continue"),
                96,
                Color.white,
                AssetLib.UiFont,
                TextAnchor.MiddleCenter,
                728f,
                172f);
            _title.rectTransform.anchorMin = _title.rectTransform.anchorMax =
                new Vector2(0.5f, 1f);
            _title.rectTransform.anchoredPosition = new Vector2(0f, -130f);
            TmpTextStyle.ApplyShadow(
                _title,
                new Color32(0xBF, 0xDF, 0xFF, 0xFF),
                new Vector2(0f, -3f));

            RectTransform imageSlot = UiFactory.Node(_dialog, "ImageSlot");
            imageSlot.anchorMin = imageSlot.anchorMax = new Vector2(0.5f, 1f);
            imageSlot.sizeDelta = new Vector2(796f, 368f);
            imageSlot.anchoredPosition = new Vector2(0f, -472f);
            _slot = UiFactory.Img(
                imageSlot,
                "Background",
                "Art/Sprites/Setting/dialog_card_slot");
            _slot.rectTransform.anchorMin = Vector2.zero;
            _slot.rectTransform.anchorMax = Vector2.one;
            _slot.rectTransform.offsetMin = Vector2.zero;
            _slot.rectTransform.offsetMax = Vector2.zero;
            _slot.sprite = AssetLib.Sprite9Design(
                "Art/Sprites/Setting/dialog_card_slot",
                90f,
                90f,
                90f,
                90f);
            _slot.type = Image.Type.Sliced;

            _revival = UiFactory.Img(
                imageSlot,
                "RevivalImage",
                "Art/Sprites/Bubble/revival_popup_img");
            _revival.rectTransform.anchorMin = Vector2.zero;
            _revival.rectTransform.anchorMax = Vector2.one;
            _revival.rectTransform.offsetMin = Vector2.zero;
            _revival.rectTransform.offsetMax = Vector2.zero;
            _revival.preserveAspect = true;

            _subtitle = UiFactory.Label(
                _dialog,
                "Subtitle",
                Localization.Tr("bomb_exploded"),
                52,
                new Color32(0x66, 0x4C, 0x43, 0xFF),
                AssetLib.UiFont,
                TextAnchor.MiddleCenter,
                856f,
                62f);
            _subtitle.rectTransform.anchorMin = _subtitle.rectTransform.anchorMax =
                new Vector2(0.5f, 1f);
            _subtitle.rectTransform.anchoredPosition = new Vector2(0f, -719f);
            _subtitle.enableWordWrapping = true;

            _restartButton = CommonButton.Create(
                _dialog,
                "RestartButton",
                CommonButton.Variant.Orange,
                Localization.Tr("restart"));
            _restartButton.Root.anchorMin = _restartButton.Root.anchorMax =
                new Vector2(0.5f, 1f);
            _restartButton.Root.sizeDelta = new Vector2(700f, 240f);
            _restartButton.Root.anchoredPosition = new Vector2(0f, -926f);
            _restartButton.OnClick = Restart;

            Image close = UiFactory.Img(
                _dialog,
                "CloseButton",
                "Art/Sprites/Continue/close_x",
                144f,
                144f);
            close.rectTransform.anchorMin = close.rectTransform.anchorMax =
                new Vector2(0.5f, 1f);
            close.rectTransform.anchoredPosition = new Vector2(420f, -148f);
            UiFactory.MakeButton(close.gameObject, Restart);
            _closeButton = close.GetComponent<PressButton>();

            BindPrefabRuntime();
            _root.gameObject.SetActive(false);
        }

        bool HasPrefabReferences()
        {
            return _root != null && _dim != null && _dialog != null &&
                   _card != null && _slot != null && _revival != null &&
                   _titleBack != null && _title != null && _subtitle != null &&
                   _dimGroup != null && _dialogGroup != null &&
                   _restartButton != null && _closeButton != null;
        }

        /// <summary>Injects the owner and binds the fixed BombRevivalPanel prefab hierarchy.</summary>
        public void InitializePrefabRuntime(BubblePage page, Transform parent = null)
        {
            Page = page;
            BindPrefabRuntime();
            if (parent != null && _root != null && _root != parent &&
                !_root.IsChildOf(parent))
            {
                _root.SetParent(parent, false);
            }
            HidePanel();
        }

        public void BindPrefabRuntime()
        {
            if (_root == null)
                _root = FindNamed<RectTransform>(transform, "BombRevivalPanel", true);
            if (_root == null) return;

            _dim ??= FindNamed<Image>(_root, "Dim");
            _dialog ??= FindNamed<RectTransform>(_root, "Dialog");
            _card ??= FindNamed<Image>(_dialog, "Card");
            _slot ??= FindNamed<Image>(_dialog, "Background");
            _revival ??= FindNamed<Image>(_dialog, "RevivalImage");
            _titleBack ??= FindNamed<TMP_Text>(_dialog, "TitleBack");
            _title ??= FindNamed<TMP_Text>(_dialog, "Title");
            _subtitle ??= FindNamed<TMP_Text>(_dialog, "Subtitle");
            _restartButton ??= FindNamed<CommonButton>(_dialog, "RestartButton");
            _closeButton ??= FindNamed<PressButton>(_dialog, "CloseButton");
            _dimGroup ??= _dim != null ? _dim.GetComponent<CanvasGroup>() : null;
            _dialogGroup ??= _dialog != null ? _dialog.GetComponent<CanvasGroup>() : null;

            string continueText = Localization.Tr("continue");
            if (_titleBack != null)
            {
                _titleBack.text = continueText;
                TmpTextStyle.ApplyOutline(
                    _titleBack,
                    new Color32(0x00, 0x31, 0x5C, 0xFF),
                    32f);
            }
            if (_title != null)
            {
                _title.text = continueText;
                TmpTextStyle.ApplyShadow(
                    _title,
                    new Color32(0xBF, 0xDF, 0xFF, 0xFF),
                    new Vector2(0f, -3f));
            }
            if (_subtitle != null)
                _subtitle.text = Localization.Tr("bomb_exploded");

            if (_restartButton != null)
            {
                _restartButton.BindPrefabRuntime();
                _restartButton.SetText(Localization.Tr("restart"));
                _restartButton.OnClick = Restart;
            }
            if (_closeButton != null)
                _closeButton.OnClick = Restart;
        }

        static T FindNamed<T>(Transform parent, string objectName, bool excludeRoot = false)
            where T : Component
        {
            if (parent == null) return null;
            foreach (T component in parent.GetComponentsInChildren<T>(true))
            {
                if (excludeRoot && component.transform == parent) continue;
                if (component.name == objectName) return component;
            }
            return null;
        }

        public void PlayAppear()
        {
            if (_root == null) return;
            StopAllCoroutines();
            Visible = true;
            _root.gameObject.SetActive(true);
            StartCoroutine(AppearCo());
        }

        public void HidePanel()
        {
            Visible = false;
            StopAllCoroutines();
            if (_root != null) _root.gameObject.SetActive(false);
        }

        IEnumerator AppearCo()
        {
            _dimGroup.alpha = 0f;
            _dialogGroup.alpha = 0f;
            _dialog.localScale = Vector3.one * 0.8f;
            float elapsed = 0f;
            while (elapsed < APPEAR_DURATION)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / APPEAR_DURATION);
                _dimGroup.alpha = raw;
                _dialogGroup.alpha = raw;
                float scale = Mathf.LerpUnclamped(
                    0.8f,
                    1f,
                    Tween.Evaluate(Ease.OutBack, raw));
                _dialog.localScale = Vector3.one * scale;
                yield return null;
            }
            _dimGroup.alpha = 1f;
            _dialogGroup.alpha = 1f;
            _dialog.localScale = Vector3.one;
        }

        void Restart()
        {
            Page?.OnBombRevivalRestart();
        }
    }
}
