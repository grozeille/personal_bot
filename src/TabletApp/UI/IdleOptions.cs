using System.Text.Json;

namespace TabletApp.UI;

/// <summary>
/// Seuils d'endormissement automatique (section "Idle" de appsettings.json).
/// Minutes fractionnaires acceptées (0.1 = 6 s) pour tester sans attendre.
/// Valeurs par défaut si le fichier est absent ou invalide.
/// </summary>
internal sealed record IdleOptions(TimeSpan TiredAfter, TimeSpan SleepAfter)
{
    public static TimeSpan DefaultTired => TimeSpan.FromMinutes(5);

    public static TimeSpan DefaultSleep => TimeSpan.FromMinutes(10);

    public static IdleOptions Default { get; } = new(DefaultTired, DefaultSleep);

    public static IdleOptions Load(string? configPath = null)
    {
        try
        {
            string path = configPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return Default;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("Idle", out JsonElement idle))
            {
                return Default;
            }
            double tired = idle.TryGetProperty("TiredAfterMinutes", out JsonElement t)
                && t.TryGetDouble(out double n) && n > 0
                ? n : DefaultTired.TotalMinutes;
            double sleep = idle.TryGetProperty("SleepAfterMinutes", out JsonElement s)
                && s.TryGetDouble(out double m) && m > tired
                ? m : tired + DefaultSleep.TotalMinutes - DefaultTired.TotalMinutes;
            return new IdleOptions(TimeSpan.FromMinutes(tired), TimeSpan.FromMinutes(sleep));
        }
        catch
        {
            // Config illisible : repli silencieux sur les défauts (jamais de crash).
            return Default;
        }
    }
}
