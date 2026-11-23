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

    /// <summary>Pose directe (annule la piste en cours, tenue jusqu'au prochain ordre).</summary>
    public async Task<string> SendPoseAsync(double? yaw, double? pitch, double? roll, CancellationToken ct = default)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            if (yaw is not null)
            {
                writer.WriteNumber("yaw", yaw.Value);
            }
            if (pitch is not null)
            {
                writer.WriteNumber("pitch", pitch.Value);
            }
            if (roll is not null)
            {
                writer.WriteNumber("roll", roll.Value);
            }
            writer.WriteEndObject();
        }
        string payload = Encoding.UTF8.GetString(stream.ToArray()) + "\n";
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
        await using var net = client.GetStream();
        await net.WriteAsync(Encoding.UTF8.GetBytes(payload), ct).ConfigureAwait(false);
        using var reader = new StreamReader(net, Encoding.UTF8, leaveOpen: true);
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
