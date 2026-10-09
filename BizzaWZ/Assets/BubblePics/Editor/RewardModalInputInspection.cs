using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        public static async void ReviewRewardModalInput(string folder)
        {
            var game = BizzaGameplayBridge.Page;
            if (!EditorApplication.isPlaying || game == null || UIModule.Instance.HasPopup)
                throw new InvalidOperationException("Idle production gameplay required.");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            var hits = new List<RaycastResult>();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            int width = Screen.width, height = Screen.height;
            int moves = game.StepsLeft, round = game.RoundSeq, collected = game.CollectedImgs.Count;
            float gold = ItemUtils.GetItemCount(E_ItemType.Gold), cash = ItemUtils.GetItemCount(E_ItemType.Dollar);
            int ads = SaveDataUtils.GameData.todayAdTimes;
            GetRewardPanel popup = null;
            GameObject FirstHit(Vector2 point)
            {
                hits.Clear(); pointer.position = point;
                EventSystem.current.RaycastAll(pointer, hits);
                return hits.Count == 0 ? null : hits[0].gameObject;
            }
            Vector2 ButtonPoint(Button button)
            {
                var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var rect = button.targetGraphic.rectTransform;
                return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            }
            void CheckGrid()
            {
                Canvas.ForceUpdateCanvases();
                for (int y = 0; y < 17; y++)
                for (int x = 0; x < 9; x++)
                {
                    var point = new Vector2(Mathf.Lerp(1, Screen.width - 1, x / 8f),
                        Mathf.Lerp(1, Screen.height - 1, y / 16f));
                    var hit = FirstHit(point);
                    if (hit == null || !hit.transform.IsChildOf(popup.transform))
                    {
                        var background = popup.transform.Find("Backdrop").GetComponent<Image>();
                        var corners = new Vector3[4]; background.rectTransform.GetWorldCorners(corners);
                        report.AppendLine("DIAGNOSTIC backdrop raycast=" + background.raycastTarget + " depth=" + background.depth +
                            " rect=" + background.rectTransform.rect + " rootScale=" + popup.transform.localScale +
                            " corners=" + corners[0] + " / " + corners[2]);
                        throw new InvalidOperationException("Click-through at " + point + ": " + (hit != null ? hit.name : "no hit"));
                    }
                }
            }
            try
            {
                foreach (int viewHeight in new[] { 2340, 1920 })
                {
                    PlayModeWindow.SetCustomRenderingResolution(1080, (uint)viewHeight, "Reward modal input review");
                    await UniTask.DelayFrame(3);
                    var behind = new List<Button>();
                    foreach (var button in App.I.HudRoot.GetComponentsInChildren<Button>())
                        if (button.targetGraphic != null && HitStandard(button)) behind.Add(button);
                    if (behind.Count == 0) throw new InvalidOperationException("No reachable gameplay Buttons to exercise.");
                    popup = (GetRewardPanel)await UIModule.Instance.OpenPage(UIPageIds.GetRewardPanel,
                        new ItemEntry { Type = E_ItemType.Gold, Count = 1000 },
                        new ItemEntry { Type = E_ItemType.Dollar, Count = 12.08f },
                        DoubleGetRewardPanel.E_UseScene.DailyTask, (Action<bool>)null);
                    var barrier = popup.transform.Find("Backdrop").GetComponent<Image>();
                    if (barrier.color.a < .6f || barrier.sprite != null || !barrier.raycastTarget ||
                        popup.transform.Find("BackgroundUnderlay") != null)
                        throw new InvalidOperationException("Original dimmed modal background/input blocking missing.");
                    var content = popup.transform.Find("Content");
                    if (content.Find("RewardDragon") != null || content.Find("Panel") != null ||
                        content.Find("ReferenceHeader") != null || popup.claimBtn.transform.Find("ReferenceClaim") != null ||
                        popup.closeBtn.transform.Find("ReferenceCollect") != null ||
                        content.Find("RewardRow") == null || popup.LevelObj.activeSelf)
                        throw new InvalidOperationException("Ordinary reward structure was not restored.");
                    var title = content.Find("HeaderCaption").GetComponent<TMPro.TMP_Text>();
                    if (title.text != LanguageUtils.GetText("Reward_Title"))
                        throw new InvalidOperationException("Ordinary reward title is not localized.");
                    if (popup.closeBtn.gameObject.activeSelf)
                        throw new InvalidOperationException("Collect delay was removed.");
                    // EventSystem uses the last rendered graphic depths. Begin at the
                    // first presented frame, after the newly instantiated UI is rendered.
                    await UniTask.WaitForEndOfFrame(game);
                    for (int frame = 0; frame < 20; frame++)
                    {
                        CheckGrid();
                        await UniTask.Yield();
                    }
                    report.AppendLine("PASS " + Screen.width + "x" + Screen.height + " opening: all 153 grid points block underlying UI across 20 frames.");
                    await UniTask.Delay(650, ignoreTimeScale: true);
                    CheckGrid();
                    int blockedTaps = 0;
                    foreach (var button in behind)
                    {
                        var hit = FirstHit(ButtonPoint(button));
                        if (hit == null || !hit.transform.IsChildOf(popup.transform))
                            throw new InvalidOperationException("Gameplay Button exposed: " + button.name);
                        if (hit.GetComponentInParent<Button>() != null) continue; // Never claim rewards or watch ads in QA.
                        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
                        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
                        ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
                        blockedTaps++;
                    }
                    if (blockedTaps == 0) throw new InvalidOperationException("No background taps exercised.");
                    report.AppendLine("PASS " + blockedTaps + " taps over gameplay Button positions resolve to reward background.");
                    if (!HitStandard(popup.claimBtn.GetComponent<Button>()))
                        throw new InvalidOperationException("Claim Button is blocked.");
                    await UniTask.WaitUntil(() => popup.closeBtn.gameObject.activeSelf).Timeout(TimeSpan.FromSeconds(5));
                    if (!HitStandard(popup.closeBtn.GetComponent<Button>()))
                        throw new InvalidOperationException("Delayed Collect Button is blocked.");
                    if (popup.noThanksText.text != LanguageUtils.GetText("Btn_NoThanks"))
                        throw new InvalidOperationException("Delayed enable overwrote the runtime Collect caption.");
                    CheckGrid();
                    await PayPalCapture(folder, "reward-" + Screen.width + "x" + Screen.height + ".png");
                    report.AppendLine("PASS Claim and delayed Collect are first-hit native Buttons.");
                    // Presentation-only settlement fixture: do not enter the payout/level-advance path.
                    popup.LevelObj.SetActive(true);
                    popup.levelTxt.text = LanguageUtils.GetFormatText("Menu_LevelBtn", 8);
                    title.text = LanguageUtils.GetText("Win_Title");
                    popup.rewardText.text = BubblePics.Localization.Tr("seq_claim") + "×2";
                    popup.itemBTxt.text = LanguageUtils.GetText("CurrencyToken") + WithdrawalUtil.GetCustomizedValueByCountryType(24.16f);
                    popup.noThanksText.text = LanguageUtils.GetText("CurrencyToken") + WithdrawalUtil.GetCustomizedValueByCountryType(12.08f);
                    await UniTask.DelayFrame(3);
                    foreach (var label in popup.GetComponentsInChildren<TMPro.TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        if (label.isTextOverflowing) throw new InvalidOperationException("Text overflow: " + label.name + " / " + label.text);
                    }
                    var scale = content.localScale;
                    if (Mathf.Abs(scale.x - scale.y) > .001f) throw new InvalidOperationException("Reward content stretched.");
                    await PayPalCapture(folder, "settlement-layout-" + Screen.width + "x" + Screen.height + ".png");
                    var row = (RectTransform)content.Find("RewardRow");
                    var cashReward = row.Find("CashReward");
                    cashReward.gameObject.SetActive(false);
                    await UniTask.DelayFrame(3);
                    Canvas.ForceUpdateCanvases();
                    var coinReward = (RectTransform)row.Find("CoinReward");
                    var coinCenter = row.InverseTransformPoint(coinReward.TransformPoint(coinReward.rect.center));
                    if (Mathf.Abs(coinCenter.x) > 1) throw new InvalidOperationException("Single reward not centered: " + coinCenter);
                    await PayPalCapture(folder, "single-reward-layout-" + Screen.width + "x" + Screen.height + ".png");
                    cashReward.gameObject.SetActive(true);
                    report.AppendLine("PASS settlement presentation fixture: level, doubled amount, no text overflow, uniform content scale; single reward centers.");
                    await InspectGeneratedRewardArt(popup, folder);
                    UIModule.Instance.ClosePage(UIPageIds.GetRewardPanel); popup = null;
                    await UniTask.DelayFrame(3);
                    foreach (var button in behind)
                        if (button != null && !HitStandard(button))
                            throw new InvalidOperationException("Gameplay input not restored: " + button.name);
                    report.AppendLine("PASS closing restores " + behind.Count + " gameplay Buttons.");
                }
                if (moves != game.StepsLeft || round != game.RoundSeq || collected != game.CollectedImgs.Count ||
                    gold != ItemUtils.GetItemCount(E_ItemType.Gold) || cash != ItemUtils.GetItemCount(E_ItemType.Dollar) ||
                    ads != SaveDataUtils.GameData.todayAdTimes)
                    throw new InvalidOperationException("Gameplay, currency or ads changed.");
                report.AppendLine("PASS gameplay, currency and ad state unchanged; no reward/ad Button invoked.");
            }
            catch (Exception exception)
            {
                report.AppendLine("FAIL " + exception); Debug.LogException(exception);
                await PayPalCapture(folder, "failure.png");
            }
            finally
            {
                if (popup != null) UIModule.Instance.ClosePage(UIPageIds.GetRewardPanel);
                PlayModeWindow.SetCustomRenderingResolution((uint)width, (uint)height, "Game");
                File.WriteAllText(Path.Combine(folder, "inspection.txt"), report.ToString());
            }
        }

        static async UniTask InspectGeneratedRewardArt(GetRewardPanel popup, string folder)
        {
            var content = popup.transform.Find("Content");
            foreach (string path in new[] { "Header", "LevelRoot/Level", "AdBonusProgress/Track", "WithdrawalProgress/Track" })
            {
                var node = content.Find(path);
                var sprite = node.GetComponent<Image>().sprite;
                if (sprite == null || !AssetDatabase.GetAssetPath(sprite).Contains("/RewardArt20260930/"))
                    throw new InvalidOperationException("Old screenshot-derived sprite remains: " + path);
                if (node.GetComponent<ApprovedContourImage>() != null)
                    throw new InvalidOperationException("Generated art still uses an approximate contour: " + path);
            }
            if (Screen.height != 2340) return;
            var ad = content.Find("AdBonusProgress").GetComponent<WathAdProgress>();
            var cash = popup.progress;
            float adBefore = ad.progressBar.fillAmount, cashBefore = cash.progressImg.fillAmount;
            string adText = ad.progressText.text, cashText = cash.percentageText.text;
            try
            {
                foreach (int percent in new[] { 0, 1, 20, 50, 100 })
                {
                    ad.progressBar.fillAmount = cash.progressImg.fillAmount = percent / 100f;
                    ad.progressText.text = cash.percentageText.text = percent + "%";
                    await UniTask.DelayFrame(3);
                    Canvas.ForceUpdateCanvases();
                    foreach (var fill in new[] { ad.progressBar, cash.progressImg })
                    {
                        var mesh = fill.canvasRenderer.GetMesh(); mesh.RecalculateBounds();
                        float expected = fill.GetPixelAdjustedRect().width * percent / 100f;
                        if (percent == 0 ? mesh.vertexCount != 0 : Mathf.Abs(mesh.bounds.size.x - expected) > 2)
                            throw new InvalidOperationException("Progress width mismatch at " + percent + "% / " + fill.transform.parent.name);
                        if (fill.type != Image.Type.Sliced || fill.sprite.border.x <= 0 || fill.sprite.border.z <= 0)
                            throw new InvalidOperationException("Missing progress end-cap slicing.");
                    }
                    await PayPalCapture(folder, "progress-" + percent + ".png");
                }
            }
            finally
            {
                ad.progressBar.fillAmount = adBefore; cash.progressImg.fillAmount = cashBefore;
                ad.progressText.text = adText; cash.percentageText.text = cashText;
            }
            File.WriteAllText(Path.Combine(folder, "generated-art-check.txt"),
                "PASS independent generated header/level/track sprites; no polygon cutout. Both progress meshes match 0, 1, 20, 50 and 100 percent, with sliced end caps. Preview progress values are display-only.");
        }
    }
}
