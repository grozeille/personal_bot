using System.Net.WebSockets;
using System.Text;
using NAudio.Wave;

namespace TabletApp.AI;

/// <summary>WebSocket réel (un seul envoi à la fois, messages réassemblés).</summary>
internal sealed class LiveClientWebSocket : ILiveSocket
{
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    public string? LastError { get; private set; }

    public string? LastCloseStatus { get; private set; }

    public async Task ConnectAsync(string url, string apiKey, CancellationToken ct)
    {
        try
        {
            _socket.Options.SetRequestHeader("x-goog-api-key", apiKey);
        }
        catch
        {
            // En-tête refusée : le query param suffit.
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            await _socket.ConnectAsync(
                new Uri($"{url}?key={apiKey}"), timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            throw;
        }
    }

    public async Task SendAsync(string json, CancellationToken ct)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(
                bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<string?> ReceiveAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            try
            {
                result = await _socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            }
            catch (WebSocketException ex)
            {
                LastError = ex.Message;
                return null;
            }
            if (result.MessageType == WebSocketMessageType.Close)
            {
                LastCloseStatus = $"{_socket.CloseStatus}: {_socket.CloseStatusDescription}";
                return null;
            }
            message.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(message.ToArray());
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        if (_socket.State == WebSocketState.Open)
        {
            try
            {
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "bye", ct).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _socket.Dispose();
        _sendLock.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>Micro temps réel (16 kHz mono 16 bits, ~100 ms par morceau).</summary>
internal sealed class LiveMic : ILiveMic
{
    private WaveInEvent? _mic;
    private bool _disposed;

    public event Action<byte[]>? Chunk;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_mic is not null)
        {
            return;
        }
        var mic = new WaveInEvent
        {
            DeviceNumber = 0,
            WaveFormat = new WaveFormat(16000, 16, 1),
        };
        mic.DataAvailable += (_, e) =>
        {
            byte[] copy = new byte[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, copy, 0, e.BytesRecorded);
            Chunk?.Invoke(copy);
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
    }
}

/// <summary>Haut-parleur streaming (PCM 24 kHz, vidable sur interruption).</summary>
internal sealed class LivePlayer : ILivePlayer
{
    private readonly WaveOutEvent _output = new() { DesiredLatency = 150 };
    private readonly RobotVoiceFilter _robot = new();
    private readonly BufferedWaveProvider _buffer;
    private bool _disposed;
    private int _peakBuffered;
    private int _dropped;

    public LivePlayer()
    {
        // 30 s : l'audio serveur arrive par rafales ; les 5 s par défaut tronquaient les phrases longues.
        _buffer = new BufferedWaveProvider(new WaveFormat(24000, 16, 1))
        {
            BufferDuration = TimeSpan.FromSeconds(30),
            DiscardOnBufferOverflow = true,
        };
        _output.Init(_buffer);
    }

    public void PlayChunk(byte[] pcm24k)
    {
        int count = pcm24k.Length;
        if (RobotVoiceFilter.Enabled)
        {
            count = _robot.Apply(pcm24k, 0, pcm24k.Length, 24000);
        }
        AddToBuffer(pcm24k, count);
    }

    /// <summary>Effets sonores : jamais filtrés (sons finis, pas de la voix).</summary>
    public void PlayRaw(byte[] pcm24k) => AddToBuffer(pcm24k, pcm24k.Length);

    private void AddToBuffer(byte[] pcm, int count)
    {
        if (count <= 0)
        {
            return;
        }
        if (_buffer.BufferedBytes + count > _buffer.BufferLength)
        {
            _dropped++;
        }
        _buffer.AddSamples(pcm, 0, count);
        if (_buffer.BufferedBytes > _peakBuffered)
        {
            _peakBuffered = _buffer.BufferedBytes;
        }
    }

    public void Clear() => _buffer.ClearBuffer();

    public bool IsDrained => _buffer.BufferedBytes == 0;

    public PlayerTurnStats TakeTurnStats()
    {
        var stats = new PlayerTurnStats(_buffer.BufferedBytes, _peakBuffered, _dropped);
        _peakBuffered = _buffer.BufferedBytes;
        _dropped = 0;
        return stats;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_output.PlaybackState != PlaybackState.Playing)
        {
            _output.Play();
        }
    }

    public void Stop()
    {
        if (_output.PlaybackState != PlaybackState.Stopped)
        {
            _output.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Stop();
        _output.Dispose();
    }
}
