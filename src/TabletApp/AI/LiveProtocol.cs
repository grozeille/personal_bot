using System.Text.Json;

namespace TabletApp.AI;

/// <summary>Appel d'outil reçu (set_emotion : Emotion renseigné).</summary>
internal sealed record LiveToolCall(string Id, string Name, string? Emotion, string? Sound, string? Gesture);

/// <summary>Mise à jour serveur décodée (plusieurs aspects peuvent coexister).</summary>
internal sealed record LiveServerUpdate(
    bool SetupComplete,
    bool TurnComplete,
    bool Interrupted,
    bool GoAway,
    byte[]? Audio,
    string? InputTranscript,
    string? OutputTranscript,
    IReadOnlyList<LiveToolCall> ToolCalls,
    IReadOnlyList<string> CancelledIds)
{
    internal static LiveServerUpdate Empty { get; } = new(
        false, false, false, false, null, null, null,
        Array.Empty<LiveToolCall>(), Array.Empty<string>());
}

/// <summary>
/// Protocole brut Gemini Live (BidiGenerateContent JSON). Construit les
/// messages client et décode les messages serveur. Sans réseau : testé.
/// </summary>
internal static class LiveProtocol
{
    internal const string Endpoint =
        "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";
    internal const string Model = "models/gemini-3.8-live";

    /// <summary>Langue imposée (transcription + réponses), sinon hallucinations (es/de).</summary>
    internal const string LanguageCode = "fr-FR";

    public static string BuildSetup(string systemPrompt, IReadOnlyList<string> emotions, IReadOnlyList<string> sounds, IReadOnlyList<string> gestures) =>
        JsonSerializer.Serialize(new
        {
            setup = new
            {
                model = Model,
                generationConfig = new
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new
                    {
                        languageCode = LanguageCode,
                    },
                },
                systemInstruction = new
                {
                    parts = new[] { new { text = systemPrompt } },
                },
                tools = new[]
                {
                    new
                    {
                        functionDeclarations = new object[]
                        {
                            new
                            {
                                name = "set_emotion",
                                description = "Change le visage du robot pour l'émotion ressentie.",
                                parameters = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        emotion = new
                                        {
                                            type = "STRING",
                                            description = "L'émotion à afficher sur le visage.",
                                            @enum = emotions,
                                        },
                                    },
                                    required = new[] { "emotion" },
                                },
                                behavior = "NON_BLOCKING",
                            },
                            new
                            {
                                name = "play_gesture",
                                description = "Joue un geste de la tete (bouge + expression + son eventuel).",
                                parameters = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        gesture = new
                                        {
                                            type = "STRING",
                                            description = "Le geste a jouer.",
                                            @enum = gestures,
                                        },
                                    },
                                    required = new[] { "gesture" },
                                },
                                behavior = "NON_BLOCKING",
                            },
                            new
                            {
                                name = "play_sound",
                                description = "Joue un petit son robot.",
                                parameters = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        sound = new
                                        {
                                            type = "STRING",
                                            description = "Le son voulu.",
                                            @enum = sounds,
                                        },
                                    },
                                    required = new[] { "sound" },
                                },
                                behavior = "NON_BLOCKING",
                            },
                            new
                            {
                                name = "play_fart",
                                description = "Joue un prout, sans parler.",
                                behavior = "NON_BLOCKING",
                            },
                            new
                            {
                                name = "end_conversation",
                                description = "Termine la discussion quand l'enfant dit au revoir ou demande d'arreter.",
                            },
                            new
                            {
                                name = "run_calibration",
                                description = "Relance la calibration du wake word quand on le demande.",
                            }
                        },
                    },
                },
                inputAudioTranscription = new { },
                outputAudioTranscription = new { },
            },
        });

    public static string BuildAudioChunk(byte[] pcm16k) => JsonSerializer.Serialize(new
    {
        realtimeInput = new
        {
            audio = new
            {
                data = Convert.ToBase64String(pcm16k),
                mimeType = "audio/pcm;rate=16000",
            },
        },
    });

    public static string BuildTurnComplete() => JsonSerializer.Serialize(new
    {
        clientContent = new { turnComplete = true },
    });

    public static string BuildClientTurn(string text) => JsonSerializer.Serialize(new
    {
        clientContent = new
        {
            turns = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text } },
                },
            },
            turnComplete = true,
        },
    });

    public static string BuildToolResponse(IEnumerable<(string Id, string Name)> calls) =>
        JsonSerializer.Serialize(new
        {
            toolResponse = new
            {
                functionResponses = calls.Select(call => new
                {
                    id = call.Id,
                    name = call.Name,
                    response = new { result = "ok" },
                }).ToArray(),
            },
        });

    public static LiveServerUpdate ParseUpdate(string json)
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (root.TryGetProperty("setupComplete", out _))
        {
            return LiveServerUpdate.Empty with { SetupComplete = true };
        }
        if (root.TryGetProperty("toolCall", out JsonElement toolCall)
            && toolCall.TryGetProperty("functionCalls", out JsonElement calls))
        {
            var list = new List<LiveToolCall>();
            foreach (JsonElement call in calls.EnumerateArray())
            {
                string id = call.TryGetProperty("id", out JsonElement idEl)
                    ? idEl.GetString() ?? "" : "";
                string name = call.TryGetProperty("name", out JsonElement nameEl)
                    ? nameEl.GetString() ?? "" : "";
                list.Add(new LiveToolCall(id, name, ReadEmotion(call), ReadSound(call), ReadGesture(call)));
            }
            return LiveServerUpdate.Empty with { ToolCalls = list };
        }
        if (root.TryGetProperty("toolCallCancellation", out JsonElement cancel)
            && cancel.TryGetProperty("ids", out JsonElement ids))
        {
            var list = new List<string>();
            foreach (JsonElement id in ids.EnumerateArray())
            {
                if (id.GetString() is { } value)
                {
                    list.Add(value);
                }
            }
            return LiveServerUpdate.Empty with { CancelledIds = list };
        }
        if (root.TryGetProperty("goAway", out _))
        {
            return LiveServerUpdate.Empty with { GoAway = true };
        }
        if (root.TryGetProperty("serverContent", out JsonElement content))
        {
            byte[]? audio = null;
            if (content.TryGetProperty("modelTurn", out JsonElement turn)
                && turn.TryGetProperty("parts", out JsonElement parts))
            {
                var bytes = new List<byte>();
                foreach (JsonElement part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("inlineData", out JsonElement blob)
                        && blob.TryGetProperty("data", out JsonElement data)
                        && data.GetString() is { Length: > 0 } encoded)
                    {
                        try
                        {
                            bytes.AddRange(Convert.FromBase64String(encoded));
                        }
                        catch (FormatException)
                        {
                        }
                    }
                }
                if (bytes.Count > 0)
                {
                    audio = bytes.ToArray();
                }
            }
            return new LiveServerUpdate(
                SetupComplete: false,
                TurnComplete: Flag(content, "turnComplete"),
                Interrupted: Flag(content, "interrupted"),
                GoAway: false,
                Audio: audio,
                InputTranscript: Transcript(content, "inputTranscription"),
                OutputTranscript: Transcript(content, "outputTranscription"),
                ToolCalls: Array.Empty<LiveToolCall>(),
                CancelledIds: Array.Empty<string>());
        }
        return LiveServerUpdate.Empty;
    }

    private static bool Flag(JsonElement content, string name) =>
        content.TryGetProperty(name, out JsonElement flag)
        && flag.ValueKind == JsonValueKind.True;

    private static string? Transcript(JsonElement content, string name) =>
        content.TryGetProperty(name, out JsonElement node)
        && node.TryGetProperty("text", out JsonElement text)
        && text.GetString() is { Length: > 0 } value
            ? value
            : null;

    private static string? ReadGesture(JsonElement call)
    {
        if (!call.TryGetProperty("args", out JsonElement args))
        {
            return null;
        }
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("gesture", out JsonElement direct)
            && direct.GetString() is { Length: > 0 } wanted)
        {
            return wanted;
        }
        if (args.ValueKind == JsonValueKind.String
            && args.GetString() is { Length: > 0 } encoded)
        {
            try
            {
                using var doc = JsonDocument.Parse(encoded);
                if (doc.RootElement.TryGetProperty("gesture", out JsonElement nested)
                    && nested.GetString() is { Length: > 0 } nestedWanted)
                {
                    return nestedWanted;
                }
            }
            catch (JsonException)
            {
            }
        }
        return null;
    }

    private static string? ReadSound(JsonElement call)
    {
        if (!call.TryGetProperty("args", out JsonElement args))
        {
            return null;
        }
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("sound", out JsonElement direct)
            && direct.GetString() is { Length: > 0 } wanted)
        {
            return wanted;
        }
        if (args.ValueKind == JsonValueKind.String
            && args.GetString() is { Length: > 0 } encoded)
        {
            try
            {
                using var doc = JsonDocument.Parse(encoded);
                if (doc.RootElement.TryGetProperty("sound", out JsonElement nested)
                    && nested.GetString() is { Length: > 0 } nestedWanted)
                {
                    return nestedWanted;
                }
            }
            catch (JsonException)
            {
            }
        }
        return null;
    }

    private static string? ReadEmotion(JsonElement call)
    {
        if (!call.TryGetProperty("args", out JsonElement args))
        {
            return null;
        }
        if (args.ValueKind == JsonValueKind.Object
            && args.TryGetProperty("emotion", out JsonElement direct)
            && direct.GetString() is { Length: > 0 } feeling)
        {
            return feeling;
        }
        if (args.ValueKind == JsonValueKind.String
            && args.GetString() is { Length: > 0 } encoded)
        {
            try
            {
                using var doc = JsonDocument.Parse(encoded);
                if (doc.RootElement.TryGetProperty("emotion", out JsonElement nested)
                    && nested.GetString() is { Length: > 0 } nestedFeeling)
                {
                    return nestedFeeling;
                }
            }
            catch (JsonException)
            {
            }
        }
        return null;
    }
}
