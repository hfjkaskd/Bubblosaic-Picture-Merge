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
        static async void ReviewSlotEntryProgress()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation/SlotEntryProgress-Fit-20260929"));
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "inspection.txt");
            File.WriteAllText(file, "RUNNING " + DateTime.UtcNow.ToString("O"));
            var report = new StringBuilder();
            int failures = 0;
            void Check(bool ok, string message) { report.AppendLine((ok ? "PASS " : "FAIL ") + message); if (!ok) failures++; }
            SlotEnter slot = null;
            try
            {
                if (!EditorApplication.isPlaying || BizzaGameplayBridge.Page == null)
                    throw new InvalidOperationException("Normal gameplay startup must finish first.");
                slot = UIModule.Instance.GetPage<RealGamePanel>().GetComponentInChildren<SlotEnter>();
                var fill = slot.progressImag;
                int savedProgress = SlotProgressUtil.Current;
                int moves = BizzaGameplayBridge.Page.StepsLeft;
                Check(fill.enabled && fill.sprite != null && fill.GetComponent<WithdrawCloudProgressFill>().enabled,
                    "Native progress Image and sliced fill component are visible");
                Check(fill.rectTransform.rect.height > 45f,
                    "Fill height includes sprite padding and covers the track interior");
                Check(slot.progressTxt.transform.GetSiblingIndex() > fill.transform.GetSiblingIndex(), "Counter renders above fill");

                // Exercise only the existing visual properties, without mutating saves or earning spins.
                foreach (int value in new[] { 0, 3, 5 })
                {
                    fill.fillAmount = value / 5f;
                    slot.progressTxt.text = value + "/5";
                    Canvas.ForceUpdateCanvases();
                    await UniTask.DelayFrame(3);
                    var mesh = fill.canvasRenderer.GetMesh();
                    if (value == 0) Check(mesh.vertexCount == 0, "0/5 has no visible fill mesh");
                    else
                    {
                        mesh.RecalculateBounds();
                        Check(Mathf.Abs(mesh.bounds.size.x / fill.GetPixelAdjustedRect().width - value / 5f) < .025f,
                            value + "/5 rendered width = " + mesh.bounds.size.x + " of " + fill.GetPixelAdjustedRect().width);
                    }
                    await PayPalCapture(folder, "Visual-State-" + value + "of5.png");
                }
                slot.OnRefresh();
                await UniTask.DelayFrame(3);
                Check(Mathf.Approximately(fill.fillAmount, (float)savedProgress / SlotProgressUtil.RequiredPassedLevels),
                    "Production OnRefresh restores actual earned progress");
                Check(slot.progressTxt.text == savedProgress + "/" + SlotProgressUtil.RequiredPassedLevels, "Production counter matches fill");
                await PayPalCapture(folder, "Unity-Live.png");
                var rect = (RectTransform)slot.transform;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var canvas = slot.GetComponentInParent<Canvas>();
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var bottom = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var top = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                report.AppendLine("Screen=" + Screen.width + "x" + Screen.height + " Badge bottom=" + bottom + " top=" + top);
                var button = slot.GetComponent<Button>();
                Check(HitStandard(button), "777 native Button remains reachable");
                PressStandard(button);
                await UniTask.Delay(1000, ignoreTimeScale: true);
                var machine = UIModule.Instance.GetPage<SlotPanel>();
                Check(machine != null && machine.gameObject.activeInHierarchy, "777 opens production SlotPanel");
                if (machine != null)
                {
                    PressStandard(machine.closeBtn.GetComponent<Button>());
                    await UniTask.Delay(500, ignoreTimeScale: true);
                }
                Check(!UIModule.Instance.PageIsOpen(UIPageIds.SlotPanel), "Back closes SlotPanel");
                Check(savedProgress == SlotProgressUtil.Current && moves == BizzaGameplayBridge.Page.StepsLeft,
                    "Verification preserves earned spins and gameplay moves");
            }
            catch (Exception e) { Check(false, e.ToString()); Debug.LogException(e); }
            finally
            {
                if (slot != null) slot.OnRefresh();
                report.AppendLine("Visual-State captures are temporary visual tests. Unity-Live.png uses production saved progress.");
                report.AppendLine("Failures=" + failures + " Completed=" + DateTime.UtcNow.ToString("O"));
                File.WriteAllText(file, report.ToString());
            }
        }
    }
}
