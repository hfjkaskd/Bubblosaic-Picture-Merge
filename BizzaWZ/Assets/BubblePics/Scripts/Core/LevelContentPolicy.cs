using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Data-driven level content policy. Values are read from
    /// Resources/Levels/content_policy.json and deliberately have safe
    /// built-in defaults so a missing config never makes bundled levels
    /// unplayable.
    /// </summary>
    [Serializable]
    public sealed class LevelContentPolicy
    {
        public const string BundledSource = "bundled";
        public const string StreamingSource = "streaming";
        public const string CacheSource = "cache";
        public const string CdnSource = "cdn";

        public string[] source_order =
        {
            StreamingSource,
            BundledSource,
            CacheSource,
            CdnSource,
        };
        public bool network_enabled = true;
        public bool offline_fallback_enabled = true;
        public bool cdn_resize_enabled = true;
        public int max_concurrent_downloads = 4;
        public int request_timeout_seconds = 20;
        public int memory_cache_limit = 48;
        public string cache_directory = "level_images";

        static LevelContentPolicy _current;

        public static LevelContentPolicy Current
        {
            get
            {
                if (_current == null) _current = Load();
                return _current;
            }
        }

        static LevelContentPolicy Load()
        {
            var policy = new LevelContentPolicy();
            var json = Resources.Load<TextAsset>("Levels/content_policy");
            if (json != null)
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(json.text, policy);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "Level content policy is invalid; using safe defaults: " +
                        ex.Message);
                    policy = new LevelContentPolicy();
                }
            }
            policy.Sanitize();
            return policy;
        }

        void Sanitize()
        {
            max_concurrent_downloads = Mathf.Clamp(max_concurrent_downloads, 1, 16);
            request_timeout_seconds = Mathf.Clamp(request_timeout_seconds, 3, 120);
            memory_cache_limit = Mathf.Clamp(memory_cache_limit, 8, 256);
            cache_directory = SanitizeDirectory(cache_directory);

            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cleaned = new List<string>();
            if (source_order != null)
            {
                foreach (string raw in source_order)
                {
                    string source = NormalizeSource(raw);
                    if (source.Length > 0 && unique.Add(source))
                        cleaned.Add(source);
                }
            }

            // A malformed or empty policy must remain offline playable.
            if (cleaned.Count == 0)
            {
                cleaned.Add(StreamingSource);
                cleaned.Add(BundledSource);
                cleaned.Add(CacheSource);
                cleaned.Add(CdnSource);
            }
            source_order = cleaned.ToArray();
        }

        public bool ContainsSource(string source)
        {
            string expected = NormalizeSource(source);
            foreach (string entry in source_order)
            {
                if (string.Equals(entry, expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        static string NormalizeSource(string source)
        {
            switch ((source ?? "").Trim().ToLowerInvariant())
            {
                case "resource":
                case "resources":
                case "builtin":
                case BundledSource:
                    return BundledSource;
                case "streamingassets":
                case "streaming_assets":
                case "local_file":
                case StreamingSource:
                    return StreamingSource;
                case "disk":
                case "persistent":
                case "persistent_cache":
                case CacheSource:
                    return CacheSource;
                case "remote":
                case "network":
                case CdnSource:
                    return CdnSource;
                default:
                    return "";
            }
        }

        static string SanitizeDirectory(string value)
        {
            string clean = (value ?? "").Trim()
                .Replace('\\', '_')
                .Replace('/', '_');
            if (clean == "." || clean == ".." || clean.Length == 0)
                return "level_images";
            return clean;
        }

#if UNITY_EDITOR
        /// <summary>Allows editor tests to reload the JSON after an asset change.</summary>
        public static void Reload()
        {
            _current = null;
        }
#endif
    }
}
