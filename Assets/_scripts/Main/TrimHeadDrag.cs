using UnityEngine;
using UnityEngine.EventSystems;

public class TrimHeadDrag : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    public RectTransform viewerRect;  // Reference to the Waveform Viewer Rect
    public RectTransform trimHeadRect;  // Reference to the Trim Head Rect

    private float viewerHalfWidth;

    private void Start()
    {
        // Dynamically calculate the half-width of the viewer at runtime
        viewerHalfWidth = viewerRect.rect.width / 2;
        Debug.Log($"Viewer Half-Width: {viewerHalfWidth}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("TrimHead drag started");
        trimHeadRect.localScale = Vector3.one * .9f; // Slightly enlarge for feedback
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Convert screen point to local point within the viewerRect
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewerRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPointerPosition
        );

        // Clamp the X position within the bounds of the viewer
        float clampedX = Mathf.Clamp(localPointerPosition.x, -viewerHalfWidth, viewerHalfWidth);

        // Update the Trim Head's position
        trimHeadRect.anchoredPosition = new Vector2(clampedX, trimHeadRect.anchoredPosition.y);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("TrimHead drag ended");
        trimHeadRect.localScale = Vector3.one; // Reset scale
    }
}
