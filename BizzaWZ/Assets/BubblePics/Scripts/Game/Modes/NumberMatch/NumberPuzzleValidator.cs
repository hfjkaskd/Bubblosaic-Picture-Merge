using System;
using System.Collections.Generic;

namespace BubblePics
{
    public static class NumberPuzzleValidator
    {
        public const int ResultMin = 1;
        public const int ResultMax = 30;
        public const int MinGroupSpacing = 2;
        public const int GroupSize = 4;

        public static bool Validate(NumberPuzzle puzzle, out List<string> errors)
        {
            errors = new List<string>();
            if (puzzle == null)
            {
                errors.Add("puzzle is null");
                return false;
            }

            if (string.IsNullOrWhiteSpace(puzzle.id))
                errors.Add("puzzle id is empty");
            if (!string.Equals(puzzle.tier, NumberPuzzle.SimpleTier, StringComparison.Ordinal) &&
                !string.Equals(puzzle.tier, NumberPuzzle.HardTier, StringComparison.Ordinal))
                errors.Add($"puzzle '{puzzle.id}' has unknown tier '{puzzle.tier}'");

            NumberPuzzleGroup[] groups = puzzle.groups;
            if (groups == null || groups.Length == 0)
            {
                errors.Add($"puzzle '{puzzle.id}' has no groups");
                return false;
            }

            var results = new List<int>(groups.Length);
            var allFormulas = new HashSet<string>(StringComparer.Ordinal);
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                NumberPuzzleGroup group = groups[groupIndex];
                if (group == null)
                {
                    errors.Add($"group {groupIndex} is null");
                    continue;
                }

                int expected = group.result;
                string[] formulas = group.formulas;
                if (expected < ResultMin || expected > ResultMax)
                    errors.Add($"group {groupIndex} result {expected} out of [{ResultMin},{ResultMax}]");
                if (formulas == null || formulas.Length != GroupSize)
                    errors.Add($"group {groupIndex} has {formulas?.Length ?? 0} formulas (expect {GroupSize})");

                var seen = new HashSet<string>(StringComparer.Ordinal);
                if (formulas != null)
                {
                    for (int slot = 0; slot < formulas.Length; slot++)
                    {
                        string formula = formulas[slot] ?? string.Empty;
                        if (!seen.Add(formula))
                            errors.Add($"group {groupIndex} duplicate formula '{formula}'");
                        if (!allFormulas.Add(formula))
                            errors.Add($"formula '{formula}' is repeated across groups");
                        if (!NumberFormula.TryValidate(formula, out int actual, out string error))
                            errors.Add($"group {groupIndex} formula '{formula}': {error}");
                        else if (actual != expected)
                            errors.Add($"group {groupIndex} formula '{formula}' = {actual} != group result {expected}");
                    }
                }

                results.Add(expected);
            }

            for (int i = 0; i < results.Count; i++)
            {
                for (int j = i + 1; j < results.Count; j++)
                {
                    int difference = Math.Abs(results[i] - results[j]);
                    if (difference == 0)
                        errors.Add($"groups {i},{j} share result {results[i]}");
                    else if (difference < MinGroupSpacing)
                        errors.Add($"groups {i},{j} results {results[i]}/{results[j]} spacing {difference} < {MinGroupSpacing}");
                }
            }

            ValidateWaves(puzzle, allFormulas, errors);
            return errors.Count == 0;
        }

        static void ValidateWaves(
            NumberPuzzle puzzle,
            HashSet<string> expected,
            List<string> errors)
        {
            if (puzzle.waves == null || puzzle.waves.Length == 0)
            {
                errors.Add($"puzzle '{puzzle.id}' has no waves");
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int waveIndex = 0; waveIndex < puzzle.waves.Length; waveIndex++)
            {
                string[] wave = puzzle.waves[waveIndex];
                if (wave == null || wave.Length == 0)
                {
                    errors.Add($"wave {waveIndex} is empty");
                    continue;
                }

                for (int formulaIndex = 0; formulaIndex < wave.Length; formulaIndex++)
                {
                    string formula = wave[formulaIndex] ?? string.Empty;
                    if (!expected.Contains(formula))
                        errors.Add($"wave {waveIndex} contains unknown formula '{formula}'");
                    if (!seen.Add(formula))
                        errors.Add($"waves repeat formula '{formula}'");
                }
            }

            foreach (string formula in expected)
            {
                if (!seen.Contains(formula))
                    errors.Add($"waves omit formula '{formula}'");
            }
        }
    }
}
