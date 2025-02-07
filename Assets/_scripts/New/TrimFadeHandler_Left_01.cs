using UnityEngine;
using UnityEngine.EventSystems;

public class TrimFadeHandler_Left_01 : BaseTrimFadeHandler
{
    #region Connected Handler References
    public RectTransform rightHandlerTrimPoint;
    public RectTransform rightHandlerFadePoint;
    #endregion

    #region Base Class Implementations
    protected override void InitializePositions()
    {
        if (Mathf.Approximately(trimPoint.anchoredPosition.x, 0) &&
            Mathf.Approximately(fadePoint.anchoredPosition.x, 0))
        {
            Vector2 defaultTrimPos = NormalizedToPanel(new Vector2(0, 0), true);
            Vector2 defaultFadePos = NormalizedToPanel(new Vector2(0, 0), false);

            trimPoint.anchoredPosition = defaultTrimPos;
            fadePoint.anchoredPosition = defaultFadePos;

            targetTrimPosition = defaultTrimPos;
            targetFadePosition = defaultFadePos;

            Debug.Log($"[TrimFadeHandler_Left] Initialized positions - Trim: {defaultTrimPos}, Fade: {defaultFadePos}");
        }
    }
    protected override void HandleTrimDrag(Vector2 localPoint)
    {
        float leftBoundary = parentPanel.rect.xMin;
        float rightBoundary = rightHandlerTrimPoint != null ?
            rightHandlerTrimPoint.anchoredPosition.x : parentPanel.rect.xMax;

        // Clamp position within boundaries
        float clampedX = Mathf.Clamp(localPoint.x + trimPointerOffset.x, leftBoundary, rightBoundary);
        targetTrimPosition = new Vector2(clampedX, trimPoint.anchoredPosition.y);

        // Update fade point if not modified or aligned
        if (!isFadePointModified || IsAlignedWithTrim(dynamicAlignmentTolerance))
        {
            targetFadePosition = new Vector2(clampedX, fadePoint.anchoredPosition.y);
            isFadePointModified = false;
        }

        // Immediately notify position change
        NotifyPositionsChanged();
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);

        // Ensure final position is notified
        NotifyPositionsChanged();
    }

    protected override void HandleFadeDrag(Vector2 localPoint)
    {
        float leftBoundary = trimPoint.anchoredPosition.x;
        float rightBoundary = rightHandlerFadePoint != null ?
            rightHandlerFadePoint.anchoredPosition.x : parentPanel.rect.xMax;

        float clampedX = Mathf.Clamp(localPoint.x + fadePointerOffset.x, leftBoundary, rightBoundary);
        targetFadePosition = new Vector2(clampedX, fadePoint.anchoredPosition.y);


    }

    protected override void ValidatePositions()
    {
        if (targetFadePosition.x < targetTrimPosition.x)
        {
            targetFadePosition = new Vector2(targetTrimPosition.x, targetFadePosition.y);
        }
    }

    protected override Vector2 GetFadeLineBottomEdge()
    {
        return new Vector2(
            Mathf.Max(fadePoint.anchoredPosition.x, trimPoint.anchoredPosition.x),
            fadePoint.anchoredPosition.y - (fadePoint.rect.height * 0.5f));
    }
    #endregion
}
