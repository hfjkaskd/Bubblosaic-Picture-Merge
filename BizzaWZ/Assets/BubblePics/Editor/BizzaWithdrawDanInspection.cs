#if BIZZA_REAL_WITHDRAW
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Live layout diagnostics only; never invokes claim buttons or changes account/save data.
    public static class BizzaWithdrawDanInspection
    {
        private static bool capturing;

        public static void Open(string folder) => OpenAsync(folder).Forget(Debug.LogException);

        private static async UniTask OpenAsync(string folder)
        {
            if (!EditorApplication.isPlaying || UIModule.Instance == null)
                throw new InvalidOperationException("Enter Play Mode and finish UI initialization first.");
            await UIModule.Instance.OpenPage(UIPageIds.WithdrawDanPanel);
            await UniTask.NextFrame();
            Inspect(folder);
            CaptureScreenshot(folder);
        }

        public static void Inspect(string folder)
        {
            WithdrawDanPanel page = GetPage();
            Directory.CreateDirectory(folder);
            Canvas.ForceUpdateCanvases();
            var report = new StringBuilder();
            report.Append("utc=").Append(DateTime.UtcNow.ToString("O"))
                .Append(" screen=").Append(Screen.width).Append('x').Append(Screen.height)
                .Append(" page=").Append(page.name).Append(" active=").Append(page.gameObject.activeInHierarchy);
            Bounds("page", page.transform as RectTransform, report);
            ImageInfo("page.progressImg", page.progressImg, report);
            TextInfo("page.progressTxt", page.progressTxt, report);
            TextInfo("page.hintTxt", page.hintTxt, report);
            if (page.hintTxt != null && page.progressImg != null)
                report.Append("\nhintOverlapsProgress=").Append(ScreenBounds(page.hintTxt.rectTransform)
                    .Overlaps(ScreenBounds(page.progressImg.rectTransform)));
            int index = 0;
            foreach (var item in page.GetComponentsInChildren<WithdrawDanItem>(false))
            {
                string label = "item[" + index++ + "] " + item.name;
                Bounds(label, item.transform as RectTransform, report);
                IconBounds(label, item, report);
                ImageInfo(label + ".progressImage", item.progressImage, report);
                TextInfo(label + ".Num", item.progressText, report);
                TextInfo(label + ".hintText", item.hintText, report);
            }
            foreach (var scroll in page.GetComponentsInChildren<ScrollRect>(true))
            {
                RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.transform as RectTransform;
                float viewportHeight = viewport != null ? viewport.rect.height : 0f;
                float contentHeight = scroll.content != null && viewport != null
                    ? RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, scroll.content).size.y : 0f;
                report.Append("\nscroll=").Append(scroll.name).Append(" parent=").Append(scroll.transform.parent.name)
                    .Append(" active=").Append(scroll.isActiveAndEnabled).Append(" vertical=").Append(scroll.vertical)
                    .Append(" viewportHeight=").Append(viewportHeight).Append(" contentHeight=").Append(contentHeight)
                    .Append(" canScrollVertically=").Append(scroll.isActiveAndEnabled && scroll.vertical && contentHeight > viewportHeight)
                    .Append(" normalizedPosition=").Append(scroll.normalizedPosition);
                Bounds("scroll.viewport", viewport, report);
                Bounds("scroll.content", scroll.content, report);
            }
            File.WriteAllText(Path.Combine(folder, "withdraw-dan-layout.txt"), report.ToString());
        }

        public static void CaptureScreenshot(string folder)
        {
            GetPage();
            Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "withdraw-dan-live.png"));
        }

        public static void CaptureFillVariants(string folder) => CaptureFillVariantsAsync(folder).Forget(Debug.LogException);

        private static async UniTask CaptureFillVariantsAsync(string folder)
        {
            if (capturing) throw new InvalidOperationException("Fill capture is already running.");
            WithdrawDanPanel page = GetPage();
            Directory.CreateDirectory(folder);
            var images = new List<Image>();
            if (page.progressImg != null) images.Add(page.progressImg);
            foreach (var item in page.GetComponentsInChildren<WithdrawDanItem>(false))
                if (item.progressImage != null && !images.Contains(item.progressImage)) images.Add(item.progressImage);
            var original = new float[images.Count];
            for (int i = 0; i < images.Count; i++) original[i] = images[i].fillAmount;
            capturing = true;
            try
            {
                foreach (int percent in new[] { 0, 2, 40, 100 })
                {
                    GetPage();
                    foreach (Image image in images) if (image != null) image.fillAmount = percent / 100f;
                    Canvas.ForceUpdateCanvases();
                    await UniTask.NextFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, "withdraw-dan-fill-" + percent + ".png"));
                    await UniTask.NextFrame(); // Allow the queued end-of-frame screenshot before restoring/changing fill.
                }
            }
            finally
            {
                for (int i = 0; i < images.Count; i++) if (images[i] != null) images[i].fillAmount = original[i];
                capturing = false;
                Canvas.ForceUpdateCanvases();
            }
        }

        private static WithdrawDanPanel GetPage()
        {
            var page = EditorApplication.isPlaying && UIModule.Instance != null
                ? UIModule.Instance.GetPage<WithdrawDanPanel>() : null;
            if (page == null || !page.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The live WithdrawDanPanel must be open.");
            return page;
        }

        private static void ImageInfo(string label, Image image, StringBuilder report)
        {
            if (image == null) { report.Append('\n').Append(label).Append("=missing"); return; }
            Bounds(label, image.rectTransform, report);
            report.Append(" fill=").Append(image.fillAmount).Append(" imageType=").Append((int)image.type)
                .Append(" fillMethod=").Append((int)image.fillMethod).Append(" enabled=").Append(image.enabled);
        }

        private static void IconBounds(string label, WithdrawDanItem item, StringBuilder report)
        {
            if (item.icon == null) { report.Append('\n').Append(label).Append(".Icon=missing"); return; }
            Rect itemBounds = ScreenBounds((RectTransform)item.transform);
            foreach (RectTransform rect in item.icon.GetComponentsInChildren<RectTransform>(true))
            {
                Bounds(label + ".Icon/" + rect.name, rect, report);
                Rect screen = ScreenBounds(rect);
                var insets = new Vector4(screen.xMin - itemBounds.xMin, screen.yMin - itemBounds.yMin,
                    itemBounds.xMax - screen.xMax, itemBounds.yMax - screen.yMax);
                bool inside = insets.x >= -0.5f && insets.y >= -0.5f && insets.z >= -0.5f && insets.w >= -0.5f;
                report.Append(" insideItemRect=").Append(inside)
                    .Append(" itemInsetsLTRB=").Append(new Vector4(insets.x, insets.w, insets.z, insets.y).ToString("F2"));
            }
        }

        private static void TextInfo(string label, TMP_Text text, StringBuilder report)
        {
            if (text == null) { report.Append('\n').Append(label).Append("=missing"); return; }
            text.ForceMeshUpdate();
            Bounds(label, text.rectTransform, report);
            report.Append(" object=").Append(text.name).Append(" text=").Append(text.text.Replace('\n', ' '))
                .Append(" fontSize=").Append(text.fontSize).Append(" autoSize=").Append(text.enableAutoSizing)
                .Append(" overflow=").Append(text.isTextOverflowing).Append(" overflowMode=").Append((int)text.overflowMode)
                .Append(" preferredHeight=").Append(text.preferredHeight);
        }

        private static void Bounds(string label, RectTransform rect, StringBuilder report)
        {
            report.Append('\n').Append(label);
            if (rect == null) { report.Append("=missing"); return; }
            Rect screen = ScreenBounds(rect);
            report.Append(" designSize=").Append(rect.rect.size.ToString("F2"))
                .Append(" screenBounds=").Append(screen.ToString("F2")).Append(" pixelHeight=").Append(screen.height)
                .Append(" active=").Append(rect.gameObject.activeInHierarchy);
        }

        private static Rect ScreenBounds(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
#endif
