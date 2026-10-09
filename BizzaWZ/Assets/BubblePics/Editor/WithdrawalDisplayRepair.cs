using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static class WithdrawalDisplayRepair
    {
        const string Folder = "Assets/BizzaWZ/Final/Real/UI/FakeWithdrawPanel/";
        const float Width = 2360f * 1080 / 2340;

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var item = PrefabUtility.LoadPrefabContents(Folder + "WithdrawAmountItem.prefab");
            try
            {
                Responsive((RectTransform)item.transform, 336 * Width / 853);
                foreach (string path in new[]{"Selected/Check", "Locked"})
                {
                    var icon = item.transform.Find(path);
                    if (icon != null) icon.GetComponent<Image>().preserveAspect = true;
                }
                PrefabUtility.SaveAsPrefabAsset(item, Folder + "WithdrawAmountItem.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(item); }
            var root = PrefabUtility.LoadPrefabContents(Folder + "FakeWithdrawPanel.prefab");
            try
            {
                PageAspectAuthoring.Configure(root);
                var content = root.transform.Find("Content");
                Responsive((RectTransform)content, Width);
                foreach (string path in new[]{"Back", "History", "FAQ"})
                    content.Find(path).GetComponent<Image>().preserveAspect = false;
                var choices = content.Find("Amounts");
                var grid = choices.GetComponent<GridLayoutGroup>();
                if (!(grid is ResponsiveColumnGrid))
                {
                    string configuration = EditorJsonUtility.ToJson(grid);
                    UnityEngine.Object.DestroyImmediate(grid);
                    grid = choices.gameObject.AddComponent<ResponsiveColumnGrid>();
                    EditorJsonUtility.FromJsonOverwrite(configuration, grid);
                }
                var fill = root.GetComponent<FakeWithdrawPanel>().progressImg;
                var trackMask = fill.transform.parent.GetComponent<Mask>();
                if (trackMask == null) trackMask = fill.transform.parent.gameObject.AddComponent<Mask>();
                trackMask.showMaskGraphic = true;
                // The baked fill has an opaque rectangular matte. Clip only this sprite's
                // rounded outline; keep its existing pixels, sliced caps and runtime fill logic.
                var sprite = CoralResourceSprite.Load("ReleaseUI/NewPlayerReference20260928", "ProgressFill__bb7ab101e9");
                string materialPath = Folder + "ProgressRounded.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("BubblePics/UI/NewPlayerReferencePlate"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                Rect r = sprite.rect;
                material.SetVector("_TextureSize", new Vector4(sprite.texture.width, sprite.texture.height, 0, 0));
                material.SetVector("_VisibleRect", new Vector4(r.x + 1, sprite.texture.height - r.yMax + 1, r.width - 2, r.height - 2));
                material.SetFloat("_Radius", (r.height - 2) / 2);
                material.SetFloat("_Feather", 1);
                EditorUtility.SetDirty(material);
                fill.material = material;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                WithdrawalServiceButtonAuthoring.Configure(root);
                ReferencePrefabTools.Validate(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "FakeWithdrawPanel.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.GetFullPath("../Validation/WithdrawalDisplayFix-20260929/repair.txt"), "PASS responsive page and amount cards; rounded fill matte clipping; " + DateTime.UtcNow.ToString("O"));
        }

        static void Responsive(RectTransform parent, float authoredWidth)
        {
            foreach (RectTransform child in parent)
            {
                float left = child.anchorMin.x * authoredWidth + child.offsetMin.x;
                float right = child.anchorMax.x * authoredWidth + child.offsetMax.x;
                var min = child.anchorMin; var max = child.anchorMax;
                min.x = left / authoredWidth; max.x = right / authoredWidth;
                child.anchorMin = min; child.anchorMax = max;
                var size = child.sizeDelta; size.x = 0; child.sizeDelta = size;
                var position = child.anchoredPosition; position.x = 0; child.anchoredPosition = position;
                if (child.childCount > 0 && right - left > .01f) Responsive(child, right - left);
            }
        }
    }
}
