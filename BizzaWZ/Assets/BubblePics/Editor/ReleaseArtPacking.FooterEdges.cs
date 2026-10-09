using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace BubblePics.EditorTools
{
    public static partial class ReleaseArtPacking
    {
        [Serializable] sealed class FooterContours { public string source, atlas; public FooterContour[] buttons; }
        [Serializable] sealed class FooterContour { public string sprite, material; public float[] sourceRect, start; public FooterCurve[] curves; }
        [Serializable] sealed class FooterCurve { public float[] p; }

        // Offline sprite-mask authoring only. Original RGB artwork, atlas coordinates and IDs are retained.
        // The asymmetric crest cannot be represented by the former rounded rectangle + ellipse union.
        public static void RepairFooterEdges()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var spec = JsonUtility.FromJson<FooterContours>(File.ReadAllText("Assets/BubblePics/Editor/FooterButtonContours.json"));
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation/FooterButtonEdges-20260930"));
            Directory.CreateDirectory(folder);
            string backup = Path.Combine(folder, "Atlas0-before.png");
            if (!File.Exists(backup)) File.Copy(spec.atlas, backup);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var report = new StringBuilder();
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(spec.source)) || !atlas.LoadImage(File.ReadAllBytes(spec.atlas)))
                    throw new InvalidOperationException("Cannot decode source art.");
                source.filterMode = FilterMode.Bilinear;
                var sheet = new Sheet { texture = source };
                bakeShaders = new Dictionary<string, Shader>();
                var importer = (TextureImporter)AssetImporter.GetAtPath(spec.atlas);
                var factory = new SpriteDataProviderFactories(); factory.Init();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
                var rects = provider.GetSpriteRects();
                var before = atlas.GetPixels32();
                var after = (Color32[])before.Clone();
                var touched = new bool[before.Length];
                foreach (var button in spec.buttons)
                {
                    SpriteRect target = null;
                    foreach (var r in rects) if (r.name == button.sprite) { target = r; break; }
                    if (target == null) throw new InvalidOperationException("Missing packed sprite " + button.sprite);
                    int width = (int)target.rect.width, height = (int)target.rect.height;
                    if (width != button.sourceRect[2] || height != button.sourceRect[3])
                        throw new InvalidOperationException("Do not rescale artwork during edge repair.");
                    var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/BubblePics/Resources/CoralV3/ReferenceMaterials/" + button.material + ".mat"));
                    Color32[] pixels;
                    try
                    {
                        material.SetFloat("_MaskMode", 0);
                        // Clear the complete baked caption, including the small upper tip of the dollar sign.
                        if (button.material == "Crown") material.SetVector("_EraseRect", new Vector4(0, -50, 118, 35));
                        var r = button.sourceRect;
                        pixels = Render(sheet, new Rect(r[0], source.height - r[1] - r[3], r[2], r[3]), material, width, height);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(material); }
                    var contour = Flatten(button);
                    int recovered = 0;
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        int samples = 0;
                        for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++)
                            if (ContainsPoint(contour, new Vector2(x + (sx + .5f) / 4f, height - y - (sy + .5f) / 4f))) samples++;
                        int index = y * width + x;
                        pixels[index].a = (byte)Mathf.RoundToInt(255f * samples / 16f);
                        int destination = ((int)target.rect.y + y) * atlas.width + (int)target.rect.x + x;
                        if (pixels[index].a > before[destination].a + 100) recovered++;
                    }
                    // Re-extrude the existing two-pixel packing gutter; other sprites remain byte-identical.
                    for (int y = -2; y < height + 2; y++) for (int x = -2; x < width + 2; x++)
                    {
                        int destination = ((int)target.rect.y + y) * atlas.width + (int)target.rect.x + x;
                        after[destination] = pixels[Mathf.Clamp(y, 0, height - 1) * width + Mathf.Clamp(x, 0, width - 1)];
                        touched[destination] = true;
                    }
                    report.AppendLine(button.sprite + " restored edge pixels=" + recovered + " size=" + width + "x" + height);
                }
                for (int i = 0; i < before.Length; i++)
                    if (!touched[i] && !before[i].Equals(after[i])) throw new InvalidOperationException("Unrelated atlas pixels changed.");
                atlas.SetPixels32(after); atlas.Apply(); File.WriteAllBytes(spec.atlas, atlas.EncodeToPNG());
                AssetDatabase.ImportAsset(spec.atlas, ImportAssetOptions.ForceSynchronousImport);
                report.AppendLine("PASS: other sprites unchanged; sprite IDs/rectangles and prefab layout unchanged.");
                File.WriteAllText(Path.Combine(folder, "authoring.txt"), report.ToString());
            }
            finally { UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(atlas); }
        }

        static List<Vector2> Flatten(FooterContour contour)
        {
            var points = new List<Vector2>();
            var a = new Vector2(contour.start[0], contour.start[1]); points.Add(a);
            foreach (var curve in contour.curves)
            {
                var p = curve.p;
                var b = new Vector2(p[0], p[1]); var c = new Vector2(p[2], p[3]); var d = new Vector2(p[4], p[5]);
                for (int step = 1; step <= 16; step++)
                {
                    float t = step / 16f, u = 1 - t;
                    points.Add(u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d);
                }
                a = d;
            }
            return points;
        }

        static bool ContainsPoint(List<Vector2> points, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
            {
                var a = points[i]; var b = points[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
