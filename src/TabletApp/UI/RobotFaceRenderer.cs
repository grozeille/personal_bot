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
/// (<see cref="ExpressionController"/>, squash-swap-stretch) et le cycle de
/// sommeil (<see cref="SleepController"/>).
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
    private IdleOptions _idleOptions = IdleOptions.Default;
    private IdleController _idle;

    private int _frames;
    private TimeSpan _fpsWindowStart;

    public int Fps { get; private set; }

    /// <summary>Début du bâillement / réveil (sons système).</summary>
    public event Action? YawnStarted;
    public event Action? WokeUp;

    /// <summary>Maj disponible : affiche l'icône en haut à droite.</summary>
    public bool UpdateAvailable { get; set; }

    /// <summary>Maj en cours : points animés à la place du visage. Mis par MainForm.</summary>
    public bool BusyDots { get; set; }

    /// <summary>Parole en cours : bouche grande ouverte (rire). Mis par MainForm.</summary>
    public bool Speaking { get; set; }

    /// <summary>Concentration (discussion) : regard recentré figé, sans clignement.</summary>
    public bool Concentrated { get; set; }

    /// <summary>Halo voix : fixe en écoute, pulsé en réflexion.</summary>
    public VoiceGlow Glow { get; set; }

    /// <summary>Partie de Uno : table affichée à la place du visage. Mis par MainForm.</summary>
    public UnoTableState? UnoTable { get; set; }

    private bool _wasSpeaking;
    private TimeSpan? _speakEdgeAt;
    private bool _wasConcentrated;
    private double _calmX;
    private double _calmY;
    private TimeSpan _prevFrameAt = TimeSpan.MinValue;

    /// <summary>Expression affichée (pour restaurer après la confirmation).</summary>
    public RobotExpression CurrentExpression => _expression.Current;

    /// <summary>
    /// Seuils d'endormissement (recharge le pilote d'inactivité).
    /// Positionné au démarrage depuis appsettings.json.
    /// </summary>
    public IdleOptions Idle
    {
        get => _idleOptions;
        set
        {
            _idleOptions = value;
            _idle = new IdleController(value.TiredAfter, value.SleepAfter);
        }
    }

    public RobotFaceRenderer()
    {
        _idle = new IdleController(_idleOptions.TiredAfter, _idleOptions.SleepAfter);
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

    public void SetExpression(RobotExpression expression)
    {
        UnoTable = null; // Un visage demandé chasse la table de Uno.
        _expression.SetExpression(expression, _clock.Elapsed);
    }

    public void SetEmotion(string emotion)
    {
        UnoTable = null;
        _expression.SetEmotion(emotion, _clock.Elapsed);
    }

    /// <summary>
    /// Activité (touche, toucher, futur mot-clé) : réarme l'endormissement,
    /// réveille si endormi, restaure neutre si fatigué automatique.
    /// </summary>
    public void NotifyActivity(bool wake = true)
    {
        TimeSpan now = _clock.Elapsed;
        bool wasAutoTired = _idle.NotifyActivity(now);
        if (wake && !_sleep.IsAwake)
        {
            _sleep.WakeUp(now);
        }
        else if (wasAutoTired && _sleep.IsAwake)
        {
            _expression.SetExpression(RobotExpression.Neutral, now);
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        TimeSpan now = _clock.Elapsed;
        double frameDt = _prevFrameAt == TimeSpan.MinValue
            ? 0 : (now - _prevFrameAt).TotalSeconds;
        _prevFrameAt = now;
        if (Speaking != _wasSpeaking)
        {
            _speakEdgeAt = now;
            _wasSpeaking = Speaking;
        }
        _sleep.Update(now);
        if (_sleep.IsAwake)
        {
            if (_idle.SleepDue(now))
            {
                _sleep.FallAsleep(now);
            }
            else if (_idle.PollTired(now))
            {
                _expression.SetExpression(RobotExpression.Tired, now);
            }
        }
        if (_sleep.Current != _prevSleep)
        {
            if (_sleep.Current == SleepController.State.Yawning)
            {
                _expression.SetExpression(RobotExpression.Tired, now);
                YawnStarted?.Invoke();
            }
            else if (_sleep.Current == SleepController.State.Asleep)
            {
                _expression.SetExpression(RobotExpression.Sleep, now);
            }
            else if (_sleep.Current == SleepController.State.Waking)
            {
                _expression.SetExpression(RobotExpression.Neutral, now);
                WokeUp?.Invoke();
            }
            _prevSleep = _sleep.Current;
        }
        _expression.Update(now);

        double open;
        double gazeX = _gaze.X;
        double gazeY = _gaze.Y;
        MouthOverride? mouthOverride = null;
        if (_sleep.IsAwake)
        {
            if (Concentrated)
            {
                if (!_wasConcentrated)
                {
                    _calmX = _gaze.X;
                    _calmY = _gaze.Y;
                    _wasConcentrated = true;
                }
                (_calmX, _calmY) = EaseGaze(_calmX, _calmY, frameDt);
                gazeX = _calmX;
                gazeY = _calmY;
                open = 1.0;
            }
            else
            {
                _wasConcentrated = false;
                _blink.Update(now);
                _gaze.Update(now);
                gazeX = _gaze.X;
                gazeY = _gaze.Y;
                open = FacePainter.Blinks(_expression.Current) ? _blink.OpenAmount : 1.0;
            }
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
        if (mouthOverride is null && _sleep.IsAwake)
        {
            // Squash de la bouche de parole (150 ms comme les transitions).
            double speakT = _speakEdgeAt is null
                ? 1.0
                : Math.Min(1.0, (now - _speakEdgeAt.Value).TotalMilliseconds / 150.0);
            if (Speaking)
            {
                mouthOverride = new MouthOverride(RobotExpression.Laugh, SpeakEase(speakT));
            }
            else if (speakT < 1.0)
            {
                mouthOverride = new MouthOverride(RobotExpression.Laugh, SpeakEase(1.0 - speakT));
            }
        }
        UpdateFps(now);

        if (UnoTable is not null)
        {
            UnoTablePainter.DrawTable(e.Surface.Canvas, e.Info.Width, e.Info.Height, UnoTable);
            return;
        }
        FacePainter.DrawFace(
            e.Surface.Canvas, e.Info.Width, e.Info.Height,
            open, gazeX, gazeY, _sleep.Yawn, now.TotalSeconds,
            _expression.Current, _expression.Previous, _expression.Progress,
            mouthOverride, _sleep.IsAwake,
            updateAvailable: UpdateAvailable, glow: Glow, busyDots: BusyDots);
    }

    private static float SpeakEase(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return (float)Math.Max(0.02, t * t * (3 - 2 * t));
    }

    internal static (double X, double Y) EaseGaze(double x, double y, double dtSeconds)
    {
        double k = Math.Min(1, Math.Max(0, dtSeconds) * 6);
        return (x + (0 - x) * k, y + (0 - y) * k);
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
