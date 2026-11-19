using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TabletApp.UI;
using TabletApp.Update;

namespace TabletApp;

internal sealed class MainForm : Form
{
    // Touches 1-9,0 : même ordre que les visages du dessin de référence (lecture 1-10).
    private static readonly RobotExpression[] ExpressionKeys =
    {
        RobotExpression.Neutral,
        RobotExpression.Dead,
        RobotExpression.Mean,
        RobotExpression.Dizzy,
        RobotExpression.Happy,
        RobotExpression.Tired,
        RobotExpression.Love,
        RobotExpression.Laugh,
        RobotExpression.Wink,
        RobotExpression.Sleep,
    };

    private readonly RobotFaceRenderer _face;
    private readonly System.Windows.Forms.Timer _titleTimer;
    private readonly System.Windows.Forms.Timer _holdTimer;
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly QuitGesture _quit = new();
    private readonly UpdateGesture _update = new();
    private readonly RobotUpdater _updater;
    private readonly QuitMessageFilter _quitFilter;
    private bool _applying;
    private bool _iconPreview;

    // Empêche la mise en veille de l'écran / du système (tête de robot allumée en continu).
    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;

    // Désactive le "clic droit sur appui long" tactile dans notre fenêtre.
    private const int WM_TABLET_QUERYSYSTEMGESTURESTATUS = 0x02CC;
    private const int TABLET_DISABLE_PRESSANDHOLD = 0x00000001;

    public MainForm()
    {
        Text = "Robot Companion";
        BackColor = Color.Black;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        WindowState = FormWindowState.Maximized;
        TopMost = true;

        _face = new RobotFaceRenderer { Dock = DockStyle.Fill };
        Controls.Add(_face);

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.S)
            {
                _face.FallAsleep();
            }
            else if (e.KeyCode == Keys.W)
            {
                _face.WakeUp();
            }
            else if (e.KeyCode == Keys.F11)
            {
                _face.SetExpression(RobotExpression.Thinking);
            }
            else if (e.KeyCode == Keys.F12)
            {
                _face.SetExpression(RobotExpression.Shocked);
            }
            else if (e.KeyCode == Keys.U)
            {
                // Dev : forcer une vérification de maj (le robot le fait seul toutes les 5 min).
                _ = CheckForUpdatesAsync();
            }
            else if (e.KeyCode == Keys.I)
            {
                // Dev : aperçu de l'icône de maj (sans NAS), y compris le
                // feedback d'appui. Sans effet sur la vraie détection.
                _iconPreview = !_iconPreview;
                UpdateFaceIcon();
            }
            else if (Digit(e.KeyCode) is { } digit)
            {
                _face.SetExpression(ExpressionKeys[digit]);
            }
        };

        UpdateOptions options = UpdateOptions.Load();
        _updater = new RobotUpdater(new VelopackUpdateSource(options.Source));

        _titleTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _titleTimer.Tick += (_, _) =>
            Text = $"Robot Companion — {_face.Fps} FPS"
                + (_updater.UpdateAvailable ? " — MAJ !" : "")
                + (_iconPreview ? " — APERÇU" : "");
        _titleTimer.Start();

        _holdTimer = new System.Windows.Forms.Timer { Interval = 100 };
        _holdTimer.Tick += (_, _) =>
        {
            if (!Control.MouseButtons.HasFlag(MouseButtons.Left))
            {
                // Doigt relevé (ou UP perdu) : on désarme les deux gestes
                // au lieu de quitter / mettre à jour plus tard.
                _holdTimer.Stop();
                _quit.Reset();
                _update.Reset();
                _face.UpdateProgress = 0;
                return;
            }
            _quit.Tick(Now());
            _update.Tick(Now());
            _face.UpdateProgress = _update.Progress;
            if (_quit.QuitRequested)
            {
                _holdTimer.Stop();
                Close();
            }
            else if (_update.UpdateRequested && !_applying)
            {
                _holdTimer.Stop();
                _ = DownloadAndApplyAsync();
            }
        };

        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(60_000, (int)options.CheckInterval.TotalMilliseconds),
        };
        _updateTimer.Tick += (_, _) => _ = CheckForUpdatesAsync();
        _updateTimer.Start();

        _quitFilter = new QuitMessageFilter(this);
        Application.AddMessageFilter(_quitFilter);

        // Premier contrôle peu après le démarrage (silencieux si NAS hors-ligne).
        Shown += (_, _) => _ = CheckForUpdatesAsync();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        // Vrai plein écran : couvre aussi la barre des tâches (Maximized seul la respecte).
        WindowState = FormWindowState.Normal;
        Bounds = Screen.FromControl(this).Bounds;
        Cursor.Hide();
        SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_TABLET_QUERYSYSTEMGESTURESTATUS)
        {
            m.Result = (IntPtr)TABLET_DISABLE_PRESSANDHOLD;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Application.RemoveMessageFilter(_quitFilter);
        SetThreadExecutionState(ES_CONTINUOUS);
        Cursor.Show();
        base.OnFormClosed(e);
    }

    private static TimeSpan Now() => TimeSpan.FromMilliseconds(Environment.TickCount64);

    private static int? Digit(Keys key) => key switch
    {
        >= Keys.D1 and <= Keys.D9 => (int)(key - Keys.D1),
        Keys.D0 => 9,
        >= Keys.NumPad1 and <= Keys.NumPad9 => (int)(key - Keys.NumPad1),
        Keys.NumPad0 => 9,
        _ => null,
    };

    private async Task CheckForUpdatesAsync()
    {
        if (_applying)
        {
            return;
        }
        await _updater.CheckAsync().ConfigureAwait(true);
        UpdateFaceIcon();
        if (!_updater.UpdateAvailable)
        {
            _update.Reset();
            _face.UpdateProgress = 0;
        }
    }

    private void UpdateFaceIcon() =>
        _face.UpdateAvailable = _updater.UpdateAvailable || _iconPreview;

    private async Task DownloadAndApplyAsync()
    {
        _applying = true;
        _face.UpdateProgress = 1;
        await _updater.DownloadAndApplyAsync().ConfigureAwait(true);
        // Succès = Velopack a relancé l'app (on ne revient pas ici).
        // Échec = on réarme le geste, la maj reste proposée.
        _update.Reset();
        _face.UpdateProgress = 0;
        UpdateFaceIcon();
        _applying = false;
    }

    private void OnQuitDown()
    {
        Point location = PointToClient(MousePosition);
        _quit.Down(location, Now());
        if (_face.UpdateAvailable)
        {
            // Icône visible (vraie maj ou aperçu dev) : l'appui maintenu
            // est suivi ; sans maj réelle, l'installation est sans effet.
            _update.Down(location, ClientSize.Width, Now());
        }
        _holdTimer.Start();
    }

    private void OnQuitUp()
    {
        _quit.Up(Now());
        _update.Up(Now());
        _face.UpdateProgress = _update.Progress;
        _holdTimer.Stop();
        if (_quit.QuitRequested)
        {
            Close();
        }
        else if (_update.UpdateRequested && !_applying)
        {
            _ = DownloadAndApplyAsync();
        }
    }

    /// <summary>
    /// Les touchers/clics arrivent au contrôle sous le doigt (le canvas), pas au
    /// formulaire : ce filtre les intercepte au niveau applicatif pour les
    /// gestes maintenus (sortie en haut-gauche, mise à jour en haut-droite).
    /// </summary>
    private sealed class QuitMessageFilter : IMessageFilter
    {
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;

        private readonly MainForm _form;

        public QuitMessageFilter(MainForm form)
        {
            _form = form;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_LBUTTONDOWN)
            {
                _form.OnQuitDown();
            }
            else if (m.Msg == WM_LBUTTONUP)
            {
                _form.OnQuitUp();
            }
            return false;
        }
    }
}
