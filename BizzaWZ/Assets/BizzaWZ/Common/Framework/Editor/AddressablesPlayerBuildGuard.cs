using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;

/// <summary>Rejects a player build when its required Addressables content failed or is missing.</summary>
public sealed class AddressablesPlayerBuildGuard : BuildPlayerProcessor
{
    private const string RuntimePathToken = "{UnityEngine.AddressableAssets.Addressables.RuntimePath}";
    private static bool requiresAddressables;
    private static bool expectsContentBuild;
    private static bool contentBuildCompleted;
    private static string contentBuildError;

    // AddressablesPlayerBuildProcessor builds and stages content at order 1.
    public override int callbackOrder => 2;

    public static void BeginBuild(BuildTarget target)
    {
        BuildScript.buildCompleted -= RecordContentBuild;
        contentBuildError = null;
        contentBuildCompleted = false;
        expectsContentBuild = false;
        requiresAddressables = UsesAddressables(target);
        if (!requiresAddressables) return;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            throw Failure("Addressables settings are missing for this player build.");

        expectsContentBuild = settings.BuildAddressablesWithPlayerBuild == AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer ||
            (settings.BuildAddressablesWithPlayerBuild == AddressableAssetSettings.PlayerBuildOption.PreferencesValue &&
             EditorPrefs.GetBool("Addressables.BuildAddressablesWithPlayerBuild", true));
        BuildScript.buildCompleted += RecordContentBuild;
    }

    public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
    {
        ValidateCurrentBuild(buildPlayerContext.BuildPlayerOptions.target);
    }

    public static void ValidateCurrentBuild(BuildTarget target)
    {
        BuildScript.buildCompleted -= RecordContentBuild;
        if (!requiresAddressables) return;
        if (expectsContentBuild && !contentBuildCompleted)
            throw Failure("No Addressables player-content build result was received; stale output cannot validate this build.");
        ValidateOutput(Addressables.BuildPath, target, contentBuildError);
    }

    private static void RecordContentBuild(AddressableAssetBuildResult result)
    {
        if (!(result is AddressablesPlayerBuildResult)) return;
        contentBuildCompleted = true;
        if (!string.IsNullOrEmpty(result.Error) && string.IsNullOrEmpty(contentBuildError))
            contentBuildError = result.Error;
    }

    private static bool UsesAddressables(BuildTarget target)
    {
        // Match AssetUtils and UIPageWhiteBuildResourcesProcessor: white builds
        // load staged Resources prefabs and do not require an Addressables catalog.
        string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildPipeline.GetBuildTargetGroup(target));
        foreach (string symbol in symbols.Split(';'))
            if (string.Equals(symbol.Trim(), "BIZZA_REAL_WITHDRAW", StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>Validates an output directory without starting or changing a build.</summary>
    public static void ValidateOutput(string buildPath, BuildTarget target, string buildError = null)
    {
        // Check the current result first: files left by an older build must not
        // turn a failed content build into an apparently successful player build.
        if (!string.IsNullOrEmpty(buildError))
            throw Failure("Addressables content build failed: " + buildError);

        string root = Path.GetFullPath(buildPath);
        string settingsPath = Path.Combine(root, "settings.json");
        RequireNonEmptyFile(settingsPath);

        ResourceManagerRuntimeData runtimeData;
        try
        {
            runtimeData = JsonUtility.FromJson<ResourceManagerRuntimeData>(File.ReadAllText(settingsPath));
        }
        catch (Exception exception)
        {
            throw Failure("Cannot read Addressables runtime settings: " + settingsPath + "\n" + exception.Message);
        }

        string targetName = BuildPipeline.GetBuildTargetName(target);
        if (runtimeData == null || !string.Equals(runtimeData.BuildTarget, targetName, StringComparison.Ordinal))
            throw Failure("Addressables runtime settings do not match build target " + targetName + ": " + settingsPath);

        int localCatalogs = 0;
        if (runtimeData.CatalogLocations != null)
        {
            foreach (var location in runtimeData.CatalogLocations)
            {
                // CatalogLocations also contains hash dependencies. Only the
                // main catalog locations represent required catalog files.
                if (location == null || location.Keys == null ||
                    Array.IndexOf(location.Keys, ResourceManagerRuntimeData.kCatalogAddress) < 0)
                    continue;

                string catalogPath = ResolveLocalCatalogPath(root, location.InternalId);
                if (catalogPath == null) continue;
                string extension = Path.GetExtension(catalogPath);
                if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".bin", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".bundle", StringComparison.OrdinalIgnoreCase))
                    throw Failure("Unsupported local Addressables catalog: " + catalogPath);
                RequireNonEmptyFile(catalogPath);
                localCatalogs++;
            }
        }

        if (localCatalogs == 0)
            throw Failure("Addressables runtime settings contain no packaged local catalog: " + settingsPath);
    }

    private static string ResolveLocalCatalogPath(string root, string internalId)
    {
        if (string.IsNullOrWhiteSpace(internalId))
            throw Failure("Addressables catalog has an empty internal ID.");

        string path = internalId.Replace(RuntimePathToken, root);
        if (path.IndexOf('{') >= 0 || path.IndexOf('}') >= 0)
            throw Failure("Cannot resolve Addressables catalog path: " + internalId);

        if (!Path.IsPathRooted(path) && Uri.TryCreate(path, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) return null;
            if (!uri.IsFile) throw Failure("Unsupported Addressables catalog path: " + internalId);
            path = uri.LocalPath;
        }

        path = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
        string boundary = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
            throw Failure("Local catalog is outside the packaged Addressables directory: " + internalId);
        return path;
    }

    private static void RequireNonEmptyFile(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw Failure("Required Addressables build output is missing or empty: " + path);
    }

    private static BuildFailedException Failure(string message)
    {
        return new BuildFailedException("[AddressablesPlayerBuildGuard] " + message);
    }
}

/// <summary>Starts result collection before Unity's Addressables player processor.</summary>
public sealed class AddressablesPlayerBuildGuardBegin : BuildPlayerProcessor
{
    public override int callbackOrder => 0;

    public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
    {
        AddressablesPlayerBuildGuard.BeginBuild(buildPlayerContext.BuildPlayerOptions.target);
    }
}
