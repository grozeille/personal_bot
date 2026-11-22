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

        if (args.Contains("--wake-record"))
        {
            Environment.Exit(SelfTest.WakeRecord() ? 0 : 1);
            return;
        }

        if (args.Contains("--live-record"))
        {
            int at = Array.IndexOf(args, "--live-record");
            string[] rest = at >= 0
                ? args.Skip(at + 1).Where(a => !a.StartsWith("--")).ToArray()
                : Array.Empty<string>();
            int seconds = 30;
            string? path = null;
            foreach (string a in rest)
            {
                if (int.TryParse(a, out int s) && s > 0)
                {
                    seconds = Math.Min(s, 120);
                }
                else
                {
                    path = a;
                }
            }
            Environment.Exit(SelfTest.LiveRecord(seconds, path) ? 0 : 1);
            return;
        }

        if (args.Contains("--live-sim"))
        {
            int at = Array.IndexOf(args, "--live-sim");
            string? path = at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--")
                ? args[at + 1]
                : null;
            Environment.Exit(SelfTest.LiveSim(path) ? 0 : 1);
            return;
        }

        if (args.Contains("--enroll"))
        {
            Environment.Exit(SelfTest.Enroll() ? 0 : 1);
            return;
        }

        if (args.Contains("--enroll-sim"))
        {
            int at = Array.IndexOf(args, "--enroll-sim");
            var files = new List<string>();
            for (int i = at + 1; i < args.Length && !args[i].StartsWith("--"); i++)
            {
                files.Add(args[i]);
            }
            Environment.Exit(SelfTest.EnrollSim(files) ? 0 : 1);
            return;
        }

        if (args.Contains("--wake-pick"))
        {
            int at = Array.IndexOf(args, "--wake-pick");
            bool apply = args.Contains("--apply");
            string? silence = null;
            var takes = new List<string>();
            for (int i = at + 1; i < args.Length; i++)
            {
                if (args[i] == "--apply")
                {
                    continue;
                }
                if (args[i] == "--silence" && i + 1 < args.Length)
                {
                    silence = args[++i];
                    continue;
                }
                if (args[i].StartsWith("--"))
                {
                    break;
                }
                takes.Add(args[i]);
            }
            if (takes.Count == 0)
            {
                Console.WriteLine("Usage : --wake-pick prise1.wav [prise2...] [--silence s.wav] [--apply].");
                Environment.Exit(1);
                return;
            }
            Environment.Exit(SelfTest.WakePick(takes, silence, apply) ? 0 : 1);
            return;
        }

        if (args.Contains("--wake-sweep"))
        {
            int at = Array.IndexOf(args, "--wake-sweep");
            string[] rest = at >= 0
                ? args.Skip(at + 1).Where(a => !a.StartsWith("--")).ToArray()
                : Array.Empty<string>();
            string? path = null;
            double gain = 1.0;
            if (rest.Length == 1 && double.TryParse(rest[0],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double only))
            {
                gain = only;
            }
            else
            {
                if (rest.Length >= 1)
                {
                    path = rest[0];
                }
                if (rest.Length >= 2 && double.TryParse(rest[1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double g2))
                {
                    gain = g2;
                }
            }
            Environment.Exit(SelfTest.WakeSweep(path, gain) ? 0 : 1);
            return;
        }

        if (args.Contains("--wake-decode"))
        {
            int at = Array.IndexOf(args, "--wake-decode");
            string[] rest = at >= 0
                ? args.Skip(at + 1).Where(a => !a.StartsWith("--")).ToArray()
                : Array.Empty<string>();
            string? path = null;
            double gain = 1.0;
            if (rest.Length == 1 && double.TryParse(rest[0],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double only))
            {
                gain = only;
            }
            else
            {
                if (rest.Length >= 1)
                {
                    path = rest[0];
                }
                if (rest.Length >= 2 && double.TryParse(rest[1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double g2))
                {
                    gain = g2;
                }
            }
            Environment.Exit(SelfTest.WakeDecode(path, gain) ? 0 : 1);
            return;
        }

        if (args.Contains("--say"))
        {
            int at = Array.IndexOf(args, "--say");
            string? text = at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--")
                ? args[at + 1]
                : null;
            Environment.Exit(SelfTest.Say(text) ? 0 : 1);
            return;
        }

        if (args.Contains("--wake-try"))
        {
            int at = Array.IndexOf(args, "--wake-try");
            string[] rest = at >= 0
                ? args.Skip(at + 1).Where(a => !a.StartsWith("--")).ToArray()
                : Array.Empty<string>();
            string? keywords = rest.Length >= 1 ? rest[0] : null;
            string? path = null;
            float threshold = 0.05f;
            if (rest.Length >= 2)
            {
                if (double.TryParse(rest[1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double g))
                {
                    threshold = (float)g;
                }
                else
                {
                    path = rest[1];
                }
            }
            if (rest.Length >= 3 && double.TryParse(rest[2],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double t2))
            {
                threshold = (float)t2;
            }
            float score = 1.0f;
            if (rest.Length >= 4 && double.TryParse(rest[3],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double s2))
            {
                score = (float)s2;
            }
            Environment.Exit(SelfTest.WakeTry(keywords, path, threshold, score) ? 0 : 1);
            return;
        }

        if (args.Contains("--wake-slice"))
        {
            int at = Array.IndexOf(args, "--wake-slice");
            string[] rest = at >= 0
                ? args.Skip(at + 1).Where(a => !a.StartsWith("--")).ToArray()
                : Array.Empty<string>();
            string? path = null;
            string? dest = null;
            var nums = new System.Collections.Generic.List<double>();
            var wavs = new System.Collections.Generic.List<string>();
            foreach (string a in rest)
            {
                if (double.TryParse(a,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double v))
                {
                    nums.Add(v);
                }
                else
                {
                    wavs.Add(a);
                }
            }
            if (wavs.Count == 1)
            {
                dest = wavs[0];
            }
            else if (wavs.Count >= 2)
            {
                path = wavs[0];
                dest = wavs[1];
            }
            if (nums.Count < 2)
            {
                Console.WriteLine("Usage : --wake-slice [wav] t0 t1 [dst.wav].");
                Environment.Exit(1);
                return;
            }
            Environment.Exit(SelfTest.WakeSlice(path, nums[0], nums[1], dest) ? 0 : 1);
            return;
        }

        if (args.Contains("--wake-live"))
        {
            int at = Array.IndexOf(args, "--wake-live");
            string[] rest = at >= 0
                ? args.Skip(at + 1).Where(a => !a.StartsWith("--")).ToArray()
                : Array.Empty<string>();
            string? path = null;
            string? keywords = null;
            double gate = 150.0;
            double only = 150.0;
            var items = rest.ToList();
            int kidx = items.FindIndex(a => a.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
            if (kidx >= 0)
            {
                keywords = items[kidx];
                items.RemoveAt(kidx);
            }
            string[] r = items.ToArray();
            if (r.Length >= 1 && !double.TryParse(r[0],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out only))
            {
                path = r[0];
            }
            else if (r.Length == 1)
            {
                gate = only;
            }
            if (r.Length >= 2 && double.TryParse(r[1],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double g2))
            {
                gate = g2;
            }
            Environment.Exit(SelfTest.WakeLive(path, gate, keywords) ? 0 : 1);
            return;
        }

        if (args.Contains("--stt-file"))
        {
            int at = Array.IndexOf(args, "--stt-file");
            string? path = at >= 0 && at + 1 < args.Length && !args[at + 1].StartsWith("--")
                ? args[at + 1]
                : null;
            Environment.Exit(SelfTest.SttFile(path) ? 0 : 1);
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
