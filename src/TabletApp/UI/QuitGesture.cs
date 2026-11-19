using System.Drawing;

namespace TabletApp.UI;

/// <summary>
/// Geste de sortie tactile (pas de clavier sur la tablette) : appui maintenu
/// 3 secondes dans le coin haut-gauche. Logique pure testable (tests BDD).
/// </summary>
internal sealed class QuitGesture
{
    private static readonly TimeSpan HoldRequired = TimeSpan.FromSeconds(3);

    private readonly float _zoneSize;
    private TimeSpan? _downAt;

    public bool QuitRequested { get; private set; }

    public QuitGesture(float zoneSize = 120)
    {
        _zoneSize = zoneSize;
    }

    public void Down(PointF location, TimeSpan now)
    {
        // Tout nouvel appui repart de zéro (un UP perdu ne doit jamais armer un futur appui).
        _downAt = null;
        QuitRequested = false;
        if (location.X >= 0 && location.Y >= 0
            && location.X < _zoneSize && location.Y < _zoneSize)
        {
            _downAt = now;
        }
    }

    public void Reset()
    {
        _downAt = null;
        QuitRequested = false;
    }

    public void Up(TimeSpan now)
    {
        Check(now);
        _downAt = null;
    }

    public void Tick(TimeSpan now) => Check(now);

    private void Check(TimeSpan now)
    {
        if (_downAt is { } downAt && now - downAt >= HoldRequired)
        {
            QuitRequested = true;
        }
    }
}
