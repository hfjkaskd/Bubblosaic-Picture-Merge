using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Entry point used by restored gameplay plugins. Pages are instantiated
    /// only from authored prefabs and then retained, matching Godot UIManager.
    /// </summary>
    public static class MetaFlowPresenter
    {
        static readonly Dictionary<MetaFlowId, MetaFlowView> Views = new();

        public static T Get<T>(MetaFlowId id) where T : MetaFlowView
        {
            if (Views.TryGetValue(id, out var cached) && cached != null)
                return cached as T;

            var catalog = MetaFlowCatalog.Current;
            if (catalog == null || !catalog.TryGet(id, out var prefab))
            {
                Debug.LogError($"Meta-flow prefab is not configured: {id}");
                return null;
            }

            if (App.I == null)
            {
                Debug.LogError($"Cannot show {id} before App has initialized.");
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, App.I.transform, false);
            var mounts = instance.GetComponent<PrefabMountSet>();
            mounts?.Attach(App.I);
            var view = instance.GetComponent<T>() ?? instance.GetComponentInChildren<T>(true);
            if (view == null)
            {
                Debug.LogError($"Meta-flow prefab '{prefab.name}' has no {typeof(T).Name}.");
                UnityEngine.Object.Destroy(instance);
                return null;
            }

            AssetLib.ApplyLocalizedFonts(instance.transform);
            view.InitializePrefabRuntime();
            Views[id] = view;
            return view;
        }

        public static MetaIntroBannerView ShowBanner(MetaFlowId id, int? percent = null)
        {
            var view = Get<MetaIntroBannerView>(id);
            view?.Play(percent);
            return view;
        }

        public static DailyIncentiveWinView ShowDailyIncentiveWin()
        {
            var catalog = MetaFlowCatalog.Current;
            if (catalog != null && !catalog.DailyIncentiveWinEnabled) return null;
            var view = Get<DailyIncentiveWinView>(MetaFlowId.DailyIncentiveWinPage);
            view?.Play();
            return view;
        }

        public static DailyFirstStepBonusView ShowDailyFirstStepBonus(
            RectTransform movesTarget,
            Action<int> grantSteps,
            bool previewOnly = false)
        {
            var catalog = MetaFlowCatalog.Current;
            if (catalog != null && !catalog.DailyFirstStepEnabled) return null;
            var view = Get<DailyFirstStepBonusView>(MetaFlowId.DailyFirstStepBonusPanel);
            int amount = catalog != null ? catalog.DailyFirstStepBonus : 3;
            view?.Play(amount, movesTarget, grantSteps, previewOnly);
            return view;
        }

        public static BonusRewardView ShowBonusReward(
            Sprite icon,
            int count,
            RectTransform target,
            Action completed = null)
        {
            var view = Get<BonusRewardView>(MetaFlowId.BonusRewardPage);
            view?.Play(icon, count, target, completed);
            return view;
        }

        public static UnlimitedLuckyView ShowUnlimitedLucky(
            RectTransform badge,
            Action badgeArrived = null)
        {
            var view = Get<UnlimitedLuckyView>(MetaFlowId.UnlimitLuckyPage);
            view?.Play(badge, badgeArrived);
            return view;
        }

        public static LuckyBreakRoundIntroView ShowLuckyBreakRoundIntro(
            RectTransform movesTarget,
            Action arrived = null)
        {
            var view = Get<LuckyBreakRoundIntroView>(MetaFlowId.LuckyBreakRoundIntro);
            view?.Play(movesTarget, arrived);
            return view;
        }

        public static TutorialSpotlightView ShowTutorial(
            MetaFlowId id,
            Sprite bubblePreview,
            float bubbleDiameter = 200f)
        {
            var view = Get<TutorialSpotlightView>(id);
            view?.Play(bubblePreview, bubbleDiameter);
            return view;
        }

        public static NewSceneView ShowNewScene(Sprite chapterImage, Action claimed = null)
        {
            var view = Get<NewSceneView>(MetaFlowId.NewScenePage);
            view?.Play(chapterImage, claimed);
            return view;
        }

        public static CompleteEncourageView ShowCompleteEncourage(
            CompletePerformance performance,
            float centerY = -1f)
        {
            var catalog = MetaFlowCatalog.Current;
            if (catalog != null && !catalog.CompleteEncourageEnabled) return null;
            var result = CompleteEncouragePicker.Pick(
                performance,
                catalog != null ? catalog.CompleteEncourageThreshold : 60);
            var view = Get<CompleteEncourageView>(MetaFlowId.CompleteEncourageBanner);
            view?.Play(result, centerY);
            return view;
        }

        public static void Hide(MetaFlowId id)
        {
            if (Views.TryGetValue(id, out var view) && view != null)
                view.Hide();
        }

        public static void HideAll()
        {
            foreach (var pair in Views)
                if (pair.Value != null)
                    pair.Value.Hide();
        }
    }
}
