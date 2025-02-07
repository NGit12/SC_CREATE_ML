using System;
using System.Threading.Tasks;
using UnityEngine;
using FMODUnity;
using FMOD;

public class WaveformDataReader
{
    /// <summary>
    /// Asynchronously reads normalized PCM data from an audio file using FMOD.
    /// </summary>
    /// <param name="filePath">The file path of the audio file.</param>
    /// <param name="resolution">The desired resolution of the waveform data.</param>
    /// <param name="channel">The audio channel to extract (0 for left, 1 for right).</param>
    /// <returns>Normalized PCM data for waveform visualization.</returns>
    
    public static async Task<float[]> ReadWaveformDataAsync(string filePath, int resolution, int channel = 0)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            UnityEngine.Debug.LogError("WaveformDataReader: File path is null or empty.");
            return null;
        }

        FMOD.Sound sound;
        FMOD.RESULT result = RuntimeManager.CoreSystem.createSound(
            filePath,
            FMOD.MODE.DEFAULT | FMOD.MODE.LOOP_OFF | FMOD.MODE.ACCURATETIME,
            out sound
        );

        if (result != FMOD.RESULT.OK)
        {
            UnityEngine.Debug.LogError($"WaveformDataReader: Failed to create sound. FMOD Error: {result}");
            return null;
        }

        // Retrieve PCM data information
        sound.getLength(out uint pcmLength, FMOD.TIMEUNIT.PCMBYTES);
        sound.getFormat(out SOUND_TYPE type, out SOUND_FORMAT format, out int channels, out int bits);

        if (channel >= channels)
        {
            UnityEngine.Debug.LogWarning($"WaveformDataReader: Invalid channel index {channel} for a file with {channels} channel(s). Defaulting to channel 0.");
            channel = 0;
        }

        int totalSamples = (int)(pcmLength / (bits / 8) / channels);
        int skipInterval = Mathf.Max(1, totalSamples / resolution);
        float[] waveformData = new float[resolution];

        IntPtr pcmData1 = IntPtr.Zero, pcmData2 = IntPtr.Zero;
        uint len1 = 0, len2 = 0;

        try
        {
            FMOD.RESULT lockResult = sound.@lock(0, pcmLength, out pcmData1, out pcmData2, out len1, out len2);
            if (lockResult != FMOD.RESULT.OK)
            {
                UnityEngine.Debug.LogError($"WaveformDataReader: Failed to lock PCM data. FMOD Error: {lockResult}");
                return null;
            }

            await Task.Run(() =>
            {
                unsafe
                {
                    short* samples = (short*)pcmData1.ToPointer();
                    if (samples == null)
                    {
                        UnityEngine.Debug.LogError("WaveformDataReader: PCM data pointer is null.");
                        return;
                    }

                    for (int i = 0, dataIndex = 0; i < totalSamples && dataIndex < resolution; i += skipInterval, dataIndex++)
                    {
                        waveformData[dataIndex] = Mathf.Abs(samples[i * channels + channel] / 32768f); // Normalize
                    }
                }
            });
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"WaveformDataReader: Error processing PCM data - {ex.Message}");
            return null;
        }
        finally
        {
            if (pcmData1 != IntPtr.Zero || pcmData2 != IntPtr.Zero)
            {
                sound.unlock(pcmData1, pcmData2, len1, len2);
            }

            sound.release();
        }

        return waveformData;
    }
}

