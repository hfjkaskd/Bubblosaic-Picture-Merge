using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BubblePics.EditorTools
{
    // Drives an ordinary board merge and observes its result; never fabricates rewards or saves.
    public static class BizzaPuzzleRewardInspection
    {
        private static bool running;
        private static readonly int[] CaptureMilliseconds = { 150, 300, 500, 800 };

        public static void Run(string folder, bool closure)
        {
            var page = BizzaGameplayBridge.Page;
            if (running || !EditorApplication.isPlaying || page == null ||
                BizzaGameplayBridge.IsInputBlocked || page.IsInteractionLocked() || page.Merge.IsBusy || page.Fly.IsFlying)
                throw new InvalidOperationException("Gameplay must be idle for puzzle reward inspection.");
            if (Bizza.Sdk.ChannelConfig.Instance.real_CustomConfig.singleCurrencyMode)
                throw new InvalidOperationException("Dollar inspection requires the existing dual-currency configuration.");
            RectTransform layer = UIModule.Instance.RewardItemLayer;
            if (layer == null || CountCurrencyFx(layer, out _) != 0)
                throw new InvalidOperationException("Reward layer must exist and contain no active currency FX.");

            BubbleView source = null, target = null;
            var bubbles = new List<BubbleView>(page.Field.AllBubbles());
            foreach (var first in bubbles)
            {
                foreach (var second in bubbles)
                {
                    if (first == second || first.Fragment == null || second.Fragment == null ||
                        first.IsMagnetBubble || second.IsMagnetBubble ||
                        first.Fragment.IsRainbow || second.Fragment.IsRainbow ||
                        first.Fragment.PendingStickerReturn || second.Fragment.PendingStickerReturn ||
                        !first.CanMergeWith(second) || page.Merge.HasTargetConflict(first, second)) continue;
                    var merged = BubbleView.MergeResult(second.Fragment, first.Fragment.HeldPaths);
                    bool full = merged.Count == 1 && merged[0].Count == 0;
                    if (full != closure) continue;
                    source = first;
                    target = second;
                    break;
                }
                if (source != null) break;
            }
            if (source == null) throw new InvalidOperationException("No suitable " + (closure ? "full" : "partial") + " merge on the current board.");
            Directory.CreateDirectory(folder);
            string prefix = Path.Combine(folder, closure ? "puzzle-reward-closure" : "puzzle-reward-partial");
            File.WriteAllText(prefix + "-result.txt", "RUNNING " + DateTime.UtcNow.ToString("O"));
            running = true;
            page.StartCoroutine(Guard(Observe(page, source, target, layer, prefix, closure), prefix));
        }

        private static IEnumerator Guard(IEnumerator observation, string prefix)
        {
            try
            {
                while (true)
                {
                    bool next = false;
                    Exception failure = null;
                    try { next = observation.MoveNext(); }
                    catch (Exception exception) { failure = exception; }
                    if (failure != null)
                    {
                        File.WriteAllText(prefix + "-result.txt", "FAIL\n" + failure);
                        Debug.LogException(failure);
                        yield break;
                    }
                    if (!next) yield break;
                    yield return observation.Current;
                }
            }
            finally
            {
                (observation as IDisposable)?.Dispose();
                running = false;
            }
        }

        private static IEnumerator Observe(BubblePage page, BubbleView source, BubbleView target,
            RectTransform layer, string prefix, bool closure)
        {
            int round = page.RoundSeq;
            int collectedBefore = page.CollectedImgs.Count;
            float balanceBefore = ItemUtils.GetItemCount(E_ItemType.Dollar);
            float lastBalance = balanceBefore;
            int balanceChanges = 0, grants = 0, maximumFx = 0;
            bool seenFx = false, finished = false;
            bool sortingVerified = false;
            var presentationReport = new StringBuilder();
            double firstFxTime = -1;
            int nextCapture = 0;
            Vector3 lastWorld = target.transform.position;
            Vector2 expected = Vector2.zero, actual = Vector2.zero;
            float error = -1f;
            double start = Time.realtimeSinceStartupAsDouble;
            double idleSince = -1;
            void OnChanged(ItemEntry before, ItemEntry after)
            {
                if (after.Type == E_ItemType.Dollar && after.Count > before.Count) grants++;
            }
            BizzaEventSystem.On(EventDefine.Item.ItemChangedWithData, OnChanged);
            try
            {
                page.EmitMergeAttempted(true);
                source.ClearDragFlag();
                page.StartCoroutine(page.Merge.Run(source, target));
                while (Time.realtimeSinceStartupAsDouble - start < 12d && page != null && page.RoundSeq == round)
                {
                    if (target != null) lastWorld = target.transform.position;
                    float balance = ItemUtils.GetItemCount(E_ItemType.Dollar);
                    if (balance != lastBalance) { balanceChanges++; lastBalance = balance; }
                    int count = CountCurrencyFx(layer, out Transform fx);
                    maximumFx = Mathf.Max(maximumFx, count);
                    if (fx != null && !seenFx)
                    {
                        seenFx = true;
                        firstFxTime = Time.realtimeSinceStartupAsDouble;
                        expected = App.I.Cam.WorldToScreenPoint(lastWorld);
                        Canvas canvas = layer.GetComponentInParent<Canvas>().rootCanvas;
                        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                        actual = RectTransformUtility.WorldToScreenPoint(camera, fx.position);
                        error = Vector2.Distance(expected, actual);
                        sortingVerified = VerifyPresentationOrder(fx, presentationReport);
                        ScreenCapture.CaptureScreenshot(prefix + "-first-fx.png");
                    }
                    if (closure && seenFx && nextCapture < CaptureMilliseconds.Length &&
                        (Time.realtimeSinceStartupAsDouble - firstFxTime) * 1000d >= CaptureMilliseconds[nextCapture])
                    {
                        int requestedMs = CaptureMilliseconds[nextCapture++];
                        ScreenCapture.CaptureScreenshot(prefix + "-fx-" + requestedMs + "ms.png");
                        presentationReport.Append("\ncaptureRequestedMs=").Append(requestedMs)
                            .Append(" actualMs=").Append(((Time.realtimeSinceStartupAsDouble - firstFxTime) * 1000d).ToString("F1"));
                    }
                    if (!page.Merge.IsBusy && count == 0 && (!closure || (seenFx && nextCapture == CaptureMilliseconds.Length)))
                    {
                        if (idleSince < 0) idleSince = Time.realtimeSinceStartupAsDouble;
                        if (Time.realtimeSinceStartupAsDouble - idleSince >= 0.5d) { finished = true; break; }
                    }
                    else idleSince = -1;
                    yield return null;
                }
            }
            finally { BizzaEventSystem.Off(EventDefine.Item.ItemChangedWithData, OnChanged); }

            float delta = ItemUtils.GetItemCount(E_ItemType.Dollar) - balanceBefore;
            int collectedDelta = page != null ? page.CollectedImgs.Count - collectedBefore : -1;
            bool pass = finished && (closure
                ? delta > 0 && balanceChanges == 1 && grants == 1 && collectedDelta == 1 && seenFx && maximumFx == 1 && error <= 2f && sortingVerified
                : delta == 0 && balanceChanges == 0 && grants == 0 && collectedDelta == 0 && !seenFx);
            File.WriteAllText(prefix + "-result.txt", (pass ? "PASS" : "FAIL") +
                "\nclosure=" + closure + " finished=" + finished + " round=" + round +
                "\nbalanceBefore=" + balanceBefore + " balanceAfter=" + ItemUtils.GetItemCount(E_ItemType.Dollar) +
                " balanceDelta=" + delta + " balanceChanges=" + balanceChanges + " positiveGrantEvents=" + grants +
                "\ncollectedDelta=" + collectedDelta + " maximumActiveCurrencyFx=" + maximumFx +
                "\nexpectedScreenOrigin=" + expected.ToString("F3") + " actualScreenOrigin=" + actual.ToString("F3") +
                " originErrorPixels=" + error + "\nfxAboveCollectedImages=" + sortingVerified + presentationReport +
                "\nutc=" + DateTime.UtcNow.ToString("O"));
        }

        private static bool VerifyPresentationOrder(Transform fx, StringBuilder report)
        {
            Canvas fxCanvas = EffectiveSortingCanvas(fx);
            report.Append("\nfxSortingCanvas=").Append(DescribeCanvas(fxCanvas));
            if (fxCanvas == null) return false;
            bool valid = true;
            int imageCount = 0;
            foreach (Canvas image in App.I.HudRoot.GetComponentsInChildren<Canvas>(false))
            {
                if (!image.isActiveAndEnabled || (image.name != "FlyImage" && image.name != "ImageFly" &&
                    image.name != "WordFly" && image.name != "CategoryFly")) continue;
                imageCount++;
                Canvas imageCanvas = EffectiveSortingCanvas(image.transform);
                bool sameRoot = imageCanvas != null && imageCanvas.rootCanvas == fxCanvas.rootCanvas;
                bool sameRenderMode = imageCanvas != null && imageCanvas.renderMode == fxCanvas.renderMode;
                int fxLayer = SortingLayer.GetLayerValueFromID(fxCanvas.sortingLayerID);
                int imageLayer = imageCanvas != null ? SortingLayer.GetLayerValueFromID(imageCanvas.sortingLayerID) : 0;
                bool above = imageCanvas != null && (fxLayer > imageLayer ||
                    (fxLayer == imageLayer && fxCanvas.sortingOrder > imageCanvas.sortingOrder));
                report.Append("\ncollectedImage=").Append(image.name).Append(" sortingCanvas=")
                    .Append(DescribeCanvas(imageCanvas)).Append(" sameRoot=").Append(sameRoot)
                    .Append(" sameRenderMode=").Append(sameRenderMode).Append(" fxAbove=").Append(above);
                valid &= sameRoot && sameRenderMode && above;
            }
            report.Append("\nconcurrentCollectedImageCanvases=").Append(imageCount);
            return valid && imageCount > 0;
        }

        private static Canvas EffectiveSortingCanvas(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                Canvas canvas = current.GetComponent<Canvas>();
                if (canvas != null && canvas.isActiveAndEnabled && (canvas.overrideSorting || canvas.isRootCanvas))
                    return canvas;
            }
            return null;
        }

        private static string DescribeCanvas(Canvas canvas)
        {
            if (canvas == null) return "missing";
            return canvas.name + " root=" + canvas.rootCanvas.name + " renderMode=" + canvas.renderMode +
                " sortingLayer=" + SortingLayer.IDToName(canvas.sortingLayerID) + "(" + canvas.sortingLayerID + ")" +
                " sortingLayerValue=" + SortingLayer.GetLayerValueFromID(canvas.sortingLayerID) +
                " sortingOrder=" + canvas.sortingOrder + " overrideSorting=" + canvas.overrideSorting;
        }

        private static int CountCurrencyFx(RectTransform layer, out Transform first)
        {
            first = null;
            int count = 0;
            for (int i = 0; i < layer.childCount; i++)
            {
                Transform child = layer.GetChild(i);
                if (!child.gameObject.activeInHierarchy ||
                    (child.name != "ItemCollectFx_Dollar" && child.name != "ItemCollectFx_Gold" &&
                     child.name != "ItemCollectFx_WithDrawDanDollar")) continue;
                if (first == null) first = child;
                count++;
            }
            return count;
        }
    }
}
