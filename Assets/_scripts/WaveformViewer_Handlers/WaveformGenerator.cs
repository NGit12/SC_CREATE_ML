using UnityEngine;

public class WaveformGenerator : MonoBehaviour
{
    [Header("Waveform Visual Settings")]
    [Tooltip("Width of the generated waveform texture")]
    [SerializeField] private int textureWidth = 1024;
    public int TextureWidth => textureWidth;

    [Tooltip("Height of the generated waveform texture")]
    [SerializeField] private int textureHeight = 256;
    public int TextureHeight => textureHeight;

    [Tooltip("Main color of the waveform visualization")]
    [SerializeField] private Color waveformColor = Color.green;
    public Color WaveformColor => waveformColor;

    [Tooltip("Scale factor for the waveform amplitude")]
    [SerializeField] private float scaleFactor = 1.0f;
    public float ScaleFactor => scaleFactor;

    /// <summary>
    /// Generates a waveform texture accurately representing the amplitude of the sound.
    /// </summary>
    public Texture2D GenerateWaveformTexture(
        float[] leftChannelData,
        float[] rightChannelData,
        int customTextureWidth = 0,
        int customTextureHeight = 0,
        Color? customWaveformColor = null,
        float? customScaleFactor = null,
        Color? customCenterLineColor = null)
    {
        // Use custom values if provided, otherwise use serialized values
        int finalTextureWidth = customTextureWidth > 0 ? customTextureWidth : textureWidth;
        int finalTextureHeight = customTextureHeight > 0 ? customTextureHeight : textureHeight;
        Color finalWaveformColor = customWaveformColor ?? waveformColor;
        float finalScaleFactor = customScaleFactor ?? scaleFactor;

        if (leftChannelData == null || leftChannelData.Length == 0)
        {
            Debug.LogError("WaveformGenerator: Left channel data is null or empty. Cannot generate waveform.");
            return null;
        }

        Texture2D waveformTexture = new Texture2D(finalTextureWidth, finalTextureHeight, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[finalTextureWidth * finalTextureHeight];

        // Fill the texture with transparency
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        int stepSize = Mathf.CeilToInt(leftChannelData.Length / (float)finalTextureWidth);
        int textureMidpoint = finalTextureHeight / 2;

        // Find the maximum amplitude in the entire sample for proper scaling
        float maxGlobalAmplitude = 0f;
        for (int i = 0; i < leftChannelData.Length; i++)
        {
            maxGlobalAmplitude = Mathf.Max(maxGlobalAmplitude, Mathf.Abs(leftChannelData[i]));
        }

        // Adjust scale factor based on global maximum
        float amplitudeScaleFactor = (finalScaleFactor * textureMidpoint) / maxGlobalAmplitude;

        for (int x = 0; x < finalTextureWidth; x++)
        {
            // Initialize maximum absolute amplitude for this segment
            float maxAbsAmplitude = 0f;

            // Find the maximum absolute amplitude in this segment
            int startIndex = x * stepSize;
            int endIndex = Mathf.Min(startIndex + stepSize, leftChannelData.Length);

            for (int i = startIndex; i < endIndex; i++)
            {
                float absAmplitude = Mathf.Abs(leftChannelData[i]);
                if (absAmplitude > maxAbsAmplitude)
                {
                    maxAbsAmplitude = absAmplitude;
                }
            }

            // Scale the amplitude
            float scaledAmplitude = maxAbsAmplitude * amplitudeScaleFactor;

            // Calculate pixel positions for both top and bottom halves
            int topY = textureMidpoint + Mathf.RoundToInt(scaledAmplitude);
            int bottomY = textureMidpoint - Mathf.RoundToInt(scaledAmplitude);

            // Clamp the values to stay within texture bounds
            topY = Mathf.Clamp(topY, 0, finalTextureHeight - 1);
            bottomY = Mathf.Clamp(bottomY, 0, finalTextureHeight - 1);

            // Draw the waveform symmetrically
            for (int y = bottomY; y <= topY; y++)
            {
                float distanceFromCenter = Mathf.Abs(y - textureMidpoint) / (float)textureMidpoint;
                Color pixelColor = finalWaveformColor;
                pixelColor.a = Mathf.Lerp(1f, 0.7f, distanceFromCenter); // Slight fade at extremes
                pixels[y * finalTextureWidth + x] = pixelColor;
            }
        }

        // Apply the pixels to the texture and return
        waveformTexture.SetPixels(pixels);
        waveformTexture.Apply();

        return waveformTexture;
    }
}