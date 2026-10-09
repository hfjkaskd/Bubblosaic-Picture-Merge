using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Disposable authoring preview; never opens a page or runs advertisement/save flows.
    public static class BizzaDialogLayoutInspection
    {
        const string LosePrefabPath = "Assets/BizzaWZ/Common/BizzaGame/LosePanel/LosePanel.prefab";
        const int Width = 1200;
        const int Height = 850;

        public static void RenderLosePanel(string folder)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before the lose dialog preview.");
            Directory.CreateDirectory(folder);
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(Width, Height, 24);
            RenderTexture previous = RenderTexture.active;
            var report = new StringBuilder("LosePanel prefab preview: 1200x850, one design unit per pixel.\n");
            try
            {
                var cameraObject = new GameObject("Dialog preview camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = Height * 0.5f;
                camera.aspect = (float)Width / Height;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.02f, 0.12f, 0.26f);
                camera.targetTexture = target;

                var canvasObject = new GameObject("Dialog preview canvas", typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(Width, Height);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LosePrefabPath);
                if (prefab == null) throw new InvalidOperationException("LosePanel prefab was not found.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.transform.SetParent(canvas.transform, false);
                var panel = instance.GetComponent<LosePanel>();
                if (panel == null) throw new InvalidOperationException("LosePanel component was not found.");

                CaptureState(panel, true, camera, target, folder, report);
                CaptureState(panel, false, camera, target, folder, report);
                report.Append("\nScope: authored layout and serialized visibility groups only; OnOpen, advertisements,")
                    .Append(" save data, callbacks and live input were not invoked.\nutc=")
                    .Append(DateTime.UtcNow.ToString("O"));
                File.WriteAllText(Path.Combine(folder, "lose-dialog-layout.txt"), report.ToString());
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void CaptureState(LosePanel panel, bool revive, Camera camera, RenderTexture target,
            string folder, StringBuilder report)
        {
            SetVisible(panel.reviveObjs, revive);
            SetVisible(panel.loseObjs, !revive);
            var root = (RectTransform)panel.transform;
            root.ForceUpdateRectTransforms();
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(false))
                text.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();

            string state = revive ? "revive" : "no-revive";
            report.Append("\nstate=").Append(state);
            var background = panel.transform.Find("bg") as RectTransform;
            if (background == null) throw new InvalidOperationException("LosePanel background is missing.");
            report.Append("\nbackground size=").Append(background.rect.size.ToString("F2"))
                .Append(" screenBounds=").Append(ScreenBounds(background, camera).ToString("F2"));
            foreach (Button button in panel.GetComponentsInChildren<Button>(false))
            {
                Image face = button.targetGraphic as Image;
                report.Append("\nbutton=").Append(button.name)
                    .Append(" size=").Append(((RectTransform)button.transform).rect.size.ToString("F2"))
                    .Append(" screenBounds=").Append(ScreenBounds((RectTransform)button.transform, camera).ToString("F2"))
                    .Append(" faceSprite=").Append(face != null && face.sprite != null ? face.sprite.name : "missing")
                    .Append(" faceOnButton=").Append(face != null && face.transform == button.transform);
            }
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(false))
                report.Append("\nlabel=").Append(text.name).Append(" text=").Append(text.text)
                    .Append(" screenBounds=").Append(ScreenBounds(text.rectTransform, camera).ToString("F2"));

            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(folder, "lose-dialog-" + state + ".png"), image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
            report.Append('\n');
        }

        static void SetVisible(GameObject[] objects, bool visible)
        {
            if (objects == null) return;
            foreach (GameObject item in objects)
                if (item != null) item.SetActive(visible);
        }

        static Rect ScreenBounds(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = camera.WorldToScreenPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
