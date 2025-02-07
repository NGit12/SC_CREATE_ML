using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using static TrimFadeTypes;

/// <summary>
/// Centralized State Manager for handling all source states, playback status, and UI updates.
/// </summary>
public class StateManager_01 : MonoBehaviour
{
    #region Singleton Pattern
    private static StateManager_01 instance;
    public static StateManager_01 Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("StateManager_01");
                instance = go.AddComponent<StateManager_01>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    #endregion

    #region Debug Settings
    [Header("Debug Settings")]
    [Tooltip("Enable to show state updates in console")]
    [SerializeField] private bool enableStateDebugLogs = false;
    #endregion


    #region Events
    public event Action<string, AudioSourceState> OnSourceStateChanged;
    public event Action<string> OnSourceActivated;
    public event Action<string, bool> OnPlaybackStateChanged;
    public event Action<string, float> OnDurationChanged;     // New event for duration updates
    public event Action<string, float> OnPositionChanged;     // New event for position updates

    public event Action<string, TrimFadePoints> OnTrimPointsChanged;    // Event for trim points updates
    public event Action<string, TrimFadePoints> OnFadePointsChanged;    // Event for fade points updates

    public event Action<string, bool> OnLoopStateChanged;
    #endregion

    #region State Structure
    [System.Serializable]
    public class AudioSourceState
    {
        // Source identification
        public string sourceId;
        public string filePath;

        // Status flags
        public bool isAssigned;
        public bool isPlaying;
        public bool isInsideTrigger;
        public bool isActive;

        // Playback information
        public float playbackPosition;
        public float totalDuration;    // New field for total duration

        // Add loop state
        public bool isLooping = false;  // Default to false

        // New trim and fade fields
        public float trimInPosition;     // Normalized position (0-1) of left trim point
        public float trimOutPosition;    // Normalized position (0-1) of right trim point
        public float fadeInDuration;     // Duration in seconds of fade in
        public float fadeOutDuration;    // Duration in seconds of fade out
        public Vector2 fadeInPoint;      // Position of fade in handle
        public Vector2 fadeOutPoint;     // Position of fade out handle

        // Helper methods INSIDE the class
        public (float start, float end) GetEffectivePlaybackRange()
        {
            return (this.trimInPosition * this.totalDuration,
                    this.trimOutPosition * this.totalDuration);
        }

        public float ClampToTrimBounds(float position)
        {
            var (start, end) = GetEffectivePlaybackRange();
            return Mathf.Clamp(position, start, end);
        }
    }
    #endregion

    #region Private Fields
    private Dictionary<string, AudioSourceState> sourceStates = new Dictionary<string, AudioSourceState>();
    private string activeSourceId;
    private bool isAnyAudioPlaying = false;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        ResetAllStates();
    }
    #endregion

    #region State Management
    /// <summary>
    /// Clears all stored states at startup
    /// </summary>
    public void ResetAllStates()
    {
        sourceStates.Clear();
        activeSourceId = null;
        isAnyAudioPlaying = false;
        Debug.Log("[StateManager_01] All states reset");
    }

    /// <summary>
    /// Registers a new source in the system
    /// </summary>
    public void RegisterSource(GameObject source)
    {
        if (source == null) return;

        string sourceId = GetSourceId(source);
        if (!sourceStates.ContainsKey(sourceId))
        {
            sourceStates[sourceId] = new AudioSourceState
            {
                sourceId = sourceId,
                filePath = "",
                isAssigned = false,
                isPlaying = false,
                isInsideTrigger = false,
                isActive = false,
                isLooping = false,  // Initialize loop state
                playbackPosition = 0f,
                totalDuration = 0f,

                trimInPosition = 0f,
                trimOutPosition = 1f,
                fadeInDuration = 0f,
                fadeOutDuration = 0f,
                fadeInPoint = Vector2.zero,
                fadeOutPoint = new Vector2(1f, 0f)
            };
            Debug.Log($"[StateManager_01] Registered new source: {sourceId}");
        }
    }

    /// <summary>
    /// Updates an existing source's state
    /// </summary>
    public void UpdateSourceState(GameObject source, Action<AudioSourceState> updateAction)
    {
        if (source == null) return;

        string sourceId = GetSourceId(source);
        if (sourceStates.TryGetValue(sourceId, out var state))
        {
            // Store previous values for comparison
            float previousPosition = state.playbackPosition;
            float previousDuration = state.totalDuration;
            bool previousPlayState = state.isPlaying;

            // Store previous trim/fade values
            float previousTrimIn = state.trimInPosition;
            float previousTrimOut = state.trimOutPosition;
            Vector2 previousFadeIn = state.fadeInPoint;
            Vector2 previousFadeOut = state.fadeOutPoint;

            // Update state
            updateAction(state);

            if (enableStateDebugLogs)
            {
                Debug.Log($"[StateManager_01] Updated state for {sourceId}: {JsonUtility.ToJson(state)}");
            }


            // Trigger specific events based on what changed
            if (state.playbackPosition != previousPosition)
            {
                OnPositionChanged?.Invoke(sourceId, state.playbackPosition);
            }
            if (state.totalDuration != previousDuration)
            {
                OnDurationChanged?.Invoke(sourceId, state.totalDuration);
            }
            if (state.isPlaying != previousPlayState)
            {
                OnPlaybackStateChanged?.Invoke(sourceId, state.isPlaying);
            }

            // Trigger trim/fade events if changed
            if (state.trimInPosition != previousTrimIn || state.trimOutPosition != previousTrimOut)
            {
                OnTrimPointsChanged?.Invoke(sourceId, new TrimFadePoints
                {
                    TrimPosition = new Vector2(state.trimInPosition, 0),
                    FadePosition = new Vector2(state.trimOutPosition, 0)
                });
            }

            if (state.fadeInPoint != previousFadeIn || state.fadeOutPoint != previousFadeOut)
            {
                OnFadePointsChanged?.Invoke(sourceId, new TrimFadePoints
                {
                    TrimPosition = state.fadeInPoint,
                    FadePosition = state.fadeOutPoint
                });
            }

            // Always trigger the general state changed event
            OnSourceStateChanged?.Invoke(sourceId, state);
        }
    }
    #endregion

    #region State Queries
    /// <summary>
    /// Retrieves all source states
    /// </summary>
    public Dictionary<string, AudioSourceState> GetAllSourceStates()
    {
        return sourceStates;
    }

    /// <summary>
    /// Gets the state of a specific source
    /// </summary>
    public AudioSourceState GetSourceState(GameObject source)
    {
        string sourceId = GetSourceId(source);
        if (!sourceStates.ContainsKey(sourceId))
        {
            Debug.LogError($"[StateManager_01] No state found for {sourceId}");
            return null;
        }
        return sourceStates[sourceId];

    }
    #endregion

    #region Playback State Management
    /// <summary>
    /// Sets the active source and notifies listeners
    /// </summary>
    public void SetActiveSource(GameObject source)
    {
        string sourceId = GetSourceId(source);
        if (activeSourceId == sourceId) return;

        // Deactivate previous source
        if (!string.IsNullOrEmpty(activeSourceId))
        {
            UpdateSourceState(GetGameObjectFromSourceId(activeSourceId),
                state => state.isActive = false);
        }

        // Activate new source
        activeSourceId = sourceId;
        UpdateSourceState(source, state => state.isActive = true);
        OnSourceActivated?.Invoke(sourceId);
        //Debug.Log($"[StateManager_01] Active source set to: {sourceId}");
    }

    /// <summary>
    /// Updates the playback state of a source
    /// </summary>
    public void UpdatePlaybackState(GameObject source, bool isPlaying)
    {
        string sourceId = GetSourceId(source);
        UpdateSourceState(source, state => state.isPlaying = isPlaying);
        OnPlaybackStateChanged?.Invoke(sourceId, isPlaying);

        UpdateGlobalPlaybackState();
    }

    /// <summary>
    /// Updates source's total duration
    /// </summary>
    public void UpdateDuration(GameObject source, float duration)
    {
        if (source == null) return;

        UpdateSourceState(source, state =>
        {
            state.totalDuration = duration;
            OnDurationChanged?.Invoke(GetSourceId(source), duration);
        });
    }

    /// <summary>
    /// Updates source's current playback position
    /// </summary>
    public void UpdatePosition(GameObject source, float position)
    {
        if (source == null) return;

        UpdateSourceState(source, state =>
        {
            state.playbackPosition = position;
            // Only fire position event if this is the active source
            if (state.isActive)
            {
                OnPositionChanged?.Invoke(GetSourceId(source), position);
            }
        });
    }

    /// <summary>
    /// Sets trigger state for a source
    /// </summary>
    public void SetInsideTrigger(GameObject source, bool inside)
    {
        UpdateSourceState(source, state => state.isInsideTrigger = inside);
        //Debug.Log($"[StateManager_01] Trigger state for {GetSourceId(source)}: {inside}");
    }

    public void ToggleLoopState(GameObject source)
    {
        if (source == null) return;

        UpdateSourceState(source, state => {
            state.isLooping = !state.isLooping;
            Debug.Log($"[StateManager_01] Loop state for {state.sourceId} set to: {state.isLooping}");
        });

        var state = GetSourceState(source);
        if (state != null)
        {
            OnLoopStateChanged?.Invoke(state.sourceId, state.isLooping);
        }
    }

    // Get loop state method
    public bool GetLoopState(GameObject source)
    {
        var state = GetSourceState(source);
        return state?.isLooping ?? false;
    }

#endregion

#region Trim/Fade State Management
/// <summary>
/// Updates trim points for a source and notifies listeners
/// </summary>
public void UpdateTrimPoints(GameObject source, float trimInPos, float trimOutPos)
    {
        if (source == null) return;

        string sourceId = GetSourceId(source);
        UpdateSourceState(source, state =>
        {
            state.trimInPosition = Mathf.Clamp01(Mathf.Min(trimInPos, trimOutPos));
            state.trimOutPosition = Mathf.Clamp01(Mathf.Max(trimInPos, trimOutPos));
            state.playbackPosition = state.ClampToTrimBounds(state.playbackPosition);
        });

        var state = GetSourceState(source);
        if (state != null)
        {
            OnTrimPointsChanged?.Invoke(sourceId, new TrimFadePoints
            {
                TrimPosition = new Vector2(state.trimInPosition, 0),
                FadePosition = new Vector2(state.trimOutPosition, 0)
            });
        }
    }


    /// <summary>
    /// Updates fade points for a source and notifies listeners
    /// </summary>
    public void UpdateFadePoints(GameObject source, Vector2 fadeInPoint, Vector2 fadeOutPoint)
    {
        if (source == null) return;

        string sourceId = GetSourceId(source);
        UpdateSourceState(source, state =>
        {
            state.fadeInPoint = fadeInPoint;
            state.fadeOutPoint = fadeOutPoint;
            // Calculate fade durations based on the distance between fade and trim points
            state.fadeInDuration = Mathf.Abs(fadeInPoint.x - state.trimInPosition) * state.totalDuration;
            state.fadeOutDuration = Mathf.Abs(fadeOutPoint.x - state.trimOutPosition) * state.totalDuration;
        });

        OnFadePointsChanged?.Invoke(sourceId, new TrimFadePoints
        {
            TrimPosition = fadeInPoint,
            FadePosition = fadeOutPoint
        });
    }

    /// <summary>
    /// Calculates fade duration based on fade points and trim points
    /// </summary>
    private float CalculateFadeDuration(Vector2 fadePoint, Vector2 trimPoint, float totalDuration)
    {
        // Convert positions to absolute time values
        float fadeTimePosition = fadePoint.x * totalDuration;
        float trimTimePosition = trimPoint.x * totalDuration;

        // Calculate duration based on distance between fade and trim points
        return Mathf.Abs(fadeTimePosition - trimTimePosition);
    }


    public (float trimIn, float trimOut) GetTrimPoints(GameObject source)
    {
        var state = GetSourceState(source);
        return state != null ? (state.trimInPosition, state.trimOutPosition) : (0f, 1f);
    }

    public (Vector2 fadeIn, Vector2 fadeOut) GetFadePoints(GameObject source)
    {
        var state = GetSourceState(source);
        return state != null ? (state.fadeInPoint, state.fadeOutPoint) : (Vector2.zero, Vector2.zero);
    }
    #endregion

    #region Helper Methods
    private void UpdateGlobalPlaybackState()
    {
        isAnyAudioPlaying = sourceStates.Values.Any(s => s.isPlaying);
    }



    public string GetSourceId(GameObject source) =>
        source != null ? $"source_{source.GetInstanceID()}" : null;

    public GameObject GetGameObjectFromSourceId(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return null;

        int instanceId = int.Parse(sourceId.Replace("source_", ""));
        return (GameObject)UnityEngine.Object.FindObjectsOfType(typeof(GameObject))
            .FirstOrDefault(go => go.GetInstanceID() == instanceId);
    }

    /// <summary>
    /// Logs state updates in JSON format for better debugging
    /// </summary>
    private void LogStateUpdate(string sourceId, AudioSourceState state)
    {
        // This method is kept but not used in UpdateSourceState anymore
        string stateJson = JsonUtility.ToJson(state, true);
        Debug.Log($"[StateManager_01] State updated for {sourceId}:\n{stateJson}");
    }

    #region Debug Helpers
    private void LogTrimUpdate(string sourceId, float trimIn, float trimOut)
    {
        if (!enableStateDebugLogs) return;

        Debug.Log($"[StateManager_01] Trim points updated for {sourceId}:" +
                  $"\n\tTrim In: {trimIn:F3}" +
                  $"\n\tTrim Out: {trimOut:F3}");
    }

    private void LogFadeUpdate(string sourceId, Vector2 fadeIn, Vector2 fadeOut)
    {
        if (!enableStateDebugLogs) return;

        Debug.Log($"[StateManager_01] Fade points updated for {sourceId}:" +
                  $"\n\tFade In: {fadeIn}" +
                  $"\n\tFade Out: {fadeOut}");
    }

    private void LogStateTransition(string sourceId, string transition, string details = "")
    {
        if (!enableStateDebugLogs) return;

        Debug.Log($"[StateManager_01] {sourceId} - {transition}" +
                  (!string.IsNullOrEmpty(details) ? $"\n\tDetails: {details}" : ""));
    }
    #endregion


    #endregion
}