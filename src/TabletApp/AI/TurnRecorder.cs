using System.Text;
using System.Text.Json;

namespace TabletApp.AI;

/// <summary>Options debug (section "Debug" de appsettings.json + flag --record-debug).</summary>
internal sealed record DebugOptions(bool RecordAudio)
{
    public static DebugOptions Default { get; } = new(false);

    public static DebugOptions Load(string? configPath = null)
    {
        try
        {
            string path = configPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return Default;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Debug", out JsonElement debug))
            {
                return Default;
            }
            bool record = debug.TryGetProperty("RecordAudio", out JsonElement r)
                && r.ValueKind == JsonValueKind.True;
            return new DebugOptions(record);
        }
        catch
        {
            return Default;
        }
    }
}

/// <summary>Enregistreur de tours injectable (réel ou nul en temps normal).</summary>
internal interface ITurnRecorder
{
    void AppendMic(byte[] pcm16k, double rms, bool speech, bool muted);
    void AppendRobot(byte[] pcm24k);
    void NoteHeard(string text);
    void NoteSaid(string text);
    void EndTurn(int sentChunks, double sentPeakRms);
    void Finish();
}

/// <summary>
/// Enregistreur debug des discussions Live : par tour, le micro entendu
/// (16 kHz mono), la réplique du robot (24 kHz mono) et un sidecar JSON
/// (stats, transcriptions). Tampon tournant : purge par âge puis par
/// taille. Local uniquement, OFF par défaut (vie privée).
/// </summary>
internal sealed class TurnRecorder : ITurnRecorder, IDisposable
{
    public static string DefaultDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TabletApp", "debug-audio");

    private readonly string _dir;
    private readonly TimeSpan _maxAge;
    private readonly long _maxBytes;
    private readonly string _session;
    private readonly object _sync = new();
    private readonly MemoryStream _mic = new();
    private readonly MemoryStream _robot = new();
    private readonly StringBuilder _heard = new();
    private readonly StringBuilder _said = new();
    private int _turn;
    private int _micChunks;
    private int _micSpeechChunks;
    private int _micMutedChunks;
    private double _micPeak;
    private int _robotChunks;
    private bool _disposed;

    public TurnRecorder(string? dir = null, TimeSpan? maxAge = null, long? maxBytes = null)
    {
        _dir = dir ?? DefaultDir;
        _maxAge = maxAge ?? TimeSpan.FromHours(48);
        _maxBytes = maxBytes ?? 200L * 1024 * 1024;
        _session = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(_dir);
        Purge();
    }

    public void AppendMic(byte[] pcm16k, double rms, bool speech, bool muted)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _mic.Write(pcm16k, 0, pcm16k.Length);
            _micChunks++;
            if (rms > _micPeak)
            {
                _micPeak = rms;
            }
            if (speech)
            {
                _micSpeechChunks++;
            }
            if (muted)
            {
                _micMutedChunks++;
            }
        }
    }

    public void AppendRobot(byte[] pcm24k)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _robot.Write(pcm24k, 0, pcm24k.Length);
            _robotChunks++;
        }
    }

    public void NoteHeard(string text)
    {
        lock (_sync)
        {
            if (_heard.Length > 0)
            {
                _heard.Append(' ');
            }
            _heard.Append(text);
        }
    }

    public void NoteSaid(string text)
    {
        lock (_sync)
        {
            if (_said.Length > 0)
            {
                _said.Append(' ');
            }
            _said.Append(text);
        }
    }

    public void EndTurn(int sentChunks, double sentPeakRms)
    {
        lock (_sync)
        {
            WriteTurn(sentChunks, sentPeakRms, partial: false);
        }
    }

    /// <summary>Clôt le tour partiel de fin de session (sans jeter).</summary>
    public void Finish()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            if (_mic.Length > 0 || _robot.Length > 0)
            {
                WriteTurn(-1, _micPeak, partial: true);
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_mic.Length > 0 || _robot.Length > 0)
            {
                WriteTurn(-1, _micPeak, partial: true);
            }
            _mic.Dispose();
            _robot.Dispose();
        }
    }

    private void WriteTurn(int sentChunks, double sentPeakRms, bool partial)
    {
        _turn++;
        string stem = Path.Combine(_dir, $"turn-{_session}-{_turn:000}");
        WriteWav(stem + "-mic.wav", _mic.ToArray(), 16000);
        WriteWav(stem + "-robot.wav", _robot.ToArray(), 24000);
        var meta = new
        {
            turn = _turn,
            at = DateTime.Now.ToString("s"),
            partial,
            micChunks = _micChunks,
            micSpeechChunks = _micSpeechChunks,
            micMutedChunks = _micMutedChunks,
            micPeakRms = Math.Round(_micPeak, 1),
            sentChunks,
            sentPeakRms = Math.Round(sentPeakRms, 1),
            robotChunks = _robotChunks,
            heard = _heard.ToString(),
            said = _said.ToString(),
        };
        File.WriteAllText(stem + ".json", JsonSerializer.Serialize(meta));
        _mic.SetLength(0);
        _robot.SetLength(0);
        _heard.Clear();
        _said.Clear();
        _micChunks = 0;
        _micSpeechChunks = 0;
        _micMutedChunks = 0;
        _micPeak = 0;
        _robotChunks = 0;
    }

    /// <summary>En-tête RIFF minimal + PCM 16 bits mono (déterministe, testé).</summary>
    internal static void WriteWav(string path, byte[] pcm16, int sampleRate)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcm16.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcm16.Length);
        writer.Write(pcm16);
    }

    /// <summary>Purge par tour (jamais de fichiers orphelins) : âge puis taille.</summary>
    private void Purge()
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(_dir, "turn-*");
        }
        catch
        {
            return;
        }
        DateTime now = DateTime.Now;
        var groups = new Dictionary<string, List<(string Path, DateTime Time, long Size)>>();
        foreach (string file in files)
        {
            try
            {
                var info = new FileInfo(file);
                string stem = StemOf(info.Name);
                if (!groups.TryGetValue(stem, out var group))
                {
                    group = new List<(string, DateTime, long)>();
                    groups[stem] = group;
                }
                group.Add((file, info.LastWriteTime, info.Length));
            }
            catch
            {
                // Fichier verrouillé ou parti : on passe au suivant.
            }
        }
        var survivors = new List<(DateTime Time, long Size, List<string> Paths)>();
        foreach (var group in groups.Values)
        {
            DateTime oldest = group.Min(f => f.Time);
            if (now - oldest > _maxAge)
            {
                DeleteGroup(group.Select(f => f.Path));
            }
            else
            {
                survivors.Add((oldest, group.Sum(f => f.Size), group.Select(f => f.Path).ToList()));
            }
        }
        long total = survivors.Sum(s => s.Size);
        foreach (var victim in survivors.OrderBy(s => s.Time))
        {
            if (total <= _maxBytes)
            {
                break;
            }
            DeleteGroup(victim.Paths);
            total -= victim.Size;
        }
    }

    private static string StemOf(string name)
    {
        foreach (string suffix in new[] { "-mic.wav", "-robot.wav", ".json" })
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return name.Substring(0, name.Length - suffix.Length);
            }
        }
        return name;
    }

    private static void DeleteGroup(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
