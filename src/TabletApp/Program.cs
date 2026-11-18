namespace TabletApp;

// Fenêtre placeholder (issue #1) : prouve le pipeline WinForms win-x86.
// Le rendu SkiaSharp 60 FPS arrive avec l'issue #2 (UI/RobotFaceRenderer.cs).
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Form
        {
            Text = "Robot Companion",
            BackColor = Color.Black,
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Maximized,
        });
    }
}
