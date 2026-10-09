using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static partial class AllUiAuthoring
    {
        static async UniTask ReviewRewardFxMaterialsCore(string folder,Action<bool,string> check)
        {
            LanguageUtils.SelectedLanguage="pt-BR";Localization.SetLocale("pt_BR");
            float beforeGold=ItemUtils.GetItemCount(E_ItemType.Gold),beforeCash=ItemUtils.GetItemCount(E_ItemType.Dollar);
            int beforeAds=SaveDataUtils.GameData.todayAdTimes;
            var p=(GetRewardPanel)await UIModule.Instance.OpenPage(UIPageIds.GetRewardPanel,
                new ItemEntry{Type=E_ItemType.Gold,Count=1000},new ItemEntry{Type=E_ItemType.Dollar,Count=11.34f},
                DoubleGetRewardPanel.E_UseScene.DailyTask,(Action<bool>)null);
            inspectedPage=p;await UniTask.Delay(1000,ignoreTimeScale:true);
            foreach(var animation in new[]{RewardCollectAnimation.Legacy,RewardCollectAnimation.BurstCollect})
            foreach(var type in new[]{E_ItemType.Dollar,E_ItemType.Gold,E_ItemType.Dollar})
            {
                if(!VFXUtils.itemFlyTarget.TryGetValue(type,out var target)||target==null)
                    throw new InvalidOperationException("Production reward target is missing.");
                var targetImage=target.GetComponent<Image>()??target.GetComponentInChildren<Image>();
                var source=ItemUtils.GetItemIcon(type);
                Material material=null;
                if(source==null&&targetImage!=null){source=targetImage.sprite;material=targetImage.material;}
                check(source!=null,"Production source sprite exists / "+(int)type);
                check(material!=null,"Production source retains authored material / "+(int)type);
                check(Application.isFocused,"Unity Game view is focused for actual animation playback");
                check(material != null && material.shader.name == "BubblePics/UI/ApprovedHudFrame",
                    "Source uses the authored cutout shader: " + AssetDatabase.GetAssetPath(source) + " / " + AssetDatabase.GetAssetPath(material));
                int completed=0;
                string prefix=(animation==RewardCollectAnimation.Legacy?"Legacy-":"Native-")+(type==E_ItemType.Dollar?"Cash":"Gold");
                VFXUtils.PlayItemCollectFx(type,p.claimBtn.transform.position,()=>completed++,target:target,animation:animation);
                await UniTask.Delay(180,ignoreTimeScale:true);
                if(animation==RewardCollectAnimation.Legacy)
                {
                    int icons=0;
                    foreach(var icon in UIModule.Instance.RewardItemLayer.GetComponentsInChildren<Image>())
                    {
                        if(!icon.name.StartsWith("ItemCollect"))continue;
                        icons++;check(icon.sprite==source&&icon.material==material,"Sprite and material remain paired / "+prefix+" / "+icon.name);
                    }
                    check(icons>0,"Actual pooled reward icons are visible / "+prefix);
                }
                await PayPalCapture(folder,prefix+"-Start.png");
                await UniTask.Delay(230,ignoreTimeScale:true);
                await PayPalCapture(folder,prefix+"-Scatter.png");
                await UniTask.Delay(400,ignoreTimeScale:true);
                await PayPalCapture(folder,prefix+"-Flight.png");
                await UniTask.Delay(1900,ignoreTimeScale:true);
                check(completed==1,"Original completion callback occurs exactly once / "+prefix);
            }
            check(Mathf.Approximately(beforeGold,ItemUtils.GetItemCount(E_ItemType.Gold))&&Mathf.Approximately(beforeCash,ItemUtils.GetItemCount(E_ItemType.Dollar)),
                "Visual-only playback does not grant or change currency");
            check(beforeAds==SaveDataUtils.GameData.todayAdTimes,"Visual-only playback does not watch ads");
            CloseRuntime();
        }
    }
}
