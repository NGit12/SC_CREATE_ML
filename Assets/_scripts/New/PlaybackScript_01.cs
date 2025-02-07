using UnityEngine;
using FMOD.Studio;
using FMODUnity;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine.Rendering;

/// <summary>
/// Manages FMOD audio playback, handles state management, and controls spatial audio positioning.
/// Implements FMOD Programmer Instrument for dynamic audio file loading and playback.
/// </summary>
public class PlaybackScript_01 : MonoBehaviour
{
    #region Configuration and Constants
    [Header("FMOD Configuration")]
    [SerializeField] private EventReference programmerSoundEvent;

    // Constants for time conversion
    private const float MILLISECONDS_TO_SECONDS = 0.001f;
    #endregion

    #region Private Fields
    private EventInstance eventInstance;
    private FMOD.Studio.EVENT_CALLBACK eventCallback;
    private bool isEventInitialized;

    // Audio state
    private AudioState audioState;

    // Manager references
    private StateManager_01 stateManager;
    private AudioAssignmentManager_01 audioAssignmentManager;
    private WaveformController_01 waveformController;

    private const string VOLUME_PARAMETER = "Volume";
    private float currentVolume = 1.0f;
    #endregion

    #region Structs and Classes
    /// <summary>
    /// Encapsulates all audio playback state information
    /// </summary>
    private class AudioState
    {
        public string FilePath { get; set; } = "";
        public bool IsInTrigger { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsLooping { get; set; }
        public float PlaybackPosition { get; set; }
        public float TotalDuration { get; set; }
    }
    #endregion

    #region Properties
    /// <summary>
    /// Public properties with controlled access to audio state
    /// </summary>
    public bool IsPlaying => audioState.IsPlaying;
    public bool IsFileAssigned => !string.IsNullOrEmpty(audioState.FilePath);
    public bool IsEventInitialized => isEventInitialized;

    /// <summary>
    /// Gets total duration in seconds
    /// </summary>
    public float GetTotalDuration()
    {
        return audioState.TotalDuration;
    }
    #endregion

    #region Unity Lifecycle Methods
    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        RegisterAndRestoreState();
    }

    private void Update()
    {
        UpdatePlaybackState();
    }

    private void OnDestroy()
    {
        CleanupFMODResources();
    }
    #endregion

    #region Initialization
    /// <summary>
    /// Initializes all required components and states
    /// </summary>
    private void InitializeComponents()
    {
        audioState = new AudioState();
        stateManager = StateManager_01.Instance;
        audioAssignmentManager = FindObjectOfType<AudioAssignmentManager_01>();
        waveformController = FindObjectOfType<WaveformController_01>();
        eventCallback = new EVENT_CALLBACK(FMODEventCallback);
    }

    /// <summary>
    /// Initializes audio file and gets duration when file is first assigned
    /// </summary>
    public void InitializeAudioFile()
    {
        Debug.Log($"[PlaybackScript_01] Checking file assignment - FilePath: {audioState?.FilePath}");

        if (!IsFileAssigned)
        {
            Debug.LogError("[PlaybackScript_01] No file assigned, cannot initialize FMOD event.");
            return;
        }

        try
        {
            // Create Sound directly to get duration
            RuntimeManager.CoreSystem.createSound(
                audioState.FilePath,
                FMOD.MODE.DEFAULT,
                out FMOD.Sound sound
            );

            // Get and set duration
            sound.getLength(out uint length, FMOD.TIMEUNIT.MS);
            float durationInSeconds = length / 1000.0f;

            // Update duration in state manager
            stateManager.UpdateDuration(gameObject, durationInSeconds);
            Debug.Log($"[PlaybackScript_01] Audio file initialized with duration: {durationInSeconds:F2}s");

            // Release the temporary sound
            sound.release();

            // Create the actual event instance
            CreateEventInstance();
            ConfigureEventInstance();
            isEventInitialized = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlaybackScript_01] Error initializing audio file: {e.Message}");
            isEventInitialized = false;
        }
    }

    /// <summary>
    /// Sets the audio file path
    /// </summary>
    public void SetFilePath(string path)
    {
        audioState.FilePath = path;
        Debug.Log($"[PlaybackScript_01] File path set to: {path}");
    }

    /// <summary>
    /// Registers with StateManager and restores previous state
    /// </summary>
    private void RegisterAndRestoreState()
    {
        stateManager.RegisterSource(gameObject);
        RestoreState();
    }

    /// <summary>
    /// Restores previous state from StateManager
    /// </summary>
    private void RestoreState()
    {
        var state = stateManager.GetSourceState(gameObject);
        if (state != null)
        {
            audioState.FilePath = state.filePath;
            audioState.IsPlaying = state.isPlaying;
            audioState.PlaybackPosition = state.playbackPosition;

            if (IsFileAssigned)
            {
                InitializeFMODEvent();
            }
        }
    }

    /// <summary>
    /// Initializes FMOD event if requirements are met
    /// </summary>
    private void InitializeFMODEvent()
    {
        if (!ValidateFMODRequirements()) return;

        try
        {
            // Call InitializeAudioFile instead of duplicating initialization
            InitializeAudioFile();
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlaybackScript_01] FMOD initialization failed: {e.Message}");
        }
    }

    /// <summary>
    /// Validates FMOD initialization requirements
    /// </summary>
    private bool ValidateFMODRequirements()
    {
        Debug.Log($"[PlaybackScript_01] Validating - IsFileAssigned: {IsFileAssigned}, FilePath: {audioState?.FilePath}");

        if (!IsFileAssigned)
        {
            Debug.LogError("[PlaybackScript_01] No file assigned, cannot initialize FMOD event.");
            return false;
        }

        if (programmerSoundEvent.IsNull)
        {
            Debug.LogError("[PlaybackScript_01] FMOD event reference missing!");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates and configures the FMOD event instance
    /// </summary>
    private void CreateEventInstance()
    {
        if (programmerSoundEvent.IsNull)
        {
            throw new Exception("FMOD event reference is missing!");
        }

        eventInstance = RuntimeManager.CreateInstance(programmerSoundEvent);
        eventInstance.setCallback(eventCallback);

        // Set initial user data
        GCHandle stringHandle = GCHandle.Alloc(audioState.FilePath, GCHandleType.Pinned);
        eventInstance.setUserData(GCHandle.ToIntPtr(stringHandle));
    }

    /// <summary>
    /// Configures the event instance with necessary parameters
    /// </summary>
    private void ConfigureEventInstance()
    {
        if (!eventInstance.isValid()) return;

        UpdateFmodEventPosition();  // Then update position
    }
    #endregion

    #region FMOD Callback Handling
    [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
    private static FMOD.RESULT FMODEventCallback(EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
    {
        EventInstance instance = new EventInstance(instancePtr);
        instance.getUserData(out IntPtr stringPtr);
        GCHandle stringHandle = GCHandle.FromIntPtr(stringPtr);
        string filePath = stringHandle.Target as string;

        switch (type)
        {
            case EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
                HandleProgrammerSoundCreation(parameterPtr, filePath);
                break;

            case EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND:
                HandleProgrammerSoundDestruction(parameterPtr);
                break;

            case EVENT_CALLBACK_TYPE.DESTROYED:
                stringHandle.Free();
                break;
        }

        return FMOD.RESULT.OK;
    }

    private static void HandleProgrammerSoundCreation(IntPtr parameterPtr, string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        var parameter = (PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(
            parameterPtr, typeof(PROGRAMMER_SOUND_PROPERTIES));

        RuntimeManager.CoreSystem.createSound(
            filePath,
            FMOD.MODE.DEFAULT,
            out FMOD.Sound sound
        );

        // Get actual sound duration
        sound.getLength(out uint length, FMOD.TIMEUNIT.MS);
        float durationInSeconds = length / 1000.0f;
        //Debug.Log($"[PlaybackScript_01] Sound length: {durationInSeconds:F2}s");


        parameter.sound = sound.handle;
        Marshal.StructureToPtr(parameter, parameterPtr, false);
    }

    private static void HandleProgrammerSoundDestruction(IntPtr parameterPtr)
    {
        var parameter = (PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(
            parameterPtr, typeof(PROGRAMMER_SOUND_PROPERTIES));

        var sound = new FMOD.Sound(parameter.sound);
        sound.release();
    }
    #endregion

    #region Playback Control
    /// <summary>
    /// Starts audio playback if all requirements are met
    /// </summary>
    public async Task StartPlayback()
    {
        if (!PrepareForPlayback()) return;

        if (!audioState.IsPlaying)
        {
            // Get trim position from state
            var state = stateManager.GetSourceState(gameObject);
            if (state != null)
            {
                var (trimStart, _) = state.GetEffectivePlaybackRange();
                // Set initial position to trim in point
                eventInstance.setTimelinePosition((int)(trimStart * 1000)); // Convert to milliseconds
            }

            await InitiatePlayback();
        }
    }

    /// <summary>
    /// Prepares the system for playback
    /// </summary>
    private bool PrepareForPlayback()
    {
        var state = stateManager.GetSourceState(gameObject);
        if (state != null && !IsFileAssigned)
        {
            audioState.FilePath = state.filePath;
        }

        if (!IsFileAssigned) return false;
        if (!isEventInitialized) InitializeFMODEvent();

        return true;
    }

    /// <summary>
    /// Initiates the actual playback process
    /// </summary>
    private async Task InitiatePlayback()
    {
        UpdateFmodEventPosition();
        SetFMODParameters();
        eventInstance.start();
        audioState.IsPlaying = true;
        stateManager.UpdatePlaybackState(gameObject, true);
        await waveformController.SwitchToSource(gameObject);
    }

    /// <summary>
    /// Stops audio playback
    /// </summary>
    public void StopPlayback()
    {
        if (audioState.IsPlaying)
        {
            eventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            audioState.IsPlaying = false;
            stateManager.UpdatePlaybackState(gameObject, false);
        }
    }

    /// <summary>
    /// Updates playback state and position
    /// </summary>
    private void UpdatePlaybackState()
    {
        if (!isEventInitialized || !audioState.IsPlaying) return;

        UpdateFmodEventPosition();

        // Get current position from FMOD
        eventInstance.getTimelinePosition(out int position);
        float currentPosition = position * MILLISECONDS_TO_SECONDS;

        // Apply volume fade
        ApplyFadeVolume(currentPosition);

        // Get trim bounds and loop state from state manager
        var state = stateManager.GetSourceState(gameObject);
        if (state != null)
        {
            // Get trim bounds
            var (trimStart, trimEnd) = state.GetEffectivePlaybackRange();

            // Check if we're approaching the trim out point
            // Add a small buffer to handle the transition before actually reaching the end
            float bufferTime = 0.05f; // 50ms buffer
            if (currentPosition >= (trimEnd - bufferTime))
            {
                if (state.isLooping)
                {
                    // Smoothly transition to trim start
                    // Stop the current playback
                    eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);

                    // Set position to trim start
                    int timelinePosition = (int)(trimStart * 1000); // Convert to milliseconds
                    eventInstance.setTimelinePosition(timelinePosition);

                    // Restart playback
                    eventInstance.start();

                    // Update our state
                    audioState.PlaybackPosition = trimStart;
                    stateManager.UpdatePosition(gameObject, trimStart);

                    Debug.Log($"[PlaybackScript_01] Loop transition at: {currentPosition:F2}s to: {trimStart:F2}s");
                }
                else
                {
                    // If not looping, stop at trim out
                    if (currentPosition >= trimEnd)
                    {
                        StopPlayback();
                    }
                }
                return;
            }

            // Normal position update
            audioState.PlaybackPosition = currentPosition;
            stateManager.UpdatePosition(gameObject, currentPosition);


        }
    }
    #endregion

    #region Fade Controls

    /// <summary>
    /// Applies volume fade based on current playback position
    /// </summary>
    private void ApplyFadeVolume(float currentPosition)
    {
        if (!eventInstance.isValid()) return;

        var state = stateManager.GetSourceState(gameObject);
        if (state == null) return;

        float volume = CalculateFadeVolume(currentPosition, state);

        // Apply volume to FMOD event
        eventInstance.setParameterByName(VOLUME_PARAMETER, volume);
        currentVolume = volume;
    }

    /// <summary>
    /// Calculates fade volume based on current position and fade settings
    /// </summary>
    private float CalculateFadeVolume(float currentPosition, StateManager_01.AudioSourceState state)
    {
        // Get absolute time positions
        float fadeInStart = state.trimInPosition * state.totalDuration;
        float fadeInEnd = fadeInStart + state.fadeInDuration;

        float fadeOutEnd = state.trimOutPosition * state.totalDuration;
        float fadeOutStart = fadeOutEnd - state.fadeOutDuration;

        // Handle fade in
        if (currentPosition <= fadeInEnd)
        {
            return Mathf.Lerp(0f, 1f, (currentPosition - fadeInStart) / state.fadeInDuration);
        }
        // Handle fade out
        else if (currentPosition >= fadeOutStart)
        {
            return Mathf.Lerp(1f, 0f, (currentPosition - fadeOutStart) / state.fadeOutDuration);
        }

        return 1.0f; // Full volume outside fade regions
    }

    #endregion

    #region Position and State Management
    /// <summary>
    /// Gets current playback position
    /// </summary>
    public float GetPlaybackPosition() => audioState.PlaybackPosition;

    /// <summary>
    /// Sets playback position and updates state
    /// </summary>
    public void SetPlaybackPosition(float position)
    {
        if (!isEventInitialized || !eventInstance.isValid()) return;

        // Convert position from seconds to milliseconds for FMOD
        int timelinePosition = (int)(position * 1000);

        // Set FMOD position directly
        eventInstance.setTimelinePosition(timelinePosition);

        // Update our state
        audioState.PlaybackPosition = position;
        stateManager.UpdatePosition(gameObject, position);
    }

    /// <summary>
    /// Updates FMOD event position based on GameObject transform
    /// </summary>
    private void UpdateFmodEventPosition()
    {
        if (!eventInstance.isValid()) return;

        try
        {
            var attributes = RuntimeUtils.To3DAttributes(gameObject.transform);
            eventInstance.set3DAttributes(attributes);
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlaybackScript_01] Error updating 3D attributes: {e.Message}");
        }
    }

    /// <summary>
    /// Sets up FMOD parameters for playback
    /// </summary>
    private void SetFMODParameters()
    {
        if (!eventInstance.isValid()) return;

        GCHandle stringHandle = GCHandle.Alloc(audioState.FilePath, GCHandleType.Pinned);
        eventInstance.setUserData(GCHandle.ToIntPtr(stringHandle));
    }
    #endregion

    #region Trigger Interactions
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("TriggerBox")) return;

        // Update trigger state
        audioState.IsInTrigger = true;
        stateManager.SetInsideTrigger(gameObject, true);

        // Start playback if initialized
        if (isEventInitialized)
        {
            StartPlayback();
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("TriggerBox")) return;

        // Update trigger state first
        audioState.IsInTrigger = false;
        stateManager.SetInsideTrigger(gameObject, false);

        // Then stop playback
        StopPlayback();
    }
    #endregion

    #region Cleanup
    /// <summary>
    /// Cleans up FMOD resources when the script is destroyed
    /// </summary>
    private void CleanupFMODResources()
    {
        if (eventInstance.isValid())
        {
            eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            eventInstance.release();
        }
    }
    #endregion
}