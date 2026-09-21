using System;
using System.Collections.Generic;
using System.IO;
using Spine.Unity;
using UnityEngine;

namespace BubblePics.SpineLite
{
    /// <summary>
    /// Read-only compatibility view over an official Spine.SkeletonData.
    /// Existing BubblePics gameplay code keeps its stable API while all
    /// parsing, constraints, animation state and mesh generation are provided
    /// by the official spine-csharp/spine-unity 4.1 runtimes.
    /// </summary>
    public sealed class SkeletonData
    {
        internal SkeletonData(Spine.SkeletonData runtimeData)
        {
            RuntimeData = runtimeData;
            var animations = new Dictionary<string, SpineAnimation>(
                StringComparer.Ordinal);
            if (runtimeData != null)
            {
                foreach (Spine.Animation animation in runtimeData.Animations)
                    animations[animation.Name] = new SpineAnimation(animation);
            }
            Animations = animations;
        }

        internal Spine.SkeletonData RuntimeData { get; }

        public IReadOnlyDictionary<string, SpineAnimation> Animations { get; }

        public static SkeletonData Load(string module)
        {
            OfficialSpineModule loaded = OfficialSpineAssets.Load(module);
            return loaded?.CompatibilityData;
        }
    }

    public sealed class SpineAnimation
    {
        internal SpineAnimation(Spine.Animation runtimeAnimation)
        {
            RuntimeAnimation = runtimeAnimation;
        }

        internal Spine.Animation RuntimeAnimation { get; }

        public string Name => RuntimeAnimation?.Name ?? string.Empty;

        public float Duration => RuntimeAnimation?.Duration ?? 0f;
    }

    internal sealed class OfficialSpineModule
    {
        public SkeletonDataAsset Asset;
        public SkeletonData CompatibilityData;
    }

    internal static class OfficialSpineAssets
    {
        const string ResourceRoot = "SpineOfficial";
        const string TextureRoot = "Art/SpinePages";

        static readonly Dictionary<string, OfficialSpineModule> Cache =
            new Dictionary<string, OfficialSpineModule>(StringComparer.Ordinal);

        public static OfficialSpineModule Load(
            string module,
            SkeletonDataAsset persistentAsset = null)
        {
            if (string.IsNullOrWhiteSpace(module)) return null;

            string persistentAssetName = module + "_preview_SkeletonData";
            if (persistentAsset != null &&
                string.Equals(
                    persistentAsset.name,
                    persistentAssetName,
                    StringComparison.Ordinal))
            {
                Spine.SkeletonData persistentData =
                    persistentAsset.GetSkeletonData(false);
                if (persistentData != null)
                {
                    if (Cache.TryGetValue(
                            module,
                            out OfficialSpineModule persistentCached) &&
                        persistentCached.Asset == persistentAsset)
                    {
                        return persistentCached;
                    }

                    var persistentResult = new OfficialSpineModule
                    {
                        Asset = persistentAsset,
                        CompatibilityData = new SkeletonData(persistentData)
                    };
                    Cache[module] = persistentResult;
                    return persistentResult;
                }
            }

            if (Cache.TryGetValue(module, out OfficialSpineModule cached))
                return cached;

            TextAsset skeletonJson = ResourceAssetLoader.Load<TextAsset>(
                $"{ResourceRoot}/{module}_skeleton");
            TextAsset atlasText = ResourceAssetLoader.Load<TextAsset>(
                $"{ResourceRoot}/{module}_atlas");
            if (skeletonJson == null || atlasText == null)
            {
                Debug.LogError(
                    $"Official Spine 4.1 resources are missing for '{module}'. " +
                    $"Expected Resources/{ResourceRoot}/" +
                    $"{module}_skeleton.txt and " +
                    $"{module}_atlas.txt.");
                return null;
            }

            try
            {
                List<AtlasPageDefinition> pages = ParsePages(atlasText.text);
                if (pages.Count == 0)
                    throw new InvalidDataException("Atlas contains no pages.");

                var materials = new Material[pages.Count];
                var materialByPage = new Dictionary<string, Material>(
                    StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < pages.Count; index++)
                {
                    AtlasPageDefinition page = pages[index];
                    string pageBaseName = Path.GetFileNameWithoutExtension(
                        page.Name);
                    Texture2D texture = ResourceAssetLoader.Load<Texture2D>(
                        $"{TextureRoot}/{module}_{pageBaseName}");
                    if (texture == null)
                    {
                        throw new InvalidDataException(
                            $"Atlas page texture is missing: " +
                            $"{module}_{pageBaseName}");
                    }

                    Shader shader = Shader.Find("Spine/Skeleton");
                    if (shader == null)
                        throw new InvalidOperationException(
                            "Official Spine/Skeleton shader was not found.");

                    // The recovered completion ribbon page is straight-alpha
                    // even though its source atlas declares PMA. Treating it
                    // as PMA recreates the bright fringe reported on the
                    // settlement title, so preserve the verified exception.
                    bool usePma = page.PremultipliedAlpha &&
                        !string.Equals(
                            module,
                            "complete_title",
                            StringComparison.Ordinal);
                    Material straightTemplate =
                        PrefabCatalog.Current?.SpineStraightAlphaMaterial;
                    if (!usePma && straightTemplate == null)
                    {
                        throw new InvalidOperationException(
                            "PrefabCatalog is missing the serialized Spine " +
                            "straight-alpha material. Runtime-created shader " +
                            "keywords can be stripped from Android builds.");
                    }

                    var material = usePma
                        ? new Material(shader)
                        : new Material(straightTemplate);
                    material.name = $"{module}_{pageBaseName}_Spine41";
                    material.mainTexture = texture;
                    material.hideFlags = HideFlags.HideAndDontSave;
                    ApplyPmaTextureSetting(material, usePma);
                    materials[index] = material;
                    materialByPage[pageBaseName] = material;
                }

                SpineAtlasAsset atlasAsset =
                    SpineAtlasAsset.CreateRuntimeInstance(
                        atlasText,
                        materials,
                        true,
                        _ => new ModuleTextureLoader(materialByPage));
                atlasAsset.name = module + "_AtlasAsset_Official41";
                atlasAsset.hideFlags = HideFlags.HideAndDontSave;

                SkeletonDataAsset skeletonAsset =
                    SkeletonDataAsset.CreateRuntimeInstance(
                        skeletonJson,
                        atlasAsset,
                        true,
                        1f);
                skeletonAsset.name = module + "_SkeletonData_Official41";
                skeletonAsset.hideFlags = HideFlags.HideAndDontSave;
                Spine.SkeletonData runtimeData =
                    skeletonAsset.GetSkeletonData(false);
                if (runtimeData == null)
                    throw new InvalidDataException(
                        "Official runtime returned no SkeletonData.");

                var result = new OfficialSpineModule
                {
                    Asset = skeletonAsset,
                    CompatibilityData = new SkeletonData(runtimeData)
                };
                Cache[module] = result;
                return result;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Official Spine 4.1 failed to load '{module}': " +
                    exception);
                return null;
            }
        }

        /// <summary>
        /// Runtime equivalent of Spine.Unity.MaterialChecks' PMA toggle.
        /// MaterialChecks is compiled only for UNITY_EDITOR by the official
        /// Spine package, so gameplay code must not reference it in a player
        /// build.
        /// </summary>
        static void ApplyPmaTextureSetting(Material material, bool usePma)
        {
            const string straightAlphaProperty = "_StraightAlphaInput";
            const string straightAlphaKeyword = "_STRAIGHT_ALPHA_INPUT";
            const string alphaPremultiplyKeyword = "_ALPHAPREMULTIPLY_ON";
            const string vertexOnlyPremultiplyKeyword =
                "_ALPHAPREMULTIPLY_VERTEX_ONLY";
            const string alphaBlendKeyword = "_ALPHABLEND_ON";

            if (material.HasProperty(straightAlphaProperty))
            {
                material.SetInt(straightAlphaProperty, usePma ? 0 : 1);
                if (usePma)
                    material.DisableKeyword(straightAlphaKeyword);
                else
                    material.EnableKeyword(straightAlphaKeyword);
                return;
            }

            material.DisableKeyword(alphaPremultiplyKeyword);
            if (usePma)
            {
                material.DisableKeyword(alphaBlendKeyword);
                material.EnableKeyword(vertexOnlyPremultiplyKeyword);
            }
            else
            {
                material.DisableKeyword(vertexOnlyPremultiplyKeyword);
                material.EnableKeyword(alphaBlendKeyword);
            }
        }

        static List<AtlasPageDefinition> ParsePages(string atlasText)
        {
            var result = new List<AtlasPageDefinition>();
            string[] lines = (atlasText ?? string.Empty)
                .Replace("\r", string.Empty)
                .Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                string name = lines[index].Trim();
                if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (index > 0 && !string.IsNullOrWhiteSpace(lines[index - 1]))
                    continue;

                bool pma = false;
                for (int metadata = index + 1;
                     metadata < lines.Length;
                     metadata++)
                {
                    string line = lines[metadata].Trim();
                    if (string.IsNullOrEmpty(line) || line.IndexOf(':') < 0)
                        break;
                    if (line.StartsWith("pma:", StringComparison.Ordinal))
                    {
                        pma = string.Equals(
                            line.Substring(4).Trim(),
                            "true",
                            StringComparison.OrdinalIgnoreCase);
                    }
                }
                result.Add(new AtlasPageDefinition(name, pma));
            }
            return result;
        }

        readonly struct AtlasPageDefinition
        {
            public AtlasPageDefinition(
                string name,
                bool premultipliedAlpha)
            {
                Name = name;
                PremultipliedAlpha = premultipliedAlpha;
            }

            public string Name { get; }

            public bool PremultipliedAlpha { get; }
        }

        sealed class ModuleTextureLoader : Spine.TextureLoader
        {
            readonly Dictionary<string, Material> _materialByPage;

            public ModuleTextureLoader(
                Dictionary<string, Material> materialByPage)
            {
                _materialByPage = materialByPage;
            }

            public void Load(Spine.AtlasPage page, string path)
            {
                string pageName = Path.GetFileNameWithoutExtension(path);
                if (!_materialByPage.TryGetValue(
                        pageName,
                        out Material material))
                {
                    throw new InvalidDataException(
                        $"No material was created for atlas page '{pageName}'.");
                }
                page.rendererObject = material;
                if (page.width == 0 || page.height == 0)
                {
                    page.width = material.mainTexture.width;
                    page.height = material.mainTexture.height;
                }
            }

            public void Unload(object texture)
            {
            }
        }
    }
}
