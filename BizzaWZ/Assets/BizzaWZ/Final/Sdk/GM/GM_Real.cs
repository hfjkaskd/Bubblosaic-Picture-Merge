#if UNITY_EDITOR && BIZZA_REAL_WITHDRAW
using UnityEditor;
using UnityEngine;

public static class GM_Real
{
    [MenuItem("Tools/Bizza/Test Daily Mission %#d")]
    private static void TestDailyMission()
    {
        if (!Application.isPlaying) return;

        var uiModule = UIModule.Instance;
        var panel = uiModule != null ? uiModule.GetPage<DailyMissionPanel>() : null;
        if (panel == null || !panel.isActiveAndEnabled || panel.IsClosing)
        {
            Debug.LogWarning("请先在运行模式中打开每日任务面板，再执行每日任务测试。");
            return;
        }

        panel.EditorTestDailyMission();
    }

    [MenuItem("Tools/Bizza/Test Daily Mission %#d", true)]
    private static bool ValidateTestDailyMission()
    {
        return Application.isPlaying;
    }
}
#endif
