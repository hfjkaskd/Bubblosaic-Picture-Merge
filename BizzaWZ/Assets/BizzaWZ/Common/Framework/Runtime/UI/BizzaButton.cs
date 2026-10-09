using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Sirenix.OdinInspector;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class ButtonState
{
    public int StateId;

    [LabelText("开关状态跟随按钮状态的对象")]
    public List<GameObject> StateObjects;


    [LabelText("进入时 开启的对象")]
    public List<GameObject> StateToEnableObjects;

    [LabelText("进入时 关闭的对象")]
    public List<GameObject> StateDisableObjects;

    public void SetState(bool isOn)
    {
        foreach (var stateObj in StateObjects)
        {
            stateObj?.SetActive(isOn);
        }

        if (isOn)
        {
            foreach (var stateObj in StateToEnableObjects)
            {
                stateObj?.SetActive(true);
            }

            foreach (var toDisable in StateDisableObjects)
            {
                toDisable?.SetActive(false);
            }
        }
    }
}

public class BizzaButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler, IInitializePotentialDragHandler
{
    public Transform scaleTarget;
    public bool canDrag = true;

    // Optional standard Unity Button, configured only on migrated withdrawal prefabs.
    [SerializeField] private Button standardButton;
    private ScrollRect standardScroll;

    [SerializeField, Min(0f)]
    private float pressScaleRatio = 0.92f;

    [SerializeField] private bool animateStandardButtonPress;
    private Coroutine pressReleaseRoutine;

    public float longClickPeriod = 0.5f; // 长按时间

    public bool interactable = true; // 是否可交互

    public UnityEvent onLongClick;

    public UnityEvent onClick;

    private bool isLongClick = false; // 是否长按

    private float m_LastInvokeTime;

    public List<ButtonState> ButtonStates = new List<ButtonState>();

    public int StateId;

    private Vector3 m_NormalScale = Vector3.one;

    private bool m_IsPointerDown;

    private void Awake()
    {
        if (standardButton != null) standardScroll = GetComponentInParent<ScrollRect>();
        if (scaleTarget == null)
        {
            scaleTarget = transform.Find("Scale");
        }

        if (scaleTarget == null)
        {
            scaleTarget = transform;
        }
        m_NormalScale = scaleTarget.localScale;
        ChangeButtonState(0);
    }

    private void OnEnable()
    {
        if (standardButton != null) standardButton.interactable = interactable;
    }

    private void OnDisable()
    {
        if (standardButton != null) standardButton.interactable = false;
        bool wasReleasing = pressReleaseRoutine != null;
        StopPressRelease();
        RestoreNormalScale();
        if (wasReleasing && scaleTarget != null) scaleTarget.localScale = m_NormalScale;
    }

    private void Update()
    {
        if (standardButton != null) return;
        if (interactable && isLongClick)
        {
            if (Time.time - m_LastInvokeTime >= longClickPeriod)
            {
                onLongClick.Invoke();

                m_LastInvokeTime = Time.time;
            }
        }
    }

    // 按下事件
    public void OnPointerDown(PointerEventData eventData)
    {
        if (standardButton != null)
        {
            if (animateStandardButtonPress && standardButton.IsInteractable()) ApplyPressedScale();
            return;
        }
        ApplyPressedScale();
        // if (interactable)
        // {
        // isLongClick = true;
        // }
        SoundManager.Instance.PlaySFX("SFX_Click");
    }

    // 抬起事件
    public void OnPointerUp(PointerEventData eventData)
    {
        if (standardButton != null)
        {
            if (animateStandardButtonPress) RestoreNormalScale(true);
            return;
        }
        RestoreNormalScale();
        // isLongClick = false;
        if (interactable && RectTransformUtility.RectangleContainsScreenPoint(gameObject.GetComponent<RectTransform>(), eventData.position))
        {
            onClick.Invoke();
        }
    }

    public void StopClick()
    {
        if (interactable)
        {
            isLongClick = false;
        }
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (standardButton != null)
        {
            if (canDrag) standardScroll?.OnDrag(eventData);
            return;
        }
        if (canDrag)
        {
            GetComponentInParent<ScrollRect>()?.OnDrag(eventData);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (standardButton != null)
        {
            if (canDrag && standardScroll != null)
            {
                eventData.eligibleForClick = false;
                standardScroll.OnBeginDrag(eventData);
            }
            return;
        }
        if (canDrag)
        {
            GetComponentInParent<ScrollRect>()?.OnBeginDrag(eventData);
        }
    }

    void IEndDragHandler.OnEndDrag(PointerEventData eventData)
    {
        if (standardButton != null)
        {
            if (canDrag) standardScroll?.OnEndDrag(eventData);
            return;
        }
        if (canDrag)
        {
            GetComponentInParent<ScrollRect>()?.OnEndDrag(eventData);
        }
    }

    private void ApplyPressedScale()
    {
        if (scaleTarget == null) return;
        bool wasReleasing = pressReleaseRoutine != null;
        StopPressRelease();
        if (!m_IsPointerDown)
        {
            if (!wasReleasing) m_NormalScale = scaleTarget.localScale;
            m_IsPointerDown = true;
        }
        scaleTarget.localScale = m_NormalScale * pressScaleRatio;
    }

    private void RestoreNormalScale(bool animate = false)
    {
        if (!m_IsPointerDown || scaleTarget == null)
        {
            return;
        }

        m_IsPointerDown = false;
        if (animate && isActiveAndEnabled)
        {
            pressReleaseRoutine = StartCoroutine(AnimatePressRelease(scaleTarget.localScale, m_NormalScale));
        }
        else
        {
            scaleTarget.localScale = m_NormalScale;
        }
    }

    private IEnumerator AnimatePressRelease(Vector3 from, Vector3 normal)
    {
        const float reboundDuration = 0.09f;
        const float settleDuration = 0.09f;
        Vector3 rebound = normal * 1.05f;
        for (float elapsed = 0f; elapsed < reboundDuration; elapsed += Time.unscaledDeltaTime)
        {
            scaleTarget.localScale = Vector3.LerpUnclamped(from, rebound, Mathf.SmoothStep(0f, 1f, elapsed / reboundDuration));
            yield return null;
        }
        for (float elapsed = 0f; elapsed < settleDuration; elapsed += Time.unscaledDeltaTime)
        {
            scaleTarget.localScale = Vector3.LerpUnclamped(rebound, normal, Mathf.SmoothStep(0f, 1f, elapsed / settleDuration));
            yield return null;
        }
        scaleTarget.localScale = normal;
        pressReleaseRoutine = null;
    }

    private void StopPressRelease()
    {
        if (pressReleaseRoutine == null) return;
        StopCoroutine(pressReleaseRoutine);
        pressReleaseRoutine = null;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (standardButton != null && canDrag) standardScroll?.OnInitializePotentialDrag(eventData);
    }


    public void ChangeButtonState(int stateId)
    {
        StateId = stateId;
        foreach (var item in ButtonStates)
        {
            item.SetState(item.StateId == stateId);
        }
    }

}
