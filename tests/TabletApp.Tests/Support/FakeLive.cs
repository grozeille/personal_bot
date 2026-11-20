using TabletApp.AI;

namespace TabletApp.Tests.Support;

/// <summary>Transport live simulé : messages serveur scriptés, envois journalisés.</summary>
internal sealed class FakeLiveSocket : ILiveSocket
{
    private readonly Queue<string?> _incoming = new();
    private readonly SemaphoreSlim _arrived = new(0, int.MaxValue);

    public List<string> Sent { get; } = new();

    public bool Connected { get; private set; }

    public bool Closed { get; private set; }

    public bool FailConnect { get; set; }

    public string? LastError => null;

    public string? LastCloseStatus => null;

    public void Script(params string?[] messages)
    {
        foreach (string? message in messages)
        {
            _incoming.Enqueue(message);
            _arrived.Release();
        }
    }

    public Task ConnectAsync(string url, string apiKey, CancellationToken ct)
    {
        if (FailConnect)
        {
            throw new InvalidOperationException("Pas de réseau.");
        }
        Connected = true;
        return Task.CompletedTask;
    }

    public Task SendAsync(string json, CancellationToken ct)
    {
        Sent.Add(json);
        return Task.CompletedTask;
    }

    public async Task<string?> ReceiveAsync(CancellationToken ct)
    {
        // Boucle : un message scripté en cours de run réveille l'attente
        // (jetons périmés possibles après un script initial : on re-vérifie).
        while (true)
        {
            if (_incoming.Count > 0)
            {
                return _incoming.Dequeue();
            }
            await _arrived.WaitAsync(ct);
        }
    }

    public Task CloseAsync(CancellationToken ct)
    {
        Closed = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Micro simulé : morceaux poussés à la main par le test.</summary>
internal sealed class FakeLiveMic : ILiveMic
{
    public event Action<byte[]>? Chunk;

    public void Push(byte[] pcm) => Chunk?.Invoke(pcm);

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>Haut-parleur simulé : octets et vidages journalisés.</summary>
internal sealed class FakeLivePlayer : ILivePlayer
{
    public List<byte> Played { get; } = new();

    public int Clears { get; private set; }

    public void PlayChunk(byte[] pcm24k) => Played.AddRange(pcm24k);

    public void Clear() => Clears++;

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
