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

        [SerializeField] RectTransform _viewRoot;
        [SerializeField] Spine.Unity.SkeletonGraphic _spine;
        [SerializeField] string _spineModule = "combo";
        public bool AutoFree;

        /// <summary>Mount the authored overlay with the other gameplay UI.</summary>
        public void InitializePrefabRuntime(RectTransform hudRoot)
        {
            if (hudRoot == null || _viewRoot == null || _spine == null)
                throw new System.InvalidOperationException("ComboOverlay requires its authored UI prefab and gameplay HUD.");
            transform.SetParent(hudRoot, false);
            SetViewActive(false);
        }

        /// <summary>Load the shared Spine resources on first use, identically on all platforms.</summary>
        public void BindPrefabRuntime()
        {
            if (_spine.IsValid) return;
            var module = SpineLite.OfficialSpineAssets.Load(_spineModule);
            if (module == null)
                throw new System.InvalidOperationException("Combo Spine resources failed to load.");
            _spine.skeletonDataAsset = module.Asset;
            _spine.Initialize(false);
            _spine.AnimationState.Complete += OnAnimationCompleted;
        }

        void OnAnimationCompleted(Spine.TrackEntry entry)
        {
            if (!entry.Loop) HideNow();
        }

        public void ShowSkin(string skin)
        {
            if (string.IsNullOrEmpty(skin)) return;
            BindPrefabRuntime();
            _spine.AnimationState.ClearTracks();
            _spine.Skeleton.SetSkin(skin);
            _spine.Skeleton.SetToSetupPose();
            SetViewActive(true);
            _spine.AnimationState.SetAnimation(0, ANIM, false);
            _spine.Update(0f);
            _spine.UpdateMesh();
        }

        public void ShowCombo(string skin) => ShowSkin(skin);
        public void ShowWonderful() => ShowSkin("flower_wonderful");
        public void ShowLucky() => ShowSkin("lucky");
        public void ShowBigMerge() => ShowSkin("big _merge");

        public void HideNow()
        {
            if (AutoFree) { Destroy(gameObject); return; }
            SetViewActive(false);
            if (_spine != null && _spine.IsValid) _spine.AnimationState.ClearTracks();
        }

        void SetViewActive(bool active)
        {
            var target = _viewRoot != null ? _viewRoot.gameObject : gameObject;
            target.SetActive(active);
        }

        void SetWorldPosition(Vector3 worldPosition)
        {
            _viewRoot.position = ImageFlyAnimator.WorldToHudPosition(worldPosition);
        }

        public static ComboOverlay SpawnLucky(Transform worldRoot, Vector3 worldPos)
        {
            var catalog = PrefabCatalog.Current;
            var overlay = PrefabCatalog.InstantiateComponent<ComboOverlay>(catalog.ComboOverlay, App.I.HudRoot);
            overlay.gameObject.name = "LuckyOverlay";
            overlay.InitializePrefabRuntime(App.I.HudRoot);
            overlay.AutoFree = true;
            overlay.SetWorldPosition(worldPos);
            overlay.ShowLucky();
            return overlay;
        }

        void OnDestroy()
        {
            if (_spine != null && _spine.IsValid)
                _spine.AnimationState.Complete -= OnAnimationCompleted;
        }
    }
}
