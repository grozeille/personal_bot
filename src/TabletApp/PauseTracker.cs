namespace TabletApp;

/// <summary>
/// Pause de discussion ("attends", "pause") : la session Live se ferme
/// mais le contexte est gardé. Reprise sous 10 min = suite (pas de
/// re-bonjour, juste "Oui ?") ; au-delà = discussion fraîche (le carnet
/// mémoire, lui, persiste toujours). Temps injecté (testable).
/// </summary>
internal sealed class PauseTracker
{
    internal static readonly TimeSpan ResumeWindow = TimeSpan.FromMinutes(10);

    private DateTime? _pausedAt;

    public bool IsPaused => _pausedAt.HasValue;

    public void Pause(DateTime now) => _pausedAt = now;

    /// <summary>Démarrage : true = reprise (contexte gardé), false = fraîche.</summary>
    public bool TryResume(DateTime now)
    {
        bool resume = _pausedAt.HasValue && now - _pausedAt.Value < ResumeWindow;
        _pausedAt = null;
        return resume;
    }
}
