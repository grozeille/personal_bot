using NAudio.Wave;
using SherpaOnnx;

namespace TabletApp.AI;

/// <summary>Détecteur de mot-clé (sherpa réel ou fake en tests).</summary>
internal interface IWakeDetector : IDisposable
{
    void Feed(float[] samples);
    bool TakeDetected();
    void Reset();
}

/// <summary>État d'un tampon micro pour le diagnostic (sonde --wake-probe).</summary>
internal sealed record WakeTick(double Rms, bool GateOpen, bool Feeding, bool FeedingStarted, bool FeedingStopped, bool Detected);

/// <summary>
/// Détecteur sherpa-onnx (zipformer KWS zh-en, words "HEY_PATATE_*").
/// Le stream est réinitialisé après chaque détection.
/// </summary>
internal sealed class SherpaWakeDetector : IWakeDetector
{
    internal const string KeywordsFileName = "keywords-patate.txt";

    private const string EncoderFile = "encoder-epoch-13-avg-2-chunk-8-left-64.int8.onnx";
    private const string DecoderFile = "decoder-epoch-13-avg-2-chunk-8-left-64.onnx";
    private const string JoinerFile = "joiner-epoch-13-avg-2-chunk-8-left-64.int8.onnx";
    private const string TokensFile = "tokens.txt";

    private readonly KeywordSpotter _spotter;
    private readonly OnlineStream _stream;
    private bool _disposed;

    /// <summary>Dernière variante reconnue (mise au point).</summary>
    internal string LastKeyword { get; private set; } = "";

    /// <summary>Dossier modèle embarqué (installé par Velopack).</summary>
    internal static string BundledModelDir() =>
        Path.Combine(AppContext.BaseDirectory, "Models", "kws");

    /// <summary>Dossier modèle local (réglages perso, prioritaire si complet).</summary>
    internal static string OverrideModelDir() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TabletApp", "models", "sherpa-onnx-kws-zipformer-zh-en-3M-2025-12-20");

    internal static string ResolveModelDir()
    {
        string local = OverrideModelDir();
        if (File.Exists(Path.Combine(local, EncoderFile))
            && File.Exists(Path.Combine(local, KeywordsFileName)))
        {
            return local;
        }
        return BundledModelDir();
    }

    /// <summary>Construit si les fichiers sont présents et valides, sinon false.</summary>
    internal static bool TryCreate(float threshold, float score, out SherpaWakeDetector? detector)
    {
        detector = null;
        try
        {
            string modelDir = ResolveModelDir();
            if (!File.Exists(Path.Combine(modelDir, EncoderFile))
                || !File.Exists(Path.Combine(modelDir, KeywordsFileName)))
            {
                return false;
            }
            detector = new SherpaWakeDetector(modelDir, threshold, score);
            return true;
        }
        catch
        {
            detector?.Dispose();
            detector = null;
            return false;
        }
    }

    private SherpaWakeDetector(string modelDir, float threshold, float score)
    {
        var config = new KeywordSpotterConfig();
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Transducer.Encoder = Path.Combine(modelDir, EncoderFile);
        config.ModelConfig.Transducer.Decoder = Path.Combine(modelDir, DecoderFile);
        config.ModelConfig.Transducer.Joiner = Path.Combine(modelDir, JoinerFile);
        config.ModelConfig.Tokens = Path.Combine(modelDir, TokensFile);
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.NumThreads = 1;
        config.ModelConfig.Debug = 0;
        config.KeywordsFile = Path.Combine(modelDir, KeywordsFileName);
        config.KeywordsThreshold = threshold;
        config.KeywordsScore = score;
        _spotter = new KeywordSpotter(config);
        _stream = _spotter.CreateStream();
    }

    public void Feed(float[] samples) => _stream.AcceptWaveform(16000, samples);

    public bool TakeDetected()
    {
        // Le fichier ne contient que nos variantes : tout hit réveille.
        while (_spotter.IsReady(_stream))
        {
            _spotter.Decode(_stream);
            string keyword = _spotter.GetResult(_stream).Keyword;
            if (keyword.Length > 0)
            {
                LastKeyword = keyword;
                _spotter.Reset(_stream);
                return true;
            }
        }
        return false;
    }

    public void Reset() => _spotter.Reset(_stream);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _stream.Dispose();
        _spotter.Dispose();
    }
}

/// <summary>
/// Écoute always-on du micro par défaut : alimente le détecteur et lève
/// <c>Detected</c> (sur thread audio !) à chaque "Hey Patate", avec 3 s
/// d'anti-rebond. Le micro n'est ouvert qu'entre Start et Stop.
/// </summary>
internal sealed class WakeWordListener : IDisposable
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    // Seuil d'alimentation (150/32768, bien au-dessus du plancher ~0,0002).
    private readonly double _gate;
    private const int PreRollBuffers = 3;

    private readonly IWakeDetector _detector;
    private readonly int _device;
    private readonly TimeSpan _quietHangover;
    private readonly object _sync = new();
    private readonly Queue<float[]> _preRoll = new();
    private WaveInEvent? _mic;
    private DateTime _lastFire = DateTime.MinValue;
    private DateTime? _quietSince;
    private bool _feeding;
    private bool _disposed;

    public event Action? Detected;

    public event Action<WakeTick>? Tick;
    internal double GateRms => _gate * 32768.0;
    public WakeWordListener(
        IWakeDetector detector, int deviceNumber = 0, TimeSpan? quietHangover = null, double gateRms = 150.0)
    {
        _detector = detector;

        _gate = gateRms / 32768.0;        _device = deviceNumber;
        _quietHangover = quietHangover ?? TimeSpan.FromSeconds(2);
    }

    public void Start()
    {
        if (_disposed || _mic is not null)
        {
            return;
        }
        var mic = new WaveInEvent
        {
            DeviceNumber = _device,
            WaveFormat = new WaveFormat(16000, 16, 1),
        };
        mic.DataAvailable += (_, e) =>
        {
            int samples = e.BytesRecorded / 2;
            float[] buffer = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                buffer[i] = (short)(e.Buffer[i * 2] | (e.Buffer[i * 2 + 1] << 8)) / 32768f;
            }
            OnAudio(buffer);
        };
        _mic = mic;
        mic.StartRecording();
    }

    public void Stop()
    {
        _mic?.StopRecording();
        _mic?.Dispose();
        _mic = null;
    }

    internal void OnAudio(float[] samples)
    {
        // VAD : on n'alimente le décodeur que sur parole (+300 ms avant),
        // et on le réinitialise après un silence (état borné, pas de dérive).
        double sum = 0;
        foreach (float sample in samples)
        {
            sum += sample * sample;
        }
        double rms = samples.Length == 0 ? 0 : Math.Sqrt(sum / samples.Length);
        bool fire = false;

        bool started = false;
        bool stopped = false;
        bool gateOpen = rms >= _gate;
        bool feedingAfter = false;        lock (_sync)
        {
            if (gateOpen)
            {
                _quietSince = null;
                if (!_feeding)
                {
                    _feeding = true;

                    started = true;                    foreach (float[] old in _preRoll)
                    {
                        _detector.Feed(old);
                    }
                }
                _detector.Feed(samples);
            }
            else
            {
                _quietSince ??= DateTime.UtcNow;
                if (_feeding && DateTime.UtcNow - _quietSince > _quietHangover)
                {
                    _feeding = false;

                    stopped = true;                    _preRoll.Clear();
                    _detector.Reset();
                }
                else if (!_feeding)
                {
                    _preRoll.Enqueue((float[])samples.Clone());
                    while (_preRoll.Count > PreRollBuffers)
                    {
                        _preRoll.Dequeue();
                    }
                }
            }
            if (_detector.TakeDetected() && DateTime.UtcNow - _lastFire > Cooldown)
            {
                _lastFire = DateTime.UtcNow;
                fire = true;
            }
        feedingAfter = _feeding;
        }
        if (fire)
        {
            Detected?.Invoke();

        if (Tick is not null)
        {
            Tick(new WakeTick(rms, gateOpen, feedingAfter, started, stopped, fire));
        }        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
        _detector.Dispose();
    }
}
