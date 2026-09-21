using System;
using System.Collections.Generic;
using System.Linq;

namespace BubblePics
{
    public sealed class LevelValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public readonly List<string> Errors = new();
        public int MaxImageIndex { get; internal set; }
        public int ImageCount { get; internal set; }
        public int TokenCount { get; internal set; }
        public int WaveCount { get; internal set; }

        internal void Add(string error)
        {
            Errors.Add(error);
        }

        public override string ToString()
        {
            return IsValid ? "valid" : string.Join("; ", Errors);
        }
    }

    /// <summary>
    /// Port of BubbleLevelValidator. Besides syntax and image bounds it proves
    /// that every image closes into exactly one whole image and that the wave
    /// schedule cannot deadlock before all later waves are released.
    /// </summary>
    public static class LevelValidator
    {
        sealed class Fragment
        {
            public int Image;
            public int[] Path;
        }

        sealed class Group
        {
            public int Image;
            public int[] Parent;
            public readonly HashSet<int> Quadrants = new();
        }

        public static LevelValidationResult Validate(
            string sequence,
            int imageCount = 0)
        {
            var result = new LevelValidationResult();
            var waves = SplitWaves(sequence);
            result.WaveCount = waves.Count;

            var fragments = new List<Fragment>();
            int maxImage = 0;
            int parsedTokenCount = 0;
            for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
            {
                foreach (string token in waves[waveIndex])
                {
                    if (!TryParseToken(
                            token,
                            out int image,
                            out int[] path,
                            out bool tangramMold,
                            out string error))
                    {
                        result.Add(
                            $"wave {waveIndex + 1} token \"{token}\" is invalid: {error}");
                        continue;
                    }

                    maxImage = Math.Max(maxImage, image);
                    parsedTokenCount++;
                    if (!tangramMold)
                        fragments.Add(new Fragment { Image = image - 1, Path = path });
                }
            }

            result.MaxImageIndex = maxImage;
            result.TokenCount = parsedTokenCount;
            if (parsedTokenCount == 0 || fragments.Count == 0)
            {
                result.Add("layout has no tokens");
                return result;
            }

            if (imageCount <= 0) imageCount = maxImage;
            result.ImageCount = imageCount;
            if (imageCount <= 0)
            {
                result.Add("image count cannot be determined");
                return result;
            }
            if (maxImage > imageCount)
            {
                result.Add(
                    $"token image {maxImage} exceeds image count {imageCount}");
            }

            foreach (Fragment fragment in fragments)
            {
                if (fragment.Image >= imageCount)
                {
                    result.Add(
                        $"token image {fragment.Image + 1} exceeds image count {imageCount}");
                }
            }
            if (!result.IsValid) return result;

            CheckClosure(fragments, imageCount, result);
            if (!result.IsValid) return result;

            SimulateWaves(waves, result);
            return result;
        }

        public static bool TryParseToken(
            string token,
            out int image,
            out int[] path,
            out string error)
        {
            return TryParseToken(
                token, out image, out path, out _, out error);
        }

        public static bool TryParseToken(
            string token,
            out int image,
            out int[] path,
            out bool tangramMold,
            out string error)
        {
            image = 0;
            path = Array.Empty<int>();
            tangramMold = false;
            error = "";

            string[] segments = (token ?? "").Trim().Split('.');
            if (segments.Length == 0 || segments[0].Length == 0)
            {
                error = "empty token";
                return false;
            }
            if (!int.TryParse(segments[0], out image))
            {
                error = "image index is not an integer";
                return false;
            }
            if (image < 1)
            {
                error = "image index must be >= 1";
                return false;
            }

            if (segments.Length == 2 &&
                string.Equals(
                    segments[1], "M", StringComparison.OrdinalIgnoreCase))
            {
                tangramMold = true;
                return true;
            }

            path = new int[Math.Max(0, segments.Length - 1)];
            for (int i = 1; i < segments.Length; i++)
            {
                if (!int.TryParse(segments[i], out int quadrant))
                {
                    error = $"path segment {i + 1} is not an integer";
                    return false;
                }
                if (quadrant < 1 || quadrant > 4)
                {
                    error = $"quadrant must be 1..4 (got {quadrant})";
                    return false;
                }
                path[i - 1] = quadrant - 1;
            }
            return true;
        }

        static List<List<string>> SplitWaves(string sequence)
        {
            var waves = new List<List<string>>();
            foreach (string waveText in (sequence ?? "").Split('|'))
            {
                var wave = new List<string>();
                foreach (string entry in waveText.Split(','))
                {
                    foreach (string chip in entry.Split('+'))
                    {
                        string clean = chip.Trim();
                        if (clean.Length > 0) wave.Add(clean);
                    }
                }
                waves.Add(wave);
            }
            return waves;
        }

        static void CheckClosure(
            List<Fragment> fragments,
            int imageCount,
            LevelValidationResult result)
        {
            var byImage = new Dictionary<int, List<int[]>>();
            foreach (Fragment fragment in fragments)
            {
                if (!byImage.TryGetValue(fragment.Image, out var paths))
                {
                    paths = new List<int[]>();
                    byImage[fragment.Image] = paths;
                }
                paths.Add(fragment.Path);
            }

            for (int image = 0; image < imageCount; image++)
            {
                if (!byImage.TryGetValue(image, out var paths))
                {
                    result.Add($"image {image + 1} has no fragments");
                    continue;
                }
                CheckImageClosure(image, paths, result);
            }
        }

        static void CheckImageClosure(
            int image,
            List<int[]> paths,
            LevelValidationResult result)
        {
            var present = new Dictionary<string, int>();
            int maxDepth = 0;
            foreach (int[] path in paths)
            {
                string key = PathKey(path);
                present[key] = present.TryGetValue(key, out int count) ? count + 1 : 1;
                maxDepth = Math.Max(maxDepth, path.Length);
            }

            for (int depth = maxDepth; depth > 0; depth--)
            {
                var groups = new Dictionary<string, Dictionary<int, int>>();
                foreach (var pair in present.ToArray())
                {
                    if (pair.Value <= 0) continue;
                    int[] path = KeyPath(pair.Key);
                    if (path.Length != depth) continue;
                    string parentKey = PathKey(path.Take(path.Length - 1));
                    if (!groups.TryGetValue(parentKey, out var quadrants))
                    {
                        quadrants = new Dictionary<int, int>();
                        groups[parentKey] = quadrants;
                    }
                    int quadrant = path[path.Length - 1];
                    quadrants[quadrant] =
                        quadrants.TryGetValue(quadrant, out int count)
                            ? count + pair.Value
                            : pair.Value;
                }

                foreach (var group in groups)
                {
                    int merges = int.MaxValue;
                    for (int quadrant = 0; quadrant < 4; quadrant++)
                    {
                        merges = Math.Min(
                            merges,
                            group.Value.TryGetValue(quadrant, out int quadrantCount)
                                ? quadrantCount
                                : 0);
                    }
                    if (merges <= 0 || merges == int.MaxValue) continue;

                    int[] parent = KeyPath(group.Key);
                    for (int quadrant = 0; quadrant < 4; quadrant++)
                    {
                        string child = PathKey(parent.Concat(new[] { quadrant }));
                        present[child] -= merges;
                    }
                    present[group.Key] =
                        present.TryGetValue(group.Key, out int count)
                            ? count + merges
                            : merges;
                }
            }

            int whole = present.TryGetValue("", out int wholeCount) ? wholeCount : 0;
            var orphans = present
                .Where(p => p.Key.Length > 0 && p.Value > 0)
                .Take(4)
                .Select(p => Token(image, KeyPath(p.Key)) +
                             (p.Value > 1 ? $" x{p.Value}" : ""))
                .ToArray();
            int orphanCount = present
                .Where(p => p.Key.Length > 0 && p.Value > 0)
                .Sum(p => p.Value);

            if (whole != 1)
                result.Add($"image {image + 1} closes into {whole} whole images instead of 1");
            if (orphanCount > 0)
            {
                result.Add(
                    $"image {image + 1} has {orphanCount} unmergeable fragments: " +
                    string.Join(", ", orphans));
            }
        }

        static void SimulateWaves(
            List<List<string>> waves,
            LevelValidationResult result)
        {
            var board = new Dictionary<string, int>();
            int nextWave = 0;
            DropWave(board, waves, nextWave++);

            int guard = 0;
            while (guard++ < 100000)
            {
                bool mergedAny = false;
                Group group = FindCompletableGroup(board);
                while (group != null)
                {
                    mergedAny = true;
                    ApplyMerge(board, group);
                    if (nextWave < waves.Count)
                        DropWave(board, waves, nextWave++);
                    group = FindCompletableGroup(board);
                }

                if (nextWave >= waves.Count)
                {
                    if (!BoardEmpty(board))
                        result.Add("board still contains fragments after all waves");
                    return;
                }

                if (!mergedAny)
                {
                    bool remainingEmpty = true;
                    for (int i = nextWave; i < waves.Count; i++)
                    {
                        if (waves[i].Count == 0) continue;
                        remainingEmpty = false;
                        break;
                    }
                    if (remainingEmpty)
                    {
                        if (!BoardEmpty(board))
                            result.Add("board still contains fragments after all non-empty waves");
                        return;
                    }

                    result.Add(
                        $"deadlock after wave {nextWave}; " +
                        $"{waves.Count - nextWave} later waves can never be released");
                    return;
                }
            }

            result.Add("deadlock simulation exceeded the safety iteration limit");
        }

        static void DropWave(
            Dictionary<string, int> board,
            List<List<string>> waves,
            int index)
        {
            if (index < 0 || index >= waves.Count) return;
            foreach (string token in waves[index])
            {
                if (!TryParseToken(
                        token,
                        out int image,
                        out int[] path,
                        out bool tangramMold,
                        out _))
                    continue;
                if (tangramMold) continue;
                string key = $"{image - 1}|{PathKey(path)}";
                board[key] = board.TryGetValue(key, out int count) ? count + 1 : 1;
            }
        }

        static Group FindCompletableGroup(Dictionary<string, int> board)
        {
            var groups = new Dictionary<string, Group>();
            foreach (var pair in board)
            {
                if (pair.Value <= 0) continue;
                int separator = pair.Key.IndexOf('|');
                int image = int.Parse(pair.Key.Substring(0, separator));
                int[] path = KeyPath(pair.Key.Substring(separator + 1));
                if (path.Length == 0) continue;
                int[] parent = path.Take(path.Length - 1).ToArray();
                string groupKey = $"{image}|{PathKey(parent)}";
                if (!groups.TryGetValue(groupKey, out Group group))
                {
                    group = new Group { Image = image, Parent = parent };
                    groups[groupKey] = group;
                }
                group.Quadrants.Add(path[path.Length - 1]);
            }
            return groups.Values.FirstOrDefault(g => g.Quadrants.Count == 4);
        }

        static void ApplyMerge(Dictionary<string, int> board, Group group)
        {
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                string child =
                    $"{group.Image}|{PathKey(group.Parent.Concat(new[] { quadrant }))}";
                board[child]--;
            }
            if (group.Parent.Length == 0) return;
            string parent = $"{group.Image}|{PathKey(group.Parent)}";
            board[parent] = board.TryGetValue(parent, out int count) ? count + 1 : 1;
        }

        static bool BoardEmpty(Dictionary<string, int> board)
        {
            return board.All(pair => pair.Value <= 0);
        }

        static string PathKey(IEnumerable<int> path)
        {
            return string.Join(".", path);
        }

        static int[] KeyPath(string key)
        {
            if (string.IsNullOrEmpty(key)) return Array.Empty<int>();
            return key.Split('.').Select(int.Parse).ToArray();
        }

        static string Token(int zeroBasedImage, IEnumerable<int> path)
        {
            string suffix = string.Join(".", path.Select(q => (q + 1).ToString()));
            return suffix.Length == 0
                ? (zeroBasedImage + 1).ToString()
                : $"{zeroBasedImage + 1}.{suffix}";
        }
    }
}
