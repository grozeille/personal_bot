using System.Text.Json;

namespace TabletApp.AI;

/// <summary>
/// Clé API Gemini, lue dans l'ordre : variable d'environnement
/// <c>GEMINI_API_KEY</c> → User Secrets → <c>appsettings.json</c> (voir README).
/// La clé ne transite jamais par les logs ni les exceptions.
/// </summary>
internal static class GeminiKey
{
    // Doit rester synchronisé avec <UserSecretsId> du csproj.
    private const string UserSecretsId = "2845a56a-79cc-4c30-b8b0-654897b03861";

    /// <summary>Chemin standard du fichier User Secrets (lu et écrit).</summary>
    internal static string SecretsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "UserSecrets", UserSecretsId, "secrets.json");

    public static string? Resolve()
    {
        string? env = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }
        if (ReadFlatKey(SecretsPath()) is { } fromSecrets)
        {
            return fromSecrets;
        }
        string appSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        return ReadNestedKey(appSettings);
    }

    /// <summary>
    /// Stocke la clé dans le fichier User Secrets (fusionne sans écraser le reste).
    /// </summary>
    internal static void StoreApiKey(string path, string key)
    {
        var values = new Dictionary<string, string>();
        try
        {
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        values[prop.Name] = prop.Value.GetString() ?? "";
                    }
                }
            }
        }
        catch
        {
            // Fichier illisible : on repart d'un fichier propre.
        }
        values["Gemini:ApiKey"] = key;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(
            values, new JsonSerializerOptions { WriteIndented = true }));
    }

    // dotnet user-secrets stocke les clés à plat : {"Gemini:ApiKey": "..."}.
    internal static string? ReadFlatKey(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("Gemini:ApiKey", out JsonElement key)
                && key.GetString() is { Length: > 0 } value
                ? value : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadNestedKey(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("Gemini", out JsonElement gemini)
                && gemini.TryGetProperty("ApiKey", out JsonElement key)
                && key.GetString() is { Length: > 0 } value
                ? value : null;
        }
        catch
        {
            return null;
        }
    }
}
