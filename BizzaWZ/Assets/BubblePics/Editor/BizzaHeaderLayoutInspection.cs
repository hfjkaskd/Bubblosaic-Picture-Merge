using System;
using System.Collections.Generic;
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
    // Isolated prefab authoring preview. Does not enter Play Mode, open pages or access account/save data.
    public static class BizzaHeaderLayoutInspection
    {
        const string WidgetPath = "Assets/BizzaWZ/Final/Real/UI/GamePanel/Resources/Resources/GameUiWidget.prefab";
        const string TopPath = "Assets/BubblePics/RuntimePrefabs/UI/TopGameBar.prefab";
        const string ToolbarPath = "Assets/BubblePics/RuntimePrefabs/UI/BubbleToolbar.prefab";
        const string PropPath = "Assets/BubblePics/Resources/Prefabs/FrameworkBubbleProp.prefab";
        const float Tolerance = 2f;

        public static void Render(string folder)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before the header prefab preview.");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            bool pass = true;
            pass &= RenderCase(folder, "1080x2400", 1080, 2400, new Rect(0, 0, 1080, 2400), report);
            pass &= RenderCase(folder, "1080x1920", 1080, 1920, new Rect(0, 0, 1080, 1920), report);
            pass &= RenderCase(folder, "720x1600", 720, 1600, new Rect(0, 0, 720, 1600), report);
            pass &= RenderCase(folder, "1080x2400-safe", 1080, 2400,
                Rect.MinMaxRect(60, 90, 1020, 2280), report);
            report.Append("\nScope: authentic prefabs and runtime layout methods in an isolated preview scene.")
                .Append(" BR gift branch, sample values and locked prop state are visual preview data only.")
                .Append(" Portrait uses its runtime Spine loader and idle pose, with the preview camera coordinate conversion.")
                .Append(" Gameplay initialization, animations, callbacks and touch delivery are not exercised.")
                .Append("\nutc=").Append(DateTime.UtcNow.ToString("O"));
            File.WriteAllText(Path.Combine(folder, "header-layout-result.txt"), (pass ? "PASS\n" : "FAIL\n") + report);
            if (!pass) throw new InvalidOperationException("Header preview failed; see header-layout-result.txt.");
        }

        static bool RenderCase(string folder, string name, int width, int height, Rect safeArea, StringBuilder report)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(width, height, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D capture = null;
            report.Append("\n\ncase=").Append(name);
            try
            {
                DeviceLayoutMetrics layout = DeviceLayout.Compute(width, height, safeArea);
                report.Append(" viewport=").Append(layout.ViewWidth).Append('x').Append(layout.ViewHeight)
                    .Append(" pixelsPerDesignUnit=").Append(layout.PixelsPerDesignUnit)
                    .Append(" safePixels=").Append(safeArea.ToString("F2"));
                Camera camera = CreateCamera(scene, target, layout);
                RectTransform hud = CreateCanvas(scene, camera, layout);
                CreateBackground(scene, layout);

                GameObject widget = InstantiatePrefab(WidgetPath, scene);
                GameplayHudHeader header = widget.GetComponentInChildren<GameplayHudHeader>(true);
                if (header == null) throw new InvalidOperationException("GameUiWidget has no GameplayHudHeader.");
                header.Root.SetParent(hud, false);
                widget.SetActive(false);
                header.gameObject.SetActive(true);
                TopGameBar top = InstantiatePrefab(TopPath, scene).GetComponent<TopGameBar>();
                if (top == null) throw new InvalidOperationException("TopGameBar component is missing.");
                top.MountHeader(header);
                header.ApplyDeviceLayout(layout);
                top.ApplyDeviceLayout();
                SetPreviewValues(header, top);
                PreparePortrait(top, header, layout);

                ToolbarView toolbar = InstantiatePrefab(ToolbarPath, scene).GetComponent<ToolbarView>();
                if (toolbar == null || toolbar.PropRoot == null)
                    throw new InvalidOperationException("Toolbar prefab references are missing.");
                toolbar.Root.SetParent(hud, false);
                toolbar.ApplyDeviceLayout(layout);
                header.MountEntries(toolbar);
                var tools = new List<Button>();
                for (int i = 0; i < 3; i++)
                {
                    GameObject prop = InstantiatePrefab(PropPath, scene);
                    prop.transform.SetParent(toolbar.PropRoot, false);
                    prop.transform.SetSiblingIndex(i);
                    ToolButton view = prop.GetComponent<ToolButton>();
                    view.InitializePrefabRuntime(ToolDef.All[i]);
                    view.Refresh(1, 0, false, false, true);
                    tools.Add(prop.GetComponent<UIPropEntry>().btn);
                }
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(toolbar.PropRoot);
                foreach (TMP_Text label in hud.GetComponentsInChildren<TMP_Text>(false))
                    label.ForceMeshUpdate(true, true);
                Canvas.ForceUpdateCanvases();

                bool pass = Verify(header, top, toolbar, tools, camera, layout, report);
                camera.Render();
                RenderTexture.active = target;
                capture = new Texture2D(width, height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                capture.Apply();
                File.WriteAllBytes(Path.Combine(folder, "header-" + name + ".png"), capture.EncodeToPNG());
                report.Append("\ncaseResult=").Append(pass ? "PASS" : "FAIL");
                return pass;
            }
            catch (Exception exception)
            {
                report.Append("\ncaseResult=FAIL\n").Append(exception);
                return false;
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

        static Camera CreateCamera(Scene scene, RenderTexture target, DeviceLayoutMetrics layout)
        {
            var go = new GameObject("Header preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.GetComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = layout.ViewHeight * 0.5f;
            camera.aspect = (float)layout.ScreenWidthPixels / layout.ScreenHeightPixels;
            camera.transform.position = new Vector3(0, 0, -100);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.16f, 0.35f);
            camera.targetTexture = target;
            return camera;
        }

        static RectTransform CreateCanvas(Scene scene, Camera camera, DeviceLayoutMetrics layout)
        {
            var go = new GameObject("Header preview HUD", typeof(Canvas));
            SceneManager.MoveGameObjectToScene(go, scene);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            canvas.sortingOrder = 100;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(layout.ViewWidth, layout.ViewHeight);
            return rect;
        }

        static void CreateBackground(Scene scene, DeviceLayoutMetrics layout)
        {
            Sprite sprite = AssetLib.Sprite("Art/Sprites/PsdSkin20260807/Gameplay/background");
            if (sprite == null) throw new InvalidOperationException("Gameplay background resource is missing.");
            var go = new GameObject("Gameplay background preview", typeof(SpriteRenderer));
            SceneManager.MoveGameObjectToScene(go, scene);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -100;
            float scale = Mathf.Max(layout.ViewWidth / sprite.bounds.size.x, layout.ViewHeight / sprite.bounds.size.y);
            go.transform.localScale = Vector3.one * scale;
            go.transform.position = -sprite.bounds.center * scale;
        }

        static GameObject InstantiatePrefab(string path, Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            // Edit Mode forbids reparenting children while the preview instance is connected.
            // Unpack this disposable instance so the normal runtime mounts can run unchanged.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            return instance;
        }

        static void SetPreviewValues(GameplayHudHeader header, TopGameBar top)
        {
            CurrencyBar currency = header.CurrencyRow.GetComponent<CurrencyBar>();
            if (currency == null) throw new InvalidOperationException("Header currency reference is missing.");
            currency.coinTxt.text = "0,00";
            currency.dollarTxt.text = "51.5";
            currency.clashTxt.text = "\u2248 R$0,00";
            currency.curLevelTxt.text = "1";
            currency.addCoinTxt.gameObject.SetActive(false);
            currency.addDollarTxt.gameObject.SetActive(false);
            Transform daily = header.EntryRow.Find("DailyMissionItem/DailyMission");
            Transform badge = header.EntryRow.Find("DailyMissionItem/Badge");
            if (daily == null || badge == null) throw new InvalidOperationException("Gift country variants are missing.");
            // BR (country value 2) selects DailyMission; ItemForCountry normally hides the badge branch.
            daily.gameObject.SetActive(true);
            badge.gameObject.SetActive(false);
            SetText(top.Root, "Panels/TargetPanel/WhiteCard/Banner", "Alvo");
            SetText(top.Root, "Panels/TargetPanel/WhiteCard/Num", "0/4");
            SetText(top.Root, "Panels/MovesPanel/WhiteCard/Banner", "Movimentos");
            SetText(top.Root, "Panels/MovesPanel/WhiteCard/Num", "\u221e");
            Transform reward = top.Root.Find("Panels/TargetPanel/RewardCoin");
            if (reward != null) reward.gameObject.SetActive(false);
        }

        static void SetText(Transform root, string path, string value)
        {
            TMP_Text label = root.Find(path)?.GetComponent<TMP_Text>();
            if (label == null) throw new InvalidOperationException("Missing preview label: " + path);
            label.text = value;
        }

        static void PreparePortrait(TopGameBar top, GameplayHudHeader header, DeviceLayoutMetrics layout)
        {
            DolphinDecoration portrait = top.Dolphin;
            if (portrait == null) throw new InvalidOperationException("TopGameBar portrait is missing.");
            // Read the authored offset through Unity serialization, without reflection or a new runtime branch.
            var serialized = new SerializedObject(top);
            Vector2 offset = serialized.FindProperty("_portraitOffset").vector2Value;
            var panels = (RectTransform)top.Root.Find("Panels");
            Vector2 design = header.StatusToDesign(new Vector2(offset.x, -panels.offsetMax.y + offset.y));
            Vector3 world = new Vector3(design.x - layout.ViewWidth * 0.5f, layout.ViewHeight * 0.5f - design.y, 0);
            portrait.BindPrefabRuntime();
            portrait.ApplyHudLayout(world, header.LayoutScale);
            portrait.SetSortingOrder(101);
            var idle = portrait.Spine.SetAnimation("idle", true, 0f);
            if (idle == null) throw new InvalidOperationException("Portrait idle animation is missing.");
            idle.TrackTime = 0f;
            idle.TimeScale = 0f;
            portrait.Spine.ApplyAndRender();
        }

        static bool Verify(GameplayHudHeader header, TopGameBar top, ToolbarView toolbar, List<Button> tools,
            Camera camera, DeviceLayoutMetrics layout, StringBuilder report)
        {
            bool pass = true;
            Rect safe = layout.SafeAreaPixels;
            Rect currency = VisibleBounds(header.CurrencyRow, camera);
            Rect target = Bounds(top.TargetCard, camera);
            Rect moves = Bounds((RectTransform)top.Root.Find("Panels/MovesPanel/WhiteCard"), camera);
            Rect status = Union(target, moves);
            foreach (Renderer renderer in top.Dolphin.GetComponentsInChildren<Renderer>(false))
                if (renderer.enabled) status = Union(status, Bounds(renderer.bounds, camera));
            Rect entries = VisibleBounds(header.EntryRow, camera);
            Rect slot = VisibleBounds(header.EntryRow.Find("SlotEnter"), camera);
            Rect gift = VisibleBounds(header.EntryRow.Find("DailyMissionItem"), camera);
            foreach (Graphic graphic in header.EntryRow.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.enabled || graphic.color.a <= 0.001f || graphic.canvasRenderer.GetAlpha() <= 0.001f) continue;
                Rect bounds = Bounds(graphic.rectTransform, camera);
                report.Append("\nentryGraphic=").Append(graphic.transform.parent.name).Append('/').Append(graphic.name)
                    .Append(" bounds=").Append(bounds.ToString("F2"));
            }
            Rect bar = Bounds(toolbar.PropRoot, camera);
            Rect footer = Union(entries, bar);
            foreach (var row in new[] { new NamedRect("currency", currency), new NamedRect("status", status),
                new NamedRect("slot", slot), new NamedRect("gift", gift), new NamedRect("toolbar", bar) })
            {
                bool inside = Contains(safe, row.Rect);
                pass &= inside;
                report.Append('\n').Append(row.Name).Append('=').Append(row.Rect.ToString("F2")).Append(" inSafeArea=").Append(inside);
            }
            float currencyGap = currency.yMin - status.yMax;
            float footerGap = status.yMin - footer.yMax;
            float slotGap = bar.xMin - slot.xMax;
            float giftGap = gift.xMin - bar.xMax;
            bool entriesMounted = header.EntryRow.parent == toolbar.Root;
            bool entriesAligned = Mathf.Abs(Bounds(header.EntryRow, camera).center.y - bar.center.y) <= Tolerance;
            bool sideEntriesClear = slotGap >= 0f && giftGap >= 0f &&
                !slot.Overlaps(bar) && !gift.Overlaps(bar);
            pass &= entriesMounted && entriesAligned && sideEntriesClear &&
                currencyGap >= -Tolerance && footerGap >= -Tolerance;
            float expectedTop = (layout.SafeTop + 24f * header.LayoutScale) * layout.PixelsPerDesignUnit;
            float actualTop = layout.ScreenHeightPixels - Bounds(header.Root, camera).yMax;
            float toolbarScale = toolbar.Root.localScale.y;
            float bottomPadding = bar.yMin - safe.yMin;
            float expectedBottomPadding = 16f * toolbarScale * layout.PixelsPerDesignUnit;
            float expectedToolbarHeight = ToolbarView.BAR_HEIGHT + layout.SafeBottom / toolbarScale;
            pass &= Mathf.Abs(actualTop - expectedTop) <= Tolerance &&
                Mathf.Abs(bottomPadding - expectedBottomPadding) <= Tolerance &&
                Mathf.Abs(toolbar.Root.rect.height - expectedToolbarHeight) <= 0.01f;
            report.Append("\nentriesMountedToToolbar=").Append(entriesMounted).Append(" entriesAligned=").Append(entriesAligned)
                .Append(" sideEntriesClear=").Append(sideEntriesClear)
                .Append("\ncurrencyGapPixels=").Append(currencyGap).Append(" headerFooterGapPixels=").Append(footerGap)
                .Append(" slotBarGapPixels=").Append(slotGap).Append(" barGiftGapPixels=").Append(giftGap)
                .Append(" headerScale=").Append(header.LayoutScale).Append(" headerTopPixels=").Append(actualTop)
                .Append(" expectedTopPixels=").Append(expectedTop).Append(" toolbarScale=").Append(toolbarScale)
                .Append(" toolbarHeight=").Append(toolbar.Root.rect.height).Append(" expectedToolbarHeight=").Append(expectedToolbarHeight)
                .Append(" toolbarBottomPaddingPixels=").Append(bottomPadding).Append(" expectedBottomPaddingPixels=").Append(expectedBottomPadding);

            CurrencyBar currencyView = header.CurrencyRow.GetComponent<CurrencyBar>();
            var buttons = new List<Button>
            {
                currencyView.coinBtn.GetComponent<Button>(),
                currencyView.dollarBtn.GetComponent<Button>(),
                currencyView.settingBtn.GetComponent<Button>()
            };
            foreach (Button button in header.EntryRow.GetComponentsInChildren<Button>(false)) buttons.Add(button);
            buttons.AddRange(tools);
            int index = 0;
            foreach (Button button in buttons)
            {
                Graphic graphic = button != null ? button.targetGraphic : null;
                bool valid = graphic != null && graphic.enabled && graphic.gameObject.activeInHierarchy &&
                    graphic.raycastTarget && (graphic.transform == button.transform || graphic.transform.IsChildOf(button.transform));
                Rect bounds = graphic != null ? Bounds(graphic.rectTransform, camera) : new Rect();
                valid &= Contains(safe, bounds) && bounds.width >= 48f * layout.PixelsPerDesignUnit &&
                    bounds.height >= 48f * layout.PixelsPerDesignUnit;
                pass &= valid;
                report.Append("\nbutton[").Append(index++).Append("]=").Append(button != null ? button.name : "missing")
                    .Append(" bounds=").Append(bounds.ToString("F2")).Append(" standardButtonAndGraphic=").Append(valid);
            }
            pass &= buttons.Count >= 8;
            for (int i = 0; i < tools.Count; i++)
            {
                Rect a = Bounds(tools[i].targetGraphic.rectTransform, camera);
                pass &= Contains(bar, a);
                for (int j = i + 1; j < tools.Count; j++)
                    pass &= !a.Overlaps(Bounds(tools[j].targetGraphic.rectTransform, camera));
            }
            report.Append("\nbuttonCount=").Append(buttons.Count).Append(" propCount=").Append(tools.Count);
            return pass;
        }

        readonly struct NamedRect
        {
            public readonly string Name;
            public readonly Rect Rect;
            public NamedRect(string name, Rect rect) { Name = name; Rect = rect; }
        }

        static Rect VisibleBounds(Transform root, Camera camera)
        {
            if (root == null) throw new InvalidOperationException("An authored HUD row or side entry is missing.");
            bool found = false;
            Rect result = new Rect();
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(false))
            {
                if (!graphic.enabled || graphic.color.a <= 0.001f || graphic.canvasRenderer.GetAlpha() <= 0.001f) continue;
                Rect next = Bounds(graphic.rectTransform, camera);
                result = found ? Union(result, next) : next;
                found = true;
            }
            if (!found) throw new InvalidOperationException("No visible row graphics: " + root.name);
            return result;
        }

        static Rect Bounds(RectTransform rect, Camera camera)
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

        static Rect Bounds(Bounds bounds, Camera camera)
        {
            Vector3 min = camera.WorldToScreenPoint(bounds.min), max = camera.WorldToScreenPoint(bounds.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
            Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));

        static bool Contains(Rect outer, Rect inner) => inner.width > 0 && inner.height > 0 &&
            inner.xMin >= outer.xMin - Tolerance && inner.yMin >= outer.yMin - Tolerance &&
            inner.xMax <= outer.xMax + Tolerance && inner.yMax <= outer.yMax + Tolerance;
    }
}
