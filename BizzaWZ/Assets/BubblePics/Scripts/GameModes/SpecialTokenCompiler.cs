using System;
using System.Collections.Generic;
using System.Linq;

namespace BubblePics.GameModes
{
    public sealed class CompiledModeGroup
    {
        public int ImageId;
        public int SourceGroupId;
        public string GroupLabel = string.Empty;
        public readonly Dictionary<int, string> CodesBySlot =
            new Dictionary<int, string>();
        public TangramTargetData TangramTarget;
    }

    public sealed class CompiledModeLevel
    {
        public GameplayKind Kind;
        public string SourceLayout = string.Empty;
        public string NumericLayout = string.Empty;
        public int StepLimit;
        public readonly List<CompiledModeGroup> Groups =
            new List<CompiledModeGroup>();
        public readonly List<string> Errors = new List<string>();

        public bool IsValid =>
            Errors.Count == 0 &&
            Groups.Count > 0 &&
            !string.IsNullOrWhiteSpace(NumericLayout);
    }

    /// <summary>
    /// Ports BubbleLevelParser.rewrite_special_tokens and the strict catalog
    /// checks. Source prefixes are compiled before the numeric Bubble runtime,
    /// preserving the original slot-is-first-occurrence rule for s/w tokens.
    /// </summary>
    public static class SpecialTokenCompiler
    {
        public static CompiledModeLevel CompileCategory(
            string layout,
            int stepLimit,
            CategoryMatchCatalogData catalog)
        {
            return CompileNamedGroups(
                GameplayKind.CategoryMatch,
                's',
                layout,
                stepLimit,
                code => catalog != null && catalog.Icons.ContainsKey(code),
                id => catalog != null &&
                      catalog.Categories.TryGetValue(id, out var label)
                    ? label.Resolve()
                    : string.Empty);
        }

        public static CompiledModeLevel CompileWord(
            string layout,
            int stepLimit,
            WordMatchCatalogData catalog)
        {
            return CompileNamedGroups(
                GameplayKind.WordMatch,
                'w',
                layout,
                stepLimit,
                code => catalog != null && catalog.Words.ContainsKey(code),
                id => catalog != null &&
                      catalog.Categories.TryGetValue(id, out var label)
                    ? label.Resolve()
                    : string.Empty);
        }

        static CompiledModeLevel CompileNamedGroups(
            GameplayKind kind,
            char prefix,
            string layout,
            int stepLimit,
            Func<string, bool> codeExists,
            Func<int, string> labelFor)
        {
            var result = new CompiledModeLevel
            {
                Kind = kind,
                SourceLayout = layout ?? string.Empty,
                StepLimit = stepLimit,
            };
            if (stepLimit < 0)
                result.Errors.Add("step_limit < 0");
            if (string.IsNullOrWhiteSpace(layout))
            {
                result.Errors.Add("layout is empty");
                return result;
            }

            var groupsBySource = new Dictionary<int, CompiledModeGroup>();
            var seenCodes = new Dictionary<int, HashSet<string>>();
            string[] waves = layout.Split(new[] { '|' }, StringSplitOptions.None);
            var encodedWaves = new List<string>(waves.Length);
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                var encodedTokens = new List<string>();
                string[] entries = waves[waveIndex].Split(',');
                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    string entry = entries[entryIndex].Trim();
                    if (entry.Length == 0) continue;
                    string[] parts = entry.Split('+');
                    var encodedParts = new List<string>(parts.Length);
                    for (int partIndex = 0; partIndex < parts.Length; partIndex++)
                    {
                        string part = parts[partIndex].Trim();
                        if (!TryParsePrefixedCode(
                                part,
                                prefix,
                                out int groupId,
                                out int memberId))
                        {
                            result.Errors.Add(
                                $"invalid {prefix}G.K token '{part}'");
                            continue;
                        }
                        string code = groupId + "." + memberId;
                        if (!codeExists(code))
                        {
                            result.Errors.Add(
                                $"catalog entry is missing: {prefix}{code}");
                            continue;
                        }

                        if (!groupsBySource.TryGetValue(
                                groupId,
                                out CompiledModeGroup group))
                        {
                            group = new CompiledModeGroup
                            {
                                ImageId = groupsBySource.Count,
                                SourceGroupId = groupId,
                                GroupLabel = labelFor(groupId),
                            };
                            groupsBySource[groupId] = group;
                            seenCodes[groupId] = new HashSet<string>(
                                StringComparer.Ordinal);
                            result.Groups.Add(group);
                        }
                        if (!seenCodes[groupId].Add(code))
                        {
                            result.Errors.Add(
                                $"group {groupId} repeats {prefix}{code}");
                            continue;
                        }
                        int slot = group.CodesBySlot.Count;
                        if (slot >= 4)
                        {
                            result.Errors.Add(
                                $"group {groupId} has more than four members");
                            continue;
                        }
                        group.CodesBySlot[slot] = code;
                        encodedParts.Add(
                            (group.ImageId + 1) + "." + (slot + 1));
                    }
                    if (encodedParts.Count > 0)
                        encodedTokens.Add(string.Join("+", encodedParts));
                }
                // Empty waves are intentionally retained; they are pacing
                // separators in both recovered manifests.
                encodedWaves.Add(string.Join(",", encodedTokens));
            }

            foreach (CompiledModeGroup group in result.Groups)
            {
                if (group.CodesBySlot.Count != 4)
                {
                    result.Errors.Add(
                        $"group {group.SourceGroupId} has " +
                        $"{group.CodesBySlot.Count} members; expected 4");
                }
            }
            result.NumericLayout = string.Join("|", encodedWaves);
            return result;
        }

        public static CompiledModeLevel CompileTangram(
            string layout,
            int stepLimit,
            TangramCatalogData catalog)
        {
            var result = new CompiledModeLevel
            {
                Kind = GameplayKind.Tangram,
                SourceLayout = layout ?? string.Empty,
                StepLimit = stepLimit,
            };
            if (stepLimit < 0) result.Errors.Add("step_limit < 0");
            if (string.IsNullOrWhiteSpace(layout))
            {
                result.Errors.Add("layout is empty");
                return result;
            }

            var groups = new Dictionary<int, CompiledModeGroup>();
            var masks = new Dictionary<int, int>();
            string[] waves = layout.Split(new[] { '|' }, StringSplitOptions.None);
            var encodedWaves = new List<string>(waves.Length);
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                var encodedTokens = new List<string>();
                foreach (string rawEntry in waves[waveIndex].Split(','))
                {
                    string entry = rawEntry.Trim();
                    if (entry.Length == 0) continue;
                    var encodedParts = new List<string>();
                    foreach (string rawPart in entry.Split('+'))
                    {
                        string part = rawPart.Trim();
                        if (!TryParseTangramToken(
                                part,
                                out int code,
                                out int slot,
                                out bool mold))
                        {
                            result.Errors.Add(
                                $"invalid tangram token '{part}'");
                            continue;
                        }
                        if (catalog == null ||
                            !catalog.Targets.TryGetValue(
                                code,
                                out TangramTargetData target))
                        {
                            result.Errors.Add(
                                $"tangram catalog entry is missing: t{code}");
                            continue;
                        }
                        if (!groups.TryGetValue(
                                code,
                                out CompiledModeGroup group))
                        {
                            group = new CompiledModeGroup
                            {
                                ImageId = groups.Count,
                                SourceGroupId = code,
                                GroupLabel = target.SourceCellName,
                                TangramTarget = target,
                            };
                            groups[code] = group;
                            masks[code] = 0;
                            result.Groups.Add(group);
                        }

                        int bit = mold ? 1 : 1 << slot;
                        if ((masks[code] & bit) != 0)
                        {
                            result.Errors.Add(
                                $"tangram t{code} repeats " +
                                (mold ? "mold" : "piece " + slot));
                            continue;
                        }
                        masks[code] |= bit;
                        if (mold)
                        {
                            encodedParts.Add((group.ImageId + 1) + ".M");
                            continue;
                        }

                        int zeroBasedSlot = slot - 1;
                        group.CodesBySlot[zeroBasedSlot] = code + "." + slot;
                        encodedParts.Add(
                            (group.ImageId + 1) + "." + slot);
                    }
                    if (encodedParts.Count > 0)
                        encodedTokens.Add(string.Join("+", encodedParts));
                }
                encodedWaves.Add(string.Join(",", encodedTokens));
            }

            foreach (CompiledModeGroup group in result.Groups)
            {
                const int CompleteMask = 1 | (1 << 1) | (1 << 2) |
                                         (1 << 3) | (1 << 4);
                if (!masks.TryGetValue(group.SourceGroupId, out int mask) ||
                    mask != CompleteMask)
                {
                    result.Errors.Add(
                        $"tangram t{group.SourceGroupId} must contain one " +
                        "mold and pieces 1..4 exactly once");
                }
                ValidateTangramTarget(group.TangramTarget, result.Errors);
            }
            result.NumericLayout = string.Join("|", encodedWaves);
            return result;
        }

        public static void ValidateTangramTarget(
            TangramTargetData target,
            ICollection<string> errors)
        {
            if (target == null)
            {
                errors.Add("tangram target is null");
                return;
            }
            if (target.Pieces == null || target.Pieces.Length != 4)
            {
                errors.Add(
                    $"tangram t{target.Code} piece count is not 4");
                return;
            }
            var ids = new HashSet<int>();
            int preplaced = 0;
            foreach (TangramPieceData piece in target.Pieces)
            {
                if (piece == null || piece.PieceId < 0 || piece.PieceId > 3 ||
                    !ids.Add(piece.PieceId))
                {
                    errors.Add(
                        $"tangram t{target.Code} has an invalid/duplicate piece id");
                    continue;
                }
                if (piece.Preplaced) preplaced++;
                if (piece.Polygon == null || piece.Polygon.Length < 3 ||
                    piece.Polygon.Any(point =>
                        float.IsNaN(point.x) || float.IsInfinity(point.x) ||
                        float.IsNaN(point.y) || float.IsInfinity(point.y)))
                {
                    errors.Add(
                        $"tangram t{target.Code}.{piece.PieceId + 1} " +
                        "has an invalid polygon");
                }
            }
            if (preplaced != 1)
                errors.Add(
                    $"tangram t{target.Code} preplaced count is {preplaced}; expected 1");
        }

        static bool TryParsePrefixedCode(
            string token,
            char prefix,
            out int group,
            out int member)
        {
            group = 0;
            member = 0;
            if (string.IsNullOrWhiteSpace(token) || token.Length < 4 ||
                char.ToLowerInvariant(token[0]) !=
                char.ToLowerInvariant(prefix))
                return false;
            string[] parts = token.Substring(1).Split('.');
            return parts.Length == 2 &&
                   int.TryParse(parts[0], out group) && group >= 1 &&
                   int.TryParse(parts[1], out member) && member >= 1;
        }

        static bool TryParseTangramToken(
            string token,
            out int code,
            out int slot,
            out bool mold)
        {
            code = 0;
            slot = 0;
            mold = false;
            if (string.IsNullOrWhiteSpace(token) || token.Length < 4 ||
                char.ToLowerInvariant(token[0]) != 't')
                return false;
            string[] parts = token.Substring(1).Split('.');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out code) || code < 1)
                return false;
            mold = string.Equals(
                parts[1],
                "M",
                StringComparison.OrdinalIgnoreCase);
            return mold ||
                   (int.TryParse(parts[1], out slot) && slot >= 1 && slot <= 4);
        }
    }
}
