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
        public static async void ReviewSlotRewardLayout(string folder)
        {
            if (!EditorApplication.isPlaying || BizzaGameplayBridge.Page == null)
                throw new InvalidOperationException("Production gameplay startup must finish first.");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "inspection.txt"), "RUNNING");
            var report = new StringBuilder();
            float gold = ItemUtils.GetItemCount(E_ItemType.Gold), cash = ItemUtils.GetItemCount(E_ItemType.Dollar);
            int spins = SlotProgressUtil.Current;
            string language = LanguageUtils.SelectedLanguage, locale = Localization.CurrentLocale;
            SlotPanel page = null;
            try
            {
                LanguageUtils.SelectedLanguage = "en-US"; Localization.SetLocale("en");
                page = (SlotPanel)await UIModule.Instance.OpenPage(UIPageIds.SlotPanel);
                var reward = page.slotRewardPanel;
                var coinRect = (RectTransform)reward.coinObj.transform;
                var cashRect = (RectTransform)reward.dollarObj.transform;
                var row = (RectTransform)coinRect.parent;
                var button = reward.btnObj.GetComponent<Button>();
                Vector2 coinSize = coinRect.sizeDelta, cashSize = cashRect.sizeDelta;
                Vector3 buttonPosition = button.transform.localPosition;
                reward.gameObject.SetActive(true);
                foreach (int state in new[] { 0, 1, 2, 0, 2 })
                {
                    reward.Init(state == 0 ? 0 : 1000, state == 1 ? 0 : .92f,
                        state == 0 ? nameof(E_WzIconType.StackMoney) : nameof(E_WzIconType.PileWealth));
                    await UniTask.DelayFrame(4);
                    Canvas.ForceUpdateCanvases();
                    Vector3 coinCenter = row.InverseTransformPoint(coinRect.TransformPoint(coinRect.rect.center));
                    Vector3 cashCenter = row.InverseTransformPoint(cashRect.TransformPoint(cashRect.rect.center));
                    if (state == 0 && (reward.coinObj.activeSelf || !reward.dollarObj.activeSelf || Mathf.Abs(cashCenter.x) > .1f))
                        throw new InvalidOperationException("Cash-only reward is not centered.");
                    if (state == 1 && (!reward.coinObj.activeSelf || reward.dollarObj.activeSelf || Mathf.Abs(coinCenter.x) > .1f))
                        throw new InvalidOperationException("Coin-only reward is not centered.");
                    if (state == 2 && (!reward.coinObj.activeSelf || !reward.dollarObj.activeSelf ||
                        coinCenter.x >= 0 || cashCenter.x <= 0 || Mathf.Abs(coinCenter.x + cashCenter.x) > .1f))
                        throw new InvalidOperationException("Dual rewards are not balanced.");
                    if (coinRect.sizeDelta != coinSize || cashRect.sizeDelta != cashSize || button.transform.localPosition != buttonPosition)
                        throw new InvalidOperationException("Reward or button dimensions changed.");
                    if (!HitStandard(button)) throw new InvalidOperationException("OK Button is not reachable.");
                    string label = state == 0 ? "Cash-Only" : state == 1 ? "Coin-Only" : "Both-Rewards";
                    report.AppendLine("PASS " + label + ": centered active cards, original size, native OK reachable.");
                    await PayPalCapture(folder, label + ".png");
                }
            }
            catch (Exception exception) { report.AppendLine("FAIL " + exception); Debug.LogException(exception); }
            finally
            {
                if (page != null) UIModule.Instance.ClosePage(UIPageIds.SlotPanel);
                LanguageUtils.SelectedLanguage = language; Localization.SetLocale(locale);
                bool unchanged = gold == ItemUtils.GetItemCount(E_ItemType.Gold) && cash == ItemUtils.GetItemCount(E_ItemType.Dollar)
                    && spins == SlotProgressUtil.Current;
                report.AppendLine((unchanged ? "PASS " : "FAIL ") + "Balances and spins unchanged; reward was not claimed.");
                File.WriteAllText(Path.Combine(folder, "inspection.txt"), report.ToString());
            }
        }
    }
}
