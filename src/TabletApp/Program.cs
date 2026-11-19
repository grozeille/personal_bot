using System.Globalization;

namespace TabletApp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Velopack.VelopackApp.Build().Run();

        if (args.Length >= 2 && args[0] == "--self-test-update")
        {
            if (args.Contains("--post-restart"))
            {
                string expected = args.Length >= 4 ? args[3] : "";
                Environment.Exit(SelfTest.RunUpdateProbePostRestart(args[1], expected) ? 0 : 1);
                return;
            }
            Environment.Exit(SelfTest.RunUpdateProbe(
                args[1], args.Contains("--apply-and-restart")) ? 0 : 1);
            return;
        }

        if (args.Contains("--self-test"))
        {
            Environment.Exit(SelfTest.Run() ? 0 : 1);
            return;
        }

        if (args.Contains("--perf"))
        {
            Environment.Exit(SelfTest.Perf() ? 0 : 1);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
