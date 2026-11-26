using System.IO;
using System.Media;
using System.Text;

namespace DynamicIsland;

static class SoundService
{
    const int SampleRate = 44100;
    static readonly Dictionary<string, byte[]> SoundWavs = new();

    static SoundService()
    {
        try
        {
            SoundWavs["charging"] = SynthesizeChime([(523.25, 0.12), (659.25, 0.12), (783.99, 0.22)]);
            SoundWavs["unplugged"] = SynthesizeChime([(659.25, 0.10), (523.25, 0.18)]);
            SoundWavs["battery_low"] = SynthesizeChime([(440.0, 0.14), (392.0, 0.25)]);
            SoundWavs["battery_critical"] = SynthesizeChime([(880.0, 0.10), (0.0, 0.05), (880.0, 0.18)]);
            SoundWavs["battery_full"] = SynthesizeChime([(523.25, 0.10), (659.25, 0.10), (783.99, 0.10), (1046.50, 0.28)]);
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
    }

    public static void Play(string name)
    {
        if (!SoundWavs.TryGetValue(name, out byte[]? wav) || wav == null) return;
        Task.Run(() =>
        {
            try
            {
                using var ms = new MemoryStream(wav);
                using var player = new SoundPlayer(ms);
                player.Play();
            }
            catch { }
        });
    }

    static byte[] SynthesizeChime((double Freq, double Dur)[] notes)
    {
        int totalSamples = 0;
        foreach (var (_, dur) in notes)
            totalSamples += (int)(SampleRate * dur);

        int dataSize = totalSamples * 2;
        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt subchunk
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); // Subchunk1Size (16 for PCM)
        bw.Write((short)1); // AudioFormat (1 for PCM)
        bw.Write((short)1); // NumChannels (1 mono)
        bw.Write(SampleRate); // SampleRate
        bw.Write(SampleRate * 2); // ByteRate (SampleRate * NumChannels * BitsPerSample/8)
        bw.Write((short)2); // BlockAlign (NumChannels * BitsPerSample/8)
        bw.Write((short)16); // BitsPerSample

        // data subchunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);

        foreach (var (freq, dur) in notes)
        {
            int numSamples = (int)(SampleRate * dur);
            for (int i = 0; i < numSamples; i++)
            {
                double t = i / (double)SampleRate;
                short sample = 0;
                if (freq > 1.0)
                {
                    double env = Math.Exp(-3.5 * (t / dur));
                    double val = 32767.0 * 0.40 * env * Math.Sin(2.0 * Math.PI * freq * t);
                    sample = (short)Math.Clamp(Math.Round(val), short.MinValue, short.MaxValue);
                }
                bw.Write(sample);
            }
        }

        return ms.ToArray();
    }
}
