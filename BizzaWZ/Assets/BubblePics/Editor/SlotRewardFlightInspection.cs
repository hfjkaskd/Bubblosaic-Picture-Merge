using System;
using System.IO;
using Bizza.FlyMoney;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewSlotRewardFlightCore(string folder, Action<bool, string> check)
        {
            LanguageUtils.SelectedLanguage = "pt-BR"; Localization.SetLocale("pt_BR");
            float beforeGold = ItemUtils.GetItemCount(E_ItemType.Gold), beforeCash = ItemUtils.GetItemCount(E_ItemType.Dollar);
            int beforeAds = SaveDataUtils.GameData.todayAdTimes, beforeSpins = SlotProgressUtil.Current;
            var page = (SlotPanel)await UIModule.Instance.OpenPage(UIPageIds.SlotPanel);
            inspectedPage = page;
            await UniTask.Delay(700, ignoreTimeScale:true);
            var reward = page.slotRewardPanel;
            var coin = reward.coinTargetPos.GetComponent<Image>();
            var cash = reward.dollarTargetPos.GetComponent<Image>();
            check(coin != null && cash != null, "Both production targets are currency Images, not amount text");
            if (coin == null || cash == null) return;
            foreach (var image in new[] { coin, cash })
            {
                check(image.isActiveAndEnabled && image.sprite != null, "Visible authored target " + image.name);
                check(image.sprite.vertices.Length > 4 && image.useSpriteMesh, "Transparent contour is retained " + image.name);
            }
            var a = reward.coinTarget.valueText; var b = reward.dollarTarget.valueText;
            check(a.font == b.font && a.fontSizeMax == b.fontSizeMax && a.color == b.color && a.alignment == b.alignment,
                "Slot balances share font, size, color and alignment");
            await PayPalCapture(folder, "Slot-Balances-Unity.png");
            foreach (var animation in new[] { RewardCollectAnimation.Legacy, RewardCollectAnimation.BurstCollect })
            {
                check(Application.isFocused, "Game view focused before playback / " + (int)animation);
                reward.gameObject.SetActive(true);
                reward.Init(1000, 12.86f, nameof(E_WzIconType.PileWealth), animation == RewardCollectAnimation.BurstCollect);
                await UniTask.Delay(350, ignoreTimeScale:true);
                string prefix = animation == RewardCollectAnimation.Legacy ? "Legacy" : "Native";
                await PayPalCapture(folder, prefix + "-Reward.png");
                Vector3 origin = reward.btnObj.transform.position;
                int coinsArrived = 0, cashArrived = 0;
                // Exercise the exact production presentation path without granting money or calling an ad SDK.
                VFXUtils.PlayItemCollectFx(E_ItemType.Dollar, origin, () => cashArrived++, target:cash.transform, animation:animation);
                VFXUtils.PlayItemCollectFx(E_ItemType.Gold, origin, () => coinsArrived++, target:coin.transform, animation:animation);
                reward.gameObject.SetActive(false);
                bool sawCoins = false, sawCash = false, coinsRose = false, cashRose = false;
                float firstCoinY = float.NaN, firstCashY = float.NaN;
                string frames = Path.Combine(folder, prefix + "-Frames"); Directory.CreateDirectory(frames);
                {
                    for (int frame = 0; frame < 28; frame++)
                    {
                        await UniTask.Delay(100, ignoreTimeScale:true);
                        if (animation == RewardCollectAnimation.Legacy)
                        {
                            foreach (var image in UIModule.Instance.RewardItemLayer.GetComponentsInChildren<Image>())
                            {
                                if (image.name != "ItemCollectIcon" || image.color.a <= .02f) continue;
                                bool gold = image.sprite == coin.sprite, money = image.sprite == cash.sprite;
                                if (!gold && !money) continue;
                                check(image.useSpriteMesh && image.material == (gold ? coin.material : cash.material), "Legacy sprite contour and material stay paired");
                                float y = image.transform.position.y;
                                if (gold) { sawCoins = true; if (float.IsNaN(firstCoinY)) firstCoinY = y; else coinsRose |= y > firstCoinY + 100; }
                                else { sawCash = true; if (float.IsNaN(firstCashY)) firstCashY = y; else cashRose |= y > firstCashY + 100; }
                            }
                        }
                        else
                        {
                            foreach (var graphic in UIModule.Instance.RewardItemLayer.GetComponentsInChildren<FlyMoneyGraphic>())
                            {
                                if (graphic.VertexCount == 0 || graphic.mainTexture != coin.sprite.texture) continue;
                                var mesh = graphic.canvasRenderer.GetMesh();
                                if (mesh == null) continue;
                                var uv = mesh.uv; var vertices = mesh.vertices;
                                if (uv.Length == 0) continue;
                                Vector2 averageUv = Vector2.zero; Vector3 center = Vector3.zero;
                                for (int i = 0; i < uv.Length; i++) { averageUv += uv[i]; center += vertices[i]; }
                                averageUv /= uv.Length; center /= uv.Length;
                                Vector2 pixel = new Vector2(averageUv.x * coin.sprite.texture.width, averageUv.y * coin.sprite.texture.height);
                                bool gold = coin.sprite.rect.Contains(pixel), money = cash.sprite.rect.Contains(pixel);
                                if (!gold && !money) continue;
                                check(graphic.material == (gold ? coin.material : cash.material), "Native batch retains displayed icon material");
                                float y = graphic.transform.TransformPoint(center).y;
                                if (gold) { sawCoins = true; if (float.IsNaN(firstCoinY)) firstCoinY = y; else coinsRose |= y > firstCoinY + 100; }
                                else { sawCash = true; if (float.IsNaN(firstCashY)) firstCashY = y; else cashRose |= y > firstCashY + 100; }
                            }
                        }
                        ScreenCapture.CaptureScreenshot(Path.Combine(frames, frame.ToString("D3") + ".png"));
                    }
                }
                check(sawCoins && sawCash, "Both displayed currency sprites animate / " + prefix);
                check(coinsRose && cashRose, "Both currencies move upward toward header / " + prefix);
                check(coinsArrived == 1 && cashArrived == 1, "Both arrival callbacks run exactly once / " + prefix);
                await PayPalCapture(folder, prefix + "-Finished.png");
            }
            // Page closure must finish each pending callback once, without leaving particles alive.
            int cancelled = 0;
            VFXUtils.PlayItemCollectFx(E_ItemType.Gold, page.slotBtn.transform.position, () => cancelled++, target:coin.transform, animation:RewardCollectAnimation.BurstCollect);
            CloseRuntime(); await UniTask.Delay(250, ignoreTimeScale:true);
            check(cancelled == 1, "Closing the destination page safely completes pending collection once");
            check(beforeGold == ItemUtils.GetItemCount(E_ItemType.Gold) && beforeCash == ItemUtils.GetItemCount(E_ItemType.Dollar), "Review never grants or changes currency");
            check(beforeAds == SaveDataUtils.GameData.todayAdTimes && beforeSpins == SlotProgressUtil.Current, "Review never watches ads or consumes spins");
        }
    }
}
