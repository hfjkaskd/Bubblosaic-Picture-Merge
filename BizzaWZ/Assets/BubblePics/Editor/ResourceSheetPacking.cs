using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BubblePics.EditorTools
{
    // Authoring support for losslessly cropped source sheets. A subsequent manual
    // page rebuild starts from the full approved source, in its original coordinates.
    public static class ResourceSheetPacking
    {
        public static void RestoreForAuthoring(string path, string originalSource)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            const string prefix = "apk-crop-v1|";
            if (importer == null || !importer.userData.StartsWith(prefix, StringComparison.Ordinal)) return;
            var data = importer.userData.Split('|');
            if (data.Length != 7) throw new InvalidOperationException("Invalid crop metadata: " + path);
            var n = new float[6];
            for (int i = 0; i < n.Length; i++) n[i] = float.Parse(data[i + 1], CultureInfo.InvariantCulture);
            if (!File.Exists(originalSource)) throw new FileNotFoundException("Approved full source required for page authoring.", originalSource);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Path.GetDirectoryName(path).Replace('\\', '/') }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material == null || !material.HasProperty("_TextureSize")) continue;
                var size = material.GetVector("_TextureSize");
                float dx = n[2] * size.x / n[4], dy = n[3] * size.y / n[5];
                for (int i = 0; i < 6; i++) TranslateRect(material, "_Erase" + i, dx, dy);
                TranslateRect(material, "_VisibleRect", dx, dy);
                TranslateRect(material, "_MirrorPatch", dx, dy);
                TranslateRect(material, "_TopCurve", dx, dy);
                var point = material.GetVector("_SamplePoint");
                if (point.z > 0) { point.x += dx; point.y += dy; material.SetVector("_SamplePoint", point); }
                var rows = material.GetVector("_SampleRows");
                if (rows.z > 0) { rows.x += dy; rows.y += dy; material.SetVector("_SampleRows", rows); }
                TranslateSample(material, "_SampleX", dx);
                TranslateSample(material, "_TailSampleX", dx);
                TranslateSample(material, "_SampleY", dy);
                if (material.GetVector("_MirrorPatch").z > 0)
                    material.SetFloat("_MirrorY", material.GetFloat("_MirrorY") + dy);
                size.x *= n[0] / n[4]; size.y *= n[1] / n[5];
                material.SetVector("_TextureSize", size);
                EditorUtility.SetDirty(material);
            }
            File.Copy(originalSource, path, true);
            importer.userData = string.Empty;
            importer.SaveAndReimport();
        }

        static void TranslateRect(Material material, string property, float x, float y)
        {
            var v = material.GetVector(property);
            if (v.z <= 0) return;
            v.x += x; v.y += y;
            material.SetVector(property, v);
        }

        static void TranslateSample(Material material, string property, float delta)
        {
            float value = material.GetFloat(property);
            if (value >= 0) material.SetFloat(property, value + delta);
        }
    }
}
