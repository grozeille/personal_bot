using System.Runtime.InteropServices;
using SkiaSharp;
using TabletApp.UI;

namespace TabletApp;

/// <summary>
/// Mode test sans UI (<c>--self-test</c>) : simule l'animation en pas de 16 ms,
/// vérifie les invariants du clignement, du regard, du sommeil et des
/// expressions, et rend des frames offscreen en PNG (vérifiables sans écran).
/// Usage : <c>dotnet run --project src/TabletApp -- --self-test</c>.
/// Le rapport est affiché dans la console et copié dans %TEMP%\tabletapp-selftest.txt.
/// </summary>
internal static class SelfTest
{
    private static readonly string[] EmotionNames =
    {
        "neutral", "dead", "mean", "dizzy", "happy", "tired", "love", "laugh", "wink", "sleep",
    };

    private static readonly RobotExpression[] Emotions =
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

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public static bool Run()
    {
        AttachConsole(-1); // ATTACH_PARENT_PROCESS : sortie dans le terminal appelant.
        var log = new List<string>();
        bool ok = CheckBlink(log) & CheckGaze(log) & CheckSleep(log)
            & CheckExpression(log) & CheckBlinkGating(log);
        RenderFrames(log);
        RenderExpressionSheet(log);
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

    private static bool CheckSleep(List<string> log)
    {
        var sleep = new SleepController();
        var step = TimeSpan.FromMilliseconds(16);
        var now = TimeSpan.Zero;
        var end = TimeSpan.FromSeconds(20);
        sleep.FallAsleep(TimeSpan.FromSeconds(1));
        bool wakeSent = false;
        double maxYawn = 0;
        bool asleepStatic = true; // EyeOpen == 0 sur [5, 9]
        double wakeMin = 1;
        double wakeMax = 0;
        bool reachedAsleep = false;
        bool reachedAwakeAgain = false;

        while (now < end)
        {
            if (!wakeSent && now >= TimeSpan.FromSeconds(10))
            {
                sleep.WakeUp(now);
                wakeSent = true;
            }
            sleep.Update(now);
            if (sleep.Current == SleepController.State.Asleep)
            {
                reachedAsleep = true;
            }
            if (wakeSent && sleep.Current == SleepController.State.Awake)
            {
                reachedAwakeAgain = true;
            }
            if (sleep.Current == SleepController.State.Yawning)
            {
                maxYawn = Math.Max(maxYawn, sleep.Yawn);
            }
            if (now >= TimeSpan.FromSeconds(5) && now < TimeSpan.FromSeconds(9) && sleep.EyeOpen != 0)
            {
                asleepStatic = false;
            }
            if (sleep.Current == SleepController.State.Waking)
            {
                wakeMin = Math.Min(wakeMin, sleep.EyeOpen);
                wakeMax = Math.Max(wakeMax, sleep.EyeOpen);
            }
            now += step;
        }

        bool ok = reachedAsleep && reachedAwakeAgain && asleepStatic
            && maxYawn > 0.5 && wakeMin < 0.3 && wakeMax > 0.6;
        log.Add($"Sleep: endormi={reachedAsleep}, statique={asleepStatic}, " +
            $"baillement max {maxYawn:F2}, réveil [{wakeMin:F2}-{wakeMax:F2}], " +
            $"réveillé={reachedAwakeAgain} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckExpression(List<string> log)
    {
        var expression = new ExpressionController();
        var step = TimeSpan.FromMilliseconds(16);
        var now = TimeSpan.Zero;
        bool ok = true;

        for (int i = 0; i < EmotionNames.Length; i++)
        {
            expression.SetEmotion(EmotionNames[i], now);
            ok &= !expression.Transitioning; // changement instantané (transitions désactivées)
            TimeSpan end = now + TimeSpan.FromSeconds(1);
            while (now < end)
            {
                expression.Update(now);
                now += step;
            }
            bool match = expression.Current == Emotions[i]
                && !expression.Transitioning
                && expression.PopScale == 1.0;
            ok &= match;
        }

        // Émotion inconnue : ignorée, l'expression courante est conservée.
        expression.SetEmotion("nimportequoi", now);
        ok &= expression.Current == RobotExpression.Sleep;

        log.Add($"Expression: {EmotionNames.Length} émotions mappées, inconnue ignorée -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckBlinkGating(List<string> log)
    {
        // Seuls neutral et happy clignent ; le sommeil ferme tous les yeux.
        bool neutralBlinks = !RenderBytes(RobotExpression.Neutral, 1.0, awake: true)
            .SequenceEqual(RenderBytes(RobotExpression.Neutral, 0.0, awake: true));
        bool happyBlinks = !RenderBytes(RobotExpression.Happy, 1.0, awake: true)
            .SequenceEqual(RenderBytes(RobotExpression.Happy, 0.0, awake: true));
        bool winkStatic = RenderBytes(RobotExpression.Wink, 1.0, awake: true)
            .SequenceEqual(RenderBytes(RobotExpression.Wink, 0.0, awake: true));
        bool dizzyStatic = RenderBytes(RobotExpression.Dizzy, 1.0, awake: true)
            .SequenceEqual(RenderBytes(RobotExpression.Dizzy, 0.0, awake: true));
        bool sleepOverrides = !RenderBytes(RobotExpression.Happy, 1.0, awake: false)
            .SequenceEqual(RenderBytes(RobotExpression.Happy, 0.0, awake: false));
        bool ok = neutralBlinks && happyBlinks && winkStatic && dizzyStatic && sleepOverrides;
        log.Add($"BlinkGating: neutral={neutralBlinks}, happy={happyBlinks}, " +
            $"wink statique={winkStatic}, dizzy statique={dizzyStatic}, " +
            $"sommeil prioritaire={sleepOverrides} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static byte[] RenderBytes(RobotExpression expression, double open, bool awake)
    {
        using var bitmap = new SKBitmap(400, 300);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(canvas, 400, 300, open, 0, 0, 0, 0, expression, 1.0, awake);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void RenderFrames(List<string> log)
    {
        var frames = new (string Name, double Open, double GazeX, double Yawn, double Time, bool Awake)[]
        {
            ("neutre", 1.0, 0.0, 0.0, 0.0, true),
            ("clin", 0.0, 0.0, 0.0, 0.0, true),
            ("gauche", 1.0, -1.0, 0.0, 0.0, true),
            ("droite", 1.0, 1.0, 0.0, 0.0, true),
            ("baillement", 0.35, 0.0, 1.0, 0.5, false),
            ("dodo", 0.0, 0.0, 0.0, 0.0, false),
            ("reveil", 0.45, 0.0, 0.0, 0.0, false),
        };
        foreach (var (name, open, gazeX, yawn, time, awake) in frames)
        {
            using var bitmap = new SKBitmap(800, 600);
            using var canvas = new SKCanvas(bitmap);
            FacePainter.DrawFace(
                canvas, 800, 600, open, gazeX, 0, yawn, time,
                RobotExpression.Neutral, 1.0, awake);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            string path = Path.Combine(Path.GetTempPath(), $"tabletapp-frame-{name}.png");
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
            log.Add($"Frame '{name}' -> {path}");
        }
    }

    private static void RenderExpressionSheet(List<string> log)
    {
        const int cellWidth = 280;
        const int cellHeight = 260;
        const int labelHeight = 40;
        using var sheet = new SKBitmap(cellWidth * EmotionNames.Length, cellHeight + labelHeight);
        using var canvas = new SKCanvas(sheet);
        canvas.Clear(SKColors.Black);
        using var font = new SKFont { Size = 22 };
        using var text = new SKPaint { Color = SKColors.Gray, IsAntialias = true };
        for (int i = 0; i < Emotions.Length; i++)
        {
            using var cell = new SKBitmap(cellWidth, cellHeight);
            using var cellCanvas = new SKCanvas(cell);
            FacePainter.DrawFace(
                cellCanvas, cellWidth, cellHeight, 1.0, 0, 0, 0, 0, Emotions[i], 1.0);
            using var image = SKImage.FromBitmap(cell);
            canvas.DrawImage(image, (float)(i * cellWidth), 0f, SKSamplingOptions.Default);
            canvas.DrawText(
                EmotionNames[i], (float)(i * cellWidth + 10), cellHeight + 28,
                SKTextAlign.Left, font, text);
        }
        using var sheetImage = SKImage.FromBitmap(sheet);
        using var data = sheetImage.Encode(SKEncodedImageFormat.Png, 100);
        string path = Path.Combine(Path.GetTempPath(), "tabletapp-frame-expressions.png");
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        log.Add($"Planche expressions -> {path}");
    }
}
