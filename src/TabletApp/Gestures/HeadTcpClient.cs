using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TabletApp.AI;

namespace TabletApp.Gestures;

/// <summary>
/// Envoi TCP (JSON + \n) vers le simu Godot ou le futur serveur Pi.
/// Une connexion par geste : simple, sans etat, robuste aux redemarrages.
/// </summary>
internal sealed class HeadTcpClient(string host, int port)
{
    private readonly string _host = host;
    private readonly int _port = port;

    public async Task<string> SendMovesAsync(string movesJson, CancellationToken ct = default)
    {
        // Minifie (gestures.json est indente pour les humains ; le protocole
        // exige une ligne = un message, sans \n internes).
        using JsonDocument doc = JsonDocument.Parse(movesJson);
        string compact = JsonSerializer.Serialize(doc.RootElement);
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            VoiceLog.Error($"HEAD connexion {_host}:{_port} impossible ({ex.GetType().Name}) — simu/serveur lance ?");
            throw;
        }
        await using var stream = client.GetStream();
        byte[] payload = Encoding.UTF8.GetBytes("{\"moves\":" + compact + "}\n");
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        string? reply = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        VoiceLog.Info($"HEAD -> {_host}:{_port} : {reply}");
        return reply ?? "";
    }

    public static string Host =>
        ResolveHost();

    public static int Port =>
        ResolvePort();

    private static string ResolveHost()
    {
        string? env = Environment.GetEnvironmentVariable("HEAD_SIM_HOST");
        return string.IsNullOrEmpty(env) ? MotionOptions.Load().TargetHost : env;
    }

    private static int ResolvePort() =>
        int.TryParse(Environment.GetEnvironmentVariable("HEAD_SIM_PORT"), out int port)
            ? port
            : MotionOptions.Load().Port;
}
