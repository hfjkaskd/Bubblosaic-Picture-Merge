using System;
using System.Collections.Generic;
using System.Linq;

namespace BubblePics
{
    /// <summary>
    /// Validates and repairs a resumable round before it is allowed to mutate
    /// the live board. This mirrors BubbleProgressStore.validate_snapshot and
    /// BubbleProgressRepair.repair_missing_leaves from the reference project.
    /// </summary>
    public static class RoundProgressValidator
    {
        sealed class ParsedPath
        {
            public int ImageId;
            public int[] Path;
        }

        public static bool ValidateAndRepair(
            RoundProgress snapshot,
            LevelData level,
            int imageCount,
            out bool repaired,
            out string error)
        {
            repaired = false;
            error = "";
            if (snapshot == null)
            {
                error = "snapshot is null";
                return false;
            }
            if (level == null || string.IsNullOrWhiteSpace(level.layout))
            {
                error = "level layout is unavailable";
                return false;
            }
            if (imageCount <= 0)
            {
                error = "image count is invalid";
                return false;
            }
            if (snapshot.bubbles == null || snapshot.pendingWaves == null)
            {
                error = "snapshot arrays are missing";
                return false;
            }

            if (!TryBuildLeafUniverse(
                    level.layout, imageCount, out var universe, out error))
                return false;

            var collected = new HashSet<int>();
            foreach (int imageId in snapshot.collectedImages ?? Array.Empty<int>())
            {
                if (imageId < 0 || imageId >= imageCount)
                {
                    error = $"collected image {imageId} is out of range";
                    return false;
                }
                if (!collected.Add(imageId))
                {
                    error = $"collected image {imageId} is duplicated";
                    return false;
                }
            }

            var boardPaths = new Dictionary<int, List<int[]>>();
            var moldOwners = new Dictionary<int, int>();
            foreach (BubbleProgress bubble in snapshot.bubbles)
            {
                if (bubble == null ||
                    !TryParseCombinedToken(
                        bubble.token, imageCount, out int imageId,
                        out List<int[]> paths,
                        out bool tangramMold,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "snapshot contains a null bubble";
                    return false;
                }
                if (collected.Contains(imageId))
                {
                    error =
                        $"orphan: collected image {imageId} still has a board piece";
                    return false;
                }
                if (tangramMold && !TryRegisterMold(
                        moldOwners, imageId, out error))
                    return false;
                if (!boardPaths.TryGetValue(imageId, out var list))
                    boardPaths[imageId] = list = new List<int[]>();

                foreach (int[] path in paths)
                {
                    if (path.Length == 0)
                    {
                        error =
                            $"orphan: whole image {imageId} remains on the board";
                        return false;
                    }
                    if (!OverlapsAnyLeaf(path, universe[imageId]))
                    {
                        error =
                            $"board path {Tokenize(imageId, path)} is outside the level";
                        return false;
                    }
                    foreach (int[] existing in list)
                    {
                        if (!PathsOverlap(existing, path)) continue;
                        error =
                            $"dup/overlap: image {imageId} pieces " +
                            $"{PathKey(existing)} / {PathKey(path)}";
                        return false;
                    }
                    list.Add(path);
                }
            }

            var accounted = new Dictionary<int, HashSet<string>>();
            foreach (var pair in boardPaths)
            {
                HashSet<string> imageAccounted = GetAccounted(accounted, pair.Key);
                foreach (int[] held in pair.Value)
                {
                    foreach (int[] leaf in universe[pair.Key])
                    {
                        if (IsPrefix(held, leaf))
                            imageAccounted.Add(PathKey(leaf));
                    }
                }
            }

            foreach (RoundWaveProgress wave in snapshot.pendingWaves)
            {
                if (wave == null)
                {
                    error = "snapshot contains a null wave";
                    return false;
                }
                foreach (string token in wave.tokens ?? Array.Empty<string>())
                {
                    if (!TryParseCombinedToken(
                            token, imageCount, out int imageId,
                            out List<int[]> paths,
                            out bool tangramMold,
                            out error))
                        return false;
                    if (collected.Contains(imageId))
                    {
                        error =
                            $"orphan: collected image {imageId} remains in pending waves";
                        return false;
                    }
                    if (tangramMold && !TryRegisterMold(
                            moldOwners, imageId, out error))
                        return false;
                    foreach (int[] path in paths)
                    {
                        if (!ContainsPath(universe[imageId], path))
                        {
                            error =
                                $"pending path {Tokenize(imageId, path)} is outside the level";
                            return false;
                        }
                        string pathKey = PathKey(path);
                        if (!GetAccounted(accounted, imageId).Add(pathKey))
                        {
                            error =
                                $"dup/overlap: pending path " +
                                $"{Tokenize(imageId, path)} is already owned";
                            return false;
                        }
                    }
                }
            }

            var missingTokens = new List<string>();
            for (int imageId = 0; imageId < imageCount; imageId++)
            {
                if (collected.Contains(imageId)) continue;
                HashSet<string> imageAccounted =
                    GetAccounted(accounted, imageId);
                foreach (int[] leaf in universe[imageId])
                {
                    if (!imageAccounted.Contains(PathKey(leaf)))
                        missingTokens.Add(Tokenize(imageId, leaf));
                }
            }

            if (missingTokens.Count > 0)
            {
                var waves = snapshot.pendingWaves.ToList();
                waves.Add(new RoundWaveProgress { tokens = missingTokens.ToArray() });
                snapshot.pendingWaves = waves.ToArray();
                repaired = true;
            }
            return true;
        }

        static bool TryBuildLeafUniverse(
            string layout,
            int imageCount,
            out Dictionary<int, List<int[]>> universe,
            out string error)
        {
            universe = new Dictionary<int, List<int[]>>();
            error = "";
            for (int imageId = 0; imageId < imageCount; imageId++)
                universe[imageId] = new List<int[]>();

            foreach (string wave in (layout ?? "").Split('|'))
            {
                foreach (string entry in wave.Split(','))
                {
                    foreach (string rawChip in entry.Split('+'))
                    {
                        string chip = rawChip.Trim();
                        if (chip.Length == 0) continue;
                        if (!LevelValidator.TryParseToken(
                                chip,
                                out int image,
                                out int[] path,
                                out bool tangramMold,
                                out string parseError))
                        {
                            error =
                                $"level leaf \"{chip}\" is invalid: {parseError}";
                            return false;
                        }
                        int imageId = image - 1;
                        if (imageId < 0 || imageId >= imageCount)
                        {
                            error =
                                $"level leaf \"{chip}\" exceeds image count {imageCount}";
                            return false;
                        }
                        if (tangramMold) continue;
                        if (!ContainsPath(universe[imageId], path))
                            universe[imageId].Add(path);
                    }
                }
            }

            for (int imageId = 0; imageId < imageCount; imageId++)
            {
                if (universe[imageId].Count > 0) continue;
                error = $"image {imageId} has no leaf paths";
                return false;
            }
            return true;
        }

        static bool TryParseCombinedToken(
            string token,
            int imageCount,
            out int imageId,
            out List<int[]> paths,
            out bool tangramMold,
            out string error)
        {
            imageId = -1;
            paths = new List<int[]>();
            tangramMold = false;
            error = "";
            foreach (string rawChip in (token ?? "").Split('+'))
            {
                string chip = rawChip.Trim();
                if (chip.Length == 0) continue;
                if (!LevelValidator.TryParseToken(
                        chip,
                        out int image,
                        out int[] path,
                        out bool chipIsMold,
                        out string parseError))
                {
                    error = $"token \"{token}\" is invalid: {parseError}";
                    return false;
                }
                int chipImage = image - 1;
                if (chipImage < 0 || chipImage >= imageCount)
                {
                    error =
                        $"token \"{token}\" exceeds image count {imageCount}";
                    return false;
                }
                if (imageId < 0)
                    imageId = chipImage;
                else if (imageId != chipImage)
                {
                    error = $"combined token \"{token}\" crosses images";
                    return false;
                }
                if (chipIsMold)
                {
                    if (tangramMold)
                    {
                        error = $"combined token \"{token}\" repeats tangram mold";
                        return false;
                    }
                    tangramMold = true;
                }
                else
                {
                    paths.Add(path);
                }
            }
            if (imageId >= 0 && (paths.Count > 0 || tangramMold)) return true;
            error = "token is empty";
            return false;
        }

        static bool TryRegisterMold(
            IDictionary<int, int> owners,
            int imageId,
            out string error)
        {
            int count = owners.TryGetValue(imageId, out int current)
                ? current + 1
                : 1;
            owners[imageId] = count;
            if (count <= 1)
            {
                error = "";
                return true;
            }
            error = $"duplicate tangram mold for image {imageId}";
            return false;
        }

        static bool PathsOverlap(IReadOnlyList<int> a, IReadOnlyList<int> b)
        {
            int common = Math.Min(a.Count, b.Count);
            for (int i = 0; i < common; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        static bool ContainsPath(IEnumerable<int[]> paths, IReadOnlyList<int> path)
        {
            return paths.Any(candidate => candidate.SequenceEqual(path));
        }

        static bool OverlapsAnyLeaf(
            IReadOnlyList<int> path,
            IEnumerable<int[]> leaves)
        {
            return leaves.Any(leaf => IsPrefix(path, leaf));
        }

        static bool IsPrefix(IReadOnlyList<int> prefix, IReadOnlyList<int> path)
        {
            if (prefix.Count > path.Count) return false;
            for (int i = 0; i < prefix.Count; i++)
                if (prefix[i] != path[i]) return false;
            return true;
        }

        static HashSet<string> GetAccounted(
            Dictionary<int, HashSet<string>> accounted,
            int imageId)
        {
            if (!accounted.TryGetValue(imageId, out var paths))
                accounted[imageId] = paths = new HashSet<string>();
            return paths;
        }

        static string PathKey(IEnumerable<int> path)
        {
            return string.Join(".", path);
        }

        static string Tokenize(int imageId, IEnumerable<int> path)
        {
            string suffix =
                string.Join(".", path.Select(q => (q + 1).ToString()));
            return suffix.Length == 0
                ? (imageId + 1).ToString()
                : $"{imageId + 1}.{suffix}";
        }
    }
}
