using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SkiaSharp.Views.Desktop;

namespace TabletApp.UI;

/// <summary>
/// Visage du robot : héberge le canvas SkiaSharp rafraîchi à ~60 FPS et
/// pilote le dessin (<see cref="FacePainter"/>) avec le clignement
/// (<see cref="BlinkController"/>), le regard de surveillance
/// (<see cref="GazeController"/>) et le cycle de sommeil
/// (<see cref="SleepController"/>, prioritaire quand il est actif).
/// </summary>
internal sealed class RobotFaceRenderer : UserControl
{
    private readonly SKControl _canvas;
    private readonly System.Windows.Forms.Timer _frameTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly BlinkController _blink = new();
    private readonly GazeController _gaze = new();
    private readonly SleepController _sleep = new();

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

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        TimeSpan now = _clock.Elapsed;
        _sleep.Update(now);

        // Le sommeil fige clignement et regard ; seul le cycle de sommeil pilote les yeux.
        double open;
        if (_sleep.IsAwake)
        {
            _blink.Update(now);
            _gaze.Update(now);
            open = _blink.OpenAmount;
        }
        else
        {
            open = _sleep.EyeOpen;
        }
        UpdateFps(now);

        FacePainter.DrawFace(
            e.Surface.Canvas, e.Info.Width, e.Info.Height,
            open, _gaze.X, _gaze.Y, _sleep.Yawn, now.TotalSeconds);
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
