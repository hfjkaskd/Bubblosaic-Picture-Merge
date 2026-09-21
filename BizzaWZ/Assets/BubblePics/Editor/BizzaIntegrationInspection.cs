using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BubblePics.EditorTools
{
    // Inspection and editor control only. Never changes runtime initialization or saves.
    [InitializeOnLoad]
    public static class BizzaIntegrationInspection
    {
        [Serializable] private sealed class Command { public string operation; public string path; }
        private static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation"));
        private static double nextPoll;

        static BizzaIntegrationInspection() { EditorApplication.update += Poll; }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            string commandPath = Path.Combine(Folder, "command.json");
            if (!File.Exists(commandPath)) return;
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(commandPath));
            File.Delete(commandPath);
            try
            {
                switch (command.operation)
                {
                    case "refresh":
                        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before refreshing assets.");
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        break;
                    case "configure": BizzaPrefabIntegration.Configure(); break;
                    case "stop": EditorApplication.isPlaying = false; break;
                    case "play": EditorApplication.isPlaying = true; break;
                    case "open":
                        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before opening a scene.");
                        EditorSceneManager.OpenScene(command.path);
                        break;
                    case "capture":
                        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Capture requires Play Mode.");
                        ScreenCapture.CaptureScreenshot(Path.Combine(Folder, command.path));
                        break;
                    case "inspect": Inspect(); break;
                    case "reward-font-preview": BizzaRewardFontInspection.Render(Folder); break;
                    case "configure-ad-cadence": BizzaDifficultyInspection.ConfigureAds(Folder); break;
                    case "verify-difficulty": BizzaDifficultyInspection.Verify(Folder); break;
                    case "verify-reward-timing": VerifyRewardTiming(); break;
                    case "merge-partial": MergePartial(); break;
                    default: throw new InvalidOperationException("Unknown editor inspection command.");
                }
                File.WriteAllText(Path.Combine(Folder, "command-result.txt"), command.operation + " accepted " + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception exception)
            {
                File.WriteAllText(Path.Combine(Folder, "command-result.txt"), exception.ToString());
                Debug.LogException(exception);
            }
        }

        private static void MergePartial()
        {
            var page = BizzaGameplayBridge.Page;
            if (!EditorApplication.isPlaying || page == null || BizzaGameplayBridge.IsInputBlocked || page.IsInteractionLocked())
                throw new InvalidOperationException("Gameplay must be ready for a partial merge.");
            var bubbles = page.Field.AllBubbles().ToArray();
            foreach (var source in bubbles)
            foreach (var target in bubbles)
            {
                if (source == target || !source.CanMergeWith(target) || page.Merge.HasTargetConflict(source, target)) continue;
                var result = BubbleView.MergeResult(target.Fragment, source.Fragment.HeldPaths);
                if (result.Count == 1 && result[0].Count == 0) continue;
                File.AppendAllText(Path.Combine(Folder, "partial-merge-validation.txt"),
                    $"Partial successful merge requested; collectedBefore={page.CollectedImgs.Count}; stepsBefore={page.StepsLeft}; runtime={Time.realtimeSinceStartupAsDouble}\n");
                // Same gameplay calls as InputController.CommitMerge; no fabricated pointer or reward result.
                page.EmitMergeAttempted(true);
                source.ClearDragFlag();
                page.StartCoroutine(page.Merge.Run(source, target));
                return;
            }
            throw new InvalidOperationException("No available partial merge on current board.");
        }

        private static void VerifyRewardTiming()
        {
            var gate = new RewardPopupTiming.Gate(RewardPopupTiming.IntervalSeconds);
            void Check(bool value) { if (!value) throw new InvalidOperationException("Reward timing assertion failed."); }
            gate.Begin(100);
            Check(!gate.TryReserve(124.999));
            Check(gate.TryReserve(125));
            Check(!gate.TryReserve(125));
            gate.Shown(127);
            gate.Begin(140); // A new round must not restart the existing timer.
            Check(!gate.TryReserve(151.999));
            Check(gate.TryReserve(152));
            gate.Shown(160); // Settlement resets the same timer.
            Check(!gate.TryReserve(184.999));
            Check(gate.TryReserve(190)); // Idle time does not itself open a page.
            File.WriteAllText(Path.Combine(Folder, "reward-timing-verification.txt"),
                "PASS: configured 25-second interval; before boundary blocked; at boundary allowed; duplicate merge blocked; actual panel open resets; new round preserves timer; settlement resets; late merge allowed. Pure timing verification, no reward payout or ad simulation.");
        }

        private static void Inspect()
        {
            var page = App.I != null ? App.I.Page : null;
            var lines = new System.Collections.Generic.List<string>
            {
                "playing=" + EditorApplication.isPlaying,
                "resolution=" + Screen.width + "x" + Screen.height,
                "scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                "defines=" + PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup),
                "app=" + (App.I != null), "page=" + (page != null)
            };
            lines.Add("frameDelta=" + Time.unscaledDeltaTime + " targetFps=" + Application.targetFrameRate);
            if (page != null)
            {
                lines.Add("level=" + page.CurrentLevelNumber + " round=" + page.RoundSeq + " steps=" + page.StepsLeft + " remaining=" + page.CountRemainingBubbles());
                lines.Add("tutorial=" + SaveState.TutorialDone + " dead=" + page.IsDead() + " won=" + page.IsWon() + " locked=" + page.IsInteractionLocked());
                lines.Add("props hint=" + SaveState.GetToolCount("hint") + " drop=" + SaveState.GetToolCount("drop") + " magnet=" + SaveState.GetToolCount("magnet"));
                lines.Add("snapshotLength=" + SaveState.RoundSnapshot.Length);
                Canvas hudCanvas = App.I.HudRoot.GetComponentInParent<Canvas>().rootCanvas;
                Camera hudCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
                float maxFlightProjectionError = 0f;
                foreach (var bubble in page.Field.AllBubbles())
                {
                    Vector2 expected = App.I.Cam.WorldToScreenPoint(bubble.transform.position);
                    Vector3 hudPosition = ImageFlyAnimator.WorldToHudPosition(bubble.transform.position);
                    Vector2 actual = RectTransformUtility.WorldToScreenPoint(hudCamera, hudPosition);
                    maxFlightProjectionError = Mathf.Max(maxFlightProjectionError, Vector2.Distance(expected, actual));
                }
                lines.Add("flightProjectionMaxPixelError=" + maxFlightProjectionError);
                foreach (var entry in SaveDataUtils.GameData.bubbleGameplayValues)
                    if (entry.key.StartsWith("tool_", StringComparison.Ordinal)) lines.Add("gameplayFlag=" + entry.key + "=" + entry.value);
                lines.Add("inputEnabled=" + page.Input.InputEnabled + " bridgeBlocked=" + BizzaGameplayBridge.IsInputBlocked + " transparent=" + TransparentBlock.IsBlock + " loading=" + LoadingBlock.IsBlock);
                foreach (var b in page.Field.AllBubbles())
                    lines.Add("bubble=" + b.name + " world=" + b.transform.position + " screen=" + App.I.Cam.WorldToScreenPoint(b.transform.position));
            }
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Camera>()) lines.Add("camera=" + c.name + " enabled=" + c.enabled + " depth=" + c.depth);
            foreach (var b in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>()) lines.Add("button=" + b.name + " interactable=" + b.IsInteractable());
            foreach (var t in UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()) if (t.gameObject.activeInHierarchy)
                lines.Add("text=" + t.name + " | " + t.text + " | shader=" + (t.fontSharedMaterial != null ? t.fontSharedMaterial.shader.name : "missing"));
            File.WriteAllLines(Path.Combine(Folder, "runtime-state.txt"), lines);
        }
    }
}
