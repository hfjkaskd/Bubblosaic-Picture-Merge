using System;
using System.IO;
using BubblePics;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BubblePics.EditorTools
{
    // Authoring only: writes concrete prefabs inspected and used by every player platform.
    public static class BizzaPrefabIntegration
    {
        const string Root = "Assets/BubblePics/";
        static T Ref<T>(SerializedObject source, string name) where T : Object =>
            (T)source.FindProperty(name).objectReferenceValue;
        static void Set(SerializedObject source, string name, Object value) =>
            source.FindProperty(name).objectReferenceValue = value;

        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before authoring.");
            ConfigureProp();
            ConfigureToolbar();
            ConfigurePanel();
            ConfigurePropConfig();
            ConfigureLoading();
            ConfigureAppRoot();
            RepairCommonConfirmReferences();
            AssetDatabase.SaveAssets();
        }

        static void ConfigureProp()
        {
            var path = Root + "Resources/Prefabs/FrameworkBubbleProp.prefab";
            var go = PrefabUtility.LoadPrefabContents(Root + "RuntimePrefabs/UI/ToolButton.prefab");
            try
            {
                var view = go.GetComponent<ToolButton>();
                // Match the source toolbar's zero-size layout holders; the authored child owns the button hit area.
                ((RectTransform)go.transform).sizeDelta = Vector2.zero;
                var serialized = new SerializedObject(view);
                var entry = go.AddComponent<UIPropEntry>();
                entry.btn = Ref<PressButton>(serialized, "_pressButton");
                entry.btn.targetGraphic = Ref<Image>(serialized, "_bg");
                entry.btn.transition = Selectable.Transition.None;
                entry.propIcon = Ref<Image>(serialized, "_icon");
                entry.itemNumTxt = Ref<TMP_Text>(serialized, "_countBadge");
                entry.unlockLevelTxt = Ref<TMP_Text>(serialized, "_lvLabel");
                entry.lockIcon = Ref<RectTransform>(serialized, "_lockOverlay").gameObject;
                entry.haveTips = Ref<Image>(serialized, "_countBadgeBg").gameObject;
                entry.addTips = Ref<Image>(serialized, "_adBadgeBg").gameObject;
                entry.cancelTips = Ref<Image>(serialized, "_freeBadge").gameObject;
                entry.cancelTips.SetActive(false);
                var canvas = go.GetComponent<Canvas>();
                if (canvas == null) canvas = go.AddComponent<Canvas>();
                var raycaster = go.GetComponent<GraphicRaycaster>();
                if (raycaster == null) raycaster = go.AddComponent<GraphicRaycaster>();
                canvas.overrideSorting = false;
                Set(serialized, "_hlCanvas", canvas);
                Set(serialized, "_hlRaycaster", raycaster);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        static void ConfigureAppRoot()
        {
            var path = Root + "Resources/Prefabs/AppRoot.prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var system in go.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
                    Object.DestroyImmediate(system.gameObject);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        static void RepairCommonConfirmReferences()
        {
            var path = "Assets/BizzaWZ/Final/FunctionTools/CommonPublic/UI/CommonConfirmTipsPanel.prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/BizzaWZ/Common/Framework/Res/Fonts/MainFont_Simple.asset");
                var background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BizzaWZ/Common/BizzaGame/Z_ReplaceAssets/UI_Frame/Common/Common_Frame.png");
                var button = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/BizzaWZ/Common/BizzaGame/Z_ReplaceAssets/UI_Frame/Common/Btn_Normael.png");
                foreach (var label in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    label.font = font;
                    label.fontSharedMaterial = font.material;
                    label.color = label.name == "Des" ? new Color32(42, 50, 70, 255) : Color.white;
                }
                foreach (var graphic in go.GetComponentsInChildren<Image>(true))
                {
                    if (graphic.name == "PageMask") continue;
                    if (graphic.name == "BG (1)")
                    {
                        graphic.sprite = background;
                        graphic.enabled = true;
                        graphic.type = Image.Type.Sliced;
                        continue;
                    }
                    if (graphic.sprite != null) continue;
                    if (graphic.name == "ConfirmBtn") graphic.sprite = button;
                    else if (graphic.name == "BG") graphic.sprite = background;
                    else { graphic.enabled = false; continue; }
                    graphic.type = Image.Type.Sliced;
                }
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        static void ConfigureToolbar()
        {
            var path = Root + "RuntimePrefabs/UI/BubbleToolbar.prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = go.GetComponent<ToolbarView>();
                var serialized = new SerializedObject(view);
                foreach (var name in new[] { "Hint", "Drop", "Magnet" })
                {
                    var button = Ref<ToolButton>(serialized, name);
                    if (button != null) Object.DestroyImmediate(button.gameObject);
                    Set(serialized, name, null);
                }
                var row = go.GetComponentInChildren<HorizontalLayoutGroup>(true);
                var settings = Ref<RectTransform>(serialized, "_settingsBtn");
                if (settings != null) settings.gameObject.SetActive(false);
                Set(serialized, "_propRoot", row.transform);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (var press in go.GetComponentsInChildren<PressButton>(true))
                {
                    press.targetGraphic = press.GetComponent<Graphic>();
                    press.transition = Selectable.Transition.None;
                }
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        static void ConfigurePanel()
        {
            var path = "Assets/BizzaWZ/Common/UI/GamePanel/RealGamePanel.prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var panel = go.GetComponent<RealGamePanel>();
                panel.uIPropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Resources/Prefabs/FrameworkBubbleProp.prefab");
                panel.whiteUiPrefab = null;
                foreach (var graphic in go.GetComponentsInChildren<Graphic>(true))
                    graphic.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        static void ConfigureLoading()
        {
            var path = "Assets/BizzaWZ/Common/MenuSystem/Common/LoadingPanel/LoadingPanel.prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var page = go.GetComponent<LoadingPanel>();
                for (int i = go.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "RuntimePrefabs/Pages/SplashPage.prefab");
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
                var rect = (RectTransform)visual.transform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var serialized = new SerializedObject(page);
                Set(serialized, "gameplayVisual", visual.GetComponent<SplashPage>());
                serialized.ApplyModifiedPropertiesWithoutUndo();
                page.CameraObj = null;
                page.busRect = null;
                page.progressBar = null;
                page.progressTxt = null;
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        static void ConfigurePropConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<PropConfigSO>("Assets/BizzaWZ/Common/Resources/Configs/PropConfig.asset");
            config.PropCfgInfos.Clear();
            for (int i = 0; i < ToolDef.All.Length; i++)
            {
                var definition = ToolDef.All[i];
                var matches = AssetDatabase.FindAssets(Path.GetFileName(definition.Icon) + " t:Sprite", new[] { Root.TrimEnd('/') });
                Sprite icon = null;
                foreach (var guid in matches)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(path) == Path.GetFileName(definition.Icon))
                    { icon = AssetDatabase.LoadAssetAtPath<Sprite>(path); break; }
                }
                if (icon == null) throw new InvalidOperationException("Missing prop icon " + definition.Icon);
                config.PropCfgInfos.Add(new PropConfigInfo {
                    propType = BizzaGameplayBridge.ToolType(definition.Id), propIcon = icon,
                    unlockFunction = true, unlockCondition = new PropUnlockCondition { unlockLevel = definition.UnlockLevel },
                    cancelFunction = false, preLimitNum = int.MaxValue, newPlayerPropCount = definition.InitCount
                });
            }
            EditorUtility.SetDirty(config);
        }
    }
}
