using UnityEngine;
using UnityEngine.EventSystems;

public class TestDraggable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private bool isDragging = false;
    private Vector3 dragOffset;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[TestDraggable] Main Camera is not found.");
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (EventSystem.current.IsPointerOverGameObject(eventData.pointerId))
        {
            Debug.Log($"[TestDraggable] Pointer over UI, starting drag on {gameObject.name}");

            // Calculate drag offset
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                GetComponent<RectTransform>(),
                Input.mousePosition,
                mainCamera,
                out Vector3 globalMousePos
            );

            dragOffset = transform.position - globalMousePos;
            isDragging = true;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDragging)
        {
            Debug.Log($"[TestDraggable] Dragging stopped on {gameObject.name}");
            isDragging = false;
        }
    }

    private void Update()
    {
        if (isDragging)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                GetComponent<RectTransform>(),
                Input.mousePosition,
                mainCamera,
                out Vector3 globalMousePos
            );

            transform.position = globalMousePos + dragOffset;
        }
    }
}
