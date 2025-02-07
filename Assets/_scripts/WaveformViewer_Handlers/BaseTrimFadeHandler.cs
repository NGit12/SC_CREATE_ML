using static TrimFadeTypes;
using UnityEngine.EventSystems;
using UnityEngine.UI.Extensions;
using UnityEngine;
using System.Collections.Generic;
using static TrimFadeTypes;

public abstract class BaseTrimFadeHandler : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    public RectTransform parentPanel;
    public RectTransform trimPoint;
    public RectTransform fadePoint;
    public UILineRenderer fadeLine;

    [Header("Settings")]
    public float movementSmoothing = 10f;
    [Range(1f, 20f)] public float dynamicAlignmentTolerance = 10f;

    [Header("Position Settings")]
    [SerializeField] protected float trimYOffset = -145f;
    [SerializeField] protected float fadeYOffset = 148f;

    // Movement state
    protected bool isDraggingTrim = false;
    protected bool isDraggingFade = false;
    protected bool isFadePointModified = false;
    protected Vector2 trimPointerOffset;
    protected Vector2 fadePointerOffset;
    protected Vector2 targetTrimPosition;
    protected Vector2 targetFadePosition;

    // Events
    public delegate void TrimFadePositionHandler(TrimFadePoints points);
    public event TrimFadePositionHandler OnPositionsChanged;

    protected virtual void Start()
    {
        ValidateComponents();
        InitializePositions();
    }

    protected abstract void InitializePositions();
    protected abstract void HandleTrimDrag(Vector2 localPoint);
    protected abstract void HandleFadeDrag(Vector2 localPoint);
    protected abstract Vector2 GetFadeLineBottomEdge();
    protected abstract void ValidatePositions();

    #region Position Conversion Methods
    protected Vector2 NormalizedToPanel(Vector2 normalizedPos, bool isTrimPoint)
    {
        float panelWidth = parentPanel.rect.width;
        float xPos = (normalizedPos.x * panelWidth) - (panelWidth / 2);
        float yPos = isTrimPoint ? trimYOffset : fadeYOffset;
        return new Vector2(xPos, yPos);
    }

    public Vector2 PanelToNormalized(Vector2 panelPos)
    {
        float panelWidth = parentPanel.rect.width;
        float normalizedX = (panelPos.x + (panelWidth / 2)) / panelWidth;
        return new Vector2(normalizedX, panelPos.y);
    }

    public (Vector2 trim, Vector2 fade) GetNormalizedPositions()
    {
        Vector2 normalizedTrim = PanelToNormalized(trimPoint.anchoredPosition);
        Vector2 normalizedFade = PanelToNormalized(fadePoint.anchoredPosition);
        return (normalizedTrim, normalizedFade);
    }
    #endregion

    #region Position Management
    public virtual void SetPositions(Vector2 trimPos, Vector2 fadePos)
    {
        Vector2 panelTrimPos = NormalizedToPanel(trimPos, true);
        Vector2 panelFadePos = NormalizedToPanel(fadePos, false);

        targetTrimPosition = panelTrimPos;
        targetFadePosition = panelFadePos;

        trimPoint.anchoredPosition = panelTrimPos;
        fadePoint.anchoredPosition = panelFadePos;

        ValidatePositions();
    }

    protected void NotifyPositionsChanged()
    {
        if (OnPositionsChanged != null)
        {
            var (normalizedTrim, normalizedFade) = GetNormalizedPositions();
            OnPositionsChanged.Invoke(new TrimFadePoints
            {
                TrimPosition = normalizedTrim,
                FadePosition = normalizedFade
            });
        }
    }
    #endregion

    #region Interface Implementations
    public virtual void OnPointerDown(PointerEventData eventData)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (result.gameObject == trimPoint.gameObject)
            {
                HandleTrimPointDown(eventData);
                break;
            }
            else if (result.gameObject == fadePoint.gameObject)
            {
                HandleFadePointDown(eventData);
                break;
            }
        }
    }

    public virtual void OnDrag(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentPanel, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        if (isDraggingTrim)
        {
            HandleTrimDrag(localPoint);
        }
        else if (isDraggingFade)
        {
            HandleFadeDrag(localPoint);
        }

        if (isDraggingTrim || isDraggingFade)
        {
            NotifyPositionsChanged();
        }
    }

    public virtual void OnPointerUp(PointerEventData eventData)
    {
        isDraggingTrim = false;
        isDraggingFade = false;
    }
    #endregion

    #region Handler Methods
    protected virtual void HandleTrimPointDown(PointerEventData eventData)
    {
        isDraggingTrim = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentPanel, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
        trimPointerOffset = trimPoint.anchoredPosition - localPoint;
    }

    protected virtual void HandleFadePointDown(PointerEventData eventData)
    {
        isDraggingFade = true;
        isFadePointModified = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentPanel, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
        fadePointerOffset = fadePoint.anchoredPosition - localPoint;
    }
    #endregion


    #region Update Methods
    protected virtual void LateUpdate()
    {
        UpdatePositions();
        UpdateFadeLine();
        ValidatePositions();
    }

    private void UpdatePositions()
    {
        trimPoint.anchoredPosition = Vector2.Lerp(
            trimPoint.anchoredPosition,
            targetTrimPosition,
            Time.deltaTime * movementSmoothing
        );

        fadePoint.anchoredPosition = Vector2.Lerp(
            fadePoint.anchoredPosition,
            targetFadePosition,
            Time.deltaTime * movementSmoothing
        );
    }

    protected virtual void UpdateFadeLine()
    {
        Vector2 trimTopEdge = new Vector2(
            trimPoint.anchoredPosition.x,
            trimPoint.anchoredPosition.y + (trimPoint.rect.height * 0.5f));

        Vector2 fadeBottomEdge = GetFadeLineBottomEdge();

        fadeLine.Points = new Vector2[] { trimTopEdge, fadeBottomEdge };
        fadeLine.SetVerticesDirty();
    }
    #endregion

    #region Helper Methods
    private void ValidateComponents()
    {
        if (parentPanel == null || trimPoint == null || fadePoint == null || fadeLine == null)
        {
            Debug.LogError("TrimFadeHandler: Required components are missing!", this);
        }
    }

    protected bool IsAlignedWithTrim(float tolerance)
    {
        return Mathf.Abs(fadePoint.anchoredPosition.x - trimPoint.anchoredPosition.x) <= tolerance;
    }
    #endregion
}

