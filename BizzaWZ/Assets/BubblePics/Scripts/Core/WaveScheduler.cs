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
