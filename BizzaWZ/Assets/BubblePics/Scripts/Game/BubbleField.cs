using System.Collections.Generic;
using System.Linq;
using BubblePics.GameModes;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Port of bubble_field.gd. Design coordinates: x 0..1080 (left->right),
    /// y 0..2400 (top->down). World = App.DesignToWorld.
    /// </summary>
    public class BubbleField : MonoBehaviour
    {
        public static float ViewW => DeviceLayout.ViewWidth;
        public static float ViewH => DeviceLayout.ViewHeight;
        public const float BASE_RADIUS_RATIO = 0.11111f;
        public const float ASPECT_CAP_W_OVER_H = 0.5f;
        public const float WIDE_PER_ROW_AT_SQUARE = 8f;
        public const float EDGE_MARGIN_PX = 3f;
        public const float NAV_BOTTOM_HEIGHT_PX = 303f;
        public const float NAV_BOTTOM_HEIGHT_PX_V2 = 220f;
        public const float SETTLE_GAP_PX = 1f;
        public const float SETTLE_SCAN_STEP_PX = 6f;
        public const float ADAPTIVE_SETTLE_WINDOW_RADII = 3f;
        const float SPAWN_INITIAL_VY = 1200f;
        const int SPAWN_OVERLAP_MAX_ATTEMPTS = 8;

        public float BaseRadius = 142.105f;
        public float FloorY = 2097f; // design y

        [Header("Prefab authoring")]
        [SerializeField] Transform _container;
        [SerializeField] Transform _walls;
        [SerializeField] BoxCollider2D _wallLeft;
        [SerializeField] BoxCollider2D _wallRight;
        [SerializeField] BoxCollider2D _wallBottom;

        public Transform Container
        {
            get => _container;
            private set => _container = value;
        }

        static bool _fallbackWarningShown;
        int _spawnInFlight;

        public static Vector3 D2W(float dx, float dy) => App.DesignToWorld(new Vector2(dx, dy));
        public static Vector2 W2D(Vector3 w) => App.WorldToDesign(w);

        public void Init(Transform worldRoot)
        {
            ResolveAuthoringReferences();
            if (Container == null || _walls == null)
            {
                if (!_fallbackWarningShown)
                {
                    _fallbackWarningShown = true;
                    Debug.LogWarning(
                        $"{name}: BubbleField prefab hierarchy is unavailable; using compatibility construction. " +
                        "Prefab generation should call BubbleField.SetupPrefabAuthoring().");
                }
                BuildCompatibilityHierarchy(worldRoot != null ? worldRoot : transform);
            }
            ComputeLayout();
        }

        /// <summary>Runtime initialization alias for prefab-based callers.</summary>
        public void InitializePrefabRuntime(Transform worldRoot)
        {
            Init(worldRoot);
        }

        /// <summary>
        /// Editor/generator helper. Creates the fixed container and wall bodies once
        /// so the resulting BubbleWorld prefab can serialize all references.
        /// </summary>
        public void SetupPrefabAuthoring()
        {
            ResolveAuthoringReferences();
            BuildCompatibilityHierarchy(transform);
            BuildWalls();
        }

        public void ApplyDynamicPerRow(float perRow)
        {
            if (perRow > 0) BaseRadius = ViewW / (2f * perRow);
        }

        public void ComputeLayout(float perRowScale = 1f)
        {
            BaseRadius = BubbleLayoutMath.ComputeBaseRadius(
                DeviceLayout.Current,
                perRowScale);

            float navHeight = AppConfig.GameUi2
                ? NAV_BOTTOM_HEIGHT_PX_V2
                : NAV_BOTTOM_HEIGHT_PX;
            FloorY = DeviceLayout.Current.FloorY(navHeight);
        }

        public void BuildWalls()
        {
            ResolveAuthoringReferences();
            if (_walls == null)
                BuildCompatibilityHierarchy(transform);

            float t = 40f, m = EDGE_MARGIN_PX;
            _wallLeft = EnsureWall(_wallLeft, "WallL");
            _wallRight = EnsureWall(_wallRight, "WallR");
            _wallBottom = EnsureWall(_wallBottom, "WallB");
            ConfigureWall(_wallLeft, new Vector2(m - t * 0.5f, ViewH * 0.5f), new Vector2(t, ViewH * 4));
            ConfigureWall(_wallRight, new Vector2(ViewW - m + t * 0.5f, ViewH * 0.5f), new Vector2(t, ViewH * 4));
            ConfigureWall(_wallBottom, new Vector2(ViewW * 0.5f, FloorY - m + t * 0.5f), new Vector2(ViewW * 2, t));
        }

        void ResolveAuthoringReferences()
        {
            if (Container == null) Container = transform.Find("BubbleContainer");
            if (_walls == null) _walls = transform.Find("Walls");
            if (_walls == null) return;
            if (_wallLeft == null) _wallLeft = FindWall("WallL");
            if (_wallRight == null) _wallRight = FindWall("WallR");
            if (_wallBottom == null) _wallBottom = FindWall("WallB");
        }

        BoxCollider2D FindWall(string wallName)
        {
            var wall = _walls != null ? _walls.Find(wallName) : null;
            return wall != null ? wall.GetComponent<BoxCollider2D>() : null;
        }

        void BuildCompatibilityHierarchy(Transform parent)
        {
            if (Container == null)
            {
                Container = new GameObject("BubbleContainer").transform;
                Container.SetParent(parent, false);
            }
            if (_walls == null)
            {
                _walls = new GameObject("Walls").transform;
                _walls.SetParent(parent, false);
            }
            _wallLeft = EnsureWall(_wallLeft, "WallL");
            _wallRight = EnsureWall(_wallRight, "WallR");
            _wallBottom = EnsureWall(_wallBottom, "WallB");
        }

        BoxCollider2D EnsureWall(BoxCollider2D current, string wallName)
        {
            if (current != null) return current;
            var existing = FindWall(wallName);
            if (existing != null) return existing;

            var go = new GameObject(wallName);
            go.transform.SetParent(_walls, false);
            var col = go.AddComponent<BoxCollider2D>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            return col;
        }

        static void ConfigureWall(BoxCollider2D col, Vector2 designPos, Vector2 size)
        {
            if (col == null) return;
            col.transform.position = D2W(designPos.x, designPos.y);
            col.size = size;
            col.enabled = true;
            var rb = col.GetComponent<Rigidbody2D>();
            if (rb == null) rb = col.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
        }

        void ApplyPlayBounds(BubbleView b)
        {
            b.SetPlayBounds(EDGE_MARGIN_PX, ViewW - EDGE_MARGIN_PX, FloorY);
        }

        // ------------------------------------------------------------ spawning
        public BubbleView SpawnToken(string token, Texture2D[] textures, float targetX = -1f)
        {
            var frag = BubbleFragment.FromToken(token);
            if (frag == null || frag.ImageId < 0 || frag.ImageId >= textures.Length) return null;
            var b = BubbleView.Create(Container, frag, textures[frag.ImageId], BaseRadius);
            ApplyPlayBounds(b);
            var designPos = targetX >= 0 ? SpawnPosForColumn(targetX) : ResolveSpawnPosition();
            b.transform.position = D2W(designPos.x, designPos.y);
            b.Body.velocity = new Vector2(Random.Range(-30f, 30f), -SPAWN_INITIAL_VY);
            return b;
        }

        public BubbleView SpawnSpecial(BubbleFragment fragment, Texture2D texture, float targetX = -1f)
        {
            if (fragment == null) return null;
            var bubble = BubbleView.Create(Container, fragment, texture, BaseRadius);
            if (bubble == null) return null;
            ApplyPlayBounds(bubble);
            var designPos = targetX >= 0f ? SpawnPosForColumn(targetX) : ResolveSpawnPosition();
            bubble.transform.position = D2W(designPos.x, designPos.y);
            bubble.Body.velocity = new Vector2(Random.Range(-30f, 30f), -SPAWN_INITIAL_VY);
            return bubble;
        }

        public BubbleView SpawnRestored(BubbleProgress progress, Texture2D[] textures)
        {
            if (progress == null) return null;
            var frag = BubbleFragment.FromToken(progress.token);
            if (frag == null || frag.ImageId < 0 || frag.ImageId >= textures.Length)
                return null;
            var bubble = BubbleView.Create(
                Container, frag, textures[frag.ImageId], BaseRadius);
            ApplyPlayBounds(bubble);
            bubble.transform.position = new Vector3(
                progress.worldX, progress.worldY, 0f);
            bubble.Body.velocity = new Vector2(
                progress.velocityX, progress.velocityY);
            int lockCount = progress.lockCount > 0
                ? progress.lockCount
                : progress.locked ? 1 : 0;
            if (AppConfig.LockedMode && lockCount > 0)
                bubble.SetLock(lockCount);
            if (progress.landed) bubble.MarkLandedSettled();
            return bubble;
        }

        /// <summary>
        /// Restores a saved board in deterministic current-layout positions.
        /// Persisted world coordinates are intentionally ignored because they
        /// may have been captured with a different safe area or aspect ratio.
        /// </summary>
        public List<BubbleView> SpawnRestoredSettled(
            IReadOnlyList<BubbleProgress> progresses,
            Texture2D[] textures)
        {
            var spawned = new List<BubbleView>();
            if (progresses == null || progresses.Count == 0 ||
                textures == null || textures.Length == 0)
                return spawned;

            BeginSpawnBatch();
            try
            {
                foreach (BubbleProgress progress in progresses)
                {
                    if (progress == null) continue;
                    var frag = BubbleFragment.FromToken(progress.token);
                    if (frag == null || frag.ImageId < 0 ||
                        frag.ImageId >= textures.Length)
                        continue;

                    var bubble = BubbleView.Create(
                        Container, frag, textures[frag.ImageId], BaseRadius);
                    if (bubble == null) continue;
                    ApplyPlayBounds(bubble);
                    int lockCount = progress.lockCount > 0
                        ? progress.lockCount
                        : progress.locked ? 1 : 0;
                    if (AppConfig.LockedMode && lockCount > 0)
                        bubble.SetLock(lockCount);
                    spawned.Add(bubble);
                }

                var radii = spawned.Select(b => b.GetRadius()).ToList();
                var positions = ComputeSettledPositions(radii);
                for (int i = 0; i < spawned.Count; i++)
                {
                    BubbleView bubble = spawned[i];
                    bubble.transform.position =
                        D2W(positions[i].x, positions[i].y);
                    if (bubble.Body != null)
                        bubble.Body.velocity = Vector2.zero;
                    bubble.MarkLandedSettled();
                }
            }
            finally
            {
                EndSpawnBatch();
            }
            return spawned;
        }

        Vector2 SpawnPosForColumn(float targetX)
        {
            float xMin = EDGE_MARGIN_PX + BaseRadius;
            float xMax = ViewW - EDGE_MARGIN_PX - BaseRadius;
            float jitter = BaseRadius * 0.1f;
            float x = Mathf.Clamp(targetX + Random.Range(-jitter, jitter), xMin, xMax);
            return new Vector2(x, Random.Range(-BaseRadius * 3.5f, -BaseRadius * 2.5f));
        }

        Vector2 ResolveSpawnPosition()
        {
            float xMin = EDGE_MARGIN_PX + BaseRadius;
            float xMax = ViewW - EDGE_MARGIN_PX - BaseRadius;
            float bandTop = -BaseRadius * 3.5f, bandBottom = -BaseRadius * 2.5f;
            for (int attempt = 0; attempt < SPAWN_OVERLAP_MAX_ATTEMPTS; attempt++)
            {
                var cand = new Vector2(Random.Range(xMin, xMax), Random.Range(bandTop, bandBottom));
                if (Physics2D.OverlapCircle(D2W(cand.x, cand.y), BaseRadius) == null) return cand;
            }
            return new Vector2(Random.Range(xMin, xMax), Random.Range(bandTop, bandBottom));
        }

        /// <summary>Level 1 opening: spread across width and drop.</summary>
        public void SpawnWaveSpread(List<string> tokens, Texture2D[] textures)
        {
            int n = tokens.Count;
            if (n == 0) return;
            var frags = tokens.Select(BubbleFragment.FromToken).ToList();
            var radii = frags.Select(f => BaseRadius *
                (f != null && f.TangramMold
                    ? TangramMatchContent.MoldRadiusScale
                    : BubbleView.RadiusScaleFor(f?.HeldPaths.Count ?? 1)))
                .ToList();
            float totalDiam = radii.Sum(r => 2f * r);
            float usable = ViewW - 2 * EDGE_MARGIN_PX;
            float gap = Mathf.Max(0, usable - totalDiam) / (n + 1);
            float cursor = EDGE_MARGIN_PX + gap;
            for (int i = 0; i < n; i++)
            {
                var frag = frags[i];
                if (frag == null || frag.ImageId < 0 || frag.ImageId >= textures.Length) continue;
                var b = BubbleView.Create(Container, frag, textures[frag.ImageId], BaseRadius);
                ApplyPlayBounds(b);
                b.transform.position = D2W(cursor + radii[i], -BaseRadius - radii[i]);
                b.Body.velocity = new Vector2(0, -SPAWN_INITIAL_VY);
                cursor += 2 * radii[i] + gap;
            }
        }

        /// <summary>Opening wave: pre-settled placement (shuffled when no target_xs).</summary>
        public List<BubbleView> SpawnWaveSettled(List<string> tokens, Texture2D[] textures, List<float> targetXs = null)
        {
            var spawned = new List<BubbleView>();
            int n = tokens.Count;
            if (n == 0) return spawned;
            var frags = tokens.Select(BubbleFragment.FromToken).ToList();
            var radii = frags.Select(f => BaseRadius *
                (f != null && f.TangramMold
                    ? TangramMatchContent.MoldRadiusScale
                    : BubbleView.RadiusScaleFor(f?.HeldPaths.Count ?? 1)))
                .ToList();
            var orderFrags = frags;
            var orderRadii = radii;
            var orderXs = targetXs ?? new List<float>();
            if (targetXs == null || targetXs.Count == 0)
            {
                var indices = Enumerable.Range(0, n).ToList();
                for (int i = indices.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (indices[i], indices[j]) = (indices[j], indices[i]);
                }
                orderFrags = indices.Select(i => frags[i]).ToList();
                orderRadii = indices.Select(i => radii[i]).ToList();
            }
            var positions = ComputeSettledPositions(orderRadii, orderXs);
            for (int i = 0; i < n; i++)
            {
                var frag = orderFrags[i];
                if (frag == null || frag.ImageId < 0 || frag.ImageId >= textures.Length) continue;
                var b = BubbleView.Create(Container, frag, textures[frag.ImageId], BaseRadius);
                ApplyPlayBounds(b);
                b.transform.position = D2W(positions[i].x, positions[i].y);
                b.Body.velocity = Vector2.zero;
                b.MarkLandedSettled();
                spawned.Add(b);
            }
            return spawned;
        }

        public List<Vector2> ComputeSettledPositions(List<float> radii, List<float> targetXs = null)
        {
            var positions = new List<Vector2>();
            int n = radii.Count;
            if (n == 0) return positions;
            float window = BaseRadius * ADAPTIVE_SETTLE_WINDOW_RADII;
            for (int i = 0; i < n; i++)
            {
                float r = radii[i];
                float xMin = EDGE_MARGIN_PX + r;
                float xMax = ViewW - EDGE_MARGIN_PX - r;
                if (xMax < xMin) xMax = xMin;
                float tx = targetXs != null && i < targetXs.Count ? targetXs[i] : -1f;
                if (tx >= 0)
                {
                    float winMin = Mathf.Max(xMin, tx - window);
                    float winMax = Mathf.Min(xMax, tx + window);
                    if (winMax >= winMin) { xMin = winMin; xMax = winMax; }
                }
                float bestX = xMin, bestY = float.NegativeInfinity;
                float x = xMin;
                while (x <= xMax + 0.01f)
                {
                    float candX = Mathf.Min(x, xMax);
                    float candY = SettleRestY(candX, r, positions, radii);
                    if (candY > bestY + 0.01f) { bestY = candY; bestX = candX; }
                    x += SETTLE_SCAN_STEP_PX;
                }
                positions.Add(new Vector2(bestX, bestY));
            }
            return positions;
        }

        float SettleRestY(float x, float r, List<Vector2> placed, List<float> placedRadii)
        {
            // NOTE Godot y-down: rest = floor - r, stacking = smaller y.
            // We keep design coords: pick the LARGEST y (lowest on screen)
            // that does not overlap => min over stacked candidates.
            float restY = FloorY - r;
            for (int j = 0; j < placed.Count; j++)
            {
                var c = placed[j];
                float rj = placedRadii[j];
                float minDist = r + rj + SETTLE_GAP_PX;
                float dx = Mathf.Abs(x - c.x);
                if (dx >= minDist) continue;
                float dy = Mathf.Sqrt(minDist * minDist - dx * dx);
                restY = Mathf.Min(restY, c.y - dy);
            }
            return restY;
        }

        public void BeginSpawnBatch() { _spawnInFlight++; }
        public void EndSpawnBatch() { _spawnInFlight = Mathf.Max(0, _spawnInFlight - 1); }
        public bool IsSpawnInFlight => _spawnInFlight > 0;

        // ------------------------------------------------------------ board queries
        public List<BubbleView> AllBubbles()
        {
            var list = new List<BubbleView>();
            if (Container == null) return list;
            foreach (Transform c in Container)
            {
                var b = c.GetComponent<BubbleView>();
                if (b != null && b.State != BubbleState.Dead) list.Add(b);
            }
            return list;
        }

        public int CountBoardBubbles() => AllBubbles().Count;

        public bool HasAliveBubble() => AllBubbles().Any(b => b.State == BubbleState.Alive);

        public bool AreAllBubblesLanded()
        {
            bool anyAlive = false;
            foreach (var b in AllBubbles())
            {
                if (b.State != BubbleState.Alive) continue;
                anyAlive = true;
                if (!b.HasLanded) return false;
            }
            return anyAlive;
        }

        public bool AreAllBubblesAtRest(float maxSpeed)
        {
            bool anyAlive = false;
            foreach (var b in AllBubbles())
            {
                if (b.State != BubbleState.Alive) continue;
                anyAlive = true;
                if (!b.HasLanded) return false;
                if (b.Body.velocity.magnitude > maxSpeed) return false;
            }
            return anyAlive;
        }

        public int CountBoardBubbleSlots()
        {
            int sum = 0;
            foreach (var b in AllBubbles())
            {
                if (b.State != BubbleState.Alive || b.Fragment == null) continue;
                sum += b.Fragment.HeldPaths.Count;
            }
            return sum;
        }

        public bool HasCompletableGroup()
        {
            var groups = new Dictionary<string, HashSet<int>>();
            foreach (var b in AllBubbles())
            {
                if (b.State != BubbleState.Alive || b.Fragment == null) continue;
                string key = b.Fragment.ImageId + "|" + string.Join(",", b.Fragment.ParentPath());
                if (!groups.TryGetValue(key, out var quads))
                    groups[key] = quads = new HashSet<int>();
                foreach (int q in b.Fragment.LastQuadrants()) quads.Add(q);
            }
            return groups.Values.Any(q => q.Count >= 4);
        }

        public void ClearBoard()
        {
            foreach (var b in AllBubbles()) Destroy(b.gameObject);
        }
    }
}
