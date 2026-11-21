using System.Text.Json;

namespace TabletApp.Gestures;

/// <summary>
/// Cible tête (section "Motion" de appsettings.json) : hôte et port TCP du
/// simu Godot ou du Pi. Env HEAD_SIM_HOST/HEAD_SIM_PORT prioritaires (voir
/// HeadTcpClient). Valeurs par défaut si absent/invalide.
/// </summary>
internal sealed record MotionOptions(string TargetHost, int Port)
{
    public static string DefaultHost => "127.0.0.1";

    public static int DefaultPort => 9080;

    public static MotionOptions Default { get; } = new(DefaultHost, DefaultPort);

    public static MotionOptions Load(string? configPath = null)
    {
        try
        {
            string path = configPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return Default;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Motion", out JsonElement motion))
            {
                return Default;
            }
            string host = motion.TryGetProperty("TargetHost", out JsonElement h)
                && h.GetString() is { Length: > 0 } name
                ? name : DefaultHost;
            int port = motion.TryGetProperty("Port", out JsonElement p)
                && p.TryGetInt32(out int n) && n is > 0 and <= 65535
                ? n : DefaultPort;
            return new MotionOptions(host, port);
        }
        catch
        {
            // Config illisible : repli silencieux sur les défauts (jamais de crash).
            return Default;
        }
    }
}
