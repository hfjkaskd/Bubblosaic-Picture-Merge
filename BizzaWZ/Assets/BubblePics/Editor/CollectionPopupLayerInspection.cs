using System;
using System.IO;
using System.Text;
using BubblePics.GameModes;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        // Presentation fixture: use the real collection coroutine and popup without
        // completing a puzzle, granting a reward, watching ads or editing a save.
        public static async void ReviewCollectionPopupLayer(string folder)
        {
            var page = BizzaGameplayBridge.Page;
            if (!EditorApplication.isPlaying || page == null || page.Fly.IsFlying || page.Merge.IsBusy)
                throw new InvalidOperationException("Idle production gameplay required.");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder();
            var selection = ModeSession.ActiveSelection;
            var compiled = ModeSession.ActiveCompiledLevel;
            var icons = ModeSession.ActiveCategoryIcons;
            string locale = Localization.CurrentLocale;
            float timeScale = Time.timeScale;
            int collected = page.CollectedImgs.Count, steps = page.StepsLeft;
            float cash = ItemUtils.GetItemCount(E_ItemType.Dollar), gold = ItemUtils.GetItemCount(E_ItemType.Gold);
            int ads = SaveDataUtils.GameData.todayAdTimes;
            GetRewardPanel popup = null;
            try
            {
                Localization.SetLocale("en");
                var category = new CompiledModeLevel { Kind = GameplayKind.CategoryMatch };
                category.Groups.Add(new CompiledModeGroup { ImageId = 196, SourceGroupId = 196, GroupLabel = "Tomatoes" });
                ModeSession.Activate(new LevelModeSelection { Kind = GameplayKind.CategoryMatch }, category);
                page.Fly.Play(null, App.DesignToWorld(new Vector2(DeviceLayout.ViewWidth * .5f, 1050f)),
                    760f, 0, 0f, 196);
                await UniTask.Delay(520);
                Time.timeScale = 0f;
                var flight = App.I.HudRoot.Find("CategoryFly");
                var canvas = flight != null ? flight.GetComponent<Canvas>() : null;
                if (canvas == null || canvas.overrideSorting)
                    throw new InvalidOperationException("Category collection escaped gameplay sorting.");
                report.AppendLine("PASS real category coroutine uses inherited gameplay ordering.");
                var categoryDisc = flight.Find("CategoryDisc").GetComponent<RawImage>();
                if (categoryDisc.texture == null || categoryDisc.texture != BubbleView.ActiveBubblePicTex() ||
                    categoryDisc.materialForRendering == null || !categoryDisc.materialForRendering.shader.isSupported)
                    throw new InvalidOperationException("Category collection skin or material is missing.");
                report.AppendLine("PASS category collection uses the current gameplay bubble texture and supported UI material.");
                await PayPalCapture(folder, "category-gameplay.png");

                popup = (GetRewardPanel)await UIModule.Instance.OpenPage(UIPageIds.GetRewardPanel,
                    new ItemEntry { Type = E_ItemType.Gold, Count = 1000 },
                    new ItemEntry { Type = E_ItemType.Dollar, Count = 12.60f },
                    DoubleGetRewardPanel.E_UseScene.DailyTask, (Action<bool>)null);
                await UniTask.Delay(500, ignoreTimeScale: true);
                // Reproduce the former sorting error with the same frozen production frame.
                canvas.overrideSorting = true; canvas.sortingOrder = 1000;
                await PayPalCapture(folder, "old-order-reproduction.png");
                canvas.overrideSorting = false;
                await PayPalCapture(folder, "after.png");
                if (!page.Fly.IsFlying || !flight.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Opening the reward popup cancelled collection.");
                report.AppendLine("PASS popup covers the active collection without cancelling its coroutine.");
                page.Fly.Cancel(); // Cancel only this inspection fixture, before landing changes the HUD.
                Time.timeScale = timeScale;
                var claimButton = popup.claimBtn.GetComponent<Button>();
                // The production click guard uses scaled frame time. An unfocused editor
                // can render too few frames for a fixed wall-clock delay to finish it.
                await UniTask.WaitUntil(() => claimButton.IsInteractable()).Timeout(TimeSpan.FromSeconds(5));
                await UniTask.WaitForEndOfFrame(page);
                if (!HitStandard(claimButton))
                    throw new InvalidOperationException("Reward claim Button is not reachable.");
                report.AppendLine("PASS reward claim Button reachable after its normal click guard.");
                UIModule.Instance.ClosePage(UIPageIds.GetRewardPanel); popup = null;
                Time.timeScale = 0f;

                // Both other collection branches share the same gameplay/popup boundary.
                category.Kind = GameplayKind.WordMatch;
                ModeSession.Activate(new LevelModeSelection { Kind = GameplayKind.WordMatch }, category);
                page.Fly.Play(null, Vector3.zero, 500f, 0, 0f, 196);
                var word = App.I.HudRoot.Find("WordFly");
                if (word == null || word.GetComponent<Canvas>().overrideSorting)
                    throw new InvalidOperationException("Word collection escaped gameplay sorting.");
                if (word.Find("WordDisc").GetComponent<RawImage>().texture != BubbleView.ActiveBubblePicTex())
                    throw new InvalidOperationException("Word collection uses a different bubble skin.");
                report.AppendLine("PASS word collection uses the same active bubble skin.");
                page.Fly.Cancel(); await UniTask.DelayFrame(2);
                ModeSession.Clear();
                BubbleView sample = null;
                foreach (var bubble in page.Field.AllBubbles())
                    if (bubble.SourceTexture != null) { sample = bubble; break; }
                if (sample == null) throw new InvalidOperationException("No loaded photo available.");
                page.Fly.Play(sample.SourceTexture, Vector3.zero, 500f, 0, 0f, sample.ImageId);
                var photo = App.I.HudRoot.Find("FlyImage");
                if (photo == null || photo.GetComponent<Canvas>().overrideSorting)
                    throw new InvalidOperationException("Photo collection escaped gameplay sorting.");
                var trail = GameObject.Find("FlyTrail");
                var trailMaterial = trail != null ? trail.GetComponent<ParticleSystemRenderer>().sharedMaterial : null;
                if (trailMaterial == null || trailMaterial.mainTexture == null || !trailMaterial.shader.isSupported)
                    throw new InvalidOperationException("Collection trail is missing its texture or material.");
                report.AppendLine("PASS collection trail has a texture and supported material.");
                report.AppendLine("PASS word and photo collections also stay inside gameplay sorting.");
                page.Fly.Cancel();
                Time.timeScale = timeScale;
                if (!VFXUtils.itemFlyTarget.TryGetValue(E_ItemType.Dollar, out var cashTarget) || cashTarget == null)
                    throw new InvalidOperationException("Missing production cash collection target.");
                var cashImage = cashTarget.GetComponent<Image>() ?? cashTarget.GetComponentInChildren<Image>();
                if (cashImage == null || cashImage.sprite == null || cashImage.material == null ||
                    !cashImage.material.shader.isSupported)
                    throw new InvalidOperationException("Missing currency source sprite or material.");
                report.AppendLine("PASS currency source retains its sprite and supported material: " + cashImage.material.shader.name);
                if (Application.isFocused)
                {
                int cashCompletions = 0;
                VFXUtils.PlayItemCollectFx(E_ItemType.Dollar,
                    ImageFlyAnimator.WorldToHudPosition(App.DesignToWorld(new Vector2(DeviceLayout.ViewWidth * .5f, 1050f))),
                    () => cashCompletions++, target: cashTarget, animation: RewardCollectAnimation.BurstCollect);
                await UniTask.Delay(250, ignoreTimeScale: true);
                int fxGraphics = 0;
                foreach (var graphic in UIModule.Instance.RewardItemLayer.GetComponentsInChildren<Graphic>())
                {
                    if (!graphic.isActiveAndEnabled) continue;
                    fxGraphics++;
                    if (graphic.mainTexture == null || graphic.materialForRendering == null ||
                        !graphic.materialForRendering.shader.isSupported)
                        throw new InvalidOperationException("Missing currency animation texture or material: " + graphic.name);
                }
                if (fxGraphics == 0) throw new InvalidOperationException("No currency animation graphics were rendered.");
                await PayPalCapture(folder, "cash-effect.png");
                await UniTask.WaitUntil(() => cashCompletions > 0).Timeout(TimeSpan.FromSeconds(6));
                if (cashCompletions != 1) throw new InvalidOperationException("Currency effect completion count incorrect.");
                report.AppendLine("PASS native currency animation has supported materials/textures and completes exactly once.");
                }
                else report.AppendLine("SKIP native cash flight playback: Unity has no focused Game view; foreground pause is preserved.");
                if (collected != page.CollectedImgs.Count || steps != page.StepsLeft ||
                    cash != ItemUtils.GetItemCount(E_ItemType.Dollar) || gold != ItemUtils.GetItemCount(E_ItemType.Gold) ||
                    ads != SaveDataUtils.GameData.todayAdTimes)
                    throw new InvalidOperationException("Gameplay or reward state changed.");
                report.AppendLine("PASS collected count, moves, currency and ads unchanged.");
            }
            catch (Exception exception) { report.AppendLine("FAIL " + exception); Debug.LogException(exception); }
            finally
            {
                page.Fly.Cancel();
                if (popup != null) UIModule.Instance.ClosePage(UIPageIds.GetRewardPanel);
                ModeSession.Activate(selection, compiled, icons);
                Localization.SetLocale(locale);
                Time.timeScale = timeScale;
                File.WriteAllText(Path.Combine(folder, "inspection.txt"), report.ToString());
            }
        }
    }
}
