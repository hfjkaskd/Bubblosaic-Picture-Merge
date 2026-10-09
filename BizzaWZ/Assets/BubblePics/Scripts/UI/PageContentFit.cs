using UnityEngine;
using UnityEngine.EventSystems;

namespace BubblePics
{
    /// <summary>Fits the prefab's authored content rectangle with one uniform scale.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class PageContentFit : UIBehaviour
    {
        [SerializeField] RectTransform content;
        RectTransform viewport;
        public RectTransform Content => content;

        protected override void OnEnable() { base.OnEnable(); Fit(); }
        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); Fit(); }

        void Fit()
        {
            if (content == null) return;
            if (viewport == null) viewport = (RectTransform)transform;
            Vector2 size = content.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            float scale = Mathf.Min(viewport.rect.width / size.x, viewport.rect.height / size.y);
            if (scale <= 0) return;
            if (!Mathf.Approximately(content.localScale.x, scale) || !Mathf.Approximately(content.localScale.y, scale))
                content.localScale = new Vector3(scale, scale, 1);
        }
    }
}
