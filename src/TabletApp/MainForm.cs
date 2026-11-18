using System.Drawing;
using System.Windows.Forms;
using TabletApp.UI;

namespace TabletApp;

internal sealed class MainForm : Form
{
    private readonly RobotFaceRenderer _face;
    private readonly System.Windows.Forms.Timer _titleTimer;

    public MainForm()
    {
        Text = "Robot Companion";
        BackColor = Color.Black;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };

        _face = new RobotFaceRenderer { Dock = DockStyle.Fill };
        Controls.Add(_face);

        _titleTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _titleTimer.Tick += (_, _) => Text = $"Robot Companion — {_face.Fps} FPS";
        _titleTimer.Start();
    }
}
