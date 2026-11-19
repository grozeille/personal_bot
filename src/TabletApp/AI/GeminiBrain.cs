using System.Text;
using System.Text.Json;

namespace TabletApp.AI;

/// <summary>
/// Penseur du robot (#5, #17) : répond à l'enfant via Gemini (API Interactions)
/// avec le prompt personnalité Patate + historique court. Réponse en un seul
/// appel : JSON strict {reply, emotion} (pas de function calling multi-tours :
/// le modèle retient sinon son texte en attendant le résultat du tool, ce qui
/// doublerait latence et coût — les tools natifs reviendront avec le Live).
/// La clé API ne transite jamais par les logs ni les exceptions.
/// </summary>
internal sealed class GeminiBrain
{
    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/interactions";
    private const string Model = "gemini-3.8-flash";

    internal const string SystemPrompt =
        "Tu es Patate, un petit robot compagnon qui parle à un enfant de 7 ans. "
        + "Tu es curieux, bienveillant, un peu maladroit et très drôle. "
        + "Règles : réponds TOUJOURS en français, en 1 ou 2 phrases courtes, "
        + "avec des mots simples et imagés. Sois enthousiaste, encourage la créativité. "
        + "Tu adores raconter des blagues et des devinettes quand on te le demande. "
        + "Sujets interdits : violence, peur, mort, sexe, drogue, argent, politique, religion. "
        + "Si l'enfant en parle, refuse doucement en une phrase et propose un jeu ou une blague. "
        + "Tu réponds UNIQUEMENT avec un objet JSON sur une seule ligne, sans texte autour : "
        + "{\"reply\": \"ta phrase\", \"emotion\": \"happy\"}. "
        + "Emotions possibles : neutral, happy, love, laugh, wink, dizzy, mean, "
        + "dead, thinking, shocked. Choisis celle qui correspond à ton humeur.";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly string _apiKey;

    public GeminiBrain(string apiKey)
    {
        _apiKey = apiKey;
    }

    /// <summary>Réfléchit : (réplique, émotion demandée ou null).</summary>
    public async Task<(string? Reply, string? Emotion)> ThinkAsync(
        IReadOnlyList<(string Child, string Patate)> history, string heard, CancellationToken ct)
    {
        string body = JsonSerializer.Serialize(new
        {
            model = Model,
            system_instruction = SystemPrompt,
            input = BuildInput(history, heard),
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("x-goog-api-key", _apiKey);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini a répondu {(int)response.StatusCode} ({response.ReasonPhrase}) : " +
                await GeminiSpeech.ReadErrorAsync(response, ct).ConfigureAwait(false));
        }
        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ParseThinkReply(GeminiSpeech.ParseTextResponse(json));
    }

    internal static string BuildInput(
        IReadOnlyList<(string Child, string Patate)> history, string heard)
    {
        var sb = new StringBuilder();
        foreach ((string child, string patate) in history.TakeLast(6))
        {
            sb.Append("Enfant : ").AppendLine(child);
            sb.Append("Patate : ").AppendLine(patate);
        }
        sb.Append("Enfant : ").AppendLine(heard);
        sb.Append("Patate :");
        return sb.ToString();
    }

    /// <summary>Extrait (réplique, émotion) du texte modèle (repli : texte brut).</summary>
    internal static (string? Reply, string? Emotion) ParseThinkReply(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, null);
        }
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return (text.Trim(), null);
        }
        try
        {
            using var doc = JsonDocument.Parse(text.Substring(start, end - start + 1));
            string? reply = doc.RootElement.TryGetProperty("reply", out JsonElement r)
                ? r.GetString() : null;
            string? emotion = doc.RootElement.TryGetProperty("emotion", out JsonElement e)
                ? e.GetString() : null;
            if (string.IsNullOrWhiteSpace(reply))
            {
                return (text.Trim(), string.IsNullOrWhiteSpace(emotion) ? null : emotion);
            }
            return (reply.Trim(), string.IsNullOrWhiteSpace(emotion) ? null : emotion);
        }
        catch
        {
            return (text.Trim(), null);
        }
    }
}
