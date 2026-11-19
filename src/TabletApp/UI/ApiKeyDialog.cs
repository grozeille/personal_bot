using System.Drawing;
using System.Windows.Forms;

namespace TabletApp.UI;

/// <summary>
/// Dialogue de première installation : saisit la clé API Gemini quand elle
/// est absente et la stocke (User Secrets). Grandes zones tactiles.
/// </summary>
internal sealed class ApiKeyDialog : Form
{
    private readonly TextBox _box;

    public string ApiKey { get; private set; } = "";

    public ApiKeyDialog()
    {
        Text = "Clé API Gemini";
        Size = new Size(620, 250);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        BackColor = Color.Black;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 14);

        var label = new Label
        {
            Text = "Colle ta clé API Gemini (AI Studio) :",
            Dock = DockStyle.Top,
            Height = 45,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
        };
        _box = new TextBox
        {
            UseSystemPasswordChar = true,
            Dock = DockStyle.Top,
            Height = 45,
            Font = new Font("Segoe UI", 14),
        };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 70,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 10, 12, 0),
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 130, Height = 45 };
        var cancel = new Button { Text = "Plus tard", DialogResult = DialogResult.Cancel, Width = 130, Height = 45 };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        Controls.Add(_box);
        Controls.Add(label);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            ApiKey = _box.Text.Trim();
        }
        base.OnFormClosing(e);
    }
}
