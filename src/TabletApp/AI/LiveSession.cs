using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace TabletApp.AI;

/// <summary>Socket WebSocket injectable (réel ou fake en tests).</summary>
internal interface ILiveSocket : IAsyncDisposable
{
    string? LastError { get; }
    string? LastCloseStatus { get; }
    Task ConnectAsync(string url, string apiKey, CancellationToken ct);
    Task SendAsync(string json, CancellationToken ct);
    Task<string?> ReceiveAsync(CancellationToken ct);
    Task CloseAsync(CancellationToken ct);
}

/// <summary>Micro streaming injectable (16 kHz mono 16 bits).</summary>
internal interface ILiveMic : IDisposable
{
    event Action<byte[]>? Chunk;
    void Start();
    void Stop();
}

/// <summary>Haut-parleur streaming injectable (PCM 24 kHz).</summary>
internal interface ILivePlayer : IDisposable
{
    void PlayChunk(byte[] pcm24k);
    void Clear();
    void Start();
    void Stop();
}

/// <summary>Événements de session vers l'app (sur threads de fond !).</summary>
internal sealed record LiveCallbacks(
    Action<string> OnEmotion,
    Action<string> OnSound,
    Action<string> OnGesture,
    Action<string> OnHeardTranscript,
    Action<string> OnRobotTranscript,
    Action<bool> OnSpeaking,
    Action<TabletApp.UI.VoiceGlow> OnGlow,
    Action OnUserSpeech,
    Action<string> OnLog,
    Action? OnCalibrationRequested = null);

/// <summary>
/// Session vocale Live : micro streamé, audio modèle joué, tools exécutés,
/// interruptions gérées, fin sur silence local prolongé. Transport injecté.
/// </summary>
internal sealed class LiveSession
{
    internal const string GreetingPrompt =
        "L'enfant vient d'arriver. Salue-le en un seul mot : Bonjour.";

    private readonly string _apiKey;
    private readonly string _systemPrompt;
    private readonly IReadOnlyList<string> _emotions;
    private readonly IReadOnlyList<string> _sounds;
    private readonly IReadOnlyList<string> _gestures;
    private readonly ILiveSocket _socket;
    private readonly ILiveMic _mic;
    private readonly ILivePlayer _player;
    private readonly LiveCallbacks _events;
    private readonly TimeSpan _idleTimeout;
    private readonly TimeSpan _turnTimeout;
    private readonly TimeSpan _maxTurnTimeout;

    private readonly object _sync = new();
    private DateTime _lastSpeech = DateTime.UtcNow;
    private DateTime _turnStartedAt = DateTime.UtcNow;
    private bool _wasSpeech;
    private DateTime _lastModelActivity = DateTime.MinValue;
    private bool _turnCompleteSent;
    private bool _heardSpeech;
    private volatile bool _speaking;
    private DateTime _speakEndedAt = DateTime.MinValue;
    private volatile bool _mutedTurn;
    private volatile bool _dropAudio;
    private int _speechChunks;
    private int _turnAudioChunks;
    private double _turnPeakRms;
    private CancellationTokenSource? _stop;

    public LiveSession(
        string apiKey, string systemPrompt, IReadOnlyList<string> emotions, IReadOnlyList<string> sounds, IReadOnlyList<string> gestures,
        ILiveSocket socket, ILiveMic mic, ILivePlayer player,
        LiveCallbacks events, TimeSpan? idleTimeout = null, TimeSpan? turnTimeout = null,
        TimeSpan? maxTurnTimeout = null)
    {
        _apiKey = apiKey;
        _systemPrompt = systemPrompt;
        _emotions = emotions;
        _sounds = sounds;
        _gestures = gestures;
        _socket = socket;
        _mic = mic;
        _player = player;
        _events = events;
        _idleTimeout = idleTimeout ?? TimeSpan.FromSeconds(60);
        _turnTimeout = turnTimeout ?? TimeSpan.FromMilliseconds(1200);
        _maxTurnTimeout = maxTurnTimeout ?? TimeSpan.FromSeconds(12);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _stop = stop;
        var setupWatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await _socket.ConnectAsync(
                LiveProtocol.Endpoint, _apiKey, stop.Token).ConfigureAwait(false);
            string setupJson = LiveProtocol.BuildSetup(_systemPrompt, _emotions, _sounds, _gestures);
            await _socket.SendAsync(setupJson, stop.Token).ConfigureAwait(false);
            await WaitForSetupAsync(setupJson, stop.Token, ct).ConfigureAwait(false);
            _events.OnLog($"Live : session prête en {setupWatch.Elapsed.TotalSeconds:F1} s.");
            _events.OnGlow(TabletApp.UI.VoiceGlow.Listening);
            await _socket.SendAsync(
                LiveProtocol.BuildClientTurn(GreetingPrompt), stop.Token).ConfigureAwait(false);
            _mic.Chunk += OnMicChunk;
            _mic.Start();
            _player.Start();
            try
            {
                await ReceiveLoopAsync(stop).ConfigureAwait(false);
            }
            finally
            {
                _mic.Stop();
            }
        }
        finally
        {
            _events.OnSpeaking(false);
            _events.OnGlow(TabletApp.UI.VoiceGlow.None);
            try
            {
                await _socket.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    public void RequestStop()
    {
        try
        {
            _stop?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// Tap : coupe la réponse en cours et revient à l'écoute, sans couper
    /// la session. La fin du tour serveur est ignorée ; le prochain tour
    /// utilisateur réarme normalement.
    /// </summary>
    public void InterruptToListening()
    {
        _player.Clear();
        lock (_sync)
        {
            _speaking = false;
            _speakEndedAt = DateTime.MinValue;
            _dropAudio = true;
            _lastModelActivity = DateTime.UtcNow;
            _turnCompleteSent = false;
            _speechChunks = 0;
        }
        _events.OnSpeaking(false);
        _events.OnGlow(TabletApp.UI.VoiceGlow.Listening);
        _events.OnLog("Live : réponse coupée, retour à l'écoute.");
    }

    private async Task WaitForSetupAsync(string setupJson, CancellationToken linked, CancellationToken user)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(linked);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            while (true)
            {
                string? message = await _socket.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                if (message is null)
                {
                    _events.OnLog($"Live : setup rejeté ({_socket.LastError ?? _socket.LastCloseStatus ?? "sans détail"}).");
                    _events.OnLog("Live : setup envoyé : " + setupJson);
                    throw new InvalidOperationException("Live fermé pendant le setup.");
                }
                if (LiveProtocol.ParseUpdate(message).SetupComplete)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (!user.IsCancellationRequested)
        {
            throw new TimeoutException("Live : setup sans réponse (20 s).");
        }
    }

    private async Task ReceiveLoopAsync(CancellationTokenSource stop)
    {
        var robotText = new StringBuilder();
        while (!stop.IsCancellationRequested)
        {
            string? message;
            try
            {
                message = await _socket.ReceiveAsync(stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            if (message is null)
            {
                break;
            }
            LiveServerUpdate update;
            try
            {
                update = LiveProtocol.ParseUpdate(message);
            }
            catch (JsonException)
            {
                _events.OnLog("Live : message illisible ignoré.");
                continue;
            }
            if (update.Audio is { Length: > 0 } pcm)
            {
                double lag;
                lock (_sync)
                {
                    _lastModelActivity = DateTime.UtcNow;
                    _turnCompleteSent = false;
                    _speechChunks = 0;                    lag = (DateTime.UtcNow - _lastSpeech).TotalSeconds;
                }
                if (!_speaking)
                {
                    _speaking = true;
                    _events.OnSpeaking(true);
                    _events.OnGlow(TabletApp.UI.VoiceGlow.Thinking);
                    _events.OnLog($"Live : réponse vocale après {lag:F1} s.");
                }
                if (!_mutedTurn && !_dropAudio)
                {
                    _player.PlayChunk(pcm);
                }
            }
            if (update.InputTranscript is { } heard)
            {
                _events.OnHeardTranscript(heard);
            }
            if (update.OutputTranscript is { } said)
            {
                lock (_sync)
                {
                    _lastModelActivity = DateTime.UtcNow;
                    _turnCompleteSent = false;
                    _speechChunks = 0;                }
                if (!_mutedTurn && !_dropAudio)
                {
                    robotText.Append(said);
                }
            }
            if (update.ToolCalls.Count > 0)
            {
                lock (_sync)
                {
                    _lastModelActivity = DateTime.UtcNow;
                    _turnCompleteSent = false;
                    _speechChunks = 0;                }
            }
            bool endByTool = false;
            bool calibrateByTool = false;
            foreach (LiveToolCall call in update.ToolCalls)
            {
                if (call.Name == "end_conversation")
                {
                    _events.OnLog("Live : fin de discussion demandée.");
                    endByTool = true;
                }
                if (call.Name == "run_calibration")
                {
                    _events.OnLog("Live : calibration demandée.");
                    calibrateByTool = true;
                }
                if (call.Name == "set_emotion" && call.Emotion is { } feeling)
                {
                    _events.OnEmotion(feeling);
                }
                if (call.Name == "play_sound" && call.Sound is { } sfx)
                {
                    _events.OnSound(sfx);
                }
                if (call.Name == "play_fart")
                {
                    _events.OnSound("prout");
                    _mutedTurn = true;
                }
                if (call.Name == "play_gesture" && call.Gesture is { } g)
                {
                    _events.OnGesture(g);
                }
                await _socket.SendAsync(
                    LiveProtocol.BuildToolResponse(new[] { (call.Id, call.Name) }),
                    stop.Token).ConfigureAwait(false);
            }
            if (endByTool)
            {
                // Après l'acquittement : le "Bye !" déjà en file se joue.
                RequestStop();
            }
            if (calibrateByTool)
            {
                // Fin de session puis enrôlement (micro partagé : jamais en parallèle).
                _events.OnCalibrationRequested?.Invoke();
                RequestStop();
            }
            if (update.CancelledIds.Count > 0)
            {
                _events.OnLog($"Live : {update.CancelledIds.Count} appel(s) annulé(s).");
            }
            if (update.Interrupted)
            {
                _player.Clear();
                lock (_sync)
                {
                    _speakEndedAt = DateTime.UtcNow;
                    _mutedTurn = false;
                    _dropAudio = false;
                }
                _speaking = false;
                _events.OnSpeaking(false);
                _events.OnGlow(TabletApp.UI.VoiceGlow.Listening);
                FlushRobotText(robotText, coupé: true);
            }
            if (update.TurnComplete)
            {
                bool dropped;
                lock (_sync)
                {
                    _speakEndedAt = DateTime.UtcNow;
                    _mutedTurn = false;
                    dropped = _dropAudio;
                    _dropAudio = false;
                }
                _speaking = false;
                _events.OnSpeaking(false);
                _events.OnGlow(TabletApp.UI.VoiceGlow.Listening);
                if (dropped)
                {
                    robotText.Clear();
                }
                else
                {
                    FlushRobotText(robotText, coupé: false);
                }
            }
            if (update.GoAway)
            {
                _events.OnLog("Live : serveur partant, fin de session.");
                break;
            }
        }

        void FlushRobotText(StringBuilder sb, bool coupé)
        {
            if (sb.Length > 0)
            {
                _events.OnRobotTranscript(sb.ToString() + (coupé ? " (coupé)" : ""));
                sb.Clear();
            }
        }
    }

    // Semi-duplex strict : le micro est sourd pendant la parole du robot,
    // plus une queue couvrant la fin du buffer audio (pas d'auto-écoute).
    internal static readonly TimeSpan SpeakMuteTail = TimeSpan.FromMilliseconds(400);
    internal const int MinSpeechChunks = 4;

    internal static bool ShouldMuteMic(bool speaking, DateTime speakEndedAt, DateTime now, TimeSpan tail)
    {
        return speaking || now - speakEndedAt < tail;
    }

    internal static bool ShouldSendTurnComplete(
        bool sent, bool heardSpeech, DateTime lastSpeech, DateTime lastModel,
        DateTime now, TimeSpan silence, int speechChunks, int minSpeechChunks,
        DateTime turnStarted, TimeSpan maxTurn)
    {
        if (sent || !heardSpeech || speechChunks < minSpeechChunks)
        {
            return false;
        }
        return (lastSpeech > lastModel && now - lastSpeech >= silence)
            || now - turnStarted >= maxTurn;
    }

    /// <summary>Couture de diagnostic : injecte un chunk micro (outil --live-sim).</summary>
    internal void FeedTestChunk(byte[] pcm16k) => OnMicChunk(pcm16k);

    /// <summary>Couture de diagnostic : simule une réponse modèle (réarme les tours).</summary>
    internal void SimulateModelActivity()
    {
        lock (_sync)
        {
            _lastModelActivity = DateTime.UtcNow;
            _turnCompleteSent = false;
            _speechChunks = 0;
        }
    }

    /// <summary>Compteur d'audio envoyé pour le diagnostic par tour (sous verrou).</summary>
    private void NoteSent(byte[] chunk, double? rms = null)
    {
        _turnAudioChunks++;
        double level = rms ?? NAudioVoice.ComputeRms(chunk, chunk.Length);
        if (level > _turnPeakRms)
        {
            _turnPeakRms = level;
        }
    }

    private void OnMicChunk(byte[] pcm16k)
    {
        double rms = NAudioVoice.ComputeRms(pcm16k, pcm16k.Length);
        List<byte[]> toSend = new();
        bool endTurn = false;
        bool endSession = false;
        lock (_sync)
        {
            DateTime now = DateTime.UtcNow;
            bool muted = ShouldMuteMic(_speaking, _speakEndedAt, now, SpeakMuteTail);
            bool speech = !muted && rms >= NAudioVoice.SpeechRmsThreshold;
            // Flux continu exigé par le serveur : ne jamais affamer le stream
            // (sinon les tours suivant le premier reviennent vides). Sourdine
            // = silence numérique (pas d'écho), jamais un trou.
            if (muted)
            {
                _wasSpeech = false;
                toSend.Add(new byte[pcm16k.Length]);
            }
            else
            {
                if (speech)
                {
                    if (_speechChunks == 0)
                    {
                        _turnStartedAt = now;
                    }
                    _lastSpeech = now;
                    _heardSpeech = true;
                    _speechChunks++;
                    if (!_wasSpeech)
                    {
                        _wasSpeech = true;
                        _events.OnUserSpeech();
                    }
                }
                else
                {
                    _wasSpeech = false;
                }
                toSend.Add(pcm16k);
                NoteSent(pcm16k, rms);
            }
            if (!muted && now - _lastSpeech > _idleTimeout)
            {
                endSession = true;
            }
            else if (!muted && ShouldSendTurnComplete(
                _turnCompleteSent, _heardSpeech, _lastSpeech, _lastModelActivity,
                now, _turnTimeout, _speechChunks, MinSpeechChunks,
                _turnStartedAt, _maxTurnTimeout))
            {
                _turnCompleteSent = true;
                _speechChunks = 0;
                _dropAudio = false;
                endTurn = true;
            }
        }
        foreach (byte[] chunk in toSend)
        {
            _ = SendAsync(LiveProtocol.BuildAudioChunk(chunk));
        }
        if (endTurn)
        {
            _events.OnLog("Live : fin de phrase détectée (client).");
            _events.OnLog($"Live : tour envoyé : {_turnAudioChunks} chunks, RMS max {_turnPeakRms:F0}.");
            _turnAudioChunks = 0;
            _turnPeakRms = 0;
            _ = SendAsync(LiveProtocol.BuildTurnComplete());
        }
        if (endSession)
        {
            _events.OnLog("Live : silence prolongé, fin de session.");
            RequestStop();
        }
    }

    private async Task SendAsync(string json)
    {
        try
        {
            await _socket.SendAsync(json, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Envoi en tâche de fond : l'échec remontera via la boucle.
        }
    }
}
