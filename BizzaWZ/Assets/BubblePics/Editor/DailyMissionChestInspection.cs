#if BIZZA_REAL_WITHDRAW
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    /// <summary>Offline art import and isolated previews; never opens a live account page.</summary>
    public static class DailyMissionChestInspection
    {
        const string Asset = "Assets/BubblePics/Resources/RewardArt20260930/DailyMissionChest.png";
        const string Prefab = "Assets/BizzaWZ/Final/Real/UI/DailyMissionPanel/DailyMissionPanel.prefab";
        const string Folder = "../Validation/DailyMissionChest-20261009";

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before previewing.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.ImportAsset(Asset, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Asset);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 512;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 512;
                settings.format = TextureImporterFormat.ASTC_6x6;
                settings.compressionQuality = 100;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            Render(1080, 1920);
            Render(1080, 2400);
            File.WriteAllText(Path.Combine(Folder, "verification.txt"),
                "PASS Unity import and isolated prefab previews at 1080x1920 and 1080x2400.\n" +
                "Chest uses an independent Resources sprite with preserved aspect ratio and no currency binding.\n" +
                "512px maximum; Android/iOS ASTC 6x6; mipmaps/read-write disabled.\n" +
                "No live account requests, reward changes, save changes or APK build.\n");
        }

        static void Render(int width, int height)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(width, height, 24);
            Texture2D capture = null;
            var previous = RenderTexture.active;
            try
            {
                var cameraObject = new GameObject("Art preview camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = height / 2f;
                camera.aspect = (float)width / height;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.04f, .21f, .38f);
                camera.targetTexture = target;
                var canvasObject = new GameObject("Art preview canvas", typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(width, height);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), scene);
                go.transform.SetParent(canvas.transform, false);
                ReferencePrefabTools.Stretch(go.transform);
                go.SetActive(true);
                foreach (var binding in go.GetComponentsInChildren<CoralResourceSprite>(true))
                {
                    var serialized = new SerializedObject(binding);
                    binding.SetSource(serialized.FindProperty("_resourcePath").stringValue,
                        serialized.FindProperty("_spriteName").stringValue);
                }
                var content = go.transform.Find("AspectContent") ?? go.transform;
                var chest = content.Find("RewardChest").GetComponent<Image>();
                if (chest.sprite == null || chest.GetComponent<WzIconAmend>() != null || !chest.preserveAspect)
                    throw new InvalidOperationException("Chest decoration is not configured correctly.");
                foreach (var caption in go.GetComponentsInChildren<ApprovedHudCaption>(true))
                    caption.gameObject.SetActive(false);
                foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    var group = text.GetComponent<CanvasGroup>();
                    if (group != null) group.alpha = 1;
                }
                void Text(string path, string value) => content.Find(path).GetComponent<TMP_Text>().text = value;
                Text("TitleCaption", "Missão diária");
                Text("RewardCaption", "Recompensa");
                Text("RewardAmount", "R$0,20");
                Text("Requirement", "Assista a 30 vídeos e receba R$0,20\nrecompensas (0/30)");
                Text("Progress", "0 / 30");
                Text("ResetTime", "Reinicia em 10:33:14");
                Text("GoState/Watch/WatchLabel", "Ver vídeo");
                var page = go.GetComponent<DailyMissionPanel>();
                page.GoObj.SetActive(true);
                page.WithdrawObj.SetActive(false);
                page.ClaimedObj.SetActive(false);
                page.claimedHint.gameObject.SetActive(false);
                content.Find("Fill").GetComponent<Image>().fillAmount = 0;
                Canvas.ForceUpdateCanvases();
                foreach (var text in go.GetComponentsInChildren<TMP_Text>(false)) text.ForceMeshUpdate(true, true);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(width, height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                capture.Apply();
                File.WriteAllBytes(Path.Combine(Folder, "daily-chest-" + width + "x" + height + ".png"), capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
