using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TabletApp.AI;

/// <summary>
/// Parole via l'API Gemini (Interactions REST v1beta) : synthèse vocale
/// (TTS) et transcription (STT) pour le test voix (#5, sans Live ni wake word).
/// La clé API ne transite jamais par les logs ni les exceptions.
/// </summary>
internal sealed class GeminiSpeech
{
    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/interactions";
    private const string TtsModel = "gemini-3.8-flash-lite-tts";
    private const string SttModel = "gemini-4-hydrogen";
    private const string Voice = "Digital Assistant 10";
    private const string ApiRevision = "2026-05-20";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    private readonly string _apiKey;

    public GeminiSpeech(string apiKey)
    {
        _apiKey = apiKey;
    }

    /// <summary>Synthétise le texte : octets d'un WAV (24 kHz, mono, 16 bits).</summary>
    public async Task<byte[]> SpeakAsync(string text, CancellationToken ct)
    {
        string body = JsonSerializer.Serialize(new
        {
            model = TtsModel,
            input = new[]
            {
                new
                {
                    type = "user_input",
                    content = new[]
                    {
                        new { type = "text", text },
                    },
                },
            },
            response_format = new { type = "audio" },
            generation_config = new { speech_config = new[] { new { voice = Voice } } },
        });
        string json = await PostAsync(body, includeRevision: false, ct).ConfigureAwait(false);
        return ParseAudioResponse(json);
    }

    /// <summary>Transcrit un WAV (retourne null si l'audio est vide/incompréhensible).</summary>
    public async Task<string?> TranscribeAsync(byte[] wav, CancellationToken ct)
    {
        string body = JsonSerializer.Serialize(new
        {
            model = SttModel,
            input = new object[]
            {
                new
                {
                    type = "text",
                    text = "Transcribe exactly what is said in this French audio clip. "
                        + "Reply with only the transcription, no commentary. "
                        + "If nothing is said, reply with nothing.",
                },
                new { type = "audio", data = Convert.ToBase64String(wav), mime_type = "audio/wav" },
            },
        });
        string json = await PostAsync(body, includeRevision: true, ct).ConfigureAwait(false);
        return ParseTextResponse(json);
    }

    private async Task<string> PostAsync(string body, bool includeRevision, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("x-goog-api-key", _apiKey);
        if (includeRevision)
        {
            request.Headers.Add("Api-Revision", ApiRevision);
        }
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini a répondu {(int)response.StatusCode} ({response.ReasonPhrase}) : " +
                await ReadErrorAsync(response, ct).ConfigureAwait(false));
        }
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Détail d'erreur API tronqué (jamais de clé : envoyée en en-tête).</summary>
    internal static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            string detail = (await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)).Trim();
            return detail.Length > 300 ? detail.Substring(0, 300) + "…" : detail;
        }
        catch
        {
            return "(illisible)";
        }
    }

    /// <summary>Dernier bloc audio des étapes modèle → octets WAV.</summary>
    internal static byte[] ParseAudioResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("steps", out JsonElement steps))
        {
            foreach (JsonElement step in steps.EnumerateArray().Reverse())
            {
                if (step.TryGetProperty("type", out JsonElement kind)
                    && kind.GetString() == "model_output"
                    && step.TryGetProperty("content", out JsonElement content))
                {
                    foreach (JsonElement item in content.EnumerateArray().Reverse())
                    {
                        if (item.TryGetProperty("type", out JsonElement itemKind)
                            && itemKind.GetString() == "audio"
                            && item.TryGetProperty("data", out JsonElement data)
                            && data.GetString() is { Length: > 0 } audio)
                        {
                            return Convert.FromBase64String(audio);
                        }
                    }
                }
            }
        }
        throw new InvalidDataException("Réponse TTS sans bloc audio.");
    }

    /// <summary>Dernier texte des étapes modèle (null si absent).</summary>
    internal static string? ParseTextResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("steps", out JsonElement steps))
        {
            foreach (JsonElement step in steps.EnumerateArray().Reverse())
            {
                if (step.TryGetProperty("type", out JsonElement kind)
                    && kind.GetString() == "model_output"
                    && step.TryGetProperty("content", out JsonElement content))
                {
                    foreach (JsonElement item in content.EnumerateArray().Reverse())
                    {
                        if (item.TryGetProperty("type", out JsonElement itemKind)
                            && itemKind.GetString() == "text"
                            && item.TryGetProperty("text", out JsonElement text)
                            && text.GetString() is { Length: > 0 } value)
                        {
                            return value.Trim();
                        }
                    }
                }
            }
        }
        return null;
    }
}
