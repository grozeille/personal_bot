namespace TabletApp.UI;

/// <summary>
/// Cycle de sommeil : bâillement (~2,2 s, yeux qui se plissent en se fermant),
/// sommeil statique (yeux fermés, aucune animation), réveil (~1,8 s,
/// paupières qui battent avant de s'ouvrir en grand).
/// Logique pure (temps injecté) pour être testable sans UI (--self-test).
/// </summary>
internal sealed class SleepController
{
    public enum State
    {
        Awake,
        Yawning,
        Asleep,
        Waking,
    }

    private static readonly TimeSpan YawnDuration = TimeSpan.FromMilliseconds(2200);
    private static readonly TimeSpan WakeDuration = TimeSpan.FromMilliseconds(1800);

    private State _state = State.Awake;
    private TimeSpan _phaseStartedAt;

    public State Current => _state;
    public bool IsAwake => _state == State.Awake;

    /// <summary>0 = yeux fermés, 1 = yeux ouverts (pilote le rendu hors phase Awake).</summary>
    public double EyeOpen { get; private set; } = 1.0;

    /// <summary>0 = normal, 1 = plissement maximal du bâillement.</summary>
    public double Yawn { get; private set; }

    public void FallAsleep(TimeSpan now)
    {
        if (_state == State.Awake)
        {
            _state = State.Yawning;
            _phaseStartedAt = now;
        }
    }

    public void WakeUp(TimeSpan now)
    {
        if (_state == State.Asleep || _state == State.Yawning)
        {
            _state = State.Waking;
            _phaseStartedAt = now;
        }
    }

    public void Update(TimeSpan now)
    {
        switch (_state)
        {
            case State.Yawning:
            {
                double p = PhaseProgress(now, YawnDuration);
                Yawn = Math.Sin(Math.PI * p); // monte puis redescend
                EyeOpen = 1.0 - Smooth(p); // se ferment
                if (p >= 1)
                {
                    _state = State.Asleep;
                    Yawn = 0;
                    EyeOpen = 0;
                }
                break;
            }
            case State.Asleep:
                Yawn = 0;
                EyeOpen = 0;
                break;
            case State.Waking:
            {
                double p = PhaseProgress(now, WakeDuration);
                Yawn = 0;
                // 60 % : battements partiels ; 40 % : ouverture franche.
                EyeOpen = p < 0.6
                    ? 0.45 * Math.Abs(Math.Sin(Math.PI * p / 0.6 * 2))
                    : Smooth((p - 0.6) / 0.4);
                if (p >= 1)
                {
                    _state = State.Awake;
                    EyeOpen = 1.0;
                }
                break;
            }
        }
    }

    private double PhaseProgress(TimeSpan now, TimeSpan duration) =>
        Math.Min(1.0, (now - _phaseStartedAt).TotalMilliseconds / duration.TotalMilliseconds);

    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }
}
