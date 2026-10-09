using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RemoteImageDelivery;
using UnityEditor;
using UnityEngine;

namespace BubblePics.EditorTools
{
    public static class LevelReuseDropInspection
    {
        public static void Run(string folder)
        {
            if (!EditorApplication.isPlaying || App.I?.Page == null || BizzaGameplayBridge.IsInputBlocked)
                throw new InvalidOperationException("Wait for gameplay to finish loading.");
            Directory.CreateDirectory(folder);
            App.I.StartCoroutine(Verify(folder));
        }

        static IEnumerator Verify(string folder)
        {
            var report = new StringBuilder();
            bool passed = false;
            var page = App.I.Page;
            var originalWaves = page.Scheduler.SnapshotWaves();
            string originalLocale = Localization.CurrentLocale;
            string originalSave = SaveState.RoundSnapshot;
            int originalLevel = page.CurrentLevelNumber;
            float originalMoney = ItemUtils.GetItemCount(E_ItemType.Dollar);
            float originalProps = ItemUtils.GetItemCount(E_ItemType.GameProp_2);
            try
            {
                Check(LevelRepo.TryGetBundled(1, out var seed), "Bundled seed exists");
                string seedLayout = seed.layout;
                Texture2D[] loaded = null;
                string error = null;
                int reconnects = 0;
                yield return LevelImageLoader.LoadForGameplay(seed, v => loaded = v, v => error = v, null, () => reconnects++);
                Check(loaded != null && error == null && seed.layout == seedLayout, "Available content stays unchanged");
                report.AppendLine("PASS normal bundled level loads without replacement");

                var missing = seed.Clone();
                missing.level = 5;
                missing.chapter = 1;
                missing.coins = 123;
                missing.level_unique_id = "qa-missing-pictures";
                int count = LevelRepo.ImageCount(missing);
                missing.image_urls = Enumerable.Range(0, count).Select(i => "https://qa-resource-miss.invalid/" + i).ToArray();
                missing.local_images = Enumerable.Repeat("qa_resource_miss", count).ToArray();
                missing.local_images_hd = missing.local_images;
                missing.local_image_files = Array.Empty<string>();
                missing.local_image_files_hd = Array.Empty<string>();
                loaded = null;
                float started = Time.realtimeSinceStartup;
                yield return LevelImageLoader.LoadForGameplay(missing, v => loaded = v, v => error = v, null, () => reconnects++);
                Check(loaded != null && error == null && missing.layout == seedLayout, "Missing images reuse complete previous layout");
                Check(missing.level == 5 && missing.chapter == 1 && missing.coins == 123 &&
                    missing.level_unique_id == "qa-missing-pictures", "Progress and reward identity retained");
                Check(missing.image_urls.SequenceEqual(seed.image_urls), "Whole image set follows reused layout");
                Check(LevelValidator.Validate(missing.layout, loaded.Length).IsValid, "Reused level is completable");
                report.AppendLine("PASS missing images reuse previous whole level; identity/rewards retained; seconds=" + (Time.realtimeSinceStartup - started).ToString("F2"));

                var frozen = JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(missing));
                loaded = null;
                yield return LevelImageLoader.LoadForGameplay(frozen, v => loaded = v, v => error = v, null, () => reconnects++);
                Check(loaded != null && frozen.layout == missing.layout && frozen.image_urls.SequenceEqual(missing.image_urls), "Frozen content resumes unchanged");
                report.AppendLine("PASS serialized reused content reloads with the same layout and pictures");

                var gap = new LevelData { level = LevelRepo.Count + 1, chapter = LevelRepo.Count / 25 + 1, coins = 321 };
                Check(!LevelRepo.TryGet(gap.level, out _), "Catalog gap is genuine");
                loaded = null;
                yield return LevelImageLoader.LoadForGameplay(gap, v => loaded = v, v => error = v, null, () => reconnects++);
                Check(loaded != null && error == null && gap.level == LevelRepo.Count + 1 && gap.coins == 321,
                    "Missing level metadata also reuses locally");
                report.AppendLine("PASS absent catalog entry reuses content without changing requested level");

                var unavailableFirst = new LevelData { level = 1, chapter = 1 };
                loaded = null;
                yield return LevelImageLoader.LoadForGameplay(unavailableFirst, v => loaded = v, v => error = v, null, () => reconnects++);
                Check(loaded != null && error == null, "Bundled seed rescue works without an eligible previous level");
                report.AppendLine("PASS starter fallback works even without eligible previous-level history");

                var client = App.I.RemoteImages.Client;
                var corrupt = new RemoteImageAsset { key = "qa-corrupt-" + Guid.NewGuid().ToString("N"),
                    absoluteUrl = "https://qa-resource-miss.invalid/corrupt.png", variant = "qa" };
                try
                {
                    File.WriteAllText(client.Cache.DataPath(corrupt), new string('x', 128));
                    Texture2D invalid = null;
                    yield return client.LoadCachedTexture(corrupt, v => invalid = v);
                    Check(invalid == null, "Corrupt cached image returns a local miss");
                    Check(!client.History.Any(record => record.url != null && record.url.Contains("qa-resource-miss.invalid")),
                        "Missing/corrupt content did not request network");
                    report.AppendLine("PASS corrupt cache is rejected without a network repair request");
                }
                finally { client.Cache.DeleteAsset(corrupt); }
                Check(reconnects == 0, "No reconnect loop or overlay");

                var board = new List<BubbleFragment> { BubbleFragment.FromToken("1.1"), BubbleFragment.FromToken("1.2") };
                var scheduler = new WaveScheduler();
                scheduler.Build("1.3,1.4|2.1,2.2,2.3,2.4", 2);
                Check(scheduler.CanPullPlayableBatch(board, 15), "Drop is available while bubbles still exist");
                var fullBoard = Enumerable.Range(1, 4).Select(i => BubbleFragment.FromToken("3." + i)).ToList();
                Check(!scheduler.CanPullPlayableBatch(fullBoard, 4), "Full board waits for a safe batch");
                Check(scheduler.CountTotalFragments() == 6, "Availability checks do not consume pieces");
                report.AppendLine("PASS existing bubbles do not prevent a valid drop; full-board check preserves queued pieces");

                Localization.SetLocale("en");
                page.Scheduler.RestoreWaves(Array.Empty<IEnumerable<string>>());
                Check(page.CountRemainingBubbles() > 0 && page.GetDropAvailability() == DropAvailability.NoPending,
                    "NoPending is distinct from existing board bubbles");
                Check(!BizzaGameplayBridge.UseTool(1), "Empty queue cannot spend a tool");
                yield return new WaitForSeconds(.3f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "drop-no-pending.png"));
                yield return null;
                yield return null;
                page.Scheduler.RestoreWaves(originalWaves);
                page.Field.BeginSpawnBatch();
                try { Check(page.GetDropAvailability() == DropAvailability.Spawning && !BizzaGameplayBridge.UseTool(1), "Spawn-in-flight gets its own reason"); }
                finally { page.Field.EndSpawnBatch(); }
                Check(originalProps == ItemUtils.GetItemCount(E_ItemType.GameProp_2), "Failed use leaves tool inventory unchanged");
                Check(originalLevel == page.CurrentLevelNumber && originalMoney == ItemUtils.GetItemCount(E_ItemType.Dollar), "Player progress and balance unchanged");
                report.AppendLine("PASS no-pending/spawning messages distinguished; refused use consumes no tool");
                passed = true;
            }
            finally
            {
                page.Scheduler.RestoreWaves(originalWaves);
                SaveState.RoundSnapshot = originalSave;
                Localization.SetLocale(originalLocale);
                File.WriteAllText(Path.Combine(folder, "result.txt"), (passed ? "PASS\n" : "FAIL: see Editor.log\n") + report);
            }
        }

        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
