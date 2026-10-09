using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace BubblePics.EditorTools
{
    public static class BizzaAndroidBuildInspection
    {
        public static void Inspect(string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "android-build-state.txt"), new[]
            {
                "utc=" + DateTime.UtcNow.ToString("O"),
                "playing=" + EditorApplication.isPlaying,
                "target=" + EditorUserBuildSettings.activeBuildTarget,
                "previousBuild=" + EditorUserBuildSettings.GetBuildLocation(BuildTarget.Android),
                "exportProject=" + EditorUserBuildSettings.exportAsGoogleAndroidProject,
                "appBundle=" + EditorUserBuildSettings.buildAppBundle,
                "addressablesPath=" + Path.GetFullPath(Addressables.BuildPath),
                "settingsPresent=" + File.Exists(Path.Combine(Addressables.BuildPath, "settings.json"))
            });
        }

        public static void BuildContent(string folder)
        {
            RequireAndroid();
            Directory.CreateDirectory(folder);
            string report = Path.Combine(folder, "addressables-build-result.txt");
            File.WriteAllText(report, "RUNNING " + DateTime.UtcNow.ToString("O"));
            AddressableAssetSettings.BuildPlayerContent(out var result);
            string error = result == null ? "Addressables returned no result." : result.Error;
            File.WriteAllText(report, string.IsNullOrEmpty(error) ? "PASS\n" + Addressables.BuildPath : "FAIL\n" + error);
            if (!string.IsNullOrEmpty(error)) throw new BuildFailedException(error);
        }

        public static void VerifyGuard(string folder)
        {
            RequireAndroid();
            string fixtures = Path.Combine(folder, "guard-fixtures-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fixtures);
            string missingCatalog = Path.Combine(fixtures, "missing-catalog");
            Directory.CreateDirectory(missingCatalog);
            File.Copy(Path.Combine(Addressables.BuildPath, "settings.json"), Path.Combine(missingCatalog, "settings.json"));
            AddressablesPlayerBuildGuard.ValidateOutput(Addressables.BuildPath, BuildTarget.Android);
            ExpectRejected(() => AddressablesPlayerBuildGuard.ValidateOutput(fixtures, BuildTarget.Android));
            ExpectRejected(() => AddressablesPlayerBuildGuard.ValidateOutput(missingCatalog, BuildTarget.Android));
            ExpectRejected(() => AddressablesPlayerBuildGuard.ValidateOutput(Addressables.BuildPath, BuildTarget.iOS));
            ExpectRejected(() => AddressablesPlayerBuildGuard.ValidateOutput(Addressables.BuildPath, BuildTarget.Android, "injected content build failure"));
            File.WriteAllText(Path.Combine(folder, "addressables-guard-result.txt"),
                "PASS: valid Android output accepted; missing settings, missing catalog, wrong target and failed build with stale valid output rejected.");
        }

        static void ExpectRejected(Action action)
        {
            try { action(); }
            catch (BuildFailedException) { return; }
            throw new InvalidOperationException("Addressables build guard accepted invalid output.");
        }

        public static void BuildApk(string folder)
        {
            RequireAndroid();
            Directory.CreateDirectory(folder);
            string output = Path.Combine(folder, "Bubblosaic-Android-fixed.apk");
            string reportPath = Path.Combine(folder, "android-build-result.txt");
            File.WriteAllText(reportPath, "RUNNING " + DateTime.UtcNow.ToString("O"));
            bool previousExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            bool previousBundle = EditorUserBuildSettings.buildAppBundle;
            var addressables = AddressableAssetSettingsDefaultObject.Settings;
            var previousContentBuild = addressables.BuildAddressablesWithPlayerBuild;
            try
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                EditorUserBuildSettings.buildAppBundle = false;
                // Always package the current prefab/resource revisions, regardless of
                // an editor's global preference to reuse older Addressables output.
                addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                    target = BuildTarget.Android,
                    locationPathName = output,
                    options = BuildOptions.None
                });
                File.WriteAllLines(reportPath, new[]
                {
                    "result=" + report.summary.result,
                    "errors=" + report.summary.totalErrors,
                    "warnings=" + report.summary.totalWarnings,
                    "duration=" + report.summary.totalTime,
                    "output=" + output
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Android validation build failed. See " + reportPath);
            }
            catch (Exception exception)
            {
                File.AppendAllText(reportPath, "\nFAIL\n" + exception);
                throw;
            }
            finally
            {
                EditorUserBuildSettings.exportAsGoogleAndroidProject = previousExport;
                EditorUserBuildSettings.buildAppBundle = previousBundle;
                addressables.BuildAddressablesWithPlayerBuild = previousContentBuild;
            }
        }

        static void RequireAndroid()
        {
            if (EditorApplication.isPlaying || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Stop Play Mode and select Android before building.");
            if (AddressableAssetSettingsDefaultObject.Settings == null)
                throw new BuildFailedException("Addressables settings are missing.");
        }
    }
}
