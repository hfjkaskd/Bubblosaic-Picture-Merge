#if BIZZA_REAL_WITHDRAW
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BubblePics.EditorTools
{
    public static class BizzaRewardInputInspection
    {
        public static void Open()
        {
            if (!EditorApplication.isPlaying || UIModule.Instance == null)
                throw new InvalidOperationException("Start gameplay first.");
            if (UIModule.Instance.GetPage<GetRewardPanel>() == null)
                Real_GetRewardPanelUtil.OpenGetRewardPanel(DoubleGetRewardPanel.E_UseScene.WinPanel);
        }

        public static void Inspect(string folder)
        {
            var page = UIModule.Instance != null ? UIModule.Instance.GetPage<GetRewardPanel>() : null;
            Directory.CreateDirectory(folder);
            if (!EditorApplication.isPlaying || page == null)
            {
                File.WriteAllText(Path.Combine(folder, "reward-input-state.txt"), "Reward page is not open.");
                return;
            }
            Canvas.ForceUpdateCanvases();
            var report = new StringBuilder();
            report.AppendLine("utc=" + DateTime.UtcNow.ToString("O"));
            report.AppendLine("closing=" + page.IsClosing + " transparent=" + TransparentBlock.IsBlock + " loading=" + LoadingBlock.IsBlock);
            report.AppendLine("tutorialComplete=" + SaveDataUtils.GameData.customTutorialEnd);
            foreach (var legacy in new[] { page.closeBtn, page.claimBtn })
            {
                var button = legacy.GetComponent<Button>();
                var rect = (RectTransform)legacy.transform;
                var canvas = legacy.GetComponentInParent<Canvas>().rootCanvas;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                report.AppendLine("button=" + legacy.name + " active=" + legacy.isActiveAndEnabled +
                    " legacyInteractable=" + legacy.interactable + " standardInteractable=" + button.interactable +
                    " effectiveInteractable=" + button.IsInteractable() + " screen=" + screen);
                foreach (var group in legacy.GetComponentsInParent<CanvasGroup>(true))
                    report.AppendLine("group=" + group.name + " interactable=" + group.interactable + " blocksRaycasts=" + group.blocksRaycasts);
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                foreach (var hit in hits)
                {
                    var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject);
                    report.AppendLine("hit=" + PathOf(hit.gameObject.transform) + " handler=" + (handler != null ? handler.name : "none"));
                }
            }
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "reward-input-state.txt"), report.ToString());
        }

        static string PathOf(Transform current)
        {
            string path = current.name;
            while (current.parent != null) { current = current.parent; path = current.name + "/" + path; }
            return path;
        }
    }
}
#endif
