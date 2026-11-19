namespace TabletApp.UI;

/// <summary>
/// Endormissement automatique par inactivité (#16) : visage fatigué après
/// <c>tiredAfter</c> sans activité, puis endormissement après
/// <c>sleepAfter</c>. Toute activité (touche, toucher, futur mot-clé)
/// réarme le minuteur. Logique pure (temps injecté), testable sans UI.
/// </summary>
internal sealed class IdleController
{
    private readonly TimeSpan _tiredAfter;
    private readonly TimeSpan _sleepAfter;
    private TimeSpan _lastActivity = TimeSpan.Zero;
    private bool _tiredShown;

    public IdleController(TimeSpan tiredAfter, TimeSpan sleepAfter)
    {
        _tiredAfter = tiredAfter;
        _sleepAfter = sleepAfter;
    }

    /// <summary>
    /// Signale une activité : réarme le minuteur. Retourne vrai si un visage
    /// fatigué automatique était affiché (l'appelant restaure alors neutre).
    /// </summary>
    public bool NotifyActivity(TimeSpan now)
    {
        _lastActivity = now;
        bool wasTired = _tiredShown;
        _tiredShown = false;
        return wasTired;
    }

    /// <summary>Vrai une seule fois quand le seuil fatigué est franchi.</summary>
    public bool PollTired(TimeSpan now)
    {
        if (_tiredShown || now - _lastActivity < _tiredAfter)
        {
            return false;
        }
        _tiredShown = true;
        return true;
    }

    /// <summary>Vrai quand le seuil d'endormissement est atteint.</summary>
    public bool SleepDue(TimeSpan now) => now - _lastActivity >= _sleepAfter;
}
