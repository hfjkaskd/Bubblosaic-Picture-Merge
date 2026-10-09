using System.Collections.Generic;
using System.Linq;

namespace BubblePics
{
    /// <summary>Port of bubble_level_parser.gd + bubble_wave_scheduler.gd.
    /// Wave separator '|', token separator ',', combined chips '+'.</summary>
    public class WaveScheduler
    {
        List<List<string>> _waves = new List<List<string>>();
        readonly Dictionary<int, int> _chipTotals = new Dictionary<int, int>();

        public void Build(string layoutRaw, int imageCount)
        {
            _waves.Clear();
            foreach (var waveStr in layoutRaw.Split('|'))
            {
                var tokens = new List<string>();
                foreach (var t in waveStr.Split(','))
                {
                    var trimmed = t.Trim();
                    if (trimmed.Length == 0) continue;
                    tokens.AddRange(NormalizeCombined(trimmed));
                }
                _waves.Add(tokens);
            }
            ComputeChipTotals(layoutRaw);
        }

        static List<string> NormalizeCombined(string entry)
        {
            if (!entry.Contains('+')) return new List<string> { entry };
            BubbleFragment special = BubbleFragment.FromToken(entry);
            if (special != null && special.TangramMold)
                return new List<string> { entry };
            var parts = entry.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (parts.Count <= 1) return parts;
            var imgs = new List<int>();
            var paths = new List<List<int>>();
            foreach (var t in parts)
            {
                var segs = t.Split('.');
                if (!int.TryParse(segs[0], out int img)) return parts;
                imgs.Add(img);
                var path = new List<int>();
                for (int i = 1; i < segs.Length; i++)
                {
                    if (!int.TryParse(segs[i], out int q)) return parts;
                    path.Add(q);
                }
                paths.Add(path);
            }
            return Combinable(imgs, paths) ? new List<string> { entry } : parts;
        }

        static bool Combinable(List<int> imgs, List<List<int>> paths)
        {
            if (paths.Count < 2 || paths.Count > 3) return false;
            int img0 = imgs[0];
            var first = paths[0];
            if (first.Count == 0) return false;
            var parent0 = first.Take(first.Count - 1).ToList();
            var seenQuads = new HashSet<int>();
            for (int i = 0; i < paths.Count; i++)
            {
                if (imgs[i] != img0) return false;
                var p = paths[i];
                if (p.Count == 0) return false;
                var parent = p.Take(p.Count - 1).ToList();
                if (!parent.SequenceEqual(parent0)) return false;
                int q = p[p.Count - 1];
                if (!seenQuads.Add(q)) return false;
            }
            return true;
        }

        void ComputeChipTotals(string layoutRaw)
        {
            _chipTotals.Clear();
            foreach (var t in layoutRaw.Replace("|", ",").Replace("+", ",").Split(','))
            {
                var trimmed = t.Trim();
                if (trimmed.Length == 0) continue;
                var first = trimmed.Split('.')[0];
                if (!int.TryParse(first, out int img)) continue;
                int id = img - 1;
                _chipTotals[id] = _chipTotals.TryGetValue(id, out int c) ? c + 1 : 1;
            }
        }

        public static int MaxImageIndex(string sequence)
        {
            int mx = 0;
            foreach (var t in sequence.Replace("|", ",").Replace("+", ",").Split(','))
            {
                var trimmed = t.Trim();
                if (trimmed.Length == 0) continue;
                var first = trimmed.Split('.')[0];
                if (int.TryParse(first, out int x)) mx = System.Math.Max(mx, x);
            }
            return mx;
        }

        public static Dictionary<int, int> ImageMaxDepths(string sequence)
        {
            var depths = new Dictionary<int, int>();
            foreach (var t in sequence.Replace("|", ",").Replace("+", ",").Split(','))
            {
                var trimmed = t.Trim();
                if (trimmed.Length == 0) continue;
                var segs = trimmed.Split('.');
                if (!int.TryParse(segs[0], out int x)) continue;
                int depth = segs.Length - 1;
                if (!depths.TryGetValue(x, out int d) || depth > d) depths[x] = depth;
            }
            return depths;
        }

        public List<string> PullNextWave()
        {
            if (_waves.Count == 0) return new List<string>();
            var wave = new List<string>(_waves[0]);
            _waves.RemoveAt(0);
            return wave;
        }

        /// <summary>
        /// Select a random-sized drop requested by the caller, completing one
        /// or two sibling groups with the live board. Old images must receive
        /// all their queued pieces before new images can enter the same drop.
        /// Counts that do not fit wait for another closure instead of topping
        /// up four bubbles after every completed picture.
        /// </summary>
        public List<string> PullNextPlayableBatch(
            IEnumerable<BubbleFragment> boardFragments,
            int maxBoardBubbles,
            int maxBoardImages = 3,
            int preferredBatchSize = 8,
            int minBatchSize = 6,
            int maxBatchSize = 10)
        {
            List<PendingToken> selected = SelectPlayableBatch(boardFragments,
                maxBoardBubbles, maxBoardImages, preferredBatchSize,
                minBatchSize, maxBatchSize);
            foreach (var wave in selected.GroupBy(value => value.WaveIndex))
            {
                foreach (PendingToken token in wave.OrderByDescending(
                             value => value.TokenIndex))
                    _waves[wave.Key].RemoveAt(token.TokenIndex);
            }
            return selected.OrderBy(value => value.WaveIndex)
                .ThenBy(value => value.TokenIndex)
                .Select(value => value.Token).ToList();
        }

        public bool CanPullPlayableBatch(
            IEnumerable<BubbleFragment> boardFragments,
            int maxBoardBubbles,
            int maxBoardImages = 3,
            int preferredBatchSize = 8,
            int minBatchSize = 6,
            int maxBatchSize = 10)
        {
            // No random state is consumed by availability checks.
            return SelectPlayableBatch(boardFragments, maxBoardBubbles,
                maxBoardImages, preferredBatchSize, minBatchSize, maxBatchSize).Count > 0;
        }

        List<PendingToken> SelectPlayableBatch(
            IEnumerable<BubbleFragment> boardFragments,
            int maxBoardBubbles,
            int maxBoardImages,
            int preferredBatchSize,
            int minBatchSize,
            int maxBatchSize)
        {
            var board = (boardFragments ?? Enumerable.Empty<BubbleFragment>())
                .Where(value => value != null && !value.IsFull()).ToList();
            var pending = CollectPendingTokens();
            if (pending.Count == 0) return new List<PendingToken>();
            int minimum = System.Math.Max(1, minBatchSize);
            int maximum = System.Math.Max(minimum, maxBatchSize);
            int preferred = System.Math.Max(minimum,
                System.Math.Min(maximum, preferredBatchSize));
            int room = System.Math.Max(0, maxBoardBubbles - board.Count);
            int limit = System.Math.Min(maximum, room);
            var visible = new HashSet<int>(board.Select(value => value.ImageId));
            var oldTokens = pending.Where(value => visible.Contains(value.Fragment.ImageId)).ToList();
            var masks = BoardMasks(board);

            var oldPlans = new DropPlan[limit + 1, 3, 1];
            oldPlans[0, 0, 0] = new DropPlan();
            foreach (var image in oldTokens.GroupBy(value => value.Fragment.ImageId))
                oldPlans = CombineImage(oldPlans,
                    ImageOptions(image.ToList(), masks, limit), false);

            // New pictures are only considered after all pending old pieces
            // are selected, even when a nested image spans several parents.
            int imageSlots = System.Math.Max(0, maxBoardImages - visible.Count);
            var newPlans = new DropPlan[limit + 1, 3, imageSlots + 1];
            if (oldTokens.Count <= limit)
            {
                for (int completed = 0; completed <= 2; completed++)
                    newPlans[oldTokens.Count, completed, 0] =
                        oldPlans[oldTokens.Count, completed, 0];
                foreach (var image in pending.Where(value =>
                             !visible.Contains(value.Fragment.ImageId))
                             .GroupBy(value => value.Fragment.ImageId))
                    newPlans = CombineImage(newPlans,
                        ImageOptions(image.ToList(), masks, limit), true);
            }

            // Try the requested random count first. If that exact count cannot
            // preserve the image/group limits, use another count in the range.
            for (int offset = 0; offset <= maximum - minimum; offset++)
            {
                int count = minimum + (preferred - minimum + offset) %
                    (maximum - minimum + 1);
                DropPlan plan = BestPlan(oldPlans, newPlans, count, visible.Count);
                if (plan != null) return plan.Tokens;
            }

            bool boardReady = masks.Values.Any(value => value == 15);
            // Do not spend pending pieces on a small top-up while the player
            // can clear existing groups. A short final tail is allowed.
            if (pending.Count >= minimum && boardReady)
                return new List<PendingToken>();
            for (int count = System.Math.Min(minimum - 1, limit); count > 0; count--)
            {
                DropPlan plan = BestPlan(oldPlans, newPlans, count, visible.Count);
                if (plan != null) return plan.Tokens;
            }

            // Old snapshots can already be full of stranded pieces. Add only
            // the smallest old-image cover to release space; never a new image.
            if (!boardReady)
            {
                var repair = new DropPlan[5, 3, 1];
                repair[0, 0, 0] = new DropPlan();
                foreach (var image in oldTokens.GroupBy(value => value.Fragment.ImageId))
                    repair = CombineImage(repair,
                        ImageOptions(image.ToList(), masks, 4), false);
                for (int count = 1; count <= 4; count++)
                    if (repair[count, 1, 0] != null)
                        return repair[count, 1, 0].Tokens;
            }
            return new List<PendingToken>();
        }

        sealed class PendingToken
        {
            public int WaveIndex;
            public int TokenIndex;
            public string Token;
            public BubbleFragment Fragment;
        }

        sealed class DropPlan
        {
            public readonly List<PendingToken> Tokens = new List<PendingToken>();
            public int Completed;
            public int Priority;
        }

        List<PendingToken> CollectPendingTokens()
        {
            var pending = new List<PendingToken>();
            for (int waveIndex = 0; waveIndex < _waves.Count; waveIndex++)
            {
                for (int tokenIndex = 0; tokenIndex < _waves[waveIndex].Count; tokenIndex++)
                {
                    string token = _waves[waveIndex][tokenIndex];
                    BubbleFragment fragment = BubbleFragment.FromToken(token);
                    if (fragment == null || fragment.IsFull() || fragment.TangramMold)
                        continue;
                    pending.Add(new PendingToken
                    {
                        WaveIndex = waveIndex,
                        TokenIndex = tokenIndex,
                        Token = token,
                        Fragment = fragment,
                    });
                }
            }
            return pending;
        }

        static Dictionary<string, int> BoardMasks(IEnumerable<BubbleFragment> board)
        {
            var masks = new Dictionary<string, int>();
            foreach (BubbleFragment fragment in board)
            {
                string key = GroupKey(fragment);
                masks.TryGetValue(key, out int mask);
                masks[key] = mask | QuadrantMask(fragment);
            }
            return masks;
        }

        static string GroupKey(BubbleFragment fragment)
        {
            return fragment.ImageId + "|" + string.Join(",", fragment.ParentPath());
        }

        static int QuadrantMask(BubbleFragment fragment)
        {
            int mask = 0;
            foreach (var path in fragment.HeldPaths)
                if (path.Count > 0) mask |= 1 << path[path.Count - 1];
            return mask;
        }

        static DropPlan Join(DropPlan left, DropPlan right)
        {
            var plan = new DropPlan
            {
                Completed = left.Completed + right.Completed,
                Priority = left.Priority + right.Priority,
            };
            plan.Tokens.AddRange(left.Tokens);
            plan.Tokens.AddRange(right.Tokens);
            return plan;
        }

        static List<DropPlan> ImageOptions(
            List<PendingToken> tokens,
            Dictionary<string, int> masks,
            int limit)
        {
            var plans = new DropPlan[limit + 1, 3, 1];
            plans[0, 0, 0] = new DropPlan();
            foreach (var group in tokens.GroupBy(value => GroupKey(value.Fragment)))
            {
                var members = group.ToList();
                masks.TryGetValue(group.Key, out int boardMask);
                var options = new List<DropPlan>();
                // Valid quadrant groups contain at most four disjoint tokens.
                if (members.Count > 4) continue;
                for (int subset = 1; subset < (1 << members.Count); subset++)
                {
                    var option = new DropPlan();
                    int occupied = boardMask;
                    bool compatible = true;
                    for (int i = 0; i < members.Count; i++)
                    {
                        if ((subset & (1 << i)) == 0) continue;
                        int added = QuadrantMask(members[i].Fragment);
                        if (added == 0 || (occupied & added) != 0)
                        {
                            compatible = false;
                            break;
                        }
                        occupied |= added;
                        option.Tokens.Add(members[i]);
                    }
                    if (!compatible || option.Tokens.Count > limit) continue;
                    option.Completed = occupied == 15 ? 1 : 0;
                    // Within old images, prefer completing visible parent groups.
                    option.Priority = boardMask != 0
                        ? option.Tokens.Count + option.Completed * 16 : 0;
                    options.Add(option);
                }
                plans = CombineImage(plans, options, false);
            }
            var result = new List<DropPlan>();
            for (int count = 1; count <= limit; count++)
                for (int completed = 0; completed <= 2; completed++)
                    if (plans[count, completed, 0] != null)
                        result.Add(plans[count, completed, 0]);
            return result;
        }

        static DropPlan[,,] CombineImage(
            DropPlan[,,] plans,
            List<DropPlan> options,
            bool newImage)
        {
            var next = (DropPlan[,,])plans.Clone();
            int limit = plans.GetLength(0) - 1;
            int imageSlots = plans.GetLength(2) - 1;
            for (int count = 0; count <= limit; count++)
            for (int completed = 0; completed <= 2; completed++)
            for (int images = 0; images <= imageSlots; images++)
            {
                DropPlan before = plans[count, completed, images];
                if (before == null) continue;
                int nextImages = images + (newImage ? 1 : 0);
                if (nextImages > imageSlots) continue;
                foreach (DropPlan option in options)
                {
                    int total = count + option.Tokens.Count;
                    int closures = completed + option.Completed;
                    if (total > limit || closures > 2) continue;
                    DropPlan current = next[total, closures, nextImages];
                    if (current == null ||
                        current.Priority < before.Priority + option.Priority)
                        next[total, closures, nextImages] = Join(before, option);
                }
            }
            return next;
        }

        static DropPlan BestPlan(
            DropPlan[,,] oldPlans,
            DropPlan[,,] newPlans,
            int count,
            int visibleImages)
        {
            if (count >= oldPlans.GetLength(0)) return null;
            for (int completed = 2; completed >= 1; completed--)
                if (oldPlans[count, completed, 0] != null)
                    return oldPlans[count, completed, 0];
            // Prefer two pictures on the board, then three. A single picture
            // is retained when only one image remains in the level.
            for (int totalImages = 2; totalImages <= 3; totalImages++)
            {
                int images = totalImages - visibleImages;
                if (images < 1 || images >= newPlans.GetLength(2)) continue;
                for (int completed = 2; completed >= 1; completed--)
                    if (newPlans[count, completed, images] != null)
                        return newPlans[count, completed, images];
            }
            if (visibleImages == 0 && newPlans.GetLength(2) > 1)
                for (int completed = 2; completed >= 1; completed--)
                    if (newPlans[count, completed, 1] != null)
                        return newPlans[count, completed, 1];
            return null;
        }

        public List<string> PullPendingTokens(int count)
        {
            var outList = new List<string>();
            while (count > 0 && _waves.Count > 0)
            {
                var wave = _waves[0];
                while (count > 0 && wave.Count > 0)
                {
                    outList.Add(wave[0]);
                    wave.RemoveAt(0);
                    count--;
                }
                if (wave.Count == 0) _waves.RemoveAt(0);
            }
            return outList;
        }

        public bool HasPendingTokens() => _waves.Any(w => w.Count > 0);

        public int NextPendingWaveSize()
        {
            foreach (var w in _waves)
                if (w.Count > 0) return w.Count;
            return 0;
        }

        public int CountTotalFragments() => _waves.Sum(w => w.Count);
        public int TotalTokenCount() => CountTotalFragments();
        public int InitialWaveSize() => NextPendingWaveSize();
        public List<int> WaveSizes() => _waves.Select(w => w.Count).ToList();
        public int WaveCount() => _waves.Count;
        public int DistinctImageCount() => _chipTotals.Count;

        public List<List<string>> SnapshotWaves()
        {
            return _waves.Select(w => new List<string>(w)).ToList();
        }

        public void RestoreWaves(IEnumerable<IEnumerable<string>> waves)
        {
            _waves = waves == null
                ? new List<List<string>>()
                : waves.Select(w => w?.ToList() ?? new List<string>()).ToList();
        }

        public bool TryTakeCompatibleToken(
            int imageId,
            IReadOnlyList<int> parentPath,
            ISet<int> excludedQuadrants,
            out string token)
        {
            token = null;
            for (int waveIndex = 0; waveIndex < _waves.Count; waveIndex++)
            {
                List<string> wave = _waves[waveIndex];
                for (int tokenIndex = 0; tokenIndex < wave.Count; tokenIndex++)
                {
                    BubbleFragment fragment = BubbleFragment.FromToken(wave[tokenIndex]);
                    if (fragment == null || fragment.ImageId != imageId ||
                        !fragment.ParentPath().SequenceEqual(parentPath))
                        continue;
                    if (fragment.LastQuadrants().Any(q => excludedQuadrants.Contains(q)))
                        continue;
                    token = wave[tokenIndex];
                    wave.RemoveAt(tokenIndex);
                    if (wave.Count == 0) _waves.RemoveAt(waveIndex);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Distinct pending "groups" (image|parentPath) — used by block size boost.</summary>
        public int PendingGroupCount()
        {
            var groups = new HashSet<string>();
            foreach (var wave in _waves)
            {
                foreach (var tok in wave)
                {
                    foreach (var part in tok.Split('+'))
                    {
                        var segs = part.Trim().Split('.');
                        if (segs.Length < 3) continue;
                        var pp = string.Join(",", segs.Skip(1).Take(segs.Length - 2));
                        groups.Add(segs[0] + "|" + pp);
                    }
                }
            }
            return groups.Count;
        }
    }
}
