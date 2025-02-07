using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading.Tasks;
using FMODUnity;
using UnityEngine.EventSystems;
using UnityEngine.UI.Extensions;
using System.Collections.Generic;
using static TrimFadeTypes;
using System.Xml.Linq;

/// <summary>
/// Manages waveform visualization, source switching, and UI updates.
/// Handles waveform generation, playhead control, and user interactions.
/// </summary>
public class WaveformController_01 : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    #region Serialized Fields
    [Header("UI References")]
    [SerializeField] private RectTransform waveformPanel;
    [SerializeField] private RectTransform playhead;

    [Header("Component References")]
    [SerializeField] private WaveformGenerator waveformGenerator;

    [Header("Trim/Fade References")]
    [SerializeField] private TrimFadeHandler_Left_01 leftTrimHandler;
    [SerializeField] private TrimFadeHandler_Right_01 rightTrimHandler;

    [Header("Loop Control")]
    [SerializeField] private Button loopButton;  // Reference to UI loop button
    [SerializeField] private Image loopButtonImage;  // Reference to loop button's image
    [SerializeField] private Color loopActiveColor = Color.green;
    [SerializeField] private Color loopInactiveColor = Color.white;

    [Header("Loop Visualization")]
    [SerializeField] private Image loopRegionOverlay;  // Reference to a UI Image for loop region
    [SerializeField] private Color loopRegionColor = new Color(0, 1, 0, 0.2f);  // Semi-transparent green


    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    #endregion

    #region Private Fields
    // Component references
    private WaveformTimeDisplay timeDisplay;
    private StateManager_01 stateManager;
    private PlaybackScript_01 currentPlaybackScript;

    // State tracking
    private string currentSourceId;
    private bool isDragging;
    private bool isScrubbing;
    private float scrubPosition;
    private bool isPlaybackActive;
    public static string CurrentActiveSourceId { get; private set; }

    // Waveform channels
    private RectTransform waveformChannel1;
    private RectTransform waveformChannel2;
    private List<GameObject> pendingDestruction = new List<GameObject>();
    #endregion

    #region Unity Lifecycle Methods
    private void Start()
    {
        InitializeComponents();
        InitializeTrimFadeHandlers();
    }

    private void Update()
    {
        if (!isDragging)
        {
            UpdatePlayheadPosition();
        }

        // Handle any pending GameObject destruction
        if (pendingDestruction.Count > 0)
        {
            foreach (var obj in pendingDestruction)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            pendingDestruction.Clear();
        }
    }

    private void OnDestroy()
    {
        // Clean up any remaining GameObjects
        CleanupWaveformChannels();

        if (leftTrimHandler != null)
        {
            leftTrimHandler.OnPositionsChanged -= HandleLeftTrimFadeChanged;
            leftTrimHandler.OnPositionsChanged -= UpdateLoopRegionVisual;
        }
        if (rightTrimHandler != null)
        {
            rightTrimHandler.OnPositionsChanged -= HandleRightTrimFadeChanged;
            rightTrimHandler.OnPositionsChanged -= UpdateLoopRegionVisual;
        }
        if (loopButton != null)
        {
            loopButton.onClick.RemoveListener(OnLoopButtonClicked);
        }

        if (stateManager != null)
        {
            stateManager.OnLoopStateChanged -= HandleLoopStateChanged;
        }

    }
    #endregion

    #region Initialization Methods
    /// <summary>
    /// Initializes all required components and references
    /// </summary>
    private void InitializeComponents()
    {
        // Initialize manager references
        stateManager = StateManager_01.Instance;
        ValidateWaveformGenerator();
        InitializeTimeDisplay();
        InitializePlayhead();

        // Initialize loop button
        if (loopButton != null)
        {
            loopButton.onClick.AddListener(OnLoopButtonClicked);
        }

        // Subscribe to loop state changes
        stateManager.OnLoopStateChanged += HandleLoopStateChanged;

        Debug.Log("[WaveformController_01] Component initialization complete.");
    }

    /// <summary>
    /// Validates and logs WaveformGenerator status
    /// </summary>
    private void ValidateWaveformGenerator()
    {
        if (waveformGenerator == null)
        {
            waveformGenerator = GetComponent<WaveformGenerator>();
            if (waveformGenerator == null)
            {
                Debug.LogError("[WaveformController_01] WaveformGenerator not found in scene!");
            }
        }
    }

    /// <summary>
    /// Initializes time display component
    /// </summary>
    private void InitializeTimeDisplay()
    {
        timeDisplay = FindObjectOfType<WaveformTimeDisplay>();
        if (timeDisplay == null)
        {
            Debug.LogError("[WaveformController_01] WaveformTimeDisplay not found in scene!");
        }
    }

    /// <summary>
    /// Initializes playhead position and settings
    /// </summary>
    private void InitializePlayhead()
    {
        if (playhead != null)
        {
            SetPlayheadPosition(0f);
            playhead.SetAsLastSibling();
        }
        else
        {
            Debug.LogError("[WaveformController_01] Playhead RectTransform not assigned!");
        }
    }

    private void InitializeTrimFadeHandlers()
    {
        // Existing trim handler initialization
        if (leftTrimHandler != null)
        {
            leftTrimHandler.OnPositionsChanged += HandleLeftTrimFadeChanged;
            // Add loop region update
            leftTrimHandler.OnPositionsChanged += UpdateLoopRegionVisual;
        }
        else
        {
            Debug.LogError("[WaveformController_01] Left trim handler reference missing!");
        }

        if (rightTrimHandler != null)
        {
            rightTrimHandler.OnPositionsChanged += HandleRightTrimFadeChanged;
            // Add loop region update
            rightTrimHandler.OnPositionsChanged += UpdateLoopRegionVisual;
        }
        else
        {
            Debug.LogError("[WaveformController_01] Right trim handler reference missing!");
        }

        // Initialize loop region overlay
        InitializeLoopRegion();
    }

    #endregion

    #region Trim Fade Handlers

    // Add these methods for handling trim/fade changes
    private void HandleLeftTrimFadeChanged(TrimFadePoints points)
    {
        if (string.IsNullOrEmpty(currentSourceId)) return;

        GameObject sourceObject = stateManager.GetGameObjectFromSourceId(currentSourceId);
        if (sourceObject == null) return;

        // Points are already normalized (0-1) from the handler
        stateManager.UpdateSourceState(sourceObject, state =>
        {
            state.trimInPosition = points.TrimPosition.x;
            state.fadeInPoint = points.FadePosition;

            // Calculate fade in duration
            state.fadeInDuration = Mathf.Abs(points.FadePosition.x - state.trimInPosition) * state.totalDuration;
            Debug.Log($"[WaveformController_01] Fade In Duration: {state.fadeInDuration:F2}s");
        });

        // Immediately update loop region
        UpdateLoopRegionVisual(points);
    }

    private void HandleRightTrimFadeChanged(TrimFadePoints points)
    {
        if (string.IsNullOrEmpty(currentSourceId)) return;

        GameObject sourceObject = stateManager.GetGameObjectFromSourceId(currentSourceId);
        if (sourceObject == null) return;

        // Points are already normalized (0-1) from the handler
        stateManager.UpdateSourceState(sourceObject, state =>
        {
            state.trimOutPosition = points.TrimPosition.x;
            state.fadeOutPoint = points.FadePosition;

            // Calculate fade out duration
            state.fadeOutDuration = Mathf.Abs(points.FadePosition.x - state.trimOutPosition) * state.totalDuration;
            Debug.Log($"[WaveformController_01] Fade Out Duration: {state.fadeOutDuration:F2}s");
        });

        // Immediately update loop region
        UpdateLoopRegionVisual(points);
    }

    private void RestoreTrimFadePositions(GameObject sourceObject)
    {
        if (sourceObject == null)
        {
            Debug.LogError("[WaveformController_01] Cannot restore positions: Source object is null");
            return;
        }

        var state = stateManager.GetSourceState(sourceObject);
        if (state == null)
        {
            Debug.LogError($"[WaveformController_01] No state found for source: {sourceObject.name}");
            return;
        }

        try
        {
            // Log current state before restoration
            Debug.Log($"[WaveformController_01] Restoring positions for {sourceObject.name}:" +
                      $"\nTrim In: {state.trimInPosition:F3}" +
                      $"\nTrim Out: {state.trimOutPosition:F3}");

            // Restore left handler positions
            if (leftTrimHandler != null)
            {
                Vector2 leftTrimPos = new Vector2(state.trimInPosition, 0);
                Vector2 leftFadePos = state.fadeInPoint;

                leftTrimHandler.SetPositions(leftTrimPos, leftFadePos);
            }
            else
            {
                Debug.LogWarning("[WaveformController_01] Left trim handler is null");
            }

            // Restore right handler positions
            if (rightTrimHandler != null)
            {
                Vector2 rightTrimPos = new Vector2(state.trimOutPosition, 0);
                Vector2 rightFadePos = state.fadeOutPoint;

                rightTrimHandler.SetPositions(rightTrimPos, rightFadePos);
            }
            else
            {
                Debug.LogWarning("[WaveformController_01] Right trim handler is null");
            }

            // Verify restoration
            VerifyHandlerPositions(state);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WaveformController_01] Error restoring positions: {e.Message}\n{e.StackTrace}");
        }
    }
        

    /// <summary>
    /// Verifies that handler positions match the state after restoration
    /// </summary>
    /// <param name="state">The source state to verify against</param>
    private void VerifyHandlerPositions(StateManager_01.AudioSourceState state)
    {
        if (leftTrimHandler != null)
        {
            var (normalizedTrim, normalizedFade) = leftTrimHandler.GetNormalizedPositions();
            Debug.Log($"[WaveformController_01] Left handler positions -" +
                     $"\nTrim: Expected={state.trimInPosition:F3}, Actual={normalizedTrim.x:F3}" +
                     $"\nFade: Expected={state.fadeInPoint.x:F3}, Actual={normalizedFade.x:F3}");
        }

        if (rightTrimHandler != null)
        {
            var (normalizedTrim, normalizedFade) = rightTrimHandler.GetNormalizedPositions();
            Debug.Log($"[WaveformController_01] Right handler positions -" +
                     $"\nTrim: Expected={state.trimOutPosition:F3}, Actual={normalizedTrim.x:F3}" +
                     $"\nFade: Expected={state.fadeOutPoint.x:F3}, Actual={normalizedFade.x:F3}");
        }
    }

    #endregion

    #region Waveform Channel Management
    /// <summary>
    /// Creates a new waveform channel with the specified name
    /// </summary>
    private RectTransform CreateWaveformChannel(string name)
    {
        GameObject channelObject = new GameObject(name, typeof(RawImage));
        channelObject.transform.SetParent(waveformPanel, false);

        RectTransform rectTransform = channelObject.GetComponent<RectTransform>();
        ConfigureChannelTransform(rectTransform);

        return rectTransform;
    }

    /// <summary>
    /// Configures the transform properties of a waveform channel
    /// </summary>
    private void ConfigureChannelTransform(RectTransform rectTransform)
    {
        rectTransform.SetSiblingIndex(0);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
    }

    /// <summary>
    /// Positions a waveform channel within the panel
    /// </summary>
    private void PositionChannel(RectTransform channel, float anchorMinY, float anchorMaxY)
    {
        channel.anchorMin = new Vector2(0f, anchorMinY);
        channel.anchorMax = new Vector2(1f, anchorMaxY);
        channel.sizeDelta = Vector2.zero;
        channel.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Safely cleans up waveform channel GameObjects
    /// </summary>
    private void CleanupWaveformChannels()
    {
        if (playhead == null || waveformPanel == null) return;

        try
        {
            // Cache playhead information
            Transform playheadParent = playhead.parent;
            int playheadSiblingIndex = playhead.GetSiblingIndex();

            // Collect children for destruction
            foreach (Transform child in waveformPanel.transform)
            {
                if (child != null && child != playhead && child.GetComponent<RawImage>() != null)
                {
                    pendingDestruction.Add(child.gameObject);
                }
            }

            // Reset references
            waveformChannel1 = null;
            waveformChannel2 = null;

            // Restore playhead
            if (playhead != null && playheadParent != null)
            {
                playhead.SetParent(playheadParent, false);
                playhead.SetSiblingIndex(playheadSiblingIndex);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[WaveformController_01] Error during cleanup: {e.Message}");
        }
    }
    #endregion

    #region Waveform Display Methods
    /// <summary>
    /// Updates the waveform display with new textures
    /// </summary>
    private void UpdateWaveformDisplay(Texture2D[] waveformTextures)
    {
        if (!ValidateWaveformTextures(waveformTextures)) return;

        int playheadIndex = playhead.GetSiblingIndex();
        CleanupWaveformChannels();

        for (int i = 0; i < waveformTextures.Length; i++)
        {
            if (waveformTextures[i] == null) continue;

            CreateAndConfigureWaveformChannel(waveformTextures[i], i, playheadIndex, waveformTextures.Length);
        }

        if (leftTrimHandler != null)
        {
            leftTrimHandler.transform.SetAsLastSibling();
        }
        if (rightTrimHandler != null)
        {
            rightTrimHandler.transform.SetAsLastSibling();
        }
        if (playhead != null)
        {
            playhead.SetAsLastSibling();
        }
    }

    /// <summary>
    /// Validates waveform texture array
    /// </summary>
    private bool ValidateWaveformTextures(Texture2D[] waveformTextures)
    {
        if (waveformTextures == null || waveformTextures.Length == 0)
        {
            Debug.LogError("[WaveformController_01] No waveform textures provided.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Creates and configures a single waveform channel
    /// </summary>
    private void CreateAndConfigureWaveformChannel(Texture2D texture, int channelIndex, int playheadIndex, int totalChannels)
    {
        RectTransform channel = CreateWaveformChannel($"WaveformChannel{channelIndex + 1}");
        channel.GetComponent<RawImage>().texture = texture;
        channel.SetSiblingIndex(playheadIndex);

        if (totalChannels == 1)
        {
            PositionChannel(channel, 0f, 1f);
        }
        else if (totalChannels == 2)
        {
            PositionChannel(channel, channelIndex == 0 ? 0.5f : 0f, channelIndex == 0 ? 1f : 0.5f);
        }
    }
    #endregion

    #region Public Interface
    /// <summary>
    /// Switches the waveform display to show the selected source
    /// </summary>
    public async Task SwitchToSource(GameObject sourceButton)
    {
        if (sourceButton == null) return;

        try
        {
            await HandleSourceSwitch(sourceButton);
        }
        catch (Exception e)
        {
            Debug.LogError($"[WaveformController_01] Error switching source: {e.Message}");
        }
    }

    /// <summary>
    /// Handles the source switching process
    /// </summary>
    private async Task HandleSourceSwitch(GameObject sourceButton)
    {
        SetupNewSource(sourceButton);
        var sourceState = stateManager.GetSourceState(sourceButton);

        if (sourceState == null) return;

        UpdateTimeDisplay(sourceState);

        string filePath = sourceState.filePath;
        if (ValidateSourceSwitch(sourceButton, filePath))
        {
            await ProcessSourceSwitch(sourceButton, sourceState);
        }
    }
    #endregion

    #region Playhead Control
    /// <summary>
    /// Updates the playhead position based on current playback state
    /// </summary>
    private void UpdatePlayheadPosition()
    {
        if (!ValidatePlaybackComponents()) return;

        var currentState = stateManager.GetSourceState(currentPlaybackScript.gameObject);
        if (currentState == null) return;

        float totalDuration = currentState.totalDuration;
        if (totalDuration <= 0) return;

        float currentPosition = currentState.playbackPosition;
        float normalizedPosition = Mathf.Clamp01(currentPosition / totalDuration);
        SetPlayheadPosition(normalizedPosition);
    }

    /// <summary>
    /// Validates required components for playback
    /// </summary>
    private bool ValidatePlaybackComponents()
    {
        return playhead != null && currentPlaybackScript != null;
    }

    /// <summary>
    /// Sets the playhead's position using a normalized value
    /// </summary>
    /// <param name="normalizedPosition">Position value between 0 and 1</param>
    private void SetPlayheadPosition(float normalizedPosition)
    {
        if (playhead == null || waveformPanel == null) return;

        float panelWidth = waveformPanel.rect.width;
        float xPosition = normalizedPosition * panelWidth;
        playhead.anchoredPosition = new Vector2(xPosition, playhead.anchoredPosition.y);
    }
    #endregion

    #region Loop Control
    /// <summary>
    /// Handles the loop button click event
    /// </summary>
    public void OnLoopButtonClicked()
    {
        if (string.IsNullOrEmpty(currentSourceId)) return;

        var sourceObj = stateManager.GetGameObjectFromSourceId(currentSourceId);
        if (sourceObj != null)
        {
            // Toggle loop state in StateManager
            stateManager.ToggleLoopState(sourceObj);

            // Get updated state
            var state = stateManager.GetSourceState(sourceObj);
            if (state != null)
            {
                // Update button visual
                UpdateLoopButtonVisual(state.isLooping);
                // Update region overlay
                loopRegionOverlay.enabled = state.isLooping;
                if (state.isLooping)
                {
                    UpdateLoopRegionVisual(new TrimFadePoints());
                }
            }
        }
    }
    private void UpdateLoopButtonVisual(bool isLooping)
    {
        if (loopButtonImage != null)
        {
            loopButtonImage.color = isLooping ? loopActiveColor : loopInactiveColor;
        }
    }


    /// <summary>
    /// Updates loop button visual state
    /// </summary>
    private void UpdateLoopButtonState(bool isLooping)
    {
        if (loopButtonImage != null)
        {
            loopButtonImage.color = isLooping ? loopActiveColor : loopInactiveColor;
        }
    }

    /// <summary>
    /// Handle loop state changes from StateManager
    /// </summary>
    private void HandleLoopStateChanged(string sourceId, bool isLooping)
    {
        if (sourceId == currentSourceId)
        {
            UpdateLoopButtonState(isLooping);
        }
    }

    private void InitializeLoopRegion()
    {
        if (loopRegionOverlay == null)
        {
            GameObject overlayObj = new GameObject("LoopRegionOverlay");
            overlayObj.transform.SetParent(waveformPanel, false);
            loopRegionOverlay = overlayObj.AddComponent<Image>();
            loopRegionOverlay.color = loopRegionColor;
            loopRegionOverlay.transform.SetSiblingIndex(1); // After waveform, before handlers

            RectTransform rectTransform = loopRegionOverlay.rectTransform;
            rectTransform.anchorMin = new Vector2(0, 0);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.sizeDelta = Vector2.zero;

            // Initially disabled
            loopRegionOverlay.enabled = false;
        }
    }

    private void UpdateLoopRegionVisual(TrimFadePoints points)
    {
        if (loopRegionOverlay == null) return;

        var state = stateManager.GetSourceState(stateManager.GetGameObjectFromSourceId(currentSourceId));
        if (state == null || !state.isLooping)
        {
            loopRegionOverlay.enabled = false;
            return;
        }

        loopRegionOverlay.enabled = true;

        // Get current positions from handlers directly
        var leftPosition = leftTrimHandler.GetNormalizedPositions().trim.x;
        var rightPosition = rightTrimHandler.GetNormalizedPositions().trim.x;

        // Update overlay immediately
        RectTransform rectTransform = loopRegionOverlay.rectTransform;
        rectTransform.anchorMin = new Vector2(leftPosition, 0);
        rectTransform.anchorMax = new Vector2(rightPosition, 1);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
    #endregion

    #region Waveform Loading and Generation
    /// <summary>
    /// Loads and generates waveform for the specified audio file
    /// </summary>
    private async Task LoadWaveform(string filePath)
    {
        if (!ValidateFilePath(filePath)) return;

        try
        {
            var channelCount = await GetAudioChannelCount(filePath);
            await GenerateWaveformTextures(filePath, channelCount);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WaveformController_01] Error loading waveform: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates the provided file path
    /// </summary>
    private bool ValidateFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
        {
            Debug.LogError($"[WaveformController_01] Invalid file path or file does not exist: {filePath}");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Gets the number of audio channels in the file
    /// </summary>
    private async Task<int> GetAudioChannelCount(string filePath)
    {
        FMOD.Sound sound;
        RuntimeManager.CoreSystem.createSound(filePath, FMOD.MODE.DEFAULT, out sound);
        sound.getFormat(out _, out _, out int channelCount, out _);
        sound.release();
        return channelCount;
    }

    /// <summary>
    /// Generates waveform textures for all channels
    /// </summary>
    private async Task GenerateWaveformTextures(string filePath, int channelCount)
    {
        if (waveformGenerator == null)
        {
            Debug.LogError("[WaveformController_01] WaveformGenerator reference is missing!");
            return;
        }

        CleanupWaveformChannels();

        Texture2D[] waveformTextures = new Texture2D[channelCount];
        for (int i = 0; i < channelCount; i++)
        {
            float[] pcmData = await WaveformDataReader.ReadWaveformDataAsync(
                filePath,
                waveformGenerator.TextureWidth,
                i
            );

            if (!ValidatePCMData(pcmData, i)) continue;

            waveformTextures[i] = GenerateChannelTexture(pcmData);
        }

        UpdateWaveformDisplay(waveformTextures);
    }

    /// <summary>
    /// Validates PCM data for a channel
    /// </summary>
    private bool ValidatePCMData(float[] pcmData, int channelIndex)
    {
        if (pcmData == null || pcmData.Length == 0)
        {
            Debug.LogError($"[WaveformController_01] PCM data for channel {channelIndex} is null or empty.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Generates a texture for a single channel
    /// </summary>
    private Texture2D GenerateChannelTexture(float[] pcmData)
    {
        if (waveformGenerator == null)
        {
            Debug.LogError("[WaveformController_01] WaveformGenerator reference is missing!");
            return null;
        }

        return waveformGenerator.GenerateWaveformTexture(
            pcmData,     // leftChannelData
            null,        // rightChannelData
            waveformGenerator.TextureWidth,
            waveformGenerator.TextureHeight,
            waveformGenerator.WaveformColor,
            waveformGenerator.ScaleFactor
            //waveformGenerator.CenterLineColor
        );
    }
    #endregion

    #region Source Management
    /// <summary>
    /// Restores the state of a source after switching
    /// </summary>
    /// <param name="state">The audio source state to restore</param>
    private void RestoreSourceState(StateManager_01.AudioSourceState state)
    {
        if (state == null)
        {
            Debug.LogWarning("[WaveformController_01] Attempted to restore null state");
            return;
        }

        try
        {
            // Update current source ID
            currentSourceId = state.sourceId;

            // Log the restoration
            Debug.Log($"[WaveformController_01] Restored state for source: {state.sourceId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[WaveformController_01] Error restoring state: {e.Message}");
        }
    }


    /// <summary>
    /// Sets up the new audio source
    /// </summary>
    private void SetupNewSource(GameObject sourceButton)
    {
        var playbackScript = sourceButton.GetComponent<PlaybackScript_01>();
        currentPlaybackScript = playbackScript;
    }

    /// <summary>
    /// Updates the time display for the current source
    /// </summary>
    private void UpdateTimeDisplay(StateManager_01.AudioSourceState sourceState)
    {
        if (timeDisplay != null)
        {
            timeDisplay.UpdateTimeDisplay(
                sourceState.totalDuration,
                sourceState.playbackPosition,
                sourceState.trimInPosition * sourceState.totalDuration,
                sourceState.trimOutPosition * sourceState.totalDuration
            );
        }
    }

    /// <summary>
    /// Validates the source switch operation
    /// </summary>
    private bool ValidateSourceSwitch(GameObject sourceButton, string filePath)
    {
        var playbackScript = sourceButton.GetComponent<PlaybackScript_01>();
        if (playbackScript == null || string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[WaveformController_01] Invalid source selection");
            return false;
        }

        string sourceId = stateManager.GetSourceId(sourceButton);
        if (sourceId == currentSourceId)
        {
            Debug.Log("[WaveformController_01] Source already active, updating waveform only.");
            return true;
        }

        return true;
    }

    /// <summary>
    /// Processes the source switch operation
    /// </summary>
    private async Task ProcessSourceSwitch(GameObject sourceButton, StateManager_01.AudioSourceState sourceState)
    {
        try
        {
            Debug.Log($"[WaveformController_01] Starting source switch to: {sourceState.sourceId}");

            // Save current source state before switching
            SaveCurrentSourceState();

            // Load waveform for the new source
            await LoadWaveform(sourceState.filePath);

            // Restore source state and update UI
            RestoreSourceState(sourceState);

            // Create local variable for the source button
            GameObject currentSourceButton = sourceButton;

            // Restore the exact trim/fade positions for this source
            RestoreTrimFadePositions(currentSourceButton);
            Debug.Log($"[WaveformController_01] Restored trim/fade positions for source: {sourceState.sourceId}");

            // Set as active source in StateManager
            stateManager.SetActiveSource(currentSourceButton);
            CurrentActiveSourceId = stateManager.GetSourceId(currentSourceButton);
            currentSourceId = sourceState.sourceId;

            // Update loop button state for new source
            UpdateLoopButtonState(sourceState.isLooping);
            loopRegionOverlay.enabled = sourceState.isLooping;
            if (sourceState.isLooping)
            {
                UpdateLoopRegionVisual(new TrimFadePoints());
            }


            Debug.Log($"[WaveformController_01] Completed switch to source: {sourceState.sourceId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[WaveformController_01] Error during source switch: {e.Message}");
        }
    }

    /// <summary>
    /// Saves the state of the current source
    /// </summary>
    private void SaveCurrentSourceState()
    {
        if (string.IsNullOrEmpty(currentSourceId)) return;

        var sourceState = stateManager.GetSourceState(GetGameObjectFromSourceId(currentSourceId));
        if (currentPlaybackScript != null && sourceState != null)
        {
            sourceState.playbackPosition = currentPlaybackScript.GetPlaybackPosition();
        }
    }
    #endregion

    #region User Input Handling
    /// <summary>
    /// Handles initial pointer down event
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsPointerOverPlayhead(eventData))
        {
            isDragging = true;
            HandleScrubbing(eventData);
        }
    }

    /// <summary>
    /// Handles continuous drag event
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging)
        {
            HandleScrubbing(eventData);
        }
    }

    /// <summary>
    /// Handles pointer up event
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }

    /// <summary>
    /// Checks if pointer is over the playhead
    /// </summary>
    private bool IsPointerOverPlayhead(PointerEventData eventData)
    {
        if (playhead == null) return false;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            playhead,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            return playhead.rect.Contains(localPoint);
        }
        return false;
    }

    /// <summary>
    /// Handles scrubbing interaction with pointer input
    /// </summary>
    private void HandleScrubbing(PointerEventData eventData)
    {
        if (!ValidateScrubbing()) return;

        Vector2 localPoint;
        if (!GetLocalPointerPosition(eventData, out localPoint)) return;

        float normalizedPosition = CalculateNormalizedPosition(localPoint);
        UpdatePlaybackPosition(normalizedPosition);
    }

    /// <summary>
    /// Validates components required for scrubbing
    /// </summary>
    private bool ValidateScrubbing()
    {
        return waveformPanel != null && currentPlaybackScript != null;
    }

    /// <summary>
    /// Gets the local pointer position within the waveform panel
    /// </summary>
    private bool GetLocalPointerPosition(PointerEventData eventData, out Vector2 localPoint)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            waveformPanel,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint);
    }

    /// <summary>
    /// Calculates normalized position from local point
    /// </summary>
    private float CalculateNormalizedPosition(Vector2 localPoint)
    {
        float normalizedPosition = (localPoint.x + waveformPanel.rect.width / 2) / waveformPanel.rect.width;
        return Mathf.Clamp01(normalizedPosition);
    }

    /// <summary>
    /// Updates playback position based on normalized position
    /// </summary>
    private void UpdatePlaybackPosition(float normalizedPosition)
    {
        var sourceState = stateManager.GetSourceState(currentPlaybackScript.gameObject);
        if (sourceState != null)
        {
            float newPosition = normalizedPosition * sourceState.totalDuration;
            currentPlaybackScript.SetPlaybackPosition(newPosition);
            SetPlayheadPosition(normalizedPosition);

            Debug.Log($"[WaveformController_01] Scrubbing - Position: {newPosition:F2}s");
        }
    }
    #endregion

    #region Helper Methods
    /// <summary>
    /// Gets GameObject from source ID
    /// </summary>
    private GameObject GetGameObjectFromSourceId(string sourceId)
    {
        return stateManager.GetGameObjectFromSourceId(sourceId);
    }

    /// <summary>
    /// Updates UI elements after source switch
    /// </summary>
    private void UpdateUIElements()
    {
        Debug.Log("[WaveformController_01] UI updated.");
    }
    #endregion
}