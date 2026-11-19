using System.Globalization;

namespace TabletApp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            Environment.Exit(SelfTest.Run() ? 0 : 1);
            return;
        }

        int t = Array.IndexOf(args, "--transition");
        if (t >= 0 && t + 3 < args.Length
            && double.TryParse(args[t + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out double progress))
        {
            Environment.Exit(SelfTest.RenderCustomTransition(args[t + 1], args[t + 2], progress) ? 0 : 1);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
