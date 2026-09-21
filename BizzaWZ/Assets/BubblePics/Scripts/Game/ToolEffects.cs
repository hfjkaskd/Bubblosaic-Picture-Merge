using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BubblePics
{
    /// <summary>Port of bubble_tool_effects.gd — hint / drop / magnet.</summary>
    public class ToolEffects : MonoBehaviour
    {
        const int MAGNET_GUARD = 64;

        public BubblePage Page;
        readonly List<BubbleView> _hintPair = new List<BubbleView>();

        // ------------------------------------------------------------ hint
        public bool CanApplyHint() => PickHintPair().Count == 2;

        public void ApplyHint()
        {
            ClearHintHighlight();
            var pair = PickHintPair();
            if (pair.Count < 2) return;
            _hintPair.AddRange(pair);
            foreach (var b in pair) b.FlashHint();
        }

        public void ClearHintHighlight()
        {
            foreach (var b in _hintPair)
                if (b != null) b.ClearHintHighlight();
            _hintPair.Clear();
        }

        List<BubbleView> PickHintPair()
        {
            var alive = Page.Field.AllBubbles()
                .Where(b => b.State == BubbleState.Alive && b.Fragment != null)
                .ToList();
            var best = PickHintPairFrom(alive.Where(b => !b.IsLocked));
            if (best.Count == 0)
                best = PickHintPairFrom(alive);
            return best;
        }

        List<BubbleView> PickHintPairFrom(IEnumerable<BubbleView> candidates)
        {
            var groups = new Dictionary<string, List<BubbleView>>();
            foreach (var b in candidates)
            {
                string key = GroupKey(b);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<BubbleView>();
                list.Add(b);
            }

            var best = BestHintGroupIn(groups.Values, true);
            if (best.Count == 0)
                best = BestHintGroupIn(groups.Values, false);
            return best;
        }

        static List<BubbleView> BestHintGroupIn(
            IEnumerable<List<BubbleView>> groups,
            bool requireCollapsible)
        {
            List<BubbleView> best = null;
            var bestRank = default(TargetingRules.GroupRank);
            bool hasBest = false;
            foreach (var g in groups)
            {
                if (g.Count < 2 || !HasMergeablePair(g)) continue;
                bool collapses = GroupCanCollapse(g);
                if (requireCollapsible && !collapses) continue;

                var anchor = GroupAnchor(g);
                var rank = new TargetingRules.GroupRank(
                    TargetingRules.GroupTier(collapses, anchor.Fragment.ParentPath().Count),
                    anchor.Fragment.LastQuadrants().Count,
                    g.Count,
                    GroupHasLocked(g));
                if (!TargetingRules.IsHintBetter(rank, bestRank, hasBest)) continue;
                bestRank = rank;
                best = g;
                hasBest = true;
            }

            if (best == null) return new List<BubbleView>();
            var bestAnchor = GroupAnchor(best);
            var src = BestMergeableSource(best, bestAnchor);
            return src == null
                ? new List<BubbleView>()
                : new List<BubbleView> { bestAnchor, src };
        }

        static BubbleView GroupAnchor(IReadOnlyList<BubbleView> members)
        {
            var anchor = members[0];
            for (int i = 1; i < members.Count; i++)
            {
                var member = members[i];
                if (member.Fragment.LastQuadrants().Count >
                    anchor.Fragment.LastQuadrants().Count)
                    anchor = member;
            }
            return anchor;
        }

        static BubbleView BestMergeableSource(
            IEnumerable<BubbleView> members,
            BubbleView anchor)
        {
            BubbleView src = null;
            foreach (var member in members)
            {
                // Keep the same direction as the original implementation:
                // the candidate checks whether it can merge into the anchor.
                if (member == anchor || !member.CanMergeWith(anchor)) continue;
                if (src == null ||
                    member.Fragment.LastQuadrants().Count >
                    src.Fragment.LastQuadrants().Count)
                    src = member;
            }
            return src;
        }

        // ------------------------------------------------------------ drop
        public bool CanApplyDrop() => Page.HasPendingTokens() && Page.Scheduler.NextPendingWaveSize() > 0;

        public IEnumerator ApplyDrop()
        {
            int n = Page.Scheduler.NextPendingWaveSize();
            yield return Page.DropPendingBubbles(n);
        }

        // ------------------------------------------------------------ magnet
        public bool CanApplyMagnet() => FindMagnetGroup().Count >= 2;

        public IEnumerator ApplyMagnet()
        {
            Page.SetToolBusy(true);
            float prevMult = Page.FusionDurationMult;
            Page.FusionDurationMult = Mathf.Clamp(0.35f / 0.45f, 0.3f, 1.5f);
            int myRound = Page.RoundSeq;
            var selected = FindMagnetGroup();
            string selectedKey = selected.Count > 0 ? GroupKey(selected[0]) : "";
            foreach (var member in selected)
                member.IsLocked = false;
            int guard = 0;
            while (guard < MAGNET_GUARD && Page.RoundSeq == myRound)
            {
                guard++;
                var pair = FindMergeablePair(selectedKey);
                if (pair.Count < 2) break;
                // The smaller fragment is consumed into the larger target.
                // Reversing these arguments destroys the more complete piece.
                yield return Page.AwaitMerge(pair[0], pair[1], true);
                yield return null;
            }
            if (Page.RoundSeq == myRound)
            {
                Page.FusionDurationMult = prevMult;
                Page.SetToolBusy(false);
            }
        }

        List<BubbleView> FindMergeablePair(string key)
        {
            if (string.IsNullOrEmpty(key)) return new List<BubbleView>();
            var members = Page.Field.AllBubbles()
                .Where(b => b.State == BubbleState.Alive && b.Fragment != null &&
                            GroupKey(b) == key)
                .ToList();
            for (int i = 0; i < members.Count; i++)
            {
                for (int j = i + 1; j < members.Count; j++)
                {
                    var a = members[i];
                    var b = members[j];
                    if (!a.CanMergeWith(b)) continue;
                    int aSize = a.Fragment.LastQuadrants().Count;
                    int bSize = b.Fragment.LastQuadrants().Count;
                    return bSize >= aSize
                        ? new List<BubbleView> { a, b }
                        : new List<BubbleView> { b, a };
                }
            }
            return new List<BubbleView>();
        }

        static string GroupKey(BubbleView bubble)
        {
            return bubble == null || bubble.Fragment == null
                ? ""
                : bubble.Fragment.ImageId + "|" +
                  string.Join(",", bubble.Fragment.ParentPath());
        }

        List<BubbleView> FindMagnetGroup()
        {
            var alive = Page.Field.AllBubbles()
                .Where(b => b.State == BubbleState.Alive && b.Fragment != null)
                .ToList();
            var groups = new Dictionary<string, List<BubbleView>>();
            foreach (var b in alive)
            {
                string key = GroupKey(b);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<BubbleView>();
                list.Add(b);
            }
            List<BubbleView> best = new List<BubbleView>();
            var bestRank = default(TargetingRules.GroupRank);
            bool hasBest = false;
            foreach (var g in groups.Values)
            {
                if (g.Count < 2 || !HasMergeablePair(g)) continue;
                bool collapses = GroupCanCollapse(g);
                var rank = new TargetingRules.GroupRank(
                    TargetingRules.GroupTier(
                        collapses,
                        g[0].Fragment.ParentPath().Count),
                    GroupAnchor(g).Fragment.LastQuadrants().Count,
                    g.Count,
                    GroupHasLocked(g));
                if (!TargetingRules.IsMagnetBetter(rank, bestRank, hasBest)) continue;
                bestRank = rank;
                best = g;
                hasBest = true;
            }
            return best;
        }

        static bool GroupCanCollapse(IEnumerable<BubbleView> members)
        {
            return TargetingRules.CanCollapse(
                members.Select(member =>
                    (IEnumerable<int>)member.Fragment.LastQuadrants()));
        }

        static bool GroupHasLocked(IEnumerable<BubbleView> members)
        {
            return members.Any(member => member.IsLocked);
        }

        static bool HasMergeablePair(IReadOnlyList<BubbleView> members)
        {
            for (int i = 0; i < members.Count; i++)
            {
                for (int j = i + 1; j < members.Count; j++)
                {
                    if (members[i].CanMergeWith(members[j]))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Pure candidate-ranking rules shared by hint and magnet selection.
        /// Public so editor-side regression checks can exercise the exact production
        /// ordering without constructing a running BubblePage.
        /// </summary>
        public static class TargetingRules
        {
            public readonly struct GroupRank
            {
                public readonly int Tier;
                public readonly int AnchorQuadrants;
                public readonly int MemberCount;
                public readonly bool HasLocked;

                public GroupRank(
                    int tier,
                    int anchorQuadrants,
                    int memberCount,
                    bool hasLocked)
                {
                    Tier = tier;
                    AnchorQuadrants = anchorQuadrants;
                    MemberCount = memberCount;
                    HasLocked = hasLocked;
                }
            }

            public static bool CanCollapse(IEnumerable<IEnumerable<int>> memberQuadrants)
            {
                var seen = new HashSet<int>();
                int total = 0;
                foreach (var quadrants in memberQuadrants)
                {
                    foreach (int quadrant in quadrants)
                    {
                        if (!seen.Add(quadrant)) return false;
                        total++;
                    }
                }
                return total == 4;
            }

            public static int GroupTier(bool canCollapse, int parentDepth)
            {
                if (!canCollapse) return 1;
                return parentDepth == 0 ? 3 : 2;
            }

            public static bool IsHintBetter(
                GroupRank candidate,
                GroupRank current,
                bool hasCurrent)
            {
                if (!hasCurrent) return true;
                if (candidate.AnchorQuadrants != current.AnchorQuadrants)
                    return candidate.AnchorQuadrants > current.AnchorQuadrants;
                return candidate.Tier > current.Tier;
            }

            public static bool IsMagnetBetter(
                GroupRank candidate,
                GroupRank current,
                bool hasCurrent)
            {
                if (!hasCurrent) return true;
                if (candidate.Tier != current.Tier)
                    return candidate.Tier > current.Tier;
                if (candidate.HasLocked != current.HasLocked)
                    return !candidate.HasLocked;
                return candidate.MemberCount > current.MemberCount;
            }
        }
    }
}
