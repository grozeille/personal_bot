using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using SkiaSharp;
using TabletApp.AI;
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
        "thinking", "shocked", "confirm",
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
        RobotExpression.Confirm,
    };

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public static bool Run()
    {
        AttachConsole(-1); // ATTACH_PARENT_PROCESS : sortie dans le terminal appelant.
        var log = new List<string>();
        bool ok = CheckBlink(log) & CheckGaze(log) & CheckSleep(log)
            & CheckExpression(log) & CheckBlinkGating(log) & CheckFaceComplete(log)
            & CheckSteadyYawn(log) & CheckFaceEquivalence(log) & CheckIdleSleep(log)
            & CheckGeminiVoice(log) & CheckSpeakingMouth(log) & CheckVoiceActivity(log)
            & CheckGeminiBrain(log) & CheckWikipedia(log) & CheckVoiceGlow(log) & CheckWakeWord(log)
            & CheckLiveMessages(log) & CheckRobotVoice(log) & CheckRobotSounds(log) & CheckMotionOptions(log);
        RenderFrames(log);
        RenderExpressionSheet(log);
        RenderTransitionFrame(log);
        RenderUpdateFrame(log);
        RenderConfirmFrame(log);
        RenderGlowFrame(log);
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
        ok &= expression.Current == RobotExpression.Confirm;

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

    private static bool CheckIdleSleep(List<string> log)
    {
        // #16 : fatigué auto après inactivité, dodo ensuite, réveil sur activité.
        // Seuils courts (5 s / 10 s) : même logique que le rendu temps réel.
        var idle = new IdleController(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        var expression = new ExpressionController();
        var sleep = new SleepController();
        var step = TimeSpan.FromMilliseconds(16);
        var now = TimeSpan.Zero;
        SleepController.State prevSleep = SleepController.State.Awake;
        idle.NotifyActivity(now);

        void frame()
        {
            sleep.Update(now);
            if (sleep.IsAwake)
            {
                if (idle.SleepDue(now))
                {
                    sleep.FallAsleep(now);
                }
                else if (idle.PollTired(now))
                {
                    expression.SetExpression(RobotExpression.Tired, now);
                }
            }
            if (sleep.Current != prevSleep)
            {
                if (sleep.Current == SleepController.State.Yawning)
                {
                    expression.SetExpression(RobotExpression.Tired, now);
                }
                else if (sleep.Current == SleepController.State.Asleep)
                {
                    expression.SetExpression(RobotExpression.Sleep, now);
                }
                else if (sleep.Current == SleepController.State.Waking)
                {
                    expression.SetExpression(RobotExpression.Neutral, now);
                }
                prevSleep = sleep.Current;
            }
            expression.Update(now);
        }

        void advance(TimeSpan duration)
        {
            TimeSpan end = now + duration;
            while (now < end)
            {
                frame();
                now += step;
            }
        }

        advance(TimeSpan.FromSeconds(6));
        bool tiredShown = expression.Current == RobotExpression.Tired;
        bool restoreSignaled = idle.NotifyActivity(now);
        expression.SetExpression(RobotExpression.Neutral, now);
        advance(TimeSpan.FromSeconds(1));
        bool backToNeutral = expression.Current == RobotExpression.Neutral && !expression.Transitioning;
        advance(TimeSpan.FromSeconds(12)); // +5 s fatigué, +5 s dodo, +2,2 s bâillement
        bool asleep = sleep.Current == SleepController.State.Asleep
            && expression.Current == RobotExpression.Sleep;
        idle.NotifyActivity(now);
        sleep.WakeUp(now);
        advance(TimeSpan.FromSeconds(3));
        bool woke = sleep.Current == SleepController.State.Awake
            && expression.Current == RobotExpression.Neutral;
        bool ok = tiredShown && restoreSignaled && backToNeutral && asleep && woke;
        log.Add($"IdleSleep: fatigué={tiredShown}, réveil-neutre={restoreSignaled && backToNeutral}, " +
            $"dodo={asleep}, réveillé={woke} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckGeminiVoice(List<string> log)
    {
        // #5 : parsers TTS/STT sur JSON figés (doc Interactions), sans réseau.
        byte[] wav = GeminiSpeech.ParseAudioResponse(
            "{\"steps\":[{\"type\":\"model_output\",\"content\":[{\"type\":\"audio\",\"data\":\"UklGRg==\"}]}]}");
        bool audioOk = wav.SequenceEqual(new byte[] { 0x52, 0x49, 0x46, 0x46 });
        bool audioKo;
        try
        {
            GeminiSpeech.ParseAudioResponse("{\"steps\":[]}");
            audioKo = false;
        }
        catch (InvalidDataException)
        {
            audioKo = true;
        }
        string? text = GeminiSpeech.ParseTextResponse(
            "{\"steps\":[{\"type\":\"model_output\",\"content\":[{\"type\":\"text\",\"text\":\"Bonjour !\"}]}]}");
        bool textOk = text == "Bonjour !";
        bool textNull = GeminiSpeech.ParseTextResponse("{\"steps\":[]}") is null;
        bool matchOk = VoiceTest.HearsBonjour("Bonjour !") && VoiceTest.HearsBonjour("bonjour")
            && VoiceTest.HearsBonjour("Ah bonjour.")
            && !VoiceTest.HearsBonjour("salut")
            && !VoiceTest.HearsBonjour("") && !VoiceTest.HearsBonjour(null);
        bool storeOk;
        string tmp = Path.Combine(Path.GetTempPath(), "tabletapp-keystore-test.json");
        try
        {
            File.WriteAllText(tmp, "{\"Autre:Cle\": \"x\"}");
            GeminiKey.StoreApiKey(tmp, "cle-test-123");
            string stored = File.ReadAllText(tmp);
            storeOk = GeminiKey.ReadFlatKey(tmp) == "cle-test-123" && stored.Contains("Autre:Cle");
        }
        finally
        {
            File.Delete(tmp);
        }
        bool ok = audioOk && audioKo && textOk && textNull && matchOk && storeOk;
        log.Add($"GeminiVoice: audio={audioOk && audioKo}, texte={textOk && textNull}, " +
            $"bonjour={matchOk}, coffre={storeOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckSpeakingMouth(List<string> log)
    {
        // Parole : la bouche grande ouverte (rire) remplace la bouche du visage.
        byte[] plain = RenderBytes(RobotExpression.Neutral, 1.0, awake: true);
        byte[] talking = RenderBytes(RobotExpression.Neutral, 1.0, awake: true,
            mouthOverride: new MouthOverride(RobotExpression.Laugh, 1.0));
        byte[] half = RenderBytes(RobotExpression.Neutral, 1.0, awake: true,
            mouthOverride: new MouthOverride(RobotExpression.Laugh, 0.5));
        bool ok = !plain.SequenceEqual(talking)
            && !plain.SequenceEqual(half)
            && !half.SequenceEqual(talking);
        log.Add($"SpeakingMouth: bouche parole distincte={ok} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckVoiceActivity(List<string> log)
    {
        // VAD : silence = 0, pleine échelle = 32767.
        byte[] silence = new byte[320];
        byte[] maxed = new byte[320];
        for (int i = 0; i < maxed.Length; i += 2)
        {
            maxed[i] = 0xFF;
            maxed[i + 1] = 0x7F;
        }
        double rmsSilence = NAudioVoice.ComputeRms(silence, silence.Length);
        double rmsMax = NAudioVoice.ComputeRms(maxed, maxed.Length);
        bool ok = rmsSilence == 0 && rmsMax == 32767;
        log.Add($"VoiceActivity: silence={rmsSilence:F0}, max={rmsMax:F0} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckGeminiBrain(List<string> log)
    {
        // #5 : penseur sur textes figés, sans réseau (JSON strict + replis).
        bool fullOk = GeminiBrain.ParseThinkReply(
                "{\"reply\": \"Salut !\", \"emotion\": \"happy\"}") == ("Salut !", "happy", null);
        bool wrappedOk = GeminiBrain.ParseThinkReply(
                "Voici : {\"reply\": \"Coucou.\", \"emotion\": \"love\"} !") == ("Coucou.", "love", null);
        bool bareOk = GeminiBrain.ParseThinkReply("Juste du texte.")
            == ("Juste du texte.", null, null);
        bool noEmotionOk = GeminiBrain.ParseThinkReply("{\"reply\": \"Yo.\"}")
            == ("Yo.", null, null);
        bool emptyOk = GeminiBrain.ParseThinkReply("   ") == (null, null, null)
            && GeminiBrain.ParseThinkReply(null) == (null, null, null);
        bool soundOk = GeminiBrain.ParseThinkReply("{\"reply\": \"Hi !\", \"sound\": \"giggle\"}")
            == ("Hi !", null, "giggle");
        string input = GeminiBrain.BuildInput(
            new List<(string Child, string Patate)> { ("a", "b") }, "coucou");
        bool inputOk = input.Contains("Enfant : coucou") && input.Contains("Patate : b");
        bool promptOk = GeminiBrain.DefaultPersonality.Contains("français")
            && GeminiBrain.DefaultPersonality.Contains("Sujets interdits")
            && GeminiBrain.BuildSystemPrompt(GeminiBrain.DefaultPersonality).Contains("\"reply\"") && GeminiBrain.BuildSystemPrompt(GeminiBrain.DefaultPersonality).Contains("\"emotion\"")
            && GeminiBrain.BuildLivePrompt(GeminiBrain.DefaultPersonality).Contains("set_emotion")
            && GeminiBrain.BuildLivePrompt(GeminiBrain.DefaultPersonality).Contains("play_fart")
            && GeminiBrain.BuildLivePrompt("X").Contains("search_wikipedia");
        (string fallbackText, bool fallbackCustom) = GeminiBrain.ResolvePersonality(
            Path.Combine(Path.GetTempPath(), "patate-config-absente.json"));
        promptOk = promptOk && !fallbackCustom && fallbackText == GeminiBrain.DefaultPersonality;
        string customPath = Path.Combine(Path.GetTempPath(), "patate-config-personnalite.json");
        File.WriteAllText(customPath, "{\"Gemini\": {\"Personality\": \"Sois bref.\"}}");
        (string customText, bool customFlag) = GeminiBrain.ResolvePersonality(customPath);
        promptOk = promptOk && customFlag && customText == "Sois bref.";
        bool paletteOk = RobotSounds.Names.All(n => GeminiBrain.DefaultPersonality.Contains(n));
        bool ok = fullOk && wrappedOk && bareOk && noEmotionOk && emptyOk && inputOk && promptOk && soundOk && paletteOk;
        log.Add($"GeminiBrain: réponse={fullOk && wrappedOk && bareOk && noEmotionOk && emptyOk}, prompt={promptOk}, son={soundOk}, palette={paletteOk}, " +
            $"entrée={inputOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckWikipedia(List<string> log)
    {
        // Recherche Wikipédia : parsing seul, sans réseau (fixtures figées).
        string? title = WikipediaSearch.ParseOpensearchTitle(
            "[\"pingu\",[\"Pingouin\"],[\"oiseau marin\"],[\"https://fr.wikipedia.org/wiki/Pingouin\"]]");
        string? none = WikipediaSearch.ParseOpensearchTitle("[\"x\",[],[],[]]");
        string? bad = WikipediaSearch.ParseOpensearchTitle("pas du json");
        string? text = WikipediaSearch.ParseExtract(
            "{\"query\":{\"pages\":{\"123\":{\"extract\":\"Le pingouin vole sous l'eau.\"}}}}");
        string? missing = WikipediaSearch.ParseExtract("{\"query\":{\"pages\":{\"-1\":{\"missing\":\"\"}}}}");
        string? broken = WikipediaSearch.ParseExtract("{}");
        bool titleOk = title == "Pingouin";
        bool noneOk = none is null && bad is null;
        bool textOk = text == "Le pingouin vole sous l'eau.";
        bool missingOk = missing is null && broken is null;
        bool ok = titleOk && noneOk && textOk && missingOk;
        log.Add($"Wikipedia: titre={titleOk}, extrait={textOk}, vide={noneOk && missingOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckVoiceGlow(List<string> log)
    {
        // Halo voix : absent/écoute distincts, réflexion pulsée, regard recentré.
        byte[] none = GlowBytes(VoiceGlow.None, 0);
        byte[] listen = GlowBytes(VoiceGlow.Listening, 0);
        byte[] think0 = GlowBytes(VoiceGlow.Thinking, 0);
        byte[] thinkQuarter = GlowBytes(VoiceGlow.Thinking, 0.25);
        (double ex, double ey) = RobotFaceRenderer.EaseGaze(1, -1, 10);
        (double sx, double sy) = RobotFaceRenderer.EaseGaze(1, 1, 0);
        bool ok = !none.SequenceEqual(listen)
            && !think0.SequenceEqual(thinkQuarter)
            && ex == 0 && ey == 0 && sx == 1 && sy == 1;
        log.Add($"VoiceGlow: halo pulsé={ok} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static byte[] GlowBytes(VoiceGlow glow, double time)
    {
        using var bitmap = new SKBitmap(400, 300);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(
            canvas, 400, 300, 1.0, 0, 0, 0, time,
            RobotExpression.Neutral, RobotExpression.Neutral, 1.0,
            updateAvailable: false, glow: glow);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private sealed class FakeSpotter : TabletApp.AI.IWakeDetector
    {
        private readonly Queue<bool> _fires;

        public FakeSpotter(params bool[] fires) => _fires = new Queue<bool>(fires);

        public int FeedCalls { get; private set; }

        public int Resets { get; private set; }

        public void Feed(float[] samples) => FeedCalls++;

        public bool TakeDetected() => _fires.Count > 0 && _fires.Dequeue();

        public void Reset() => Resets++;

        public void Dispose()
        {
        }
    }

    private static bool CheckWakeWord(List<string> log)
    {
        // Détecteur fake : déclenche une fois, anti-rebond 3 s, défauts seuils.
        using var listener = new TabletApp.AI.WakeWordListener(new FakeSpotter(true, true, false));
        int fires = 0;
        listener.Detected += () => fires++;
        listener.OnAudio(new float[160]);
        listener.OnAudio(new float[160]);
        listener.OnAudio(new float[160]);
        WakeWordOptions options = WakeWordOptions.Load(
            Path.Combine(Path.GetTempPath(), "tabletapp-jamais-un-fichier.json"),
            Path.Combine(Path.GetTempPath(), "tabletapp-jamais-une-calibration.json"));
        bool defaultsOk = options.Threshold == WakeWordOptions.DefaultThreshold
            && options.Score == WakeWordOptions.DefaultScore;
        // Moteur natif réel si le modèle est présent (sinon : non testé, pas un échec).
        bool nativeOk = true;
        bool nativeTested = TabletApp.AI.SherpaWakeDetector.TryCreate(
            0.25f, 1.0f,
            out TabletApp.AI.SherpaWakeDetector? sherpa);
        if (nativeTested && sherpa is not null)
        {
            using (sherpa)
            {
                sherpa.Feed(new float[1600]);
                nativeOk = !sherpa.TakeDetected();
            }
        }
        var gated = new FakeSpotter();
        using var listener2 = new TabletApp.AI.WakeWordListener(
            gated, quietHangover: TimeSpan.FromMilliseconds(60));
        float[] silence = new float[160];
        float[] loud = Enumerable.Repeat(0.5f, 160).ToArray();
        listener2.OnAudio(silence);
        listener2.OnAudio(silence);
        listener2.OnAudio(loud);
        bool gateOk = gated.FeedCalls == 3;
        Thread.Sleep(100);
        listener2.OnAudio(silence);
        Thread.Sleep(70);
        listener2.OnAudio(silence);
        // Ouverture : 1 reset + drain ; queue de 700 ms alimentée (+1 feed) ;
        // hangover dépassé : stop + 2e reset.
        bool resetOk = gated.Resets == 2 && gated.FeedCalls == 4;
        bool ok = fires == 1 && defaultsOk && nativeOk && gateOk && resetOk;
        log.Add($"WakeWord: déclenchements={fires}/1 attendu, défauts={defaultsOk}, " +
            $"natif={(nativeTested ? nativeOk.ToString() : "absent")}, " +
            $"vad={gateOk && resetOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckMotionOptions(List<string> log)
    {
        // Cible tête : défauts, fichier custom, surcharge env.
        string bogusOverride = Path.Combine(Path.GetTempPath(), "patate-motion-override-absent.json");
        TabletApp.Gestures.MotionOptions missing = TabletApp.Gestures.MotionOptions.Load(
            Path.Combine(Path.GetTempPath(), "patate-motion-absent.json"), bogusOverride);
        bool defaultsOk = missing == TabletApp.Gestures.MotionOptions.Default;
        string customPath = Path.Combine(Path.GetTempPath(), "patate-motion-test.json");
        File.WriteAllText(customPath, "{\"Motion\": {\"TargetHost\": \"192.168.0.99\", \"Port\": 1234}}");
        TabletApp.Gestures.MotionOptions custom = TabletApp.Gestures.MotionOptions.Load(customPath, bogusOverride);
        bool customOk = custom.TargetHost == "192.168.0.99" && custom.Port == 1234;
        string overridePath = Path.Combine(Path.GetTempPath(), "patate-motion-override.json");
        File.WriteAllText(overridePath, "{\"Motion\": {\"TargetHost\": \"192.168.0.42\", \"Port\": 9080}}");
        TabletApp.Gestures.MotionOptions over = TabletApp.Gestures.MotionOptions.Load(customPath, overridePath);
        bool overrideOk = over.TargetHost == "192.168.0.42" && over.Port == 9080;
        Environment.SetEnvironmentVariable("HEAD_SIM_HOST", "10.9.9.9");
        Environment.SetEnvironmentVariable("HEAD_SIM_PORT", "4321");
        bool envOk;
        try
        {
            envOk = TabletApp.Gestures.HeadTcpClient.Host == "10.9.9.9"
                && TabletApp.Gestures.HeadTcpClient.Port == 4321;
        }
        finally
        {
            Environment.SetEnvironmentVariable("HEAD_SIM_HOST", null);
            Environment.SetEnvironmentVariable("HEAD_SIM_PORT", null);
        }
        bool ok = defaultsOk && customOk && overrideOk && envOk;
        log.Add($"MotionOptions: defauts={defaultsOk}, fichier={customOk}, surcharge={overrideOk}, env={envOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckRobotSounds(List<string> log)
    {
        // Sons robot : palette rendue, inconnu nul, WAV valide, déterministe.
        bool namesOk = RobotSounds.Names.Count == 6;
        bool renderOk = true;
        foreach (string name in RobotSounds.Names)
        {
            byte[]? pcm = RobotSounds.Render(name);
            renderOk = renderOk && pcm is { Length: > 0 } && pcm.Length <= 24000 * 2 * 2;
            renderOk = renderOk && pcm!.Any(b => b != 0);
        }
        foreach (string sys in new[] { "reveil", "baillement", "erreur" })
        {
            renderOk = renderOk && RobotSounds.Render(sys) is { Length: > 0 };
        }
        renderOk = renderOk && RobotSounds.Render("prout") is { Length: > 0 };
        bool fileOk = Directory.GetFiles(Path.Combine(
            AppContext.BaseDirectory, "Models", "audio"), "prout-*.wav").Length >= 1;
        bool unknownOk = RobotSounds.Render("farfelu") is null;
        byte[]? giggle = RobotSounds.Render("giggle");
        byte[] wav = RobotSounds.ToWav(giggle!, 24000);
        bool wavOk = wav.Length == giggle!.Length + 44
            && wav[0] == (byte)'R' && wav[1] == (byte)'I' && wav[2] == (byte)'F' && wav[3] == (byte)'F';
        bool stableOk = RobotSounds.Render("baillement")!.SequenceEqual(RobotSounds.Render("baillement")!);
        bool ok = namesOk && renderOk && unknownOk && wavOk && stableOk && fileOk;
        log.Add($"RobotSounds: palette={namesOk && renderOk}, inconnu={unknownOk}, " +
            $"wav={wavOk}, stable={stableOk}, fichier={fileOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckRobotVoice(List<string> log)
    {
        // Filtre robot : silence préservé, effet borné, phase continue (pitch 0 épinglé).
        byte[] silence = new byte[480];
        int silenceProduced = new RobotVoiceFilter(0.0).Apply(silence, 0, silence.Length, 24000);
        bool silenceOk = silenceProduced == silence.Length && silence.All(b => b == 0);
        byte[] loud = new byte[480];
        for (int i = 0; i < loud.Length; i += 2)
        {
            loud[i] = 0x00;
            loud[i + 1] = 0x40;
        }
        byte[] once = (byte[])loud.Clone();
        new RobotVoiceFilter(0.0).Apply(once, 0, once.Length, 24000);
        byte[] twice = (byte[])loud.Clone();
        var cont = new RobotVoiceFilter(0.0);
        cont.Apply(twice, 0, 240, 24000);
        cont.Apply(twice, 240, 240, 24000);
        double rmsBefore = NAudioVoice.ComputeRms(loud, loud.Length);
        double rmsAfter = NAudioVoice.ComputeRms(once, once.Length);
        bool boundedOk = rmsAfter <= rmsBefore * 1.01;
        bool changedOk = !once.SequenceEqual(loud);
        bool continuousOk = once.SequenceEqual(twice);
        bool fallbackOk = RobotVoiceFilter.LoadEnabled(
            Path.Combine(Path.GetTempPath(), "patate-voix-absente.json")) == RobotVoiceFilter.DefaultEnabled;
        string customPath = Path.Combine(Path.GetTempPath(), "patate-voix-test.json");
        File.WriteAllText(customPath, "{\"Voice\": {\"RobotFilter\": false}}");
        bool customOk = !RobotVoiceFilter.LoadEnabled(customPath);
        // Pitch +6 demi-tons (×1,4142) : 4800 o -> ~3396 o. Continu entre chunks :
        // mêmes positions (mêmes comptes) et préfixe identique à l'octet
        // (sauf le dernier échantillon du chunk, interpolé sans le suivant).
        static byte[] FreshRamp()
        {
            byte[] ramp = new byte[4800];
            for (int i = 0; i < ramp.Length; i += 2)
            {
                short s = (short)((i / 2 * 10) % 30000);
                ramp[i] = (byte)(s & 0xFF);
                ramp[i + 1] = (byte)((s >> 8) & 0xFF);
            }
            return ramp;
        }
        byte[] ratioBuf = FreshRamp();
        int pitched = new RobotVoiceFilter(6.0).Apply(ratioBuf, 0, ratioBuf.Length, 24000);
        bool ratioOk = Math.Abs(pitched - 3396) <= 4;
        byte[] pitchOnce = FreshRamp();
        byte[] pitchTwice = (byte[])pitchOnce.Clone();
        int whole = new RobotVoiceFilter(3.0).Apply(pitchOnce, 0, pitchOnce.Length, 24000);
        var split = new RobotVoiceFilter(3.0);
        int first = split.Apply(pitchTwice, 0, pitchTwice.Length / 2, 24000);
        int second = split.Apply(pitchTwice, pitchTwice.Length / 2, pitchTwice.Length / 2, 24000);
        byte[] splitOut = pitchTwice.Take(first)
            .Concat(pitchTwice.Skip(pitchTwice.Length / 2).Take(second)).ToArray();
        bool prefixOk = pitchOnce.Take(first - 2).SequenceEqual(splitOut.Take(first - 2));
        bool pitchContOk = whole == first + second && prefixOk;
        string pitchPath = Path.Combine(Path.GetTempPath(), "patate-pitch-test.json");
        File.WriteAllText(pitchPath, "{\"Voice\": {\"PitchSemitones\": 2.5}}");
        string clampPath = Path.Combine(Path.GetTempPath(), "patate-pitch-clamp.json");
        File.WriteAllText(clampPath, "{\"Voice\": {\"PitchSemitones\": 99}}");
        bool pitchCfgOk = RobotVoiceFilter.LoadPitchSemitones(
                Path.Combine(Path.GetTempPath(), "patate-pitch-absent.json")) == 0.0
            && RobotVoiceFilter.LoadPitchSemitones(pitchPath) == 2.5
            && RobotVoiceFilter.LoadPitchSemitones(clampPath) == RobotVoiceFilter.MaxPitchSemitones;
        bool pitchOk = ratioOk && pitchContOk && pitchCfgOk;
        bool ok = silenceOk && boundedOk && changedOk && continuousOk && fallbackOk && customOk && pitchOk;
        log.Add($"RobotVoice: silence={silenceOk}, borne={boundedOk}, effet={changedOk}, " +
            $"continu={continuousOk}, config={fallbackOk && customOk}, pitch={pitchOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool CheckLiveMessages(List<string> log)
    {
        // Protocole Live : construction et décodage, sans réseau.
        string setup = LiveProtocol.BuildSetup("SYS", new[] { "happy", "sad" }, new[] { "giggle" }, new[] { "nod_yes" });
        string setupVoice = LiveProtocol.BuildSetup("SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" }, "Kore");
        bool setupOk = setup.Contains("models/gemini-3.8-live")
            && setupVoice.Contains("Kore") && setupVoice.Contains("prebuiltVoiceConfig")
            && !setup.Contains("prebuiltVoiceConfig")
            && setup.Contains("generationConfig")
            && setup.Contains("set_emotion") && setup.Contains("SYS") && setup.Contains("play_sound") && setup.Contains("play_fart")
            && setup.Contains("play_gesture") && setup.Contains("nod_yes")
            && setup.Contains("inputAudioTranscription");
        bool setupJsonOk;
        try
        {
            using (JsonDocument.Parse(setup))
            {
            }
            setupJsonOk = true;
        }
        catch
        {
            setupJsonOk = false;
        }
        string chunk = LiveProtocol.BuildAudioChunk(new byte[] { 1, 2, 3, 4 });
        bool chunkOk;
        using (JsonDocument doc = JsonDocument.Parse(chunk))
        {
            JsonElement audio = doc.RootElement.GetProperty("realtimeInput").GetProperty("audio");
            chunkOk = audio.GetProperty("mimeType").GetString() == "audio/pcm;rate=16000"
                && Convert.FromBase64String(audio.GetProperty("data").GetString()!)
                    .SequenceEqual(new byte[] { 1, 2, 3, 4 });
        }
        string answer = LiveProtocol.BuildToolResponse(new[] { ("c1", "set_emotion") });
        bool answerOk = answer.Contains("\"c1\"") && answer.Contains("set_emotion");
        string turn = LiveProtocol.BuildTurnComplete();
        string greeting = LiveProtocol.BuildClientTurn("Salut");
        bool turnOk = turn.Contains("\"turnComplete\":true") && !turn.Contains("turns")
            && greeting.Contains("\"turnComplete\":true") && greeting.Contains("Salut");
        DateTime now = new(2026, 1, 1, 12, 0, 10, DateTimeKind.Utc);
        TimeSpan silence = TimeSpan.FromMilliseconds(1200);
        TimeSpan maxTurn = TimeSpan.FromSeconds(12);
        bool fireOk = LiveSession.ShouldSendTurnComplete(false, true, now.AddSeconds(-2), DateTime.MinValue, now, silence, 4, 4, now.AddSeconds(-2), maxTurn)
            && !LiveSession.ShouldSendTurnComplete(false, false, now.AddSeconds(-2), DateTime.MinValue, now, silence, 4, 4, now.AddSeconds(-2), maxTurn)
            && !LiveSession.ShouldSendTurnComplete(true, true, now.AddSeconds(-2), DateTime.MinValue, now, silence, 4, 4, now.AddSeconds(-2), maxTurn)
            && !LiveSession.ShouldSendTurnComplete(false, true, now.AddSeconds(-2), now.AddSeconds(-1), now, silence, 4, 4, now.AddSeconds(-2), maxTurn)
            && !LiveSession.ShouldSendTurnComplete(false, true, now.AddMilliseconds(-500), DateTime.MinValue, now, silence, 4, 4, now.AddMilliseconds(-500), maxTurn)

            && !LiveSession.ShouldSendTurnComplete(false, true, now.AddSeconds(-2), DateTime.MinValue, now, silence, 3, 4, now.AddSeconds(-2), maxTurn)
            && LiveSession.ShouldSendTurnComplete(false, true, now.AddMilliseconds(-100), DateTime.MinValue, now, silence, 10, 4, now.AddSeconds(-13), maxTurn)
            && !LiveSession.ShouldSendTurnComplete(false, true, now.AddMilliseconds(-100), DateTime.MinValue, now, silence, 10, 4, now.AddSeconds(-2), maxTurn);
        LiveServerUpdate done = LiveProtocol.ParseUpdate("{\"setupComplete\":{}}");
        LiveServerUpdate call = LiveProtocol.ParseUpdate(
            "{\"toolCall\":{\"functionCalls\":[{\"id\":\"c1\",\"name\":\"set_emotion\","
            + "\"args\":{\"emotion\":\"happy\"}}]}}");
        LiveServerUpdate strArgs = LiveProtocol.ParseUpdate(
            "{\"toolCall\":{\"functionCalls\":[{\"id\":\"c2\",\"name\":\"set_emotion\","
            + "\"args\":\"{\\\"emotion\\\":\\\"love\\\"}\"}]}}");
        LiveServerUpdate wave = LiveProtocol.ParseUpdate(
            "{\"serverContent\":{\"modelTurn\":{\"parts\":[{\"inlineData\":{"
            + "\"mimeType\":\"audio/pcm;rate=24000\",\"data\":\"QUI=\"}}]}}}");
        LiveServerUpdate texts = LiveProtocol.ParseUpdate(
            "{\"serverContent\":{\"inputTranscription\":{\"text\":\"salut\"},"
            + "\"outputTranscription\":{\"text\":\"coucou\"},\"turnComplete\":true}}");
        LiveServerUpdate fart = LiveProtocol.ParseUpdate(
            "{\"toolCall\":{\"functionCalls\":[{\"id\":\"c7\",\"name\":\"play_fart\"}]}}");
        LiveServerUpdate sfx = LiveProtocol.ParseUpdate(
            "{\"toolCall\":{\"functionCalls\":[{\"id\":\"c9\",\"name\":\"play_sound\","
            + "\"args\":{\"sound\":\"giggle\"}}]}}");
        LiveServerUpdate cut = LiveProtocol.ParseUpdate(
            "{\"serverContent\":{\"interrupted\":true}}");
        LiveServerUpdate away = LiveProtocol.ParseUpdate("{\"goAway\":{}}");
        LiveServerUpdate alien = LiveProtocol.ParseUpdate("{\"nimporte\":{}}");
        LiveServerUpdate move = LiveProtocol.ParseUpdate(
            "{\"toolCall\":{\"functionCalls\":[{\"id\":\"c4\",\"name\":\"move_head\","
            + "\"args\":{\"yaw\":-30,\"pitch\":\"5.5\"}}]}}");
        LiveServerUpdate moveStr = LiveProtocol.ParseUpdate(
            "{\"toolCall\":{\"functionCalls\":[{\"id\":\"c5\",\"name\":\"move_head\","
            + "\"args\":\"{\\\"roll\\\":18}\"}]}}");
        bool parseOk = done.SetupComplete
            && call.ToolCalls.Count == 1 && call.ToolCalls[0] == new LiveToolCall("c1", "set_emotion", "happy", null, null)
            && move.ToolCalls.Count == 1 && move.ToolCalls[0].Yaw == -30
            && move.ToolCalls[0].Pitch == 5.5 && move.ToolCalls[0].Roll is null
            && moveStr.ToolCalls.Count == 1 && moveStr.ToolCalls[0].Roll == 18
            && strArgs.ToolCalls.Count == 1 && strArgs.ToolCalls[0].Emotion == "love"
            && sfx.ToolCalls.Count == 1 && sfx.ToolCalls[0].Sound == "giggle"

            && fart.ToolCalls.Count == 1 && fart.ToolCalls[0].Name == "play_fart" && fart.ToolCalls[0].Sound is null            && wave.Audio is { Length: 2 } && wave.Audio[0] == 0x41
            && texts.InputTranscript == "salut" && texts.OutputTranscript == "coucou" && texts.TurnComplete
            && cut.Interrupted && !cut.TurnComplete
            && away.GoAway
            && !alien.SetupComplete && !alien.TurnComplete && alien.Audio is null;
        bool ok = setupOk && setupJsonOk && chunkOk && answerOk && parseOk && turnOk && fireOk;
        log.Add($"LiveMessages: construction={setupOk && chunkOk && answerOk}, " +
            $"décodage={parseOk}, tours={turnOk && fireOk} -> {(ok ? "OK" : "KO")}");
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
        // Icône de maj en haut à droite.
        using var bitmap = new SKBitmap(800, 600);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(
            canvas, 800, 600, 1.0, 0, 0, 0, 0.49,
            RobotExpression.Neutral, RobotExpression.Neutral, 1.0, awake: true,
            updateAvailable: true);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        string path = Path.Combine(Path.GetTempPath(), "tabletapp-frame-update.png");
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        log.Add($"Frame 'update' -> {path}");
    }

    private static void RenderConfirmFrame(List<string> log)
    {
        // Visage de confirmation (œil X annuler, œil ✓ confirmer).
        using var bitmap = new SKBitmap(800, 600);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(
            canvas, 800, 600, 1.0, 0, 0, 0, 0.49,
            RobotExpression.Confirm, RobotExpression.Confirm, 1.0, awake: true,
            updateAvailable: true);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        string path = Path.Combine(Path.GetTempPath(), "tabletapp-frame-confirm.png");
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        log.Add($"Frame 'confirm' -> {path}");
    }

    private static void RenderGlowFrame(List<string> log)
    {
        // Halo voix autour de l'écran (ici : réflexion à mi-pulsation).
        using var bitmap = new SKBitmap(800, 600);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(
            canvas, 800, 600, 1.0, 0, 0, 0, 0.25,
            RobotExpression.Neutral, RobotExpression.Neutral, 1.0, awake: true,
            updateAvailable: false, glow: VoiceGlow.Thinking);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        string path = Path.Combine(Path.GetTempPath(), "tabletapp-frame-glow.png");
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
        log.Add($"Frame 'glow' -> {path}");
    }

    /// <summary>
    /// Sonde de validation du cycle de maj (<c>--self-test-update &lt;flux&gt;</c>) :
    /// exerce le vrai moteur Velopack contre un dossier de flux (local ou NAS),
    /// sans jamais appliquer ni redémarrer : détection, téléchargement réel
    /// (hash vérifié par le moteur), refus d'un paquet corrompu, dossier
    /// absent silencieux, dialogue de confirmation et animation de l'icône. Les frames
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
            & ProbeConfirm(log)
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

    private static bool ProbeConfirm(List<string> log)
    {
        // Tap icône -> dialogue ; œil gauche -> annulé ; œil droit ->
        // confirmé ; hors yeux et hors dialogue -> ignoré.
        var dialog = new ConfirmDialog();
        bool iconOk = ConfirmDialog.IconHit(new PointF(790, 10), 800)
            && !ConfirmDialog.IconHit(new PointF(400, 300), 800);
        bool cornerOk = ConfirmDialog.QuitHit(new PointF(10, 10))
            && !ConfirmDialog.QuitHit(new PointF(400, 300));
        bool inactiveOk = dialog.Tap(550, 280) == ConfirmTap.Ignored;
        dialog.Show(RobotExpression.Happy, ConfirmPurpose.Update);
        bool cancelOk = dialog.Tap(250, 280) == ConfirmTap.Cancelled
            && !dialog.Active && dialog.Previous == RobotExpression.Happy;
        dialog.Show(RobotExpression.Happy, ConfirmPurpose.Update);
        bool confirmOk = dialog.Tap(550, 280) == ConfirmTap.Confirmed
            && !dialog.Active && dialog.Purpose == ConfirmPurpose.Update;
        dialog.Show(RobotExpression.Happy, ConfirmPurpose.Quit);
        bool quitOk = dialog.Tap(550, 280) == ConfirmTap.Confirmed
            && dialog.Purpose == ConfirmPurpose.Quit;
        dialog.Show(RobotExpression.Happy, ConfirmPurpose.Update);
        bool outsideOk = dialog.Tap(400, 500) == ConfirmTap.Ignored && dialog.Active;
        bool frozenOk = !FacePainter.LooksAround(RobotExpression.Confirm);

        bool ok = iconOk && cornerOk && inactiveOk && cancelOk && confirmOk
            && quitOk && outsideOk && frozenOk;
        log.Add($"Probe/Confirm: icône={iconOk}, coin={cornerOk}, inactif ignoré={inactiveOk}, " +
            $"gauche annule={cancelOk}, droit confirme={confirmOk}, but sortie={quitOk}, " +
            $"hors yeux ignoré={outsideOk}, regard figé={frozenOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static bool ProbeIconFrames(List<string> log)
    {
        // Pulsation multi-frames (plusieurs rendus distincts au repos),
        // visage de confirmation distinct du neutre, icône absente sans maj.
        var pulses = Enumerable.Range(0, 8)
            .Select(i => Convert.ToHexString(RenderUpdateBytes(i * 0.25, available: true)))
            .Distinct().Count();
        byte[] rest = RenderUpdateBytes(0.49, available: true);
        byte[] confirm = RenderUpdateBytes(0.49, available: true, RobotExpression.Confirm);
        byte[] none = RenderUpdateBytes(0.49, available: false);
        bool confirmOk = !rest.SequenceEqual(confirm);
        bool gatedOk = !rest.SequenceEqual(none);
        bool pulseOk = pulses >= 4;

        SaveProbeFrame("rest", 0.49, RobotExpression.Neutral);
        SaveProbeFrame("confirm", 0.49, RobotExpression.Confirm);

        bool ok = pulseOk && confirmOk && gatedOk;
        log.Add($"Probe/Icon: {pulses}/8 frames pulsées distinctes, confirmation " +
            $"distincte={confirmOk}, masquée sans maj={gatedOk} -> {(ok ? "OK" : "KO")}");
        return ok;
    }

    private static byte[] RenderUpdateBytes(
        double time, bool available, RobotExpression expression = RobotExpression.Neutral)
    {
        using var bitmap = new SKBitmap(800, 600);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(
            canvas, 800, 600, 1.0, 0, 0, 0, time,
            expression, expression, 1.0, awake: true,
            updateAvailable: available);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void SaveProbeFrame(string name, double time, RobotExpression expression)
    {
        byte[] bytes = RenderUpdateBytes(time, available: true, expression);
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
    /// <summary>
    /// Démo sonore (--sons) : joue les 9 sons + comparatif voix brute /
    /// filtrée (hors-ligne, haut-parleur par défaut). Écoute humaine requise.
    /// </summary>
    /// <summary>
    /// Sonde wake word (--wake-probe) : 30 s d'écoute avec niveaux, porte et
    /// détections affichés + indices de dépannage. Micro réel requis.
    /// </summary>
    /// <summary>
    /// Enregistre 12 s de micro (16 kHz mono) vers %TEMP%\wake-heypatate.wav
    /// pour le balayage --wake-sweep. Reste local, jamais commité.
    /// </summary>
    public static bool WakeRecord()
    {
        string path = Path.Combine(Path.GetTempPath(), "wake-heypatate.wav");
        Console.WriteLine("Dites « Hey Patate » 3 fois, à voix normale (12 s)...");
        try
        {
            using var input = new NAudio.Wave.WaveInEvent
            {
                WaveFormat = new NAudio.Wave.WaveFormat(16000, 16, 1),
            };
            using var writer = new NAudio.Wave.WaveFileWriter(path, input.WaveFormat);
            double peak = 0;
            object sync = new();
            input.DataAvailable += (_, e) =>
            {
                lock (sync)
                {
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
                    double rms = TabletApp.AI.NAudioVoice.ComputeRms(e.Buffer, e.BytesRecorded);
                    if (rms > peak)
                    {
                        peak = rms;
                    }
                }
            };
            input.StartRecording();
            Thread.Sleep(TimeSpan.FromSeconds(12));
            input.StopRecording();
            Thread.Sleep(500);
            Console.WriteLine($"Fichier : {path} (RMS max {peak:F0}, {new FileInfo(path).Length / 1024} Ko).");
            return peak > 20;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Enregistrement impossible : {ex.Message} -> KO");
            return false;
        }
    }

    public static bool LiveRecord(int seconds, string? path)
    {
        path ??= Path.Combine(Path.GetTempPath(), "live-conversation.wav");
        (int at, string cue)[] cues =
        {
            (0, "Restez silencieux..."),
            (2, "Dites : « Hey Patate »"),
            (6, "Dites : « Bonjour Patate, comment tu t'appelles ? »"),
            (11, "Dites : « Raconte-moi une blague de prout »"),
            (16, "Dites : « Au revoir Patate »"),
            (20, "Restez silencieux..."),
        };
        Console.WriteLine($"Enregistrement {seconds} s (micro device 0, comme Live)...");
        try
        {
            using var input = new NAudio.Wave.WaveInEvent
            {
                WaveFormat = new NAudio.Wave.WaveFormat(16000, 16, 1),
            };
            using var writer = new NAudio.Wave.WaveFileWriter(path, input.WaveFormat);
            double peak = 0;
            double[] buckets = new double[Math.Max(seconds / 5, 1)];
            int chunks = 0;
            object sync = new();
            input.DataAvailable += (_, e) =>
            {
                lock (sync)
                {
                    writer.Write(e.Buffer, 0, e.BytesRecorded);
                    double rms = TabletApp.AI.NAudioVoice.ComputeRms(e.Buffer, e.BytesRecorded);
                    if (rms > peak)
                    {
                        peak = rms;
                    }
                    int b = Math.Min(chunks * buckets.Length / Math.Max(seconds * 10, 1), buckets.Length - 1);
                    if (rms > buckets[b])
                    {
                        buckets[b] = rms;
                    }
                    chunks++;
                }
            };
            input.StartRecording();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int next = 0;
            while (watch.Elapsed.TotalSeconds < seconds)
            {
                if (seconds == 30 && next < cues.Length && watch.Elapsed.TotalSeconds >= cues[next].at)
                {
                    Console.WriteLine($"  t={cues[next].at,2} s : {cues[next].cue}");
                    next++;
                }
                Thread.Sleep(100);
            }
            input.StopRecording();
            Thread.Sleep(500);
            for (int b = 0; b < buckets.Length; b++)
            {
                Console.WriteLine($"  RMS max t={b * 5}-{(b + 1) * 5} s : {buckets[b]:F0}");
            }
            Console.WriteLine($"Fichier : {path} (RMS max {peak:F0}, {new FileInfo(path).Length / 1024} Ko).");
            return peak > 20;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Enregistrement impossible : {ex.Message} -> KO");
            return false;
        }
    }

    private sealed class SimSocket : TabletApp.AI.ILiveSocket
    {
        private readonly object _sync = new();
        public readonly List<string> Sent = new();
        public string? LastError => null;
        public string? LastCloseStatus => null;
        public Task ConnectAsync(string url, string apiKey, CancellationToken ct) => Task.CompletedTask;
        public Task SendAsync(string json, CancellationToken ct)
        {
            lock (_sync)
            {
                Sent.Add(json);
            }
            return Task.CompletedTask;
        }
        public Task<string?> ReceiveAsync(CancellationToken ct) => Task.FromResult<string?>(null);
        public Task CloseAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return Sent.Count;
                }
            }
        }
        public int CountSince(int before, out int audio, out bool turn)
        {
            audio = 0;
            turn = false;
            lock (_sync)
            {
                for (int m = before; m < Sent.Count; m++)
                {
                    if (Sent[m].Contains("turnComplete"))
                    {
                        turn = true;
                    }
                    else
                    {
                        audio++;
                    }
                }
                return Sent.Count;
            }
        }

        /// <summary>Décode les PCM des messages audio envoyés depuis before.</summary>
        public List<byte[]> AudiosSince(int before)
        {
            var pcms = new List<byte[]>();
            lock (_sync)
            {
                for (int m = before; m < Sent.Count; m++)
                {
                    if (Sent[m].Contains("turnComplete"))
                    {
                        continue;
                    }
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(Sent[m]);
                        string? data = doc.RootElement
                            .GetProperty("realtimeInput").GetProperty("audio")
                            .GetProperty("data").GetString();
                        if (data is not null)
                        {
                            pcms.Add(Convert.FromBase64String(data));
                        }
                    }
                    catch
                    {
                        // Message inattendu : ignoré (diagnostic).
                    }
                }
            }
            return pcms;
        }
    }

    /// <summary>
    /// <summary>
    /// Flag --live-file [wav] [delai] : vraie session Live avec micro rejoué
    /// depuis un WAV (16 kHz mono), pour tester le comportement modèle (tools)
    /// sans parler. Le fichier démarre après delai secondes (défaut 20, après
    /// la salutation et son éventuel 2e tour). Journal dans
    /// %TEMP%\livefile-test.txt (avec verdict WIKI-CALL). true si déroulée.
    /// </summary>
    public static bool LiveFile(string? path, double delaySec = 20)
    {
        path ??= Path.Combine(Path.GetTempPath(), "live-question.wav");
        string? key;
        try
        {
            key = TabletApp.AI.GeminiKey.Resolve();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Clé Gemini : {ex.Message} -> KO");
            return false;
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.WriteLine("Clé Gemini manquante -> KO");
            return false;
        }
        if (!TryLoadMono16k(path, out float[]? samples) || samples is null)
        {
            return false;
        }
        string report = Path.Combine(Path.GetTempPath(), "livefile-test.txt");
        File.WriteAllText(report, $"LiveFile : {path} ({samples.Length / 16000.0:F1} s){Environment.NewLine}");
        void log(string line)
        {
            Console.WriteLine(line);
            File.AppendAllText(report, line + Environment.NewLine);
        }
        try
        {
            var socket = new TabletApp.AI.LiveClientWebSocket();
            using var mic = new FileLiveMic(samples, TimeSpan.FromSeconds(Math.Clamp(delaySec, 0, 120)));
            using var player = new SimPlayer();
            bool wikiCalled = false;
            var session = new TabletApp.AI.LiveSession(
                key, TabletApp.AI.GeminiBrain.LiveSystemPrompt,
                TabletApp.AI.GeminiBrain.EmotionNames, TabletApp.AI.RobotSounds.Names,
                TabletApp.AI.GeminiBrain.GestureNames,
                socket, mic, player,
                new TabletApp.AI.LiveCallbacks(
                    emotion => log("  Emotion : " + emotion),
                    sound => log("  Son : " + sound),
                    gesture => log("  Geste : " + gesture),
                    heard => log("  Entendu : '" + heard + "'"),
                    said => log("  Robot : " + said),
                    _ => { },
                    _ => { },
                    () => { },
                    msg => { log("  LOG: " + msg); if (msg.Contains("search_wikipedia")) wikiCalled = true; },
                    null, null,
                    async (query, ct) => await new TabletApp.AI.WikipediaSearch().SearchSummaryAsync(query, ct).ConfigureAwait(false)
                        ?? $"Je n'ai rien trouvé sur {query}."),
                idleTimeout: TimeSpan.FromSeconds(30),
                voiceName: TabletApp.AI.LiveProtocol.ResolveVoice());
            session.RunAsync(CancellationToken.None).GetAwaiter().GetResult();
            log("WIKI-CALL: " + (wikiCalled ? "oui" : "non"));
            return true;
        }
        catch (Exception ex)
        {
            log("ERREUR : " + ex.Message + " -> KO");
            return false;
        }
    }

    /// <summary>Micro rejouant des échantillons 16 kHz en temps réel, puis silence.</summary>
    private sealed class FileLiveMic : TabletApp.AI.ILiveMic
    {
        private readonly byte[][] _chunks;
        private readonly int _silentFrames;
        private System.Threading.Timer? _timer;
        private int _frame;
        private bool _disposed;

        public FileLiveMic(float[] samples, TimeSpan startDelay)
        {
            const int frame = 1600;
            var chunks = new List<byte[]>();
            for (int at = 0; at + frame <= samples.Length; at += frame)
            {
                byte[] bytes = new byte[frame * 2];
                for (int i = 0; i < frame; i++)
                {
                    short v = (short)Math.Clamp(samples[at + i] * 32768.0, short.MinValue, short.MaxValue);
                    bytes[i * 2] = (byte)(v & 0xFF);
                    bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
                }
                chunks.Add(bytes);
            }
            _chunks = chunks.ToArray();
            _silentFrames = Math.Max(0, (int)(startDelay.TotalMilliseconds / 100));
        }

        public event Action<byte[]>? Chunk;

        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _timer ??= new System.Threading.Timer(_ => Push(), null, 0, 100);
        }

        public void Stop()
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }

        private void Push()
        {
            int f = Interlocked.Increment(ref _frame) - 1;
            int fileIndex = f - _silentFrames;
            byte[] chunk = fileIndex >= 0 && fileIndex < _chunks.Length ? _chunks[fileIndex] : new byte[3200];
            try
            {
                Chunk?.Invoke(chunk);
            }
            catch
            {
            }
        }
    }

    private sealed class SimMic : TabletApp.AI.ILiveMic
    {
#pragma warning disable CS0067 // Jamais levé : le sim injecte via FeedTestChunk.
        public event Action<byte[]>? Chunk;
#pragma warning restore CS0067
        public void Start()
        {
        }
        public void Stop()
        {
        }
        public void Dispose()
        {
        }
    }

    private sealed class SimPlayer : TabletApp.AI.ILivePlayer
    {
        public void PlayChunk(byte[] pcm24k)
        {
        }
        public void PlayRaw(byte[] pcm24k)
        {
        }
        public void Clear()
        {
        }
        public bool IsDrained => true;
        public TabletApp.AI.PlayerTurnStats TakeTurnStats() => new(0, 0, 0);
        public void Start()
        {
        }
        public void Stop()
        {
        }
        public void Dispose()
        {
        }
    }

    public static bool LiveSim(string? path)
    {
        path ??= Path.Combine(Path.GetTempPath(), "live-conversation.wav");
        if (!TryLoadMono16k(path, out float[]? samples) || samples is null)
        {
            return false;
        }
        var socket = new SimSocket();
        var events = new TabletApp.AI.LiveCallbacks(
            _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { },
            () => Console.WriteLine("  (parole détectée)"),
            msg => Console.WriteLine("  LOG: " + msg));
        var session = new TabletApp.AI.LiveSession(
            "sim", "sim", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            socket, new SimMic(), new SimPlayer(), events,
            turnTimeout: TimeSpan.FromMilliseconds(1200));
        const int frame = 1600;
        int frames = samples.Length / frame;
        var spark = new System.Text.StringBuilder();
        double maxRms = 0;
        int sentChunks = 0;
        int loudChunks = 0;
        int turns = 0;
        int turnAudio = 0;
        var turnPcm = new List<byte[]>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int f = 0; f < frames; f++)
        {
            byte[] bytes = new byte[frame * 2];
            double sum = 0;
            for (int i = 0; i < frame; i++)
            {
                float s = samples[f * frame + i];
                sum += s * s;
                short v = (short)Math.Clamp(s * 32768.0, short.MinValue, short.MaxValue);
                bytes[i * 2] = (byte)(v & 0xFF);
                bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
            double rms = Math.Sqrt(sum / frame) * 32768;
            if (rms > maxRms)
            {
                maxRms = rms;
            }
            if (rms >= TabletApp.AI.NAudioVoice.SpeechRmsThreshold)
            {
                loudChunks++;
            }
            int before = socket.Count;
            session.FeedTestChunk(bytes);
            socket.CountSince(before, out int audio, out bool turn);
            foreach (byte[] pcm in socket.AudiosSince(before))
            {
                turnPcm.Add(pcm);
            }
            sentChunks += audio;
            turnAudio += audio;
            spark.Append(rms < 150 ? '.' : rms < 500 ? 'o' : rms < 1500 ? 'O' : '#');
            if (turn)
            {
                turns++;
                Console.WriteLine($"  TOUR {turns} fini à t={f * 0.1:F1} s ({turnAudio} chunks audio envoyés).");
                if (turnPcm.Count > 0)
                {
                    string dump = Path.Combine(Path.GetTempPath(), $"livesim-tour-{turns}.wav");
                    using var writer = new NAudio.Wave.WaveFileWriter(dump,
                        new NAudio.Wave.WaveFormat(16000, 16, 1));
                    foreach (byte[] pcm in turnPcm)
                    {
                        writer.Write(pcm, 0, pcm.Length);
                    }
                    Console.WriteLine($"  audio envoyé : {dump}.");
                    turnPcm.Clear();
                }
                turnAudio = 0;
                Thread.Sleep(2300);
                session.SimulateModelActivity();
            }
            int waitMs = (f + 1) * 100 - (int)watch.ElapsedMilliseconds;
            if (waitMs > 0)
            {
                Thread.Sleep(waitMs);
            }
        }
        Console.WriteLine("Niveaux : " + spark.ToString());
        Console.WriteLine($"Chunks : {frames}, forts (>=500) : {loudChunks}, audio envoyés : {sentChunks}, tours : {turns}.");
        Console.WriteLine($"RMS max : {maxRms:F0}.");
        return true;
    }

    public static bool Enroll()
    {
        string? key;
        try
        {
            key = TabletApp.AI.GeminiKey.Resolve();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Clé Gemini : {ex.Message} -> KO");
            return false;
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.WriteLine("Clé Gemini manquante -> KO");
            return false;
        }
        var speech = new TabletApp.AI.GeminiSpeech(key);
        async Task Speak(string text, CancellationToken ct)
        {
            Console.WriteLine($"Robot : {text}");
            byte[]? asset = TabletApp.AI.WakeEnrollment.LoadPrompt(text);
            byte[] wav = asset
                ?? await speech.SpeakAsync(text, ct).ConfigureAwait(false);
            await TabletApp.AI.NAudioVoice.PlayAsync(wav, ct).ConfigureAwait(false);
        }
        Task<TabletApp.AI.VoiceClip> Record(int seconds, CancellationToken ct)
        {
            Console.WriteLine($"  (enregistrement {seconds} s...)");
            return TabletApp.AI.NAudioVoice.RecordAsync(TimeSpan.FromSeconds(seconds), ct);
        }
        try
        {
            TabletApp.AI.CalibrationVerdict verdict = TabletApp.AI.WakeEnrollment.RunWizardAsync(
                Speak, Record, (wav, ct) => speech.TranscribeAsync(wav, ct), Console.WriteLine, _ => { },
                TabletApp.AI.CalibrationPaths.Default, CancellationToken.None)
                .GetAwaiter().GetResult();
            Console.WriteLine(verdict.Ok
                ? $"Enrôlement : OK ({verdict.Reason})."
                : $"Enrôlement : {verdict.Reason} (réglages livrés conservés).");
            return verdict.Ok;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Enrôlement impossible : {ex.Message} -> KO");
            return false;
        }
    }

    public static bool EnrollSim(IReadOnlyList<string> files)
    {
        string? key;
        try
        {
            key = TabletApp.AI.GeminiKey.Resolve();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Clé Gemini : {ex.Message} -> KO");
            return false;
        }
        if (string.IsNullOrWhiteSpace(key) || files.Count < 2)
        {
            Console.WriteLine("Usage : --enroll-sim silence.wav prise1.wav [prise2...] (clé requise).");
            return false;
        }
        var speech = new TabletApp.AI.GeminiSpeech(key);
        int next = 0;
        Task<TabletApp.AI.VoiceClip> Record(int seconds, CancellationToken ct)
        {
            int at = Math.Min(next++, files.Count - 1);
            byte[] wav = File.ReadAllBytes(files[at]);
            byte[] pcm = new byte[Math.Max(0, wav.Length - 44)];
            Array.Copy(wav, Math.Min(44, wav.Length), pcm, 0, pcm.Length);
            double peak = 0;
            for (int off = 0; off + 3200 <= pcm.Length; off += 3200)
            {
                double rms = TabletApp.AI.NAudioVoice.ComputeRms(pcm.Skip(off).Take(3200).ToArray(), 3200);
                if (rms > peak)
                {
                    peak = rms;
                }
            }
            double secs = pcm.Length / 2.0 / 16000.0;
            Console.WriteLine($"  (prise simulée : {Path.GetFileName(files[at])}, pic {peak:F0})");
            return Task.FromResult(new TabletApp.AI.VoiceClip(
                wav, peak >= 500, peak, TimeSpan.FromSeconds(secs)));
        }
        string dir = Path.Combine(Path.GetTempPath(), "enroll-sim-" + Path.GetRandomFileName());
        try
        {
            TabletApp.AI.CalibrationVerdict verdict = TabletApp.AI.WakeEnrollment.RunWizardAsync(
                (text, ct) => { Console.WriteLine($"Robot : {text}"); return Task.CompletedTask; },
                Record,
                (wav, ct) => speech.TranscribeAsync(wav, ct),
                Console.WriteLine,
                phase => Console.WriteLine($"  [phase {phase}]"),
                new TabletApp.AI.CalibrationPaths(
                    TabletApp.AI.CalibrationPaths.Default.BankPath,
                    Path.Combine(dir, "keywords-patate.txt"),
                    Path.Combine(dir, "wake-calibration.json")),
                CancellationToken.None, dir).GetAwaiter().GetResult();
            Console.WriteLine($"Verdict : {(verdict.Ok ? "OK" : "KO")} ({verdict.Reason}).");
            if (verdict.Ok)
            {
                byte[] kw = File.ReadAllBytes(Path.Combine(dir, "keywords-patate.txt"));
                Console.WriteLine($"  mots-clés : {System.Text.Encoding.UTF8.GetString(kw).Trim()} " +
                    $"(BOM : {kw.Length >= 3 && kw[0] == 0xEF && kw[1] == 0xBB && kw[2] == 0xBF})");
                TabletApp.AI.WakeWordOptions loaded = TabletApp.AI.WakeWordOptions.Load(
                    Path.Combine(Path.GetTempPath(), "tabletapp-jamais-un-fichier.json"),
                    Path.Combine(dir, "wake-calibration.json"));
                Console.WriteLine($"  relus : seuil={loaded.Threshold} porte={loaded.GateRms}.");
            }
            return verdict.Ok;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Sim impossible : {ex.Message} -> KO");
            return false;
        }
    }

    public static bool WakePick(IReadOnlyList<string> takes, string? silence, bool apply)
    {
        string silencePath = silence ?? Path.Combine(Path.GetTempPath(), "wake-sil.wav");
        try
        {
            TabletApp.AI.CalibrationVerdict verdict = TabletApp.AI.WakeEnrollment.CalibrateAsync(
                takes, silencePath,
                TabletApp.AI.CalibrationPaths.Default.BankPath,
                Console.WriteLine, CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine($"Verdict : {(verdict.Ok ? "OK" : "KO")} ({verdict.Reason}).");
            if (verdict.Ok)
            {
                Console.WriteLine($"  gagnant : {verdict.WinnerLine}");
                Console.WriteLine($"  seuil={verdict.Threshold} porte={verdict.GateRms}");
            }
            if (verdict.Ok && apply && verdict.WinnerLine is not null)
            {
                TabletApp.AI.WakeEnrollment.EnsureOverrideModelDir();
                TabletApp.AI.WakeEnrollment.WriteKeywordsNoBom(
                    TabletApp.AI.CalibrationPaths.Default.OverrideKeywordsPath,
                    new[] { verdict.WinnerLine });
                TabletApp.AI.WakeWordOptions.SaveOverride(
                    TabletApp.AI.CalibrationPaths.Default.OverrideOptionsPath,
                    new TabletApp.AI.WakeWordOptions(verdict.Threshold, 1.0f, verdict.GateRms),
                    takes.Count, verdict.WinnerLine);
                Console.WriteLine("  calibration écrite.");
            }
            return verdict.Ok;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Pick impossible : {ex.Message} -> KO");
            return false;
        }
    }

    /// <summary>
    /// Balayage hors-ligne : rejoue un WAV 16 kHz mono dans le vrai détecteur
    /// sherpa + écouteur, pour seuils × portes. Affiche les détections par réglage.
    /// </summary>
    private static bool TryLoadMono16k(string path, out float[]? samples)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Échantillon introuvable : {path} (lancez --wake-record).");
            samples = null;
            return false;
        }
        if (!TabletApp.AI.NAudioVoice.LoadMono16k(path, out samples))
        {
            Console.WriteLine($"Lecture impossible : {path} -> KO");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Décode un WAV avec le transducteur lui-même (téléphones) pour écrire
    /// les variantes du fichier keywords (--wake-decode).
    /// </summary>
    /// <summary>
    /// Transcrit un WAV via Gemini STT (témoin indépendant, --stt-file).
    /// </summary>
    /// <summary>
    /// Synthétise un texte via Gemini TTS vers %TEMP%\say.wav (--say).
    /// </summary>
    public static bool Say(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Console.WriteLine("Usage : --say « texte ».");
            return false;
        }
        string? key;
        try
        {
            key = TabletApp.AI.GeminiKey.Resolve();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Clé Gemini : {ex.Message} -> KO");
            return false;
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.WriteLine("Clé Gemini manquante -> KO");
            return false;
        }
        try
        {
            var speech = new TabletApp.AI.GeminiSpeech(key);
            byte[] wav = speech.SpeakAsync(text, CancellationToken.None).GetAwaiter().GetResult();
            string path = Path.Combine(Path.GetTempPath(), "say.wav");
            File.WriteAllBytes(path, wav);
            Console.WriteLine($"Dit : {path} ({wav.Length / 1024} Ko).");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Synthèse impossible : {ex.Message} -> KO");
            return false;
        }
    }

    /// <summary>Flag --presynth : régénère les consignes d'enrôlement (voix TTS courante).</summary>
    public static bool Presynth()
    {
        string? key;
        try
        {
            key = TabletApp.AI.GeminiKey.Resolve();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Clé Gemini : {ex.Message} -> KO");
            return false;
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.WriteLine("Clé Gemini manquante -> KO");
            return false;
        }
        string? dir = FindAudioSourceDir();
        if (dir is null)
        {
            Console.WriteLine("Dossier src/TabletApp/Models/audio introuvable -> KO");
            return false;
        }
        try
        {
            var speech = new TabletApp.AI.GeminiSpeech(key);
            foreach ((string name, string text) in TabletApp.AI.WakeEnrollment.Prompts)
            {
                byte[] wav = speech.SpeakAsync(text, CancellationToken.None).GetAwaiter().GetResult();
                if (wav.Length < 1000 || wav[0] != (byte)'R' || wav[1] != (byte)'I'
                    || wav[2] != (byte)'F' || wav[3] != (byte)'F')
                {
                    Console.WriteLine($"{name} : réponse invalide ({wav.Length} o) -> KO");
                    return false;
                }
                File.WriteAllBytes(Path.Combine(dir, name + ".wav"), wav);
                Console.WriteLine($"{name}.wav : {wav.Length / 1024} Ko.");
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Presynth : {ex.Message} -> KO");
            return false;
        }
    }

    private static string? FindAudioSourceDir()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            string p = Path.Combine(dir, "src", "TabletApp", "Models", "audio");
            if (Directory.Exists(p))
            {
                return p;
            }
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    public static bool SttFile(string? path)
    {
        path ??= Path.Combine(Path.GetTempPath(), "wake-heypatate.wav");
        if (!File.Exists(path))
        {
            Console.WriteLine($"Fichier introuvable : {path}.");
            return false;
        }
        string? key;
        try
        {
            key = TabletApp.AI.GeminiKey.Resolve();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Clé Gemini : {ex.Message} -> KO");
            return false;
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.WriteLine("Clé Gemini manquante -> KO");
            return false;
        }
        try
        {
            var speech = new TabletApp.AI.GeminiSpeech(key);
            string? text = speech.TranscribeAsync(
                File.ReadAllBytes(path), CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine("STT : " + (text ?? "(rien)"));
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"STT impossible : {ex.Message} -> KO");
            return false;
        }
    }

    /// <summary>
    /// Teste un fichier keywords candidat sur un WAV (découverte de variantes,
    /// --wake-try fic [wav] [seuil]). Affiche les coups par label.
    /// </summary>
    /// <summary>
    /// Rejoue un WAV en temps réel dans le vrai écouteur (réglages prod) :
    /// prédit exactement le comportement live (--wake-live [wav] [porte]).
    /// </summary>
    public static bool WakeSlice(string? path, double fromSec, double toSec, string? dest)
    {
        path ??= Path.Combine(Path.GetTempPath(), "wake-heypatate.wav");
        if (!TryLoadMono16k(path, out float[]? samples) || samples is null)
        {
            return false;
        }
        int from = Math.Clamp((int)(fromSec * 16000), 0, samples.Length);
        int to = Math.Clamp((int)(toSec * 16000), 0, samples.Length);
        if (to <= from)
        {
            Console.WriteLine("Usage : --wake-slice [wav] t0 t1 [dst.wav] (t0 < t1, secondes).");
            return false;
        }
        dest ??= Path.Combine(Path.GetTempPath(), $"wake-slice-{fromSec:F1}-{toSec:F1}.wav");
        using var writer = new NAudio.Wave.WaveFileWriter(dest, new NAudio.Wave.WaveFormat(16000, 16, 1));
        byte[] bytes = new byte[(to - from) * 2];
        for (int i = from; i < to; i++)
        {
            short v = (short)Math.Clamp(samples[i] * 32768.0, short.MinValue, short.MaxValue);
            bytes[(i - from) * 2] = (byte)(v & 0xFF);
            bytes[(i - from) * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        writer.Write(bytes, 0, bytes.Length);
        Console.WriteLine($"Tranche écrite : {dest} ({(to - from) / 16000.0:F1} s).");
        return true;
    }

    public static bool WakeLive(string? path, double gateRms, string? keywordsPath = null)
    {
        path ??= Path.Combine(Path.GetTempPath(), "wake-heypatate.wav");
        if (!TryLoadMono16k(path, out float[]? samples) || samples is null)
        {
            return false;
        }
        TabletApp.AI.WakeWordOptions options = TabletApp.AI.WakeWordOptions.Load();
        if (!TabletApp.AI.SherpaWakeDetector.TryCreate(
            options.Threshold, options.Score, out TabletApp.AI.SherpaWakeDetector? detector, keywordsPath)
            || detector is null)
        {
            Console.WriteLine("Détecteur : IMPOSSIBLE -> KO");
            return false;
        }
        using (detector)
        {
            using var listener = new TabletApp.AI.WakeWordListener(detector, gateRms: gateRms);
            int fed = 0;
            listener.Detected += () => Console.WriteLine($"  HIT {detector.LastKeyword} @ t={fed * 0.1:F1}s");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int frame = 1600;
            for (int at = 0; at + frame <= samples.Length; at += frame)
            {
                float[] chunk = new float[frame];
                Array.Copy(samples, at, chunk, 0, frame);
                listener.OnAudio(chunk);
                fed++;
                int waitMs = (fed + 1) * 100 - (int)watch.ElapsedMilliseconds;
                if (waitMs > 0)
                {
                    Thread.Sleep(waitMs);
                }
            }
            Console.WriteLine($"Rejeu temps réel terminé ({fed} tampons).");
        }
        return true;
    }

    public static bool WakeTry(string? keywordsPath, string? path, float threshold, float score)
    {
        if (string.IsNullOrWhiteSpace(keywordsPath) || !File.Exists(keywordsPath))
        {
            Console.WriteLine("Usage : --wake-try keywords.txt [wav] [seuil].");
            return false;
        }
        path ??= Path.Combine(Path.GetTempPath(), "wake-heypatate.wav");
        if (!TryLoadMono16k(path, out float[]? samples) || samples is null)
        {
            return false;
        }
        if (!TabletApp.AI.SherpaWakeDetector.TryCreate(
            threshold, score, out TabletApp.AI.SherpaWakeDetector? detector, keywordsPath)
            || detector is null)
        {
            Console.WriteLine("Détecteur : IMPOSSIBLE -> KO");
            return false;
        }
        using (detector)
        {
            var hits = new Dictionary<string, int>(StringComparer.Ordinal);
            const int frame = 1600;
            for (int at = 0; at + frame <= samples.Length; at += frame)
            {
                float[] chunk = new float[frame];
                Array.Copy(samples, at, chunk, 0, frame);
                detector.Feed(chunk);
                while (detector.TakeDetected())
                {
                    string label = detector.LastKeyword;
                    hits[label] = hits.TryGetValue(label, out int n) ? n + 1 : 1;
                    Console.WriteLine($"  HIT {label} @ t={at / 16000.0:F1}s");
                }
            }
            if (hits.Count == 0)
            {
                Console.WriteLine("  (aucun hit)");
            }
            foreach (var pair in hits.OrderByDescending(p => p.Value))
            {
                Console.WriteLine($"  {pair.Key} : {pair.Value}");
            }
        }
        return true;
    }

    public static bool WakeDecode(string? path, double gain)
    {
        path ??= Path.Combine(Path.GetTempPath(), "wake-heypatate.wav");
        if (!TryLoadMono16k(path, out float[]? samples) || samples is null)
        {
            return false;
        }
        if (Math.Abs(gain - 1.0) > 0.001)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Math.Clamp(samples[i] * (float)gain, -1f, 1f);
            }
            Console.WriteLine($"Gain appliqué : x{gain:F1}.");
        }
        string dir = TabletApp.AI.SherpaWakeDetector.ResolveModelDir();
        try
        {
            var modelConfig = new SherpaOnnx.OnlineModelConfig();
            modelConfig.Transducer.Encoder = Path.Combine(dir, "encoder-epoch-13-avg-2-chunk-8-left-64.int8.onnx");
            modelConfig.Transducer.Decoder = Path.Combine(dir, "decoder-epoch-13-avg-2-chunk-8-left-64.onnx");
            modelConfig.Transducer.Joiner = Path.Combine(dir, "joiner-epoch-13-avg-2-chunk-8-left-64.int8.onnx");
            modelConfig.Tokens = Path.Combine(dir, "tokens.txt");
            modelConfig.NumThreads = 1;
            modelConfig.Provider = "cpu";
            modelConfig.Debug = 1;
            modelConfig.ModelingUnit = "phone";
            var featConfig = new SherpaOnnx.FeatureConfig();
            featConfig.SampleRate = 16000;
            featConfig.FeatureDim = 80;
            var config = new SherpaOnnx.OnlineRecognizerConfig();
            config.FeatConfig = featConfig;
            config.ModelConfig = modelConfig;
            config.DecodingMethod = "greedy_search";
            config.EnableEndpoint = 0;
            using var recognizer = new SherpaOnnx.OnlineRecognizer(config);
            using var stream = recognizer.CreateStream();
            const int frame = 1600;
            int decodes = 0;
            for (int at = 0; at < samples.Length; at += frame)
            {
                int n = Math.Min(frame, samples.Length - at);
                float[] chunk = new float[n];
                Array.Copy(samples, at, chunk, 0, n);
                stream.AcceptWaveform(16000, chunk);
                while (recognizer.IsReady(stream))
                {
                    recognizer.Decode(stream);
                    decodes++;
                }
            }
            stream.InputFinished();
            while (recognizer.IsReady(stream))
            {
                recognizer.Decode(stream);
                decodes++;
            }
            Console.WriteLine($"Décodages : {decodes}");
            SherpaOnnx.OnlineRecognizerResult decoded = recognizer.GetResult(stream);
            Console.WriteLine("Téléphones : " + decoded.Text);
            Console.WriteLine("Tokens : " + string.Join(" ", decoded.Tokens));
            for (int s = 0; s < samples.Length / 16000; s++)
            {
                double sum = 0;
                for (int i = 0; i < 16000; i++)
                {
                    double v = samples[s * 16000 + i];
                    sum += v * v;
                }
                Console.WriteLine($"  t={s}s RMS={Math.Sqrt(sum / 16000) * 32768:F0}");
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Décodage impossible : {ex.Message} -> KO");
            return false;
        }
    }

    public static bool WakeSweep(string? path, double gain)
    {
        path ??= Path.Combine(Path.GetTempPath(), "wake-heypatate.wav");
        if (!TryLoadMono16k(path, out float[]? samples) || samples is null)
        {
            return false;
        }

        if (Math.Abs(gain - 1.0) > 0.001)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = Math.Clamp(samples[i] * (float)gain, -1f, 1f);
            }
            Console.WriteLine($"Gain appliqué : x{gain:F1}.");
        }
        const int frame = 1600;
        int frames = samples.Length / frame;
        double maxRms = 0;
        int[] above = new int[3];
        double[] gates = { 50.0 / 32768, 150.0 / 32768, 300.0 / 32768 };
        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            for (int i = 0; i < frame; i++)
            {
                double s = samples[f * frame + i];
                sum += s * s;
            }
            double rms = Math.Sqrt(sum / frame);
            if (rms > maxRms)
            {
                maxRms = rms;
            }
            for (int g = 0; g < gates.Length; g++)
            {
                if (rms >= gates[g])
                {
                    above[g]++;
                }
            }
        }
        Console.WriteLine($"Échantillon : {samples.Length / 16000.0:F1} s, RMS max {maxRms * 32768:F0}, " +
            $"buffers au-dessus des portes 50/150/300 : {above[0]}/{above[1]}/{above[2]}/{frames}.");
        var spark = new System.Text.StringBuilder();
        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            for (int i = 0; i < frame; i++)
            {
                double s = samples[f * frame + i];
                sum += s * s;
            }
            double r = Math.Sqrt(sum / frame) * 32768;
            spark.Append(r < 50 ? '.' : r < 150 ? 'o' : r < 300 ? 'O' : '#');
        }
        Console.WriteLine("Niveaux : " + spark.ToString());
        float[] thresholds = { 0.05f, 0.10f, 0.15f, 0.25f, 0.35f };
        double[] gateRms = { 50.0, 150.0, 300.0 };
        foreach (float threshold in thresholds)
        {
            if (!TabletApp.AI.SherpaWakeDetector.TryCreate(
                threshold, 1.0f, out TabletApp.AI.SherpaWakeDetector? detector) || detector is null)
            {
                Console.WriteLine("Détecteur : IMPOSSIBLE (modèle absent ?) -> KO");
                return false;
            }
            using (detector)
            {
                foreach (double gate in gateRms)
                {
                    detector.Reset();
                    using var listener = new TabletApp.AI.WakeWordListener(detector, gateRms: gate);
                    int fires = 0;
                    listener.Detected += () => fires++;
                    for (int f = 0; f < frames; f++)
                    {
                        float[] chunk = new float[frame];
                        Array.Copy(samples, f * frame, chunk, 0, frame);
                        listener.OnAudio(chunk);
                    }
                    Console.WriteLine($"  seuil={threshold:F2} porte={gate:F0} -> détections={fires}");
                }
            }
        }
        return true;
    }

    public static bool WakeProbe()
    {
        Console.WriteLine("Micros WinMM :");
        for (int i = 0; i < NAudio.Wave.WaveIn.DeviceCount; i++)
        {
            NAudio.Wave.WaveInCapabilities caps = NAudio.Wave.WaveIn.GetCapabilities(i);
            Console.WriteLine($"  {i} : {caps.ProductName} ({caps.Channels} canaux)");
        }
        TabletApp.AI.WakeWordOptions options = TabletApp.AI.WakeWordOptions.Load();
        string dir = TabletApp.AI.SherpaWakeDetector.ResolveModelDir();
        Console.WriteLine($"Modèle : {dir}");
        Console.WriteLine($"Seuils : porte {options.GateRms:F0} RMS, threshold {options.Threshold:F2}, score {options.Score:F1}.");
        string kw = Path.Combine(dir, TabletApp.AI.SherpaWakeDetector.KeywordsFileName);
        if (File.Exists(kw))
        {
            foreach (string line in File.ReadAllLines(kw))
            {
                Console.WriteLine($"  mot-clé : {line}");
            }
        }
        if (!TabletApp.AI.SherpaWakeDetector.TryCreate(
            options.Threshold, options.Score, out TabletApp.AI.SherpaWakeDetector? detector)
            || detector is null)
        {
            Console.WriteLine("Détecteur : IMPOSSIBLE (modèle absent ?) -> KO");
            return false;
        }
        using (detector)
        {
            using var listener = new TabletApp.AI.WakeWordListener(detector, gateRms: options.GateRms);
            object sync = new();
            int buffers = 0;
            int open = 0;
            double maxRms = 0;
            int hits = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var window = System.Diagnostics.Stopwatch.StartNew();
            double wmax = 0;
            int wopen = 0;
            int wbufs = 0;
            listener.Detected += () =>
            {
                lock (sync)
                {
                    hits++;
                }
                Console.WriteLine($"  >>> DÉTECTÉ : {detector.LastKeyword} à {watch.Elapsed.TotalSeconds:F1} s");
            };
            listener.Tick += tick =>
            {
                lock (sync)
                {
                    buffers++;
                    if (tick.GateOpen)
                    {
                        open++;
                    }
                    if (tick.Rms > maxRms)
                    {
                        maxRms = tick.Rms;
                    }
                    wbufs++;
                    if (tick.Rms > wmax)
                    {
                        wmax = tick.Rms;
                    }
                    if (tick.GateOpen)
                    {
                        wopen++;
                    }
                    if (window.Elapsed.TotalSeconds >= 1)
                    {
                        Console.WriteLine($"  t={watch.Elapsed.TotalSeconds:F0}s RMS max={wmax * 32768:F0} " +
                            $"porte={wopen}/{wbufs} alim={(tick.Feeding ? "oui" : "non")}");
                        window.Restart();
                        wmax = 0;
                        wopen = 0;
                        wbufs = 0;
                    }
                }
            };
            Console.WriteLine("Dites « Hey Patate » (30 s)...");
            listener.Start();
            Thread.Sleep(TimeSpan.FromSeconds(30));
            listener.Stop();
            Console.WriteLine($"Tampons : {buffers}, porte ouverte : {open}, " +
                $"max RMS : {maxRms * 32768:F0}, détections : {hits}.");
            if (buffers == 0)
            {
                Console.WriteLine("INDICE : aucun tampon reçu, micro inaccessible (confidentialité Windows ?).");
            }
            else if (maxRms * 32768 < 20)
            {
                Console.WriteLine("INDICE : micro quasi muet, vérifier le micro par défaut et son volume.");
            }
            else if (open == 0)
            {
                Console.WriteLine("INDICE : la porte ne s'ouvre pas, parler plus fort/près ou baisser WakeWord:GateRms.");
            }
            else if (hits == 0)
            {
                Console.WriteLine("INDICE : parole captée mais pas reconnue, baisser WakeWord:Threshold (ex. 0.15).");
            }
            else
            {
                Console.WriteLine("WAKE-PROBE: OK");
            }
        }
        return true;
    }

    public static bool Sounds()
    {
        string[] demo = { "giggle", "curious", "sad", "wow", "oops", "snore", "reveil", "baillement", "erreur" };
        foreach (string name in demo)
        {
            byte[]? pcm = RobotSounds.Render(name);
            if (pcm is null)
            {
                Console.WriteLine($"Son {name} : INTROUVABLE -> KO");
                return false;
            }
            Console.WriteLine($"Son {name} ({pcm.Length / 48000.0:F1} s)...");
            NAudioVoice.PlayAsync(RobotSounds.ToWav(pcm), CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        byte[] phrase = DemoPhrase();
        Console.WriteLine("Voix brute...");
        NAudioVoice.PlayAsync(RobotSounds.ToWav(phrase), CancellationToken.None, raw: true)
            .GetAwaiter().GetResult();
        Console.WriteLine("Voix filtrée (Voice:RobotFilter)...");
        NAudioVoice.PlayAsync(RobotSounds.ToWav(phrase), CancellationToken.None)
            .GetAwaiter().GetResult();
        Console.WriteLine("SONS: OK");
        return true;
    }

    // Fausse voyelle "aaaah" (3 harmoniques + vibrato) pour juger le filtre.
    private static byte[] DemoPhrase()
    {
        const int rate = 24000;
        const double seconds = 1.4;
        int n = (int)(seconds * rate);
        byte[] pcm = new byte[n * 2];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            double f = 180 - 70 * t / seconds;
            f *= 1.0 + 0.02 * Math.Sin(2.0 * Math.PI * 5 * t);
            phase += 2.0 * Math.PI * f / rate;
            double v = Math.Sin(phase) + 0.4 * Math.Sin(2 * phase) + 0.15 * Math.Sin(3 * phase);
            double env = Math.Sin(Math.PI * Math.Min(1.0, t / seconds));
            short s = (short)(7000 * env * v / 1.55);
            pcm[i * 2] = (byte)(s & 0xFF);
            pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return pcm;
    }

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
