using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BubblePics
{
    /// <summary>
    /// Persistent registry for the 1.0.9 meta-flow UI restored from Godot.
    /// Keeping the page references in one Resources asset mirrors
    /// UIManager's PackedScene table without scattering string paths through
    /// gameplay code.
    /// </summary>
    [CreateAssetMenu(fileName = "MetaFlowCatalog", menuName = "BubblePics/Meta Flow Catalog")]
    public sealed class MetaFlowCatalog : ScriptableObject
    {
        public const string ResourcePath = "Config/MetaFlowCatalog";

        [Serializable]
        public struct Entry
        {
            public MetaFlowId Id;
            public GameObject Prefab;
        }

        [SerializeField] Entry[] _entries = Array.Empty<Entry>();

        [Header("1.0.9 release defaults")]
        // The release profile enables these flows through AppConfig. Serialized
        // values remain useful as prefab-level developer overrides.
        [SerializeField] bool _dailyFirstStepEnabled;
        [SerializeField] bool _dailyIncentiveWinEnabled;
        [SerializeField] bool _completeEncourageEnabled;
        [SerializeField] int _dailyFirstStepBonus = 3;
        [SerializeField] int _completeEncourageThreshold = 60;
        [SerializeField] int _chapterSize = 25;

        static MetaFlowCatalog _current;

        public bool DailyFirstStepEnabled =>
            _dailyFirstStepEnabled || AppConfig.DailyFirstStepBonus;
        public bool DailyIncentiveWinEnabled =>
            _dailyIncentiveWinEnabled || AppConfig.DailyIncentiveWin;
        public bool CompleteEncourageEnabled =>
            _completeEncourageEnabled || AppConfig.CompletePageEncourageText;
        public int DailyFirstStepBonus => Mathf.Max(0, _dailyFirstStepBonus);
        public int CompleteEncourageThreshold =>
            Mathf.Clamp(_completeEncourageThreshold, 0, 100);
        public int ChapterSize => Mathf.Max(1, _chapterSize);
        public Entry[] Entries => _entries;

        public static MetaFlowCatalog Current
        {
            get
            {
                if (_current == null)
                    _current = Resources.Load<MetaFlowCatalog>(ResourcePath);
                return _current;
            }
        }

        public bool TryGet(MetaFlowId id, out GameObject prefab)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Id != id) continue;
                prefab = _entries[i].Prefab;
                return prefab != null;
            }

#if UNITY_EDITOR
            // Disabled release flows are intentionally not referenced by the
            // runtime catalog. Keep their prefabs inspectable from GM/editor
            // tools without making Unity include them in every player.
            string editorPath = EditorPrefabPath(id);
            if (!string.IsNullOrEmpty(editorPath))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(editorPath);
                if (prefab != null) return true;
            }
#endif
            prefab = null;
            return false;
        }

#if UNITY_EDITOR
        static string EditorPrefabPath(MetaFlowId id)
        {
            const string pages = "Assets/RuntimePrefabs/Pages/";
            const string ui = "Assets/RuntimePrefabs/UI/";
            return id switch
            {
                MetaFlowId.CategoryMergeBanner => pages + "CategoryMergeBanner.prefab",
                MetaFlowId.UnlimitLuckyPage => pages + "UnlimitLuckyPage.prefab",
                MetaFlowId.BonusLevelBanner => pages + "BonusLevelBanner.prefab",
                MetaFlowId.NewScenePage => pages + "NewScenePage.prefab",
                MetaFlowId.StarfishTutorialPage => pages + "StarfishTutorialPage.prefab",
                MetaFlowId.DailyFirstStepBonusPanel => ui + "DailyFirstStepBonusPanel.prefab",
                MetaFlowId.ShapePuzzleBanner => pages + "ShapePuzzleBanner.prefab",
                MetaFlowId.CompleteEncourageBanner => ui + "CompleteEncourageBanner.prefab",
                MetaFlowId.LuckyBreakRoundIntro => ui + "LuckyBreakRoundIntro.prefab",
                MetaFlowId.PreviewPieces => ui + "PreviewPieces.prefab",
                MetaFlowId.RainbowTutorialPage => pages + "RainbowTutorialPage.prefab",
                MetaFlowId.DailyIncentiveWinPage => pages + "DailyIncentiveWinPage.prefab",
                MetaFlowId.BonusRewardPage => pages + "BonusRewardPage.prefab",
                MetaFlowId.WordMergeBanner => pages + "WordMergeBanner.prefab",
                MetaFlowId.LuckyBreakBadge => ui + "LuckyBreakBadge.prefab",
                MetaFlowId.SeaHeroBanner => pages + "SeaHeroBanner.prefab",
                MetaFlowId.NumberMatchBanner => pages + "NumberMatchBanner.prefab",
                _ => string.Empty,
            };
        }
#endif

        public void ConfigurePrefabAuthoring(Entry[] entries)
        {
            _entries = entries ?? Array.Empty<Entry>();
        }

        public static void SetCurrent(MetaFlowCatalog catalog)
        {
            _current = catalog;
        }
    }

    public enum MetaFlowId
    {
        CategoryMergeBanner,
        UnlimitLuckyPage,
        BonusLevelBanner,
        NewScenePage,
        StarfishTutorialPage,
        DailyFirstStepBonusPanel,
        ShapePuzzleBanner,
        CompleteEncourageBanner,
        LuckyBreakRoundIntro,
        PreviewPieces,
        RainbowTutorialPage,
        DailyIncentiveWinPage,
        BonusRewardPage,
        WordMergeBanner,
        LuckyBreakBadge,
        SeaHeroBanner,
        NumberMatchBanner,
    }
}
