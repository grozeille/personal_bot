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
    private readonly Func<IReadOnlyList<(string Child, string Patate)>, string, CancellationToken, Task<(string? Reply, string? Emotion, string? Sound)>> _think;
    private readonly Func<string, CancellationToken, Task> _speak;
    private readonly Action<RobotExpression> _showFace;
    private readonly Func<string, CancellationToken, Task>? _playSound;
    private readonly TimeSpan _listenFor;
    private CancellationTokenSource? _currentTurn;

    public Conversation(
        Func<TimeSpan, CancellationToken, Task<string?>> listen,
        Func<IReadOnlyList<(string Child, string Patate)>, string, CancellationToken, Task<(string? Reply, string? Emotion, string? Sound)>> think,
        Func<string, CancellationToken, Task> speak,
        Action<RobotExpression> showFace,
        TimeSpan? listenFor = null,
        Func<string, CancellationToken, Task>? playSound = null)
    {
        _listen = listen;
        _think = think;
        _speak = speak;
        _showFace = showFace;
        _playSound = playSound;
        _listenFor = listenFor ?? TimeSpan.FromSeconds(10);
    }

    /// <summary>Tap : annule le tour en cours (retour à l'écoute, session maintenue).</summary>
    public void CancelTurn()
    {
        try
        {
            _currentTurn?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async Task RunAsync(RobotExpression current, CancellationToken ct)
    {
        var history = new List<(string Child, string Patate)>();
        try
        {
            using (var greet = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                _currentTurn = greet;
                try
                {
                    await _speak(Greeting, greet.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                }
                finally
                {
                    _currentTurn = null;
                }
            }
            int silent = 0;
            while (!ct.IsCancellationRequested)
            {
                using var turn = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _currentTurn = turn;
                try
                {
                    string? heard = await _listen(_listenFor, turn.Token).ConfigureAwait(false);
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
                    var (reply, emotion, sound) = await _think(history, heard, turn.Token).ConfigureAwait(false);
                    _showFace(MapEmotion(emotion, current));
                    if (_playSound is not null && !string.IsNullOrWhiteSpace(sound))
                    {
                        await _playSound(sound, turn.Token).ConfigureAwait(false);
                    }
                    if (!string.IsNullOrWhiteSpace(reply))
                    {
                        history.Add((heard, reply));
                        await _speak(reply, turn.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Tour annulé (tap) : on revient à l'écoute.
                    continue;
                }
                finally
                {
                    _currentTurn = null;
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
