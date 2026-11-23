using System.Text.Json;
using NAudio.Wave;

namespace TabletApp.AI;

/// <summary>
/// Grain robot façon EVE : reflet métallique léger (modulation 55 Hz, 12 %)
/// + passe-bande radio (400-3400 Hz, comme une annonce avion), sans hachage
/// ni micro-coupure. Phase et états de filtres conservés entre les appels
/// pour rester continu sur le streaming. Basculable via appsettings.json
/// (Voice:RobotFilter, défaut vrai, relu au démarrage).
/// Pitch : rééchantillonnage +N demi-tons (plus aigu ET plus rapide),
/// via Voice:PitchSemitones (0-6, défaut 0). Position fractionnaire
/// conservée entre les appels : pas de clic aux frontières de chunks.
/// </summary>
internal sealed class RobotVoiceFilter
{
    internal const double CarrierHz = 55.0;
    internal const double Depth = 0.12;
    internal const double HighpassHz = 400.0;
    internal const double LowpassHz = 3400.0;
    internal const bool DefaultEnabled = true;
    internal const double DefaultPitchSemitones = 0.0;
    internal const double MaxPitchSemitones = 6.0;

    private static readonly Lazy<bool> EnabledCache = new(() => LoadEnabled());
    private static readonly Lazy<double> PitchCache = new(() => LoadPitchSemitones());

    private readonly double _step;
    private double _carry;
    private double _phase;
    private double _hpPrevIn;
    private double _hpPrevOut;
    private double _lpPrevOut;

    internal static bool Enabled => EnabledCache.Value;

    internal RobotVoiceFilter(double? pitchSemitones = null)
    {
        double semi = Math.Clamp(
            pitchSemitones ?? PitchCache.Value, 0.0, MaxPitchSemitones);
        _step = Math.Pow(2.0, semi / 12.0);
    }

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

    internal static double LoadPitchSemitones(string? configPath = null)
    {
        try
        {
            string path = configPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return DefaultPitchSemitones;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("Voice", out JsonElement voice)
                && voice.TryGetProperty("PitchSemitones", out JsonElement pitch)
                && pitch.ValueKind == JsonValueKind.Number
                && pitch.TryGetDouble(out double semi)
                && double.IsFinite(semi))
            {
                return Math.Clamp(semi, 0.0, MaxPitchSemitones);
            }
        }
        catch
        {
            // Config illisible : repli silencieux sur le défaut (jamais de crash).
        }
        return DefaultPitchSemitones;
    }

    /// <summary>
    /// Applique l'effet en place sur du PCM 16 bits mono. Rééchantillonne
    /// (+N demi-tons) quand le pitch est non nul. Retourne les octets
    /// produits (toujours ≤ count, le pitch ne fait que raccourcir).
    /// Écriture en avant sûre : chaque sortie lit à une position ≥ la sienne.
    /// </summary>
    internal int Apply(byte[] buffer, int offset, int count, int sampleRate)
    {
        if (sampleRate <= 0 || count <= 0)
        {
            return 0;
        }
        int start = Math.Max(0, offset);
        int samples = Math.Max(0, Math.Min(count, buffer.Length - start) / 2);
        double step = 2.0 * Math.PI * CarrierHz / sampleRate;
        double dt = 1.0 / sampleRate;
        double hpAlpha = 1.0 / (1.0 + 2.0 * Math.PI * HighpassHz * dt);
        double lpAlpha = dt / (1.0 / (2.0 * Math.PI * LowpassHz) + dt);
        double src = _carry;
        int produced = 0;
        while (true)
        {
            int i = (int)src;
            if (i >= samples)
            {
                break;
            }
            double frac = src - i;
            short a = ReadSample(buffer, start + i * 2);
            short b = i + 1 < samples ? ReadSample(buffer, start + (i + 1) * 2) : a;
            double sample = a + (b - a) * frac;
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
            buffer[start + produced * 2] = (byte)(shaped & 0xFF);
            buffer[start + produced * 2 + 1] = (byte)((shaped >> 8) & 0xFF);
            produced++;
            src += _step;
        }
        _carry = src - samples;
        return produced * 2;
    }

    private static short ReadSample(byte[] buffer, int at) =>
        (short)(buffer[at] | (buffer[at + 1] << 8));
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
        return _filter.Apply(buffer, offset, read, WaveFormat.SampleRate);
    }
}
