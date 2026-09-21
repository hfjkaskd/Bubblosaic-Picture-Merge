using System;
using System.Collections.Generic;

namespace BubblePics
{
    [Serializable]
    public sealed class NumberPuzzleGroup
    {
        public int result;
        public string[] formulas;
    }

    [Serializable]
    public sealed class NumberPuzzle
    {
        public const string SimpleTier = "simple";
        public const string HardTier = "hard";

        public string id;
        public string tier = SimpleTier;
        public NumberPuzzleGroup[] groups;
        public string[][] waves;

        public int GroupCount => groups == null ? 0 : groups.Length;

        public IEnumerable<string> AllFormulas()
        {
            if (groups == null) yield break;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                string[] formulas = groups[groupIndex]?.formulas;
                if (formulas == null) continue;
                for (int slot = 0; slot < formulas.Length; slot++)
                    yield return formulas[slot];
            }
        }
    }
}
