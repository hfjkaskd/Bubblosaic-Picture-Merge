using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        public static async void ReviewWithdrawalGuide(string folder)
        {
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            File.WriteAllText(Path.Combine(folder, "inspection.txt"), "RUNNING");
            string locale = Localization.CurrentLocale;
            int width = Screen.width, height = Screen.height;
            float gold = ItemUtils.GetItemCount(E_ItemType.Gold), cash = ItemUtils.GetItemCount(E_ItemType.Dollar);
            int withdrawClicks = SaveDataUtils.GameData.btnWithdrawClick;
            try
            {
                if (!EditorApplication.isPlaying || BizzaGameplayBridge.Page == null)
                    throw new InvalidOperationException("Production gameplay must be ready.");
                CloseRuntime();
                Localization.SetLocale("pt_BR");
                foreach (int viewHeight in new[] { 2400, 1920 })
                {
                    PlayModeWindow.SetCustomRenderingResolution(1080, (uint)viewHeight, "Withdrawal guide review");
                    await UniTask.DelayFrame(12);
                    await CheckStep("withdraw-cash", "Teach_AllMoney", "cash-entry", viewHeight);
                    inspectedParent = await UIModule.Instance.OpenPage(UIPageIds.FakeWithdrawPanel);
                    await UniTask.Delay(1500, ignoreTimeScale: true);
                    await CheckStep("withdraw-starter", "Teach_Withdraw", "withdraw", viewHeight);
                    CloseRuntime();
                    await UniTask.DelayFrame(5);
                    await CheckStep("withdraw-coins", "Teach_coin", "coin-entry", viewHeight);
                }
                if (gold != ItemUtils.GetItemCount(E_ItemType.Gold) || cash != ItemUtils.GetItemCount(E_ItemType.Dollar)
                    || withdrawClicks != SaveDataUtils.GameData.btnWithdrawClick)
                    throw new InvalidOperationException("Review changed account state.");
                report.AppendLine("PASS No payout, withdrawal click, balance change or tutorial save change submitted.");
                report.AppendLine("Failures=0");
            }
            catch (Exception e) { report.AppendLine("FAIL " + e); Debug.LogException(e); }
            finally
            {
                UIModule.Instance.ClosePage(UIPageIds.UI_TeachTip);
                UIModule.Instance.ClosePage(UIPageIds.UI_TeachMask);
                CloseRuntime(); Localization.SetLocale(locale);
                PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Game");
                File.WriteAllText(Path.Combine(folder, "inspection.txt"), report.ToString());
            }

            async UniTask CheckStep(string marker, string textKey, string name, int viewHeight)
            {
                var target = GameObjUitl.FindObj("guide:" + marker);
                if (target == null) throw new InvalidOperationException("Marker not found: " + marker);
                var button = target.GetComponent<Button>();
                if (button == null || button.targetGraphic.gameObject != target)
                    throw new InvalidOperationException("Target is not the visible native Button: " + marker);
                var mask = (UITeachMaskPage)await UIModule.Instance.OpenPage(UIPageIds.UI_TeachMask,
                    new UITeachMaskPage.InitParam { type = UITeachMaskPage.Type.Path, path = "guide:" + marker,
                        width = -1, height = -1, alpha = 100, block = true, showHand = true,
                        retryCount = 5, retryInterval = .3f });
                var tip = (UITeachTipsPage)await UIModule.Instance.OpenPage(UIPageIds.UI_TeachTip,
                    new UITeachTipsPage.InitParam { content = textKey, posIdx = -1, heightValue = .2f });
                await UniTask.Delay(250, ignoreTimeScale: true);
                Validate();
                await PayPalCapture(folder, name + "-1080x" + viewHeight + ".png");
                if (name == "withdraw")
                {
                    var rect = (RectTransform)target.transform;
                    var saved = rect.anchoredPosition;
                    try { rect.anchoredPosition += Vector2.up * 35; await UniTask.DelayFrame(4); Validate(); }
                    finally { rect.anchoredPosition = saved; }
                }
                UIModule.Instance.ClosePage(UIPageIds.UI_TeachTip);
                UIModule.Instance.ClosePage(UIPageIds.UI_TeachMask);
                await UniTask.DelayFrame(3);

                void Validate()
                {
                    Rect expected = Bounds((RectTransform)target.transform), actual = Bounds(mask.maskParent);
                    Rect bubble = Bounds(tip.panel.rectTransform);
                    if (!mask.TargetReady || Vector2.Distance(expected.center, actual.center) > 2
                        || Vector2.Distance(expected.size, actual.size) > 2)
                        throw new InvalidOperationException("Highlight mismatch: " + name + " " + expected + " / " + actual);
                    if (bubble.Overlaps(expected) || bubble.xMin < -1 || bubble.xMax > Screen.width + 1
                        || bubble.yMin < -1 || bubble.yMax > Screen.height + 1)
                        throw new InvalidOperationException("Tip overlaps target or screen edge: " + name + " " + bubble);
                    if (!HitStandard(button)) throw new InvalidOperationException("Button is obscured: " + name);
                    tip.content.ForceMeshUpdate();
                    if (tip.content.isTextOverflowing) throw new InvalidOperationException("Tip text overflows: " + name);
                    report.AppendLine("PASS " + name + " 1080x" + viewHeight + " highlight=" + actual + " tip=" + bubble);
                }
            }
        }

        static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            foreach (var point in corners)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, point);
                min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
