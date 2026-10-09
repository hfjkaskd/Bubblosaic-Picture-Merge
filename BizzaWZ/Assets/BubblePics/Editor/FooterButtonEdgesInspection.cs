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
        public static async void ReviewFooterEdges(string folder)
        {
            if (!EditorApplication.isPlaying || BizzaGameplayBridge.Page == null)
                throw new InvalidOperationException("Production gameplay startup must finish first.");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            var page = BizzaGameplayBridge.Page;
            var header = UIModule.Instance.GetPage<RealGamePanel>().GetComponentInChildren<GameplayHudHeader>();
            var slot = header.EntryRow.GetComponentInChildren<SlotEnter>(true);
            var badge = header.EntryRow.Find("DailyMissionItem/Badge");
            var daily = header.EntryRow.Find("DailyMissionItem/DailyMission");
            var badgeShow = badge.GetComponent<BadgeShow>();
            bool badgeActive = badge.gameObject.activeSelf, dailyActive = daily.gameObject.activeSelf;
            bool badgeEnabled = badgeShow.enabled;
            int moves = page.StepsLeft, level = SaveDataUtils.GameData.playerSelectedLv, progress = SlotProgressUtil.Current;
            float money = ItemUtils.GetItemCount(E_ItemType.Dollar);
            string amount = badgeShow.priceTxt.text;
            try
            {
                // Show the existing authored US badge variant without changing country, claims or saved progress.
                badgeShow.enabled = false;
                daily.gameObject.SetActive(false); badge.gameObject.SetActive(true);
                badgeShow.priceTxt.text = "$300";
                slot.progressImag.fillAmount = .8f; slot.progressTxt.text = "4/5";
                Canvas.ForceUpdateCanvases(); await UniTask.DelayFrame(4);
                foreach (var button in new[] { slot.GetComponent<Button>(), badge.GetComponent<Button>() })
                {
                    if (button == null || button.targetGraphic == null || !HitStandard(button))
                        throw new InvalidOperationException("Native button is not reachable.");
                    var image = button.targetGraphic as Image;
                    report.AppendLine("PASS " + button.name + " reachable; image=" + image.sprite.name + " rect=" + image.rectTransform.rect);
                }
                await PayPalCapture(folder, "footer-4of5.png");
                slot.progressImag.fillAmount = 1; slot.progressTxt.text = "5/5";
                await PayPalCapture(folder, "footer-5of5.png");
                PressStandard(slot.GetComponent<Button>());
                await UniTask.Delay(800, ignoreTimeScale: true);
                if (!UIModule.Instance.PageIsOpen(UIPageIds.SlotPanel)) throw new InvalidOperationException("Slot button did not open its page.");
                UIModule.Instance.ClosePage(UIPageIds.SlotPanel);
                report.AppendLine("PASS 777 opens SlotPanel; no spin requested.");
            }
            catch (Exception exception) { report.AppendLine("FAIL " + exception); Debug.LogException(exception); }
            finally
            {
                slot.OnRefresh(); badgeShow.priceTxt.text = amount;
                badge.gameObject.SetActive(badgeActive); daily.gameObject.SetActive(dailyActive); badgeShow.enabled = badgeEnabled;
                bool unchanged = moves == page.StepsLeft && level == SaveDataUtils.GameData.playerSelectedLv &&
                    progress == SlotProgressUtil.Current && money == ItemUtils.GetItemCount(E_ItemType.Dollar);
                report.AppendLine((unchanged ? "PASS " : "FAIL ") + "Gameplay progress/balance unchanged. Captures use temporary display values only.");
                File.WriteAllText(Path.Combine(folder, "inspection.txt"), report.ToString());
            }
        }
    }
}
