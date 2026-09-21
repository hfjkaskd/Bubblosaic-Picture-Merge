using TMPro;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Central prefab registry.  The original Godot project used UIManager's
    /// PackedScene table for the same purpose.  Keeping the references in one
    /// asset removes scattered Resources paths and makes every runtime-created
    /// object originate from a persistent prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "PrefabCatalog", menuName = "BubblePics/Prefab Catalog")]
    public sealed class PrefabCatalog : ScriptableObject
    {
        const string ResourcePath = "Config/PrefabCatalog";
        static PrefabCatalog _current;

        [Header("Pages")]
        public GameObject SplashPage;
        public GameObject HomePage;
        public GameObject BubblePage;
        public GameObject PrivacyDialog;
        public GameObject LoadingOverlay;
        public GameObject SettingPage;
        public GameObject ToolUnlockPage;
        public GameObject HardLevelBanner;

        [Header("Bubble page views")]
        public GameObject BubbleWorld;
        public GameObject TopGameBar;
        public GameObject BubbleToolbar;
        public GameObject ComboOverlay;
        public GameObject CompletionWaveBand;
        public GameObject CompletePanel;
        public GameObject ContinuePanel;
        public GameObject BombRevivalPanel;
        public GameObject BombExitConfirmPanel;
        public GameObject MovesIntroPanel;
        public GameObject BubbleTutorial;

        [Header("Reusable entities")]
        public GameObject BubbleEntity;
        public GameObject CommonButton;
        public GameObject ToolButton;
        public GameObject Toast;
        public GameObject ConfettiFx;
        public GameObject ClearLevelBanner;
        public GameObject DolphinDecoration;

        [Header("Shared assets referenced by GUID")]
        public TMP_FontAsset UiFont;
        public Material SpineStraightAlphaMaterial;
        public RuntimeAssetCatalog RuntimeAssets;

        public static PrefabCatalog Current
        {
            get
            {
                if (_current == null)
                    _current = Resources.Load<PrefabCatalog>(ResourcePath);
                return _current;
            }
        }

        public static void SetCurrent(PrefabCatalog catalog)
        {
            _current = catalog;
        }

        public static T InstantiateComponent<T>(GameObject prefab, Transform parent = null)
            where T : Component
        {
            if (prefab == null) return null;
            var instance = Instantiate(prefab, parent, false);
            AssetLib.ApplyLocalizedFonts(instance.transform);
            var mounts = instance.GetComponent<PrefabMountSet>();
            if (mounts != null && App.I != null)
                mounts.Attach(App.I);
            var component = instance.GetComponent<T>();
            if (component == null)
                component = instance.GetComponentInChildren<T>(true);
            if (component == null)
            {
                Debug.LogError($"Prefab '{prefab.name}' does not contain {typeof(T).Name}.");
                Destroy(instance);
            }
            return component;
        }
    }
}
