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
        static async void ReviewMainOtter()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Validation/MainOtter-20260929"));
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            int failures = 0;
            void Check(bool ok, string message) { report.AppendLine((ok ? "PASS " : "FAIL ") + message); if (!ok) failures++; }
            string file = folder + "/inspection-" + Screen.width + "x" + Screen.height + ".txt";
            File.WriteAllText(file, "RUNNING " + DateTime.UtcNow.ToString("O"));
            DolphinDecoration mascot = null;
            try
            {
                if (!EditorApplication.isPlaying || BizzaGameplayBridge.Page == null) throw new InvalidOperationException("Normal gameplay startup must finish first.");
                var page = BizzaGameplayBridge.Page;
                var panel = UIModule.Instance.GetPage<RealGamePanel>();
                int moves = page.StepsLeft;
                var header = panel.GetComponentInChildren<GameplayHudHeader>(true);
                mascot = page.TopBar.Dolphin;
                Check(mascot != null && mascot.Spine.Data != null, "Original otter skeleton loaded through production Spine loader");
                Check(mascot.transform.Find("CoralPortrait") == null, "Main portrait no longer contains dragon overlay");
                var renderer = mascot.Spine.GetComponent<MeshRenderer>();
                Check(renderer.enabled && renderer.bounds.size.x > 0 && renderer.bounds.size.y > 0, "Otter animated mesh visible");
                foreach (string animation in new[] { "in", "idle", "applaud", "failure", "youxishengli" })
                    Check(mascot.Spine.HasAnimation(animation), "Original animation available: " + animation);
                var spine = mascot.Spine;
                mascot.ResumeIdle();
                await UniTask.Delay(300, ignoreTimeScale: true);
                float time = spine.Current.TrackTime;
                await UniTask.Delay(400, ignoreTimeScale: true);
                Check(spine.Current.TrackTime > time, "Otter animation advances in Play Mode");
                foreach (var button in header.CurrencyRow.GetComponentsInChildren<Button>(false))
                    Check(button.targetGraphic != null && HitStandard(button), "Currency/settings native Button hit: " + button.name);
                foreach (var entry in panel.propEntries)
                    Check(entry.btn != null && HitStandard(entry.btn), "Tool native Button hit: " + entry.name);
                foreach (var button in header.EntryRow.GetComponentsInChildren<Button>(false))
                    Check(button.targetGraphic != null && HitStandard(button), "Footer native Button hit: " + button.name);
                foreach(var icon in header.CurrencyRow.GetComponentsInChildren<WzIconAmend>())
                {
                    var visual=icon.image!=null?icon.image:icon.GetComponent<Image>();
                    Check(visual!=null&&visual.sprite!=null&&visual.enabled&&visual.color.a>0,"Currency icon has a visible sprite: "+icon.name);
                    Check(visual!=null&&visual.material.shader.name=="UI/Default","Baked currency icon no longer uses source-sheet clipping: "+icon.name);
                }
                var appearance=Resources.Load<ToolAppearance>("CoralV3/ToolAppearance");
                Check(appearance!=null&&appearance.Background(false)!=null&&appearance.Background(true)!=null,"Normal and locked tool backgrounds both load from release atlas");
                Check(page.Toolbar.Magnet != null && page.Toolbar.Magnet.Def.Id == "magnet", "Otter tool retains original gameplay identity");
                await PayPalCapture(folder, "Unity-" + Screen.width + "x" + Screen.height + ".png");
                mascot.PlayApplaud();
                await UniTask.Delay(500, ignoreTimeScale: true);
                await PayPalCapture(folder, "Otter-Applaud-" + Screen.width + "x" + Screen.height + ".png");
                mascot.ResumeIdle();
                var settings = header.CurrencyRow.Find("PauseButton").GetComponent<Button>();
                PressStandard(settings);
                await UniTask.Delay(750, ignoreTimeScale: true);
                var pause = UIModule.Instance.GetPage<PausePanel>();
                Check(pause != null && pause.gameObject.activeInHierarchy, "Settings Button opens production pause page");
                if (pause != null)
                {
                    PressStandard(pause.CloseButton.GetComponent<Button>());
                    await UniTask.Delay(650, ignoreTimeScale: true);
                }
                Check(page.Input.InputEnabled && !BizzaGameplayBridge.IsInputBlocked, "Closing settings returns gameplay input");
                Check(page.StepsLeft == moves, "Visual verification does not consume gameplay moves");
                report.AppendLine("Capture is raw Unity Play Mode output. Live level, puzzle, currency, inventory and country-dependent entry remain production values. No reference sample values, ad playback, payout or grant were used.");
                report.AppendLine("The older hud-layout-result uses legacy bottom-gap/center alignment expectations; current footer follows the approved baseline placement. This review checks reachable Buttons, original animations, and actual navigation.");
            }
            catch (Exception e) { Check(false, e.ToString()); Debug.LogException(e); }
            finally
            {
                if (mascot != null) mascot.ResumeIdle();
                report.AppendLine("Failures=" + failures + " Completed=" + DateTime.UtcNow.ToString("O"));
                File.WriteAllText(file, report.ToString());
            }
        }
    }
}
