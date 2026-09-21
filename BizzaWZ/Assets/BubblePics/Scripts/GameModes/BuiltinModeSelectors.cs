using System;

namespace BubblePics.GameModes
{
    public static class BuiltinModeSelectors
    {
        static bool _registered;

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            LevelModeRouter.RegisterSelector(new BonusSelector());
            LevelModeRouter.RegisterSelector(new CategorySelector());
            LevelModeRouter.RegisterSelector(new TangramSelector());
            LevelModeRouter.RegisterSelector(new WordSelector());
        }

        sealed class BonusSelector : ILevelModeSelector
        {
            public int Priority => 1000;

            public bool TrySelect(
                int globalLevel,
                bool automatic,
                out LevelModeSelection selection)
            {
                selection = null;
                if (automatic &&
                    (!AppConfig.BonusLevel || !ModeProgress.BonusPending))
                {
                    return false;
                }

                int sequence = Math.Max(1, ModeProgress.BonusCursor + 1);
                var levels = ModeCatalogRepository.BonusLevels;
                if (sequence > levels.Count) return false;
                BonusLevelData entry = levels[sequence - 1];
                var coordinate = new LevelCoordinate(globalLevel);
                selection = new LevelModeSelection
                {
                    Kind = GameplayKind.Bonus,
                    Chapter = coordinate.Chapter,
                    LevelIndex = coordinate.LevelIndex,
                    Layout = entry.Layout,
                    StepLimit = entry.StepLimit,
                    Source = "builtin_bonus_1.0.9:" + sequence,
                    Payload = entry,
                };
                return true;
            }
        }

        sealed class CategorySelector : ILevelModeSelector
        {
            public int Priority => 800;

            public bool TrySelect(
                int globalLevel,
                bool automatic,
                out LevelModeSelection selection)
            {
                selection = null;
                if (automatic && !AppConfig.CategoryLevel)
                    return false;
                var coordinate = new LevelCoordinate(globalLevel);
                if (!ModeCatalogRepository.CategoryManifest.TryGet(
                        coordinate.Chapter,
                        coordinate.LevelIndex,
                        out ModeManifestLevelEntry entry))
                    return false;
                CompiledModeLevel compiled =
                    SpecialTokenCompiler.CompileCategory(
                        entry.Layout,
                        entry.StepLimit,
                        ModeCatalogRepository.CategoryCatalog);
                if (!compiled.IsValid) return false;
                selection = EntrySelection(
                    GameplayKind.CategoryMatch,
                    entry,
                    "category_level_1.0.8",
                    compiled);
                return true;
            }
        }

        sealed class TangramSelector : ILevelModeSelector
        {
            // Sparse manifest rounds take precedence over the dense Number
            // Match schedule so enabling every release mechanic does not hide
            // half of the recovered 19+15n Tangram levels.
            public int Priority => 750;

            public bool TrySelect(
                int globalLevel,
                bool automatic,
                out LevelModeSelection selection)
            {
                selection = null;
                if (automatic && !AppConfig.ShapeLevel)
                    return false;
                ModeLevelManifest manifest =
                    ModeCatalogRepository.TangramManifest;
                var coordinate = new LevelCoordinate(globalLevel);
                if (!manifest.TryGet(
                        coordinate.Chapter,
                        coordinate.LevelIndex,
                        out ModeManifestLevelEntry entry))
                    return false;
                CompiledModeLevel compiled =
                    SpecialTokenCompiler.CompileTangram(
                        entry.Layout,
                        entry.StepLimit,
                        ModeCatalogRepository.TangramCatalog);
                if (!compiled.IsValid) return false;
                selection = EntrySelection(
                    GameplayKind.Tangram,
                    entry,
                    "shape_level_1.0.9",
                    compiled);
                return true;
            }
        }

        sealed class WordSelector : ILevelModeSelector
        {
            // Valid sparse Word rounds also take precedence over Number Match.
            // Invalid recovered entries safely fall through to the next mode.
            public int Priority => 740;

            public bool TrySelect(
                int globalLevel,
                bool automatic,
                out LevelModeSelection selection)
            {
                selection = null;
                if (automatic && !AppConfig.WordLevel) return false;
                var coordinate = new LevelCoordinate(globalLevel);
                if (!ModeCatalogRepository.WordManifest.TryGet(
                        coordinate.Chapter,
                        coordinate.LevelIndex,
                        out ModeManifestLevelEntry entry))
                    return false;
                CompiledModeLevel compiled =
                    SpecialTokenCompiler.CompileWord(
                        entry.Layout,
                        entry.StepLimit,
                        ModeCatalogRepository.WordCatalog);
                if (!compiled.IsValid) return false;
                selection = EntrySelection(
                    GameplayKind.WordMatch,
                    entry,
                    "word_preview_1.0.9",
                    compiled);
                return true;
            }
        }

        static LevelModeSelection EntrySelection(
            GameplayKind kind,
            ModeManifestLevelEntry entry,
            string source,
            CompiledModeLevel compiled)
        {
            return new LevelModeSelection
            {
                Kind = kind,
                Chapter = entry.Chapter,
                LevelIndex = entry.LevelIndex,
                Layout = entry.Layout,
                StepLimit = entry.StepLimit,
                Source = source,
                Payload = compiled,
            };
        }
    }
}
