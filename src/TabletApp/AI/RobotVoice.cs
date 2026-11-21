using System.Text.Json;
using NAudio.Wave;

namespace TabletApp.AI;

/// <summary>
/// Grain robot façon EVE : reflet métallique léger (modulation 55 Hz, 12 %)
/// + passe-bande radio (400-3400 Hz, comme une annonce avion), sans hachage
/// ni micro-coupure. Phase et états de filtres conservés entre les appels
/// pour rester continu sur le streaming. Basculable via appsettings.json
/// (Voice:RobotFilter, défaut vrai, relu au démarrage).
/// </summary>
internal sealed class RobotVoiceFilter
{
    internal const double CarrierHz = 55.0;
    internal const double Depth = 0.12;
    internal const double HighpassHz = 400.0;
    internal const double LowpassHz = 3400.0;
    internal const bool DefaultEnabled = true;

    private static readonly Lazy<bool> EnabledCache = new(() => LoadEnabled());

    private double _phase;
    private double _hpPrevIn;
    private double _hpPrevOut;
    private double _lpPrevOut;

    internal static bool Enabled => EnabledCache.Value;

    internal static bool LoadEnabled(string? configPath = null)
    {
        try
        {
            string path = configPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return DefaultEnabled;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("Voice", out JsonElement voice)
                && voice.TryGetProperty("RobotFilter", out JsonElement flag)
                && (flag.ValueKind == JsonValueKind.True || flag.ValueKind == JsonValueKind.False))
            {
                return flag.GetBoolean();
            }
        }
        catch
        {
            // Config illisible : repli silencieux sur le défaut (jamais de crash).
        }
        return DefaultEnabled;
    }

    /// <summary>Applique l'effet en place sur du PCM 16 bits mono.</summary>
    internal void Apply(byte[] buffer, int offset, int count, int sampleRate)
    {
        if (sampleRate <= 0 || count <= 0)
        {
            return;
        }
        int start = Math.Max(0, offset);
        int end = Math.Min(buffer.Length, offset + count);
        double step = 2.0 * Math.PI * CarrierHz / sampleRate;
        double dt = 1.0 / sampleRate;
        double hpAlpha = 1.0 / (1.0 + 2.0 * Math.PI * HighpassHz * dt);
        double lpAlpha = dt / (1.0 / (2.0 * Math.PI * LowpassHz) + dt);
        for (int i = start; i + 1 < end; i += 2)
        {
            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
            double ring = sample * (1.0 - Depth + Depth * Math.Sin(_phase));
            _phase += step;
            if (_phase > 2.0 * Math.PI)
            {
                _phase -= 2.0 * Math.PI;
            }
            double hp = hpAlpha * (_hpPrevOut + ring - _hpPrevIn);
            _hpPrevIn = ring;
            _hpPrevOut = hp;
            double lp = _lpPrevOut + lpAlpha * (hp - _lpPrevOut);
            _lpPrevOut = lp;
            short shaped = (short)Math.Clamp(lp, short.MinValue, short.MaxValue);
            buffer[i] = (byte)(shaped & 0xFF);
            buffer[i + 1] = (byte)((shaped >> 8) & 0xFF);
        }
    }
}

/// <summary>Flux NAudio qui robotise un flux PCM 16 bits mono à la volée.</summary>
internal sealed class RobotVoiceStream : WaveStream
{
    private readonly WaveStream _source;
    private readonly RobotVoiceFilter _filter;

    internal RobotVoiceStream(WaveStream source, RobotVoiceFilter filter)
    {
        _source = source;
        _filter = filter;
    }

    public override WaveFormat WaveFormat => _source.WaveFormat;

    public override long Length => _source.Length;

    public override long Position
    {
        get => _source.Position;
        set => _source.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        _filter.Apply(buffer, offset, read, WaveFormat.SampleRate);
        return read;
    }
}
