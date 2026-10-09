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
                    case "portrait": Screen.SetResolution(1080, 2340, false); break;
                    case "reward-view-size":
                        var dimensions = command.path.Split('x');
                        PlayModeWindow.SetCustomRenderingResolution(uint.Parse(dimensions[0]), uint.Parse(dimensions[1]), "Reward display review");
                        break;
                    case "fix-reward-display": RewardDisplayRepair.Apply(); break;
#if BIZZA_REAL_WITHDRAW
                    case "fix-country-currency": CountryCurrencyAuthoring.Apply(); break;
                    case "verify-country-currency": CountryCurrencyInspection.Run(); break;
#endif
                    case "fix-withdrawal-display": WithdrawalDisplayRepair.Apply(); break;
                    case "fix-withdraw-level-progress": WithdrawLevelProgressAuthoring.Apply(); break;
                    case "review-withdraw-level-progress": AllUiAuthoring.ReviewWithdrawLevelProgress(command.path); break;
                    case "review-lose-title": AllUiAuthoring.ReviewLoseTitle(command.path); break;
                    case "remove-reward-backdrop": RewardBackdropAuthoring.Apply(); break;
                    case "rebuild-reward-structure": RewardStructureAuthoring.Apply(); break;
                    case "apply-generated-reward-art": RewardGeneratedArtAuthoring.Apply(); break;
                    case "apply-loading-brand": LoadingBrandAuthoring.Apply(); break;
                    case "fix-withdrawal-guide": WithdrawalGuideAuthoring.Apply(); break;
                    case "review-withdrawal-guide": AllUiAuthoring.ReviewWithdrawalGuide(command.path); break;
                    case "preview-withdrawal-guide": WithdrawalGuidePreview.Render(command.path); break;
                    case "fix-withdrawal-service": WithdrawalServiceButtonAuthoring.Apply(); break;
                    case "preview-withdrawal-service": AllUiAuthoring.PreviewWithdrawalService(command.path); break;
                    case "fix-page-aspect": PageAspectAuthoring.Apply(); break;
                    case "audit-page-aspect": PageAspectAuthoring.Audit(); break;
                    case "open":
                        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before opening a scene.");
                        EditorSceneManager.OpenScene(command.path);
                        break;
                    case "capture":
                        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Capture requires Play Mode.");
                        ScreenCapture.CaptureScreenshot(Path.Combine(Folder, command.path));
                        break;
                    case "inspect": Inspect(); break;
                    case "verify-combo-layer": ComboLayerInspection.Run(command.path); break;
                    case "fix-combo-layer": ComboLayerAuthoring.Apply(); break;
                    case "fix-footer-edges": ReleaseArtPacking.RepairFooterEdges(); break;
                    case "review-footer-edges": AllUiAuthoring.ReviewFooterEdges(command.path); break;
                    case "fix-settings-edges": ReleaseArtPacking.RepairSettingsEdges(); break;
                    case "review-settings-edges": AllUiAuthoring.ReviewSettingsEdges(command.path); break;
                    case "review-collection-popup-layer": AllUiAuthoring.ReviewCollectionPopupLayer(command.path); break;
                    case "review-reward-modal-input": AllUiAuthoring.ReviewRewardModalInput(command.path); break;
                    case "fix-slot-reward-layout": SlotRewardLayoutAuthoring.Apply(); break;
                    case "review-slot-reward-layout": AllUiAuthoring.ReviewSlotRewardLayout(command.path); break;
                    case "reward-font-preview": BizzaRewardFontInspection.Render(Folder); break;
                    case "configure-ad-cadence": BizzaDifficultyInspection.ConfigureAds(Folder); break;
                    case "verify-difficulty": BizzaDifficultyInspection.Verify(Folder); break;
                    case "verify-reward-timing": VerifyRewardTiming(); break;
                    case "merge-partial": MergePartial(); break;
                    case "verify-puzzle-reward-partial": BizzaPuzzleRewardInspection.Run(Folder, false); break;
                    case "verify-puzzle-reward-closure": BizzaPuzzleRewardInspection.Run(Folder, true); break;
                    case "verify-hud-layout": BizzaHudLayoutInspection.Run(Folder); break;
                    case "preview-header-layout": BizzaHeaderLayoutInspection.Render(Folder); break;
                    case "preview-lose-dialog": BizzaDialogLayoutInspection.RenderLosePanel(Folder); break;
                    case "open-settings": BizzaSettingsInspection.Open(Folder); break;
                    case "click-settings": BizzaSettingsInspection.Click(Folder, command.path); break;
                    case "inspect-settings": BizzaSettingsInspection.Inspect(Folder); break;
                    case "inspect-android-build": BizzaAndroidBuildInspection.Inspect(Path.Combine(Folder, "EmulatorBlueScreen-20260923")); break;
                    case "optimize-apk-fonts": ApkAssetOptimization.ApplyFonts(); break;
                    case "optimize-apk-textures": ApkAssetOptimization.ApplyTextures(); break;
                    case "clean-release-fonts": ReleaseFontCleanup.Apply(); break;
                    case "finalize-hud-materials": ReleaseArtPacking.FinalizeHudMaterials(); break;
                    case "archive-release-art": ReleaseArtPacking.ArchiveSources(); break;
                    case "audit-release-fonts": ReleaseFontCleanup.Audit(); break;
                    case "audit-release-art": ReleaseArtPacking.Audit(); break;
                    case "prepare-release-art": ReleaseArtPacking.PrepareBakeShaders(); break;
                    case "bake-release-art": ReleaseArtPacking.Bake(command.path); break;
                    case "build-addressables": BizzaAndroidBuildInspection.BuildContent(Path.Combine(Folder, "EmulatorBlueScreen-20260923")); break;
                    case "verify-addressables-guard": BizzaAndroidBuildInspection.VerifyGuard(Path.Combine(Folder, "EmulatorBlueScreen-20260923")); break;
                    case "build-android-apk": BizzaAndroidBuildInspection.BuildApk(string.IsNullOrEmpty(command.path) ? Path.Combine(Folder, "EmulatorBlueScreen-20260923") : command.path); break;
                    case "verify-level-delivery": LevelDeliveryInspection.Run(Folder); break;
                    case "verify-level-catalogs": LevelDeliveryInspection.VerifyCatalogSelection(Folder); break;
                    case "verify-level-cache": LevelDeliveryInspection.VerifyRuntimeCache(Folder); break;
                    case "verify-level-reuse-drop": LevelReuseDropInspection.Run(command.path); break;
                    case "review-drop-tool-capacity": AllUiAuthoring.ReviewDropToolCapacity(command.path); break;
#if BIZZA_REAL_WITHDRAW
                    case "open-reward-input": BizzaRewardInputInspection.Open(); break;
                    case "inspect-reward-input": BizzaRewardInputInspection.Inspect(Folder); break;
                    case "open-withdraw-dan": BizzaWithdrawDanInspection.Open(Path.Combine(Folder, "WithdrawDanRounded-20260922")); break;
                    case "inspect-withdraw-dan": BizzaWithdrawDanInspection.Inspect(Path.Combine(Folder, "WithdrawDanRounded-20260922")); break;
                    case "capture-withdraw-dan": BizzaWithdrawDanInspection.CaptureScreenshot(Path.Combine(Folder, "WithdrawDanRounded-20260922")); break;
                    case "verify-withdraw-dan-fills": BizzaWithdrawDanInspection.CaptureFillVariants(Path.Combine(Folder, "WithdrawDanRounded-20260922")); break;
#endif
                    case "export-hud-layout-validation": ExportHudLayoutValidation(); break;
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
            Check(!gate.TryReserve(129.999));
            Check(gate.TryReserve(130));
            Check(!gate.TryReserve(130));
            gate.Shown(132);
            gate.Begin(145); // A new round must not restart the existing timer.
            Check(!gate.TryReserve(161.999));
            Check(gate.TryReserve(162));
            gate.Shown(170); // Settlement resets the same timer.
            Check(!gate.TryReserve(199.999));
            Check(gate.TryReserve(200)); // Idle time does not itself open a page.
            File.WriteAllText(Path.Combine(Folder, "reward-timing-verification.txt"),
                "PASS: configured 30-second interval; before boundary blocked; at boundary allowed; duplicate merge blocked; actual panel open resets; new round preserves timer; settlement resets; late merge allowed. Pure timing verification, no reward payout or ad simulation.");
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
            if (EditorApplication.isPlaying && UIModule.Instance != null)
            {
                RectTransform rewardLayer = UIModule.Instance.RewardItemLayer;
                Canvas rewardCanvas = rewardLayer != null ? rewardLayer.GetComponentInParent<Canvas>() : null;
                lines.Add("rewardLayer=" + (rewardLayer != null ? rewardLayer.name : "missing") +
                    " sortingCanvas=" + (rewardCanvas != null ? rewardCanvas.name : "missing") +
                    " sortingOrder=" + (rewardCanvas != null ? rewardCanvas.sortingOrder : -1) +
                    " overrideSorting=" + (rewardCanvas != null && rewardCanvas.overrideSorting));
            }
            if (page != null)
            {
                lines.Add("level=" + page.CurrentLevelNumber + " round=" + page.RoundSeq + " steps=" + page.StepsLeft + " remaining=" + page.CountRemainingBubbles());
                lines.Add("deviceLayout=" + DeviceLayout.ViewWidth + "x" + DeviceLayout.ViewHeight +
                    " pixelsPerDesign=" + DeviceLayout.PixelsPerDesignUnit + " safeTop=" + DeviceLayout.SafeTopDesign +
                    " safeBottom=" + DeviceLayout.SafeBottomDesign);
                DescribeRect(lines, App.I.HudRoot);
                DescribeRect(lines, page.TopBar.Root);
                DescribeRect(lines, page.TopBar.TargetCard);
                DescribeRect(lines, page.Toolbar.Root);
                DescribeRect(lines, page.Toolbar.PropRoot);
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

        private static void DescribeRect(System.Collections.Generic.List<string> lines, RectTransform rect)
        {
            if (rect == null) return;
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            lines.Add("layout=" + rect.name + " parent=" + rect.parent.name + " rect=" + rect.rect +
                " anchor=" + rect.anchoredPosition + " pivot=" + rect.pivot + " scale=" + rect.localScale +
                " canvasScale=" + canvas.scaleFactor + " screenBottomLeft=" + RectTransformUtility.WorldToScreenPoint(camera, corners[0]) +
                " screenTopRight=" + RectTransformUtility.WorldToScreenPoint(camera, corners[2]));
        }

        private static void ExportHudLayoutValidation()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before exporting.");
            string output = Path.Combine(Folder, "HudLayout-20260922", "Android");
            bool previousExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    target = BuildTarget.Android, locationPathName = output, options = BuildOptions.None
                });
                File.WriteAllText(Path.Combine(Folder, "hud-layout-build-result.txt"),
                    report.summary.result + " errors=" + report.summary.totalErrors + " output=" + output);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("HUD layout validation export failed.");
            }
            finally { EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExport; }
        }
    }
}
