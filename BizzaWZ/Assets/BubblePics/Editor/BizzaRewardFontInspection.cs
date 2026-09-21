using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BubblePics.EditorTools
{
    // Isolated authoring preview: never opens a reward flow or changes account data.
    public static class BizzaRewardFontInspection
    {
        public static void Render(string folder)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before the font preview.");
            var scene = EditorSceneManager.NewPreviewScene();
            var rt = new RenderTexture(1024, 256, 24);
            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                var cameraObject = new GameObject("Font preview camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = 128;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.12f, 0.2f);
                camera.targetTexture = rt;
                var canvasObject = new GameObject("Font preview canvas", typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(1024, 256);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BizzaWZ/Final/Real/UI/GetRewardPanel/GetRewardPanel.prefab");
                TMP_Text source = null;
                foreach (var candidate in prefab.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string guid, out long id) && id == 4284177494034142888L)
                        source = candidate;
                }
                if (source == null) throw new InvalidOperationException("Reward label not found in prefab.");
                var label = UnityEngine.Object.Instantiate(source, canvas.transform);
                label.gameObject.SetActive(true);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                label.rectTransform.localPosition = Vector3.zero;
                label.rectTransform.localRotation = Quaternion.identity;
                label.rectTransform.localScale = Vector3.one;
                label.rectTransform.sizeDelta = new Vector2(900, 160);
                label.text = "Resgatar\u00d72";
                Canvas.ForceUpdateCanvases();
                label.ForceMeshUpdate(true, true);
                Canvas.ForceUpdateCanvases();
                var lines = new List<string>();
                for (int i = 0; i < label.textInfo.characterCount; i++)
                {
                    var ch = label.textInfo.characterInfo[i];
                    var mat = label.fontSharedMaterials[ch.materialReferenceIndex];
                    lines.Add(ch.character + " font=" + ch.fontAsset.name + " shader=" + mat.shader.name + " supported=" + mat.shader.isSupported);
                    if (mat.shader.name == "Hidden/InternalErrorShader" || !mat.shader.isSupported)
                        throw new InvalidOperationException("Invalid reward glyph shader: " + ch.character);
                }
                camera.Render();
                RenderTexture.active = rt;
                image = new Texture2D(1024, 256, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1024, 256), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(folder, "reward-font-fixed.png"), image.EncodeToPNG());
                File.WriteAllLines(Path.Combine(folder, "reward-font-fixed.txt"), lines);
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
