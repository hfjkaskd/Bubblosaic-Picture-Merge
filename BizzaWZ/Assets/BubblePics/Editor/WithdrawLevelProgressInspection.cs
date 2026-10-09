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
        public static async void ReviewWithdrawLevelProgress(string folder)
        {
            if (!EditorApplication.isPlaying || BizzaGameplayBridge.Page == null)
                throw new InvalidOperationException("Production gameplay startup must finish first.");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            File.WriteAllText(Path.Combine(folder, "inspection.txt"), "RUNNING");
            float gold = ItemUtils.GetItemCount(E_ItemType.Gold), cash = ItemUtils.GetItemCount(E_ItemType.Dollar);
            int level = SaveDataUtils.GameData.playerSelectedLv;
            RealWithdrawPanel page = null;
            void Check(bool ok, string message)
            {
                report.AppendLine((ok ? "PASS " : "FAIL ") + message);
                if (!ok) throw new InvalidOperationException(message);
            }
            try
            {
                page = (RealWithdrawPanel)await UIModule.Instance.OpenPage(UIPageIds.RealWithdrawPanel);
                for (int i = 0; i < 150 && page.withdrawLevelRoot.GetComponentsInChildren<WithdrawLevelItem>().Length == 0; i++)
                    await UniTask.Delay(100, ignoreTimeScale: true);
                await UniTask.Delay(1200, ignoreTimeScale: true);
                var tiers = page.withdrawLevelRoot.GetComponentsInChildren<WithdrawLevelItem>();
                Check(tiers.Length > 0 && page.gameObject.activeInHierarchy, "Production withdrawal data loaded");
                var scroll = page.GetComponentInChildren<ScrollRect>();
                var fill = page.progressBar;
                Check(fill.type == Image.Type.Sliced && fill.GetComponent<WithdrawCloudProgressFill>() != null,
                    "Production prefab uses sliced progress with preserved caps");
                Check(fill.sprite != null && fill.sprite.border.x > 0 && fill.sprite.border.z > 0, "Lazy-loaded sprite has both end caps");
                // Select an actual future tier using its normal Button, without withdrawing.
                foreach (var tier in tiers)
                {
                    if (tier.StartLevel <= level) continue;
                    var button = tier.GetComponentInChildren<Button>();
                    PressStandard(button);
                    break;
                }
                await UniTask.DelayFrame(4);
                Canvas.ForceUpdateCanvases();
                if (scroll != null) scroll.verticalNormalizedPosition = 0;
                await UniTask.DelayFrame(4);
                Check(page.progressObj.activeInHierarchy, "Future-tier selection shows progress");
                var numbers = page.progressTxt.text.Split('/');
                Check(numbers.Length == 2 && Mathf.Abs(fill.fillAmount - float.Parse(numbers[0]) / float.Parse(numbers[1])) < .001f,
                    "Live level counter and fill ratio agree: " + page.progressTxt.text);
                await PayPalCapture(folder, "Unity-Live.png");
                float amountBefore = fill.fillAmount;
                string textBefore = page.progressTxt.text;
                try
                {
                    foreach (int value in new[] { 0, 1, 10, 25, 50 })
                    {
                        fill.fillAmount = value / 50f;
                        page.progressTxt.text = value + "/50";
                        await UniTask.DelayFrame(4);
                        Canvas.ForceUpdateCanvases();
                        var mesh = fill.canvasRenderer.GetMesh();
                        mesh.RecalculateBounds();
                        float expected = fill.GetPixelAdjustedRect().width * value / 50f;
                        Check(value == 0 ? mesh.vertexCount == 0 : Mathf.Abs(mesh.bounds.size.x - expected) < 2f,
                            "Rendered width agrees with " + value + "/50");
                        if (value >= 10)
                        {
                            float capWidth = fill.sprite.border.x / (fill.pixelsPerUnit * fill.pixelsPerUnitMultiplier);
                            var vertices = mesh.vertices;
                            bool preserved = false;
                            foreach (var vertex in vertices)
                                if (Mathf.Abs(vertex.x - fill.GetPixelAdjustedRect().xMin - capWidth) < .1f) preserved = true;
                            Check(preserved, "Left cap retains its authored width at " + value + "/50");
                        }
                        await PayPalCapture(folder, "Preview-" + value + "of50.png");
                    }
                }
                finally { fill.fillAmount = amountBefore; page.progressTxt.text = textBefore; }
                var close = page.transform.Find("Title/CloseBtn").GetComponentInChildren<Button>();
                Check(HitStandard(close), "Native back Button remains reachable");
                PressStandard(close);
                await UniTask.DelayFrame(3);
                Check(!UIModule.Instance.PageIsOpen(UIPageIds.RealWithdrawPanel), "Back closes the page");
            }
            catch (Exception exception) { report.AppendLine("FAIL " + exception); Debug.LogException(exception); }
            finally
            {
                if (page != null && UIModule.Instance.PageIsOpen(UIPageIds.RealWithdrawPanel))
                    UIModule.Instance.ClosePage(UIPageIds.RealWithdrawPanel);
                report.AppendLine((gold == ItemUtils.GetItemCount(E_ItemType.Gold) && cash == ItemUtils.GetItemCount(E_ItemType.Dollar)
                    && level == SaveDataUtils.GameData.playerSelectedLv ? "PASS " : "FAIL ") + "Balances and saved level unchanged");
                report.AppendLine("Preview images change only the progress display; Unity-Live uses production level data.");
                File.WriteAllText(Path.Combine(folder, "inspection.txt"), report.ToString());
            }
        }
    }
}
