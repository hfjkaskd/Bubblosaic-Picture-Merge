using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Explicit visual verification in an isolated preview scene only.
    public static class LevelDeliveryInspection
    {
        public static void VerifyRuntimeCache(string validationRoot)
        {
            if (!EditorApplication.isPlaying || App.I == null || BizzaGameplayBridge.IsInputBlocked)
            {
                File.WriteAllText(Path.Combine(validationRoot, "LevelPrefetch-20260923", "runtime-cache-status.txt"),
                    "WAIT: gameplay entry is not ready; run verification after startup completes.");
                return;
            }
            File.WriteAllText(Path.Combine(validationRoot, "LevelPrefetch-20260923", "runtime-cache-status.txt"), "RUNNING");
            App.I.StartCoroutine(VerifyRuntimeCacheCo(validationRoot));
        }

        static IEnumerator VerifyRuntimeCacheCo(string validationRoot)
        {
            var app = App.I;
            var delivery = app.RemoteImages;
            var report = new StringBuilder();
            string output = Path.Combine(validationRoot, "LevelPrefetch-20260923", "runtime-cache-results.txt");
            bool passed = false;
            try
            {
                Check(app.ActiveLoading == null, "Cached startup created a loading popup.");
                report.AppendLine("PASS cached startup never instantiated the loading popup");

                // Exercise the real three-level downloader, beyond the bundled
                // seed, without changing player progression or deleting cache.
                int current = delivery.Config.bundledSeedItemCount;
                Check(delivery.Config.upcomingItemCount == 3, "Upcoming window must be three levels.");
                var assets = new List<RemoteImageDelivery.RemoteImageAsset>();
                for (int level = current + 1; level <= current + 3; level++)
                    assets.AddRange(delivery.GetAssetsForItem(level));
                Check(assets.Count > 0, "No remote assets in the validation window.");
                var before = delivery.Client.GetSnapshot();
                delivery.NotifyForegroundItemReady(current);
                float deadline = Time.realtimeSinceStartup + 120f;
                while (true)
                {
                    int cached = 0;
                    foreach (var asset in assets)
                        if (delivery.Client.HasCached(asset)) cached++;
                    if (cached == assets.Count) break;
                    Check(Time.realtimeSinceStartup < deadline, "Preload timed out: " + cached + "/" + assets.Count);
                    Check(app.ActiveLoading == null, "Background preloading displayed the popup.");
                    yield return null;
                }
                report.AppendLine("PASS background preload levels=" + (current + 1) + ".." + (current + 3) +
                    " cached=" + assets.Count + "/" + assets.Count + " popup=false");

                var ready = delivery.Client.GetSnapshot();
                for (int level = current + 1; level <= current + 3; level++)
                {
                    Check(LevelRepo.TryGet(level, out var data), "Missing catalog level " + level);
                    Check(!LevelImageLoader.RequiresNetwork(data), "Prefetched level still requires network: " + level);
                    Texture2D[] loaded = null;
                    string error = null;
                    int notices = 0;
                    yield return LevelImageLoader.LoadForGameplay(data,
                        value => loaded = value, value => error = value, null,
                        () => { notices++; app.ShowReconnecting(); });
                    Check(loaded != null && error == null && notices == 0,
                        "Cached foreground load was not silent: " + level + " " + error);
                    report.AppendLine("PASS foreground level=" + level + " textures=" + loaded.Length + " reconnects=0");
                }
                var after = delivery.Client.GetSnapshot();
                report.AppendLine("delivery prefetch network=" + (ready.SessionNetworkSuccesses - before.SessionNetworkSuccesses) +
                    " foreground diskHits=" + (after.SessionDiskHits - ready.SessionDiskHits) +
                    " memoryHits=" + (after.SessionMemoryHits - ready.SessionMemoryHits) +
                    " network=" + (after.SessionNetworkSuccesses - ready.SessionNetworkSuccesses));
                Check(after.SessionNetworkSuccesses == ready.SessionNetworkSuccesses,
                    "Cached foreground loads downloaded resources again.");

                // The normal bridge also serves category stages. Observe it
                // frame by frame so a one-frame popup cannot pass unnoticed.
                int originalLevel = app.Page.CurrentLevelNumber;
                var reload = BizzaGameplayBridge.ReloadLevelAsync().GetAwaiter();
                deadline = Time.realtimeSinceStartup + 45f;
                while (!reload.IsCompleted)
                {
                    Check(app.ActiveLoading == null, "Cached bridge reload flashed the popup.");
                    Check(Time.realtimeSinceStartup < deadline, "Cached bridge reload timed out.");
                    yield return null;
                }
                reload.GetResult();
                Check(app.ActiveLoading == null && app.Page.CurrentLevelNumber == originalLevel &&
                    app.Page.Input.InputEnabled, "Cached bridge did not finish silently.");
                report.AppendLine("PASS cached bridge reload level=" + originalLevel + " popupNeverCreated=true inputEnabled=true");

                var overlay = app.ShowReconnecting();
                overlay.SetProgress(0.6f);
                Check(overlay.IsVisible, "Missing-resource overlay is not visible.");
                var labels = overlay.GetComponentsInChildren<TMP_Text>(true);
                Check(Array.Exists(labels, label => label.text == Localization.Tr("LEVEL_RECONNECTING")),
                    "Missing-resource message is incorrect.");
                app.ShowReconnecting();
                Check(Array.Exists(labels, label => label.text == "60%"), "Retry reset the loading progress.");
                app.HideLoading();
                Check(!overlay.IsVisible, "Recovery did not hide the popup.");
                app.ShowReconnecting();
                Check(overlay.IsVisible && Array.Exists(labels, label => label.text == "0%"),
                    "A later missing-resource request cannot reopen/reset the popup.");
                app.HideLoading();
                report.AppendLine("PASS reconnect popup message, retry progress, hide and later reopen");

                reload = BizzaGameplayBridge.ReloadLevelAsync().GetAwaiter();
                deadline = Time.realtimeSinceStartup + 45f;
                while (!reload.IsCompleted)
                {
                    Check(!overlay.IsVisible, "Cached reload redisplayed the old reconnect popup.");
                    Check(Time.realtimeSinceStartup < deadline, "Second cached reload timed out.");
                    yield return null;
                }
                reload.GetResult();
                Check(!overlay.IsVisible && app.Page.Input.InputEnabled,
                    "Cached reload after reconnect did not finish silently.");
                report.AppendLine("PASS cached reload after reconnect keeps the old popup hidden");
                passed = true;
            }
            finally
            {
                if (!passed) report.AppendLine("FAIL runtime verification interrupted; see Editor log");
                File.WriteAllText(output, report.ToString());
                File.WriteAllText(Path.Combine(validationRoot, "LevelPrefetch-20260923", "runtime-cache-status.txt"),
                    passed ? "PASS" : "FAIL");
                if (app != null)
                {
                    app.HideLoading();
                    delivery.NotifyForegroundItemReady(app.Page.CurrentLevelNumber);
                }
            }
        }

        public static void VerifyCatalogSelection(string validationRoot)
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play Mode before catalog verification.");
            var report = new StringBuilder();
            GameModes.ModeCatalogRepository.Reload();
            if (ResourceAssetLoader.Load<TextAsset>(GameModes.ModeCatalogRepository.BonusLevelsPath) == null)
            {
                Check(GameModes.ModeCatalogRepository.BonusLevels.Count == 0,
                    "Missing optional Bonus catalog must produce no content.");
                report.AppendLine("PASS missing optional Bonus catalog is safe");
            }

            GameModes.BuiltinModeSelectors.EnsureRegistered();
            var sentinel = new BonusSelectionProbe();
            GameModes.LevelModeRouter.RegisterSelector(sentinel);
            try
            {
                foreach (int level in new[] { 1, 2, 3, 4, 7, 17, 26, 27, 28 })
                {
                    var assets = GameModes.ModeLevelPreparer.GetUpcomingCategoryAssets(level);
                    Check(sentinel.Calls == 0, "Category prefetch queried the Bonus selector.");
                    if (level == 7 || level == 17 || level == 27)
                        Check(assets.Count > 0, "Category assets were not prefetched: " + level);
                    report.AppendLine("PASS category prefetch level=" + level + " assets=" + assets.Count);
                }
                Check(GameModes.LevelModeRouter.TryResolveAutomatic(7, out var selected) &&
                    selected.Kind == GameModes.GameplayKind.Bonus && sentinel.Calls == 1 &&
                    selected.IsAutomatic, "Automatic selector priority changed.");
                report.AppendLine("PASS automatic routing retains selector priority");
            }
            finally
            {
                GameModes.LevelModeRouter.UnregisterSelector(sentinel);
            }
            string output = Path.Combine(validationRoot, "LevelPrefetch-20260923");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "catalog-regression-results.txt"), report.ToString());
        }

        sealed class BonusSelectionProbe : GameModes.ILevelModeSelector
        {
            public GameModes.GameplayKind Kind => GameModes.GameplayKind.Bonus;
            public int Priority => int.MaxValue;
            public int Calls;

            public bool TrySelect(int globalLevel, bool automatic, out GameModes.LevelModeSelection selection)
            {
                Calls++;
                selection = new GameModes.LevelModeSelection { Kind = Kind };
                return true;
            }
        }

        public static void Run(string validationRoot)
        {
            string output = Path.Combine(validationRoot, "LevelPrefetch-20260923");
            Directory.CreateDirectory(output);
            var report = new StringBuilder();
            string[] locales = { "zh_CN", "en", "pt_BR" };
            foreach (string locale in locales)
            {
                var text = SpineLite.MiniJson.Parse(Resources.Load<TextAsset>(
                    "Localization/" + locale).text) as System.Collections.Generic.Dictionary<string, object>;
                foreach (int width in new[] { 1080, 810 })
                    Render(output, locale, (string)text["LEVEL_RECONNECTING"], width, report);
            }
            File.WriteAllText(Path.Combine(output, "visual-results.txt"), report.ToString());
        }

        static void Render(string output, string locale, string message, int width, StringBuilder report)
        {
            const int height = 1920;
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D pixels = null;
            Camera camera = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/BubblePics/RuntimePrefabs/Pages/LoadingOverlay.prefab");
                var root = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = root.GetComponent<Canvas>();
                Check(canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay,
                    "Overlay must be independent of the hidden gameplay camera.");
                Check(root.GetComponent<GraphicRaycaster>() != null, "Missing input blocker.");
                var overlay = root.GetComponent<LoadingOverlay>();
                overlay.Show();
                overlay.SetReconnecting();
                overlay.SetProgress(0.35f);
                TMP_Text status = null, progress = null;
                foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.name == "StatusLabel") status = label;
                    if (label.name == "ProgressLabel") progress = label;
                }
                Check(status != null && progress != null, "Missing prefab label bindings.");
                Check(progress.text == "35%", "Progress binding not updated.");
                Check(status.text == Localization.Tr("LEVEL_RECONNECTING"), "Reconnect binding not updated.");
                overlay.SetProgress(0.1f);
                Check(progress.text == "35%", "Progress regressed on retry.");
                status.text = message;

                var cameraGo = new GameObject("Validation camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraGo, scene);
                camera = cameraGo.GetComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = height / 2f;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.015f, 0.26f, 0.56f);
                camera.allowHDR = false;
                target = new RenderTexture(width, height, 24);
                target.Create();
                camera.targetTexture = target;
                // Render the authored overlay into a standalone QA image.
                root.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                float scale = width / 1080f;
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(1080, height / scale);
                rect.position = Vector3.zero;
                rect.localScale = Vector3.one * scale;
                Canvas.ForceUpdateCanvases();
                foreach (TMP_Text label in new[] { status, progress })
                {
                    label.ForceMeshUpdate(true, true);
                    Check(!label.isTextOverflowing, "Text overflow: " + locale + " " + label.name);
                    Check(label.textInfo.characterCount > 0, "No rendered text: " + label.name);
                }
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;
                string name = "reconnect-" + locale + "-" + width;
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
                overlay.Hide();
                Check(!status.gameObject.activeInHierarchy, "Overlay did not close.");
                report.AppendLine("PASS " + name + "; text=" + message + "; lines=" + status.textInfo.lineCount);
            }
            finally
            {
                if (camera != null) camera.targetTexture = null;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void Check(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
        }
    }
}
