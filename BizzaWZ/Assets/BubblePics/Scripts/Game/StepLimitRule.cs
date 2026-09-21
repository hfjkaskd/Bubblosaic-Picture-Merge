using UnityEngine;

namespace BubblePics
{
    /// <summary>Port of step_limit_death_rule.gd.</summary>
    public class StepLimitRule
    {
        public const int MIN_STEPS = 5;

        public int StepsLeft { get; private set; }
        public int UsedSteps { get; private set; }
        public bool Unlimited { get; private set; }

        public System.Action<int> StepsChanged;

        public void OnRoundStart(BubblePage page)
        {
            UsedSteps = 0;
            Unlimited = page.IsMovesUnlimited();
            if (Unlimited) { Refresh(); return; }
            int limit = page.GetStepLimit();
            StepsLeft = limit > 0 ? limit : Mathf.Max(MIN_STEPS, Mathf.CeilToInt(page.CountTotalFragments()));
            Refresh();
        }

        /// <summary>Every merge attempt (success or fail) consumes a step.</summary>
        public void OnMergeAttempt(bool succeeded)
        {
            UsedSteps++;
            if (Unlimited) return;
            if (!BubbleSpecialRules.ShouldConsumeStep(
                    succeeded, AppConfig.StepUseErrorOnly))
                return;
            StepsLeft = Mathf.Max(0, StepsLeft - 1);
            Refresh();
        }

        public bool IsDead() => !Unlimited && StepsLeft <= 0;

        public void Revive(int add)
        {
            StepsLeft = Mathf.Max(0, StepsLeft + add);
            Refresh();
        }

        public void GrantBonusSteps(int add)
        {
            if (Unlimited || add <= 0) return;
            StepsLeft = Mathf.Max(0, StepsLeft + add);
            Refresh();
        }

        public void SetStepsLeft(int v)
        {
            StepsLeft = Mathf.Max(0, v);
            Refresh();
        }

        public void Restore(int stepsLeft, int usedSteps, bool unlimited)
        {
            Unlimited = unlimited;
            UsedSteps = Mathf.Max(0, usedSteps);
            StepsLeft = Mathf.Max(0, stepsLeft);
            Refresh();
        }

        void Refresh() { StepsChanged?.Invoke(StepsLeft); }
    }
}
