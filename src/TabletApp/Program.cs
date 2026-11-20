using System.Globalization;
using System.Runtime.InteropServices;

namespace TabletApp;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    /// <summary>Flag --head-test : joue tilt_curious (tete + visage) au demarrage.</summary>
    internal static bool AutoHeadTest { get; private set; }

    /// <summary>Flag --voice-test : lance le scénario voix une fois au démarrage.</summary>
    internal static bool AutoVoiceTest { get; private set; }

    /// <summary>Flag --discuter : lance la discussion libre au démarrage.</summary>
    internal static bool AutoDiscuss { get; private set; }

    [STAThread]
    private static void Main(string[] args)
    {
        Velopack.VelopackApp.Build().Run();
        AttachConsole(-1); // Console parente si lancé depuis un terminal (logs voix).

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

        if (args.Contains("--sons"))
        {
            Environment.Exit(SelfTest.Sounds() ? 0 : 1);
            return;
        }

        if (args.Contains("--wake-probe"))
        {
            Environment.Exit(SelfTest.WakeProbe() ? 0 : 1);
            return;
        }

        AutoHeadTest = args.Contains("--head-test");
        AutoVoiceTest = args.Contains("--voice-test");
        AutoDiscuss = args.Contains("--discuter");

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
