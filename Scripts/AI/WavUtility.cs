using System;
using System.IO;
using UnityEngine;

public static class WavUtility
{
    public static byte[] FromAudioFloat(float[] samples, int channels, int sampleRate)
    {
        MemoryStream stream = new MemoryStream();
        BinaryWriter writer = new BinaryWriter(stream);

        int byteRate = sampleRate * channels * 2;
        int subChunk2 = samples.Length * 2;
        int chunkSize = 36 + subChunk2;

        // WAV header
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(chunkSize);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(subChunk2);

        // PCM 轉換
        foreach (float f in samples)
        {
            short val = (short)Mathf.Clamp(f * 32767f, -32768, 32767);
            writer.Write(val);
        }

        return stream.ToArray();
    }
}
