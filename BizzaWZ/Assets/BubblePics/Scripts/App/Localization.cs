using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Runtime localization backed by the original game's Godot translation
    /// catalog. Locale aliases and the default-to-English behaviour mirror
    /// launcher.gd::_apply_system_locale().
    /// </summary>
    public static class Localization
    {
        const string EnglishLocale = "en";
        const string SimplifiedChineseLocale = "zh_CN";

        static readonly Dictionary<string, string> EmptyMap =
            new Dictionary<string, string>(StringComparer.Ordinal);

        static Dictionary<string, string> _map;
        static Dictionary<string, string> _englishFallback;
        static Dictionary<string, string> _simplifiedChineseFallback;
        static string _currentLocale;
        static bool _loaded;

        /// <summary>The normalized locale currently used by <see cref="Tr"/>.</summary>
        public static string CurrentLocale
        {
            get
            {
                EnsureLoaded();
                return _currentLocale;
            }
        }

        /// <summary>
        /// Raised after <see cref="SetLocale"/> changes the active locale.
        /// Existing views may subscribe when live language switching is needed.
        /// </summary>
        public static event Action LocaleChanged;

        static void EnsureLoaded()
        {
            if (_loaded) return;
            LoadLocale(DetectSystemLocale());
        }

        static void LoadLocale(string locale)
        {
            _currentLocale = NormalizeLocale(locale);
            _map = LoadMap(_currentLocale);
            _englishFallback = _currentLocale == EnglishLocale
                ? _map
                : LoadMap(EnglishLocale);
            _simplifiedChineseFallback = _currentLocale == SimplifiedChineseLocale
                ? _map
                : LoadMap(SimplifiedChineseLocale);
            _loaded = true;
        }

        static Dictionary<string, string> LoadMap(string locale)
        {
            var ta = Resources.Load<TextAsset>("Localization/" + locale);
            if (ta == null) return EmptyMap;

            var parsed = SpineLite.MiniJson.Parse(ta.text) as Dictionary<string, object>;
            if (parsed == null) return EmptyMap;

            var result = new Dictionary<string, string>(parsed.Count, StringComparer.Ordinal);
            foreach (var kv in parsed)
                result[kv.Key] = kv.Value as string ?? string.Empty;
            return result;
        }

        /// <summary>
        /// Selects a supported locale or its nearest original-game alias.
        /// Unsupported locale names intentionally fall back to English.
        /// </summary>
        public static void SetLocale(string locale)
        {
            string normalized = NormalizeLocale(locale);
            EnsureLoaded();
            if (string.Equals(normalized, _currentLocale, StringComparison.Ordinal))
                return;

            LoadLocale(normalized);
            LocaleChanged?.Invoke();
        }

        /// <summary>Returns to the locale reported by the current device.</summary>
        public static void UseSystemLocale()
        {
            SetLocale(DetectSystemLocale());
        }

        /// <summary>
        /// Normalizes platform locale strings such as zh-Hant-HK, pt-PT,
        /// in-ID and no-NO to the resources shipped by the original game.
        /// </summary>
        public static string NormalizeLocale(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
                return EnglishLocale;

            string normalized = locale.Trim().Replace('-', '_');
            string lower = normalized.ToLowerInvariant();
            if (lower == "chinesetraditional")
                return "zh_TW";
            if (lower == "chinesesimplified" || lower == "chinese")
                return SimplifiedChineseLocale;
            if (lower == "zh" || lower.StartsWith("zh_", StringComparison.Ordinal))
            {
                bool traditional =
                    lower.Contains("hant") ||
                    lower.Contains("_tw") ||
                    lower.Contains("_hk") ||
                    lower.Contains("_mo");
                return traditional ? "zh_TW" : SimplifiedChineseLocale;
            }

            int separator = lower.IndexOf('_');
            string language = separator >= 0 ? lower.Substring(0, separator) : lower;
            switch (language)
            {
                case "en": return "en";
                case "pt": return "pt_BR";
                case "id":
                case "in": return "id";
                case "tl":
                case "fil": return "fil";
                case "es": return "es";
                case "ru": return "ru";
                case "de": return "de";
                case "ja": return "ja";
                case "fr": return "fr";
                case "ko": return "ko";
                case "it": return "it";
                case "pl": return "pl";
                case "tr": return "tr";
                case "uk": return "uk";
                case "th": return "th";
                case "ms": return "ms";
                case "vi": return "vi";
                case "ro": return "ro";
                case "nl": return "nl";
                case "nb":
                case "no": return "nb";
                case "fi": return "fi";
                case "el": return "el";
                case "sv": return "sv";
                case "hu": return "hu";
                case "bn": return "bn";
                case "ta": return "ta";
                case "ur": return "ur";
                default: return EnglishLocale;
            }
        }

        static string DetectSystemLocale()
        {
            try
            {
                string culture = CultureInfo.CurrentUICulture != null
                    ? CultureInfo.CurrentUICulture.Name
                    : string.Empty;
                if (IsSupportedLanguageName(culture))
                    return NormalizeLocale(culture);
            }
            catch (CultureNotFoundException)
            {
                // Some stripped mobile runtimes have no usable UI culture.
            }

            switch (Application.systemLanguage)
            {
                case SystemLanguage.ChineseTraditional: return "zh_TW";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return SimplifiedChineseLocale;
                case SystemLanguage.Portuguese: return "pt_BR";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Vietnamese: return "vi";
                case SystemLanguage.Romanian: return "ro";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Norwegian: return "nb";
                case SystemLanguage.Finnish: return "fi";
                case SystemLanguage.Greek: return "el";
                case SystemLanguage.Swedish: return "sv";
                case SystemLanguage.Hungarian: return "hu";
                default: return EnglishLocale;
            }
        }

        static bool IsSupportedLanguageName(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale)) return false;
            string normalized = locale.Trim().Replace('-', '_').ToLowerInvariant();
            if (normalized == "chinese" ||
                normalized == "chinesesimplified" ||
                normalized == "chinesetraditional" ||
                normalized == "zh" ||
                normalized.StartsWith("zh_", StringComparison.Ordinal))
                return true;

            int separator = normalized.IndexOf('_');
            string language = separator >= 0 ? normalized.Substring(0, separator) : normalized;
            switch (language)
            {
                case "en":
                case "pt":
                case "id":
                case "in":
                case "tl":
                case "fil":
                case "es":
                case "ru":
                case "de":
                case "ja":
                case "fr":
                case "ko":
                case "it":
                case "pl":
                case "tr":
                case "uk":
                case "th":
                case "ms":
                case "vi":
                case "ro":
                case "nl":
                case "nb":
                case "no":
                case "fi":
                case "el":
                case "sv":
                case "hu":
                case "bn":
                case "ta":
                case "ur":
                    return true;
                default:
                    return false;
            }
        }

        public static string Tr(string key)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(key)) return key ?? string.Empty;

            if (TryGetNonEmpty(_map, key, out string value))
                return value;
            if (TryGetNonEmpty(_englishFallback, key, out value))
                return value;
            if (TryGetNonEmpty(_simplifiedChineseFallback, key, out value))
                return value;
            return key;
        }

        static bool TryGetNonEmpty(
            Dictionary<string, string> map,
            string key,
            out string value)
        {
            value = null;
            return map != null &&
                   map.TryGetValue(key, out value) &&
                   !string.IsNullOrEmpty(value);
        }
    }
}
