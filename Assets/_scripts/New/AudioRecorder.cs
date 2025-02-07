using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;

/// <summary>
/// Handles audio recording functionality with a simplified UI interface
/// </summary>
public class AudioRecorder : MonoBehaviour
{
    #region Events
    // Event triggered when a recording is successfully saved
    public static event Action<string> OnRecordingSaved;
    #endregion

    #region Recording Settings
    [Header("Recording Settings")]
    [SerializeField] private int sampleRate = 44100;
    [SerializeField] private int maxRecordingTime = 120;
    #endregion

    #region UI References
    [Header("UI Components")]
    [SerializeField] private Button recordButton;
    [SerializeField] private TextMeshProUGUI recordButtonText;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private GameObject nameInputPanel;
    [SerializeField] private TMP_InputField fileNameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    #endregion

    #region Private Variables
    private AudioClip recording;
    private bool isRecording = false;
    private StateManager_01 stateManager;
    #endregion

    #region Unity Lifecycle Methods
    private void Awake()
    {
        // Get reference to StateManager
        stateManager = StateManager_01.Instance;
        InitializeUI();
    }

    private void Start()
    {
        if (nameInputPanel != null)
        {
            nameInputPanel.SetActive(false);
        }
    }
    #endregion

    #region UI Initialization
    /// <summary>
    /// Initialize UI components and add listeners
    /// </summary>
    private void InitializeUI()
    {
        if (recordButton) recordButton.onClick.AddListener(ToggleRecording);
        if (saveButton) saveButton.onClick.AddListener(SaveRecording);
        if (cancelButton) cancelButton.onClick.AddListener(CancelRecording);

        UpdateUI("Start Recording", "Ready to record");
    }
    #endregion

    #region Recording Controls
    /// <summary>
    /// Toggle between starting and stopping recording
    /// </summary>
    private void ToggleRecording()
    {
        if (!isRecording)
            StartRecording();
        else
            StopRecording();
    }

    /// <summary>
    /// Start audio recording
    /// </summary>
    private void StartRecording()
    {
        // Check for microphone availability
        if (Microphone.devices.Length == 0)
        {
            UpdateUI("Start Recording", "No microphone found!");
            return;
        }

        try
        {
            // Start recording
            recording = Microphone.Start(null, false, maxRecordingTime, sampleRate);
            isRecording = true;
            UpdateUI("Stop Recording", "Recording in progress...");
        }
        catch (Exception e)
        {
            Debug.LogError($"[AudioRecorder] Recording error: {e.Message}");
            UpdateUI("Start Recording", "Recording failed!");
        }
    }

    /// <summary>
    /// Stop current recording and show naming panel
    /// </summary>
    private void StopRecording()
    {
        Microphone.End(null);
        isRecording = false;

        if (recording != null)
        {
            ShowNamingPanel();
        }
        else
        {
            UpdateUI("Start Recording", "Recording failed!");
        }
    }
    #endregion

    #region UI Handling
    /// <summary>
    /// Display the naming panel for saving the recording
    /// </summary>
    private void ShowNamingPanel()
    {
        if (nameInputPanel != null && fileNameInput != null)
        {
            nameInputPanel.SetActive(true);

            // Set default filename with timestamp
            fileNameInput.text = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}";

            // Focus input field
            fileNameInput.Select();
            fileNameInput.ActivateInputField();

            UpdateUI("Start Recording", "Enter recording name");
        }
    }

    /// <summary>
    /// Save the recorded audio file
    /// </summary>
    private void SaveRecording()
    {
    if (string.IsNullOrEmpty(fileNameInput?.text)) return;

    try
    {
        string fileName = $"{fileNameInput.text}.wav";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        // First calculate the proper sample length based on recording time
        int lastSample = Microphone.GetPosition(null);
        if (lastSample <= 0) lastSample = recording.samples;

        // Create a new AudioClip with exact length
        AudioClip trimmedClip = AudioClip.Create(recording.name,
            lastSample,
            recording.channels,
            recording.frequency,
            false);

        // Get the data from original recording
        float[] samples = new float[lastSample * recording.channels];
        recording.GetData(samples, 0);

        // Set the data to our trimmed clip
        trimmedClip.SetData(samples, 0);

        // Convert and save the trimmed clip
        byte[] wavData = WavUtility.ConvertToWav(trimmedClip);
        File.WriteAllBytes(filePath, wavData);

        // Clean up
        Destroy(recording);
        recording = trimmedClip;

        nameInputPanel.SetActive(false);
        UpdateFeedbackText("Recording saved!");
        OnRecordingSaved?.Invoke(filePath);

        Debug.Log($"[AudioRecorder] Recording saved: Duration={trimmedClip.length}s, Samples={lastSample}, Path={filePath}");
    }
    catch (Exception e)
    {
        Debug.LogError($"[AudioRecorder] Save error: {e.Message}");
        UpdateFeedbackText("Failed to save recording!");
    }
}

    /// <summary>
    /// Cancel the current recording
    /// </summary>
    private void CancelRecording()
    {
        nameInputPanel.SetActive(false);
        recording = null;
        UpdateUI("Start Recording", "Recording cancelled");
    }

    /// <summary>
    /// Update UI elements with current state
    /// </summary>
    private void UpdateUI(string buttonText, string feedbackMessage)
    {
        if (recordButtonText) recordButtonText.text = buttonText;
        if (feedbackText) feedbackText.text = feedbackMessage;
    }

    /// <summary>
    /// Updates the feedback text with the provided message and logs it for debugging
    /// </summary>
    /// <param name="message">Message to display in the feedback text</param>
    private void UpdateFeedbackText(string message)
    {
        // Update UI text if component exists
        if (feedbackText != null)
        {
            feedbackText.text = message;
            Debug.Log($"[AudioRecorder] Feedback: {message}");
        }
        else
        {
            Debug.LogWarning("[AudioRecorder] Feedback text component is missing!");
        }
    }
    #endregion
}