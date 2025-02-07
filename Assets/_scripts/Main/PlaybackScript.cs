using UnityEngine;
using System;
using System.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using System.Runtime.InteropServices;

/// <summary>
/// Handles direct FMOD event management and audio playback functionality
/// Coordinates with StateManager for state persistence
/// </summary>
public class PlaybackScript : MonoBehaviour
{
    #region Fields
    // FMOD Event handling
    [SerializeField] private FMODUnity.EventReference programmerSoundEvent;  // Reference to the FMOD Programmer Sound Event
    private EventInstance eventInstance;
    private FMOD.Studio.EVENT_CALLBACK eventCallback;
    private bool isEventInitialized;

    // State tracking
    private string assignedFilePath;
    private bool isInTrigger;
    private bool isPlaying;
    private bool isLooping;
    private float savedPlaybackPosition;
    private bool hasRestoredState;

    // References
    private StateManager stateManager;
    private BlinkingEffect blinkEffect;

    // Public read-only state properties
    public bool IsInTrigger => isInTrigger;
    public bool IsPlaying => isPlaying;
    public bool IsFileAssigned
    {
        get => !string.IsNullOrEmpty(assignedFilePath);
    }
    public bool IsEventInitialized => isEventInitialized;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeComponents();
    }

    private void Start()
    {
        RestoreState();
    }

    private void Update()
    {
        if (isEventInitialized && isPlaying)
        {
            UpdateEventPosition();
        }
    }

    private void OnDestroy()
    {
        CleanupFMODEvent();
    }
    #endregion

    #region Initialization
    private void InitializeComponents()
    {
        // Get stateManager reference
        stateManager = StateManager.Instance;
        if (stateManager == null)
        {
            Debug.LogError($"{gameObject.name}: StateManager not found!");
        }

        // Cache blinking effect
        blinkEffect = GetComponent<BlinkingEffect>();

        // Initialize FMOD callback
        eventCallback = new FMOD.Studio.EVENT_CALLBACK(FMODEventCallback);
    }

    private void RestoreState()
    {
        if (stateManager == null) return;

        var state = stateManager.GetSourceState(gameObject);
        if (state != null)
        {
            RestoreFromSavedState(state);
        }
    }

    private void RestoreFromSavedState(StateManager.AudioSourceState state)
    {
        try
        {
            // Restore file assignment
            if (!string.IsNullOrEmpty(state.filePath))
            {
                assignedFilePath = state.filePath;
                InitializeFMODEvent();
            }

            // Restore playback state
            isLooping = state.isLooping;
            savedPlaybackPosition = state.playbackPosition;

            // If we're in trigger and should be playing, resume
            if (isInTrigger && state.isInTriggerBox && state.isPlaying)
            {
                ResumePlayback();
            }

            hasRestoredState = true;
            Debug.Log($"{gameObject.name}: State restored successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error restoring state: {e.Message}");
        }
    }
    #endregion

    #region FMOD Event Management
    /// <summary>
    /// Initializes FMOD event with file path and callbacks
    /// </summary>
    private void InitializeFMODEvent()
    {
        try
        {
            if (programmerSoundEvent.IsNull)
            {
                Debug.LogError($"{gameObject.name}: No FMOD event reference assigned!");
                return;
            }

            // Create new event instance
            eventInstance = RuntimeManager.CreateInstance(programmerSoundEvent);

            if (!eventInstance.isValid())
            {
                Debug.LogError($"{gameObject.name}: Failed to create FMOD event instance");
                return;
            }

            // Set up callbacks
            eventCallback = new FMOD.Studio.EVENT_CALLBACK(FMODEventCallback);
            eventInstance.setCallback(eventCallback);

            // Set up user data for callback
            GCHandle stringHandle = GCHandle.Alloc(assignedFilePath);
            eventInstance.setUserData(GCHandle.ToIntPtr(stringHandle));

            UpdateEventPosition();

            isEventInitialized = true;
            Debug.Log($"{gameObject.name}: FMOD event initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error initializing FMOD event - {e.Message}");
            isEventInitialized = false;
        }

    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    private static FMOD.RESULT FMODEventCallback(
        FMOD.Studio.EVENT_CALLBACK_TYPE type,
        IntPtr instancePtr,
        IntPtr parameterPtr)
    {
        EventInstance instance = new EventInstance(instancePtr);

        // Get the file path from user data
        IntPtr stringPtr;
        instance.getUserData(out stringPtr);
        GCHandle stringHandle = GCHandle.FromIntPtr(stringPtr);
        String filePath = stringHandle.Target as String;

        switch (type)
        {
            case FMOD.Studio.EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
                HandleProgrammerSoundCreation(parameterPtr, filePath);
                break;

            case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND:
                HandleProgrammerSoundDestruction(parameterPtr);
                break;

            case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROYED:
                stringHandle.Free();
                break;
        }

        return FMOD.RESULT.OK;
    }

    private static void HandleProgrammerSoundCreation(IntPtr parameterPtr, string filePath)
    {
        var parameter = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)
            Marshal.PtrToStructure(parameterPtr,
                typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));

        if (string.IsNullOrEmpty(filePath)) return;

        FMOD.MODE soundMode = FMOD.MODE.LOOP_NORMAL |
                             FMOD.MODE.CREATECOMPRESSEDSAMPLE |
                             FMOD.MODE.NONBLOCKING;

        FMOD.Sound sound;
        var result = RuntimeManager.CoreSystem.createSound(
            filePath,
            soundMode,
            out sound);

        if (result == FMOD.RESULT.OK)
        {
            parameter.sound = sound.handle;
            parameter.subsoundIndex = -1;
            Marshal.StructureToPtr(parameter, parameterPtr, false);
        }
    }

    private static void HandleProgrammerSoundDestruction(IntPtr parameterPtr)
    {
        var parameter = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)
            Marshal.PtrToStructure(parameterPtr,
                typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));

        var sound = new FMOD.Sound(parameter.sound);
        sound.release();
    }

    /// <summary>
    /// Updates the FMOD event's 3D attributes based on current transform
    /// </summary>
    private void UpdateEventPosition()
    {
        if (!eventInstance.isValid())
            return;

        try
        {
            // Convert GameObject transform to FMOD 3D attributes
            FMOD.ATTRIBUTES_3D attributes = FMODUnity.RuntimeUtils.To3DAttributes(gameObject.transform);
            eventInstance.set3DAttributes(attributes);
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error updating 3D attributes - {e.Message}");
        }
    }
    #endregion

    #region File Assignment
    /// <summary>
    /// Assigns an audio file and initializes FMOD event
    /// </summary>
    public async Task AssignFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError($"{gameObject.name}: Invalid file path");
            return;
        }

        try
        {
            // Cleanup existing event if any
            CleanupFMODEvent();

            // Assign new file
            assignedFilePath = filePath;
            InitializeFMODEvent();

            // Update state
            UpdateState();

            Debug.Log($"{gameObject.name}: File assigned successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error assigning file: {e.Message}");
            assignedFilePath = null;
            isEventInitialized = false;
        }
    }


    #endregion

    #region Playback Control
    /// <summary>
    /// Starts playback of the assigned audio
    /// </summary>
    private void StartPlayback()
    {
        if (!IsFileAssigned || !isEventInitialized || isPlaying)
            return;

        if (string.IsNullOrEmpty(assignedFilePath) || !System.IO.File.Exists(assignedFilePath))
        {
            Debug.LogError($"{gameObject.name}: Cannot start playback, file path is invalid or file does not exist: {assignedFilePath}");
            return;
        }

        try
        {
            // Ensure 3D attributes are set before playing
            UpdateEventPosition();

            eventInstance.start();
            isPlaying = true;
            UpdateState();
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error starting playback - {e.Message}");
        }
    }

    /// <summary>
    /// Stops audio playback
    /// </summary>
    private void StopPlayback()
    {
        if (!isPlaying) return;

        try
        {
            eventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            isPlaying = false;
            UpdateState();
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error stopping playback - {e.Message}");
        }
    }

    /// <summary>
    /// Resumes playback from saved position
    /// </summary>
    public void ResumePlayback()
    {
        if (!IsFileAssigned || !isEventInitialized) return;

        try
        {
            SetPlaybackPosition(savedPlaybackPosition);
            StartPlayback();
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error resuming playback - {e.Message}");
        }
    }

    public float GetPlaybackPosition()
    {
        return savedPlaybackPosition;
    }

    /// <summary>
    /// Sets the playback position
    /// </summary>
    public void SetPlaybackPosition(float position)
    {
        if (!isEventInitialized) return;

        try
        {
            int timelinePosition = (int)(position * GetEventLength());
            eventInstance.setTimelinePosition(timelinePosition);
            savedPlaybackPosition = position;
            UpdateState();
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error setting position - {e.Message}");
        }
    }

    /// <summary>
    /// Sets the spatial position of the audio
    /// </summary>
    private void UpdateSpatialPosition()
    {
        if (!isEventInitialized) return;

        try
        {
            eventInstance.set3DAttributes(RuntimeUtils.To3DAttributes(transform));
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error updating position - {e.Message}");
        }
    }
    #endregion

    #region Trigger Box Interaction
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("TriggerBox")) return;

        isInTrigger = true;

        // Restore state if not already done
        if (stateManager != null && !hasRestoredState)
        {
            var state = stateManager.GetSourceState(gameObject);
            if (state != null)
            {
                RestoreFromSavedState(state);
            }
        }

        // ✅ Always start playback from the beginning on re-enter
        SetPlaybackPosition(0f);
        StartPlayback();

        UpdateState();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("TriggerBox")) return;

        isInTrigger = false;

        // Save state before stopping
        if (isPlaying)
        {
            SavePlaybackPosition();
        }

        StopPlayback();
        UpdateState();
    }
    #endregion

    #region State Management
    private void UpdateState()
    {
        if (stateManager == null) return;

        stateManager.UpdateState(gameObject, state => {
            state.filePath = assignedFilePath;
            state.isPlaying = isPlaying;
            state.isLooping = isLooping;
            state.playbackPosition = savedPlaybackPosition;
            state.isInTriggerBox = isInTrigger;
        });
    }

    /// <summary>
    /// Saves the current state of this audio source
    /// </summary>
    public void SaveCurrentState()
    {
        if (stateManager == null) return;

        try
        {
            // Save current playback position if playing
            if (isPlaying && eventInstance.isValid())
            {
                int position;
                eventInstance.getTimelinePosition(out position);
                savedPlaybackPosition = position / (float)GetEventLength();
            }

            // Update state manager with current state
            stateManager.UpdateState(gameObject, state =>
            {
                state.filePath = assignedFilePath;
                state.isPlaying = isPlaying;
                state.isLooping = isLooping;
                state.playbackPosition = savedPlaybackPosition;
                state.isInTriggerBox = isInTrigger;
                state.timelinePosition = GetCurrentTimelinePosition();
                state.lastPlaybackPosition = savedPlaybackPosition;
            });

            Debug.Log($"{gameObject.name}: Current state saved successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error saving current state: {e.Message}");
        }
    }

    /// <summary>
    /// Gets the current timeline position from FMOD event
    /// </summary>
    private int GetCurrentTimelinePosition()
    {
        if (!eventInstance.isValid()) return 0;

        int position;
        eventInstance.getTimelinePosition(out position);
        return position;
    }

    private void SavePlaybackPosition()
    {
        if (!isEventInitialized) return;

        try
        {
            int position;
            eventInstance.getTimelinePosition(out position);
            savedPlaybackPosition = position / (float)GetEventLength();
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error saving position - {e.Message}");
        }
    }

    private int GetEventLength()
    {
        if (!isEventInitialized) return 0;

        try
        {
            FMOD.Studio.EventDescription description;
            eventInstance.getDescription(out description);
            int length;
            description.getLength(out length);
            return length;
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error getting length - {e.Message}");
            return 0;
        }
    }
    #endregion

    #region Public Methods
    public void SetLoopState(bool state)
    {
        isLooping = state;
        UpdateState();
    }

    public bool GetLoopState() => isLooping;

    public string GetAssignedFilePath() => assignedFilePath;

    public void GetCurrentState(out bool isActive, out float position,
        out bool isLooping, out string filePath)
    {
        isActive = isPlaying;
        position = savedPlaybackPosition;
        isLooping = this.isLooping;
        filePath = assignedFilePath;
    }
    #endregion

    #region Cleanup
    private void CleanupFMODEvent()
    {
        try
        {
            if (isEventInitialized && eventInstance.isValid())
            {
                // Remove callback first
                eventInstance.setCallback(null);

                // Stop and release
                eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                eventInstance.release();
                isEventInitialized = false;

                // Clear reference
                eventInstance.clearHandle();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"{gameObject.name}: Error cleaning up FMOD event - {e.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        // Ensure cleanup happens before domain reload
        CleanupFMODEvent();

        // Remove any remaining GC handles
        if (eventInstance.isValid())
        {
            IntPtr stringPtr;
            eventInstance.getUserData(out stringPtr);
            if (stringPtr != IntPtr.Zero)
            {
                GCHandle stringHandle = GCHandle.FromIntPtr(stringPtr);
                if (stringHandle.IsAllocated)
                {
                    stringHandle.Free();
                }
            }
        }
    }
    #endregion
}