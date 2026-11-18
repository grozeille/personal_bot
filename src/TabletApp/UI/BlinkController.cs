namespace TabletApp.UI;

/// <summary>
/// Clignement aléatoire à fréquence humaine (~15/min) : intervalle 2-6 s,
/// fermeture/ouverture en ~140 ms, avec doubles clignements occasionnels.
/// Logique pure (temps injecté) pour être testable sans UI (--self-test).
/// </summary>
internal sealed class BlinkController
{
    private static readonly TimeSpan BlinkDuration = TimeSpan.FromMilliseconds(140);
    private static readonly TimeSpan DoubleBlinkGap = TimeSpan.FromMilliseconds(200);

    private readonly Random _random;
    private TimeSpan _nextBlinkAt = TimeSpan.FromSeconds(1.5);
    private TimeSpan _blinkStartedAt;
    private bool _blinking;
    private bool _doubleBlinkQueued;

    /// <summary>1 = œil ouvert, 0 = œil fermé.</summary>
    public double OpenAmount { get; private set; } = 1.0;

    public bool IsBlinking => _blinking;

    public BlinkController() : this(new Random())
    {
    }

    public BlinkController(Random random)
    {
        _random = random;
    }

    public void Update(TimeSpan now)
    {
        if (!_blinking)
        {
            if (now >= _nextBlinkAt)
            {
                _blinking = true;
                _blinkStartedAt = now;
            }
            return;
        }

        double t = (now - _blinkStartedAt).TotalMilliseconds / BlinkDuration.TotalMilliseconds;
        if (t >= 1)
        {
            _blinking = false;
            OpenAmount = 1.0;
            if (_doubleBlinkQueued)
            {
                _doubleBlinkQueued = false;
                _nextBlinkAt = now + DoubleBlinkGap;
            }
            else
            {
                _doubleBlinkQueued = _random.NextDouble() < 0.15;
                _nextBlinkAt = now + TimeSpan.FromSeconds(2 + _random.NextDouble() * 4);
            }
            return;
        }

        // Descente puis remontée fluides (1 -> 0 -> 1).
        OpenAmount = 1.0 - Math.Sin(Math.PI * t);
    }
}
