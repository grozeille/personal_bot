using System.Text.Json;

namespace TabletApp.Data;

/// <summary>Entrée Pokédex (sous-ensemble utile au jeu).</summary>
internal sealed record PokedexEntry(int Id, string NameFr, string NameEn, string[] TypesFr,
    int Generation, bool Legendary, string CategoryFr, string DescriptionFr,
    string? EvolvesFrom, string[] EvolvesTo);

/// <summary>
/// Lecteur Pokédex local (pokedex.json) : recherche exacte puis floue
/// (insensible casse/accents) + tirage aléatoire. Pur et testable :
/// le JSON est injecté, LoadDefault() lit le fichier livré.
/// </summary>
internal sealed class PokedexReader
{
    private readonly List<PokedexEntry> _entries;

    public PokedexReader(string json)
    {
        _entries = Parse(json);
    }

    public int Count => _entries.Count;

    /// <summary>Charge le fichier livré (null si absent/illisible).</summary>
    public static PokedexReader? LoadDefault(string? baseDir = null)
    {
        try
        {
            string path = Path.Combine(baseDir ?? AppContext.BaseDirectory, "Data", "pokedex.json");
            if (!File.Exists(path))
            {
                return null;
            }
            var reader = new PokedexReader(File.ReadAllText(path));
            return reader.Count > 0 ? reader : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Fiche compacte : exact puis flou, sinon suggestions.</summary>
    public string? FindSummary(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || _entries.Count == 0)
        {
            return null;
        }
        string norm = Normalize(query);
        PokedexEntry? exact = _entries.FirstOrDefault(e =>
            Normalize(e.NameFr) == norm || Normalize(e.NameEn) == norm);
        if (exact is not null)
        {
            return Format(exact, null);
        }
        var ranked = _entries
            .Select(e => (entry: e, dist: Distance(norm, Normalize(e.NameFr))))
            .OrderBy(x => x.dist)
            .Take(3)
            .ToArray();
        if (ranked.Length > 0 && ranked[0].dist <= 2 && ranked[0].dist * 3 <= norm.Length)
        {
            return Format(ranked[0].entry, query.Trim());
        }
        string hints = string.Join(", ", ranked.Select(x => x.entry.NameFr));
        return $"Je ne connais pas « {query.Trim()} »." +
            (hints.Length > 0 ? $" Tu voulais dire : {hints} ?" : "");
    }

    /// <summary>Tire un Pokémon mystère (jeu : le modèle connaît la réponse).</summary>
    public string? RandomSummary(Random rng)
    {
        if (_entries.Count == 0)
        {
            return null;
        }
        return Format(_entries[rng.Next(_entries.Count)], null);
    }

    internal static string Format(PokedexEntry e, string? heardAs)
    {
        var sb = new System.Text.StringBuilder();
        if (heardAs is not null)
        {
            sb.Append($"(proche de « {heardAs} ») ");
        }
        sb.Append($"{e.NameFr} (n°{e.Id})");
        if (e.TypesFr.Length > 0)
        {
            sb.Append(" — " + string.Join("/", e.TypesFr));
        }
        if (e.Generation > 0)
        {
            sb.Append($", gén {e.Generation}");
        }
        if (e.Legendary)
        {
            sb.Append(", légendaire");
        }
        sb.Append('.');
        if (e.DescriptionFr.Length > 0)
        {
            sb.Append(' ').Append(e.DescriptionFr);
        }
        if (e.EvolvesTo.Length > 0)
        {
            sb.Append(" Évolue en : " + string.Join(", ", e.EvolvesTo) + ".");
        }
        else if (e.EvolvesFrom is { Length: > 0 })
        {
            sb.Append($" Évolution de {e.EvolvesFrom}.");
        }
        return sb.ToString();
    }

    /// <summary>Normalise pour la comparaison floue (casse/accents/espaces).</summary>
    internal static string Normalize(string s)
    {
        string folded = s.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(folded.Length);
        foreach (char c in folded)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>Distance de Levenshtein (petites chaînes).</summary>
    internal static int Distance(string a, string b)
    {
        int n = a.Length;
        int m = b.Length;
        int[,] d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }
        for (int j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    private static List<PokedexEntry> Parse(string json)
    {
        var entries = new List<PokedexEntry>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("pokemon", out JsonElement arr)
                || arr.ValueKind != JsonValueKind.Array)
            {
                return entries;
            }
            foreach (JsonElement p in arr.EnumerateArray())
            {
                string nameFr = Get(p, "name_fr");
                if (nameFr.Length == 0)
                {
                    continue;
                }
                entries.Add(new PokedexEntry(
                    GetInt(p, "id"), nameFr, Get(p, "name_en"), GetList(p, "types_fr"),
                    GetInt(p, "generation"), GetBool(p, "legendary"), Get(p, "category_fr"),
                    Get(p, "description_fr"), NullIfEmpty(Get(p, "evolves_from")), GetList(p, "evolves_to")));
            }
        }
        catch (JsonException)
        {
        }
        return entries;
    }

    private static string Get(JsonElement e, string key) =>
        e.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static string? NullIfEmpty(string s) => s.Length == 0 ? null : s;

    private static int GetInt(JsonElement e, string key) =>
        e.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.Number
            && v.TryGetInt32(out int n) ? n : 0;

    private static bool GetBool(JsonElement e, string key) =>
        e.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.True;

    private static string[] GetList(JsonElement e, string key)
    {
        if (!e.TryGetProperty(key, out JsonElement v) || v.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }
        var list = new List<string>();
        foreach (JsonElement item in v.EnumerateArray())
        {
            if (item.GetString() is { Length: > 0 } s)
            {
                list.Add(s);
            }
        }
        return list.ToArray();
    }
}
