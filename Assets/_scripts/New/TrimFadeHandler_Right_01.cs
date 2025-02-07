using UnityEngine;

public class TrimFadeHandler_Right_01 : BaseTrimFadeHandler
{
    #region Connected Handler References
    public RectTransform leftHandlerTrimPoint;
    public RectTransform leftHandlerFadePoint;
    #endregion

    #region Base Class Implementations
    protected override void InitializePositions()
    {
        if (Mathf.Approximately(trimPoint.anchoredPosition.x, 0) &&
            Mathf.Approximately(fadePoint.anchoredPosition.x, 0))
        {
            // Set trim point to right edge (1,0)
            Vector2 defaultTrimPos = NormalizedToPanel(new Vector2(1, 0), true);
            // Set fade point to right edge initially (1,0)
            Vector2 defaultFadePos = NormalizedToPanel(new Vector2(1, 0), false);

            trimPoint.anchoredPosition = defaultTrimPos;
            fadePoint.anchoredPosition = defaultFadePos;

            targetTrimPosition = defaultTrimPos;
            targetFadePosition = defaultFadePos;

            Debug.Log($"[TrimFadeHandler_Right] Initialized positions - Trim: {defaultTrimPos}, Fade: {defaultFadePos}");
        }
    }

    protected override void HandleTrimDrag(Vector2 localPoint)
    {
        float leftBoundary = leftHandlerTrimPoint != null ?
            leftHandlerTrimPoint.anchoredPosition.x : parentPanel.rect.xMin;
        float rightBoundary = parentPanel.rect.xMax;

        // Clamp position within boundaries
        float clampedX = Mathf.Clamp(localPoint.x + trimPointerOffset.x, leftBoundary, rightBoundary);
        targetTrimPosition = new Vector2(clampedX, trimPoint.anchoredPosition.y);

        // Update fade point if not modified or within alignment tolerance
        if (!isFadePointModified || IsAlignedWithTrim(dynamicAlignmentTolerance))
        {
            targetFadePosition = new Vector2(clampedX, fadePoint.anchoredPosition.y);
            isFadePointModified = false;
        }

        NotifyPositionsChanged();
    }

    protected override void HandleFadeDrag(Vector2 localPoint)
    {
        float leftBoundary = leftHandlerTrimPoint != null ?
            leftHandlerTrimPoint.anchoredPosition.x : parentPanel.rect.xMin;
        float rightBoundary = trimPoint.anchoredPosition.x;

        float clampedX = Mathf.Clamp(localPoint.x + fadePointerOffset.x, leftBoundary, rightBoundary);
        targetFadePosition = new Vector2(clampedX, fadePoint.anchoredPosition.y);


    }

    protected override void ValidatePositions()
    {
        // Check if fade point has moved beyond trim point
        if (targetFadePosition.x > targetTrimPosition.x)
        {
            targetFadePosition = new Vector2(targetTrimPosition.x, fadePoint.anchoredPosition.y);
            isFadePointModified = false;
        }
    }

    protected override Vector2 GetFadeLineBottomEdge()
    {
        return new Vector2(
            Mathf.Min(fadePoint.anchoredPosition.x, trimPoint.anchoredPosition.x),
            fadePoint.anchoredPosition.y - (fadePoint.rect.height * 0.5f));
    }
    #endregion
}