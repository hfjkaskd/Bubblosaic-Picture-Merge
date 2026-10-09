using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UITeachMaskFocusPage : UIPageBase<UITeachMaskFocusPage.FocusArgs>
{
    public struct FocusArgs
    {
        public Transform target;
        public float width;
        public float height;
        public float alpha;
    }
    public RectTransform maskParent;
    public Image block;
    private Transform target;
    private Camera cam;
    private FocusArgs focusArgs;
    protected override void OnOpen(UITeachMaskFocusPage.FocusArgs args)
    {
        this.focusArgs = args;

        this.target = this.focusArgs.target;

        cam = Camera.main;

        Vector2 canvasPos;

        Vector2 size = new Vector2(focusArgs.width, focusArgs.height);

        var (outPos, outSize) = GetCanvasPosByTransform(target.transform, target.transform is RectTransform);

        canvasPos = outPos;

        if (size is { x: <= 0, y: <= 0 })
        {
            size = outSize;
        }

        // maskParent
        maskParent.anchoredPosition = canvasPos;
        maskParent.sizeDelta = size;

        Color color = block.color;
        color.a = this.focusArgs.alpha / 255;
        block.color = color;
    }

    private (Vector2 pos, Vector2 size) GetCanvasPosByTransform(Transform target, bool isUI)
    { 
        return UITeachMaskPage.GetCanvasPosByTransform(target, isUI);
    }

    void Update()
    {
        if (cam != null)
        {
            var (outPos, outSize) = GetCanvasPosByTransform(target.transform, target.transform is RectTransform);
            
            maskParent.anchoredPosition = outPos;
        }
    }

    protected override void OnClose()
    {
    }

    protected override void OnHide()
    {
    }

    public PageId LegacyPageType => UIPageIds.UI_TeachMaskFocus;

    protected override void OnShow()
    {
    }
}

public static partial class UIPageIds
{
    public static readonly PageId UI_TeachMaskFocus = "UITeachMaskFocusPage";
}
