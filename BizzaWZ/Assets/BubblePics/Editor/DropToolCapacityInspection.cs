using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        // Test fixtures exercise the production Button, inventory and spawn path.
        // Runtime board limits and gameplay methods are never replaced for the test.
        public static async void ReviewDropToolCapacity(string folder)
        {
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            string file = Path.Combine(folder, "inspection.txt");
            File.WriteAllText(file, "RUNNING");
            void Check(bool ok, string message)
            {
                report.AppendLine((ok ? "PASS " : "FAIL ") + message);
                if (!ok) throw new InvalidOperationException(message);
            }

            BubblePage page = null;
            UIPropEntry entry = null;
            List<BubbleView> originalBoard = null;
            List<List<string>> originalWaves = null;
            string originalSnapshot = null;
            int originalLevel = 0;
            ItemEntry originalItem = default;
            bool hadItem = false, hadUseCount = false, fixtureStarted = false;
            int originalUseCount = 0;
            var randomState = UnityEngine.Random.state;
            try
            {
                page = BizzaGameplayBridge.Page;
                Check(EditorApplication.isPlaying && page != null && !BizzaGameplayBridge.IsInputBlocked,
                    "Normal InitWZ gameplay startup is ready");
                Check(!page.IsRoundFinalized() && !page.IsToolBusy() && !page.Field.IsSpawnInFlight,
                    "Round is idle before fixture");
                entry = UIModule.Instance.GetPage<RealGamePanel>().propEntries
                    .First(value => value.itemType == E_ItemType.GameProp_2);
                var settings = new SerializedObject(page);
                int cap = settings.FindProperty("_maxBoardBubbles").intValue;
                int imageCap = settings.FindProperty("_maxBoardImages").intValue;
                int minimum = settings.FindProperty("_minDropBubbles").intValue;
                int maximum = settings.FindProperty("_maxDropBubbles").intValue;
                Check(page.PickedTextures.Length >= imageCap && cap < imageCap * 16,
                    "Fixture has enough loaded textures and unique pieces");

                var allTokens = new List<string>();
                for (int parent = 1; parent <= 4; parent++)
                    for (int quadrant = 1; quadrant <= 4; quadrant++)
                        for (int image = 1; image <= imageCap; image++)
                            allTokens.Add(image + "." + parent + "." + quadrant);
                var boardTokens = allTokens.Take(cap).ToList();
                var pendingTokens = allTokens.Skip(cap).ToList();

                originalBoard = page.Field.AllBubbles();
                Check(originalBoard.All(value => value.State == BubbleState.Alive), "Original board is stable");
                originalWaves = page.Scheduler.SnapshotWaves();
                originalSnapshot = SaveState.RoundSnapshot;
                originalLevel = SaveDataUtils.GameData.playerSelectedLv;
                hadItem = SaveDataUtils.ItemData.itemMap.TryGetValue(E_ItemType.GameProp_2, out originalItem);
                hadUseCount = NumbericalStatistics._propUseTimes.TryGetValue(E_ItemType.GameProp_2, out originalUseCount);
                fixtureStarted = true;
                foreach (var bubble in originalBoard)
                {
                    bubble.State = BubbleState.Dead;
                    bubble.gameObject.SetActive(false);
                }
                SaveDataUtils.GameData.playerSelectedLv = Mathf.Max(originalLevel, entry.PropConfigInfo.unlockCondition.unlockLevel);
                SaveDataUtils.ItemData.itemMap[E_ItemType.GameProp_2] = new ItemEntry { Type = E_ItemType.GameProp_2, Count = 5 };
                NumbericalStatistics._propUseTimes[E_ItemType.GameProp_2] = 0;
                entry.Refresh();
                page.Field.SpawnWaveSettled(boardTokens, page.PickedTextures);
                page.Scheduler.RestoreWaves(new[] { pendingTokens });
                await UniTask.DelayFrame(2);
                Check(page.Field.CountBoardBubbles() == cap, "Fixture reaches configured board cap: " + cap);
                int queued = page.Scheduler.CountTotalFragments();
                page.ConsumeNextWave();
                await UniTask.DelayFrame(2);
                Check(page.Scheduler.CountTotalFragments() == queued && page.Field.CountBoardBubbles() == cap,
                    "Automatic drops still stop at the board cap");
                Check(page.GetDropAvailability() == DropAvailability.Ready && page.Tools.CanApplyDrop(),
                    "Drop tool is available at bubble/image limits");
                Check(HitStandard(entry.btn), "Production drop Button is reachable");

                async UniTask ClickAndVerify(string name, bool doubleClick)
                {
                    int beforeBoard = page.Field.CountBoardBubbles();
                    var beforeTokens = page.Scheduler.SnapshotWaves().SelectMany(wave => wave).ToArray();
                    float beforeItems = ItemUtils.GetItemCount(E_ItemType.GameProp_2);
                    PressStandard(entry.btn);
                    if (doubleClick) PressStandard(entry.btn);
                    for (int i = 0; i < 300 && (page.IsToolBusy() || page.Field.IsSpawnInFlight); i++)
                        await UniTask.Delay(20, ignoreTimeScale: true);
                    Check(!page.IsToolBusy() && !page.Field.IsSpawnInFlight, name + " finishes spawning");
                    int dropped = beforeTokens.Length - page.Scheduler.CountTotalFragments();
                    Check(dropped >= Mathf.Min(minimum, beforeTokens.Length) && dropped <= maximum,
                        name + " uses one bounded batch: " + dropped);
                    Check(page.Field.CountBoardBubbles() == beforeBoard + dropped,
                        name + " actually spawns " + beforeBoard + " -> " + page.Field.CountBoardBubbles());
                    Check(ItemUtils.GetItemCount(E_ItemType.GameProp_2) == beforeItems - 1,
                        name + " consumes exactly one tool");
                    Check(page.Scheduler.SnapshotWaves().SelectMany(wave => wave).SequenceEqual(beforeTokens.Skip(dropped)),
                        name + " preserves remaining queue order without duplicates");
                    foreach (string token in beforeTokens.Take(dropped))
                        Check(page.Field.AllBubbles().Count(b => b.Fragment.ToTokenString() == token) == 1,
                            name + " spawned queued token once: " + token);
                }

                await ClickAndVerify("At cap (two rapid clicks)", true);
                queued = page.Scheduler.CountTotalFragments();
                page.ConsumeNextWave();
                await UniTask.DelayFrame(2);
                Check(page.Scheduler.CountTotalFragments() == queued, "Automatic drops also stop above the cap");
                await ClickAndVerify("Already above cap", false);

                var tail = page.Scheduler.SnapshotWaves().SelectMany(wave => wave).Take(2).ToArray();
                Check(tail.Length == 2, "Fixture retains a two-piece tail");
                page.Scheduler.RestoreWaves(new[] { tail });
                await ClickAndVerify("Short final tail", false);
                float left = ItemUtils.GetItemCount(E_ItemType.GameProp_2);
                Check(page.Field.CountBoardBubbles() > 0 && page.GetDropAvailability() == DropAvailability.NoPending,
                    "Visible bubbles are distinguished from an empty pending queue");
                PressStandard(entry.btn);
                Check(ItemUtils.GetItemCount(E_ItemType.GameProp_2) == left, "Empty queue consumes no tool");

                page.Scheduler.RestoreWaves(originalWaves);
                page.Field.BeginSpawnBatch();
                try
                {
                    Check(page.GetDropAvailability() == DropAvailability.Spawning, "In-flight drop remains blocked");
                    PressStandard(entry.btn);
                    Check(ItemUtils.GetItemCount(E_ItemType.GameProp_2) == left, "In-flight rejection consumes no tool");
                }
                finally { page.Field.EndSpawnBatch(); }
                report.AppendLine("RESULT PASS");
            }
            catch (Exception exception)
            {
                report.AppendLine("RESULT FAIL " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                if (fixtureStarted)
                {
                    BizzaGameplayBridge.CancelToolEffect();
                    page.Field.ClearBoard();
                    foreach (var bubble in originalBoard)
                    {
                        bubble.State = BubbleState.Alive;
                        bubble.gameObject.SetActive(true);
                    }
                    page.Scheduler.RestoreWaves(originalWaves);
                    SaveState.RoundSnapshot = originalSnapshot;
                    SaveDataUtils.GameData.playerSelectedLv = originalLevel;
                    if (hadItem) SaveDataUtils.ItemData.itemMap[E_ItemType.GameProp_2] = originalItem;
                    else SaveDataUtils.ItemData.itemMap.Remove(E_ItemType.GameProp_2);
                    if (hadUseCount) NumbericalStatistics._propUseTimes[E_ItemType.GameProp_2] = originalUseCount;
                    else NumbericalStatistics._propUseTimes.Remove(E_ItemType.GameProp_2);
                    entry.Refresh();
                    SaveDataUtils.Save();
                    report.AppendLine("RESTORED original board, queue, selected level and tool inventory");
                }
                UnityEngine.Random.state = randomState;
                File.WriteAllText(file, report.ToString());
            }
        }
    }
}
