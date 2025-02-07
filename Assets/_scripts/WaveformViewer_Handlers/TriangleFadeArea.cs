using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple implementation of fade triangle visualization
/// </summary>
public class TriangleFadeArea : Graphic
{
    public enum HandlerType { Left, Right }

    [SerializeField] private HandlerType handlerType = HandlerType.Left;

    public HandlerType CurrentHandlerType
    {
        get => handlerType;
        set
        {
            handlerType = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        // Create a simple vertex
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        // Define triangle points based on handler type
        if (handlerType == HandlerType.Left)
        {
            // Left handler triangle
            vertex.position = new Vector3(rectTransform.rect.width, rectTransform.rect.height, 0);
            vh.AddVert(vertex);  // Top right

            vertex.position = new Vector3(0, 0, 0);
            vh.AddVert(vertex);  // Bottom left

            vertex.position = new Vector3(0, rectTransform.rect.height, 0);
            vh.AddVert(vertex);  // Top left
        }
        else
        {
            // Right handler triangle
            vertex.position = new Vector3(0, rectTransform.rect.height, 0);
            vh.AddVert(vertex);  // Top left

            vertex.position = new Vector3(rectTransform.rect.width, 0, 0);
            vh.AddVert(vertex);  // Bottom right

            vertex.position = new Vector3(rectTransform.rect.width, rectTransform.rect.height, 0);
            vh.AddVert(vertex);  // Top right
        }

        // Add the triangle
        vh.AddTriangle(0, 1, 2);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}