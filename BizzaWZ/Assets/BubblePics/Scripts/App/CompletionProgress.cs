using System;

namespace BubblePics
{
    public interface ICompletedLevelStore
    {
        /// <summary>
        /// Must be idempotent. Replaying an already completed level must retain
        /// existing duration/details while marking it completed.
        /// </summary>
        bool MarkCompleted(int chapter, int levelIndex);
    }

    public static class LevelCompletionJournal
    {
        const int ChapterSize = 25;

        public static int PendingLevel => SaveState.PendingLevelCompletion;

        public static bool Begin(int level)
        {
            return SaveState.BeginLevelCompletion(level);
        }

        public static bool Commit(int level, ICompletedLevelStore store = null)
        {
            if (level <= 0 || PendingLevel != level) return false;
            ToChapterLevel(level, out int chapter, out int index);
            if (store != null && !store.MarkCompleted(chapter, index))
                return false;
            return SaveState.FinalizeLevelCompletion(level);
        }

        public static bool Recover(ICompletedLevelStore store = null)
        {
            int pending = PendingLevel;
            return pending <= 0 || Commit(pending, store);
        }

        static void ToChapterLevel(
            int level,
            out int chapter,
            out int index)
        {
            chapter = (level - 1) / ChapterSize + 1;
            index = (level - 1) % ChapterSize + 1;
        }
    }

    public enum CompletionRoute
    {
        NextLevel,
        Home,
    }

    /// <summary>Pure route decision shared by completion UI and validation.</summary>
    public static class CompletionRouteDecision
    {
        public static CompletionRoute Decide(
            int completedLevel,
            bool nextLevelAvailable,
            bool homeCycleEnabled,
            bool categoryRouteActive = false)
        {
            if (!nextLevelAvailable) return CompletionRoute.Home;
            if (homeCycleEnabled) return CompletionRoute.Home;
            return CompletionRoute.NextLevel;
        }
    }
}
