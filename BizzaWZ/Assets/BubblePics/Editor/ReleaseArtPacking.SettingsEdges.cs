using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace BubblePics.EditorTools
{
    public static partial class ReleaseArtPacking
    {
        [Serializable] sealed class SettingsContours
        {
            public string source, atlas, material, sprite;
            public float[] sourceRect;
            public FooterContour outer, interior;
        }

        public static void RepairSettingsEdges()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var spec = JsonUtility.FromJson<SettingsContours>(File.ReadAllText("Assets/BubblePics/Editor/SettingsFrameContours.json"));
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation/SettingsEdges-20260930"));
            Directory.CreateDirectory(folder);
            string backup = Path.Combine(folder, "Atlas0-before.png");
            if (!File.Exists(backup)) File.Copy(spec.atlas, backup);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(spec.material));
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(spec.source)) || !atlas.LoadImage(File.ReadAllBytes(spec.atlas)))
                    throw new InvalidOperationException("Cannot decode settings artwork.");
                source.filterMode = FilterMode.Bilinear;
                var importer = (TextureImporter)AssetImporter.GetAtPath(spec.atlas);
                var factory = new SpriteDataProviderFactories(); factory.Init();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
                SpriteRect target = null;
                foreach (var r in provider.GetSpriteRects()) if (r.name == spec.sprite) { target = r; break; }
                if (target == null) throw new InvalidOperationException("Settings panel sprite missing.");
                int width = (int)target.rect.width, height = (int)target.rect.height;
                if (width != spec.sourceRect[2] || height != spec.sourceRect[3]) throw new InvalidOperationException("Panel size must stay unchanged.");
                // The original art has a shallow arch with asymmetric shoulders, not a rounded box.
                material.SetVector("_TopCurve", Vector4.zero);
                material.SetVector("_VisibleRect", Vector4.zero);
                bakeShaders = new Dictionary<string, Shader>();
                var r0 = spec.sourceRect;
                var rect = new Rect(r0[0], source.height - r0[1] - r0[3], r0[2], r0[3]);
                var pixels = Render(new Sheet { texture = source }, rect, material, width, height);
                var original = source.GetPixels32();
                var outer = ScanContour(Flatten(spec.outer), width, height);
                var interior = ScanContour(Flatten(spec.interior), width, height);
                var before = atlas.GetPixels32(); var after = (Color32[])before.Clone();
                int recovered = 0, restoredRim = 0;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    int i = y * width + x, destination = ((int)target.rect.y + y) * atlas.width + (int)target.rect.x + x;
                    var sourcePixel = original[((int)rect.y + y) * source.width + (int)rect.x + x];
                    // Protect the authored outer rim from content erasure, especially beside Close.
                    float content = interior[i] / 16f;
                    var color = (Color32)Color.Lerp((Color)sourcePixel, (Color)pixels[i], content);
                    color.a = (byte)Mathf.RoundToInt(255f * outer[i] / 16f);
                    if (color.a > before[destination].a + 100) recovered++;
                    if (content < 1 && color.a > 0 && !color.Equals(before[destination])) restoredRim++;
                    pixels[i] = color;
                }
                for (int y = -2; y < height + 2; y++) for (int x = -2; x < width + 2; x++)
                    after[((int)target.rect.y + y) * atlas.width + (int)target.rect.x + x] =
                        pixels[Mathf.Clamp(y, 0, height - 1) * width + Mathf.Clamp(x, 0, width - 1)];
                atlas.SetPixels32(after); atlas.Apply(); File.WriteAllBytes(spec.atlas, atlas.EncodeToPNG());
                AssetDatabase.ImportAsset(spec.atlas, ImportAssetOptions.ForceSynchronousImport);
                File.WriteAllText(Path.Combine(folder, "authoring.txt"),
                    "PASS restored silhouette pixels=" + recovered + "; restored rim pixels=" + restoredRim +
                    "; original size=" + width + "x" + height + "; atlas coordinates, sprite IDs and prefab unchanged.\n");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(atlas);
            }
        }

        // Offline scan conversion: four subpixel rows, sorted contour intersections, no per-pixel polygon search.
        static byte[] ScanContour(List<Vector2> polygon, int width, int height)
        {
            var coverage = new byte[width * height];
            var intersections = new List<float>(polygon.Count);
            for (int row = 0; row < height; row++) for (int sy = 0; sy < 4; sy++)
            {
                float y = row + (sy + .5f) / 4f; intersections.Clear();
                for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
                {
                    var a = polygon[i]; var b = polygon[j];
                    if ((a.y > y) != (b.y > y)) intersections.Add(a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y));
                }
                intersections.Sort();
                for (int i = 0; i + 1 < intersections.Count; i += 2)
                {
                    int first = Mathf.Max(0, Mathf.CeilToInt(intersections[i] * 4 - .5f));
                    int end = Mathf.Min(width * 4, Mathf.CeilToInt(intersections[i + 1] * 4 - .5f));
                    for (int sample = first; sample < end; sample++) coverage[(height - 1 - row) * width + sample / 4]++;
                }
            }
            return coverage;
        }
    }
}
