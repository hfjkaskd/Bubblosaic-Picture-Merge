using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    // Observes live HUD geometry and temporarily exercises layout during a simulated bar offset.
    public static class BizzaHudLayoutInspection
    {
        private const float PixelTolerance = 2f;

        public static void Run(string folder)
        {
            var app = App.I;
            var page = BizzaGameplayBridge.Page;
            if (!EditorApplication.isPlaying || app == null || page == null ||
                BizzaGameplayBridge.IsInputBlocked || page.IsInteractionLocked() || page.Merge.IsBusy || page.Fly.IsFlying)
                throw new InvalidOperationException("Gameplay must be idle for HUD layout inspection.");
            Directory.CreateDirectory(folder);
            string prefix = Path.Combine(folder, "hud-layout");
            var report = new StringBuilder();
            bool pass = true;
            RectTransform top = page.TopBar.Root, toolbar = page.Toolbar.Root;
            Vector2 topRest = top.anchoredPosition, toolbarRest = toolbar.anchoredPosition;
            try
            {
                Canvas.ForceUpdateCanvases();
                Canvas canvas = app.HudRoot.GetComponentInParent<Canvas>().rootCanvas;
                report.Append("screen=").Append(Screen.width).Append('x').Append(Screen.height)
                    .Append(" safeArea=").Append(Screen.safeArea).Append(" design=")
                    .Append(DeviceLayout.ViewWidth).Append('x').Append(DeviceLayout.ViewHeight)
                    .Append("\nrootCanvas=").Append(canvas.name).Append(" scaleFactor=").Append(canvas.scaleFactor)
                    .Append(" hudScale=").Append(app.HudRoot.localScale.ToString("F4"))
                    .Append(" hudPivot=").Append(app.HudRoot.pivot.ToString("F3"));
                Rect hud = ScreenBounds(app.HudRoot);
                float viewportError = Mathf.Max(Mathf.Abs(hud.xMin), Mathf.Abs(hud.yMin),
                    Mathf.Abs(hud.xMax - Screen.width), Mathf.Abs(hud.yMax - Screen.height));
                pass &= viewportError <= PixelTolerance;
                report.Append("\nhudBounds=").Append(hud.ToString("F3")).Append(" viewportErrorPixels=").Append(viewportError);

                Rect target = ReportBounds("targetCard", page.TopBar.TargetCard, report);
                var moves = top.Find("Panels/MovesPanel/WhiteCard") as RectTransform;
                Rect movesBounds = ReportBounds("movesCard", moves, report);
                pass &= InViewport(target) && InViewport(movesBounds);
                Rect bar = ReportBounds("toolbarBackground", toolbar.Find("BarBg") as RectTransform, report);
                pass &= InViewport(bar);
                var panel = UIModule.Instance.GetPage<RealGamePanel>();
                int buttons = 0;
                foreach (var entry in panel.propEntries)
                {
                    Button button = entry.btn != null ? entry.btn : entry.GetComponentInChildren<Button>(false);
                    Graphic graphic = button != null ? button.targetGraphic : null;
                    if (graphic == null || !graphic.gameObject.activeInHierarchy) continue;
                    Rect bounds = ReportBounds("propButton" + buttons + ":" + graphic.name, graphic.rectTransform, report);
                    pass &= InViewport(bounds);
                    buttons++;
                }
                pass &= buttons == 3;
                report.Append("\npropButtonGraphics=").Append(buttons);
                var header = panel.GetComponentInChildren<GameplayHudHeader>(true);
                pass &= header != null && top.parent == header.StatusMount && header.Root.parent == app.HudRoot;
                if (header != null)
                {
                    Rect entryRow = ReportBounds("footerEntryRow", header.EntryRow, report);
                    Rect slot = VisibleScreenBounds(header.EntryRow.Find("SlotEnter"));
                    Rect gift = VisibleScreenBounds(header.EntryRow.Find("DailyMissionItem"));
                    Rect footer = Union(Union(slot, gift), bar);
                    Rect status = Union(target, movesBounds);
                    if (page.TopBar.Dolphin != null)
                        foreach (Renderer renderer in page.TopBar.Dolphin.GetComponentsInChildren<Renderer>(false))
                            if (renderer.enabled) status = Union(status, ScreenBounds(renderer.bounds, app.Cam));
                    bool entriesMounted = header.EntryRow.parent == toolbar;
                    bool entriesAligned = Mathf.Abs(entryRow.center.y - bar.center.y) <= PixelTolerance;
                    float slotGap = bar.xMin - slot.xMax;
                    float giftGap = gift.xMin - bar.xMax;
                    float footerGap = status.yMin - footer.yMax;
                    bool sideEntriesClear = slotGap >= 0f && giftGap >= 0f &&
                        !slot.Overlaps(bar) && !gift.Overlaps(bar);
                    pass &= entriesMounted && entriesAligned && sideEntriesClear && footerGap >= -PixelTolerance &&
                        InSafeArea(slot) && InSafeArea(gift) && InSafeArea(bar);
                    float toolbarScale = toolbar.localScale.y;
                    float bottomGap = bar.yMin - DeviceLayout.Current.SafeBottom * DeviceLayout.PixelsPerDesignUnit;
                    float expectedBottomGap = 16f * toolbarScale * DeviceLayout.PixelsPerDesignUnit;
                    pass &= Mathf.Abs(bottomGap - expectedBottomGap) <= PixelTolerance;
                    report.Append("\nentriesMountedToToolbar=").Append(entriesMounted).Append(" entriesAligned=").Append(entriesAligned)
                        .Append(" sideEntriesClear=").Append(sideEntriesClear)
                        .Append("\nslotBounds=").Append(slot.ToString("F3")).Append(" giftBounds=").Append(gift.ToString("F3"))
                        .Append(" slotBarGapPixels=").Append(slotGap).Append(" barGiftGapPixels=").Append(giftGap)
                        .Append(" headerFooterGapPixels=").Append(footerGap)
                        .Append("\nsharedHeaderScale=").Append(header.LayoutScale).Append(" toolbarScale=").Append(toolbarScale)
                        .Append(" toolbarSafeBottomGap=").Append(bottomGap).Append(" expectedBottomGap=").Append(expectedBottomGap);
                }
                ReportCurrencyBounds(UIModule.Instance, target, movesBounds, report);
                float exitDistance = page.TopBar.GetExitSlideDistance(topRest.y);

                top.anchoredPosition = topRest + new Vector2(0f, 210f);
                page.TopBar.SetHudBarOffset(210f);
                toolbar.anchoredPosition = toolbarRest + new Vector2(0f, -160f);
                Vector2 topOffset = top.anchoredPosition, toolbarOffset = toolbar.anchoredPosition;
                for (int i = 0; i < 2; i++)
                {
                    page.TopBar.ApplyDeviceLayout();
                    page.Toolbar.ApplyDeviceLayout();
                    float expectedToolbarHeight = ToolbarView.BAR_HEIGHT + DeviceLayout.Current.SafeBottom / toolbar.localScale.y;
                    bool stable = Mathf.Abs(top.rect.height - page.TopBar.LayoutHeight) <= 0.01f &&
                        Mathf.Abs(toolbar.rect.height - expectedToolbarHeight) <= 0.01f &&
                        Vector2.Distance(top.anchoredPosition, topOffset) <= 0.01f &&
                        Vector2.Distance(toolbar.anchoredPosition, toolbarOffset) <= 0.01f;
                    pass &= stable;
                    report.Append("\nlayoutPass=").Append(i + 1).Append(" stable=").Append(stable)
                        .Append(" topHeight=").Append(top.rect.height).Append(" topPosition=").Append(top.anchoredPosition)
                        .Append(" toolbarHeight=").Append(toolbar.rect.height).Append(" toolbarPosition=").Append(toolbar.anchoredPosition);
                }
                float offsetExitDistance = page.TopBar.GetExitSlideDistance(topRest.y);
                bool stableExitDistance = Mathf.Abs(exitDistance - offsetExitDistance) <= 0.01f;
                top.anchoredPosition = topRest + Vector2.up * exitDistance;
                page.TopBar.SetHudBarOffset(exitDistance);
                Canvas.ForceUpdateCanvases();
                Rect targetExit = ScreenBounds(page.TopBar.TargetCard);
                Rect movesExit = ScreenBounds(moves);
                bool cardsOffscreen = targetExit.yMin >= Screen.height - PixelTolerance &&
                    movesExit.yMin >= Screen.height - PixelTolerance;
                pass &= stableExitDistance && cardsOffscreen;
                report.Append("\nexitDistance=").Append(exitDistance).Append(" offsetExitDistance=").Append(offsetExitDistance)
                    .Append(" stableExitDistance=").Append(stableExitDistance).Append(" cardsOffscreen=").Append(cardsOffscreen)
                    .Append(" targetExitBounds=").Append(targetExit.ToString("F3"))
                    .Append(" movesExitBounds=").Append(movesExit.ToString("F3"));
            }
            catch (Exception exception)
            {
                pass = false;
                report.Append("\nexception=").Append(exception);
            }
            finally
            {
                top.anchoredPosition = topRest;
                toolbar.anchoredPosition = toolbarRest;
                page.TopBar.SetHudBarOffset(0f);
                page.TopBar.ApplyDeviceLayout();
                page.Toolbar.ApplyDeviceLayout();
                Canvas.ForceUpdateCanvases();
            }
            report.Append("\nrestoredTopPosition=").Append(top.anchoredPosition)
                .Append(" restoredToolbarPosition=").Append(toolbar.anchoredPosition)
                .Append("\nutc=").Append(DateTime.UtcNow.ToString("O"));
            File.WriteAllText(prefix + "-result.txt", (pass ? "PASS\n" : "FAIL\n") + report);
            ScreenCapture.CaptureScreenshot(prefix + "-restored.png");
        }

        private static Rect ReportBounds(string name, RectTransform rect, StringBuilder report)
        {
            if (rect == null) throw new InvalidOperationException("Missing HUD graphic: " + name);
            Rect bounds = ScreenBounds(rect);
            report.Append('\n').Append(name).Append(" bounds=").Append(bounds.ToString("F3"))
                .Append(" inViewport=").Append(InViewport(bounds));
            return bounds;
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
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect ScreenBounds(Bounds bounds, Camera camera)
        {
            Vector3 min = camera.WorldToScreenPoint(bounds.min), max = camera.WorldToScreenPoint(bounds.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect VisibleScreenBounds(Transform root)
        {
            if (root == null) throw new InvalidOperationException("An authored HUD side entry is missing.");
            bool found = false;
            Rect result = new Rect();
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.enabled || graphic.color.a <= 0.001f || graphic.canvasRenderer.GetAlpha() <= 0.001f) continue;
                Rect next = ScreenBounds(graphic.rectTransform);
                result = found ? Union(result, next) : next;
                found = true;
            }
            if (!found) throw new InvalidOperationException("No visible side-entry graphics: " + root.name);
            return result;
        }

        private static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        private static bool InSafeArea(Rect bounds)
        {
            Rect safe = DeviceLayout.Current.SafeAreaPixels;
            return bounds.width > 0f && bounds.height > 0f && bounds.xMin >= safe.xMin - PixelTolerance &&
                bounds.yMin >= safe.yMin - PixelTolerance && bounds.xMax <= safe.xMax + PixelTolerance &&
                bounds.yMax <= safe.yMax + PixelTolerance;
        }

        private static bool InViewport(Rect bounds) => bounds.width > 0f && bounds.height > 0f &&
            bounds.xMin >= -PixelTolerance && bounds.yMin >= -PixelTolerance &&
            bounds.xMax <= Screen.width + PixelTolerance && bounds.yMax <= Screen.height + PixelTolerance;

        private static void ReportCurrencyBounds(UIModule ui, Rect target, Rect moves, StringBuilder report)
        {
            foreach (CurrencyBar currency in ui.GetComponentsInChildren<CurrencyBar>(false))
            {
                // Image bounds exclude transient floating reward text; overlap stays a visual review item.
                foreach (Image graphic in currency.GetComponentsInChildren<Image>(false))
                {
                    if (!graphic.enabled || graphic.color.a <= 0.001f) continue;
                    Rect bounds = ScreenBounds(graphic.rectTransform);
                    report.Append("\ncurrencyImage=").Append(graphic.name).Append(" bounds=").Append(bounds.ToString("F3"))
                        .Append(" overlapsTarget=").Append(bounds.Overlaps(target))
                        .Append(" overlapsMoves=").Append(bounds.Overlaps(moves));
                }
            }
        }
    }
}
