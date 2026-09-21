using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RemoteImageDelivery.Editor
{
    /// <summary>
    /// File-level regression checks for the recovered cache lifecycle. This is
    /// intentionally framework-free so projects can run it from a menu or CI
    /// without adding the Unity Test Framework package.
    /// </summary>
    public static class RemoteImageCacheLifecycleValidation
    {
        [MenuItem(
            "Tools/远程图片分发/验证原版缓存生命周期",
            priority = 230)]
        public static void RunMenu()
        {
            RunOrThrow();
            Debug.Log(
                "[RemoteImageCacheLifecycleValidation] PASS");
        }

        public static void RunBatch()
        {
            try
            {
                RunOrThrow();
                Debug.Log(
                    "[RemoteImageCacheLifecycleValidation] PASS");
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                throw;
            }
        }

        public static void RunOrThrow()
        {
            string cacheNamespace =
                "__rid_cache_lifecycle_" +
                Guid.NewGuid().ToString("N");
            var config =
                ScriptableObject.CreateInstance<RemoteImageDeliveryConfig>();
            config.cacheNamespace = cacheNamespace;
            config.resumePartialDownloads = false;
            config.validateImageHeader = true;
            config.Sanitize();

            string root = "";
            try
            {
                var cache = new RemoteImageCache(config);
                root = cache.RootPath;
                AssertSafeTestRoot(root, cacheNamespace);

                RemoteImageAsset corrupt = Asset("corrupt", 1);
                WriteBytes(cache.DataPath(corrupt), 96, 0x00);
                WriteBytes(cache.PartialPath(corrupt), 96, 0x2A);
                File.WriteAllText(
                    Path.ChangeExtension(
                        cache.DataPath(corrupt),
                        ".json"),
                    "{}");

                bool valid = cache.TryGetValidFile(
                    corrupt,
                    out _,
                    out _);
                Assert(!valid, "corrupt image unexpectedly validated");
                Assert(
                    !File.Exists(cache.DataPath(corrupt)),
                    "corrupt committed image was not deleted");
                Assert(
                    File.Exists(cache.PartialPath(corrupt)),
                    "corrupt-image cleanup must not delete .part");

                RemoteImageAsset past = Asset("past", 2);
                RemoteImageAsset keep = Asset("keep", 3);
                RemoteImageAsset orphan = Asset("orphan", 4);
                WriteCommittedStub(cache, past);
                WriteCommittedStub(cache, keep);
                WriteBytes(cache.DataPath(orphan), 96, 0x33);
                WriteBytes(cache.PartialPath(past), 96, 0x44);

                int pruned = cache.PruneExcept(
                    new HashSet<string>(StringComparer.Ordinal)
                    {
                        keep.StableKey,
                    });
                Assert(pruned == 2, "prune must count both old data files");
                Assert(
                    File.Exists(cache.DataPath(keep)),
                    "keep-set image was deleted");
                Assert(
                    !File.Exists(cache.DataPath(past)) &&
                    !File.Exists(cache.DataPath(orphan)),
                    "old or metadata-less image survived progression prune");
                Assert(
                    File.Exists(cache.PartialPath(past)),
                    "progression prune must not delete .part");

                File.WriteAllText(
                    Path.Combine(root, "dangling.json.tmp"),
                    "{}");
                int cleared = cache.ClearAll();
                Assert(cleared == 1, "manual clear must count committed images");
                Assert(
                    !File.Exists(cache.DataPath(keep)),
                    "manual clear left a committed image");
                Assert(
                    File.Exists(cache.PartialPath(past)) &&
                    File.Exists(cache.PartialPath(corrupt)),
                    "manual clear must preserve in-flight .part files");
                Assert(
                    Directory.GetFiles(root, "*.json*").Length == 0,
                    "manual clear left image metadata behind");

                // Startup is the only unconditional stale-part purge in the
                // recovered lifecycle when resume is disabled.
                _ = new RemoteImageCache(config);
                Assert(
                    Directory.GetFiles(root, "*.part").Length == 0,
                    "startup did not purge stale .part files");

                config.resumePartialDownloads = true;
                WriteBytes(cache.PartialPath(past), 96, 0x55);
                _ = new RemoteImageCache(config);
                Assert(
                    File.Exists(cache.PartialPath(past)),
                    "resume mode unexpectedly purged a .part file");
            }
            finally
            {
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                {
                    AssertSafeTestRoot(root, cacheNamespace);
                    Directory.Delete(root, true);
                }
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        static RemoteImageAsset Asset(string key, int item)
        {
            return new RemoteImageAsset
            {
                key = "https://validation.invalid/" + key + ".jpg",
                relativePath = "validation/" + key + ".jpg",
                width = 512,
                height = 512,
                itemIndex = item,
                variant = "512",
            };
        }

        static void WriteCommittedStub(
            RemoteImageCache cache,
            RemoteImageAsset asset)
        {
            WriteBytes(cache.DataPath(asset), 96, 0x22);
            File.WriteAllText(
                Path.ChangeExtension(cache.DataPath(asset), ".json"),
                "{}");
        }

        static void WriteBytes(string path, int count, byte value)
        {
            var bytes = new byte[count];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = value;
            File.WriteAllBytes(path, bytes);
        }

        static void AssertSafeTestRoot(
            string root,
            string cacheNamespace)
        {
            string persistent =
                Path.GetFullPath(Application.persistentDataPath);
            string resolved = Path.GetFullPath(root ?? "");
            string boundary = persistent.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            Assert(
                resolved.StartsWith(
                    boundary,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    Path.GetFileName(resolved),
                    cacheNamespace,
                    StringComparison.Ordinal),
                "refusing to operate outside the unique validation cache");
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
