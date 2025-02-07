using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles the drag-and-drop functionality for source buttons,
/// ensuring they can only be dragged after a file is assigned
/// and snap back if released outside a trigger box.
/// </summary>
public class DraggableSourceButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Drag Settings")]
    /// <summary>Flag indicating if the button has a file assigned and can be dragged.</summary>
    public bool isAssigned = false;

    [Header("Lerp Settings")]
    /// <summary>Smoothness of the dragging movement.</summary>
    public float dragLerpSpeed = 10f;

    /// <summary>Speed at which the button snaps back to its original position.</summary>
    public float snapBackSpeed = 5f;

    [Header("Camera Reference")]
    /// <summary>Reference to the main camera for screen-to-world position conversion.</summary>
    private Camera mainCamera;

    [Header("State and Position")]
    /// <summary>The button's original starting position.</summary>
    private Vector3 startingPosition;

    /// <summary>Drag offset to ensure smooth dragging.</summary>
    private Vector3 dragOffset;

    /// <summary>Flag to determine if the button is currently being dragged.</summary>
    private bool isDragging = false;

    /// <summary>Flag to determine if the button should snap back to its original position.</summary>
    private bool shouldSnapBack = false;

    [Header("Trigger Settings")]
    /// <summary>Cached reference to the trigger box collider.</summary>
    private Collider2D triggerBox;

    private void Start()
    {
        // Cache the starting position of the button
        startingPosition = transform.position;

        // Cache the main camera
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[DraggableSourceButton] Main Camera not found.");
        }

        // Cache the TriggerBox
        triggerBox = GameObject.FindWithTag("TriggerBox")?.GetComponent<Collider2D>();
        if (triggerBox == null)
        {
            Debug.LogError("[DraggableSourceButton] TriggerBox not found. Dragging may not work as expected.");
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isAssigned)
        {
            //Debug.Log($"[DraggableSourceButton] {gameObject.name} is not assigned. Dragging is not allowed.");
            return;
        }

        //Debug.Log($"[DraggableSourceButton] Pointer Down detected on {gameObject.name}");

        if (EventSystem.current.IsPointerOverGameObject(eventData.pointerId))
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                GetComponent<RectTransform>(),
                Input.mousePosition,
                mainCamera,
                out Vector3 globalMousePos
            );

            dragOffset = transform.position - globalMousePos;
            isDragging = true;
            shouldSnapBack = false; // Prevent snap-back during dragging
            //Debug.Log($"[DraggableSourceButton] Dragging started on {gameObject.name}");
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDragging)
        {
            isDragging = false;
            //Debug.Log($"[DraggableSourceButton] Dragging stopped on {gameObject.name}");

            // Check if the button is outside the trigger box
            if (triggerBox != null && !triggerBox.OverlapPoint(transform.position))
            {
                shouldSnapBack = true;
                //Debug.Log($"[DraggableSourceButton] {gameObject.name} released outside TriggerBox, snapping back.");
            }
        }
    }

    private void Update()
    {
        if (isDragging)
        {
            HandleDragging();
        }
        else if (shouldSnapBack)
        {
            HandleSnapBack();
        }
    }

    /// <summary>
    /// Handles the smooth dragging behavior using Lerp.
    /// </summary>
    private void HandleDragging()
    {
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            GetComponent<RectTransform>(),
            Input.mousePosition,
            mainCamera,
            out Vector3 globalMousePos
        );

        // Smoothly move to the dragged position using Lerp
        Vector3 targetPosition = globalMousePos + dragOffset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * dragLerpSpeed);
    }

    /// <summary>
    /// Smoothly snaps the button back to its original position using Lerp.
    /// </summary>
    private void HandleSnapBack()
    {
        transform.position = Vector3.Lerp(transform.position, startingPosition, Time.deltaTime * snapBackSpeed);

        // Stop snapping if the button is close enough to the starting position
        if (Vector3.Distance(transform.position, startingPosition) < 0.01f)
        {
            transform.position = startingPosition;
            shouldSnapBack = false;
            //Debug.Log($"[DraggableSourceButton] {gameObject.name} snapped back to original position.");
        }
    }
}
