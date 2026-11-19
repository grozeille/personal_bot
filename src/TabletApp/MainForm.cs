using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TabletApp.UI;

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
    private readonly System.Windows.Forms.Timer _quitTimer;
    private readonly QuitGesture _quit = new();
    private readonly QuitMessageFilter _quitFilter;

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
            else if (Digit(e.KeyCode) is { } digit)
            {
                _face.SetExpression(ExpressionKeys[digit]);
            }
        };

        _titleTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _titleTimer.Tick += (_, _) => Text = $"Robot Companion — {_face.Fps} FPS";
        _titleTimer.Start();

        _quitTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _quitTimer.Tick += (_, _) =>
        {
            _quit.Tick(Now());
            if (_quit.QuitRequested)
            {
                _quitTimer.Stop();
                Close();
            }
        };

        _quitFilter = new QuitMessageFilter(this);
        Application.AddMessageFilter(_quitFilter);
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

    private void OnQuitDown()
    {
        _quit.Down(PointToClient(MousePosition), Now());
        _quitTimer.Start();
    }

    private void OnQuitUp()
    {
        _quit.Up(Now());
        _quitTimer.Stop();
        if (_quit.QuitRequested)
        {
            Close();
        }
    }

    /// <summary>
    /// Les touchers/clics arrivent au contrôle sous le doigt (le canvas), pas au
    /// formulaire : ce filtre les intercepte au niveau applicatif pour le geste de sortie.
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
