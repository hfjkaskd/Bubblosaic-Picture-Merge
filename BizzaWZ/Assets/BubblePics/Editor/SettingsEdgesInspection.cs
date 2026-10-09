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
        public static async void ReviewSettingsEdges(string folder)
        {
            if (!EditorApplication.isPlaying || BizzaGameplayBridge.Page == null)
                throw new InvalidOperationException("Production gameplay required.");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            string locale = Localization.CurrentLocale;
            var settings = SaveDataUtils.SettingData;
            bool music = settings.enableMusic, sound = settings.enableSound, vibration = settings.enableVibrate;
            int round = BizzaGameplayBridge.Page.RoundSeq, moves = BizzaGameplayBridge.Page.StepsLeft;
            PausePanel page = null;
            try
            {
                Localization.SetLocale("en");
                page = (PausePanel)await UIModule.Instance.OpenPage(UIPageIds.PausePanel);
                World.Current.Pause(page);
                await UniTask.Delay(500, ignoreTimeScale: true);
                foreach (var owner in new[] { page.CloseButton, page.BackButton, page.ContinueButton,
                    page.musicSwitchButton, page.soundSwitchButton, page.libSwitchButton })
                    if (!HitStandard(owner.GetComponent<Button>())) throw new InvalidOperationException("Unreachable button " + owner.name);
                report.AppendLine("PASS all six native Buttons reachable; gameplay paused=" + World.Current.IsPause);
                await PayPalCapture(folder, "after.png");
                PressStandard(page.musicSwitchButton.GetComponent<Button>());
                if (settings.enableMusic == music || page.musicOnIm.gameObject.activeSelf != settings.enableMusic)
                    throw new InvalidOperationException("Music switch failed.");
                PressStandard(page.musicSwitchButton.GetComponent<Button>());
                PressStandard(page.soundSwitchButton.GetComponent<Button>());
                if (settings.enableSound == sound || page.soundOnIm.gameObject.activeSelf != settings.enableSound)
                    throw new InvalidOperationException("Sound switch failed.");
                PressStandard(page.soundSwitchButton.GetComponent<Button>());
                PressStandard(page.libSwitchButton.GetComponent<Button>());
                if (settings.enableVibrate == vibration || page.LibOnIm.gameObject.activeSelf != settings.enableVibrate)
                    throw new InvalidOperationException("Vibration switch failed.");
                PressStandard(page.libSwitchButton.GetComponent<Button>());
                report.AppendLine("PASS music, sound and vibration toggle and restore correctly.");
                PressStandard(page.ContinueButton.GetComponent<Button>());
                await UniTask.Delay(400, ignoreTimeScale: true);
                if (UIModule.Instance.PageIsOpen(UIPageIds.PausePanel) || World.Current.IsPause)
                    throw new InvalidOperationException("Continue failed to resume gameplay.");
                page = (PausePanel)await UIModule.Instance.OpenPage(UIPageIds.PausePanel);
                World.Current.Pause(page); await UniTask.Delay(400, ignoreTimeScale: true);
                PressStandard(page.CloseButton.GetComponent<Button>());
                await UniTask.Delay(400, ignoreTimeScale: true);
                if (UIModule.Instance.PageIsOpen(UIPageIds.PausePanel) || World.Current.IsPause)
                    throw new InvalidOperationException("Close failed to resume gameplay.");
                report.AppendLine("PASS Continue and Close resume gameplay.");
                if (round != BizzaGameplayBridge.Page.RoundSeq || moves != BizzaGameplayBridge.Page.StepsLeft)
                    throw new InvalidOperationException("Round or moves changed.");
                report.AppendLine("PASS round and moves unchanged; Restart not invoked.");
            }
            catch (Exception exception) { report.AppendLine("FAIL " + exception); Debug.LogException(exception); }
            finally
            {
                settings.enableMusic = music; settings.enableSound = sound; settings.enableVibrate = vibration;
                global::SoundManager.Instance.MuteBGM(!music); global::SoundManager.Instance.MuteSFX(!sound);
                if (page != null) { World.Current.Resume(page); UIModule.Instance.ClosePage(UIPageIds.PausePanel); }
                Localization.SetLocale(locale);
                File.WriteAllText(Path.Combine(folder, "inspection.txt"), report.ToString());
            }
        }
    }
}
