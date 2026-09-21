using System.Collections;
using System.Collections.Generic;
using BubblePics.GameModes;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BubblePics
{
    /// <summary>
    /// Port of bubble_input_controller.gd. Works in design coordinates
    /// (y down); positions converted from screen via App camera.
    /// </summary>
    public class InputController : MonoBehaviour
    {
        const float RIPPLE_DURATION = 0.34f;
        const float RIPPLE_PUSH_K = 0.13f;
        const float RIPPLE_TRANSMIT_K = 0.6f;
        const float RIPPLE_FRICTION_K = 0.03f;
        const float RIPPLE_MAX_DISP_K = 0.35f;
        const float RIPPLE_LAYER_DELAY = 0.05f;
        const int RIPPLE_MAX_LAYERS = 6;
        const float RIPPLE_LINK_GAP = 0.4f;
        const float DROP_IN_PLACE_MIN_DIST_RATIO = 1.0f;
        const float TAP_MAX_MOVE_RATIO = 1f / 72f;
        const float AVOID_ADD_RATE_Y = 1.134f;
        const float AVOID_MIN_RATE = 0.758f;
        const float AVOID_HOTAREA = 25f;
        const float AVOID_X_CONVERGE_RATE = 0.1f;
        const float DST_CONFLICT_WAIT_SEC = 1.5f;

        public BubblePage Page;
        public bool InputEnabled = true;

        [Header("Prefab authoring")]
        [SerializeField] BubbleTapFx _tapFx;

        BubbleView _dragging;
        Vector2 _dragOffset;      // design space
        BubbleView _hover;
        Vector2 _pressPos;
        bool _emptyDrag;
        bool _avoidFingerActive;
        Vector2 _lastMousePos;
        bool _fingerLockdownY;
        bool _uiGesture;

        public BubbleView Dragging => _dragging;
        public bool HasTapFxAuthoring => _tapFx != null && _tapFx.HasRequiredReferences;

        public void SetupPrefabAuthoring(Material sharedParticleMaterial = null)
        {
            ResolveTapFxReference();
            if (_tapFx == null)
            {
                var child = new GameObject("BubbleTapFx");
                child.transform.SetParent(transform, false);
                _tapFx = child.AddComponent<BubbleTapFx>();
            }
            _tapFx.SetupPrefabAuthoring(sharedParticleMaterial);
        }

        void Awake()
        {
            ResolveTapFxReference();
            _tapFx?.InitializeRuntime();
        }

        public void ResetState()
        {
            _tapFx?.EndTrail();
            _dragging = null;
            _avoidFingerActive = false;
            _fingerLockdownY = false;
            _emptyDrag = false;
            _uiGesture = false;
            ClearHoverTarget();
        }

        void ResolveTapFxReference()
        {
            if (_tapFx == null) _tapFx = GetComponentInChildren<BubbleTapFx>(true);
        }

        bool EnsureTapFx()
        {
            ResolveTapFxReference();
            if (_tapFx != null) return _tapFx.HasRequiredReferences || _tapFx.InitializeRuntime();

            // Only catalog-less legacy construction reaches this path. Normal
            // gameplay receives the fixed emitters from BubblePage.prefab.
            Debug.LogWarning(
                "BubblePage prefab has no BubbleTapFx; using compatibility authoring.");
            var child = new GameObject("BubbleTapFx");
            child.transform.SetParent(transform, false);
            _tapFx = child.AddComponent<BubbleTapFx>();
            _tapFx.SetupPrefabAuthoring();
            return _tapFx.InitializeRuntime();
        }

        Vector2 MouseDesign()
        {
            App app = App.I;
            Camera camera = app != null ? app.Cam : null;
            if (camera == null) return _lastMousePos;
            var w = camera.ScreenToWorldPoint(Input.mousePosition);
            return App.WorldToDesign(w);
        }

        void Update()
        {
#if UNITY_EDITOR
            if (Input.GetMouseButtonDown(0))
            {
                var hits = new List<RaycastResult>();
                if (EventSystem.current != null)
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Input.mousePosition }, hits);
                string firstHit = hits.Count > 0 ? hits[0].gameObject.name : "none";
                Debug.Log("[BubbleInput] down screen=" + Input.mousePosition + " design=" + MouseDesign() + " ui=" + firstHit + " enabled=" + InputEnabled + " blocked=" + BizzaGameplayBridge.IsInputBlocked);
            }
#endif
            App app = App.I;
            if (!InputEnabled || Page == null || app == null || app.Cam == null || BizzaGameplayBridge.IsInputBlocked)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                _uiGesture = IsPointerOverUi();
                if (_uiGesture)
                {
                    if (Input.GetMouseButtonUp(0)) _uiGesture = false;
                    return;
                }
                var pos = MouseDesign();
                TryStartDrag(pos);
                if (_dragging == null)
                {
                    _emptyDrag = true;
                    if (EnsureTapFx())
                    {
                        var worldPosition = App.DesignToWorld(pos);
                        _tapFx.Burst(worldPosition);
                        _tapFx.BeginTrail(worldPosition);
                    }
                }
            }
            if (_uiGesture)
            {
                if (Input.GetMouseButtonUp(0)) _uiGesture = false;
                return;
            }
            if (Input.GetMouseButton(0) && !Input.GetMouseButtonDown(0) && (_dragging != null || _emptyDrag))
            {
                HandleDragMotion(MouseDesign());
            }
            // Press and release can be sampled in the same frame. Always finish
            // that gesture so the bubble cannot remain captured after a short tap.
            if (Input.GetMouseButtonUp(0) && (_dragging != null || _emptyDrag))
            {
                HandleDragRelease();
            }
        }

        static bool IsPointerOverUi()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            if (Input.touchCount > 0)
                return eventSystem.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return eventSystem.IsPointerOverGameObject();
        }

        // ------------------------------------------------------------ press
        void TryStartDrag(Vector2 pos)
        {
            if (Page.IsInteractionLocked()) return;
            Page.ClearHintHighlight();
            BubbleView nearest = null;
            float nearestD = 9e9f;
            foreach (var b in Page.Field.AllBubbles())
            {
                if (b.State != BubbleState.Alive || b.Dragging || b.ReturningHome) continue;
                if (Page.Merge.IsMergingTarget(b)) continue;
                var bd = App.WorldToDesign(b.transform.position);
                float d = Vector2.Distance(bd, pos);
                if (d <= b.GetRadius() && d < nearestD)
                {
                    nearest = b;
                    nearestD = d;
                }
            }
            if (nearest == null) return;

            _dragging = nearest;
            _pressPos = pos;
            _dragOffset = App.WorldToDesign(nearest.transform.position) - pos;
            _avoidFingerActive = true;
            _fingerLockdownY = false;
            _lastMousePos = pos;
            // touch_sound=no is the original experiment's inverted group:
            // play on pickup and suppress candidate-touch sounds.
            if (!AppConfig.TouchSoundOn) SoundManager.I.Play("collide");
            nearest.StartDrag();
            var firstTarget = new Vector2(
                Mathf.Clamp(pos.x + _dragOffset.x, 0, BubbleField.ViewW),
                Mathf.Clamp(pos.y + _dragOffset.y, 0, Page.Field.FloorY));
            nearest.UpdateDrag(App.DesignToWorld(nearest.ClampIntoBounds(firstTarget)));
            nearest.SetPickupEnlarged(true);
            ApplyRipple(nearest);
            Page.EmitDragStarted(nearest);
        }

        // ------------------------------------------------------------ motion
        void HandleDragMotion(Vector2 mouse)
        {
            if (_emptyDrag)
            {
                _tapFx?.UpdateTrail(App.DesignToWorld(mouse));
                return;
            }
            if (_dragging == null || !_dragging)
            {
                _dragging = null;
                return;
            }
            if (_dragging != null)
            {
                var bubble = App.WorldToDesign(_dragging.transform.position);
                Vector2 diff = mouse - _lastMousePos;
                float r = _dragging.GetRadius();

                float bubbleXFollow = bubble.x + diff.x;
                float bubbleX = bubbleXFollow - (bubbleXFollow - mouse.x) * AVOID_X_CONVERGE_RATE;

                float bubbleY;
                if (_avoidFingerActive)
                {
                    float dy = diff.y * (diff.y > 0 ? AVOID_MIN_RATE : AVOID_ADD_RATE_Y);
                    bubbleY = bubble.y + dy;
                    float hotMin = (bubbleY - r) - AVOID_HOTAREA;
                    float hotMax = (bubbleY + r) + AVOID_HOTAREA;
                    if (mouse.y < hotMin) { bubbleY += mouse.y - hotMin; _avoidFingerActive = false; }
                    else if (mouse.y > hotMax) { bubbleY += mouse.y - hotMax; _avoidFingerActive = false; }
                }
                else
                {
                    bubbleY = bubble.y + diff.y;
                }

                if (mouse.y < bubble.y - r) _fingerLockdownY = true;
                if (_fingerLockdownY)
                {
                    if (mouse.y >= bubble.y + r)
                    {
                        bubbleY = mouse.y - r;
                        _fingerLockdownY = false;
                    }
                    else if (bubbleY > bubble.y)
                    {
                        bubbleY = bubble.y;
                    }
                }

                var clamped = _dragging.ClampIntoBounds(new Vector2(bubbleX, bubbleY));
                _dragging.UpdateDrag(App.DesignToWorld(clamped));
                _lastMousePos = mouse;
                UpdateHoverTarget(_dragging);
            }
        }

        // ------------------------------------------------------------ release
        void HandleDragRelease()
        {
            if (_emptyDrag)
            {
                _emptyDrag = false;
                _tapFx?.EndTrail();
            }
            if (_dragging == null) return;
            var src = _dragging;
            _dragging = null;
            EndDrag(src);
        }

        void EndDrag(BubbleView src)
        {
            var mouse = MouseDesign();
            bool moved = (mouse - _pressPos).magnitude >= BubbleField.ViewW * TAP_MAX_MOVE_RATIO;
            Page.EmitDragReleased(src, moved);
            src.ClearDragFlag();
            Fx.SpawnDragReleaseBurst(src.transform.position, src.transform.parent);
            var target = FindOverlapTargetAt(App.WorldToDesign(src.transform.position), src);
            ClearHoverTarget();
            StartCoroutine(ResolveDragRelease(src, target));
        }

        void OnDisable()
        {
            _tapFx?.EndTrail();
        }

        // ------------------------------------------------------------ hover
        BubbleView FindOverlapTargetAt(Vector2 designPos, BubbleView src)
        {
            float srcR = src.GetRadius();
            BubbleView best = null, rightBest = null;
            float bestOverlap = -1, rightBestOverlap = -1;
            foreach (var b in Page.Field.AllBubbles())
            {
                if (b == src || b.State != BubbleState.Alive) continue;
                if (b.IsFull()) continue;
                var bd = App.WorldToDesign(b.transform.position);
                float overlap = srcR + b.GetRadius() - Vector2.Distance(designPos, bd);
                bool isCorrect = src.CanMergeWith(b);
                float threshold = AppConfig.MergeDistanceNew
                    ? (isCorrect ? src.BaseRadius * 0.25f : src.BaseRadius * 0.5f)
                    : BubbleField.ViewW / 30f;
                if (overlap <= threshold) continue;
                if (overlap > bestOverlap) { bestOverlap = overlap; best = b; }
                if (isCorrect && overlap > rightBestOverlap) { rightBestOverlap = overlap; rightBest = b; }
            }
            return rightBest != null ? rightBest : best;
        }

        void UpdateHoverTarget(BubbleView src)
        {
            var candidate = FindOverlapTargetAt(App.WorldToDesign(src.transform.position), src);
            if (candidate == _hover) return;
            if (_hover != null) _hover.SetHovered(false);
            _hover = candidate;
            if (_hover != null)
            {
                _hover.SetHovered(true);
                _hover.SetPickupEnlarged(true);
                ApplyRipple(_hover);
                if (AppConfig.TouchSoundOn) SoundManager.I.Play("collide");
                if (AppConfig.MoveVibrationOn)
                    Haptics.Play(HapticLevel.VeryWeak);
            }
        }

        void ClearHoverTarget()
        {
            if (_hover != null) _hover.SetHovered(false);
            _hover = null;
        }

        // ------------------------------------------------------------ ripple
        void ApplyRipple(BubbleView trigger)
        {
            float baseR = Page.Field.BaseRadius;
            float gap = baseR * RIPPLE_LINK_GAP;
            float friction = baseR * RIPPLE_FRICTION_K;
            float maxDisp = baseR * RIPPLE_MAX_DISP_K;
            float seed = baseR * RIPPLE_PUSH_K;

            var pool = new List<BubbleView>();
            foreach (var b in Page.Field.AllBubbles())
            {
                if (b.State != BubbleState.Alive || b == trigger || b.Dragging) continue;
                if (b.Body.isKinematic) continue;
                pool.Add(b);
            }

            var forces = new Dictionary<BubbleView, Vector2>();
            var layerOf = new Dictionary<BubbleView, int>();
            var triggerD = App.WorldToDesign(trigger.transform.position);
            float triggerR = trigger.GetRadius();

            // layer 1
            foreach (var b in pool)
            {
                var bd = App.WorldToDesign(b.transform.position);
                float d = Vector2.Distance(triggerD, bd);
                if (d <= triggerR + b.GetRadius() + gap)
                {
                    Vector2 n = d > 0.01f ? (bd - triggerD) / d : new Vector2(Mathf.Cos(Random.value * 6.28f), Mathf.Sin(Random.value * 6.28f));
                    forces[b] = n * seed;
                    layerOf[b] = 1;
                }
            }
            // propagate
            for (int layer = 1; layer < RIPPLE_MAX_LAYERS; layer++)
            {
                var current = new List<BubbleView>();
                foreach (var kv in layerOf) if (kv.Value == layer) current.Add(kv.Key);
                if (current.Count == 0) break;
                foreach (var a in current)
                {
                    var ad = App.WorldToDesign(a.transform.position);
                    var fa = forces[a];
                    foreach (var b in pool)
                    {
                        if (layerOf.ContainsKey(b)) continue;
                        var bd = App.WorldToDesign(b.transform.position);
                        float d = Vector2.Distance(ad, bd);
                        if (d > a.GetRadius() + b.GetRadius() + gap) continue;
                        Vector2 dir2 = d > 0.01f ? (bd - ad) / d : Vector2.right;
                        float along = Vector2.Dot(fa, dir2);
                        if (along <= 0) continue;
                        Vector2 add = dir2 * along * RIPPLE_TRANSMIT_K;
                        forces[b] = forces.TryGetValue(b, out var f) ? f + add : add;
                        layerOf[b] = layer + 1;
                    }
                }
            }
            // apply
            foreach (var kv in forces)
            {
                var b = kv.Key;
                var f = kv.Value;
                if (f.magnitude < friction) continue;
                float amp = Mathf.Min(f.magnitude, maxDisp);
                int layer = layerOf[b];
                float delay = (layer - 1) * RIPPLE_LAYER_DELAY;
                // design dir -> world dir (flip y)
                b.PlayRipple(new Vector2(f.normalized.x, -f.normalized.y), amp, RIPPLE_DURATION, delay);
            }
        }

        // ------------------------------------------------------------ resolve
        IEnumerator ResolveDragRelease(BubbleView src, BubbleView target)
        {
            if (src == null) yield break;
            if (Page.Merge.IsMergingTarget(src))
            {
                yield return ReturnHomeSafe(src);
                yield break;
            }

            if (target != null)
            {
                if (TangramMatchContent.IsTangramImage(src.ImageId) &&
                    TangramMatchContent.IsTangramImage(target.ImageId) &&
                    src.Fragment != null && target.Fragment != null &&
                    !src.Fragment.TangramMold &&
                    !target.Fragment.TangramMold)
                {
                    Toast.Show(Localization.Tr("shape_puzzle_content"));
                    yield return ReturnHomeSafe(src);
                    Page.EmitDragSettled();
                    yield break;
                }
                if (src.IsLocked || target.IsLocked)
                {
                    src.FlashReject();
                    Toast.Show(Localization.Tr("merge_to_unlock"));
                    yield return ReturnHomeSafe(src);
                    Page.EmitDragSettled();
                    yield break;
                }
                if (src.CanMergeWith(target))
                {
                    if (Page.Merge.HasTargetConflict(src, target))
                    {
                        yield return AwaitConflictThenMerge(src, target);
                    }
                    else
                    {
                        yield return CommitMerge(src, target);
                    }
                    yield break;
                }
                // wrong merge
                bool relax = AppConfig.RelaxStepDeduction &&
                    (src.GetRadius() + target.GetRadius()
                     - Vector2.Distance(src.transform.position, target.transform.position)) < src.BaseRadius;
                if (relax)
                {
                    yield return ReturnHomeSafe(src);
                    Page.EmitDragSettled();
                    yield break;
                }
                Page.EmitMergeAttempted(false);
                Page.CommitDeath();
                Fx.Vibrate(2); // MEDIUM
                Page.EmitMergeRejected(src, target);
                SoundManager.I.Play("reject");
                src.FlashReject();
                target.TweenRejectShake();
                yield return ReturnHomeSafe(src);
                Page.RequestDeathUi();
                yield break;
            }

            // no target
            float movedDist = Vector3.Distance(src.transform.position, src.DragOrigin);
            if (movedDist < src.BaseRadius * DROP_IN_PLACE_MIN_DIST_RATIO)
            {
                yield return ReturnHomeSafe(src);
            }
            else
            {
                src.ClearDragFlag();
                src.RestorePhysics();
            }
            Page.EmitDragSettled();
        }

        /// <summary>Runs the return flight on the bubble itself; waits for it
        /// without deadlocking if the bubble is destroyed mid-flight.</summary>
        IEnumerator ReturnHomeSafe(BubbleView src)
        {
            if (src == null) yield break;
            src.StartReturnHome();
            while (src != null && src.ReturningHome) yield return null;
        }

        IEnumerator AwaitConflictThenMerge(BubbleView src, BubbleView target)
        {
            src.ReturningHome = true;
            float t = 0;
            while (t < DST_CONFLICT_WAIT_SEC)
            {
                t += Time.deltaTime;
                if (src == null) yield break;
                if (target == null) break; // target vanished: fall through to return-home
                if (!Page.Merge.HasTargetConflict(src, target))
                {
                    if (src.CanMergeWith(target))
                    {
                        src.ReturningHome = false;
                        yield return CommitMerge(src, target);
                        yield break;
                    }
                    break;
                }
                yield return null;
            }
            // bubble-owned coroutine: survives input resets, always restores
            if (src != null) yield return ReturnHomeSafe(src);
        }

        IEnumerator CommitMerge(BubbleView src, BubbleView target)
        {
            Page.EmitMergeAttempted(true);
            src.ClearDragFlag();
            yield return Page.Merge.Run(src, target);
        }
    }
}
