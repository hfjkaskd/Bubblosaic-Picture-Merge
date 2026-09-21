using System;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// A Godot Control scene can contain world and UI nodes below one root.
    /// The Unity port renders those nodes through separate World/HUD/Panel/
    /// Dialog roots.  A prefab keeps the scene authored as one asset, then
    /// mounts each fixed subtree under the correct runtime root.
    /// </summary>
    public sealed class PrefabMountSet : MonoBehaviour
    {
        public enum MountPoint
        {
            App,
            World,
            Hud,
            Panel,
            Dialog,
            Splash,
        }

        [Serializable]
        public struct Mount
        {
            public MountPoint Point;
            public Transform Root;
        }

        [SerializeField] Mount[] _mounts = Array.Empty<Mount>();
        bool _attached;

        public void Attach(App app)
        {
            if (app == null || _attached) return;
            foreach (var mount in _mounts)
            {
                if (mount.Root == null) continue;
                var parent = Resolve(app, mount.Point);
                if (parent == null) continue;
                mount.Root.SetParent(parent, false);
                ResetMountedTransform(mount.Root);
            }
            _attached = true;
        }

        public void SetMounts(Mount[] mounts)
        {
            _mounts = mounts ?? Array.Empty<Mount>();
        }

        static Transform Resolve(App app, MountPoint point)
        {
            return point switch
            {
                MountPoint.World => app.WorldRoot,
                MountPoint.Hud => app.HudRoot,
                MountPoint.Panel => app.PanelRoot,
                MountPoint.Dialog => app.DialogRoot,
                MountPoint.Splash => app.SplashRoot,
                _ => app.transform,
            };
        }

        static void ResetMountedTransform(Transform root)
        {
            if (root is RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
                return;
            }

            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        void OnDestroy()
        {
            if (!_attached || !Application.isPlaying) return;
            foreach (var mount in _mounts)
            {
                if (mount.Root != null && mount.Root.parent != transform)
                    Destroy(mount.Root.gameObject);
            }
        }
    }
}
