namespace TabletApp.UI;

/// <summary>
/// Cycle de sommeil : bâillement (la bouche s'ouvre en cloche pendant que le
/// visage mute vers les yeux fatigués), sommeil (visage dormir affiché tel
/// quel), réveil (battement des paupières sur yeux neutres, bouche plate
/// gardée jusqu'au bout). Logique pure (temps injecté), testable sans UI.
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

    /// <summary>1 = yeux ouverts (battement pendant le réveil uniquement).</summary>
    public double EyeOpen { get; private set; } = 1.0;

    /// <summary>0 = normal, 1 = plissement maximal du bâillement.</summary>
    public double Yawn { get; private set; }

    /// <summary>Ouverture de la bouche en O : 0 -> 1 -> 0 sur le bâillement.</summary>
    public double MouthOpen { get; private set; }

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
                Yawn = Math.Sin(Math.PI * p);
                EyeOpen = 1.0;
                MouthOpen = Math.Sin(Math.PI * p);
                if (p >= 1)
                {
                    _state = State.Asleep;
                    Yawn = 0;
                    MouthOpen = 0;
                    EyeOpen = 1.0;
                }
                break;
            }
            case State.Asleep:
                Yawn = 0;
                EyeOpen = 1.0;
                MouthOpen = 0;
                break;
            case State.Waking:
            {
                double p = PhaseProgress(now, WakeDuration);
                Yawn = 0;
                MouthOpen = 0;
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
