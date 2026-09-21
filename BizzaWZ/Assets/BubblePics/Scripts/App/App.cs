using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BubblePics
{
    /// <summary>
    /// Root bootstrapper (Godot launcher.gd + ui_manager.gd equivalent).
    /// World units = design pixels (1080x2400), camera ortho size 1200.
    /// All canvases are Screen Space - Camera so UI interleaves with world
    /// sprites via sortingOrder (Godot z_index * 10).
    /// </summary>
    public class App : MonoBehaviour
    {
        public static App I { get; private set; }

        public const float DesignW = 1080f;
        public const float DesignH = 2400f;
        public const float CanvasReferenceW = 1080f;
        public const float CanvasReferenceH = 2340f;
        public const float CanvasReferencePixelsPerUnit = 100f;
        /// <summary>
        /// Real safe-area insets converted to design pixels. The 720x1600
        /// desktop recording profile retains its measured 62px top baseline.
        /// </summary>
        public static float SafeTopDesign => DeviceLayout.SafeTopDesign;
        public static float SafeBottomDesign => DeviceLayout.SafeBottomDesign;

        [Header("Authored bootstrap references")]
        [SerializeField] Camera _cam;
        [SerializeField] Canvas _splashCanvas;   // order 1000
        [SerializeField] Canvas _hudCanvas;      // order 140
        [SerializeField] Canvas _panelCanvas;    // order 1500 (complete panel)
        [SerializeField] Canvas _dialogCanvas;   // order 2600
        [SerializeField] RectTransform _splashRoot;
        [SerializeField] RectTransform _hudRoot;
        [SerializeField] RectTransform _panelRoot;
        [SerializeField] RectTransform _dialogRoot;
        [SerializeField] Transform _worldRoot;
        [SerializeField] PrefabCatalog _prefabCatalog;

        public Camera Cam => _cam;
        public Canvas SplashCanvas => _splashCanvas;
        public Canvas HudCanvas => _hudCanvas;
        public Canvas PanelCanvas => _panelCanvas;
        public Canvas DialogCanvas => _dialogCanvas;
        public RectTransform SplashRoot => _splashRoot;
        public RectTransform HudRoot => _hudRoot;
        public RectTransform PanelRoot => _panelRoot;
        public RectTransform DialogRoot => _dialogRoot;
        public PrefabCatalog Prefabs => _prefabCatalog != null ? _prefabCatalog : PrefabCatalog.Current;

        public BubblePage Page { get; private set; }
        public HomePage Home { get; private set; }
        public Transform WorldRoot => _worldRoot;
        public BubblePicsRemoteImageDelivery RemoteImages { get; private set; }
        public bool SplashOwnsLevelLoading { get; private set; }

        SplashPage _splash;
        LoadingOverlay _loading;
        float _startupT0;

        /// <summary>Screen pixels per design pixel (uniform expand scale).</summary>
        public static float HudScale => DeviceLayout.PixelsPerDesignUnit;
        public static float HudScaleX => DeviceLayout.PixelsPerDesignUnit;
        public static float HudScaleY => DeviceLayout.PixelsPerDesignUnit;
        public static float WorldPerDesign => 1f;

        public static Vector3 DesignToWorld(Vector2 design)
            => new Vector3(
                design.x - DeviceLayout.ViewWidth / 2f,
                DeviceLayout.ViewHeight / 2f - design.y,
                0f);

        public static Vector2 WorldToDesign(Vector3 world)
            => new Vector2(
                world.x + DeviceLayout.ViewWidth / 2f,
                DeviceLayout.ViewHeight / 2f - world.y);
        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            DeviceLayout.RefreshFromScreen();
            if (_prefabCatalog == null || _cam == null || _worldRoot == null)
                throw new System.InvalidOperationException("AppRoot is missing authored gameplay references.");
            PrefabCatalog.SetCurrent(_prefabCatalog);
            RemoteImages = new BubblePicsRemoteImageDelivery(this);
            Time.fixedDeltaTime = 1f / 60f;
            Physics2D.gravity = new Vector2(0, -980f);
            Physics2D.velocityIterations = 32;
            Physics2D.positionIterations = 16;
            Physics2D.maxLinearCorrection = 6f;
            Physics2D.maxTranslationSpeed = 5000f;
            SoundManager.Create();
            SetupCanvases();
            _cam.orthographicSize = DeviceLayout.ViewHeight / 2f;
            SetPresentationVisible(false);
            BizzaEventSystem.Set(EventDefine.Frame.LanguageChange, SyncFrameworkLanguage, true);
        }

        void OnDestroy()
        {
            BizzaEventSystem.Set(EventDefine.Frame.LanguageChange, SyncFrameworkLanguage, false);
            RemoteImages?.Dispose();
            RemoteImages = null;
            if (I == this) I = null;
        }

        public void SyncFrameworkLanguage()
        {
            Localization.SetLocale(LanguageUtils.SelectedLanguage);
            Page?.TopBar?.RefreshLocalizedLabels();
            Page?.Toolbar?.Refresh();
        }

        void SetupCanvases()
        {
            BindAuthoredCanvas(ref _hudCanvas, ref _hudRoot, "HudCanvas");
            BindAuthoredCanvas(ref _panelCanvas, ref _panelRoot, "PanelCanvas");
            BindAuthoredCanvas(ref _dialogCanvas, ref _dialogRoot, "DialogCanvas");
            BindAuthoredCanvas(ref _splashCanvas, ref _splashRoot, "SplashCanvas");

            if (_worldRoot == null)
                _worldRoot = transform.Find("World");
            if (_worldRoot == null)
                Debug.LogError("AppRoot is missing its authored World root.", this);
        }

        void BindAuthoredCanvas(
            ref Canvas canvas,
            ref RectTransform root,
            string name)
        {
            if (canvas == null)
                canvas = transform.Find(name)?.GetComponent<Canvas>();

            if (canvas == null)
            {
                root = null;
                Debug.LogError(
                    $"AppRoot is missing its authored {name}. " +
                    "Runtime canvas construction is disabled.",
                    this);
                return;
            }

            root = canvas.transform as RectTransform;
        }

        void Update()
        {
            if (!DeviceLayout.RefreshFromScreen())
                return;

            ApplyDeviceLayout();
            Page?.HandleDeviceLayoutChanged();
            Home?.ApplyDeviceLayout();
        }

        void ApplyDeviceLayout()
        {
            if (_cam != null)
                _cam.orthographicSize = DeviceLayout.ViewHeight / 2f;
            FitCanvas(_hudRoot); FitCanvas(_panelRoot); FitCanvas(_dialogRoot);
        }

        /// <summary>
        /// Reapplies a GM safe-area override immediately instead of waiting for
        /// the next physical screen-size change.
        /// </summary>
        public void ForceDeviceLayoutRefreshForGm()
        {
            DeviceLayout.RefreshFromScreen();
            ApplyDeviceLayout();
            Page?.HandleDeviceLayoutChanged();
            Home?.ApplyDeviceLayout();
        }

#if UNITY_EDITOR
        /// <summary>Used only by the prefab authoring pipeline.</summary>
        public void ConfigurePrefabAuthoring(
            Camera cam,
            Canvas splashCanvas,
            Canvas hudCanvas,
            Canvas panelCanvas,
            Canvas dialogCanvas,
            Transform worldRoot,
            PrefabCatalog catalog)
        {
            _cam = cam;
            _splashCanvas = splashCanvas;
            _hudCanvas = hudCanvas;
            _panelCanvas = panelCanvas;
            _dialogCanvas = dialogCanvas;
            _splashRoot = splashCanvas != null ? splashCanvas.transform as RectTransform : null;
            _hudRoot = hudCanvas != null ? hudCanvas.transform as RectTransform : null;
            _panelRoot = panelCanvas != null ? panelCanvas.transform as RectTransform : null;
            _dialogRoot = dialogCanvas != null ? dialogCanvas.transform as RectTransform : null;
            _worldRoot = worldRoot;
            _prefabCatalog = catalog;
        }
#endif

        public void InitializeGameplay()
        {
            if (Page != null) return;
            Page = InstantiateMounted<BubblePage>(Prefabs.BubblePage);
            if (Page == null) throw new System.InvalidOperationException("BubblePage prefab is missing.");
            Page.InitializePrefabRuntime(this);
            Page.Input.InputEnabled = false;
            SplashOwnsLevelLoading = true;
            RemoteImages.BeginImageIndexWarmup();
        }

        public void SetPresentationVisible(bool visible)
        {
            _cam.enabled = visible;
            _hudCanvas.enabled = visible;
            _panelCanvas.enabled = visible;
            _dialogCanvas.enabled = visible;
            _splashCanvas.enabled = false;
        }

        public void MountGameplayUi(RealGamePanel panel)
        {
            MountCanvas(_hudRoot, panel.content);
            MountCanvas(_panelRoot, panel.content);
            MountCanvas(_dialogRoot, panel.content);
            ApplyDeviceLayout();
        }

        void MountCanvas(RectTransform root, Transform parent)
        {
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            FitCanvas(root);
        }

        void FitCanvas(RectTransform root)
        {
            if (root == null || root.parent == transform) return;
            Canvas canvas = root.GetComponentInParent<Canvas>().rootCanvas;
            root.sizeDelta = new Vector2(DeviceLayout.ViewWidth, DeviceLayout.ViewHeight);
            root.localScale = Vector3.one * (DeviceLayout.PixelsPerDesignUnit / canvas.scaleFactor);
        }

        public HomePage EnsureHomePage() => throw new System.InvalidOperationException("Gameplay navigation is owned by the framework.");
        public LoadingOverlay ShowLoading() => throw new System.InvalidOperationException("Use the framework loading lifecycle.");
        public void HideLoading() { BizzaGameplayBridge.OnLevelAssetsReady(); }

        /// <summary>
        /// Instantiates one catalog entry and attaches any authored world/UI
        /// subtrees to the same roots used by the restored Godot scenes.
        /// </summary>
        T InstantiateMounted<T>(GameObject prefab) where T : Component
        {
            var component = PrefabCatalog.InstantiateComponent<T>(prefab, transform);
            if (component == null) return null;
            var mounts = component.GetComponentInParent<PrefabMountSet>(true);
            if (mounts == null)
                mounts = component.GetComponentInChildren<PrefabMountSet>(true);
            mounts?.Attach(this);
            return component;
        }
    }
}
