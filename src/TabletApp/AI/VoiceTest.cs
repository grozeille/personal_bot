using TabletApp.UI;

namespace TabletApp.AI;

/// <summary>
/// Test voix #5 (sans wake word ni Live) : dit bonjour, écoute, affiche le
/// visage cœur si l'enfant répond « bonjour ». Les entrées/sorties sont
/// injectées (vrai matériel en app, fakes en tests).
/// </summary>
internal sealed class VoiceTest
{
    public const string Greeting = "Bonjour, je m'appelle Patate !";

    private readonly Func<string, CancellationToken, Task> _speak;
    private readonly Func<TimeSpan, CancellationToken, Task<string?>> _listen;
    private readonly Action<RobotExpression> _showFace;
    private readonly TimeSpan _listenFor;
    private readonly TimeSpan _loveFor;

    public VoiceTest(
        Func<string, CancellationToken, Task> speak,
        Func<TimeSpan, CancellationToken, Task<string?>> listen,
        Action<RobotExpression> showFace,
        TimeSpan? listenFor = null,
        TimeSpan? loveFor = null)
    {
        _speak = speak;
        _listen = listen;
        _showFace = showFace;
        _listenFor = listenFor ?? TimeSpan.FromSeconds(10);
        _loveFor = loveFor ?? TimeSpan.FromSeconds(2);
    }

    /// <summary>
    /// Déroule le scénario depuis le visage courant (restauré à la fin).
    /// Retourne la transcription entendue (null si rien).
    /// </summary>
    public async Task<string?> RunAsync(RobotExpression current, CancellationToken ct)
    {
        await _speak(Greeting, ct).ConfigureAwait(false);
        string? heard = await _listen(_listenFor, ct).ConfigureAwait(false);
        if (HearsBonjour(heard))
        {
            _showFace(RobotExpression.Love);
            await Task.Delay(_loveFor, ct).ConfigureAwait(false);
            _showFace(current);
        }
        return heard;
    }

    internal static bool HearsBonjour(string? transcript) =>
        transcript is not null
        && transcript.Contains("bonjour", StringComparison.OrdinalIgnoreCase);
}
