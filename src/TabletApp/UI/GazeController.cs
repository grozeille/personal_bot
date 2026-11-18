namespace TabletApp.UI;

/// <summary>
/// Balayage de surveillance autonome : le regard alterne entre gauche (-1),
/// centre (0) et droite (+1) avec des saccades rapides et des pauses aléatoires.
/// Logique pure (temps injecté) pour être testable sans UI (--self-test).
/// </summary>
internal sealed class GazeController
{
    private static readonly double[] Targets = { -1.0, 0.0, 1.0 };

    private readonly Random _random;
    private double _fromX;
    private double _toX;
    private TimeSpan _moveStartedAt;
    private TimeSpan _moveDuration = TimeSpan.FromMilliseconds(180);
    private TimeSpan _holdUntil = TimeSpan.FromSeconds(1);
    private bool _moving;

    /// <summary>Position horizontale du regard, -1 (gauche) à +1 (droite).</summary>
    public double X { get; private set; }

    /// <summary>
    /// Position verticale (-1 bas, +1 haut). Fixe à 0 pour l'instant :
    /// pilotée plus tard par le suivi du visage (issue #6).
    /// </summary>
    public double Y { get; set; }

    public GazeController() : this(new Random())
    {
    }

    public GazeController(Random random)
    {
        _random = random;
    }

    public void Update(TimeSpan now)
    {
        if (_moving)
        {
            double t = (now - _moveStartedAt).TotalMilliseconds / _moveDuration.TotalMilliseconds;
            if (t >= 1)
            {
                _moving = false;
                X = _toX;
                _holdUntil = now + TimeSpan.FromSeconds(1.2 + _random.NextDouble() * 2);
                return;
            }
            X = _fromX + (_toX - _fromX) * EaseOutCubic(t);
            return;
        }

        if (now >= _holdUntil)
        {
            _fromX = X;
            _toX = PickNewTarget();
            _moveStartedAt = now;
            _moveDuration = TimeSpan.FromMilliseconds(160 + _random.NextDouble() * 60);
            _moving = true;
        }
    }

    private double PickNewTarget()
    {
        double current = Targets.MinBy(t => Math.Abs(t - X));
        double next;
        do
        {
            next = Targets[_random.Next(Targets.Length)];
        } while (next == current);
        return next;
    }

    private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);
}
