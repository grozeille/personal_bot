using System.Drawing;

namespace TabletApp.UI;

/// <summary>
/// Geste de mise à jour (anti-enfant, miroir de <see cref="QuitGesture"/>) :
/// appui maintenu 3 secondes sur l'icône en haut à droite. <see cref="Progress"/>
/// (0..1) pilote l'anneau de progression autour de l'icône. Logique pure
/// testable (temps injecté, tests BDD). La largeur d'écran est fournie à
/// chaque appui car la zone est ancrée à droite.
/// </summary>
internal sealed class UpdateGesture
{
    private static readonly TimeSpan HoldRequired = TimeSpan.FromSeconds(3);

    private readonly float _zoneSize;
    private TimeSpan? _downAt;

    public bool UpdateRequested { get; private set; }

    /// <summary>Progression de l'appui en cours (0 = pas d'appui, 1 = seuil atteint).</summary>
    public double Progress { get; private set; }

    public UpdateGesture(float zoneSize = 120)
    {
        _zoneSize = zoneSize;
    }

    public void Down(PointF location, float screenWidth, TimeSpan now)
    {
        // Tout nouvel appui repart de zéro (miroir de QuitGesture : un UP
        // perdu ne doit jamais armer un futur appui).
        _downAt = null;
        UpdateRequested = false;
        Progress = 0;
        if (location.X >= screenWidth - _zoneSize && location.X <= screenWidth
            && location.Y >= 0 && location.Y < _zoneSize)
        {
            _downAt = now;
        }
    }

    public void Up(TimeSpan now)
    {
        Check(now);
        _downAt = null;
        if (!UpdateRequested)
        {
            Progress = 0;
        }
    }

    public void Tick(TimeSpan now) => Check(now);

    /// <summary>Réarme après un échec d'application (la maj reste proposée).</summary>
    public void Reset()
    {
        _downAt = null;
        Progress = 0;
        UpdateRequested = false;
    }

    private void Check(TimeSpan now)
    {
        if (_downAt is not { } downAt)
        {
            return;
        }
        double p = (now - downAt).TotalMilliseconds / HoldRequired.TotalMilliseconds;
        Progress = Math.Clamp(p, 0, 1);
        if (p >= 1)
        {
            UpdateRequested = true;
        }
    }
}
