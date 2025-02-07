using UnityEngine;
using TMPro;

/// <summary>
/// Handles the display of timing information for the waveform viewer
/// </summary>
public class WaveformTimeDisplay : MonoBehaviour
{
    [Header("UI Text References")]
    [SerializeField] private TextMeshProUGUI totalDurationText;
    [SerializeField] private TextMeshProUGUI currentPositionText;
    [SerializeField] private TextMeshProUGUI trimRangeText;

    /// <summary>
    /// Updates all time displays
    /// </summary>
    public void UpdateTimeDisplay(float totalDuration, float currentPosition, float trimInPos, float trimOutPos)
    {
        // Update total duration
        if (totalDurationText != null)
        {
            totalDurationText.text = $"Total: {FormatTime(totalDuration)}";
        }

        // Update current position relative to trim in point
        if (currentPositionText != null)
        {
            float effectivePosition = currentPosition - (trimInPos * totalDuration);
            currentPositionText.text = $"Position: {FormatTime(effectivePosition)}";
        }

        // Update trim range showing in/out points and duration
        if (trimRangeText != null)
        {
            float trimStart = trimInPos * totalDuration;
            float trimEnd = trimOutPos * totalDuration;
            float trimDuration = trimEnd - trimStart;
            trimRangeText.text = $"Trim: {FormatTime(trimStart)} → {FormatTime(trimEnd)} ({FormatTime(trimDuration)})";
        }
    }

    /// <summary>
    /// Formats time in seconds to mm:ss format
    /// </summary>
    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    /// <summary>
    /// Resets all displays to 00:00
    /// </summary>
    public void ResetDisplays()
    {
        if (totalDurationText != null) totalDurationText.text = "Total: 00:00";
        if (currentPositionText != null) currentPositionText.text = "Position: 00:00";
        if (trimRangeText != null) trimRangeText.text = "Trim: 00:00 → 00:00 (00:00)";
    }
}