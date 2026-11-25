using System.Text.Json;

namespace TabletApp.AI;

/// <summary>Recherche Wikipédia FR injectable (résumé d'intro d'article).</summary>
internal interface IWikipediaSearch
{
    Task<string?> SearchSummaryAsync(string query, CancellationToken ct);
}

/// <summary>
/// Recherche Wikipédia en français : titre via opensearch, résumé via
/// extracts (plaintext, intro). Gratuit, sans clé (fair use : throttle
/// côté appelant, petits volumes). Null si rien trouvé ou échec.
/// </summary>
internal sealed class WikipediaSearch : IWikipediaSearch
{
    private const string Api = "https://fr.wikipedia.org/w/api.php";
    internal const int MaxSummaryChars = 800;

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PersonalBot/1.0 (robot jouet personnel)");
        return http;
    }

    private readonly Func<string, CancellationToken, Task<string>> _get;
    private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();

    /// <param name="httpGet">Requêtes HTTP (injectable pour les tests).</param>
    public WikipediaSearch(Func<string, CancellationToken, Task<string>>? httpGet = null)
    {
        _get = httpGet ?? ((url, ct) => Http.GetStringAsync(url, ct));
    }

    public async Task<string?> SearchSummaryAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }
        string key = query.Trim();
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out string? cached))
            {
                return cached;
            }
        }
        try
        {
            // Titre direct d'abord (1 requête si la transcription est propre),
            // sinon recherche du titre (transcription approximative).
            string? text = await TryExtractAsync(key, ct).ConfigureAwait(false);
            string? title = key;
            if (text is null)
            {
                string open = await _get(
                    $"{Api}?action=opensearch&search={Uri.EscapeDataString(key)}&limit=3&namespace=0&format=json",
                    ct).ConfigureAwait(false);
                title = ParseOpensearchTitle(open);
                if (title is null)
                {
                    return null;
                }
                text = await TryExtractAsync(title, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }
            }
            text = text.Trim();
            string shorted = text.Length <= MaxSummaryChars ? text : text.Substring(0, MaxSummaryChars) + "…";
            string answer = $"{title} : {shorted}";
            lock (_cacheLock)
            {
                if (_cache.Count < 50)
                {
                    _cache[key] = answer;
                }
            }
            return answer;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Extrait d'un titre exact (avec redirections), null si absent.</summary>
    private async Task<string?> TryExtractAsync(string title, CancellationToken ct)
    {
        string page = await _get(
            $"{Api}?action=query&format=json&prop=extracts&exintro&explaintext&redirects=1&titles={Uri.EscapeDataString(title)}",
            ct).ConfigureAwait(false);
        return ParseExtract(page);
    }

    /// <summary>Premier titre opensearch ([requête,[titres],[desc],[urls]]), null si rien.</summary>
    internal static string? ParseOpensearchTitle(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.EnumerateArray();
            if (!items.MoveNext())
            {
                return null;
            }
            if (!items.MoveNext())
            {
                return null;
            }
            foreach (JsonElement title in items.Current.EnumerateArray())
            {
                if (title.GetString() is { Length: > 0 } found)
                {
                    return found;
                }
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Extrait plaintext du premier article trouvé, null si absent.</summary>
    internal static string? ParseExtract(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("query", out JsonElement query)
                || !query.TryGetProperty("pages", out JsonElement pages))
            {
                return null;
            }
            foreach (JsonProperty page in pages.EnumerateObject())
            {
                if (page.Value.TryGetProperty("extract", out JsonElement extract)
                    && extract.GetString() is { Length: > 0 } text)
                {
                    return text.Trim();
                }
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
