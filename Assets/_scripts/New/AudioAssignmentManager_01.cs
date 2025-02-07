using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using SFB;
using TMPro;
using System.Collections;
using System;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// Manages the assignment of audio files to source buttons and handles related UI interactions.
/// Coordinates with StateManager for maintaining source states and provides visual feedback.
/// </summary>
public class AudioAssignmentManager_01 : MonoBehaviour
{
    #region Inspector References
    [Header("UI References")]
    [SerializeField] private GameObject[] sourceButtons;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private Button loadFileButton; // Reference to the load file button

    [Header("Mobile Settings")]
    [SerializeField] private bool enableMobileAudioPicker = true;

    #endregion

    #region Private Fields
    private StateManager_01 stateManager;
    private WaveformController_01 waveformController;
    private WaveformTimeDisplay timeDisplay;
    private string lastLoadedFilePath = "";
    private Dictionary<GameObject, string> assignedFiles = new Dictionary<GameObject, string>();
    private bool isMobilePlatform;
    #endregion

    #region Unity Lifecycle Methods

    private void Awake()
    {
        // Determine platform
        isMobilePlatform = Application.isMobilePlatform;
        SetupFileLoadButton();
    }

    private void OnEnable()
    {
        // 🔥 Subscribe to the event when the object is enabled
        AudioRecorder.OnRecordingSaved += HandleRecordedFile;
    }



    private void OnDisable()
    {
        // 🛑 Unsubscribe from the event when the object is disabled (prevents memory leaks)
        AudioRecorder.OnRecordingSaved -= HandleRecordedFile;
    }

    public void HandleRecordedFile(string filePath)
    {
        Debug.Log($"[AudioAssignmentManager_01] Handling recorded file: {filePath}");

        // Validate the file exists
        if (!System.IO.File.Exists(filePath))
        {
            UpdateFeedbackText("Recording failed to save. Please try again.");
            return;
        }

        // Process the recorded file exactly like a loaded file
        HandleFileSelectionSuccess(filePath);

        if (string.IsNullOrEmpty(filePath)) return;

        Debug.Log($"[AudioAssignmentManager_01] Processing recorded file: {filePath}");
    
    lastLoadedFilePath = filePath;
    
    // Update feedback and button states
    string fileName = Path.GetFileName(lastLoadedFilePath);
    UpdateFeedbackText($"Recording ready for assignment: {fileName}");
    UpdateButtonVisuals(null);

    // Log the waveform processing pipeline start
    Debug.Log($"[AudioAssignmentManager_01] Starting waveform processing for: {lastLoadedFilePath}");
    }


    private void Start()
    {
        InitializeComponents();
        SetupButtonListeners();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// Sets up the file load button based on platform
    /// </summary>
    private void SetupFileLoadButton()
    {
        if (loadFileButton != null)
        {
            loadFileButton.onClick.RemoveAllListeners();
            loadFileButton.onClick.AddListener(() =>
            {
                if (isMobilePlatform && enableMobileAudioPicker)
                {
                    LoadFileOnMobile();
                }
                else
                {
                    LoadFile();
                }
            });
        }
    }

    #region Initialization Methods
    /// <summary>
    /// Initializes all required components and references
    /// </summary>
    private void InitializeComponents()
    {
        stateManager = StateManager_01.Instance;
        waveformController = FindObjectOfType<WaveformController_01>();
        if (waveformController == null)
        {
            Debug.LogError("[AudioAssignmentManager_01] WaveformController_01 not found in scene!");
        }

        timeDisplay = FindObjectOfType<WaveformTimeDisplay>();
        if (timeDisplay == null)
        {
            Debug.LogError("[AudioAssignmentManager_01] WaveformTimeDisplay not found in scene!");
        }

        // Subscribe to state events
        SubscribeToStateEvents();
    }

    /// <summary>
    /// Sets up click event listeners for source buttons
    /// </summary>
    private void SetupButtonListeners()
    {
        foreach (var button in sourceButtons)
        {
            if (button != null)
            {
                var buttonComponent = button.GetComponent<UnityEngine.UI.Button>();
                if (buttonComponent != null)
                {
                    buttonComponent.onClick.AddListener(() => OnSourceButtonClick(button));
                }
            }
        }
    }
    #endregion

    #region File Management
    /// <summary>
    /// Opens file picker and handles selected audio file
    /// </summary>
    public void LoadFile()
    {
        var paths = StandaloneFileBrowser.OpenFilePanel(
            "Load Audio File",
            "",
            "wav,mp3",
            false
        );

        if (paths.Length == 0)
        {
            HandleFileSelectionFailure();
            return;
        }

        HandleFileSelectionSuccess(paths[0]);
    }

    /// <summary>
    /// Handles file loading on mobile platforms using NativeGallery
    /// </summary>
    private async void LoadFileOnMobile()
    {
        try
        {
            // Request permission for audio files
            NativeGallery.Permission permission = NativeGallery.RequestPermission(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Audio);

            if (permission == NativeGallery.Permission.Granted)
            {
                NativeGallery.GetAudioFromGallery((path) =>
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            HandleFileSelectionSuccess(path);
                        });
                    }
                    else
                    {
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            HandleFileSelectionFailure();
                        });
                    }
                });
            }
            else
            {
                UpdateFeedbackText("Permission denied to access files. Please grant permission in Settings.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AudioAssignmentManager_01] Mobile file loading error: {e.Message}");
            UpdateFeedbackText("Error loading audio file. Please try again.");
        }
    }

    /// <summary>
    /// Validates if the file extension is a supported audio format
    /// </summary>
    private bool IsValidAudioFileExtension(string extension)
    {
        string[] validExtensions = { ".wav", ".mp3", ".ogg", ".m4a" };
        return Array.Exists(validExtensions, ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Requests storage permission on Android
    /// </summary>
    private async Task<bool> RequestStoragePermission()
    {
#if UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageRead))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.ExternalStorageRead);
            // Wait for permission result
            await Task.Delay(100);
            return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageRead);
        }
#endif
        return true;
    }
    private void HandleFileSelectionFailure()
    {
        UpdateFeedbackText("No file selected. Please try again.");
        lastLoadedFilePath = "";
    }

    private void HandleFileSelectionSuccess(string filePath)
    {
        lastLoadedFilePath = filePath;
        string fileName = System.IO.Path.GetFileName(lastLoadedFilePath);
        UpdateFeedbackText($"File loaded successfully: {fileName}");
        UpdateButtonVisuals(null);
    }

    #endregion

    #region Source Assignment
    /// <summary>
    /// Assigns loaded audio file to a source button
    /// </summary>
    public void AssignFileToSource(GameObject sourceButton)
    {
        if (!ValidateFileAssignment(sourceButton)) return;
        
        if (IsFileAlreadyAssigned(lastLoadedFilePath))
        {
            HandleDuplicateAssignment();
            return;
        }

        ProcessFileAssignment(sourceButton);
        UpdateDurationDisplay(sourceButton);
    }

    private bool ValidateFileAssignment(GameObject sourceButton)
    {
        if (sourceButton == null || string.IsNullOrEmpty(lastLoadedFilePath))
        {
            UpdateFeedbackText("File assignment failed. No file loaded or invalid button.");
            return false;
        }
        return true;
    }

    private bool IsFileAlreadyAssigned(string filePath)
    {
        foreach (var button in sourceButtons)
        {
            var state = stateManager.GetSourceState(button);
            if (state?.filePath == filePath && state.isAssigned)
                return true;
        }
        return false;
    }

    private void HandleDuplicateAssignment()
    {
        UpdateFeedbackText($"File already assigned to another source. Load a different file.");
    }

    private void ProcessFileAssignment(GameObject sourceButton)
    {
        try
        {
            Debug.Log($"[AudioAssignmentManager_01] Processing file assignment: {lastLoadedFilePath}");

            // Update state
            stateManager.RegisterSource(sourceButton);
            UpdateSourceState(sourceButton);

            if (!VerifyFileAssignment(sourceButton)) return;

            // Initialize FMOD and get duration
            var playbackScript = sourceButton.GetComponent<PlaybackScript_01>();
            if (playbackScript != null)
            {
                // Set file path before initialization
                playbackScript.SetFilePath(lastLoadedFilePath);
                Debug.Log($"[AudioAssignmentManager_01] Set file path in PlaybackScript: {lastLoadedFilePath}");

                playbackScript.InitializeAudioFile();
            }

            // Update UI
            UpdateUIForAssignment(sourceButton);
            EnableDragging(sourceButton);

            Debug.Log($"[AudioAssignmentManager_01] File successfully assigned to {sourceButton.name}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AudioAssignmentManager_01] Error in ProcessFileAssignment: {e.Message}");
        }
    }
    private System.Collections.IEnumerator UpdateDisplayAfterDelay(GameObject sourceButton)
    {
        // Wait for a frame to ensure FMOD initialization is complete
        yield return null;

        // Update the display
        UpdateDurationDisplay(sourceButton);

        // Log the update
        var state = stateManager.GetSourceState(sourceButton);
        if (state != null)
        {
            Debug.Log($"[AudioAssignmentManager_01] Duration display updated after delay - Total: {state.totalDuration:F2}s");
        }
    }

    /// <summary>
    /// Updates duration display for newly assigned source
    /// </summary>
    private void UpdateDurationDisplay(GameObject sourceButton)
    {
        if (timeDisplay == null)
        {
            Debug.LogError("[AudioAssignmentManager_01] TimeDisplay reference is missing!");
            return;
        }

        var state = stateManager.GetSourceState(sourceButton);
        if (state != null)
        {
            // Log the duration before updating display
            Debug.Log($"[AudioAssignmentManager_01] Updating duration display with total duration: {state.totalDuration:F2}s");

            // Update to include trim points
            timeDisplay.UpdateTimeDisplay(
                state.totalDuration,
                0f,  // Initial position
                state.trimInPosition * state.totalDuration,  // Convert normalized to absolute
                state.trimOutPosition * state.totalDuration  // Convert normalized to absolute
            );
        }
        else
        {
            Debug.LogError($"[AudioAssignmentManager_01] No state found for {sourceButton.name}");
        }
    }

    /// <summary>
    /// Subscribe to StateManager events for position updates
    /// </summary>
    private void SubscribeToStateEvents()
    {
        if (stateManager != null)
        {
            stateManager.OnPositionChanged += HandlePositionChanged;
            stateManager.OnDurationChanged += HandleDurationChanged;
            Debug.Log("[AudioAssignmentManager_01] Successfully subscribed to StateManager events");
        }
        else
        {
            Debug.LogError("[AudioAssignmentManager_01] StateManager is null during event subscription!");
        }
    }
    /// <summary>
    /// Handle position updates from StateManager
    /// </summary>
    private void HandlePositionChanged(string sourceId, float position)
    {
        var state = stateManager.GetSourceState(stateManager.GetGameObjectFromSourceId(sourceId));
        if (state != null && state.isActive && timeDisplay != null)
        {
            timeDisplay.UpdateTimeDisplay(
                state.totalDuration,
                position,
                state.trimInPosition * state.totalDuration,
                state.trimOutPosition * state.totalDuration
            );
        }
    }

    /// <summary>
    /// Handle duration updates from StateManager
    /// </summary>
    private void HandleDurationChanged(string sourceId, float duration)
    {
        Debug.Log($"[AudioAssignmentManager_01] Duration changed event received: {duration:F2}s for source {sourceId}");

        var state = stateManager.GetSourceState(stateManager.GetGameObjectFromSourceId(sourceId));
        if (state != null && timeDisplay != null)
        {
            timeDisplay.UpdateTimeDisplay(
                duration,
                state.playbackPosition,
                state.trimInPosition * duration,
                state.trimOutPosition * duration
            );
        }
    }
    #endregion

    #region State Management
    private void UpdateSourceState(GameObject sourceButton)
    {
        stateManager.UpdateSourceState(sourceButton, state =>
        {
            state.filePath = lastLoadedFilePath;
            state.isAssigned = true;
        });
    }

    private bool VerifyFileAssignment(GameObject sourceButton)
    {
        var storedFile = stateManager.GetSourceState(sourceButton)?.filePath;
        if (string.IsNullOrEmpty(storedFile))
        {
            Debug.LogError($"[AudioAssignmentManager_01] File assignment failed - StateManager did not store the file path.");
            UpdateFeedbackText($"File assignment failed. Please try again.");
            return false;
        }
        return true;
    }
    #endregion

    #region UI Management
    /// <summary>
    /// Handles click events on source buttons
    /// </summary>
    private async void OnSourceButtonClick(GameObject button)
    {
        if (!assignedFiles.ContainsKey(button) && !string.IsNullOrEmpty(lastLoadedFilePath))
        {
            AssignFileToSource(button);
        }

        if (waveformController != null)
        {
            await waveformController.SwitchToSource(button);
            stateManager.SetActiveSource(button);
        }
    }

    /// <summary>
    /// Updates visual state of source buttons
    /// </summary>
    public void UpdateButtonVisuals(GameObject activeButton)
    {
        foreach (var button in sourceButtons)
        {
            if (button == null) continue;

            var blinkingEffect = button.GetComponent<BlinkingEffect>();
            var buttonImage = button.GetComponent<UnityEngine.UI.Image>();
            bool isAssigned = stateManager.GetSourceState(button)?.isAssigned ?? false;

            UpdateButtonState(button, blinkingEffect, buttonImage, isAssigned);
        }
    }

    private void UpdateButtonState(GameObject button, BlinkingEffect blinkingEffect,
        UnityEngine.UI.Image buttonImage, bool isAssigned)
    {
        if (isAssigned)
        {
            blinkingEffect?.StopBlinking();
            if (buttonImage != null) buttonImage.color = Color.green;
        }
        else
        {
            blinkingEffect?.StartBlinking();
            if (buttonImage != null) buttonImage.color = Color.white;
        }
    }

    private void UpdateUIForAssignment(GameObject sourceButton)
    {
        var buttonImage = sourceButton.GetComponent<UnityEngine.UI.Image>();
        if (buttonImage != null)
        {
            buttonImage.color = Color.green;
        }
        UpdateFeedbackText($"File assigned to {sourceButton.name}. Dragging enabled.");
        UpdateButtonVisuals(sourceButton);
    }

    private void EnableDragging(GameObject sourceButton)
    {
        var draggable = sourceButton.GetComponent<DraggableSourceButton>();
        if (draggable != null)
        {
            draggable.isAssigned = true;
            draggable.enabled = true;
        }
    }

    private void UpdateFeedbackText(string message)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            //Debug.Log($"[AudioAssignmentManager_01] {message}");
        }
    }
    #endregion
}
#endregion