using System;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewSlotAnimationCore(string folder, Action<bool, string> check)
        {
            LanguageUtils.SelectedLanguage = "pt-BR";
            Localization.SetLocale("pt_BR");
            var page = (SlotPanel)await UIModule.Instance.OpenPage(UIPageIds.SlotPanel);
            inspectedPage = page;
            await UniTask.DelayFrame(6);
            var manager = page.slotMachineManager;
            var initial = new Vector2[manager.slotEntries.Length];
            var drift = new float[initial.Length];
            var maxWidth = new float[initial.Length];
            for (int i = 0; i < initial.Length; i++)
                initial[i] = PayPalRect((RectTransform)manager.slotEntries[i].transform).center;
            double balance = ItemUtils.Get(E_ItemType.Dollar).Count;
            int progress = SlotProgressUtil.Current;
            int completed = 0;
            string frames = Path.Combine(folder, "Frames");
            Directory.CreateDirectory(frames);
            var samples = new StringBuilder("seconds,column,centerX,centerY,scaleX,symbol\n");
            await PayPalCapture(folder, "Before-Spin.png");
            manager.PlayAnim(true, _ => completed++);
            float start = Time.unscaledTime, nextFrame = 0;
            int frame = 0;
            while (Time.unscaledTime - start < 7f)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
                float elapsed = Time.unscaledTime - start;
                for (int i = 0; i < initial.Length; i++)
                {
                    var entry = manager.slotEntries[i];
                    Vector2 center = PayPalRect((RectTransform)entry.transform).center;
                    drift[i] = Mathf.Max(drift[i], Vector2.Distance(center, initial[i]));
                    maxWidth[i] = Mathf.Max(maxWidth[i], PayPalRect((RectTransform)entry.transform).width);
                    if (elapsed >= nextFrame)
                        samples.AppendLine(FormattableString.Invariant($"{elapsed:F3},{i},{center.x:F2},{center.y:F2},{entry.transform.localScale.x:F3},{entry.img1.sprite?.name}"));
                }
                if (elapsed >= nextFrame)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(frames, $"frame-{frame++:D3}.png"));
                    nextFrame = elapsed + .1f;
                }
            }
            for (int i = 0; i < drift.Length; i++)
            {
                check(drift[i] < 1f, $"Reel {i + 1} center stays fixed throughout spin and reward bounce; max drift={drift[i]:F2}");
                check(maxWidth[i] < Mathf.Abs(initial[1].x - initial[0].x), $"Reel {i + 1} reward pulse remains within its column");
                check(!manager.slotEntries[i].img2.gameObject.activeSelf, $"Reel {i + 1} hides the outgoing symbol after settling");
            }
            check(completed == 1, "Animation completion callback fires exactly once");
            check(progress == SlotProgressUtil.Current && balance == ItemUtils.Get(E_ItemType.Dollar).Count,
                "Visual playback does not consume spins or alter the balance");
            File.WriteAllText(Path.Combine(folder, "alignment.csv"), samples.ToString());
            await PayPalCapture(folder, "After-Spin.png");
            manager.PlayAnim(false, _ => completed++);
            float repeatStart = Time.unscaledTime;
            while (completed < 2 && Time.unscaledTime - repeatStart < 12f)
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            check(completed == 2, "A consecutive free spin also reaches exactly one completion callback");
            check(progress == SlotProgressUtil.Current && balance == ItemUtils.Get(E_ItemType.Dollar).Count,
                "Consecutive visual playback preserves actual currency and spin progress");
            CloseRuntime();
        }
    }
}
