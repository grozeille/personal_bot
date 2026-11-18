using System.Runtime.InteropServices;
using SkiaSharp;
using TabletApp.UI;

namespace TabletApp;

/// <summary>
/// Mode test sans UI (<c>--self-test</c>) : simule 30 s d'animation en pas de
/// 16 ms, vérifie les invariants du clignement et du regard, et rend 4 frames
/// offscreen en PNG (vérifiables sans écran). Usage :
/// <c>dotnet run --project src/TabletApp -- --self-test</c>.
/// Le rapport est affiché dans la console et copié dans %TEMP%\tabletapp-selftest.txt.
/// </summary>
internal static class SelfTest
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public static bool Run()
    {
        AttachConsole(-1); // ATTACH_PARENT_PROCESS : sortie dans le terminal appelant.
        var log = new List<string>();
        bool ok = CheckBlink(log) & CheckGaze(log);
        RenderFrames(log);
        log.Add(ok ? "SELF-TEST: PASS" : "SELF-TEST: FAIL");
        foreach (string line in log)
        {
            Console.WriteLine(line);
        }
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "tabletapp-selftest.txt"),
            string.Join(Environment.NewLine, log) + Environment.NewLine);
        return ok;
    }

    private static bool CheckBlink(List<string> log)
    {
        var blink = new BlinkController(new Random(42));
        var step = TimeSpan.FromMilliseconds(16);
        var now = TimeSpan.Zero;
        var end = TimeSpan.FromSeconds(30);
        bool wasBlinking = false;
        var starts = new List<double>();
        double minOpen = 1;

        while (now < end)
        {
            blink.Update(now);
            if (blink.IsBlinking && !wasBlinking)
            {
                starts.Add(now.TotalSeconds);
            }
            wasBlinking = blink.IsBlinking;
            minOpen = Math.Min(minOpen, blink.OpenAmount);
            now += step;
        }

        // Intervalles hors doubles-clignements (~0,3 s) : tous entre 1,9 s et 6,5 s.
        var gaps = starts.Zip(starts.Skip(1), (a, b) => b - a).Where(g => g > 1).ToList();
        bool countOk = starts.Count is >= 3 and <= 16;
        bool gapsOk = gaps.Count > 0 && gaps.All(g => g is >= 1.9 and <= 6.5);
        bool closesOk = minOpen < 0.05;
        bool ok = countOk && gapsOk && closesOk;

        log.Add($"Blink: {starts.Count} clignements / 30 s, " +
            $"intervalles [{(gaps.Count > 0 ? gaps.Min().ToString("F1") : "?")}-" +
            $"{(gaps.Count > 0 ? gaps.Max().ToString("F1") : "?")} s], " +
            $"fermeture min {minOpen:F2} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckGaze(List<string> log)
    {
        double[] targets = { -1.0, 0.0, 1.0 };
        var gaze = new GazeController(new Random(7));
        var step = TimeSpan.FromMilliseconds(16);
        var now = TimeSpan.Zero;
        var end = TimeSpan.FromSeconds(30);
        double settled = 0; // position initiale connue
        int arrivals = 0;
        double min = 0;
        double max = 0;
        bool bounded = true;

        while (now < end)
        {
            gaze.Update(now);
            double x = gaze.X;
            if (double.IsNaN(x) || x < -1.001 || x > 1.001)
            {
                bounded = false;
            }
            min = Math.Min(min, x);
            max = Math.Max(max, x);
            double nearest = targets.MinBy(t => Math.Abs(t - x));
            if (Math.Abs(nearest - x) < 0.02 && nearest != settled)
            {
                settled = nearest;
                arrivals++;
            }
            now += step;
        }

        bool ok = bounded && arrivals >= 3 && min < -0.9 && max > 0.9;
        log.Add($"Gaze: {arrivals} saccades / 30 s, X dans [{min:F2}, {max:F2}] -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static void RenderFrames(List<string> log)
    {
        var frames = new (string Name, double Open, double GazeX, double GazeY)[]
        {
            ("neutre", 1.0, 0.0, 0.0),
            ("clin", 0.0, 0.0, 0.0),
            ("gauche", 1.0, -1.0, 0.0),
            ("droite", 1.0, 1.0, 0.0),
        };
        foreach (var (name, open, gazeX, gazeY) in frames)
        {
            using var bitmap = new SKBitmap(800, 600);
            using var canvas = new SKCanvas(bitmap);
            FacePainter.DrawFace(canvas, 800, 600, open, gazeX, gazeY);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            string path = Path.Combine(Path.GetTempPath(), $"tabletapp-frame-{name}.png");
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
            log.Add($"Frame '{name}' -> {path}");
        }
    }
}
