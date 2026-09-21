using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>
    /// Port of bomb_exit_confirm_panel.tscn. Timed-bomb rounds ask for a
    /// second confirmation before leaving for home so the active round is not
    /// discarded by an accidental settings tap.
    /// </summary>
    public sealed class BombExitConfirmPanel : MonoBehaviour
    {
        const float AppearDuration = 0.28f;
        const float SparkLifetime = 0.6f;
        const int SparkCount = 20;

        [SerializeField] RectTransform _root;
        [SerializeField] Image _dim;
        [SerializeField] RectTransform _dialog;
        [SerializeField] Image _card;
        [SerializeField] Image _slot;
        [SerializeField] Image _exitImage;
        [SerializeField] TMP_Text _titleBack;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _subtitle;
        [SerializeField] CanvasGroup _dimGroup;
        [SerializeField] CanvasGroup _dialogGroup;
        [SerializeField] CommonButton _exitButton;
        [SerializeField] CommonButton _stayButton;
        [SerializeField] PressButton _closeButton;
        [SerializeField] RectTransform _sparkRoot;
        [SerializeField] Image[] _sparks;

        public BubblePage Page { get; set; }
        public bool Visible { get; private set; }
        public RectTransform Root => _root;
        public bool HasAuthoredHierarchy => HasPrefabReferences();
        public int SparkCountAuthored => _sparks?.Length ?? 0;

        public void Build(Transform parent)
        {
            if (HasPrefabReferences())
            {
                InitializePrefabRuntime(Page, parent);
                HidePanel();
                return;
            }

            _root = UiFactory.FullStretch(
                (RectTransform)parent,
                "BombExitConfirmPanel");

            _dim = UiFactory.Rect(
                _root,
                "Dim",
                new Color(0f, 0f, 0f, 0.8f),
                0f,
                0f);
            _dim.rectTransform.anchorMin = Vector2.zero;
            _dim.rectTransform.anchorMax = Vector2.one;
            _dim.rectTransform.offsetMin = Vector2.zero;
            _dim.rectTransform.offsetMax = Vector2.zero;
            _dim.raycastTarget = true;
            _dimGroup = _dim.gameObject.AddComponent<CanvasGroup>();

            _dialog = UiFactory.Node(_root, "Dialog");
            _dialog.sizeDelta = new Vector2(1080f, 1524f);
            _dialogGroup = _dialog.gameObject.AddComponent<CanvasGroup>();

            _card = UiFactory.Img(
                _dialog,
                "Card",
                "Art/Sprites/Setting/dialog_card_bg_blue_9",
                1080f,
                1524f);
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
                Localization.Tr("exting_dialog_title"),
                96,
                new Color32(0xB6, 0xD7, 0xF7, 0xFF),
                AssetLib.UiFont,
                TextAnchor.MiddleCenter,
                728f,
                172f);
            _titleBack.rectTransform.anchorMin =
                _titleBack.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _titleBack.rectTransform.anchoredPosition = new Vector2(0f, -133f);
            TmpTextStyle.ApplyOutline(
                _titleBack,
                new Color32(0x00, 0x31, 0x5C, 0xFF),
                32f);

            _title = UiFactory.Label(
                _dialog,
                "Title",
                Localization.Tr("exting_dialog_title"),
                96,
                Color.white,
                AssetLib.UiFont,
                TextAnchor.MiddleCenter,
                728f,
                172f);
            _title.rectTransform.anchorMin =
                _title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
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

            _exitImage = UiFactory.Img(
                imageSlot,
                "ExitImage",
                "Art/Sprites/Bubble/revival_popup_exit");
            _exitImage.rectTransform.anchorMin = Vector2.zero;
            _exitImage.rectTransform.anchorMax = Vector2.one;
            _exitImage.rectTransform.offsetMin = new Vector2(0f, -36f);
            _exitImage.rectTransform.offsetMax = new Vector2(0f, -36f);
            _exitImage.preserveAspect = true;
            _exitImage.rectTransform.localScale = Vector3.one * 1.2f;

            _subtitle = UiFactory.Label(
                _dialog,
                "Subtitle",
                string.Empty,
                52,
                new Color32(0x66, 0x4C, 0x43, 0xFF),
                AssetLib.UiFont,
                TextAnchor.MiddleCenter,
                856f,
                124f);
            _subtitle.rectTransform.anchorMin =
                _subtitle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _subtitle.rectTransform.anchoredPosition = new Vector2(0f, -750f);
            _subtitle.enableWordWrapping = true;
            _subtitle.richText = true;

            _exitButton = CommonButton.Create(
                _dialog,
                "ExitButton",
                CommonButton.Variant.Orange,
                Localization.Tr("exting_dialog_exit"));
            PositionButton(_exitButton, -988f);
            _exitButton.OnClick = Exit;

            _stayButton = CommonButton.Create(
                _dialog,
                "StayButton",
                CommonButton.Variant.Blue,
                Localization.Tr("exting_dialog_stay"));
            PositionButton(_stayButton, -1228f);
            _stayButton.OnClick = Stay;

            Image close = UiFactory.Img(
                _dialog,
                "CloseButton",
                "Art/Sprites/Setting/btn_close_red",
                144f,
                144f);
            close.rectTransform.anchorMin = close.rectTransform.anchorMax =
                new Vector2(0.5f, 1f);
            close.rectTransform.anchoredPosition = new Vector2(420f, -148f);
            UiFactory.MakeButton(close.gameObject, Stay);
            _closeButton = close.GetComponent<PressButton>();

            BuildSparkNodes();
            BindPrefabRuntime();
            _root.gameObject.SetActive(false);
        }

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
            {
                _root = FindNamed<RectTransform>(
                    transform,
                    "BombExitConfirmPanel",
                    true);
            }
            if (_root == null) return;

            _dim ??= FindNamed<Image>(_root, "Dim");
            _dialog ??= FindNamed<RectTransform>(_root, "Dialog");
            _card ??= FindNamed<Image>(_dialog, "Card");
            _slot ??= FindNamed<Image>(_dialog, "Background");
            _exitImage ??= FindNamed<Image>(_dialog, "ExitImage");
            _titleBack ??= FindNamed<TMP_Text>(_dialog, "TitleBack");
            _title ??= FindNamed<TMP_Text>(_dialog, "Title");
            _subtitle ??= FindNamed<TMP_Text>(_dialog, "Subtitle");
            _exitButton ??= FindNamed<CommonButton>(_dialog, "ExitButton");
            _stayButton ??= FindNamed<CommonButton>(_dialog, "StayButton");
            _closeButton ??= FindNamed<PressButton>(_dialog, "CloseButton");
            _sparkRoot ??= FindNamed<RectTransform>(_dialog, "SparkFx");
            _dimGroup ??= _dim != null ? _dim.GetComponent<CanvasGroup>() : null;
            _dialogGroup ??=
                _dialog != null ? _dialog.GetComponent<CanvasGroup>() : null;
            if ((_sparks == null || _sparks.Length == 0) && _sparkRoot != null)
            {
                _sparks = _sparkRoot.GetComponentsInChildren<Image>(true);
            }

            string title = Localization.Tr("exting_dialog_title");
            if (_titleBack != null)
            {
                _titleBack.text = title;
                TmpTextStyle.ApplyOutline(
                    _titleBack,
                    new Color32(0x00, 0x31, 0x5C, 0xFF),
                    32f);
            }
            if (_title != null)
            {
                _title.text = title;
                TmpTextStyle.ApplyShadow(
                    _title,
                    new Color32(0xBF, 0xDF, 0xFF, 0xFF),
                    new Vector2(0f, -3f));
            }
            if (_subtitle != null)
            {
                _subtitle.richText = true;
                _subtitle.text = SubtitleText();
            }
            if (_exitImage != null)
                _exitImage.rectTransform.localScale = Vector3.one * 1.2f;

            if (_exitButton != null)
            {
                _exitButton.BindPrefabRuntime();
                _exitButton.SetText(Localization.Tr("exting_dialog_exit"));
                _exitButton.OnClick = Exit;
            }
            if (_stayButton != null)
            {
                _stayButton.BindPrefabRuntime();
                _stayButton.SetText(Localization.Tr("exting_dialog_stay"));
                _stayButton.OnClick = Stay;
            }
            if (_closeButton != null)
                _closeButton.OnClick = Stay;
        }

        public void PlayAppear()
        {
            if (_root == null) return;
            StopAllCoroutines();
            Visible = true;
            _root.gameObject.SetActive(true);
            if (_sparkRoot != null) _sparkRoot.gameObject.SetActive(true);
            StartCoroutine(AppearCo());
            if (_sparks != null && _sparks.Length > 0)
                StartCoroutine(SparkCo());
        }

        public void HidePanel()
        {
            Visible = false;
            StopAllCoroutines();
            if (_sparkRoot != null) _sparkRoot.gameObject.SetActive(false);
            if (_root != null) _root.gameObject.SetActive(false);
        }

        IEnumerator AppearCo()
        {
            _dimGroup.alpha = 0f;
            _dialogGroup.alpha = 0f;
            _dialog.localScale = Vector3.one * 0.8f;
            float elapsed = 0f;
            while (elapsed < AppearDuration)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / AppearDuration);
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

        IEnumerator SparkCo()
        {
            int count = _sparks.Length;
            var ages = new float[count];
            var velocities = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                ResetSpark(i, ages, velocities, Random.Range(0f, SparkLifetime));
            }

            while (Visible)
            {
                float delta = Time.deltaTime;
                for (int i = 0; i < count; i++)
                {
                    Image spark = _sparks[i];
                    if (spark == null) continue;
                    ages[i] += delta;
                    if (ages[i] >= SparkLifetime)
                        ResetSpark(i, ages, velocities, 0f);

                    float normalized = Mathf.Clamp01(ages[i] / SparkLifetime);
                    float alpha = normalized < 0.15f
                        ? normalized / 0.15f
                        : 1f - (normalized - 0.15f) / 0.85f;
                    Color color = new Color(1f, 0.55f, 0.15f, alpha);
                    spark.color = color;
                    spark.rectTransform.anchoredPosition += velocities[i] * delta;
                    velocities[i].y -= 250f * delta;
                    velocities[i] *= Mathf.Exp(-2f * delta);
                }
                yield return null;
            }
        }

        void ResetSpark(
            int index,
            float[] ages,
            Vector2[] velocities,
            float initialAge)
        {
            Image spark = _sparks[index];
            if (spark == null) return;
            ages[index] = initialAge;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(30f, 80f);
            velocities[index] = new Vector2(
                Mathf.Cos(angle) * speed,
                Mathf.Sin(angle) * speed);
            spark.rectTransform.anchoredPosition = Vector2.zero;
            spark.rectTransform.localScale =
                Vector3.one * Random.Range(0.35f, 0.9f);
            spark.color = new Color(1f, 0.55f, 0.15f, 0f);
        }

        void BuildSparkNodes()
        {
            _sparkRoot = UiFactory.Node(_dialog, "SparkFx");
            _sparkRoot.anchorMin = _sparkRoot.anchorMax = new Vector2(0.5f, 1f);
            _sparkRoot.sizeDelta = Vector2.zero;
            _sparkRoot.anchoredPosition = new Vector2(104.6f, -368.6f);
            _sparks = new Image[SparkCount];
            for (int i = 0; i < _sparks.Length; i++)
            {
                Image spark = UiFactory.Img(
                    _sparkRoot,
                    "Spark_" + i,
                    "Art/Sprites/Bubble/bomb_spark_particle",
                    28f,
                    28f);
                spark.raycastTarget = false;
                _sparks[i] = spark;
            }
            _sparkRoot.gameObject.SetActive(false);
        }

        static void PositionButton(CommonButton button, float y)
        {
            button.Root.anchorMin = button.Root.anchorMax = new Vector2(0.5f, 1f);
            button.Root.sizeDelta = new Vector2(700f, 240f);
            button.Root.anchoredPosition = new Vector2(0f, y);
        }

        static string SubtitleText()
        {
            string restart = Localization.Tr("exting_restart_text");
            string highlighted = "<color=#EE7F00>" + restart + "</color>";
            return Localization.Tr("exting_will_restart").Replace("%s", highlighted);
        }

        bool HasPrefabReferences()
        {
            return _root != null && _dim != null && _dialog != null &&
                   _card != null && _slot != null && _exitImage != null &&
                   _titleBack != null && _title != null && _subtitle != null &&
                   _dimGroup != null && _dialogGroup != null &&
                   _exitButton != null && _stayButton != null &&
                   _closeButton != null && _sparkRoot != null &&
                   _sparks != null && _sparks.Length == SparkCount;
        }

        static T FindNamed<T>(
            Transform parent,
            string objectName,
            bool excludeRoot = false)
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

        void Exit()
        {
            Page?.OnBombExitConfirmExit();
        }

        void Stay()
        {
            Page?.OnBombExitConfirmStay();
        }
    }
}
