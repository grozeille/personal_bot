using System.Diagnostics;
using NAudio.Wave;

namespace TabletApp.AI;

/// <summary>Extrait micro : WAV finalisé + détection de parole (VAD énergie).</summary>
internal sealed record VoiceClip(byte[] Wav, bool HadSpeech, double PeakRms, TimeSpan RecordedFor);

/// <summary>
/// Entrées/sorties audio (NAudio, WinMM/WASAPI, compatible win-x86) :
/// capture micro et lecture WAV pour le test voix.
/// </summary>
internal static class NAudioVoice
{
    // Parole vs bruit de fond (RMS sur échantillons 16 bits) + 1,2 s de
    // silence après la parole pour couper l'écoute sans attendre le max.
    internal const double SpeechRmsThreshold = 500.0;
    private static readonly TimeSpan SilenceHang = TimeSpan.FromMilliseconds(1200);

    /// <summary>
    /// Enregistre le micro (16 kHz, mono, 16 bits) jusqu'au bout de
    /// <paramref name="maxDuration"/> ou 1,2 s de silence après la parole.
    /// </summary>
    public static async Task<VoiceClip> RecordAsync(TimeSpan maxDuration, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        using var input = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
        };
        var writer = new WaveFileWriter(stream, input.WaveFormat);
        object sync = new();
        bool speech = false;
        double peak = 0;
        var watch = Stopwatch.StartNew();
        TimeSpan lastSpeechAt = TimeSpan.Zero;
        input.DataAvailable += (_, e) =>
        {
            writer.Write(e.Buffer, 0, e.BytesRecorded);
            double rms = ComputeRms(e.Buffer, e.BytesRecorded);
            lock (sync)
            {
                if (rms > peak)
                {
                    peak = rms;
                }
                if (rms >= SpeechRmsThreshold)
                {
                    speech = true;
                    lastSpeechAt = watch.Elapsed;
                }
            }
        };
        try
        {
            input.StartRecording();
            while (true)
            {
                await Task.Delay(50, ct).ConfigureAwait(false);
                lock (sync)
                {
                    if (watch.Elapsed >= maxDuration)
                    {
                        break;
                    }
                    if (speech && watch.Elapsed - lastSpeechAt >= SilenceHang)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            input.StopRecording();
            // Finalise l'en-tête WAV (ToArray reste lisible après).
            writer.Dispose();
        }
        lock (sync)
        {
            return new VoiceClip(stream.ToArray(), speech, peak, watch.Elapsed);
        }
    }

    /// <summary>Joue des octets WAV jusqu'au bout (ou annulation).</summary>
    public static async Task PlayAsync(byte[] wav, CancellationToken ct, bool raw = false)
    {
        using var stream = new MemoryStream(wav, writable: false);
        using var reader = new WaveFileReader(stream);
        // Tampon large : les prompts ne sont pas interactifs, et l'Atom subit
        // des pointes CPU (Defender, updates) qui affameraient un tampon court.
        using var output = new WaveOutEvent() { DesiredLatency = 500 };
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, _) => done.TrySetResult();
        WaveStream source = reader;
        if (!raw && RobotVoiceFilter.Enabled && reader.WaveFormat.Encoding == WaveFormatEncoding.Pcm
            && reader.WaveFormat.BitsPerSample == 16 && reader.WaveFormat.Channels == 1)
        {
            source = new RobotVoiceStream(reader, new RobotVoiceFilter());
        }
        output.Init(source);
        output.Play();
        using var registration = ct.Register(() => output.Stop());
        await done.Task.ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
    }

    /// <summary>Charge un WAV en mono 16 kHz flottant (convertit si besoin).</summary>
    internal static bool LoadMono16k(string path, out float[]? samples)
    {
        samples = null;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }
            using var reader = new WaveFileReader(path);
            using var converted = reader.WaveFormat.SampleRate == 16000
                && reader.WaveFormat.Channels == 1 && reader.WaveFormat.BitsPerSample == 16
                ? null
                : new WaveFormatConversionStream(new WaveFormat(16000, 16, 1), reader);
            WaveStream source = (WaveStream?)converted ?? reader;
            using var data = new MemoryStream();
            source.CopyTo(data);
            byte[] bytes = data.ToArray();
            samples = new float[bytes.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8)) / 32768f;
            }
            return true;
        }
        catch
        {
            samples = null;
            return false;
        }
    }

    /// <summary>
    /// Taux de passages par zéro d'un buffer PCM 16 bits mono (0..1) : une
    /// voyelle est basse (~0.05-0.15), un claquement ou du bruit blanc haut
    /// (~0.5-1.0). Complète le RMS (fort mais non voisé = bruit).
    /// </summary>
    internal static double ComputeZcr(byte[] buffer, int count)
    {
        int samples = count / 2;
        if (samples < 2)
        {
            return 1.0;
        }
        int crossings = 0;
        short previous = (short)(buffer[0] | (buffer[1] << 8));
        for (int i = 1; i < samples; i++)
        {
            short current = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
            if ((previous >= 0) != (current >= 0))
            {
                crossings++;
            }
            previous = current;
        }
        return (double)crossings / (samples - 1);
    }

    /// <summary>Niveau RMS d'un buffer PCM 16 bits mono (0 = silence).</summary>
    internal static double ComputeRms(byte[] buffer, int count)
    {
        int samples = count / 2;
        if (samples == 0)
        {
            return 0;
        }
        double sum = 0;
        for (int i = 0; i < samples; i++)
        {
            short sample = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
            sum += (double)sample * sample;
        }
        return Math.Sqrt(sum / samples);
    }
}
