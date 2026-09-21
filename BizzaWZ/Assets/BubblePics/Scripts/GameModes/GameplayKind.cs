using System;
using System.Collections.Generic;
using Obfuz;

namespace BubblePics.GameModes
{
    /// <summary>
    /// Stable gameplay identifiers shared by StageData-style selectors.
    /// Alternate content is selected before BubbleFragment sees a token, so
    /// prefixed source tokens (s/w/t) are never fed into its numeric parser.
    /// </summary>
    [ObfuzIgnore(ObfuzScope.Field)]
    public enum GameplayKind
    {
        Main = 0,
        CategoryMatch = 1,
        WordMatch = 2,
        NumberMatch = 3,
        Tangram = 4,
        Bonus = 5,
    }

    public readonly struct LevelCoordinate
    {
        public const int ChapterSize = 25;

        public LevelCoordinate(int globalLevel)
        {
            GlobalLevel = Math.Max(1, globalLevel);
            Chapter = (GlobalLevel - 1) / ChapterSize + 1;
            LevelIndex = (GlobalLevel - 1) % ChapterSize + 1;
        }

        public int GlobalLevel { get; }
        public int Chapter { get; }
        public int LevelIndex { get; }

        public string Key => Chapter + "_" + LevelIndex;

        public static int ToGlobal(int chapter, int levelIndex)
        {
            return (Math.Max(1, chapter) - 1) * ChapterSize +
                   Math.Max(1, levelIndex);
        }
    }

    /// <summary>
    /// Immutable-enough hand-off from a selector to a mode preparer. Payload
    /// belongs to the selector and must be treated as read-only by consumers.
    /// </summary>
    public sealed class LevelModeSelection
    {
        public GameplayKind Kind = GameplayKind.Main;
        public int GlobalLevel;
        public int Chapter;
        public int LevelIndex;
        public string Layout = string.Empty;
        public int StepLimit;
        public string Source = string.Empty;
        public object Payload;
        public bool IsAutomatic;

        public string CoordinateKey => Chapter + "_" + LevelIndex;

        public LevelModeSelection Clone()
        {
            return new LevelModeSelection
            {
                Kind = Kind,
                GlobalLevel = GlobalLevel,
                Chapter = Chapter,
                LevelIndex = LevelIndex,
                Layout = Layout,
                StepLimit = StepLimit,
                Source = Source,
                Payload = Payload,
                IsAutomatic = IsAutomatic,
            };
        }
    }

    /// <summary>
    /// A selector only identifies content. Network/file preparation and page
    /// mutation happen later, so selection remains deterministic and cheap.
    /// </summary>
    public interface ILevelModeSelector
    {
        int Priority { get; }

        bool TrySelect(
            int globalLevel,
            bool automatic,
            out LevelModeSelection selection);
    }

    /// <summary>
    /// StageDataManager-equivalent deterministic arbitration. Higher priority
    /// wins; Bonus intercepts a pending reward round, while sparse manifest
    /// modes take precedence over the dense Number Match release schedule.
    /// </summary>
    public static class LevelModeRouter
    {
        static readonly List<ILevelModeSelector> Selectors =
            new List<ILevelModeSelector>();

        public static void RegisterSelector(ILevelModeSelector selector)
        {
            if (selector == null || Selectors.Contains(selector)) return;
            Selectors.Add(selector);
            Selectors.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public static void UnregisterSelector(ILevelModeSelector selector)
        {
            if (selector != null) Selectors.Remove(selector);
        }

        public static bool TryResolveAutomatic(
            int globalLevel,
            out LevelModeSelection selection)
        {
            return TryResolve(globalLevel, true, null, out selection);
        }

        public static bool TryResolveExplicit(
            GameplayKind kind,
            int globalLevel,
            out LevelModeSelection selection)
        {
            return TryResolve(globalLevel, false, kind, out selection);
        }

        static bool TryResolve(
            int globalLevel,
            bool automatic,
            GameplayKind? requestedKind,
            out LevelModeSelection selection)
        {
            selection = null;
            if (globalLevel < 1) return false;

            for (int i = 0; i < Selectors.Count; i++)
            {
                ILevelModeSelector selector = Selectors[i];
                if (!selector.TrySelect(
                        globalLevel,
                        automatic,
                        out LevelModeSelection candidate) ||
                    candidate == null ||
                    candidate.Kind == GameplayKind.Main)
                    continue;

                if (requestedKind.HasValue &&
                    candidate.Kind != requestedKind.Value)
                    continue;

                var coordinate = new LevelCoordinate(globalLevel);
                candidate.GlobalLevel = globalLevel;
                if (candidate.Chapter <= 0)
                    candidate.Chapter = coordinate.Chapter;
                if (candidate.LevelIndex <= 0)
                    candidate.LevelIndex = coordinate.LevelIndex;
                candidate.IsAutomatic = automatic;
                selection = candidate;
                return true;
            }
            return false;
        }

        public static IReadOnlyList<ILevelModeSelector> RegisteredSelectors =>
            Selectors;
    }
}
