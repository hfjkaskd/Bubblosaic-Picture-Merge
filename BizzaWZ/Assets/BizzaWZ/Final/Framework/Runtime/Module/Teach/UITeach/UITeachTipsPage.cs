
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sirenix.OdinInspector;

public class UITeachTipsPage : UIPageBase<UITeachTipsPage.InitParam>
{
    public struct InitParam
    {
        public string content;
        public int posIdx;
        public bool block;
        public float alpha;
        public float heightValue;
    }

    public PageId LegacyPageType => UIPageIds.UI_TeachTip;

    public Image bg = null;
    public Image panel = null;
    public TMP_Text content = null;
    public RectTransform[] panels;
    [SerializeField] private Button dismissButton;
    [SerializeField] private Button backdropButton;
    [SerializeField] private bool followHighlight;
    [SerializeField, Min(0)] private float targetGap = 24f;
    [SerializeField, Min(0)] private float viewportMargin = 24f;
    [SerializeField, Range(0f, .5f)] private float tailOffset = .2f;
    private UITeachMaskPage highlightedPage;
    private RectTransform panelParent;
    private readonly Vector3[] focusCorners = new Vector3[4];
    private void Awake()
    {
        dismissButton.onClick.AddListener(OnClick);
        backdropButton.onClick.AddListener(OnClick);
    }

    private float clickCD = 0;

    protected override void OnOpen(InitParam param)
    {
        FollowHighlight(followHighlight && !param.block ? UIModule.Instance.GetPage<UITeachMaskPage>() : null);
        content.text = LanguageUtils.GetText(param.content);
        if (param.posIdx >= 0)
        {
            panel.rectTransform.position = panels[param.posIdx].position;
        }
        else
        {
            var parentRect = panel.transform.parent as RectTransform;
            var rect = parentRect.rect;
            float yPos = Mathf.Lerp(rect.yMin, rect.yMax, param.heightValue);
            panel.transform.position = parentRect.TransformPoint(new Vector3(0, yPos));
        }

        bg.raycastTarget = param.block;
        Color color = bg.color;
        color.a = param.alpha / 255;
        bg.color = color;
        PositionBesideHighlight();
    }

    private void LateUpdate()
    {
        PositionBesideHighlight();
    }

    public void FollowHighlight(UITeachMaskPage page)
    {
        highlightedPage = page;
        panelParent = (RectTransform)panel.transform.parent;
        SetTailDirection(false);
        PositionBesideHighlight();
    }

    private void PositionBesideHighlight()
    {
        if (highlightedPage == null || !highlightedPage.TargetReady || !highlightedPage.gameObject.activeInHierarchy)
            return;
        highlightedPage.maskParent.GetWorldCorners(focusCorners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        for (int i = 0; i < focusCorners.Length; i++)
        {
            Vector2 local = panelParent.InverseTransformPoint(focusCorners[i]);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        var rect = panel.rectTransform;
        Vector2 size = Vector2.Scale(rect.rect.size, new Vector2(Mathf.Abs(rect.localScale.x), Mathf.Abs(rect.localScale.y)));
        Rect viewport = panelParent.rect;
        float above = max.y + targetGap + size.y * .5f;
        bool below = above + size.y * .5f > viewport.yMax - viewportMargin;
        float y = below ? min.y - targetGap - size.y * .5f : above;
        bool left = (min.x + max.x) * .5f < viewport.center.x;
        float x = Mathf.Clamp((min.x + max.x) * .5f - (left ? -1f : 1f) * tailOffset * size.x,
            viewport.xMin + viewportMargin + size.x * .5f, viewport.xMax - viewportMargin - size.x * .5f);
        Vector3 position = panelParent.TransformPoint(new Vector3(x, y, 0));
        if (rect.position != position) rect.position = position;
        SetTailDirection(below, left);
    }

    private void SetTailDirection(bool upwards, bool left = false)
    {
        // Flip the bubble artwork; its child text retains the normal orientation.
        Vector3 scale = panel.rectTransform.localScale;
        float sign = upwards ? -1f : 1f;
        scale.x = Mathf.Abs(scale.x) * (left ? -1f : 1f);
        scale.y = Mathf.Abs(scale.y) * sign;
        if (panel.rectTransform.localScale != scale) panel.rectTransform.localScale = scale;
        scale = content.rectTransform.localScale;
        scale.x = Mathf.Abs(scale.x) * (left ? -1f : 1f);
        scale.y = Mathf.Abs(scale.y) * sign;
        if (content.rectTransform.localScale != scale) content.rectTransform.localScale = scale;
    }

    private void Update()
    {
        clickCD -= Time.unscaledDeltaTime;
    }

    protected override void OnShow()
    {
    }

    protected override void OnHide()
    {
    }

    protected override void OnClose()
    {
        highlightedPage = null;
    }

    public void OnClick()
    {
        if (clickCD > 0)
        {
            return;
        }

        CloseSelf();
    }

}

public static partial class UIPageIds
{
    public static readonly PageId UI_TeachTip = "UI_TeachTip";
}
