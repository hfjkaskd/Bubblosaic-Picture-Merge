using AdvancedInputFieldPlugin;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

// The prefab supplies movable form content; the background and modal barrier
// remain outside it and never move with the keyboard.
public class KeyBoardPanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private RectTransform panel;
    public RectTransform panelSizeRect;

    [Header("Keyboard Move")]
    [SerializeField] private float transitionTime = 0.25f;
    [SerializeField] private float desiredGapToKeyboard = 350;
    [LabelText("是否可以拖拽")] public bool dragable = true;

    public float autoOffsetY;
    public float dragOffsetY;

    private Canvas canvas;
    private Vector2 originalPanelPos;
    private Vector2 startPos;
    private Vector2 endPos;
    private float currentTime;
    private int lastKeyboardHeight;
    private GameObject lastSelectedObject;
    private bool initialized;
    private bool animating;
    private bool dragging;
    private readonly Vector3[] corners = new Vector3[4];

    private Camera CanvasCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay
        ? null : canvas.rootCanvas.worldCamera;

    private void Start() => EnsureInitialized();

    private bool EnsureInitialized()
    {
        if (initialized) return true;
        if (panel == null) return false;
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        originalPanelPos = panel.anchoredPosition;
        initialized = true;
        return true;
    }

    private void OnEnable()
    {
        NativeKeyboardManager.AddKeyboardHeightChangedListener(OnKeyboardHeightChanged);
    }

    private void OnDisable()
    {
        NativeKeyboardManager.RemoveKeyboardHeightChangedListener(OnKeyboardHeightChanged);
        if (initialized && panel != null) panel.anchoredPosition = originalPanelPos;
        initialized = animating = dragging = false;
        lastKeyboardHeight = 0;
        lastSelectedObject = null;
        autoOffsetY = dragOffsetY = 0f;
    }

    private void Update()
    {
        if (!EnsureInitialized()) return;
        if (animating)
        {
            currentTime += Time.unscaledDeltaTime;
            float t = transitionTime <= 0f ? 1f : Mathf.Clamp01(currentTime / transitionTime);
            panel.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            if (t >= 1f) animating = false;
        }

        if (lastKeyboardHeight <= 0 || dragging) return;
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == lastSelectedObject) return;
        lastSelectedObject = selected;
        if (TryGetSelectedField(out var field)) RevealField(field);
    }

    public void OnKeyboardHeightChanged(int keyboardHeight)
    {
        if (!EnsureInitialized()) return;
        lastKeyboardHeight = Mathf.Max(0, keyboardHeight);
        dragging = false;
        lastSelectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (lastKeyboardHeight == 0)
        {
            AnimateToOffset(0f);
        }
        else if (TryGetSelectedField(out var field))
        {
            RevealField(field);
        }
        else
        {
            AnimateToOffset(ClampOffset(panel.anchoredPosition.y - originalPanelPos.y));
        }
    }

    private bool TryGetSelectedField(out RectTransform field)
    {
        field = null;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null || !selected.transform.IsChildOf(panel) ||
            selected.GetComponent<AdvancedInputField>() == null) return false;
        field = selected.transform as RectTransform;
        return field != null;
    }

    private void RevealField(RectTransform field)
    {
        float pixelsPerUnit = PixelsPerPanelUnit();
        float currentOffset = panel.anchoredPosition.y - originalPanelPos.y;
        float restingBottom = BottomScreenY(field) - currentOffset * pixelsPerUnit;
        float offset = (lastKeyboardHeight - restingBottom) / pixelsPerUnit + desiredGapToKeyboard;
        AnimateToOffset(ClampOffset(offset));
    }

    private float PixelsPerPanelUnit()
    {
        // Sample a longer segment to avoid subtracting nearly equal screen
        // coordinates on tall/scaled canvases and accumulating rounding drift.
        const float sampleDistance = 100f;
        Transform parent = panel.parent;
        Vector3 origin = parent != null ? parent.TransformPoint(Vector3.zero) : Vector3.zero;
        Vector3 sample = Vector3.up * sampleDistance;
        Vector3 up = parent != null ? parent.TransformPoint(sample) : sample;
        return Mathf.Max(0.0001f, Mathf.Abs(
            RectTransformUtility.WorldToScreenPoint(CanvasCamera, up).y -
            RectTransformUtility.WorldToScreenPoint(CanvasCamera, origin).y) / sampleDistance);
    }

    private float BottomScreenY(RectTransform rect)
    {
        rect.GetWorldCorners(corners);
        float bottom = float.PositiveInfinity;
        for (int i = 0; i < corners.Length; i++)
            bottom = Mathf.Min(bottom, RectTransformUtility.WorldToScreenPoint(CanvasCamera, corners[i]).y);
        return bottom;
    }

    private float ClampOffset(float offset)
    {
        if (lastKeyboardHeight <= 0) return 0f;
        float pixelsPerUnit = PixelsPerPanelUnit();
        float currentOffset = panel.anchoredPosition.y - originalPanelPos.y;
        float restingBottom = BottomScreenY(panelSizeRect != null ? panelSizeRect : panel)
            - currentOffset * pixelsPerUnit;
        float maxOffset = Mathf.Max(0f,
            (lastKeyboardHeight - restingBottom) / pixelsPerUnit + desiredGapToKeyboard);
        return Mathf.Clamp(offset, 0f, maxOffset);
    }

    private void AnimateToOffset(float offset)
    {
        autoOffsetY = offset;
        dragOffsetY = 0f;
        startPos = panel.anchoredPosition;
        endPos = originalPanelPos + Vector2.up * offset;
        currentTime = 0f;
        animating = transitionTime > 0f;
        if (!animating) panel.anchoredPosition = endPos;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = dragable && lastKeyboardHeight > 0 && EnsureInitialized();
        if (dragging) animating = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragable || lastKeyboardHeight <= 0 || !EnsureInitialized()) return;
        animating = false;
        float currentOffset = panel.anchoredPosition.y - originalPanelPos.y;
        float offset = ClampOffset(currentOffset + eventData.delta.y / PixelsPerPanelUnit());
        dragOffsetY = offset - autoOffsetY;
        panel.anchoredPosition = originalPanelPos + Vector2.up * offset;
    }

    public void OnEndDrag(PointerEventData eventData) => dragging = false;

    public float GetInputToKeyboardHeight()
    {
        if (lastKeyboardHeight <= 0 || !EnsureInitialized() || !TryGetSelectedField(out var field)) return -1f;
        return BottomScreenY(field) - lastKeyboardHeight;
    }
}
