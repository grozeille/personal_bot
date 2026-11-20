using System.Globalization;
using System.Runtime.InteropServices;

namespace TabletApp;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    /// <summary>Flag --face-size LxH (ex : 1280x800) : fenetre aux dimensions tablette pour le dev.</summary>
    internal static System.Drawing.Size? FaceSize { get; private set; }

    /// <summary>Flag --head-test [geste] : joue un geste (tete + visage + son) au demarrage.</summary>
    internal static bool AutoHeadTest { get; private set; }

    /// <summary>Nom du geste joue par --head-test (defaut : tilt_curious).</summary>
    internal static string AutoHeadGesture { get; private set; } = "tilt_curious";

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

        int headIdx = Array.IndexOf(args, "--head-test");
        AutoHeadTest = headIdx >= 0;
        if (AutoHeadTest && headIdx + 1 < args.Length && !args[headIdx + 1].StartsWith("--"))
        {
            AutoHeadGesture = args[headIdx + 1];
        }
        int sizeIdx = Array.IndexOf(args, "--face-size");
        if (sizeIdx >= 0 && sizeIdx + 1 < args.Length)
        {
            string[] wh = args[sizeIdx + 1].Split('x');
            if (wh.Length == 2 && int.TryParse(wh[0], out int w) && int.TryParse(wh[1], out int h)
                && w >= 320 && h >= 240 && w <= 4096 && h <= 4096)
            {
                FaceSize = new System.Drawing.Size(w, h);
            }
        }
        AutoVoiceTest = args.Contains("--voice-test");
        AutoDiscuss = args.Contains("--discuter");

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
