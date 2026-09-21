using System.Collections.Generic;
using System.Linq;
using BubblePics.GameModes;

namespace BubblePics
{
    /// <summary>
    /// Data model of the picture pieces held by one bubble.
    /// A path is a list of quadrant indices (0..3); depth-1 levels use single-element paths.
    /// Mirrors Godot BubbleFragment (scripts/module/bubble/model/bubble_fragment.gd).
    /// </summary>
    public class BubbleFragment
    {
        public int ImageId;                       // 0-based image index
        public List<List<int>> HeldPaths = new(); // each entry is a quadrant path

        // 1.0.8/1.0.9 special-fragment state. These fields intentionally live
        // on the data model (rather than only on BubbleView) because rainbow
        // replacement and sticker pairing must survive a merge and a resume.
        public bool IsRainbow;
        public bool TangramMold;
        public bool PendingStickerReturn;
        public int StickerShapeId = -1;
        public List<int> StickerDonorPath = new();
        public int StickerHalf; // 0=plain, 1=outline donor, 2=detached fill

        public static BubbleFragment FromToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            var f = new BubbleFragment { ImageId = -1 };
            foreach (var rawPart in token.Split('+'))
            {
                var part = rawPart.Trim();
                if (part.Length == 0) continue;
                var segs = part.Split('.');
                if (!int.TryParse(segs[0], out int img)) return null;
                img -= 1;
                if (f.ImageId == -1) f.ImageId = img;
                else if (img != f.ImageId) continue; // cross-image chunk: ignore odd piece
                if (segs.Length == 2 &&
                    string.Equals(
                        segs[1], "M", System.StringComparison.OrdinalIgnoreCase))
                {
                    f.TangramMold = true;
                    continue;
                }
                var path = new List<int>();
                for (int i = 1; i < segs.Length; i++)
                {
                    if (!int.TryParse(segs[i], out int quadrant) ||
                        quadrant < 1 || quadrant > 4)
                        return null;
                    path.Add(quadrant - 1);
                }
                f.HeldPaths.Add(path);
            }
            if (f.ImageId < 0 ||
                (f.HeldPaths.Count == 0 && !f.TangramMold))
                return null;
            return f;
        }

        public List<int> ParentPath()
        {
            if (HeldPaths.Count == 0 || HeldPaths[0].Count == 0) return new List<int>();
            return HeldPaths[0].Take(HeldPaths[0].Count - 1).ToList();
        }

        public List<int> LastQuadrants()
        {
            var outp = new List<int>();
            foreach (var p in HeldPaths)
                if (p.Count > 0) outp.Add(p[p.Count - 1]);
            return outp;
        }

        public int Depth() => HeldPaths.Count == 0 ? 0 : HeldPaths[0].Count;

        public int PieceCount() => HeldPaths.Count;

        /// <summary>Full image = empty path set collapsed to root.</summary>
        public bool IsFull() => HeldPaths.Count == 1 && HeldPaths[0].Count == 0;

        public static bool SamePath(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public static bool CanMerge(BubbleFragment a, BubbleFragment b)
        {
            if (a == null || b == null) return false;
            if (a.IsRainbow || b.IsRainbow) return true;
            if (a.ImageId != b.ImageId) return false;
            if (TangramMatchContent.IsTangramImage(a.ImageId))
            {
                if (a.TangramMold == b.TangramMold) return false;
                BubbleFragment mold = a.TangramMold ? a : b;
                BubbleFragment incoming = a.TangramMold ? b : a;
                if (incoming.HeldPaths.Count == 0 || incoming.Depth() == 0)
                    return false;
                List<int> occupied = mold.LastQuadrants();
                List<int> additions = incoming.LastQuadrants();
                return occupied.Count + additions.Count <= 4 &&
                       !occupied.Intersect(additions).Any();
            }
            if (a.IsFull() || b.IsFull()) return false;
            if (!SamePath(a.ParentPath(), b.ParentPath())) return false;
            var qa = a.LastQuadrants();
            var qb = b.LastQuadrants();

            if (IsStickerPair(a, b))
                return qa.Union(qb).Distinct().Count() <= 4;
            if (a.PendingStickerReturn || b.PendingStickerReturn)
                return false;
            if (qa.Intersect(qb).Any()) return false;       // duplicated quadrant
            return qa.Count + qb.Count <= 4;
        }

        public static bool IsStickerPair(BubbleFragment a, BubbleFragment b)
        {
            if (a == null || b == null || a.ImageId != b.ImageId)
                return false;
            return SamePath(a.ParentPath(), b.ParentPath()) &&
                   a.StickerShapeId >= 0 &&
                   a.StickerShapeId == b.StickerShapeId &&
                   a.StickerHalf > 0 && b.StickerHalf > 0 &&
                   a.StickerHalf != b.StickerHalf;
        }

        /// <summary>Merged result, collapsing completed parent groups.</summary>
        public static BubbleFragment MergeResult(BubbleFragment a, BubbleFragment b)
        {
            var f = new BubbleFragment
            {
                ImageId = a.ImageId,
                TangramMold = a.TangramMold || b.TangramMold,
            };
            f.HeldPaths = a.HeldPaths.Select(p => new List<int>(p))
                .Concat(b.HeldPaths.Select(p => new List<int>(p))).ToList();
            f.Collapse();
            return f;
        }

        /// <summary>If all 4 quadrants of the parent are held, fold to the parent path (recursively).</summary>
        public void Collapse()
        {
            while (HeldPaths.Count == 4 && HeldPaths[0].Count > 0)
            {
                var parent = ParentPath();
                bool sameParent = HeldPaths.All(p => SamePath(p.Take(p.Count - 1).ToList(), parent));
                var lastQs = LastQuadrants();
                if (sameParent && lastQs.Distinct().Count() == 4)
                    HeldPaths = new List<List<int>> { parent };
                else break;
            }
        }

        public string ToTokenString()
        {
            var parts = HeldPaths.Select(p =>
            {
                var s = (ImageId + 1).ToString();
                foreach (var seg in p) s += "." + (seg + 1);
                return s;
            }).ToList();
            if (TangramMold)
                parts.Insert(0, (ImageId + 1) + ".M");
            return string.Join("+", parts);
        }

        public BubbleFragment Clone()
        {
            return new BubbleFragment
            {
                ImageId = ImageId,
                HeldPaths = HeldPaths.Select(p => new List<int>(p)).ToList(),
                IsRainbow = IsRainbow,
                TangramMold = TangramMold,
                PendingStickerReturn = PendingStickerReturn,
                StickerShapeId = StickerShapeId,
                StickerDonorPath = new List<int>(StickerDonorPath),
                StickerHalf = StickerHalf,
            };
        }
    }
}
