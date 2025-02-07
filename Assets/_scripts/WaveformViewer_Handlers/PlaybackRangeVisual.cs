using UnityEngine;
using UnityEngine.UI;

public class PlaybackRangeVisual : MonoBehaviour
{
    #region Constants
    private const float MIN_DELTA_FOR_UPDATE = 0.001f;      // Minimum change required for update
    private const float FORCE_UPDATE_THRESHOLD = 0.5f;      // Threshold for forced updates
    private const float SMOOTH_UPDATE_THRESHOLD = 0.01f;    // Threshold for smooth updates
    private const float VELOCITY_MULTIPLIER = 1.5f;         // Velocity multiplier for smoother movement
    #endregion

    #region Serialized Fields
    [Header("Required References")]
    [SerializeField] private RectTransform parentPanel;
    [SerializeField] private Image rangeImage;

    [Header("Visual Settings")]
    [SerializeField] private Color rangeColor = new Color(0.5f, 0.5f, 1f, 0.2f);
    [SerializeField] private bool useGradient = true;
    [SerializeField] private float heightOffset = 2f;

    [Header("Animation Settings")]
    [SerializeField] private float smoothSpeed = 25f;
    [SerializeField] private float updateThreshold = 0.005f;
    [SerializeField] private float snapThreshold = 0.1f;
    #endregion

    #region Private Variables
    private RectTransform rangeRect;
    private Vector2 targetPosition;
    private float targetWidth;
    private bool needsUpdate;
    private bool isDirty;
    private bool isAnimating;
    private Vector2 currentVelocity;
    private float widthVelocity;
    private Vector2 previousTargetPosition;
    private float previousTargetWidth;
    private Rect panelRect;
    #endregion

    #region Unity Lifecycle Methods
    private void Awake()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        InitializeVisual();
    }

    private void Start()
    {
        if (!rangeRect)
        {
            InitializeVisual();
        }
    }

    private void Update()
    {
        if (!needsUpdate || !isAnimating) return;

        if (ShouldUpdatePositions())
        {
            UpdateSmoothPositions();
        }
        else
        {
            needsUpdate = false;
            isAnimating = false;
        }
    }

    private void OnDisable()
    {
        needsUpdate = false;
        isAnimating = false;
    }
    #endregion

    #region Initialization Methods
    private void InitializeComponents()
    {
        rangeRect = rangeImage != null ? rangeImage.rectTransform : null;
        if (parentPanel != null)
        {
            panelRect = parentPanel.rect;
        }
    }

    private void InitializeVisual()
    {
        if (!rangeImage)
        {
            CreateRangeImage();
        }

        UpdateRangeImageProperties();
        SetupRectTransforms();
        SetRangeSize(0);
    }

    private void CreateRangeImage()
    {
        GameObject visualObj = new GameObject("RangeVisual", typeof(RectTransform), typeof(Image));
        visualObj.transform.SetParent(transform, false);
        rangeImage = visualObj.GetComponent<Image>();
        rangeRect = visualObj.GetComponent<RectTransform>();
    }

    private void UpdateRangeImageProperties()
    {
        if (isDirty || rangeImage.color != rangeColor)
        {
            rangeImage.color = rangeColor;
            isDirty = false;
        }
        rangeImage.transform.SetAsFirstSibling();
    }
    #endregion

    #region Transform Setup Methods
    private void SetupRectTransforms()
    {
        SetupParentTransform();
        SetupRangeTransform();
    }

    private void SetupParentTransform()
    {
        RectTransform thisRect = (RectTransform)transform;
        if (thisRect.anchorMin != Vector2.zero || thisRect.anchorMax != Vector2.one)
        {
            thisRect.anchorMin = Vector2.zero;
            thisRect.anchorMax = Vector2.one;
            thisRect.offsetMin = Vector2.zero;
            thisRect.offsetMax = Vector2.zero;
        }
    }

    private void SetupRangeTransform()
    {
        if (rangeRect != null)
        {
            if (rangeRect.anchorMin != Vector2.zero || rangeRect.anchorMax != new Vector2(0, 1))
            {
                rangeRect.anchorMin = Vector2.zero;
                rangeRect.anchorMax = new Vector2(0, 1);
                rangeRect.pivot = new Vector2(0, 0.5f);
                rangeRect.anchoredPosition = Vector2.zero;
                rangeRect.sizeDelta = new Vector2(0, -heightOffset * 2);
            }
        }
    }
    #endregion

    #region Update Methods
    private bool ShouldUpdatePositions()
    {
        if (!rangeRect) return false;

        float positionDelta = Vector2.Distance(rangeRect.anchoredPosition, targetPosition);
        float widthDelta = Mathf.Abs(rangeRect.sizeDelta.x - targetWidth);

        return positionDelta > MIN_DELTA_FOR_UPDATE || widthDelta > MIN_DELTA_FOR_UPDATE;
    }

    private void UpdateSmoothPositions()
    {
        if (!rangeRect) return;

        float deltaTime = Time.unscaledDeltaTime;

        Vector2 newPosition = Vector2.SmoothDamp(
            rangeRect.anchoredPosition,
            targetPosition,
            ref currentVelocity,
            deltaTime * (1f / smoothSpeed),
            Mathf.Infinity,
            deltaTime
        );

        float newWidth = Mathf.SmoothDamp(
            rangeRect.sizeDelta.x,
            targetWidth,
            ref widthVelocity,
            deltaTime * (1f / smoothSpeed),
            Mathf.Infinity,
            deltaTime
        );

        ApplyPositionUpdates(newPosition, newWidth);
        CheckUpdateCompletion();
    }

    private void ApplyPositionUpdates(Vector2 newPosition, float newWidth)
    {
        if (Vector2.Distance(rangeRect.anchoredPosition, newPosition) > MIN_DELTA_FOR_UPDATE)
        {
            rangeRect.anchoredPosition = newPosition;
        }

        if (Mathf.Abs(rangeRect.sizeDelta.x - newWidth) > MIN_DELTA_FOR_UPDATE)
        {
            SetRangeSize(newWidth);
        }
    }

    private void CheckUpdateCompletion()
    {
        if (Vector2.Distance(rangeRect.anchoredPosition, targetPosition) < SMOOTH_UPDATE_THRESHOLD &&
            Mathf.Abs(rangeRect.sizeDelta.x - targetWidth) < SMOOTH_UPDATE_THRESHOLD)
        {
            needsUpdate = false;
            isAnimating = false;
        }
    }
    #endregion

    #region Public Methods
    public void UpdateRangeVisual(float leftX, float rightX)
    {
        if (!rangeRect || !parentPanel) return;

        if (panelRect != parentPanel.rect)
        {
            panelRect = parentPanel.rect;
        }

        ProcessRangeUpdate(leftX, rightX);
    }

    public void ForceUpdatePosition()
    {
        if (!rangeRect) return;

        rangeRect.anchoredPosition = targetPosition;
        SetRangeSize(targetWidth);
        needsUpdate = false;
        isAnimating = false;
    }

    public void SetRangeColor(Color newColor, bool updateGradient = true)
    {
        if (rangeColor == newColor) return;

        rangeColor = newColor;
        if (rangeImage)
        {
            rangeImage.color = rangeColor;
            if (updateGradient && useGradient)
            {
                isDirty = true;
                ApplyGradient();
            }
        }
    }
    #endregion

    #region Private Helper Methods
    private void ProcessRangeUpdate(float leftX, float rightX)
    {
        float localLeftX = leftX - panelRect.x;
        float localRightX = rightX - panelRect.x;

        localLeftX = Mathf.Clamp(localLeftX, 0, panelRect.width);
        localRightX = Mathf.Clamp(localRightX, 0, panelRect.width);

        float newWidth = Mathf.Max(0, localRightX - localLeftX);
        UpdateRangePosition(localLeftX, newWidth);
    }

    private void UpdateRangePosition(float localLeftX, float newWidth)
    {
        float positionDelta = Vector2.Distance(new Vector2(localLeftX, 0), targetPosition);
        float widthDelta = Mathf.Abs(newWidth - targetWidth);

        if (ShouldForceUpdate(positionDelta, widthDelta))
        {
            ApplyForceUpdate(localLeftX, newWidth);
        }
        else if (ShouldSmoothUpdate(positionDelta, widthDelta))
        {
            ApplySmoothUpdate(localLeftX, newWidth);
        }

        UpdatePreviousValues();
    }

    private bool ShouldForceUpdate(float positionDelta, float widthDelta)
    {
        return positionDelta > FORCE_UPDATE_THRESHOLD || widthDelta > FORCE_UPDATE_THRESHOLD;
    }

    private void ApplyForceUpdate(float localLeftX, float newWidth)
    {
        rangeRect.anchoredPosition = new Vector2(localLeftX, 0);
        SetRangeSize(newWidth);
        ResetAnimationState();
    }

    private void ResetAnimationState()
    {
        currentVelocity = Vector2.zero;
        widthVelocity = 0f;
        needsUpdate = false;
        isAnimating = false;
    }

    private bool ShouldSmoothUpdate(float positionDelta, float widthDelta)
    {
        return positionDelta > SMOOTH_UPDATE_THRESHOLD || widthDelta > SMOOTH_UPDATE_THRESHOLD;
    }

    private void ApplySmoothUpdate(float localLeftX, float newWidth)
    {
        targetPosition = new Vector2(localLeftX, 0);
        targetWidth = newWidth;
        needsUpdate = true;
        isAnimating = true;
    }

    private void UpdatePreviousValues()
    {
        previousTargetPosition = targetPosition;
        previousTargetWidth = targetWidth;

        if (useGradient && isDirty)
        {
            ApplyGradient();
        }
    }

    private void SetRangeSize(float width)
    {
        if (rangeRect != null)
        {
            Vector2 newSize = new Vector2(width, -heightOffset * 2);
            if (Vector2.Distance(rangeRect.sizeDelta, newSize) > MIN_DELTA_FOR_UPDATE)
            {
                rangeRect.sizeDelta = newSize;
            }
        }
    }

    private void ApplyGradient()
    {
        if (!rangeImage) return;

        var colorKeys = new GradientColorKey[]
        {
            new GradientColorKey(rangeColor, 0.0f),
            new GradientColorKey(rangeColor * 0.8f, 1.0f)
        };

        var alphaKeys = new GradientAlphaKey[]
        {
            new GradientAlphaKey(rangeColor.a * 0.8f, 0.0f),
            new GradientAlphaKey(rangeColor.a * 0.4f, 1.0f)
        };

        var gradient = new Gradient();
        gradient.SetKeys(colorKeys, alphaKeys);

        rangeImage.color = rangeColor;
        isDirty = false;
    }
    #endregion
}