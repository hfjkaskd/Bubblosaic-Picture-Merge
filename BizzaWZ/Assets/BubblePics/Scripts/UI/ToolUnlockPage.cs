using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    /// <summary>Port of tool_unlock_page.gd — icon slam + hbdk spine loop.</summary>
    public class ToolUnlockPage : MonoBehaviour
    {
        static readonly Vector2 ICON_HOME_POS = new Vector2(540, 1080); // icon center design (240..840 x, 780..1380 y)
        const float ICON_DROP_HEIGHT = 320f;
        const float ICON_START_SCALE = 2.2f;
        const float SLAM_DURATION = 0.3f;
        const float REBOUND_PEAK_SCALE = 1.15f;
        const float REBOUND_UP_DURATION = 0.1f;
        const float REBOUND_DOWN_DURATION = 0.18f;
        const float SPINE_LOOP_FADE_IN = 0.4f;
        const float INPUT_BLOCK_DURATION = 0.4f;

        [SerializeField] RectTransform _root;
        [SerializeField] RectTransform _content;
        [SerializeField] CanvasGroup _contentCg;
        [SerializeField] CanvasGroup _overlayCg;
        [SerializeField] Image _icon;
        [SerializeField] CanvasGroup _iconCg;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _desc;
        [SerializeField] PressButton _tapPress;
        [SerializeField] SpineLite.SpineSprite _spineLoop;
        [SerializeField] SpineLite.SpineSprite _spineEntry;
        [SerializeField] GameObject _spineRoot;
        [SerializeField, Range(0f, 1f)] float _spineLoopMaxAlpha = 0.42f;
        [SerializeField, Range(0f, 1f)] float _spineEntryMaxAlpha = 0.52f;
        [System.NonSerialized] ToolButton _sourceButton;
        Coroutine _showRoutine;
        Coroutine _spineLoopFadeRoutine;
        Coroutine _inputBlockRoutine;
        float _inputBlockedUntil;
        readonly List<PressButton> _blockedPressButtons = new List<PressButton>();
        readonly List<bool> _blockedPressButtonStates = new List<bool>();

        void OnEnable()
        {
            Localization.LocaleChanged += RefreshLocalizedLabels;
            RefreshLocalizedLabels();
        }

        void OnDisable()
        {
            Localization.LocaleChanged -= RefreshLocalizedLabels;
        }

        void RefreshLocalizedLabels()
        {
            if (_sourceButton == null || _sourceButton.Def == null) return;
            string id = _sourceButton.Def.Id.ToUpperInvariant();
            if (_title != null) _title.text = Localization.Tr("BUBBLE_TOOL_" + id);
            if (_desc != null) _desc.text = Localization.Tr("BUBBLE_UNLOCK_DESC_" + id);
        }

        public void Build(Transform parent, ToolButton tb)
        {
            if (HasPrefabReferences())
            {
                InitializePrefabRuntime(parent, tb);
                return;
            }

            _sourceButton = tb;
            var ownRoot = transform as RectTransform;
            _root = ownRoot != null && transform != parent
                ? ownRoot
                : UiFactory.FullStretch((RectTransform)parent, "ToolUnlockPage");
            if (_root.parent != parent)
                _root.SetParent(parent, false);
            _root.gameObject.name = "ToolUnlockPage";
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            var overlay = UiFactory.Rect(_root, "Overlay", new Color(0, 0, 0, 0.8f), 0, 0);
            var ovRt = overlay.rectTransform;
            ovRt.anchorMin = Vector2.zero; ovRt.anchorMax = Vector2.one;
            ovRt.offsetMin = Vector2.zero; ovRt.offsetMax = Vector2.zero;
            overlay.raycastTarget = true;
            _overlayCg = overlay.gameObject.AddComponent<CanvasGroup>();

            _content = UiFactory.FullStretch(_root, "Content");
            _contentCg = _content.gameObject.AddComponent<CanvasGroup>();
            // icon/texts render above the hbdk spine via nested canvas
            var contentCanvas = _content.gameObject.AddComponent<Canvas>();
            contentCanvas.overrideSorting = true;
            contentCanvas.sortingOrder = 2620;
            _content.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // hbdk spine between the dim overlay (2600) and content (2620)
            _spineRoot = new GameObject("HbdkSpine");
            // Keep the world subtree inside the authoring root so a single
            // prefab contains the complete Godot scene. Runtime init remounts
            // it under App.WorldRoot.
            _spineRoot.transform.SetParent(_root, false);
            var loopGo = new GameObject("Loop");
            loopGo.transform.SetParent(_spineRoot.transform, false);
            _spineLoop = loopGo.AddComponent<SpineLite.SpineSprite>();
            _spineLoop.Load("hbdk");
            _spineLoop.SortingOrder = 2610;
            _spineLoop.Tint = new Color(1, 1, 1, 0);
            var entryGo = new GameObject("Entry");
            entryGo.transform.SetParent(_spineRoot.transform, false);
            entryGo.transform.localPosition = new Vector3(40, 0, 0);
            _spineEntry = entryGo.AddComponent<SpineLite.SpineSprite>();
            _spineEntry.Load("hbdk");
            _spineEntry.SortingOrder = 2611;
            entryGo.SetActive(false);

            _title = UiFactory.Label(_content, "Title", Localization.Tr(("BUBBLE_TOOL_" + tb.Def.Id).ToUpper()), 120,
                new Color(1f, 0.776f, 0.251f), AssetLib.UiFont, TextAnchor.MiddleCenter, 1000, 190);
            _title.rectTransform.anchorMin = _title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _title.rectTransform.anchoredPosition = new Vector2(0, -465);
            _title.gameObject.AddComponent<CurveLabelEffect>().Amplitude = 45f;
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 1;
            _title.fontSizeMax = 120;

            _icon = UiFactory.Img(_content, "Icon", tb.Def.UnlockIcon, 600, 600);
            ApplyUnlockIcon(_icon, _icon.sprite);
            _icon.rectTransform.anchorMin = _icon.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _icon.rectTransform.anchoredPosition = new Vector2(0, -ICON_HOME_POS.y);
            _iconCg = _icon.gameObject.AddComponent<CanvasGroup>();

            _desc = UiFactory.Label(_content, "Desc", Localization.Tr("BUBBLE_UNLOCK_DESC_" + tb.Def.Id.ToUpper()), 64,
                Color.white, AssetLib.UiFont, TextAnchor.MiddleCenter, 840, 230);
            _desc.rectTransform.anchorMin = _desc.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _desc.rectTransform.anchoredPosition = new Vector2(0, -1675);

            // tap catcher
            var tap = UiFactory.Rect(_root, "TapCatcher", new Color(0, 0, 0, 0), 0, 0);
            var tapRt = tap.rectTransform;
            tapRt.anchorMin = Vector2.zero; tapRt.anchorMax = Vector2.one;
            tapRt.offsetMin = Vector2.zero; tapRt.offsetMax = Vector2.zero;
            UiFactory.MakeButton(tap.gameObject, Dismiss, pressAnim: false, sound: true);
            _tapPress = tap.GetComponent<PressButton>();

            InitializePrefabRuntime(parent, tb);
            _root.gameObject.SetActive(false);
        }

        /// <summary>Injects the highlighted tool into ToolUnlockPage.prefab.</summary>
        public void InitializePrefabRuntime(Transform parent, ToolButton tb)
        {
            _sourceButton = tb;
            _closing = false;
            _origPressed = null;
            if (_root == null)
                _root = transform as RectTransform;
            if (_root != null && parent != null && _root.parent != parent)
                _root.SetParent(parent, false);

            BindPrefabRuntime();

            if (_spineRoot != null && App.I != null)
            {
                if (!_spineRoot.transform.IsChildOf(App.I.WorldRoot))
                    _spineRoot.transform.SetParent(App.I.WorldRoot, false);
                _spineRoot.transform.position = App.DesignToWorld(new Vector2(540, 1080));
            }

            if (_sourceButton != null && _sourceButton.Def != null)
            {
                string id = _sourceButton.Def.Id;
                if (_title != null)
                    _title.text = Localization.Tr(("BUBBLE_TOOL_" + id).ToUpper());
                if (_desc != null)
                    _desc.text = Localization.Tr("BUBBLE_UNLOCK_DESC_" + id.ToUpper());
                if (_icon != null)
                {
                    Sprite iconSprite = AssetLib.Sprite(
                        string.IsNullOrEmpty(_sourceButton.Def.UnlockIcon)
                            ? _sourceButton.Def.Icon
                            : _sourceButton.Def.UnlockIcon);
                    ApplyUnlockIcon(_icon, iconSprite);
                }
            }

            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        static void ApplyUnlockIcon(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.preserveAspect = true;
            if (sprite == null) return;

            Vector2 size = sprite.rect.size;
            float scale = Mathf.Min(
                1f,
                Mathf.Min(650f / Mathf.Max(size.x, 1f),
                    740f / Mathf.Max(size.y, 1f)));
            image.rectTransform.sizeDelta = size * scale;
        }

        /// <summary>Restores button and Spine runtime state after prefab load.</summary>
        public void BindPrefabRuntime()
        {
            if (_tapPress != null)
            {
                _tapPress.OnClick = Dismiss;
                _tapPress.PressAnim = false;
                _tapPress.PlaySound = true;
            }
            if (_title != null)
            {
                // The restored Godot FontVariation uses weight 800. TMP's
                // synthetic Bold value is much heavier, so the dedicated
                // title material supplies the calibrated intermediate weight.
                _title.fontStyle = FontStyles.Normal;
                _title.fontWeight = FontWeight.Regular;
                _title.enableAutoSizing = true;
                _title.fontSizeMin = 1;
                _title.fontSizeMax = 120;
                TmpTextStyle.ClearEffects(_title);
                TmpTextStyle.ApplyOutline(
                    _title,
                    new Color(0.42f, 0.18f, 0.02f),
                    24f);
                TmpTextStyle.ApplyDisplayTitleFace(_title);
                var curve = _title.GetComponent<CurveLabelEffect>();
                if (curve != null)
                    curve.Amplitude = 45f;
            }
            if (_desc != null)
            {
                _desc.enableWordWrapping = true;
                _desc.overflowMode = TextOverflowModes.Overflow;
                TmpTextStyle.ClearEffects(_desc);
                TmpTextStyle.ApplyOutline(
                    _desc,
                    new Color(0, 0, 0, 0.35f),
                    6f);
            }
            if (_iconCg == null && _icon != null)
                _iconCg = _icon.GetComponent<CanvasGroup>() ?? _icon.gameObject.AddComponent<CanvasGroup>();

            if (_spineLoop == null && _spineRoot != null)
            {
                var spines = _spineRoot.GetComponentsInChildren<SpineLite.SpineSprite>(true);
                if (spines.Length > 0) _spineLoop = spines[0];
                if (spines.Length > 1) _spineEntry = spines[1];
            }
            if (_spineLoop != null)
            {
                if (_spineLoop.Data == null) _spineLoop.Load("hbdk");
                _spineLoop.SortingOrder = 2610;
            }
            if (_spineEntry != null)
            {
                if (_spineEntry.Data == null) _spineEntry.Load("hbdk");
                _spineEntry.SortingOrder = 2611;
            }
        }

        bool HasPrefabReferences()
        {
            return _root != null
                && _content != null
                && _contentCg != null
                && _overlayCg != null
                && _icon != null
                && _title != null
                && _desc != null
                && _tapPress != null
                && _spineRoot != null
                && _spineLoop != null
                && _spineEntry != null;
        }

        System.Action<ToolButton> _origPressed;
        System.Action<ToolButton> _unlockPressed;

        public void Show()
        {
            if (_sourceButton == null || _root == null) return;
            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }
            if (_spineLoopFadeRoutine != null)
            {
                StopCoroutine(_spineLoopFadeRoutine);
                _spineLoopFadeRoutine = null;
            }
            CancelInputBlockAndRestore();
            _root.gameObject.SetActive(true);
            _sourceButton.PlayUnlockHighlight();
            // godot: pressing the highlighted toolbar button closes the popup
            // and then fires the tool for real
            _origPressed = _sourceButton.PressedTool;
            _unlockPressed = _ => PressHighlightedAndUse();
            _sourceButton.PressedTool = _unlockPressed;
            BeginInputBlock();
            _showRoutine = StartCoroutine(ShowCo());
        }

        IEnumerator ShowCo()
        {
            StartCoroutine(GenericPopup.Open(_content, _contentCg, _overlayCg));
            ResetSpines();
            // Godot starts the hbdk loop and the icon slam in the same frame.
            _spineLoopFadeRoutine = StartCoroutine(FadeInSpineLoop());

            // icon slam
            var rt = _icon.rectTransform;
            var home = new Vector2(0, -ICON_HOME_POS.y);
            var start = home + new Vector2(0, ICON_DROP_HEIGHT);
            rt.anchoredPosition = start;
            rt.localScale = Vector3.one * ICON_START_SCALE;
            _iconCg.alpha = 0;
            float t = 0;
            while (t < SLAM_DURATION)
            {
                t += Time.deltaTime;
                float k = Tween.Evaluate(Ease.InQuad, Mathf.Clamp01(t / SLAM_DURATION));
                rt.anchoredPosition = Vector2.Lerp(start, home, k);
                rt.localScale = Vector3.one * Mathf.Lerp(ICON_START_SCALE, 1f, k);
                _iconCg.alpha = k;
                yield return null;
            }
            // entry spine + rebound
            _spineEntry.gameObject.SetActive(true);
            var entry = _spineEntry.SetAnimation("hbdk_1", false, 0f);
            if (entry != null) entry.Completed = () => _spineEntry.gameObject.SetActive(false);
            yield return Tween.Scale(rt, Vector3.one * REBOUND_PEAK_SCALE, REBOUND_UP_DURATION, Ease.OutQuad);
            yield return Tween.Scale(rt, Vector3.one, REBOUND_DOWN_DURATION, Ease.InQuad);
            _showRoutine = null;
        }

        IEnumerator FadeInSpineLoop()
        {
            _spineLoop.SetAnimation("hbdk_2", true, 0f);
            float f = 0;
            while (f < SPINE_LOOP_FADE_IN)
            {
                f += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(f / SPINE_LOOP_FADE_IN) * _spineLoopMaxAlpha;
                _spineLoop.Tint = new Color(1, 1, 1, alpha);
                yield return null;
            }
            _spineLoop.Tint = new Color(1f, 1f, 1f, _spineLoopMaxAlpha);
            _spineLoopFadeRoutine = null;
        }

        void ResetSpines()
        {
            if (_spineLoop != null)
            {
                _spineLoop.ClearTracks();
                _spineLoop.Tint = new Color(1f, 1f, 1f, 0f);
                _spineLoop.gameObject.SetActive(true);
            }
            if (_spineEntry != null)
            {
                _spineEntry.ClearTracks();
                _spineEntry.Tint = new Color(1f, 1f, 1f, _spineEntryMaxAlpha);
                _spineEntry.gameObject.SetActive(false);
            }
        }

        void BeginInputBlock()
        {
            CancelInputBlockAndRestore();
            _inputBlockedUntil = Time.unscaledTime + INPUT_BLOCK_DURATION;

            RememberAndBlock(_tapPress);
            if (_sourceButton != null)
            {
                var buttons = _sourceButton.GetComponentsInChildren<PressButton>(true);
                foreach (var button in buttons)
                    RememberAndBlock(button);
            }
            _inputBlockRoutine = StartCoroutine(InputBlockCo());
        }

        void CancelInputBlockAndRestore()
        {
            if (_inputBlockRoutine != null)
            {
                StopCoroutine(_inputBlockRoutine);
                _inputBlockRoutine = null;
            }
            RestoreBlockedInput();
        }

        void RememberAndBlock(PressButton button)
        {
            if (button == null || _blockedPressButtons.Contains(button)) return;
            _blockedPressButtons.Add(button);
            _blockedPressButtonStates.Add(button.Interactable);
            button.Interactable = false;
        }

        IEnumerator InputBlockCo()
        {
            while (Time.unscaledTime < _inputBlockedUntil)
                yield return null;
            RestoreBlockedInput();
        }

        void RestoreBlockedInput()
        {
            for (int i = 0; i < _blockedPressButtons.Count; i++)
            {
                var button = _blockedPressButtons[i];
                if (button != null)
                    button.Interactable = _blockedPressButtonStates[i];
            }
            _blockedPressButtons.Clear();
            _blockedPressButtonStates.Clear();
            _inputBlockedUntil = 0f;
            _inputBlockRoutine = null;
        }

        bool IsInputBlocked => Time.unscaledTime < _inputBlockedUntil;

        /// <summary>Consumes Escape/Android back while visible and closes after the brief input shield.</summary>
        public bool HandleBackRequest()
        {
            if (_root == null || !_root.gameObject.activeInHierarchy)
                return false;
            if (_closing || IsInputBlocked)
                return true;
            Dismiss();
            return true;
        }

        void Update()
        {
            if (_root != null && _root.gameObject.activeInHierarchy &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                HandleBackRequest();
            }
        }

        void Dismiss()
        {
            if (IsInputBlocked) return;
            StartCoroutine(DismissCo(false));
        }

        /// <summary>Recording flow: player presses the highlighted toolbar button,
        /// which closes the popup and immediately fires the tool for real.</summary>
        public void PressHighlightedAndUse()
        {
            if (IsInputBlocked) return;
            StartCoroutine(DismissCo(true));
        }

        bool _closing;

        IEnumerator DismissCo(bool useTool)
        {
            if (_closing) yield break;
            _closing = true;
            if (_showRoutine != null)
            {
                StopCoroutine(_showRoutine);
                _showRoutine = null;
            }
            if (_spineLoopFadeRoutine != null)
            {
                StopCoroutine(_spineLoopFadeRoutine);
                _spineLoopFadeRoutine = null;
            }
            CancelInputBlockAndRestore();
            var tb = _sourceButton;
            if (tb != null)
            {
                tb.PressedTool = _origPressed; // restore the real tool handler
                tb.StopUnlockHighlight();
            }
            yield return GenericPopup.Close(_content, _contentCg, _overlayCg);
            if (_spineRoot != null) Destroy(_spineRoot);
            // the page UI (incl. the full-screen TapCatcher) lives under the
            // dialog canvas — it must go too, or it eats every click below
            var rootObject = _root != null ? _root.gameObject : null;
            if (rootObject != null) Destroy(rootObject);
            if (useTool && tb != null && tb.PressedTool != null) tb.PressedTool(tb);
            if (gameObject != rootObject) Destroy(gameObject);
        }

        void OnDestroy()
        {
            CancelInputBlockAndRestore();
            if (_sourceButton != null && _sourceButton.PressedTool == _unlockPressed)
                _sourceButton.PressedTool = _origPressed;
        }
    }
}
