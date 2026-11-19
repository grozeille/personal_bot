using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SkiaSharp.Views.Desktop;

namespace TabletApp.UI;

/// <summary>
/// Visage du robot : héberge le canvas SkiaSharp rafraîchi à ~60 FPS et
/// pilote le dessin (<see cref="FacePainter"/>) avec le clignement
/// (<see cref="BlinkController"/>), le regard de surveillance
/// (<see cref="GazeController"/>), les expressions
/// (<see cref="ExpressionController"/>) et le cycle de sommeil
/// (<see cref="SleepController"/>). Le sommeil force l'expression :
/// bâillement vers fatigue (bouche en O qui grandit), dormir, puis réveil
/// vers neutre (battement gardant la bouche plate jusqu'au bout).
/// </summary>
internal sealed class RobotFaceRenderer : UserControl
{
    private readonly SKControl _canvas;
    private readonly System.Windows.Forms.Timer _frameTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly BlinkController _blink = new();
    private readonly GazeController _gaze = new();
    private readonly ExpressionController _expression = new();
    private readonly SleepController _sleep = new();
    private SleepController.State _prevSleep = SleepController.State.Awake;

    private int _frames;
    private TimeSpan _fpsWindowStart;

    public int Fps { get; private set; }

    public RobotFaceRenderer()
    {
        BackColor = Color.Black;
        _canvas = new SKControl { Dock = DockStyle.Fill };
        _canvas.PaintSurface += OnPaintSurface;
        Controls.Add(_canvas);

        _frameTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _frameTimer.Tick += (_, _) => _canvas.Invalidate();
        _frameTimer.Start();
    }

    public void FallAsleep() => _sleep.FallAsleep(_clock.Elapsed);

    public void WakeUp() => _sleep.WakeUp(_clock.Elapsed);

    public void SetExpression(RobotExpression expression) =>
        _expression.SetExpression(expression, _clock.Elapsed);

    public void SetEmotion(string emotion) =>
        _expression.SetEmotion(emotion, _clock.Elapsed);

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        TimeSpan now = _clock.Elapsed;
        _sleep.Update(now);
        if (_sleep.Current != _prevSleep)
        {
            if (_sleep.Current == SleepController.State.Yawning)
            {
                _expression.SetExpression(RobotExpression.Tired, now);
            }
            else if (_sleep.Current == SleepController.State.Asleep)
            {
                _expression.SetExpression(RobotExpression.Sleep, now);
            }
            else if (_sleep.Current == SleepController.State.Waking)
            {
                _expression.SetExpression(RobotExpression.Neutral, now);
            }
            _prevSleep = _sleep.Current;
        }
        _expression.Update(now);

        double open;
        MouthOverride? mouthOverride = null;
        if (_sleep.IsAwake)
        {
            _blink.Update(now);
            _gaze.Update(now);
            open = FacePainter.Blinks(_expression.Current) ? _blink.OpenAmount : 1.0;
        }
        else if (_sleep.Current == SleepController.State.Waking)
        {
            open = _sleep.EyeOpen;
            mouthOverride = new MouthOverride(RobotExpression.Sleep, 1.0);
        }
        else if (_sleep.Current == SleepController.State.Yawning)
        {
            open = 1.0;
            mouthOverride = new MouthOverride(
                RobotExpression.Shocked, Math.Max(0.08, _sleep.MouthOpen));
        }
        else
        {
            open = 1.0;
        }
        UpdateFps(now);

        FacePainter.DrawFace(
            e.Surface.Canvas, e.Info.Width, e.Info.Height,
            open, _gaze.X, _gaze.Y, _sleep.Yawn, now.TotalSeconds,
            _expression.Current, _expression.Previous, _expression.Progress,
            mouthOverride, _sleep.IsAwake);
    }

    private void UpdateFps(TimeSpan now)
    {
        _frames++;
        if (now - _fpsWindowStart >= TimeSpan.FromSeconds(1))
        {
            Fps = _frames;
            _frames = 0;
            _fpsWindowStart = now;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frameTimer.Stop();
            _frameTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
