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

    /// <summary>Cible tête locale (ex. tablette -> simu PC), survit aux mises à jour.</summary>
    public static string OverridePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TabletApp", "motion-override.json");

    public static MotionOptions Load(string? configPath = null, string? overridePath = null)
    {
        MotionOptions fromApp = ReadSection(
            configPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        MotionOptions fromLocal = ReadSection(overridePath ?? OverridePath);
        return new MotionOptions(
            fromLocal.TargetHost.Length > 0 ? fromLocal.TargetHost
                : fromApp.TargetHost.Length > 0 ? fromApp.TargetHost : DefaultHost,
            fromLocal.Port > 0 ? fromLocal.Port
                : fromApp.Port > 0 ? fromApp.Port : DefaultPort);
    }

    private static MotionOptions ReadSection(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new MotionOptions("", -1);
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Motion", out JsonElement motion))
            {
                return new MotionOptions("", -1);
            }
            string host = motion.TryGetProperty("TargetHost", out JsonElement h)
                && h.GetString() is { Length: > 0 } name
                ? name : "";
            int port = motion.TryGetProperty("Port", out JsonElement p)
                && p.TryGetInt32(out int n) && n is > 0 and <= 65535
                ? n : -1;
            if (host.Length == 0 && port < 0)
            {
                return new MotionOptions("", -1);
            }
            return new MotionOptions(
                host.Length == 0 ? DefaultHost : host,
                port < 0 ? DefaultPort : port);
        }
        catch
        {
            // Config illisible : comme absente (l'autre source gagne).
            return new MotionOptions("", -1);
        }
    }
}
