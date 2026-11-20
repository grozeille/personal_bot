using System.Text.Json;

namespace TabletApp.Gestures;

/// <summary>
/// Bibliotheque de gestes (gestures.json) : nom -&gt; (expression, piste moves brute).
/// La piste est transmise telle quelle au simu/Pi : l'intelligence reste ici.
/// </summary>
internal sealed class GestureLibrary
{
    public record Gesture(string Name, string Expression, string MovesJson);

    private readonly Dictionary<string, Gesture> _gestures = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _gestures.Count;

    public static GestureLibrary Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var lib = new GestureLibrary();
        foreach (var g in doc.RootElement.GetProperty("gestures").EnumerateObject())
        {
            lib._gestures[g.Name] = new Gesture(
                g.Name,
                g.Value.GetProperty("expression").GetString() ?? "neutral",
                g.Value.GetProperty("moves").GetRawText());
        }
        return lib;
    }

    /// <summary>Résout gestures.json : dossier exe, puis remontée dev vers src/TabletApp/Gestures/.</summary>
    public static string ResolvePath()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "Gestures", "gestures.json"),
            Path.Combine(AppContext.BaseDirectory, "gestures.json"),
        ];
        foreach (string c in candidates)
        {
            if (File.Exists(c))
            {
                return c;
            }
        }
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            string p = Path.Combine(dir, "src", "TabletApp", "Gestures", "gestures.json");
            if (File.Exists(p))
            {
                return p;
            }
            dir = Path.GetDirectoryName(dir);
        }
        throw new FileNotFoundException("gestures.json introuvable.");
    }

    public bool TryGet(string name, out Gesture gesture) => _gestures.TryGetValue(name, out gesture!);
}
