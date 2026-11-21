using System.Text.Json;

namespace TabletApp.AI;

/// <summary>
/// Réglages du wake word (section "WakeWord" de appsettings.json) : seuil de
/// déclenchement et score. Le mot-clé lui-même est fixé par keywords-patate.txt
/// (phonèmes, voir docs du modèle). Valeurs par défaut si absent/invalide.
/// </summary>
internal sealed record WakeWordOptions(float Threshold, float Score, double GateRms)
{
    public static float DefaultThreshold => 0.25f;

    public static float DefaultScore => 1.0f;

    public static double DefaultGateRms => 150.0;

    public static WakeWordOptions Default { get; } = new(DefaultThreshold, DefaultScore, DefaultGateRms);

    public static WakeWordOptions Load(string? configPath = null)
    {
        try
        {
            string path = configPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return Default;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("WakeWord", out JsonElement wake))
            {
                return Default;
            }
            float threshold = wake.TryGetProperty("Threshold", out JsonElement t)
                && t.TryGetSingle(out float n) && n is > 0 and <= 1
                ? n : DefaultThreshold;
            float score = wake.TryGetProperty("Score", out JsonElement s)
                && s.TryGetSingle(out float m) && m > 0
                ? m : DefaultScore;
            double gate = wake.TryGetProperty("GateRms", out JsonElement g)
                && g.TryGetDouble(out double d) && d > 0
                ? d : DefaultGateRms;
            return new WakeWordOptions(threshold, score, gate);
        }
        catch
        {
            // Config illisible : repli silencieux sur les défauts (jamais de crash).
            return Default;
        }
    }
}
