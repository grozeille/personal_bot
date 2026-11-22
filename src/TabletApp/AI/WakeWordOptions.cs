using System.Text.Json;

namespace TabletApp.AI;

/// <summary>
/// Réglages du wake word : appsettings.json (section "WakeWord"), surchargés
/// par la calibration d'enrôlement (%LocalAppData%\TabletApp\wake-calibration.json,
/// qui survit aux mises à jour). Le mot-clé lui-même est fixé par
/// keywords-patate.txt (phonèmes, voir docs du modèle). Valeurs par défaut
/// si absent/invalide.
/// </summary>
internal sealed record WakeWordOptions(float Threshold, float Score, double GateRms, bool AutoEnroll = true)
{
    public static float DefaultThreshold => 0.25f;

    public static float DefaultScore => 1.0f;

    public static double DefaultGateRms => 150.0;

    public static WakeWordOptions Default { get; } = new(DefaultThreshold, DefaultScore, DefaultGateRms);

    /// <summary>Calibration écrite par l'enrôlement (survit aux mises à jour).</summary>
    public static string OverridePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TabletApp", "wake-calibration.json");

    public static WakeWordOptions Load(string? configPath = null, string? overridePath = null)
    {
        WakeWordOptions fromApp = ReadSection(
            configPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        WakeWordOptions fromCal = ReadSection(overridePath ?? OverridePath);
        return new WakeWordOptions(
            Pick(fromCal.Threshold, fromApp.Threshold, DefaultThreshold),
            Pick(fromCal.Score, fromApp.Score, DefaultScore),
            Pick(fromCal.GateRms, fromApp.GateRms, DefaultGateRms),
            fromApp.AutoEnroll);
    }

    private static float Pick(float cal, float app, float @default) =>
        cal >= 0 ? cal : app >= 0 ? app : @default;

    private static double Pick(double cal, double app, double @default) =>
        cal >= 0 ? cal : app >= 0 ? app : @default;

    /// <summary>Écrit la calibration (seuil/score/porte + métadonnées).</summary>
    public static void SaveOverride(string path, WakeWordOptions options, int takes, string winner)
    {
        string? dir = Path.GetDirectoryName(path);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }
        string json = JsonSerializer.Serialize(new
        {
            WakeWord = new
            {
                options.Threshold,
                options.Score,
                options.GateRms,
            },
            Calibration = new
            {
                At = DateTime.Now.ToString("s"),
                Takes = takes,
                Winner = winner,
            },
        });
        File.WriteAllText(path, json);
    }

    private static WakeWordOptions ReadSection(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new WakeWordOptions(-1, -1, -1);
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("WakeWord", out JsonElement wake))
            {
                return new WakeWordOptions(-1, -1, -1);
            }
            float threshold = wake.TryGetProperty("Threshold", out JsonElement t)
                && t.TryGetSingle(out float n) && n is > 0 and <= 1
                ? n : -1;
            float score = wake.TryGetProperty("Score", out JsonElement s)
                && s.TryGetSingle(out float m) && m > 0
                ? m : -1;
            double gate = wake.TryGetProperty("GateRms", out JsonElement g)
                && g.TryGetDouble(out double d) && d > 0
                ? d : -1;
            bool auto = !wake.TryGetProperty("AutoEnroll", out JsonElement a)
                || a.ValueKind != JsonValueKind.False;
            return new WakeWordOptions(threshold, score, gate, auto);
        }
        catch
        {
            // Config illisible : comme absente (l'autre source gagne).
            return new WakeWordOptions(-1, -1, -1);
        }
    }
}
