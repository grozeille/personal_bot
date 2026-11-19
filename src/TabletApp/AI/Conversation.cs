using TabletApp.UI;

namespace TabletApp.AI;

/// <summary>
/// Boucle de discussion : salue, puis écoute → réfléchit → répond avec
/// l'émotion demandée, jusqu'à 2 silences de suite ou annulation, et dit
/// au revoir. Entrées/sorties injectées (vrai matériel en app, fakes en tests).
/// </summary>
internal sealed class Conversation
{
    public const string Greeting = "Je t'écoute...";
    public const string Goodbye = "Bye !";

    private readonly Func<TimeSpan, CancellationToken, Task<string?>> _listen;
    private readonly Func<IReadOnlyList<(string Child, string Patate)>, string, CancellationToken, Task<(string? Reply, string? Emotion)>> _think;
    private readonly Func<string, CancellationToken, Task> _speak;
    private readonly Action<RobotExpression> _showFace;
    private readonly TimeSpan _listenFor;

    public Conversation(
        Func<TimeSpan, CancellationToken, Task<string?>> listen,
        Func<IReadOnlyList<(string Child, string Patate)>, string, CancellationToken, Task<(string? Reply, string? Emotion)>> think,
        Func<string, CancellationToken, Task> speak,
        Action<RobotExpression> showFace,
        TimeSpan? listenFor = null)
    {
        _listen = listen;
        _think = think;
        _speak = speak;
        _showFace = showFace;
        _listenFor = listenFor ?? TimeSpan.FromSeconds(10);
    }

    public async Task RunAsync(RobotExpression current, CancellationToken ct)
    {
        var history = new List<(string Child, string Patate)>();
        try
        {
            await _speak(Greeting, ct).ConfigureAwait(false);
            int silent = 0;
            while (!ct.IsCancellationRequested)
            {
                string? heard = await _listen(_listenFor, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(heard))
                {
                    silent++;
                    if (silent >= 2)
                    {
                        break;
                    }
                    continue;
                }
                silent = 0;
                var (reply, emotion) = await _think(history, heard, ct).ConfigureAwait(false);
                _showFace(MapEmotion(emotion, current));
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    history.Add((heard, reply));
                    await _speak(reply, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Arrêt demandé (touche D) : on conclut poliment ci-dessous.
        }
        await _speak(Goodbye, CancellationToken.None).ConfigureAwait(false);
        _showFace(current);
    }

    private static RobotExpression MapEmotion(string? emotion, RobotExpression fallback) =>
        emotion is not null && ExpressionController.TryParse(emotion, out RobotExpression parsed)
            ? parsed
            : fallback;
}
