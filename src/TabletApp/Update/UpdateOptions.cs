using System.Text.Json;

namespace TabletApp.Update;

/// <summary>
/// Configuration de l'auto-update (section "Update" de appsettings.json).
/// Source = dossier Velopack sur le NAS, monté au même chemin (P:\RobotMaj)
/// sur le PC et la tablette. Valeurs par défaut si le fichier est absent.
/// </summary>
internal sealed record UpdateOptions(string Source, TimeSpan CheckInterval)
{
    public static string DefaultSource => @"P:\RobotMaj";

    public static TimeSpan DefaultInterval => TimeSpan.FromMinutes(5);

    public static UpdateOptions Default { get; } = new(DefaultSource, DefaultInterval);

    public static UpdateOptions Load(string? configPath = null)
    {
        try
        {
            string path = configPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return Default;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Update", out JsonElement update))
            {
                return Default;
            }
            string source = update.TryGetProperty("Source", out JsonElement s)
                && s.GetString() is { Length: > 0 } v
                ? v : DefaultSource;
            int minutes = update.TryGetProperty("CheckIntervalMinutes", out JsonElement m)
                && m.TryGetInt32(out int n) && n > 0
                ? n : (int)DefaultInterval.TotalMinutes;
            return new UpdateOptions(source, TimeSpan.FromMinutes(minutes));
        }
        catch
        {
            // Config illisible : repli silencieux sur les défauts (jamais de crash).
            return Default;
        }
    }
}
