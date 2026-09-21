using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics
{
    [System.Serializable]
    public class ToolDef
    {
        const string SkinRoot = "Art/Sprites/PsdSkin20260807";

        public string Id;
        public int UnlockLevel;
        public int InitCount;
        public int AdGrant;
        public string Icon, IconDisable, UnlockIcon;

        public static readonly ToolDef[] All =
        {
            new ToolDef { Id = "hint", UnlockLevel = 3, InitCount = 3, AdGrant = 3,
                Icon = SkinRoot + "/Gameplay/hint_icon_small",
                IconDisable = SkinRoot + "/Gameplay/hint_icon_small",
                UnlockIcon = SkinRoot + "/ToolUnlock/hint_large" },
            new ToolDef { Id = "drop", UnlockLevel = 5, InitCount = 2, AdGrant = 2,
                Icon = SkinRoot + "/ToolUnlock/magnet_small",
                IconDisable = SkinRoot + "/ToolUnlock/magnet_small",
                UnlockIcon = SkinRoot + "/ToolUnlock/magnet_large" },
            new ToolDef { Id = "magnet", UnlockLevel = 10, InitCount = 1, AdGrant = 1,
                Icon = SkinRoot + "/ToolUnlock/drop_small",
                IconDisable = SkinRoot + "/ToolUnlock/drop_small",
                UnlockIcon = SkinRoot + "/ToolUnlock/drop_large" },
        };
    }

    public enum ToolState { Locked, Free, Count, Ad }

    /// <summary>Port of tool_button.gd/tscn — 204x204 button with badges.</summary>
    public class ToolButton : MonoBehaviour
    {
        const float BTN_HIT_SIZE = 204f;

        [SerializeField] public ToolDef Def;
        public ToolState State { get; private set; }
        public System.Action<ToolButton> PressedTool;

        [SerializeField] RectTransform _root;
        [SerializeField] Image _bg;
        [SerializeField] Image _icon;
        [SerializeField] Image _countBadgeBg;
        [SerializeField] Image _adBadgeBg;
        [SerializeField] Image _freeBadge;
        [SerializeField] Image _lockIcon;
        [SerializeField] TMP_Text _countBadge;
        [SerializeField] TMP_Text _adBadge;
        [SerializeField] TMP_Text _lvLabel;
        [SerializeField] RectTransform _lockOverlay;
        [SerializeField] PressButton _pressButton;
        [SerializeField] Canvas _hlCanvas;
        [SerializeField] GraphicRaycaster _hlRaycaster;
        Coroutine _pulseCo;

        public void Build(Transform parent, ToolDef def)
        {
            if (HasPrefabReferences())
            {
                if (_root.parent != parent)
                    _root.SetParent(parent, false);
                InitializePrefabRuntime(def);
                return;
            }

            Def = def;
            var ownRoot = transform as RectTransform;
            var rt = ownRoot != null && transform != parent
                ? ownRoot
                : UiFactory.Node(parent, "Tool_" + def.Id);
            if (rt.parent != parent)
                rt.SetParent(parent, false);
            rt.gameObject.name = "Tool_" + def.Id;
            rt.sizeDelta = new Vector2(204, 204);
            _bg = UiFactory.Img(rt, "Bg", "Art/Sprites/PsdSkin20260807/Gameplay/tool_button_enabled", 184, 184);
            _bg.preserveAspect = true;
            _icon = UiFactory.Img(rt, "Icon", def.Icon, 156, 156);

            _countBadgeBg = UiFactory.Img(rt, "CountBadgeBg", "Art/Sprites/Bubble/badge_count_bg", 80, 80);
            SetTopRight(_countBadgeBg.rectTransform, -72, -8, 8, 72);
            _countBadge = UiFactory.Label(_countBadgeBg.rectTransform, "Count", "3", 55, Color.white, AssetLib.NumFont, TextAnchor.MiddleCenter, 80, 80);

            _adBadgeBg = UiFactory.Img(rt, "AdBadgeBg", "Art/Sprites/Bubble/badge_count_bg", 80, 80);
            SetTopRight(_adBadgeBg.rectTransform, -72, -8, 8, 72);
            _adBadge = UiFactory.Label(_adBadgeBg.rectTransform, "Ad", "AD", 40, Color.white, AssetLib.NumFont, TextAnchor.MiddleCenter, 80, 80);

            _freeBadge = UiFactory.Img(rt, "FreeBadge", "Art/Sprites/PsdSkin20260807/ToolUnlock/free_badge", 109, 54);
            SetTopRight(_freeBadge.rectTransform, -104, -4, 40, 76);

            _lockOverlay = UiFactory.FullStretch(rt, "LockOverlay");
            _lockIcon = UiFactory.Img(_lockOverlay, "LockIcon", "Art/Sprites/PsdSkin20260807/Gameplay/tool_lock", 68, 89);
            _lockIcon.rectTransform.anchoredPosition = new Vector2(0, 24); // godot offsets top=-74..26 => center y=-24 design
            _lvLabel = UiFactory.Label(_lockOverlay, "Lv", "Lv.3", 40, new Color(1, 1, 1, 0.9f), AssetLib.NumFont, TextAnchor.MiddleCenter, 124, 40);
            _lvLabel.rectTransform.anchoredPosition = new Vector2(0, -40); // godot top=20..60 => center y=40 design

            UiFactory.MakeButton(_bg.gameObject, HandlePressed);
            _pressButton = _bg.GetComponent<PressButton>();
            _root = rt;
            BindPrefabRuntime();
        }

        static void SetTopRight(RectTransform rt, float l, float t, float r, float b)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            // godot offsets from top-right corner: left,t(top),right,bottom (y down)
            rt.anchoredPosition = new Vector2((l + r) / 2f, -(t + b) / 2f);
            rt.sizeDelta = new Vector2(r - l, b - t);
        }

        public RectTransform Root => _root;

        /// <summary>Injects the definition and callback for ToolButton.prefab.</summary>
        public void InitializePrefabRuntime(ToolDef def, System.Action<ToolButton> onPressed = null)
        {
            if (def != null)
                Def = def;
            if (onPressed != null)
                PressedTool = onPressed;
            BindPrefabRuntime();
            if (_root != null && Def != null)
                _root.gameObject.name = "Tool_" + Def.Id;
            if (_icon != null && Def != null)
                ApplyToolIcon(_icon, AssetLib.Sprite(Def.Icon));
        }

        /// <summary>Restores button actions, which Unity does not serialize.</summary>
        public void BindPrefabRuntime()
        {
            if (_root == null)
                _root = transform as RectTransform;
            if (_pressButton == null && _bg != null)
                _pressButton = _bg.GetComponent<PressButton>();
            if (_pressButton != null)
                _pressButton.OnClick = HandlePressed;
            if (_adBadgeBg != null &&
                !true)
                _adBadgeBg.gameObject.SetActive(false);
            ConfigureBadgeText(_countBadge);
            ConfigureBadgeText(_adBadge);
            ConfigureBadgeText(_lvLabel);
        }

        static void ConfigureBadgeText(TMP_Text text)
        {
            if (text == null) return;
            TmpTextStyle.ClearEffects(text);
            TmpTextStyle.ApplyOutline(
                text,
                new Color32(0x59, 0x0C, 0x00, 0xFF),
                4f);
        }

        bool HasPrefabReferences()
        {
            return _root != null
                && _bg != null
                && _icon != null
                && _countBadgeBg != null
                && _adBadgeBg != null
                && _freeBadge != null
                && _lockIcon != null
                && _countBadge != null
                && _adBadge != null
                && _lvLabel != null
                && _lockOverlay != null;
        }

        void HandlePressed()
        {
            Fx.Vibrate(1);
            PressedTool?.Invoke(this);
        }

        public void Refresh(int level, int count, bool freeOk, bool adOk, bool effectOk)
        {
            if (level < Def.UnlockLevel) State = ToolState.Locked;
            else if (freeOk) State = ToolState.Free;
            else if (count > 0) State = ToolState.Count;
            else State = ToolState.Ad;

            bool enabledLook = (State == ToolState.Free || State == ToolState.Count
                || (State == ToolState.Ad &&
                    (false || adOk))) && effectOk;

            _bg.sprite = AssetLib.Sprite(enabledLook
                ? "Art/Sprites/PsdSkin20260807/Gameplay/tool_button_enabled"
                : "Art/Sprites/PsdSkin20260807/Gameplay/tool_button_disabled");
            if (_bg.sprite != null)
                _bg.rectTransform.sizeDelta = _bg.sprite.rect.size;
            _icon.gameObject.SetActive(State != ToolState.Locked);
            ApplyToolIcon(
                _icon,
                AssetLib.Sprite(enabledLook ? Def.Icon : Def.IconDisable));
            _countBadgeBg.gameObject.SetActive(State == ToolState.Count);
            _countBadge.text = count.ToString();
            _adBadgeBg.gameObject.SetActive(
                State == ToolState.Ad &&
                true);
            _freeBadge.gameObject.SetActive(
                State == ToolState.Free ||
                (State == ToolState.Ad && false));
            _lockOverlay.gameObject.SetActive(State == ToolState.Locked);
            _lvLabel.text = "Lv." + Def.UnlockLevel;
        }

        static void ApplyToolIcon(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.preserveAspect = true;
            if (sprite != null)
                image.rectTransform.sizeDelta = sprite.rect.size;
        }

        public void PlayUnlockHighlight()
        {
            if (_pulseCo != null) StopCoroutine(_pulseCo);
            // lift the live button above the unlock page dim overlay (godot clone z=2620)
            if (_hlCanvas == null)
            {
                _hlCanvas = Root.gameObject.AddComponent<Canvas>();
                _hlCanvas.overrideSorting = true;
                _hlCanvas.sortingOrder = 2620;
            }
            // A nested canvas needs its own raycaster or the button subtree
            // drops out of pointer raycasts permanently.
            if (_hlRaycaster == null)
                _hlRaycaster = Root.GetComponent<GraphicRaycaster>() ?? Root.gameObject.AddComponent<GraphicRaycaster>();
            _hlCanvas.enabled = true;
            _hlCanvas.overrideSorting = true;
            _hlCanvas.sortingOrder = 2620;
            _pulseCo = StartCoroutine(PulseCo());
        }

        public void StopUnlockHighlight()
        {
            if (_pulseCo != null) StopCoroutine(_pulseCo);
            Root.localScale = Vector3.one;
            if (_hlCanvas != null) _hlCanvas.overrideSorting = false;
        }

        IEnumerator PulseCo()
        {
            while (true)
            {
                yield return Tween.Scale(Root, Vector3.one * 1.12f, 0.4f, Ease.InOutSine);
                yield return Tween.Scale(Root, Vector3.one, 0.4f, Ease.InOutSine);
            }
        }
    }

    /// <summary>Port of bubble_toolbar.gd/tscn (old UI: bar 303, sep 36).</summary>
    public class ToolbarView : MonoBehaviour
    {
        public const float BAR_HEIGHT = 303f;
        public const float BAR_BACKGROUND_CENTER_Y = 203f;
        public const float BAR_BACKGROUND_HEIGHT = 258f;
        public const float BAR_VISUAL_TOP =
            BAR_BACKGROUND_CENTER_Y + BAR_BACKGROUND_HEIGHT * 0.5f;
        public static float TOTAL_HEIGHT => BAR_HEIGHT + App.SafeBottomDesign;
        const float ROW_SEPARATION = 5f;
        const float BTN_SIZE = 204f;
        const float ROW_CENTER_PITCH = BTN_SIZE + ROW_SEPARATION;

        [System.NonSerialized] public BubblePage Page;
        [SerializeField] RectTransform _root;
        [SerializeField] RectTransform _propRoot;
        public RectTransform PropRoot => _propRoot;
        [SerializeField] public ToolButton Hint;
        [SerializeField] public ToolButton Drop;
        [SerializeField] public ToolButton Magnet;
        [SerializeField] RectTransform _settingsBtn;
        [SerializeField] PressButton _settingsPress;
        public RectTransform Root => _root;
        public RectTransform SettingsBtn => _settingsBtn;
        SettingPage _settings;

        public void Build(Transform parent)
        {
            if (HasPrefabReferences())
            {
                InitializePrefabRuntime(parent, Page);
                return;
            }

            var ownRoot = transform as RectTransform;
            _root = ownRoot != null && transform != parent
                ? ownRoot
                : UiFactory.Node((RectTransform)parent, "Toolbar");
            if (_root.parent != parent)
                _root.SetParent(parent, false);
            _root.gameObject.name = "Toolbar";
            _root.anchorMin = new Vector2(0, 0);
            _root.anchorMax = new Vector2(1, 0);
            _root.pivot = new Vector2(0.5f, 0);
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = new Vector2(0, TOTAL_HEIGHT);

            var bg = UiFactory.Img(_root, "BarBg", "Art/Sprites/PsdSkin20260807/Gameplay/toolbar", 953, 258);
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0f);
            bgRt.anchoredPosition =
                new Vector2(0f, BAR_BACKGROUND_CENTER_Y);
            bg.raycastTarget = true; // toolbar blocks input (mouse_filter STOP)

            // row: 4 buttons centered
            float totalW = 4 * BTN_SIZE + 3 * ROW_SEPARATION;
            float startX = -totalW / 2f + BTN_SIZE / 2f;
            Hint = BuildTool(0, startX, ToolDef.All[0]);
            Drop = BuildTool(1, startX + (BTN_SIZE + ROW_SEPARATION), ToolDef.All[1]);
            Magnet = BuildTool(2, startX + 2 * (BTN_SIZE + ROW_SEPARATION), ToolDef.All[2]);

            _settingsBtn = UiFactory.Node(_root, "Settings");
            _settingsBtn.sizeDelta = new Vector2(204, 204);
            _settingsBtn.anchoredPosition = new Vector2(startX + 3 * (BTN_SIZE + ROW_SEPARATION), 0);
            var sbg = UiFactory.Img(_settingsBtn, "Bg", "Art/Sprites/PsdSkin20260807/Gameplay/settings_button", 184, 184);
            sbg.preserveAspect = true;
            UiFactory.MakeButton(sbg.gameObject, OnSettingsPressed);
            _settingsPress = sbg.GetComponent<PressButton>();
            BindPrefabRuntime();
            ApplyDeviceLayout();
        }

        ToolButton BuildTool(int index, float x, ToolDef def)
        {
            ToolButton tb = null;
            var catalog = PrefabCatalog.Current;
            if (catalog != null && catalog.ToolButton != null)
                tb = PrefabCatalog.InstantiateComponent<ToolButton>(catalog.ToolButton, _root);

            if (tb != null)
                tb.InitializePrefabRuntime(def, OnToolPressed);
            else
            {
                var holder = new GameObject("Tool_" + def.Id, typeof(RectTransform), typeof(ToolButton));
                holder.transform.SetParent(_root, false);
                tb = holder.GetComponent<ToolButton>();
                tb.Build(_root, def);
            }
            tb.Root.anchoredPosition = new Vector2(x, 0);
            tb.PressedTool = OnToolPressed;
            return tb;
        }

        /// <summary>Injects the owner into BubbleToolbar.prefab.</summary>
        public void InitializePrefabRuntime(Transform parent, BubblePage page)
        {
            Page = page;
            if (_root == null)
                _root = transform as RectTransform;
            if (_root != null && parent != null && _root.parent != parent)
                _root.SetParent(parent, false);
            ApplyDeviceLayout();
            BindPrefabRuntime();
        }

        public void ApplyDeviceLayout()
        {
            if (_root == null) return;
            var offsetMax = _root.offsetMax;
            offsetMax.y = TOTAL_HEIGHT;
            _root.offsetMax = offsetMax;

            var bar = FindNamedRect(_root, "BarBg");
            if (bar != null)
            {
                bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
                bar.sizeDelta = new Vector2(953f, BAR_BACKGROUND_HEIGHT);
                bar.anchoredPosition =
                    new Vector2(0f, BAR_BACKGROUND_CENTER_Y);
            }

            // Match Godot's two safe-area groups: the background grows upward,
            // while the original 303 px button row moves by the full inset.
            float safeBottom = App.SafeBottomDesign;
            var rowLayout = _root.GetComponentInChildren<HorizontalLayoutGroup>(true);
            if (rowLayout != null)
            {
                // Prefab Row children are zero-sized holders around a 204 px
                // hit target, so HLG spacing represents center-to-center pitch.
                rowLayout.spacing = ROW_CENTER_PITCH;
                var padding = rowLayout.padding;
                padding.bottom = Mathf.RoundToInt(safeBottom);
                rowLayout.padding = padding;
                LayoutRebuilder.MarkLayoutForRebuild(rowLayout.transform as RectTransform);
                return;
            }

            // Keep the non-prefab fallback equivalent to the prefab row.
            float rowOffsetY = safeBottom * 0.5f;
            SetRowItemY(Hint != null ? Hint.Root : null, rowOffsetY);
            SetRowItemY(Drop != null ? Drop.Root : null, rowOffsetY);
            SetRowItemY(Magnet != null ? Magnet.Root : null, rowOffsetY);
            SetRowItemY(_settingsBtn, rowOffsetY);
        }

        static void SetRowItemY(RectTransform item, float y)
        {
            if (item == null) return;
            var position = item.anchoredPosition;
            position.y = y;
            item.anchoredPosition = position;
        }

        /// <summary>
        /// The authored background extends 29 design pixels above the
        /// nominal 303-pixel toolbar rect. Include that overhang and the
        /// device safe-bottom growth when sliding the bar offscreen.
        /// </summary>
        public static float GetExitSlideDistance(
            RectTransform root,
            float margin = 20f)
        {
            float rectHeight = root != null
                ? root.rect.height
                : TOTAL_HEIGHT;
            return Mathf.Max(rectHeight, BAR_VISUAL_TOP) + margin;
        }

        static RectTransform FindNamedRect(Transform parent, string objectName)
        {
            if (parent == null) return null;
            foreach (var rect in parent.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == objectName)
                    return rect;
            return null;
        }

        /// <summary>Restores tool and settings actions after prefab instantiation.</summary>
        public void BindPrefabRuntime()
        {
            Hint?.InitializePrefabRuntime(ToolDef.All[0], OnToolPressed);
            Drop?.InitializePrefabRuntime(ToolDef.All[1], OnToolPressed);
            Magnet?.InitializePrefabRuntime(ToolDef.All[2], OnToolPressed);
            if (_settingsPress == null && _settingsBtn != null)
                _settingsPress = _settingsBtn.GetComponentInChildren<PressButton>(true);
            if (_settingsPress != null)
                _settingsPress.OnClick = OnSettingsPressed;
        }

        bool HasPrefabReferences()
        {
            return _root != null
                && Hint != null
                && Drop != null
                && Magnet != null
                && _settingsBtn != null;
        }

        void OnEnable()
        {
            if (Page == null || !HasPrefabReferences() || !IsToolbarVisible())
                return;

            bool adReady = IsRewardedToolAvailable();
            RefreshAdDependentButtons(adReady);
        }

        public void Refresh()
        {
            var panel = UIModule.Instance.GetPage<RealGamePanel>();
            if (panel == null || panel.propEntries == null) return;
            foreach (var entry in panel.propEntries) entry.Refresh();
        }

        /// <summary>
        /// Repaints only buttons whose enabled appearance depends on whether an
        /// ad provider can accept an on-demand readiness request. Unlike
        /// Refresh(), this never grants a free use or writes save data.
        /// </summary>
        void RefreshAdDependentButtons(bool adReady)
        {
            Refresh();
        }

        static bool IsRewardedToolAvailable()
        {
            // White-package builds deliberately contain no ad provider. Their
            // tool buttons behave as if a rewarded ad completed immediately.
            return true;
        }

        bool IsToolbarVisible()
        {
            return isActiveAndEnabled
                && gameObject.activeInHierarchy
                && _root != null
                && _root.gameObject.activeInHierarchy;
        }

        void OnToolPressed(ToolButton tb)
        {
            tb.GetComponent<UIPropEntry>()?.RequestUse();
        }

        public void OpenSettings() => OnSettingsPressed();

        void OnSettingsPressed()
        {
            if (Page != null && !Page.IsRoundFinalized()) BizzaGameplayBridge.OpenSettings();
        }

        // -------------------------------------------------- unlock popup
        public void CheckUnlockPopupsDeferred()
        {
            if (Hint != null && Drop != null && Magnet != null) StartCoroutine(CheckUnlockCo());
        }

        IEnumerator CheckUnlockCo()
        {
            int myRound = Page.RoundSeq;
            float t = 0;
            yield return new WaitForSeconds(0.4f);
            if (Page == null || Page.RoundSeq != myRound) yield break;
            while (t < 5f && Page.RoundSeq == myRound &&
                   !Page.Field.AreAllBubblesLanded())
            {
                t += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            if (Page.RoundSeq != myRound) yield break;
            foreach (var tb in new[] { Hint, Drop, Magnet })
            {
                var id = tb.Def.Id;
                if (Page.CurrentLevelNumber == tb.Def.UnlockLevel && !SaveState.GetFlag("tool_unlock_shown_" + id))
                {
                    SaveState.SetFlag("tool_unlock_shown_" + id, true);
                    ToolUnlockPage page = null;
                    var catalog = PrefabCatalog.Current;
                    if (catalog != null && catalog.ToolUnlockPage != null)
                        page = PrefabCatalog.InstantiateComponent<ToolUnlockPage>(catalog.ToolUnlockPage, App.I.DialogRoot);

                    if (page != null)
                        page.InitializePrefabRuntime(App.I.DialogRoot, tb);
                    else
                    {
                        var go = new GameObject("ToolUnlockPage", typeof(RectTransform), typeof(ToolUnlockPage));
                        page = go.GetComponent<ToolUnlockPage>();
                        page.Build(App.I.DialogRoot, tb);
                    }
                    SoundManager.I.Play("dialog");
                    page.Show();
                    yield break;
                }
            }
        }
    }
}
