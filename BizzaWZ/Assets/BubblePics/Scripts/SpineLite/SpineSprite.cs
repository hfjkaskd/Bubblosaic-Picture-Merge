using System;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace BubblePics.SpineLite
{
    /// <summary>
    /// Compatibility handle for Spine.TrackEntry. Animation playback and
    /// mixing are executed by the official spine-csharp 4.1 AnimationState.
    /// </summary>
    public sealed class TrackEntry
    {
        readonly SpineSprite _owner;
        readonly Spine.TrackEntry _runtimeEntry;
        readonly SpineAnimation _animation;
        bool _completedFired;

        internal TrackEntry(
            SpineSprite owner,
            Spine.TrackEntry runtimeEntry)
        {
            _owner = owner;
            _runtimeEntry = runtimeEntry;
            _animation = runtimeEntry?.Animation != null
                ? new SpineAnimation(runtimeEntry.Animation)
                : null;
            if (_runtimeEntry != null)
                _runtimeEntry.Complete += OnRuntimeCompleted;
        }

        public SpineAnimation Animation => _animation;

        public bool Loop
        {
            get => _runtimeEntry != null && _runtimeEntry.Loop;
            set
            {
                if (_runtimeEntry != null) _runtimeEntry.Loop = value;
            }
        }

        public float TrackTime
        {
            get => _runtimeEntry?.TrackTime ?? 0f;
            set
            {
                if (_runtimeEntry != null) _runtimeEntry.TrackTime = value;
            }
        }

        public float TimeScale
        {
            get => _runtimeEntry?.TimeScale ?? 0f;
            set
            {
                if (_runtimeEntry != null) _runtimeEntry.TimeScale = value;
            }
        }

        public float MixDuration
        {
            get => _runtimeEntry?.MixDuration ?? 0f;
            set
            {
                if (_runtimeEntry != null) _runtimeEntry.MixDuration = value;
            }
        }

        public float MixTime
        {
            get => _runtimeEntry?.MixTime ?? 0f;
            set
            {
                if (_runtimeEntry != null) _runtimeEntry.MixTime = value;
            }
        }

        public TrackEntry MixingFrom =>
            _owner?.Wrap(_runtimeEntry?.MixingFrom);

        public float Delay
        {
            get => _runtimeEntry?.Delay ?? 0f;
            set
            {
                if (_runtimeEntry != null) _runtimeEntry.Delay = value;
            }
        }

        public Action Completed { get; set; }

        public float AnimationEnd => _runtimeEntry?.AnimationEnd ?? 0f;

        public float AnimationTime => _runtimeEntry?.AnimationTime ?? 0f;

        internal Spine.TrackEntry RuntimeEntry => _runtimeEntry;

        void OnRuntimeCompleted(Spine.TrackEntry runtimeEntry)
        {
            // The former project API emitted completion only for one-shot
            // animations. Official loop-complete notifications remain inside
            // AnimationState and are intentionally not forwarded here.
            if (runtimeEntry.Loop || _completedFired) return;
            _completedFired = true;
            Completed?.Invoke();
            _owner?.EmitAnimationCompleted(this);
        }
    }

    /// <summary>
    /// Project-facing Spine component backed entirely by official
    /// SkeletonAnimation, Skeleton, AnimationState and MeshGenerator objects.
    /// The class name and serialized fields remain stable so existing prefabs
    /// migrate without losing references.
    /// </summary>
    public sealed class SpineSprite : MonoBehaviour
    {
        public float TimeScale = 1f;
        public Color Tint = Color.white;

        public event Action<TrackEntry> AnimationCompleted;

        public SkeletonData Data { get; private set; }

        public SkeletonAnimation Runtime => _runtime;

        public Spine.ExposedList<Spine.Bone> Bones =>
            _runtime?.Skeleton?.Bones;

        public Spine.ExposedList<Spine.Slot> Slots =>
            _runtime?.Skeleton?.Slots;

        readonly Dictionary<Spine.TrackEntry, TrackEntry> _entries =
            new Dictionary<Spine.TrackEntry, TrackEntry>();
        readonly Dictionary<string, string> _attachmentOverrides =
            new Dictionary<string, string>(StringComparer.Ordinal);

        SkeletonAnimation _runtime;
        MeshRenderer _meshRenderer;
        string _module;
        int _sortingOrder;
        string _sortingLayer = "Default";
        Color _appliedTint = new Color(-1f, -1f, -1f, -1f);

        public int SortingOrder
        {
            get => _meshRenderer != null
                ? _meshRenderer.sortingOrder
                : _sortingOrder;
            set
            {
                _sortingOrder = value;
                if (_meshRenderer != null)
                    _meshRenderer.sortingOrder = value;
            }
        }

        public string SortingLayerName
        {
            get => _meshRenderer != null
                ? _meshRenderer.sortingLayerName
                : _sortingLayer;
            set
            {
                _sortingLayer = string.IsNullOrEmpty(value)
                    ? "Default"
                    : value;
                if (_meshRenderer != null)
                    _meshRenderer.sortingLayerName = _sortingLayer;
            }
        }

        public void Load(string module)
        {
            EnsureRuntime();
            OfficialSpineModule loaded = OfficialSpineAssets.Load(
                module,
                _runtime.skeletonDataAsset);
            if (loaded == null)
            {
                Data = null;
                return;
            }

            _entries.Clear();
            _attachmentOverrides.Clear();
            _module = module;
            Data = loaded.CompatibilityData;

            _runtime.skeletonDataAsset = loaded.Asset;
            _runtime.Initialize(true, false);
            _runtime.UpdateTiming = UpdateTiming.ManualUpdate;
            _runtime.UpdateMode = UpdateMode.FullUpdate;
            _runtime.updateWhenInvisible = UpdateMode.FullUpdate;
            _runtime.timeScale = 1f;
            _runtime.pmaVertexColors = true;
            _runtime.useClipping = true;
            _runtime.UpdateComplete -= OnRuntimeUpdateComplete;
            _runtime.UpdateComplete += OnRuntimeUpdateComplete;

            ApplyPresentation(true);
            ApplyAndRender();
        }

        public bool HasAnimation(string name)
        {
            return Data?.RuntimeData?.FindAnimation(name) != null;
        }

        public List<string> AnimationNames()
        {
            var names = new List<string>();
            if (Data == null) return names;
            foreach (KeyValuePair<string, SpineAnimation> animation in
                     Data.Animations)
            {
                names.Add(animation.Key);
            }
            return names;
        }

        public void SetSkin(string name)
        {
            Spine.Skeleton skeleton = _runtime?.Skeleton;
            if (skeleton == null || string.IsNullOrEmpty(name)) return;
            Spine.Skin skin = skeleton.Data.FindSkin(name);
            if (skin == null)
            {
                Debug.LogWarning(
                    $"Official Spine skin '{name}' does not exist in " +
                    $"'{_module}'.",
                    this);
                return;
            }
            skeleton.SetSkin(skin);
            skeleton.SetSlotsToSetupPose();
            ApplyAndRender();
        }

        public bool SetAttachment(
            string slotName,
            string attachmentName)
        {
            Spine.Skeleton skeleton = _runtime?.Skeleton;
            if (skeleton == null || string.IsNullOrEmpty(slotName))
                return false;
            Spine.SlotData slotData = skeleton.Data.FindSlot(slotName);
            if (slotData == null) return false;
            Spine.Attachment attachment = attachmentName == null
                ? null
                : skeleton.GetAttachment(slotData.Index, attachmentName);
            if (attachmentName != null && attachment == null)
                return false;

            skeleton.Slots.Items[slotData.Index].Attachment = attachment;
            _attachmentOverrides[slotName] = attachmentName;
            ApplyAndRender();
            return true;
        }

        public bool ClearAttachmentOverride(string slotName)
        {
            if (!_attachmentOverrides.Remove(slotName)) return false;
            Spine.Slot slot = _runtime?.Skeleton?.FindSlot(slotName);
            if (slot == null) return false;
            slot.SetToSetupPose();
            ApplyAndRender();
            return true;
        }

        public void SetDefaultMix(float mix)
        {
            if (_runtime?.AnimationState?.Data != null)
                _runtime.AnimationState.Data.DefaultMix = Mathf.Max(0f, mix);
        }

        public TrackEntry SetAnimation(
            string name,
            bool loop,
            float mixDuration = -1f)
        {
            if (!HasAnimation(name) || _runtime?.AnimationState == null)
                return null;
            Spine.TrackEntry runtimeEntry =
                _runtime.AnimationState.SetAnimation(0, name, loop);
            if (mixDuration >= 0f)
                runtimeEntry.MixDuration = mixDuration;
            return Wrap(runtimeEntry);
        }

        public TrackEntry AddAnimation(
            string name,
            float delay,
            bool loop,
            float mixDuration = -1f)
        {
            if (!HasAnimation(name) || _runtime?.AnimationState == null)
                return null;
            Spine.TrackEntry runtimeEntry =
                _runtime.AnimationState.AddAnimation(0, name, loop, delay);
            if (mixDuration >= 0f)
            {
                runtimeEntry.MixDuration = mixDuration;
                if (delay <= 0f && runtimeEntry.Previous != null)
                {
                    runtimeEntry.Delay =
                        runtimeEntry.Previous.TrackComplete -
                        mixDuration + delay;
                }
            }
            return Wrap(runtimeEntry);
        }

        public void ClearTracks()
        {
            _runtime?.AnimationState?.ClearTracks();
        }

        public TrackEntry Current => Wrap(
            _runtime?.AnimationState?.GetCurrent(0));

        public void ApplyAndRender()
        {
            if (_runtime == null || !_runtime.valid) return;
            ApplyPresentation(false);
            _runtime.Update(0f);
            _runtime.LateUpdateMesh();
        }

        internal TrackEntry Wrap(Spine.TrackEntry runtimeEntry)
        {
            if (runtimeEntry == null) return null;
            if (_entries.TryGetValue(
                    runtimeEntry,
                    out TrackEntry wrapped))
            {
                return wrapped;
            }
            wrapped = new TrackEntry(this, runtimeEntry);
            _entries[runtimeEntry] = wrapped;
            return wrapped;
        }

        internal void EmitAnimationCompleted(TrackEntry entry)
        {
            AnimationCompleted?.Invoke(entry);
        }

        void EnsureRuntime()
        {
            if (_runtime == null)
                _runtime = GetComponent<SkeletonAnimation>();
            if (_runtime == null)
                _runtime = gameObject.AddComponent<SkeletonAnimation>();

            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _sortingOrder = _meshRenderer.sortingOrder;
            _sortingLayer = _meshRenderer.sortingLayerName;
        }

        void Update()
        {
            if (_runtime == null || !_runtime.valid) return;
            ApplyPresentation(false);
            _runtime.Update(Time.deltaTime * TimeScale);
        }

        void OnRuntimeUpdateComplete(ISkeletonAnimation animation)
        {
            ApplyPresentation(false);
            ApplyAttachmentOverrides();
        }

        void ApplyPresentation(bool force)
        {
            Spine.Skeleton skeleton = _runtime?.Skeleton;
            if (skeleton == null) return;
            if (force || _appliedTint != Tint)
            {
                skeleton.R = Tint.r;
                skeleton.G = Tint.g;
                skeleton.B = Tint.b;
                skeleton.A = Tint.a;
                _appliedTint = Tint;
            }
            if (_meshRenderer != null)
            {
                _meshRenderer.sortingOrder = _sortingOrder;
                _meshRenderer.sortingLayerName = _sortingLayer;
            }
        }

        void ApplyAttachmentOverrides()
        {
            Spine.Skeleton skeleton = _runtime?.Skeleton;
            if (skeleton == null || _attachmentOverrides.Count == 0)
                return;

            foreach (KeyValuePair<string, string> item in
                     _attachmentOverrides)
            {
                Spine.SlotData slotData = skeleton.Data.FindSlot(item.Key);
                if (slotData == null) continue;
                Spine.Attachment attachment = item.Value == null
                    ? null
                    : skeleton.GetAttachment(slotData.Index, item.Value);
                if (item.Value == null || attachment != null)
                    skeleton.Slots.Items[slotData.Index].Attachment = attachment;
            }
        }

        void OnDestroy()
        {
            if (_runtime != null)
                _runtime.UpdateComplete -= OnRuntimeUpdateComplete;
        }
    }
}
