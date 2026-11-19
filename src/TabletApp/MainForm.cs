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
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly ConfirmDialog _confirm = new();
    private readonly RobotUpdater _updater;
    private readonly TapMessageFilter _tapFilter;
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

        UpdateOptions options = UpdateOptions.Load();
        _updater = new RobotUpdater(new VelopackUpdateSource(options.Source));
        _face.Idle = IdleOptions.Load();

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.S)
            {
                // Toute touche est une activité (réarme l'endormissement,
                // réveille). S gère son cas plus bas (pas de réveil).
                _face.NotifyActivity();
            }
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.S)
            {
                _face.NotifyActivity(wake: false);
                _face.FallAsleep();
            }
            else if (e.KeyCode == Keys.W)
            {
                _face.WakeUp();
            }
            else if (e.KeyCode == Keys.F11)
            {
                _confirm.Cancel();
                _face.SetExpression(RobotExpression.Thinking);
            }
            else if (e.KeyCode == Keys.F12)
            {
                _confirm.Cancel();
                _face.SetExpression(RobotExpression.Shocked);
            }
            else if (e.KeyCode == Keys.U)
            {
                // Dev : forcer une vérification de maj (le robot le fait seul toutes les 5 min).
                _ = CheckForUpdatesAsync();
            }
            else if (e.KeyCode == Keys.C)
            {
                _confirm.Cancel();
                _face.SetExpression(RobotExpression.Confirm);
            }
            else if (e.KeyCode == Keys.I)
            {
                // Dev : aperçu de l'icône de maj (sans NAS), tap icône puis
                // yeux testables comme une vraie maj. Sans effet sur la détection.
                _iconPreview = !_iconPreview;
                UpdateFaceIcon();
            }
            else if (Digit(e.KeyCode) is { } digit)
            {
                _confirm.Cancel();
                _face.SetExpression(ExpressionKeys[digit]);
            }
        };

        _titleTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _titleTimer.Tick += (_, _) =>
            Text = $"Robot Companion — {_face.Fps} FPS"
                + (_updater.UpdateAvailable ? " — MAJ !" : "")
                + (_iconPreview ? " — APERÇU" : "");
        _titleTimer.Start();

        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(60_000, (int)options.CheckInterval.TotalMilliseconds),
        };
        _updateTimer.Tick += (_, _) => _ = CheckForUpdatesAsync();
        _updateTimer.Start();

        _tapFilter = new TapMessageFilter(this);
        Application.AddMessageFilter(_tapFilter);

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
        Application.RemoveMessageFilter(_tapFilter);
        SetThreadExecutionState(ES_CONTINUOUS);
        Cursor.Show();
        base.OnFormClosed(e);
    }

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
        if (!_updater.UpdateAvailable && _confirm.Active && !_applying)
        {
            // Maj disparue (NAS débranché) : on referme le dialogue.
            _confirm.Cancel();
            _face.SetExpression(_confirm.Previous);
        }
    }

    private void UpdateFaceIcon() =>
        _face.UpdateAvailable = _updater.UpdateAvailable || _iconPreview;

    private async Task DownloadAndApplyAsync()
    {
        _applying = true;
        _confirm.Cancel();
        _face.SetExpression(_confirm.Previous);
        await _updater.DownloadAndApplyAsync().ConfigureAwait(true);
        // Succès = Velopack a relancé l'app (on ne revient pas ici).
        // Échec = la maj reste proposée.
        UpdateFaceIcon();
        _applying = false;
    }

    private void OnTapDown()
    {
        // Tout toucher est une activité (réveille sur tablette sans clavier).
        _face.NotifyActivity();
        // Que des taps simples : pas d'appui long (Windows le promeut en
        // clic droit sur la tablette). Coin haut-gauche = sortir (toujours),
        // icône haut-droite = maj (si affichée).
        Point location = PointToClient(MousePosition);
        if (_confirm.Active)
        {
            (float vx, float vy) = FacePainter.ToView(
                location.X, location.Y, ClientSize.Width, ClientSize.Height);
            switch (_confirm.Tap(vx, vy))
            {
                case ConfirmTap.Cancelled:
                    _face.SetExpression(_confirm.Previous);
                    break;
                case ConfirmTap.Confirmed:
                    if (_confirm.Purpose == ConfirmPurpose.Quit)
                    {
                        Close();
                    }
                    else
                    {
                        _face.SetExpression(_confirm.Previous);
                        if (!_applying)
                        {
                            _ = DownloadAndApplyAsync();
                        }
                    }
                    break;
                case ConfirmTap.Ignored:
                    break;
            }
            return;
        }
        if (ConfirmDialog.QuitHit(location))
        {
            _confirm.Show(_face.CurrentExpression, ConfirmPurpose.Quit);
            _face.SetExpression(RobotExpression.Confirm);
        }
        else if (_face.UpdateAvailable && !_applying
            && ConfirmDialog.IconHit(location, ClientSize.Width))
        {
            _confirm.Show(_face.CurrentExpression, ConfirmPurpose.Update);
            _face.SetExpression(RobotExpression.Confirm);
        }
    }

    /// <summary>
    /// Les touchers/clics arrivent au contrôle sous le doigt (le canvas), pas au
    /// formulaire : ce filtre les intercepte au niveau applicatif pour les
    /// taps (sortie en haut-gauche, maj en haut-droite puis yeux).
    /// </summary>
    private sealed class TapMessageFilter : IMessageFilter
    {
        private const int WM_LBUTTONDOWN = 0x0201;

        private readonly MainForm _form;

        public TapMessageFilter(MainForm form)
        {
            _form = form;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_LBUTTONDOWN)
            {
                _form.OnTapDown();
            }
            return false;
        }
    }
}
