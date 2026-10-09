using System;
using System.IO;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace BubblePics.EditorTools
{
    public static class ComboLayerAuthoring
    {
        const string PrefabPath = "Assets/BubblePics/RuntimePrefabs/UI/ComboOverlay.prefab";
        const string MaterialPath = "Assets/BubblePics/RuntimePrefabs/UI/ComboGraphic.mat";

        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring.");
            string backup = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../Validation/ComboLayer-20260929/ComboOverlay-before.prefab"));
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(PrefabPath, backup);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/BizzaWZ/Common/ThirdParty/Spine/Spine/Runtime/spine-unity/Materials/SkeletonGraphicDefault.mat");
                material = new Material(template) { name = "ComboGraphic" };
                // combo_title.png is straight alpha, matching its atlas declaration.
                material.SetFloat("_StraightAlphaInput", 1f);
                material.EnableKeyword("_STRAIGHT_ALPHA_INPUT");
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var authoringCanvas = new GameObject("AuthoringCanvas", typeof(RectTransform), typeof(Canvas));
            var root = new GameObject("ComboOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            try
            {
                root.transform.SetParent(authoringCanvas.transform, false);
                root.layer = LayerMask.NameToLayer("UI");
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var canvas = root.GetComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = 1600;
                var group = root.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                var controller = root.AddComponent<ComboOverlay>();

                var view = new GameObject("ComboView", typeof(RectTransform));
                view.layer = root.layer;
                var viewRect = view.GetComponent<RectTransform>();
                viewRect.SetParent(rect, false);
                viewRect.anchorMin = viewRect.anchorMax = new Vector2(.5f, 1f);
                viewRect.anchoredPosition = new Vector2(0f, -ComboOverlay.OVERLAY_Y);
                viewRect.sizeDelta = Vector2.zero;
                var spineObject = new GameObject("Spine", typeof(RectTransform));
                spineObject.layer = root.layer;
                spineObject.transform.SetParent(viewRect, false);
                // Spine data uses design units; SkeletonGraphic multiplies vertices by 100 PPU.
                spineObject.transform.localScale = Vector3.one / App.CanvasReferencePixelsPerUnit;
                var graphic = spineObject.AddComponent<SkeletonGraphic>();
                graphic.material = material;
                graphic.raycastTarget = false;
                graphic.maskable = false;
                graphic.MeshGenerator.settings.pmaVertexColors = true;
                view.SetActive(false);

                var serialized = new SerializedObject(controller);
                serialized.FindProperty("_viewRoot").objectReferenceValue = viewRect;
                serialized.FindProperty("_spine").objectReferenceValue = graphic;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(authoringCanvas); }
        }
    }
}
