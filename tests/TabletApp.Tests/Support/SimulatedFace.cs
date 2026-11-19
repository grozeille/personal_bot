using TabletApp.UI;

namespace TabletApp.Tests.Support;

/// <summary>
/// Visage simulé headless : horloge + contrôleurs + historique pour assertions.
/// Même graine à chaque run pour des tests déterministes.
/// </summary>
internal sealed class SimulatedFace
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(16);
    private static readonly double[] GazeTargets = { -1.0, 0.0, 1.0 };

    private bool _wasBlinking;
    private double _settledGaze;

    public BlinkController Blink { get; } = new(new Random(42));
    public GazeController Gaze { get; } = new(new Random(7));
    public SleepController Sleep { get; } = new();
    public ExpressionController Expression { get; } = new();
    public TimeSpan Now { get; private set; }

    public List<double> BlinkStarts { get; } = new();
    public double MinBlinkOpen { get; private set; } = 1;
    public int Saccades { get; private set; }
    public double MinGazeX { get; private set; }
    public double MaxGazeX { get; private set; }
    public bool GazeBounded { get; private set; } = true;
    public double WakeMinOpen { get; private set; } = 1;
    public double WakeMaxOpen { get; private set; }

    /// <summary>Ouverture des yeux à la fin de la simulation (pour la capture finale).</summary>
    public double FinalOpen { get; private set; } = 1;

    public void Advance(TimeSpan duration)
    {
        TimeSpan end = Now + duration;
        while (Now < end)
        {
            Sleep.Update(Now);
            if (Sleep.IsAwake)
            {
                Blink.Update(Now);
                Gaze.Update(Now);
                TrackBlink();
                TrackGaze();
                FinalOpen = Blink.OpenAmount;
            }
            else
            {
                FinalOpen = Sleep.EyeOpen;
            }
            TrackSleep();
            Now += Step;
        }
    }

    private void TrackBlink()
    {
        if (Blink.IsBlinking && !_wasBlinking)
        {
            BlinkStarts.Add(Now.TotalSeconds);
        }
        _wasBlinking = Blink.IsBlinking;
        MinBlinkOpen = Math.Min(MinBlinkOpen, Blink.OpenAmount);
    }

    private void TrackGaze()
    {
        double x = Gaze.X;
        if (double.IsNaN(x) || x < -1.001 || x > 1.001)
        {
            GazeBounded = false;
        }
        MinGazeX = Math.Min(MinGazeX, x);
        MaxGazeX = Math.Max(MaxGazeX, x);
        double nearest = GazeTargets.MinBy(t => Math.Abs(t - x));
        if (Math.Abs(nearest - x) < 0.02 && nearest != _settledGaze)
        {
            _settledGaze = nearest;
            Saccades++;
        }
    }

    private void TrackSleep()
    {
        if (Sleep.Current == SleepController.State.Waking)
        {
            WakeMinOpen = Math.Min(WakeMinOpen, Sleep.EyeOpen);
            WakeMaxOpen = Math.Max(WakeMaxOpen, Sleep.EyeOpen);
        }
    }
}
