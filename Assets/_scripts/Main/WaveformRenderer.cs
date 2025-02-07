using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WaveformRenderer : MonoBehaviour
{
    public RectTransform waveformPlaceholder;
    public RectTransform trimHead;
    public TMP_Text sourceButtonIndicator;
    public Slider fadeInSlider;
    public Slider fadeOutSlider;

    private Image waveformImage;
    private string activeSourceButtonName;

    // Define static colors for each button
    private readonly Color S1Color = Color.red;
    private readonly Color S2Color = Color.blue;
    private readonly Color S3Color = Color.green;

    private void InitializeWaveform()
    {
        waveformImage = waveformPlaceholder.GetComponent<Image>();
        if (waveformImage == null)
        {
            waveformImage = waveformPlaceholder.gameObject.AddComponent<Image>();
        }
        waveformImage.color = Color.white; // Default color
    }

    public void LoadWaveform(string filePath)
    {
        if (waveformImage == null)
            InitializeWaveform();

        Debug.Log("WaveformRenderer: Loading waveform for " + filePath);
        waveformImage.fillAmount = 1.0f;

        UpdateSourceButtonIndicator(activeSourceButtonName, System.IO.Path.GetFileName(filePath));
    }

    public void HighlightSourceButton(string buttonName)
    {
        activeSourceButtonName = buttonName;
        sourceButtonIndicator.text = $"Active Source: {buttonName}";
        Debug.Log("WaveformRenderer: Highlighting " + buttonName);
    }

    public void ToggleWaveformView(GameObject button)
    {
        Debug.Log($"ToggleWaveformView: Toggling view for button = {button.name}");

        if (button == null)
        {
            Debug.LogError("WaveformRenderer: Null button passed to ToggleWaveformView!");
            return;
        }

        var playbackScript = button.GetComponent<PlaybackScript>();
        if (playbackScript != null)
        {
            string assignedFilePath = playbackScript.GetAssignedFilePath();
            if (!string.IsNullOrEmpty(assignedFilePath))
            {
                // Highlight the source button and update the indicator
                HighlightSourceButton(button.name);
                UpdateSourceButtonIndicator(button.name, System.IO.Path.GetFileName(assignedFilePath));

                // Update the waveform view color based on the button
                switch (button.name)
                {
                    case "Button_S1":
                        waveformImage.color = Color.blue;
                        break;
                    case "Button_S2":
                        waveformImage.color = Color.red;
                        break;
                    case "Button_S3":
                        waveformImage.color = Color.green;
                        break;
                    default:
                        waveformImage.color = Color.gray;
                        break;
                }

                Debug.Log($"WaveformRenderer: Toggled view for {button.name} with file {assignedFilePath}");
            }
            else
            {
                Debug.LogWarning($"WaveformRenderer: No file assigned to {button.name}.");
            }
        }
        else
        {
            Debug.LogError($"WaveformRenderer: Button {button.name} does not have a PlaybackScript!");
        }
    }

    private void UpdateSourceButtonIndicator(string sourceButtonName, string fileName)
    {
        sourceButtonIndicator.text = $"Active Source: {sourceButtonName}\nFile: {fileName}";
        Debug.Log($"WaveformRenderer: Updated indicator for {sourceButtonName} with file {fileName}");
    }

    public void AdjustFadeIn(float value)
    {
        Debug.Log($"WaveformRenderer: Adjusting fade-in to {value}");
    }

    public void AdjustFadeOut(float value)
    {
        Debug.Log($"WaveformRenderer: Adjusting fade-out to {value}");
    }

    public void UpdateTrimHeadPosition(float normalizedPosition)
    {
        Debug.Log($"WaveformRenderer: Updating trim head to {normalizedPosition}");
        Vector2 position = trimHead.anchoredPosition;
        position.x = normalizedPosition * waveformPlaceholder.rect.width;
        trimHead.anchoredPosition = position;
    }
}
