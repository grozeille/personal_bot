using System.Drawing;
using System.Runtime.InteropServices;
using SkiaSharp;
using TabletApp.UI;
using TabletApp.Update;
using Velopack;
using Velopack.Sources;

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
        "thinking", "shocked",
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
        RobotExpression.Thinking,
        RobotExpression.Shocked,
    };

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public static bool Run()
    {
        AttachConsole(-1); // ATTACH_PARENT_PROCESS : sortie dans le terminal appelant.
        var log = new List<string>();
        bool ok = CheckBlink(log) & CheckGaze(log) & CheckSleep(log)
            & CheckExpression(log) & CheckBlinkGating(log) & CheckFaceComplete(log)
            & CheckSteadyYawn(log) & CheckFaceEquivalence(log);
        RenderFrames(log);
        RenderExpressionSheet(log);
        RenderTransitionFrame(log);
        RenderUpdateFrame(log);
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
        var seen = new List<SleepController.State>();
        SleepController.State last = SleepController.State.Awake;
        double maxMouth = 0;
        bool asleepShown = true; // visage affiché tel quel pendant [5, 9]
        double wakeMin = 1;
        double wakeMax = 0;
        bool reachedAwakeAgain = false;

        while (now < end)
        {
            if (!wakeSent && now >= TimeSpan.FromSeconds(10))
            {
                sleep.WakeUp(now);
                wakeSent = true;
            }
            sleep.Update(now);
            if (sleep.Current != last)
            {
                seen.Add(sleep.Current);
                last = sleep.Current;
            }
            if (sleep.Current == SleepController.State.Yawning)
            {
                maxMouth = Math.Max(maxMouth, sleep.MouthOpen);
            }
            if (now >= TimeSpan.FromSeconds(5) && now < TimeSpan.FromSeconds(9) && sleep.EyeOpen != 1.0)
            {
                asleepShown = false;
            }
            if (sleep.Current == SleepController.State.Waking)
            {
                wakeMin = Math.Min(wakeMin, sleep.EyeOpen);
                wakeMax = Math.Max(wakeMax, sleep.EyeOpen);
            }
            if (wakeSent && sleep.Current == SleepController.State.Awake)
            {
                reachedAwakeAgain = true;
            }
            now += step;
        }

        bool orderOk = seen.SequenceEqual(new[]
        {
            SleepController.State.Yawning,
            SleepController.State.Asleep,
            SleepController.State.Waking,
            SleepController.State.Awake,
        });
        bool ok = orderOk && maxMouth > 0.9 && asleepShown
            && wakeMin < 0.3 && wakeMax > 0.6 && reachedAwakeAgain;
        log.Add($"Sleep: phases dans l'ordre={orderOk}, bouche max {maxMouth:F2}, " +
            $"dodo affiché={asleepShown}, réveil [{wakeMin:F2}-{wakeMax:F2}], " +
            $"réveillé={reachedAwakeAgain} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckExpression(List<string> log)
    {
        var expression = new ExpressionController();
        var step = TimeSpan.FromMilliseconds(16);
        var now = TimeSpan.Zero;
        bool ok = true;
        RobotExpression prev = RobotExpression.Neutral;

        for (int i = 0; i < EmotionNames.Length; i++)
        {
            bool changing = Emotions[i] != prev;
            TimeSpan t0 = now;
            expression.SetEmotion(EmotionNames[i], now);
            prev = Emotions[i];
            ok &= expression.Transitioning == changing;
            while (now < t0 + TimeSpan.FromMilliseconds(150))
            {
                expression.Update(now);
                now += step;
            }
            ok &= !changing || (expression.Progress > 0.0 && expression.Progress < 1.0);
            while (now < t0 + TimeSpan.FromSeconds(1))
            {
                expression.Update(now);
                now += step;
            }
            ok &= expression.Current == Emotions[i]
                && !expression.Transitioning
                && expression.Progress == 1.0;
        }

        // Émotion inconnue : ignorée, l'expression courante est conservée.
        expression.SetEmotion("nimportequoi", now);
        ok &= expression.Current == RobotExpression.Shocked;

        // À mi-transition, le rendu diffère des deux états stables.
        byte[] mid = RenderBytes(RobotExpression.Happy, 1.0, true, previous: RobotExpression.Neutral, progress: 0.5);
        ok &= !mid.SequenceEqual(RenderBytes(RobotExpression.Happy, 1.0, true, previous: RobotExpression.Happy, progress: 1.0));
        ok &= !mid.SequenceEqual(RenderBytes(RobotExpression.Neutral, 1.0, true, previous: RobotExpression.Neutral, progress: 1.0));

        log.Add($"Expression: {EmotionNames.Length} émotions mappées, inconnue ignorée, " +
            $"mi-transition distincte -> {(ok ? "OK" : "KO")}");
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

    private static bool CheckFaceComplete(List<string> log)
    {
        // Gardien anti-régression : les deux moitiés du visage contiennent des
        // pixels (un clip vide/supprimé ne doit plus passer inaperçu).
        using var bitmap = RenderBitmap(RobotExpression.Neutral, 1.0, awake: true);
        int left = 0;
        int right = 0;
        for (int x = 0; x < bitmap.Width; x += 2)
        {
            for (int y = 0; y < bitmap.Height; y += 2)
            {
                SKColor p = bitmap.GetPixel(x, y);
                if ((p.Red + p.Green + p.Blue) / 3 < 40)
                {
                    continue;
                }
                if (x < bitmap.Width / 2)
                {
                    left++;
                }
                else
                {
                    right++;
                }
            }
        }
        bool ok = left > 100 && right > 100;
        log.Add($"FaceComplete: gauche={left}, droite={right} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckSteadyYawn(List<string> log)
    {
        // Non-régression : pendant le bâillement, rien ne doit osciller dans le
        // temps (même progression, instants différents = pixels identiques).
        byte[] a = RenderBytes(RobotExpression.Tired, 1.0, awake: false, yawn: 1.0, timeSeconds: 0.5);
        byte[] b = RenderBytes(RobotExpression.Tired, 1.0, awake: false, yawn: 1.0, timeSeconds: 1.7);
        bool ok = a.SequenceEqual(b);
        log.Add($"SteadyYawn: bâillement stable dans le temps -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckFaceEquivalence(List<string> log)
    {
        // Les paires partageant une partie doivent vraiment l'être au pixel
        // (sinon : câblage des parties à corriger, le moteur anime en repli).
        var drifted = new List<string>();
        int count = 0;
        foreach (var (part, a, b) in SvgFaceLoader.DeclaredPairs())
        {
            count++;
            if (!SvgFaceLoader.PixelsMatch(part, a, b))
            {
                var (ax, ay) = SvgFaceLoader.PartCentroid(part, a);
                var (bx, by) = SvgFaceLoader.PartCentroid(part, b);
                drifted.Add($"{part} {a}/{b} (centres ({ax:F0},{ay:F0}) vs ({bx:F0},{by:F0}))");
            }
        }
        bool ok = drifted.Count == 0;
        log.Add($"FaceEquivalence: {count} paires vérifiées, " +
            (ok ? "aucune dérive" : "dérive: " + string.Join("; ", drifted)) +
            $" -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static SKBitmap RenderBitmap(
        RobotExpression expression, double open, bool awake,
        MouthOverride? mouthOverride = null, double yawn = 0.0, double timeSeconds = 0.0,
        RobotExpression? previous = null, double progress = 1.0)
    {
        var bitmap = new SKBitmap(400, 300);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(canvas, 400, 300, open, 0, 0, yawn, timeSeconds,
            expression, previous ?? expression, progress, mouthOverride, awake);
        return bitmap;
    }

    private static byte[] RenderBytes(
        RobotExpression expression, double open, bool awake,
        MouthOverride? mouthOverride = null, double yawn = 0.0, double timeSeconds = 0.0,
        RobotExpression? previous = null, double progress = 1.0)
    {
        using var bitmap = RenderBitmap(expression, open, awake, mouthOverride, yawn, timeSeconds, previous, progress);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void RenderFrames(List<string> log)
    {
        var frames = new (string Name, RobotExpression Expr, double Open, double GazeX, double Yawn, double Time, bool Awake, MouthOverride? Mouth)[]
        {
            ("neutre", RobotExpression.Neutral, 1.0, 0.0, 0.0, 0.0, true, null),
            ("clin", RobotExpression.Neutral, 0.0, 0.0, 0.0, 0.0, true, null),
            ("gauche", RobotExpression.Neutral, 1.0, -1.0, 0.0, 0.0, true, null),
            ("droite", RobotExpression.Neutral, 1.0, 1.0, 0.0, 0.0, true, null),
            ("baillement", RobotExpression.Tired, 1.0, 0.0, 1.0, 0.5, false, new MouthOverride(RobotExpression.Shocked, 1.0)),
            ("dodo", RobotExpression.Sleep, 1.0, 0.0, 0.0, 0.0, false, null),
            ("reveil", RobotExpression.Neutral, 0.45, 0.0, 0.0, 0.0, false, new MouthOverride(RobotExpression.Sleep, 1.0)),
        };
        foreach (var (name, expr, open, gazeX, yawn, time, awake, mouth) in frames)
        {
            using var bitmap = new SKBitmap(800, 600);
            using var canvas = new SKCanvas(bitmap);
            FacePainter.DrawFace(
                canvas, 800, 600, open, gazeX, 0, yawn, time,
                expr, expr, 1.0, mouth, awake);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            string path = Path.Combine(Path.GetTempPath(), $"tabletapp-frame-{name}.png");
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
            log.Add($"Frame '{name}' -> {path}");
        }
    }

    private static void RenderUpdateFrame(List<string> log)
    {
        // Icône de maj en haut à droite + anneau à 65 % (appui en cours).
        using var bitmap = new SKBitmap(800, 600);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(
            canvas, 800, 600, 1.0, 0, 0, 0, 0.49,
            RobotExpression.Neutral, RobotExpression.Neutral, 1.0, awake: true,
            updateAvailable: true, updateProgress: 0.65);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        string path = Path.Combine(Path.GetTempPath(), "tabletapp-frame-update.png");
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        log.Add($"Frame 'update' -> {path}");
    }

    /// <summary>
    /// Sonde de validation du cycle de maj (<c>--self-test-update &lt;flux&gt;</c>) :
    /// exerce le vrai moteur Velopack contre un dossier de flux (local ou NAS),
    /// sans jamais appliquer ni redémarrer : détection, téléchargement réel
    /// (hash vérifié par le moteur), refus d'un paquet corrompu, dossier
    /// absent silencieux, gestes d'appui et animation de l'icône. Les frames
    /// de preuve sont copiées dans %TEMP%\tabletapp-probe-*.png.
    /// Usage : <c>dotnet run --project src/TabletApp -- --self-test-update P:\RobotMaj</c>.
    /// Depuis l'app installée (Setup.exe), ajouter <c>--apply-and-restart</c>
    /// pour la jambe complète : la maj est appliquée et l'app redémarre sur
    /// la sonde <c>--post-restart</c> (aucune UI, preuve dans
    /// %TEMP%\tabletapp-update-probe-restart.txt).
    /// Note : le moteur applique au démarrage un paquet stocké par un run
    /// précédent — relancez depuis une install fraîche pour une preuve exacte.
    /// </summary>
    public static bool RunUpdateProbe(string feedDir, bool applyAndRestart = false)
    {
        AttachConsole(-1); // ATTACH_PARENT_PROCESS : sortie dans le terminal appelant.
        var log = new List<string>();
        // Corrupt avant Download : la jambe corrompue doit trouver un cache
        // de paquets vierge, sinon elle réutiliserait le paquet sain déjà
        // téléchargé au lieu du paquet trafiqué.
        bool ok = ProbeDetect(feedDir, log, out string? version)
            & ProbeCorrupt(feedDir, log)
            & ProbeDownload(feedDir, version, log)
            & ProbeOffline(log)
            & ProbeGestures(log)
            & ProbeIconFrames(log);
        if (ok && applyAndRestart && version is not null)
        {
            ProbeApplyRestart(feedDir, version, log); // ne revient pas (redémarre)
        }
        log.Add(ok ? "UPDATE-PROBE: PASS" : "UPDATE-PROBE: FAIL");
        WriteProbeLog(log);
        return ok;
    }

    /// <summary>
    /// Vérification post-redémarrage : la version courante est la version
    /// appliquée et le flux ne propose plus rien. Preuve dans
    /// %TEMP%\tabletapp-update-probe-restart.txt.
    /// </summary>
    public static bool RunUpdateProbePostRestart(string feedDir, string expected)
    {
        AttachConsole(-1);
        var log = new List<string>();
        bool ok;
        try
        {
            var manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(feedDir)));
            string current = manager.CurrentVersion?.ToString() ?? "(inconnue)";
            UpdateInfo? rest = manager.CheckForUpdatesAsync().GetAwaiter().GetResult();
            ok = current == expected && rest is null;
            log.Add($"Probe/Restart: version courante={current} (attendue={expected}), " +
                $"reste à jour={rest is null} -> {(ok ? "OK" : "KO")}");
        }
        catch (Exception ex)
        {
            ok = false;
            log.Add($"Probe/Restart: {ex.GetType().Name} {ex.Message} -> KO");
        }
        log.Add(ok ? "UPDATE-PROBE-RESTART: PASS" : "UPDATE-PROBE-RESTART: FAIL");
        foreach (string line in log)
        {
            Console.WriteLine(line);
        }
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "tabletapp-update-probe-restart.txt"),
            string.Join(Environment.NewLine, log) + Environment.NewLine);
        return ok;
    }

    private static void ProbeApplyRestart(string feedDir, string version, List<string> log)
    {
        var manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(feedDir)));
        UpdateInfo? info = manager.CheckForUpdatesAsync().GetAwaiter().GetResult();
        if (info is null)
        {
            log.Add("Probe/Apply: cible perdue avant application -> KO");
            log.Add("UPDATE-PROBE: FAIL");
            WriteProbeLog(log);
            Environment.Exit(1);
            return;
        }
        log.Add($"Probe/Apply: application de {info.TargetFullRelease.Version} " +
            $"(attendue={version}) + redémarrage...");
        WriteProbeLog(log); // Le processus ne revient pas : journal avant de partir.
        manager.ApplyUpdatesAndRestart(info,
            new[] { "--self-test-update", feedDir, "--post-restart", version });
    }

    private static void WriteProbeLog(List<string> log)
    {
        foreach (string line in log)
        {
            Console.WriteLine(line);
        }
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "tabletapp-update-probe.txt"),
            string.Join(Environment.NewLine, log) + Environment.NewLine);
    }

    private static bool ProbeDetect(string feedDir, List<string> log, out string? version)
    {
        var updater = new RobotUpdater(new VelopackUpdateSource(feedDir));
        updater.CheckAsync().GetAwaiter().GetResult();
        version = updater.PendingVersion;
        bool detected = updater.UpdateAvailable && version is not null;
        if (!detected)
        {
            // Diagnostic : moteur inutilisable hors install Velopack ?
            string cause = "aucune maj dans le flux";
            try
            {
                var manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(feedDir)));
                UpdateInfo? info = manager.CheckForUpdatesAsync().GetAwaiter().GetResult();
                cause = info is null
                    ? "flux à jour (moteur OK)"
                    : "incohérence détecteur/moteur";
            }
            catch (Exception ex)
            {
                cause = $"{ex.GetType().Name} — relancez depuis l'app installée " +
                    "(Setup.exe) : le moteur Velopack exige une installation";
            }
            log.Add($"Probe/Detect: flux '{feedDir}' -> {cause} -> KO");
            return false;
        }
        log.Add($"Probe/Detect: flux '{feedDir}' -> maj {version} proposée, " +
            "icône affichée -> OK");
        return true;
    }

    private static bool ProbeDownload(string feedDir, string? version, List<string> log)
    {
        // Téléchargement réel via le moteur (le hash est vérifié par Velopack),
        // sans appliquer : aucun redémarrage pendant la sonde.
        try
        {
            var manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(feedDir)));
            UpdateInfo? info = manager.CheckForUpdatesAsync().GetAwaiter().GetResult();
            if (info is null)
            {
                log.Add("Probe/Download: moteur sans cible alors que la détection a réussi -> KO");
                return false;
            }
            int percent = 0;
            manager.DownloadUpdatesAsync(info, p => percent = p, default)
                .GetAwaiter().GetResult();
            bool match = version is not null
                && info.TargetFullRelease.Version.ToString() == version;
            log.Add($"Probe/Download: {info.TargetFullRelease.FileName} " +
                $"({info.TargetFullRelease.Size} octets, {percent} %) téléchargé, " +
                $"version {version} concordante={match} -> " +
                (match ? "OK" : "KO"));
            return match;
        }
        catch (Exception ex)
        {
            log.Add($"Probe/Download: {ex.GetType().Name} {ex.Message} -> KO");
            return false;
        }
    }

    private static bool ProbeCorrupt(string feedDir, List<string> log)
    {
        // Copie du flux avec un paquet trafiqué (index intact) : le
        // téléchargement local ne vérifie pas le hash, c'est l'application qui
        // doit refuser le paquet (hash invalide) et conserver la version.
        string copy = Path.Combine(
            Path.GetTempPath(), "robot-maj-corrompue-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(copy);
            foreach (string file in Directory.GetFiles(feedDir))
            {
                File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));
            }
            // Trafique tous les paquets (full + delta) : quel que soit le chemin
            // choisi par le moteur, l'application doit refuser le hash invalide.
            string[] nupkgs = Directory.GetFiles(copy, "*.nupkg");
            if (nupkgs.Length == 0)
            {
                log.Add("Probe/Corrupt: aucun nupkg dans le flux -> KO");
                return false;
            }
            foreach (string nupkg in nupkgs)
            {
                byte[] bytes = File.ReadAllBytes(nupkg);
                bytes[bytes.Length / 2] ^= 0xFF;
                File.WriteAllBytes(nupkg, bytes);
            }

            var manager = new UpdateManager(new SimpleFileSource(new DirectoryInfo(copy)));
            UpdateInfo? info = manager.CheckForUpdatesAsync().GetAwaiter().GetResult();
            if (info is null)
            {
                log.Add("Probe/Corrupt: flux corrompu sans cible -> KO");
                return false;
            }
            string before = manager.CurrentVersion?.ToString() ?? "?";
            try
            {
                manager.DownloadUpdatesAsync(info, null, default).GetAwaiter().GetResult();
            }
            catch (Exception downloadEx)
            {
                // Cas observé : ChecksumFailedException pendant le transfert.
                return ReportCorruptRefused(log, copy, before, "téléchargement", downloadEx);
            }
            try
            {
                manager.ApplyUpdatesAndRestart(info);
                log.Add("Probe/Corrupt: paquet trafiqué APPLIQUÉ -> KO");
                return false;
            }
            catch (Exception applyEx)
            {
                return ReportCorruptRefused(log, copy, before, "application", applyEx);
            }
        }
        catch (Exception ex)
        {
            log.Add($"Probe/Corrupt: {ex.GetType().Name} {ex.Message} -> KO");
            return false;
        }
        finally
        {
            try { Directory.Delete(copy, recursive: true); } catch { }
        }
    }

    private static bool ReportCorruptRefused(
        List<string> log, string feedCopy, string before, string stage, Exception ex)
    {
        var check = new UpdateManager(new SimpleFileSource(new DirectoryInfo(feedCopy)));
        string after = check.CurrentVersion?.ToString() ?? "?";
        bool conserved = after == before;
        log.Add($"Probe/Corrupt: paquet trafiqué refusé au {stage} " +
            $"({ex.GetType().Name}), version {before} conservée={conserved} -> " +
            (conserved ? "OK" : "KO"));
        return conserved;
    }

    private static bool ProbeOffline(List<string> log)
    {
        // Dossier absent : silencieux, aucune maj, aucune exception.
        string missing = Path.Combine(
            Path.GetTempPath(), "robot-maj-absent-" + Guid.NewGuid().ToString("N"));
        try
        {
            var updater = new RobotUpdater(new VelopackUpdateSource(missing));
            updater.CheckAsync().GetAwaiter().GetResult();
            bool silent = !updater.UpdateAvailable && updater.PendingVersion is null;
            log.Add($"Probe/Offline: dossier absent -> silencieux={silent} -> " +
                (silent ? "OK" : "KO"));
            return silent;
        }
        catch (Exception ex)
        {
            log.Add($"Probe/Offline: {ex.GetType().Name} {ex.Message} -> KO");
            return false;
        }
    }

    private static bool ProbeGestures(List<string> log)
    {
        // Appui 3 s sur l'icône -> demandé ; appui court -> sans effet ;
        // appui hors zone (même maintenu) -> sans effet.
        var hold = new UpdateGesture();
        hold.Down(new PointF(790, 10), 800, TimeSpan.Zero);
        hold.Tick(TimeSpan.FromSeconds(3));
        bool holdOk = hold.UpdateRequested && hold.Progress == 1.0;

        var tap = new UpdateGesture();
        tap.Down(new PointF(790, 10), 800, TimeSpan.Zero);
        tap.Tick(TimeSpan.FromSeconds(1));
        tap.Up(TimeSpan.FromSeconds(1));
        bool tapOk = !tap.UpdateRequested && tap.Progress == 0;

        var away = new UpdateGesture();
        away.Down(new PointF(400, 300), 800, TimeSpan.Zero);
        away.Tick(TimeSpan.FromSeconds(5));
        away.Up(TimeSpan.FromSeconds(5));
        bool awayOk = !away.UpdateRequested && away.Progress == 0;

        bool ok = holdOk && tapOk && awayOk;
        log.Add($"Probe/Gestures: appui 3 s={holdOk}, court sans effet={tapOk}, " +
            $"hors zone sans effet={awayOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool ProbeIconFrames(List<string> log)
    {
        // Pulsation multi-frames (plusieurs rendus distincts au repos),
        // anneau réactif (0 / 0,5 / 1 distincts), icône absente sans maj.
        var pulses = Enumerable.Range(0, 8)
            .Select(i => Convert.ToHexString(RenderUpdateBytes(i * 0.25, 0, available: true)))
            .Distinct().Count();
        byte[] ring0 = RenderUpdateBytes(0.49, 0, available: true);
        byte[] ringHalf = RenderUpdateBytes(0.49, 0.5, available: true);
        byte[] ringFull = RenderUpdateBytes(0.49, 1, available: true);
        bool ringOk = !ring0.SequenceEqual(ringHalf)
            && !ringHalf.SequenceEqual(ringFull)
            && !ring0.SequenceEqual(ringFull);
        byte[] none = RenderUpdateBytes(0.49, 0, available: false);
        bool gatedOk = !ring0.SequenceEqual(none);
        bool pulseOk = pulses >= 4;

        SaveProbeFrame("rest", 0.49, 0);
        SaveProbeFrame("hold", 0.49, 0.65);

        bool ok = pulseOk && ringOk && gatedOk;
        log.Add($"Probe/Icon: {pulses}/8 frames pulsées distinctes, anneau 0/50/100 " +
            $"distinct={ringOk}, masquée sans maj={gatedOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static byte[] RenderUpdateBytes(double time, double progress, bool available)
    {
        using var bitmap = new SKBitmap(800, 600);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(
            canvas, 800, 600, 1.0, 0, 0, 0, time,
            RobotExpression.Neutral, RobotExpression.Neutral, 1.0, awake: true,
            updateAvailable: available, updateProgress: progress);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void SaveProbeFrame(string name, double time, double progress)
    {
        byte[] bytes = RenderUpdateBytes(time, progress, available: true);
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), $"tabletapp-probe-{name}.png"), bytes);
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
                cellCanvas, cellWidth, cellHeight, 1.0, 0, 0, 0, 0,
                Emotions[i], Emotions[i], 1.0);
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

    private static void RenderTransitionFrame(List<string> log)
    {
        // Transition neutre -> joie à 25/50/75 % : les yeux sont identiques
        // (immobiles), seule la bouche mute.
        foreach (double progress in new[] { 0.25, 0.5, 0.75 })
        {
            using var bitmap = new SKBitmap(800, 600);
            using var canvas = new SKCanvas(bitmap);
            FacePainter.DrawFace(canvas, 800, 600, 1.0, 0, 0, 0, 0,
                RobotExpression.Happy, RobotExpression.Neutral, progress);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            string path = Path.Combine(Path.GetTempPath(), $"tabletapp-frame-transition-{(int)(progress * 100)}.png");
            using var stream = File.OpenWrite(path);
            data.SaveTo(stream);
            log.Add($"Frame transition ({progress:P0}) -> {path}");
        }
    }

    /// <summary>
    /// Diagnostic perf : temps moyen/p95 d'une frame par scénario et par taille,
    /// pour chiffrer le coût réel (glow, transition) avant d'optimiser.
    /// </summary>
    public static bool Perf()
    {
        AttachConsole(-1);
        var log = new List<string>();
        var sizes = new (int W, int H, string Name)[]
        {
            (800, 600, "test"),
            (2752, 1152, "PC-fullscreen"),
            (800, 1280, "tablette"),
        };
        var scenarios = new (string Name, bool Glow)[]
        {
            ("repos-neutre", true),
            ("repos-sans-glow", false),
        };
        foreach (var (w, h, name) in sizes)
        {
            using var bitmap = new SKBitmap(w, h);
            using var canvas = new SKCanvas(bitmap);
            foreach (var (sname, glow) in scenarios)
            {
                for (int i = 0; i < 10; i++)
                {
                    FacePainter.DrawFace(canvas, w, h, 1.0, 0, 0, 0, 0,
                        RobotExpression.Neutral, RobotExpression.Neutral, 1.0, enableGlow: glow);
                }
                var sw = new List<double>();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 120; i++)
                {
                    watch.Restart();
                    FacePainter.DrawFace(canvas, w, h, 1.0, 0, 0, 0, 0,
                        RobotExpression.Neutral, RobotExpression.Neutral, 1.0, enableGlow: glow);
                    sw.Add(watch.Elapsed.TotalMilliseconds);
                }
                sw.Sort();
                log.Add($"{name} {w}x{h} {sname}: moy {sw.Average():F1} ms, p95 {sw[(int)(sw.Count * 0.95)]:F1} ms");
            }
        }
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "tabletapp-perf.txt"),
            string.Join(Environment.NewLine, log) + Environment.NewLine);
        return true;
    }
}
