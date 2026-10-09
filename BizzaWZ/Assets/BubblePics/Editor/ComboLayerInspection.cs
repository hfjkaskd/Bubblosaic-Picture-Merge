using System;
using System.Collections;
using System.IO;
using System.Text;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace BubblePics.EditorTools
{
    // Exercises only presentation. Cancel the real image flight before its landing callback.
    public static class ComboLayerInspection
    {
        public static void Run(string folder)
        {
            var page = BizzaGameplayBridge.Page;
            if (!EditorApplication.isPlaying || page == null || page.Fly.IsFlying || page.Merge.IsBusy)
                throw new InvalidOperationException("An idle gameplay page is required.");
            Directory.CreateDirectory(folder);
            page.StartCoroutine(Observe(page, folder));
        }

        static IEnumerator Observe(BubblePage page, string folder)
        {
            var report = new StringBuilder();
            var combo = page.Combo;
            BubbleView sample = null;
            foreach (var bubble in page.Field.AllBubbles())
                if (bubble.SourceTexture != null) { sample = bubble; break; }
            if (sample == null) throw new InvalidOperationException("No loaded photo on this board.");
            int count = page.CollectedImgs.Count;
            int steps = page.StepsLeft;
            float balance = ItemUtils.GetItemCount(E_ItemType.Dollar);
            try
            {
                combo.ShowCombo("flower_excellent");
                page.Fly.Play(sample.SourceTexture,
                    App.DesignToWorld(new Vector2(DeviceLayout.ViewWidth * .5f - 120f, ComboOverlay.OVERLAY_Y)),
                    650f, 0, 0f, sample.ImageId);
                yield return new WaitForSeconds(.22f);
                var graphic = combo.GetComponentInChildren<SkeletonGraphic>(true);
                Canvas canvas = graphic != null ? graphic.GetComponentInParent<Canvas>() : null;
                report.AppendLine("comboUi=" + (graphic != null));
                report.AppendLine("rootMode=" + (canvas != null ? canvas.rootCanvas.renderMode.ToString() : "WorldMesh"));
                report.AppendLine("comboOrder=" + (canvas != null ? canvas.sortingOrder : -1));
                report.AppendLine("raycastTarget=" + (graphic != null && graphic.raycastTarget));
                var flight = App.I.HudRoot.Find("FlyImage");
                var flightCanvas = flight != null ? flight.GetComponent<Canvas>() : null;
                bool above = canvas != null && canvas.overrideSorting && flightCanvas != null &&
                    canvas.rootCanvas == flightCanvas.rootCanvas && canvas.sortingLayerID == flightCanvas.sortingLayerID &&
                    canvas.sortingOrder > flightCanvas.sortingOrder;
                report.AppendLine("aboveActualImageFlight=" + above);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "overlap.png"));
                yield return null;
                yield return null;
                page.Fly.Cancel();
                combo.HideNow();
                combo.ShowCombo("flower_excellent");
                yield return new WaitForSeconds(.25f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, "gameplay.png"));
                yield return null;
                yield return new WaitForSeconds(2f);
                if (graphic != null)
                {
                    bool hidden = !graphic.gameObject.activeInHierarchy;
                    report.AppendLine("autoHidden=" + hidden);
                    foreach (string skin in new[] { "flower_nice", "flower_perfect", "flower_excellent", "flower_wonderful", "lucky", "big _merge" })
                    {
                        combo.ShowSkin(skin);
                        yield return null;
                        report.AppendLine("skin=" + skin + " active=" + graphic.gameObject.activeInHierarchy +
                            " matched=" + (graphic.Skeleton.Skin.Name == skin));
                        combo.HideNow();
                    }
                    var lucky = ComboOverlay.SpawnLucky(App.I.WorldRoot, sample.transform.position);
                    yield return new WaitForSeconds(2f);
                    report.AppendLine("luckyFreed=" + (lucky == null));
                }
                report.AppendLine("gameStateUnchanged=" + (count == page.CollectedImgs.Count && steps == page.StepsLeft &&
                    balance == ItemUtils.GetItemCount(E_ItemType.Dollar)));
                File.WriteAllText(Path.Combine(folder, "result.txt"), report.ToString());
            }
            finally
            {
                page.Fly.Cancel();
                combo.HideNow();
            }
        }
    }
}
