using UnityEngine;

namespace BubblePics
{
    /// <summary>
    /// Reusable build and safety policy for the in-game GM panel.
    /// The authored asset lives at Resources/Config/BubblePicsGmConfig.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BubblePicsGmConfig",
        menuName = "BubblePics/GM 调试配置")]
    public sealed class GmConfig : ScriptableObject
    {
        const string ResourcePath = "Config/BubblePicsGmConfig";
        static GmConfig _current;

        [Header("可用渠道")]
        [Tooltip("允许在 Unity 编辑器的运行模式中使用 GM。")]
        public bool enableInEditor = true;

        [Tooltip("允许在 Development Build 中使用 GM。")]
        public bool enableInDevelopmentBuild = true;

        [Tooltip("正式 Release 包默认必须关闭。")]
        public bool enableInReleaseBuild;

        [Header("编辑器显示")]
        [Tooltip(
            "仅在 Unity 编辑器 Play Mode 中放大 SRDebugger；" +
            "移动端仍使用 Settings.asset 中的原始倍率。")]
        [Range(1f, 3f)]
        public float editorSrDebuggerUiScale = 1.5f;

        [Header("安全")]
        [Tooltip("清除全部存档、退出等高风险命令必须先打开危险操作开关。")]
        public bool requireDangerousActionUnlock = true;

        [Tooltip("GM 可写入的金币和道具数量上限，避免错误输入溢出。")]
        [Min(1)]
        public int maxWritableAmount = 999999999;

        public static GmConfig Current
        {
            get
            {
                if (_current == null)
                    _current = Resources.Load<GmConfig>(ResourcePath);
                return _current;
            }
        }

        public static bool IsAvailable
        {
            get
            {
#if BUBBLEPICS_WHITE_PACKAGE
                GmConfig config = Current;
                if (Application.isEditor)
                    return config == null || config.enableInEditor;
                if (Debug.isDebugBuild)
                    return config == null || config.enableInDevelopmentBuild;
                return config != null && config.enableInReleaseBuild;
#else
                return false;
#endif
            }
        }

        public static int ClampAmount(int value)
        {
            int max = Current != null
                ? Mathf.Max(1, Current.maxWritableAmount)
                : 999999999;
            return Mathf.Clamp(value, 0, max);
        }

#if UNITY_EDITOR
        public static void ClearEditorCache()
        {
            _current = null;
        }
#endif
    }
}
