using UnityEngine;

namespace BubblePics
{
    /// <summary>Combo counter (combo_counter.gd): praise every 3rd consecutive merge.</summary>
    public class ComboCounter
    {
        int _times;
        int _maxTimes;

        public (bool show, string tier, string skin) OnMerge()
        {
            _times++;
            _maxTimes = Mathf.Max(_maxTimes, _times);
            if (_times >= 3 && _times % 3 == 0)
            {
                string tier = _times == 3 ? "nice" : _times == 6 ? "perfect" : "excellent";
                return (true, tier, "flower_" + tier);
            }
            return (false, "", "");
        }

        public int Times => _times;
        public int MaxTimes => _maxTimes;
        public void OnBreak() { _times = 0; }
        public void Reset() { _times = 0; _maxTimes = 0; }
    }

    /// <summary>Port of combo_overlay.gd — spine combo_title with word skins.</summary>
    public class ComboOverlay : MonoBehaviour
    {
        public const float OVERLAY_Y = 510f;
        const string ANIM = "Animation";

        [SerializeField] Transform _viewRoot;
        [SerializeField] SpineLite.SpineSprite _spine;
        public bool AutoFree;

        public void Build(Transform worldRoot)
        {
            if (_spine != null)
            {
                InitializePrefabRuntime(worldRoot);
                gameObject.SetActive(false);
                return;
            }

            // During prefab authoring the component is copied from a temporary
            // controller to the wrapper root. Keep the authored visual under
            // the World mount so destroying that controller does not remove it.
            if (Application.isPlaying)
            {
                transform.SetParent(worldRoot, false);
                _viewRoot = transform;
            }
            else
            {
                var view = new GameObject("ComboView");
                view.transform.SetParent(worldRoot, false);
                _viewRoot = view.transform;
            }
            var go = new GameObject("Spine");
            go.transform.SetParent(_viewRoot, false);
            _spine = go.AddComponent<SpineLite.SpineSprite>();
            _spine.Load("combo");
            _spine.SortingOrder = 1600; // godot z=160
            BindPrefabRuntime();
            SetWorldPosition(App.DesignToWorld(new Vector2(BubbleField.ViewW * 0.5f, OVERLAY_Y)));
            SetViewActive(false);
        }

        /// <summary>Injects the runtime parent for an instance loaded from ComboOverlay.prefab.</summary>
        public void InitializePrefabRuntime(Transform worldRoot)
        {
            if (_viewRoot == null)
                _viewRoot = transform;
            if (worldRoot != null)
            {
                if (_viewRoot == transform)
                    transform.SetParent(worldRoot, false);
                else if (!_viewRoot.IsChildOf(worldRoot))
                    _viewRoot.SetParent(worldRoot, false);
            }
            BindPrefabRuntime();
            SetWorldPosition(App.DesignToWorld(new Vector2(BubbleField.ViewW * 0.5f, OVERLAY_Y)));
        }

        /// <summary>Restores non-serialized Spine callbacks after prefab instantiation.</summary>
        public void BindPrefabRuntime()
        {
            if (_viewRoot == null)
                _viewRoot = _spine != null ? _spine.transform.parent : transform;
            if (_spine == null)
                _spine = _viewRoot.GetComponentInChildren<SpineLite.SpineSprite>(true);
            if (_spine == null) return;
            if (_spine.Data == null)
                _spine.Load("combo");
            _spine.SortingOrder = 1600;
            _spine.AnimationCompleted -= OnAnimationCompleted;
            _spine.AnimationCompleted += OnAnimationCompleted;
        }

        void OnAnimationCompleted(SpineLite.TrackEntry entry)
        {
            HideNow();
        }

        public void ShowSkin(string skin)
        {
            if (string.IsNullOrEmpty(skin)) return;
            if (_spine == null)
                BindPrefabRuntime();
            if (_spine == null) return;
            _spine.SetSkin(skin);
            SetViewActive(true);
            _spine.SetAnimation(ANIM, false, 0f);
        }

        public void ShowCombo(string skin) => ShowSkin(skin);
        public void ShowWonderful() => ShowSkin("flower_wonderful");
        public void ShowLucky() => ShowSkin("lucky");
        public void ShowBigMerge() => ShowSkin("big _merge");

        public void HideNow()
        {
            if (AutoFree) { Destroy(gameObject); return; }
            SetViewActive(false);
            if (_spine != null) _spine.ClearTracks();
        }

        void SetViewActive(bool active)
        {
            var target = _viewRoot != null ? _viewRoot.gameObject : gameObject;
            target.SetActive(active);
        }

        void SetWorldPosition(Vector3 worldPosition)
        {
            if (_viewRoot != null)
                _viewRoot.position = worldPosition;
            else
                transform.position = worldPosition;
        }

        public static ComboOverlay SpawnLucky(Transform worldRoot, Vector3 worldPos)
        {
            ComboOverlay overlay = null;
            var catalog = PrefabCatalog.Current;
            if (catalog != null && catalog.ComboOverlay != null)
                overlay = PrefabCatalog.InstantiateComponent<ComboOverlay>(catalog.ComboOverlay, worldRoot);

            if (overlay != null)
            {
                overlay.gameObject.name = "LuckyOverlay";
                overlay.InitializePrefabRuntime(worldRoot);
            }
            else
            {
                var go = new GameObject("LuckyOverlay");
                overlay = go.AddComponent<ComboOverlay>();
                overlay.Build(worldRoot);
            }
            overlay.AutoFree = true;
            overlay.SetWorldPosition(worldPos);
            overlay.ShowLucky();
            return overlay;
        }

        void OnDestroy()
        {
            if (_spine != null)
                _spine.AnimationCompleted -= OnAnimationCompleted;
        }
    }
}
