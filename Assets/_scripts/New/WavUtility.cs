using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using FMODUnity;
using FMOD;

public static class WavUtility
{
    /// <summary>
    /// Converts an AudioClip to WAV format and returns the byte array.
    /// </summary>
    /// <param name="clip">AudioClip to convert</param>
    /// <returns>Byte array of WAV file</returns>
    public static byte[] ConvertToWav(AudioClip clip)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            int sampleRate = clip.frequency;
            int channels = clip.channels;
            float[] samples = new float[clip.samples * channels];
            clip.GetData(samples, 0);

            // Write WAV Header
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + samples.Length * 2);
            writer.Write("WAVE".ToCharArray());
            writer.Write("fmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write("data".ToCharArray());
            writer.Write(samples.Length * 2);

            // Write PCM Data
            foreach (float sample in samples)
            {
                short intSample = (short)(sample * 32767);
                writer.Write(intSample);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }

    /// <summary>
    /// Reads waveform data from a WAV file using FMOD.
    /// </summary>
    /// <param name="filePath">Path of the WAV file.</param>
    /// <param name="resolution">Resolution for waveform data extraction.</param>
    /// <returns>Normalized PCM data for visualization.</returns>
    public static float[] ReadWaveformData(string filePath, int resolution)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            UnityEngine.Debug.Log("WavUtility: File path is null or empty.");
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
            UnityEngine.Debug.Log($"WavUtility: Failed to create sound. FMOD Error: {result}");
            return null;
        }

        sound.getLength(out uint pcmLength, FMOD.TIMEUNIT.PCMBYTES);
        sound.getFormat(out SOUND_TYPE type, out SOUND_FORMAT format, out int channels, out int bits);

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
                UnityEngine.Debug.LogError($"WavUtility: Failed to lock PCM data. FMOD Error: {lockResult}");
                return null;
            }

            unsafe
            {
                short* samples = (short*)pcmData1.ToPointer();
                for (int i = 0, dataIndex = 0; i < totalSamples && dataIndex < resolution; i += skipInterval, dataIndex++)
                {
                    waveformData[dataIndex] = Mathf.Abs(samples[i * channels] / 32768f);
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"WavUtility: Error processing PCM data - {ex.Message}");
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
