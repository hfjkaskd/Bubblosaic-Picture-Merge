using System;
using System.Collections;
using UnityEngine;

namespace BubblePics
{
    /// <summary>Common lifetime and authored-mount handling for meta pages.</summary>
    public abstract class MetaFlowView : MonoBehaviour
    {
        [SerializeField] protected RectTransform panelRoot;
        [SerializeField] protected Transform worldRoot;
        [SerializeField] protected CanvasGroup canvasGroup;

        Coroutine _activeRoutine;

        public bool Visible { get; private set; }
        public event Action<MetaFlowView> Closed;

        public virtual void InitializePrefabRuntime()
        {
            if (panelRoot == null)
                panelRoot = FindNamed<RectTransform>(transform, "PanelMount");
            if (worldRoot == null)
                worldRoot = FindNamed<Transform>(transform, "WorldMount");
            if (canvasGroup == null && panelRoot != null)
                canvasGroup = panelRoot.GetComponent<CanvasGroup>();
            SetRootsActive(false);
        }

        public virtual void Show()
        {
            StopActiveRoutine();
            Visible = true;
            SetRootsActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public virtual void Hide()
        {
            StopActiveRoutine();
            if (!Visible && !RootsActive()) return;
            Visible = false;
            SetRootsActive(false);
            Closed?.Invoke(this);
        }

        public virtual void HandleBackRequest()
        {
            Hide();
        }

        public void ConfigurePrefabAuthoring(
            RectTransform authoredPanelRoot,
            Transform authoredWorldRoot,
            CanvasGroup authoredCanvasGroup)
        {
            panelRoot = authoredPanelRoot;
            worldRoot = authoredWorldRoot;
            canvasGroup = authoredCanvasGroup;
        }

        protected void Run(IEnumerator routine)
        {
            StopActiveRoutine();
            if (routine != null)
                _activeRoutine = StartCoroutine(RunTracked(routine));
        }

        protected void StopActiveRoutine()
        {
            if (_activeRoutine == null) return;
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }

        protected static string Text(string key, string fallback)
        {
            string value = Localization.Tr(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }

        protected static string FormatPercent(string template, int percent)
        {
            template ??= string.Empty;
            if (template.Contains("{0}"))
                return string.Format(template, percent);
            if (template.Contains("%d"))
            {
                // Recovered Godot translations use printf-style "%d%%".
                // Replacing only the integer token leaves a visible double
                // percent sign in Unity.
                return template
                    .Replace("%d", percent.ToString())
                    .Replace("%%", "%");
            }
            return template;
        }

        protected static T FindNamed<T>(Transform parent, string objectName)
            where T : Component
        {
            if (parent == null) return null;
            foreach (var component in parent.GetComponentsInChildren<T>(true))
                if (component.name == objectName)
                    return component;
            return null;
        }

        IEnumerator RunTracked(IEnumerator routine)
        {
            yield return routine;
            _activeRoutine = null;
        }

        void SetRootsActive(bool active)
        {
            if (panelRoot != null) panelRoot.gameObject.SetActive(active);
            if (worldRoot != null) worldRoot.gameObject.SetActive(active);
        }

        bool RootsActive()
        {
            return (panelRoot != null && panelRoot.gameObject.activeSelf) ||
                   (worldRoot != null && worldRoot.gameObject.activeSelf);
        }
    }
}
