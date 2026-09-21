using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubblePics.GameModes
{
    [Serializable]
    public sealed class ModeManifestLevelEntry
    {
        public int Chapter;
        public int LevelIndex;
        public string Layout = string.Empty;
        public int StepLimit;

        public int GlobalLevel =>
            LevelCoordinate.ToGlobal(Chapter, LevelIndex);
        public string Key => Chapter + "_" + LevelIndex;
    }

    public sealed class CategoryIconEntry
    {
        public string Code = string.Empty;
        public string Url = string.Empty;
        public string Sha256 = string.Empty;
        public long ByteSize;
    }

    public sealed class LocalizedModeLabel
    {
        public string DefaultText = string.Empty;
        public readonly Dictionary<string, string> Translations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Resolve(string locale = null)
        {
            string normalized = Localization.NormalizeLocale(
                string.IsNullOrWhiteSpace(locale)
                    ? Localization.CurrentLocale
                    : locale);
            if (Translations.TryGetValue(normalized, out string exact) &&
                !string.IsNullOrWhiteSpace(exact))
                return exact;

            int separator = normalized.IndexOf('_');
            if (separator > 0 &&
                Translations.TryGetValue(
                    normalized.Substring(0, separator),
                    out string language) &&
                !string.IsNullOrWhiteSpace(language))
                return language;

            if (Translations.TryGetValue("en", out string english) &&
                !string.IsNullOrWhiteSpace(english))
                return english;
            return DefaultText ?? string.Empty;
        }
    }

    public sealed class CategoryMatchCatalogData
    {
        public int Version;
        public readonly Dictionary<string, CategoryIconEntry> Icons =
            new Dictionary<string, CategoryIconEntry>(StringComparer.Ordinal);
        public readonly Dictionary<int, LocalizedModeLabel> Categories =
            new Dictionary<int, LocalizedModeLabel>();
    }

    public sealed class WordEntry
    {
        public string Code = string.Empty;
        public LocalizedModeLabel Label = new LocalizedModeLabel();
    }

    public sealed class WordMatchCatalogData
    {
        public int Version;
        public readonly Dictionary<string, WordEntry> Words =
            new Dictionary<string, WordEntry>(StringComparer.Ordinal);
        public readonly Dictionary<int, LocalizedModeLabel> Categories =
            new Dictionary<int, LocalizedModeLabel>();
    }

    [Serializable]
    public sealed class TangramPieceData
    {
        public int PieceId;
        public bool Preplaced;
        public Vector2[] Polygon = Array.Empty<Vector2>();
    }

    [Serializable]
    public sealed class TangramTargetData
    {
        public int Code;
        public TangramPieceData[] Pieces = Array.Empty<TangramPieceData>();
        public string OriginalResourceStem = string.Empty;
        public Rect OriginalCrop;
        public string SourceCellName = string.Empty;
    }

    public sealed class TangramCatalogData
    {
        public int Version;
        public readonly Dictionary<int, TangramTargetData> Targets =
            new Dictionary<int, TangramTargetData>();
    }

    [Serializable]
    public sealed class BonusLevelData
    {
        public int Sequence;
        public int LevelIndex;
        public int StepLimit;
        public string DifficultyType = string.Empty;
        public string Layout = string.Empty;
        public string[] ImageUrls = Array.Empty<string>();
        public string[] ResourceStems = Array.Empty<string>();
    }

    public sealed class ModeLevelManifest
    {
        public int Version;
        public readonly Dictionary<string, ModeManifestLevelEntry> Levels =
            new Dictionary<string, ModeManifestLevelEntry>(StringComparer.Ordinal);

        public bool TryGet(
            int chapter,
            int levelIndex,
            out ModeManifestLevelEntry entry)
        {
            return Levels.TryGetValue(
                chapter + "_" + levelIndex,
                out entry);
        }
    }
}
