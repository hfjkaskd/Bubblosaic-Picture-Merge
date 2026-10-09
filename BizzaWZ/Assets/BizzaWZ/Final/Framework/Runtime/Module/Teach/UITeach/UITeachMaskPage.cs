using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityExtensions;

public class UITeachMaskPage : UIPageBase<UITeachMaskPage.InitParam>
{
     [Obfuz.ObfuzIgnore]
    public enum Type
    {
        None,
        Path,
        Pos,
    }
  [Obfuz.ObfuzIgnore]

    public enum Shape
    {
        [LabelText("矩形")] Rect,
        [LabelText("圆形")] Circle,
    }

    public struct InitParam
    {
        public Action<UITeachMaskPage> onOpen;
        public Action onClose;
        public GameObject target;
        public Type type;
        public Vector3 worldPos;
        public string path;
        public float width;
        public float height;
        public bool block;
        public Shape shape;
        public float alpha;
        public int retryCount;
        public float retryInterval;
        public float clickCD;
        public bool bgBlock;
        public bool showHand;
    }

    public PageId LegacyPageType => UIPageIds.UI_TeachMask;

    private InitParam param;

    public RectTransform maskParent;
    public Image imgMask;
    public Image block;
    public Image finger;
    public Sprite circleSprite;
    [SerializeField] private Button[] backgroundButtons;
    private GameObject target;
    private Button targetButton;
    private bool savedInteractable;
    private bool temporarilyDisabled;
    private Coroutine cooldownRoutine;
    private float clickTime;
    private RectTransform targetRect;
    private RectTransform maskViewport;
    private Camera targetCamera;
    private Camera maskCamera;
    private readonly Vector3[] targetCorners = new Vector3[4];
    public bool TargetReady { get; private set; }
    private void Awake()
    {
        foreach(var button in backgroundButtons) button.onClick.AddListener(OnBackgroundClick);
    }
    private void OnBackgroundClick()
    {
        if(param.bgBlock && Time.unscaledTime>=clickTime) CloseSelf();
    }
    private void OnTargetClick()
    {
        if(Time.unscaledTime>=clickTime) CloseSelf();
    }
    private void UnbindTarget()
    {
        if(cooldownRoutine!=null){StopCoroutine(cooldownRoutine);cooldownRoutine=null;}
        if(targetButton!=null){targetButton.onClick.RemoveListener(OnTargetClick);if(temporarilyDisabled)targetButton.interactable=savedInteractable;}
        temporarilyDisabled=false;targetButton=null;
    }
    private IEnumerator RestoreAfterCooldown(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if(targetButton!=null)targetButton.interactable=savedInteractable;
        temporarilyDisabled=false;cooldownRoutine=null;
    }

    protected override void OnOpen(InitParam param)
    {
        UnbindTarget();
        this.param = param;
        TargetReady = false;
        targetRect = null;
        target = null;
        clickTime = float.MaxValue;
        maskParent.sizeDelta = Vector2.zero;
        maskParent.position = new Vector3(-10000, 0, 0);
        finger.gameObject.SetObjActive(false);
        block.raycastTarget = true;
        if (param.type == Type.Pos)
        {
            if (param.target != null)
            { 
                target = param.target;
            }
            Init();
        }
        else if (param.type == Type.Path)
        {
            StartCoroutine(GameObjUitl.FindGameObject(param.path, findObj =>
            {
                target = findObj;
                maskParent.gameObject.SetActive(false);
                // Invoke(nameof(Init), 0.03f);
                GameUtils.DelayDo(() =>
                {
                    Init();
                }, 0.05f);
            }, CloseSelf, param.retryInterval, param.retryCount));
        }

        this.param.onOpen?.Invoke(this);
    }

    public static (Vector2 pos, Vector2 size) GetCanvasPosByTransform(Transform target, bool isUI)
    {
        var canvas=UIModule.Instance.UICanvas;var rect=(RectTransform)canvas.transform;
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        Vector2 screen=isUI?RectTransformUtility.WorldToScreenPoint(camera,target.position):(Vector2)Camera.main.WorldToScreenPoint(target.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screen,camera,out Vector2 local);
        Vector2 size=target is RectTransform rt?rt.rect.size*rt.lossyScale.x/rect.lossyScale.x:Vector2.zero;
        return(local-rect.rect.min,size);
    }

    public void Init()
    {
        UnbindTarget();
        maskParent.gameObject.SetActive(true);
        Vector2 size=new Vector2(param.width,param.height);
        Vector2 position;
        if(target!=null)
        {
            var mapped=GetCanvasPosByTransform(target.transform,target.transform is RectTransform);
            position=mapped.pos;if(size.x<=0&&size.y<=0)size=mapped.size;
        }
        else
        {
            var canvas=UIModule.Instance.UICanvas;var rect=(RectTransform)canvas.transform;
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var screen=Camera.main.WorldToScreenPoint(param.worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screen,camera,out Vector2 local);
            position=local-rect.rect.min;
        }
        maskParent.anchoredPosition=position;maskParent.sizeDelta=size;
        FocusTarget(target != null ? target.transform as RectTransform : null, param.width, param.height);
        imgMask.enabled=param.shape==Shape.Circle;

        Color color = block.color;
        color.a = param.alpha / 255;
        block.color = color;
        foreach(var button in backgroundButtons)
        {
            button.targetGraphic.raycastTarget=param.block||param.bgBlock;
            button.interactable=true;
        }
        finger.gameObject.SetActive(param.showHand);
        clickTime=Time.unscaledTime+Mathf.Max(0,param.clickCD);
        UnbindTarget();
        targetButton=target!=null?target.GetComponent<Button>():null;
        if(targetButton!=null)
        {
            targetButton.onClick.AddListener(OnTargetClick);
            if(param.block&&param.clickCD>0)
            {
                savedInteractable=targetButton.interactable;
                temporarilyDisabled=true;targetButton.interactable=false;
                cooldownRoutine=StartCoroutine(RestoreAfterCooldown(param.clickCD));
            }
        }
    }

    public void FocusTarget(RectTransform rect, float width = -1f, float height = -1f)
    {
        targetRect = rect;
        param.width = width;
        param.height = height;
        maskViewport = (RectTransform)maskParent.parent;
        targetCamera = CanvasCamera(targetRect);
        maskCamera = CanvasCamera(maskViewport);
        TargetReady = true;
        RefreshTargetBounds();
    }

    private static Camera CanvasCamera(Transform item)
    {
        var canvas = item != null ? item.GetComponentInParent<Canvas>() : null;
        if (canvas != null) canvas = canvas.rootCanvas;
        return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    private void LateUpdate()
    {
        if (TargetReady) RefreshTargetBounds();
    }

    private void RefreshTargetBounds()
    {
        if (targetRect == null || maskViewport == null) return;
        targetRect.GetWorldCorners(targetCorners);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        for (int i = 0; i < targetCorners.Length; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, targetCorners[i]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(maskViewport, screen, maskCamera, out var local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        Vector2 anchor = maskViewport.rect.min + Vector2.Scale(maskViewport.rect.size, maskParent.anchorMin);
        Vector2 position = (min + max) * .5f - anchor;
        Vector2 size = new Vector2(param.width > 0 ? param.width : max.x - min.x,
            param.height > 0 ? param.height : max.y - min.y);
        if (maskParent.anchoredPosition != position) maskParent.anchoredPosition = position;
        if (maskParent.sizeDelta != size) maskParent.sizeDelta = size;
    }

    protected override void OnShow()
    {
    }

    protected override void OnHide()
    {
    }

    protected override void OnClose()
    {
        TargetReady = false;
        targetRect = null;
        UnbindTarget();
        this.param.onClose?.Invoke();
        // fakeButtonTwon.onClick.RemoveListener(CloseSelf);
    }
}


public static class UIPositionHelper
{
    /// <summary>
    /// 把世界坐标点转换到 UI 坐标（RectTransform 坐标系下）
    /// </summary>
    /// <param name="worldPos">世界坐标</param>
    /// <param name="worldCamera">渲染世界物体的相机</param>
    /// <param name="uiParent">目标 UI 的父节点（通常是 Canvas 下的某个 RectTransform）</param>
    /// <param name="uiCamera">UI 相机（如果 Canvas 是 Overlay 模式可以传 null）</param>
    /// <returns>UI 坐标 (anchoredPosition 用)</returns>
    public static Vector2 WorldToUIPosition(Vector3 worldPos, Camera worldCamera, RectTransform uiParent, Camera uiCamera = null)
    {
        // 1. 世界坐标 -> 屏幕像素坐标
        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPos);

        // 2. 屏幕像素坐标 -> UI局部坐标
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiParent,
            screenPos,
            uiCamera,
            out localPos);

        return localPos;
    }
}

public static partial class UIPageIds
{
    public static readonly PageId UI_TeachMask = "UI_TeachMask";
}
