using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BubblePics.GameModes
{
    /// <summary>
    /// Loads the immutable 1.0.9 release content. Catalogs, manifests and
    /// release-enabled content uses stable Resources paths. Disabled content
    /// remains available to editor GM previews through ResourceAssetLoader.
    /// </summary>
    public static class ModeCatalogRepository
    {
        public const string CategoryCatalogPath =
            "Version109/Data/CategoryMatch/category_catalog";
        public const string CategoryManifestPath =
            "Version109/Data/CategoryMatch/category_level_manifest";
        public const string WordCatalogPath =
            "Version109/Data/WordMatch/word_catalog";
        public const string WordManifestPath =
            "Version109/Data/WordMatch/word_level_manifest";
        public const string TangramCatalogPath =
            "Version109/Data/TangramMatch/tangram_catalog";
        public const string TangramManifestPath =
            "Version109/Data/TangramMatch/tangram_level_manifest";
        public const string TangramRemoteManifestPath =
            "Version109/Data/Result/shape_level_manifest";
        public const string BonusLevelsPath =
            "Version109/Data/Bonus/bonus_levels";
        public const string BonusImageRoot =
            "Art/Version109/Bonus/";
        public const string TangramOriginalRoot =
            "Art/Version109/TangramOriginals/";

        static CategoryMatchCatalogData _categoryCatalog;
        static ModeLevelManifest _categoryManifest;
        static WordMatchCatalogData _wordCatalog;
        static ModeLevelManifest _wordManifest;
        static TangramCatalogData _tangramCatalog;
        static ModeLevelManifest _tangramManifest;
        static BonusLevelData[] _bonusLevels;

        public static CategoryMatchCatalogData CategoryCatalog =>
            _categoryCatalog ??= LoadCategoryCatalog();

        public static ModeLevelManifest CategoryManifest =>
            _categoryManifest ??= LoadManifest(CategoryManifestPath);

        public static WordMatchCatalogData WordCatalog =>
            _wordCatalog ??= LoadWordCatalog();

        public static ModeLevelManifest WordManifest =>
            _wordManifest ??= LoadManifest(WordManifestPath);

        public static TangramCatalogData TangramCatalog =>
            _tangramCatalog ??= LoadTangramCatalog();

        public static ModeLevelManifest TangramManifest =>
            _tangramManifest ??= LoadFirstManifest(
                TangramRemoteManifestPath,
                "Version109/Data/TangramMatch/shape_level_manifest",
                TangramManifestPath);

        public static IReadOnlyList<BonusLevelData> BonusLevels =>
            _bonusLevels ??= LoadBonusLevels();

        public static void Reload()
        {
            _categoryCatalog = null;
            _categoryManifest = null;
            _wordCatalog = null;
            _wordManifest = null;
            _tangramCatalog = null;
            _tangramManifest = null;
            _bonusLevels = null;
        }

        static TextAsset LoadText(params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(paths[i])) continue;
                TextAsset text = ResourceAssetLoader.Load<TextAsset>(paths[i]);
                if (text != null) return text;
            }
            return null;
        }

        static CategoryMatchCatalogData LoadCategoryCatalog()
        {
            var result = new CategoryMatchCatalogData();
            TextAsset text = LoadText(CategoryCatalogPath);
            Dictionary<string, object> root =
                ModeJson.ParseObject(text != null ? text.text : null);
            if (root == null) return result;
            result.Version = ModeJson.Int(root, "version");

            Dictionary<string, object> icons = ModeJson.Object(
                root.TryGetValue("icons", out object rawIcons)
                    ? rawIcons
                    : null);
            if (icons != null)
            {
                foreach (KeyValuePair<string, object> pair in icons)
                {
                    Dictionary<string, object> entry =
                        ModeJson.Object(pair.Value);
                    string url = ModeJson.String(entry, "url").Trim();
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    result.Icons[pair.Key] = new CategoryIconEntry
                    {
                        Code = pair.Key,
                        Url = url,
                        Sha256 = ModeJson.String(entry, "sha256").Trim(),
                        ByteSize = ModeJson.Int(entry, "bytes"),
                    };
                }
            }

            LoadLabels(
                root,
                "categories",
                "category_name",
                result.Categories);
            return result;
        }

        static WordMatchCatalogData LoadWordCatalog()
        {
            var result = new WordMatchCatalogData();
            TextAsset text = LoadText(WordCatalogPath);
            Dictionary<string, object> root =
                ModeJson.ParseObject(text != null ? text.text : null);
            if (root == null) return result;
            result.Version = ModeJson.Int(root, "version");

            Dictionary<string, object> words = ModeJson.Object(
                root.TryGetValue("words", out object rawWords)
                    ? rawWords
                    : null);
            if (words != null)
            {
                foreach (KeyValuePair<string, object> pair in words)
                {
                    Dictionary<string, object> entry =
                        ModeJson.Object(pair.Value);
                    if (entry == null) continue;
                    result.Words[pair.Key] = new WordEntry
                    {
                        Code = pair.Key,
                        Label = ParseLabel(entry, "word_name"),
                    };
                }
            }

            LoadLabels(
                root,
                "categories",
                "category_name",
                result.Categories);
            return result;
        }

        static void LoadLabels(
            Dictionary<string, object> root,
            string collectionKey,
            string defaultKey,
            Dictionary<int, LocalizedModeLabel> destination)
        {
            Dictionary<string, object> labels = ModeJson.Object(
                root.TryGetValue(collectionKey, out object raw)
                    ? raw
                    : null);
            if (labels == null) return;
            foreach (KeyValuePair<string, object> pair in labels)
            {
                if (!int.TryParse(pair.Key, out int id) || id < 1) continue;
                Dictionary<string, object> entry = ModeJson.Object(pair.Value);
                if (entry == null) continue;
                destination[id] = ParseLabel(entry, defaultKey);
            }
        }

        static LocalizedModeLabel ParseLabel(
            Dictionary<string, object> entry,
            string defaultKey)
        {
            var label = new LocalizedModeLabel
            {
                DefaultText = ModeJson.String(entry, defaultKey),
            };
            Dictionary<string, object> translations = ModeJson.Object(
                entry.TryGetValue("translations", out object raw)
                    ? raw
                    : null);
            if (translations == null) return label;
            foreach (KeyValuePair<string, object> pair in translations)
            {
                string value = pair.Value as string;
                if (!string.IsNullOrWhiteSpace(value))
                    label.Translations[pair.Key] = value;
            }
            return label;
        }

        static TangramCatalogData LoadTangramCatalog()
        {
            var result = new TangramCatalogData();
            TextAsset text = LoadText(TangramCatalogPath);
            Dictionary<string, object> root =
                ModeJson.ParseObject(text != null ? text.text : null);
            if (root == null) return result;
            result.Version = ModeJson.Int(root, "version");
            Dictionary<string, object> items = ModeJson.Object(
                root.TryGetValue("items", out object rawItems)
                    ? rawItems
                    : null);
            if (items == null) return result;

            foreach (KeyValuePair<string, object> pair in items)
            {
                if (!int.TryParse(pair.Key, out int code) || code < 1)
                    continue;
                Dictionary<string, object> item = ModeJson.Object(pair.Value);
                TangramTargetData target = ParseTangramTarget(code, item);
                if (target != null) result.Targets[code] = target;
            }
            return result;
        }

        static TangramTargetData ParseTangramTarget(
            int code,
            Dictionary<string, object> item)
        {
            if (item == null) return null;
            IList rawPieces = ModeJson.Array(
                item.TryGetValue("pieces", out object piecesValue)
                    ? piecesValue
                    : null);
            if (rawPieces == null) return null;

            var pieces = new List<TangramPieceData>(rawPieces.Count);
            for (int i = 0; i < rawPieces.Count; i++)
            {
                Dictionary<string, object> piece =
                    ModeJson.Object(rawPieces[i]);
                if (piece == null) continue;
                IList rawPolygon = ModeJson.Array(
                    piece.TryGetValue("polygon", out object polygonValue)
                        ? polygonValue
                        : null);
                var polygon = new List<Vector2>();
                if (rawPolygon != null)
                {
                    for (int p = 0; p < rawPolygon.Count; p++)
                    {
                        IList point = ModeJson.Array(rawPolygon[p]);
                        if (point == null || point.Count != 2) continue;
                        polygon.Add(new Vector2(
                            ModeJson.Float(point[0]),
                            ModeJson.Float(point[1])));
                    }
                }
                pieces.Add(new TangramPieceData
                {
                    PieceId = ModeJson.Int(piece, "piece_id", -1),
                    Preplaced = ModeJson.Bool(piece, "preplaced"),
                    Polygon = polygon.ToArray(),
                });
            }

            var target = new TangramTargetData
            {
                Code = code,
                Pieces = pieces.ToArray(),
            };
            Dictionary<string, object> sourceImage = ModeJson.Object(
                item.TryGetValue("source_image", out object imageValue)
                    ? imageValue
                    : null);
            if (sourceImage != null)
            {
                string resource = ModeJson.String(sourceImage, "resource");
                target.OriginalResourceStem = string.IsNullOrWhiteSpace(resource)
                    ? string.Empty
                    : Path.GetFileNameWithoutExtension(
                        resource.Replace('\\', '/'));
                IList crop = ModeJson.Array(
                    sourceImage.TryGetValue("crop", out object cropValue)
                        ? cropValue
                        : null);
                if (crop != null && crop.Count == 4)
                {
                    target.OriginalCrop = new Rect(
                        ModeJson.Float(crop[0]),
                        ModeJson.Float(crop[1]),
                        ModeJson.Float(crop[2]),
                        ModeJson.Float(crop[3]));
                }
            }
            Dictionary<string, object> sourceCell = ModeJson.Object(
                item.TryGetValue("source_cell", out object cellValue)
                    ? cellValue
                    : null);
            if (sourceCell != null)
                target.SourceCellName = ModeJson.String(sourceCell, "name");
            return target;
        }

        static ModeLevelManifest LoadFirstManifest(params string[] paths)
        {
            TextAsset text = LoadText(paths);
            return ParseManifest(text != null ? text.text : null);
        }

        static ModeLevelManifest LoadManifest(string path)
        {
            TextAsset text = LoadText(path);
            return ParseManifest(text != null ? text.text : null);
        }

        static ModeLevelManifest ParseManifest(string json)
        {
            var result = new ModeLevelManifest();
            Dictionary<string, object> root = ModeJson.ParseObject(json);
            if (root == null) return result;
            if (root.TryGetValue("data", out object wrapped) &&
                ModeJson.Object(wrapped) is Dictionary<string, object> data)
                root = data;
            result.Version = ModeJson.Int(root, "version");
            IList levels = ModeJson.Array(
                root.TryGetValue("levels", out object rawLevels)
                    ? rawLevels
                    : null);
            if (levels == null) return result;
            for (int i = 0; i < levels.Count; i++)
            {
                Dictionary<string, object> raw =
                    ModeJson.Object(levels[i]);
                var entry = new ModeManifestLevelEntry
                {
                    Chapter = ModeJson.Int(raw, "chapter"),
                    LevelIndex = ModeJson.Int(raw, "level"),
                    Layout = ModeJson.String(raw, "layout").Trim(),
                    StepLimit = ModeJson.Int(raw, "step_limit", -1),
                };
                if (entry.Chapter < 1 || entry.LevelIndex < 1 ||
                    entry.StepLimit < 0 ||
                    string.IsNullOrWhiteSpace(entry.Layout) ||
                    result.Levels.ContainsKey(entry.Key))
                    continue;
                result.Levels[entry.Key] = entry;
            }
            return result;
        }

        static BonusLevelData[] LoadBonusLevels()
        {
            TextAsset text = LoadText(BonusLevelsPath);
            Dictionary<string, object> root =
                ModeJson.ParseObject(text != null ? text.text : null);
            IList levels = root != null &&
                           root.TryGetValue("levels", out object raw)
                ? ModeJson.Array(raw)
                : null;
            if (levels == null) return Array.Empty<BonusLevelData>();
            var result = new List<BonusLevelData>(levels.Count);
            for (int i = 0; i < levels.Count; i++)
            {
                Dictionary<string, object> item =
                    ModeJson.Object(levels[i]);
                if (item == null) continue;
                IList urls = ModeJson.Array(
                    item.TryGetValue("image_urls", out object rawUrls)
                        ? rawUrls
                        : null);
                var parsedUrls = new List<string>();
                if (urls != null)
                {
                    for (int u = 0; u < urls.Count; u++)
                    {
                        string url = urls[u] as string;
                        if (!string.IsNullOrWhiteSpace(url))
                            parsedUrls.Add(url.Trim());
                    }
                }
                IList stems = ModeJson.Array(
                    item.TryGetValue("resource_stems", out object rawStems)
                        ? rawStems
                        : null);
                var parsedStems = new List<string>();
                if (stems != null)
                {
                    for (int s = 0; s < stems.Count; s++)
                    {
                        string stem = stems[s] as string;
                        if (!string.IsNullOrWhiteSpace(stem))
                            parsedStems.Add(stem.Trim());
                    }
                }
                result.Add(new BonusLevelData
                {
                    Sequence = result.Count + 1,
                    LevelIndex = ModeJson.Int(
                        item,
                        "level_index",
                        result.Count + 1),
                    StepLimit = ModeJson.Int(item, "step_limit"),
                    DifficultyType = ModeJson.String(
                        item,
                        "difficulty_type"),
                    Layout = ModeJson.String(item, "layout_raw").Trim(),
                    ImageUrls = parsedUrls.ToArray(),
                    ResourceStems = parsedStems.ToArray(),
                });
            }
            return result.ToArray();
        }
    }
}
