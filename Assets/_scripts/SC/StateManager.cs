using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Enhanced StateManager that consolidates state management functionality
/// and provides consistent interfaces for state access and updates
/// </summary>
public class StateManager : MonoBehaviour
{
    // Add at the start of the class
    private const string LOG_PREFIX = "[StateManager] ";
    [SerializeField] private bool verboseLogging = false;  // Toggle for detailed logging

    private void LogDebug(string message, bool verbose = false)
    {
        if (!verbose || (verbose && verboseLogging))
        {
            Debug.Log($"{LOG_PREFIX}{message}");
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"{LOG_PREFIX}{message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"{LOG_PREFIX}{message}");
    }

    #region Singleton Pattern
    private static StateManager instance;
    public static StateManager Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("StateManager");
                instance = go.AddComponent<StateManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    #endregion

    #region Events
    // Enhanced event system for state changes
    public delegate void SourceStateChangedHandler(string sourceId, AudioSourceState state);
    public delegate void SourceActivatedHandler(string sourceId);
    public delegate void PlaybackStateChangedHandler(string sourceId, bool isPlaying);

    public event SourceStateChangedHandler OnSourceStateChanged;
    public event SourceActivatedHandler OnSourceActivated;
    public event PlaybackStateChangedHandler OnPlaybackStateChanged;
    #endregion

    #region State Structure
    [System.Serializable]
    public class AudioSourceState
    {
        // File State
        public string sourceId;
        public string filePath;
        public bool isInitialized;

        // Playback State
        public bool isPlaying;
        public bool isLooping;
        public float playbackPosition;
        public int timelinePosition;

        // Visual State
        public bool isInTriggerBox;
        public bool isActiveInViewer;
        public Color buttonColor = Color.white;
        public bool isBlinking;

        // Waveform State
        public float leftTrimPosition;
        public float rightTrimPosition;
        public float leftFadePosition;
        public float rightFadePosition;

        // Transform State
        public Vector3 position;
        public float lastPlaybackPosition;

        public AudioSourceState Clone()
        {
            return new AudioSourceState
            {
                sourceId = this.sourceId,
                filePath = this.filePath,
                isInitialized = this.isInitialized,
                isPlaying = this.isPlaying,
                isLooping = this.isLooping,
                playbackPosition = this.playbackPosition,
                timelinePosition = this.timelinePosition,
                isInTriggerBox = this.isInTriggerBox,
                isActiveInViewer = this.isActiveInViewer,
                buttonColor = this.buttonColor,
                isBlinking = this.isBlinking,
                leftTrimPosition = this.leftTrimPosition,
                rightTrimPosition = this.rightTrimPosition,
                leftFadePosition = this.leftFadePosition,
                rightFadePosition = this.rightFadePosition,
                position = this.position,
                lastPlaybackPosition = this.lastPlaybackPosition
            };
        }
    }
    #endregion

    #region Fields
    private Dictionary<string, AudioSourceState> sourceStates = new Dictionary<string, AudioSourceState>();
    private string activeSourceId;
    private StateManager stateManager;
    private const string SAVE_KEY_PREFIX = "AudioSourceState_";
    #endregion

    #region State Management Methods


    /// <summary>
    /// Saves the current state of a source
    /// </summary>
    public void SaveCurrentState(GameObject source)
    {
        if (source == null) return;
        string sourceId = GetSourceId(source);
        SaveState(sourceId);
    }

    /// <summary>
    /// Updates state with specific modifications
    /// </summary>
    public void UpdateState(GameObject source, Action<AudioSourceState> updateAction)
    {
        if (source == null) return;
        string sourceId = GetSourceId(source);
        UpdateSourceProperties(sourceId, updateAction);
    }

    public bool ValidateState(AudioSourceState state)
    {
        if (state == null) return false;
        if (string.IsNullOrEmpty(state.sourceId)) return false;
        if (string.IsNullOrEmpty(state.filePath) && state.isInitialized) return false;
        return true;
    }

    /// <summary>
    /// Gets the state for a specific source using various identifier types
    /// </summary>
    public AudioSourceState GetSourceState(string sourceId)
    {
        return sourceStates.TryGetValue(sourceId, out AudioSourceState state) ? state.Clone() : null;
    }

    public AudioSourceState GetSourceState(GameObject source)
    {
        if (source == null) return null;
        string sourceId = GetSourceId(source);
        return GetSourceState(sourceId);
    }

    /// <summary>
    /// Updates state properties for a source
    /// </summary>
    public void UpdateSourceState(string sourceId, AudioSourceState newState)
    {
        if (string.IsNullOrEmpty(sourceId))
        {
            LogError("UpdateSourceState called with null/empty sourceId");
            return;
        }

        LogDebug($"Updating state for source: {sourceId}", true);
        LogDebug($"Previous state: {JsonUtility.ToJson(sourceStates.GetValueOrDefault(sourceId))}", true);
        LogDebug($"New state: {JsonUtility.ToJson(newState)}", true);

        if (!sourceStates.ContainsKey(sourceId))
        {
            LogDebug($"Creating new state entry for source: {sourceId}");
            sourceStates[sourceId] = new AudioSourceState();
        }

        var state = sourceStates[sourceId];
        UpdateStateProperties(state, newState);

        OnSourceStateChanged?.Invoke(sourceId, state.Clone());
        SaveState(sourceId);
        LogDebug($"State update complete for source: {sourceId}");
    }

    private bool VerifySourceState(GameObject button)
    {
        string sourceId = GetSourceId(button);
        var state = stateManager.GetSourceState(sourceId);
        var playbackScript = button.GetComponent<PlaybackScript>();

        if (state == null) return false;
        if (playbackScript == null) return false;

        // Verify state consistency
        bool isConsistent = state.isInitialized == playbackScript.IsFileAssigned &&
                           state.filePath == playbackScript.GetAssignedFilePath();

        if (!isConsistent)
        {
            Debug.LogWarning($"State inconsistency detected for source {sourceId}");
            // Could trigger state reconciliation here
        }

        return isConsistent;
    }

    public void SaveSourceState(GameObject source)
    {
        if (source == null) return;
        string sourceId = GetSourceId(source);

        var playbackScript = source.GetComponent<PlaybackScript>();
        if (playbackScript == null) return;

        playbackScript.GetCurrentState(out bool isActive, out float position,
            out bool isLooping, out string filePath);

        var state = new AudioSourceState
        {
            sourceId = sourceId,
            filePath = filePath,
            isPlaying = isActive,
            isLooping = isLooping,
            playbackPosition = position,
            isInitialized = playbackScript.IsFileAssigned
        };

        UpdateSourceState(sourceId, state);
    }
    public void RestoreSourceState(GameObject source)
    {
        if (source == null) return;
        string sourceId = GetSourceId(source);

        var state = GetSourceState(sourceId);
        if (state == null) return;

        var playbackScript = source.GetComponent<PlaybackScript>();
        if (playbackScript == null) return;

        try
        {
            // ✅ Set playback position to last saved position
            playbackScript.SetPlaybackPosition(state.playbackPosition);
            playbackScript.SetLoopState(state.isLooping);

            // ✅ Restore file assignment if needed
            if (state.isInitialized && !string.IsNullOrEmpty(state.filePath))
            {
                if (!playbackScript.IsFileAssigned)
                {
                    _ = playbackScript.AssignFile(state.filePath);
                }
            }

            // ✅ If in trigger box and should be playing, resume
            if (playbackScript.IsInTrigger && state.isPlaying)
            {
                playbackScript.SetPlaybackPosition(state.lastPlaybackPosition);
            }

            Debug.Log($"State restored for source: {sourceId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error restoring state for {sourceId}: {e.Message}");
        }
    }

    /// <summary>
    /// Updates specific properties of a source state
    /// </summary>
    public void UpdateSourceProperties(string sourceId, Action<AudioSourceState> updateAction)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        if (!sourceStates.ContainsKey(sourceId))
        {
            sourceStates[sourceId] = new AudioSourceState();
        }

        var state = sourceStates[sourceId];
        updateAction(state);

        OnSourceStateChanged?.Invoke(sourceId, state.Clone());
        SaveState(sourceId);
    }

    /// <summary>
    /// Sets the active source in the viewer
    /// </summary>
    public void SetActiveSource(string sourceId)
    {
        LogDebug($"Setting active source: {sourceId}");
        LogDebug($"Previous active source: {activeSourceId}", true);

        if (sourceId == activeSourceId)
        {
            LogDebug("Source already active, no change needed");
            return;
        }

        if (!string.IsNullOrEmpty(activeSourceId))
        {
            UpdateSourceProperties(activeSourceId, state => {
                state.isActiveInViewer = false;
                LogDebug($"Deactivated previous source: {activeSourceId}");
            });
        }

        activeSourceId = sourceId;
        if (!string.IsNullOrEmpty(sourceId))
        {
            UpdateSourceProperties(sourceId, state => {
                state.isActiveInViewer = true;
                LogDebug($"Activated new source: {sourceId}");
            });
            OnSourceActivated?.Invoke(sourceId);
        }
    }

    /// <summary>
    /// Updates the playback state for a source
    /// </summary>
    public void UpdatePlaybackState(string sourceId, bool isPlaying)
    {
        UpdateSourceProperties(sourceId, state => state.isPlaying = isPlaying);
        OnPlaybackStateChanged?.Invoke(sourceId, isPlaying);
    }
    #endregion

    #region Helper Methods
    private void UpdateStateProperties(AudioSourceState currentState, AudioSourceState newState)
    {
        // Update all relevant properties
        currentState.sourceId = newState.sourceId;
        currentState.filePath = newState.filePath;
        currentState.isInitialized = newState.isInitialized;
        currentState.isPlaying = newState.isPlaying;
        currentState.isLooping = newState.isLooping;
        currentState.playbackPosition = newState.playbackPosition;
        currentState.timelinePosition = newState.timelinePosition;
        currentState.isInTriggerBox = newState.isInTriggerBox;
        currentState.isActiveInViewer = newState.isActiveInViewer;
        currentState.buttonColor = newState.buttonColor;
        currentState.isBlinking = newState.isBlinking;
        currentState.leftTrimPosition = newState.leftTrimPosition;
        currentState.rightTrimPosition = newState.rightTrimPosition;
        currentState.leftFadePosition = newState.leftFadePosition;
        currentState.rightFadePosition = newState.rightFadePosition;
        currentState.position = newState.position;
        currentState.lastPlaybackPosition = newState.lastPlaybackPosition;
    }

    public string GetSourceId(GameObject source)
    {
        return source != null ? $"source_{source.GetInstanceID()}" : null;
    }

    public GameObject GetActiveSource()
    {
        if (string.IsNullOrEmpty(activeSourceId)) return null;
        return FindObjectFromInstanceID(ExtractInstanceID(activeSourceId));
    }

    private int ExtractInstanceID(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId) || !sourceId.StartsWith("source_")) return -1;
        if (int.TryParse(sourceId.Substring(7), out int id))
            return id;
        return -1;
    }

    private GameObject FindObjectFromInstanceID(int id)
    {
        if (id == -1) return null;
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.GetInstanceID() == id);
    }   
    public void SetVerboseLogging(bool enabled) // Add a method to toggle verbose logging
    {
        verboseLogging = enabled;
        LogDebug($"Verbose logging {(enabled ? "enabled" : "disabled")}");
    } 
    #endregion

    #region State Persistence
    private void SaveState(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId))
        {
            LogError("SaveState called with null/empty sourceId");
            return;
        }

        try
        {
            if (sourceStates.TryGetValue(sourceId, out AudioSourceState state))
            {
                string json = JsonUtility.ToJson(state);
                PlayerPrefs.SetString(SAVE_KEY_PREFIX + sourceId, json);
                PlayerPrefs.Save();
                LogDebug($"State saved for source: {sourceId}", true);
                LogDebug($"Saved state data: {json}", true);
            }
            else
            {
                LogWarning($"No state found to save for source: {sourceId}");
            }
        }
        catch (Exception e)
        {
            LogError($"Error saving state for {sourceId}: {e.Message}\n{e.StackTrace}");
        }
    }

    private void LoadAllStates()
    {
        LogDebug("Starting to load all states");
        sourceStates.Clear();

        string savedKeys = PlayerPrefs.GetString("SavedStateKeys", "");
        LogDebug($"Found saved state keys: {savedKeys}", true);

        foreach (string key in savedKeys.Split(','))
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith(SAVE_KEY_PREFIX)) continue;

            try
            {
                string json = PlayerPrefs.GetString(key);
                var state = JsonUtility.FromJson<AudioSourceState>(json);
                string sourceId = key.Substring(SAVE_KEY_PREFIX.Length);
                sourceStates[sourceId] = state;
                LogDebug($"Loaded state for source: {sourceId}", true);
                LogDebug($"Loaded state data: {json}", true);
            }
            catch (Exception e)
            {
                LogError($"Error loading state for key {key}: {e.Message}\n{e.StackTrace}");
            }
        }

        LogDebug($"Completed loading {sourceStates.Count} states");
    }

    private void SaveAllStates()
    {
        foreach (var sourceId in sourceStates.Keys.ToList())
        {
            SaveState(sourceId);
        }

        // Save list of state keys
        string keys = string.Join(",", sourceStates.Keys.Select(id => SAVE_KEY_PREFIX + id));
        PlayerPrefs.SetString("SavedStateKeys", keys);
        PlayerPrefs.Save();
    }

    public void ResetAllStates()
    {
        foreach (var sourceId in sourceStates.Keys.ToList())
        {
            sourceStates[sourceId] = new AudioSourceState
            {
                sourceId = sourceId,
                isPlaying = false,
                playbackPosition = 0f,
                isLooping = false,
                isInTriggerBox = false,
                isActiveInViewer = false,
                lastPlaybackPosition = 0f
            };
            OnSourceStateChanged?.Invoke(sourceId, sourceStates[sourceId]);
        }

        // Reset active source
        activeSourceId = null;

        // Clear PlayerPrefs
        foreach (var key in PlayerPrefs.GetString("SavedStateKeys", "").Split(','))
        {
            if (!string.IsNullOrEmpty(key))
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
        PlayerPrefs.DeleteKey("SavedStateKeys");
        PlayerPrefs.Save();

        Debug.Log("All states reset");
    }

    private void LogStateTransition(string sourceId, AudioSourceState oldState, AudioSourceState newState)
    {
        Debug.Log($"State transition for {sourceId}:");
        Debug.Log($"Playing: {oldState?.isPlaying} -> {newState.isPlaying}");
        Debug.Log($"Active: {oldState?.isActiveInViewer} -> {newState.isActiveInViewer}");
    }


    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        InitializeSingleton();
        ResetAllStates(); ;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            ResetAllStates();
        }
    }

    private void OnApplicationQuit()
    {
        ResetAllStates();
        SaveAllStates();
        sourceStates.Clear();
        instance = null;
    }

    private void InitializeSingleton()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion
}