namespace TabletApp.AI;

/// <summary>Résultat par candidat : prises touchées + silence.</summary>
internal sealed record CandidateScore(string Label, string Line, bool[] TakeHits, bool SilenceHit);

/// <summary>Verdict de calibration (gagnant + réglages).</summary>
internal sealed record CalibrationVerdict(bool Ok, string? WinnerLine, float Threshold, double GateRms, string Reason);

/// <summary>Chemins de calibration (injectables pour les tests).</summary>
internal sealed record CalibrationPaths(string BankPath, string OverrideKeywordsPath, string OverrideOptionsPath)
{
    public static CalibrationPaths Default => new(
        Path.Combine(SherpaWakeDetector.BundledModelDir(), "candidates-patate.txt"),
        Path.Combine(SherpaWakeDetector.OverrideModelDir(), SherpaWakeDetector.KeywordsFileName),
        WakeWordOptions.OverridePath);
}

/// <summary>
/// Enrôlement "Patate" : choisit la variante de mot-clé + les réglages qui
/// matchent la voix de l'utilisateur (3 prises min), validés en rejeu live
/// avec contrôle de faux positifs sur le silence ambiant. Le gagnant seul
/// est écrit (plusieurs variantes se cannibalisent, voir historique wake).
/// </summary>
internal static class WakeEnrollment
{
    internal const int MinTakes = 3;
    internal const int MaxTakes = 5;
    internal const float DiscoverThreshold = 0.15f;
    internal const float Score = 1.0f;
    internal static readonly float[] ThresholdGrid = { 0.25f, 0.15f };
    internal static readonly double[] GateGrid = { 150.0, 100.0 };

    public static string EnrollDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TabletApp", "enroll");

    public static bool IsCalibrated(string? overridePath = null) =>
        File.Exists(overridePath ?? WakeWordOptions.OverridePath);

    /// <summary>
    /// Découpe une prise au dernier épisode de parole (comme live : pont de
    /// 700 ms) précédé de 0,7 s de contexte (= le pré-roll live). Sans ça,
    /// 1 s+ de silence initial met l'encodeur streaming dans une phase où
    /// plus rien ne matche. Pur (testable sans modèle).
    /// </summary>
    internal static float[] TrimToLastEpisode(float[] samples, double gateRms, Action<string>? log = null)
    {
        const int frame = 1600;
        int frames = samples.Length / frame;
        if (frames == 0)
        {
            return samples;
        }
        double gate = gateRms / 32768.0;
        bool[] loud = new bool[frames];
        for (int f = 0; f < frames; f++)
        {
            double sum = 0;
            for (int i = 0; i < frame; i++)
            {
                double s = samples[f * frame + i];
                sum += s * s;
            }
            loud[f] = Math.Sqrt(sum / frame) >= gate;
        }
        int end = frames - 1;
        while (end >= 0 && !loud[end])
        {
            end--;
        }
        if (end < 0)
        {
            return samples;
        }
        int runStart = end;
        int gap = 0;
        for (int f = end; f >= 0; f--)
        {
            if (loud[f])
            {
                gap = 0;
                runStart = f;
            }
            else if (++gap >= 7)
            {
                break;
            }
        }
        int keepFrom = Math.Max(0, runStart - 7);
        log?.Invoke($"Découpe : {frames * 0.1:F1} s -> {(frames - keepFrom) * 0.1:F1} s " +
            $"(parole à {runStart * 0.1:F1} s).");
        if (keepFrom == 0)
        {
            return samples;
        }
        float[] trimmed = new float[samples.Length - keepFrom * frame];
        Array.Copy(samples, keepFrom * frame, trimmed, 0, trimmed.Length);
        return trimmed;
    }

    /// <summary>Écrit un fichier de mots-clés SANS BOM (sherpa refuse le BOM).</summary>
    internal static void WriteKeywordsNoBom(string path, IEnumerable<string> lines)
    {
        string? dir = Path.GetDirectoryName(path);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, string.Join("\n", lines) + "\n", new System.Text.UTF8Encoding(false));
    }

    /// <summary>
    /// Choisit le premier résultat qui touche au moins requiredHits prises
    /// (3 min) sans toucher le silence. L'appelant ordonne par préférence
    /// (seuil, porte, ordre de la banque). Pur (testable sans modèle).
    /// </summary>
    internal static CalibrationVerdict PickWinner(
        IReadOnlyList<(string Line, float Threshold, double Gate, bool[] TakeHits, bool SilenceHit)> results,
        int requiredHits)
    {
        foreach (var r in results)
        {
            int hits = r.TakeHits.Count(h => h);
            if (r.TakeHits.Length >= MinTakes && hits >= requiredHits && !r.SilenceHit)
            {
                return new CalibrationVerdict(true, r.Line, r.Threshold, r.Gate,
                    $"{hits}/{r.TakeHits.Length} prises + silence net");
            }
        }
        return new CalibrationVerdict(false, null,
            WakeWordOptions.DefaultThreshold, WakeWordOptions.DefaultGateRms,
            "aucun candidat ne couvre les prises sans faux positif");
    }

    /// <summary>
    /// Garantit un dossier modèle local complet (copie de l'embarqué) pour
    /// que les mots-clés calibrés survivent aux mises à jour.
    /// </summary>
    internal static void EnsureOverrideModelDir()
    {
        string bundled = SherpaWakeDetector.BundledModelDir();
        string local = SherpaWakeDetector.OverrideModelDir();
        Directory.CreateDirectory(local);
        foreach (string file in Directory.GetFiles(bundled))
        {
            string dest = Path.Combine(local, Path.GetFileName(file));
            if (!File.Exists(dest))
            {
                File.Copy(file, dest);
            }
        }
    }

    /// <summary>
    /// Étape 1 (rapide, plein flux) : candidats par petits lots mélangés
    /// (à 39 d'un coup, seuls les plus courts survivent à la compétition),
    /// un détecteur vierge par prise. But : que chaque vrai gagnant touche
    /// au moins une prise (partiel) pour passer au solo.
    /// </summary>
    internal static List<CandidateScore> SweepOffline(
        IReadOnlyList<string> bankLines,
        IReadOnlyList<float[]> takes,
        float[] silence,
        float threshold,
        int requiredHits,
        Action<string>? log = null,
        int batchSize = 8)
    {
        var scores = bankLines
            .Select(l => new CandidateScore(LabelOf(l), l, new bool[takes.Count], false))
            .ToList();
        int batches = Math.Max(1, (bankLines.Count + batchSize - 1) / batchSize);
        for (int b = 0; b < batches; b++)
        {
            List<string> batchLines = bankLines
                .Where((line, i) => i % batches == b).ToList();
            string batch = Path.Combine(Path.GetTempPath(), $"enroll-bank-{Guid.NewGuid:N}.txt");
            try
            {
                WriteKeywordsNoBom(batch, batchLines);
                if (!SherpaWakeDetector.TryCreate(threshold, Score, out SherpaWakeDetector? detector, batch)
                    || detector is null)
                {
                    continue;
                }
                using (detector)
                {
                    for (int t = 0; t < takes.Count; t++)
                    {
                        detector.Reset();
                        foreach (string label in FeedTake(detector, takes[t]))
                        {
                            CandidateScore? hit = scores.FirstOrDefault(s => s.Label == label);
                            if (hit is not null)
                            {
                                hit.TakeHits[t] = true;
                            }
                        }
                    }
                    detector.Reset();
                    foreach (string label in FeedTake(detector, silence))
                    {
                        int at = scores.FindIndex(s => s.Label == label);
                        if (at >= 0)
                        {
                            scores[at] = scores[at] with { SilenceHit = true };
                        }
                    }
                }
            }
            finally
            {
                File.Delete(batch);
            }
        }
        log?.Invoke($"Balayage ({batches} lots) : " +
            $"{scores.Count(s => s.TakeHits.Count(h => h) >= requiredHits)} couvrent " +
            $"{requiredHits}/{takes.Count} prises, {scores.Count(s => s.SilenceHit)} touchent le silence.");
        var partials = scores.Where(s => s.TakeHits.Any(h => h))
            .Select(s => s.Label + ":" + string.Join("", s.TakeHits.Select(h => h ? "1" : "0")));
        log?.Invoke("Partiels : " + (partials.Any() ? string.Join(" ", partials) : "(aucun)"));
        return scores;
    }

    /// <summary>
    /// Étape 1bis (toujours) : reteste en solo les partiels (au moins une
    /// prise, couverture incomplète), à l'abri de la compétition des lots.
    /// </summary>
    internal static List<CandidateScore> SweepSolo(
        IReadOnlyList<CandidateScore> partials,
        IReadOnlyList<float[]> takes,
        float[] silence,
        float threshold,
        int requiredHits,
        Action<string>? log = null)
    {
        var rescored = new List<CandidateScore>();
        foreach (CandidateScore cand in partials.Take(8))
        {
            string solo = Path.Combine(Path.GetTempPath(), $"enroll-solo-{Guid.NewGuid:N}.txt");
            try
            {
                WriteKeywordsNoBom(solo, new[] { cand.Line });
                if (!SherpaWakeDetector.TryCreate(threshold, Score, out SherpaWakeDetector? detector, solo)
                    || detector is null)
                {
                    continue;
                }
                using (detector)
                {
                    bool[] hits = new bool[takes.Count];
                    for (int t = 0; t < takes.Count; t++)
                    {
                        detector.Reset();
                        hits[t] = FeedTake(detector, takes[t]).Count > 0;
                    }
                    detector.Reset();
                    bool sil = FeedTake(detector, silence).Count > 0;
                    rescored.Add(new CandidateScore(cand.Label, cand.Line, hits, sil));
                }
            }
            finally
            {
                File.Delete(solo);
            }
        }
        log?.Invoke($"Solo : {rescored.Count(s => s.TakeHits.Count(h => h) >= requiredHits)} " +
            $"couvrent {requiredHits}/{takes.Count} prises.");
        return rescored;
    }

    /// <summary>
    /// Étape 2 (décisive, rejeu live avec porte) : valide un candidat aux
    /// réglages prod sur chaque prise + le silence. Détecteur vierge par prise.
    /// </summary>
    internal static async Task<(bool[] TakeHits, bool SilenceHit)> ValidateLiveAsync(
        string line, float threshold, double gate,
        IReadOnlyList<float[]> takes, float[] silence,
        Action<string>? log, CancellationToken ct)
    {
        string solo = Path.Combine(Path.GetTempPath(), $"enroll-live-{Guid.NewGuid:N}.txt");
        try
        {
            WriteKeywordsNoBom(solo, new[] { line });
            if (!SherpaWakeDetector.TryCreate(threshold, Score, out SherpaWakeDetector? detector, solo)
                || detector is null)
            {
                return (new bool[takes.Count], false);
            }
            using (detector)
            {
                bool[] hits = new bool[takes.Count];
                for (int t = 0; t < takes.Count; t++)
                {
                    ct.ThrowIfCancellationRequested();
                    detector.Reset();
                    hits[t] = await FeedLiveStreamAsync(detector, takes[t], gate, ct).ConfigureAwait(false);
                }
                detector.Reset();
                bool sil = await FeedLiveStreamAsync(detector, silence, gate, ct).ConfigureAwait(false);
                log?.Invoke($"Live {LabelOf(line)} seuil={threshold} porte={gate} : " +
                    $"{hits.Count(h => h)}/{takes.Count} + silence {(sil ? "TOUCHE" : "net")}.");
                return (hits, sil);
            }
        }
        finally
        {
            File.Delete(solo);
        }
    }

    /// <summary>
    /// Calibration complète : balayage, solo si besoin, validation live
    /// sur la grille (seuil, porte), dans l'ordre de préférence.
    /// </summary>
    internal static async Task<CalibrationVerdict> CalibrateAsync(
        IReadOnlyList<string> takePaths,
        string silencePath,
        string bankPath,
        Action<string>? log,
        CancellationToken ct)
    {
        if (takePaths.Count < MinTakes)
        {
            return new CalibrationVerdict(false, null,
                WakeWordOptions.DefaultThreshold, WakeWordOptions.DefaultGateRms,
                $"prises insuffisantes ({takePaths.Count}/{MinTakes})");
        }
        if (!File.Exists(bankPath) || !File.Exists(silencePath)
            || takePaths.Any(p => !File.Exists(p)))
        {
            return new CalibrationVerdict(false, null,
                WakeWordOptions.DefaultThreshold, WakeWordOptions.DefaultGateRms,
                "fichier audio ou banque introuvable");
        }
        var takes = new List<float[]>();
        foreach (string path in takePaths)
        {
            if (!NAudioVoice.LoadMono16k(path, out float[]? samples) || samples is null)
            {
                return new CalibrationVerdict(false, null,
                    WakeWordOptions.DefaultThreshold, WakeWordOptions.DefaultGateRms,
                    $"prise illisible : {path}");
            }
            takes.Add(samples);
        }
        if (!NAudioVoice.LoadMono16k(silencePath, out float[]? silence) || silence is null)
        {
            return new CalibrationVerdict(false, null,
                WakeWordOptions.DefaultThreshold, WakeWordOptions.DefaultGateRms,
                "silence illisible");
        }
        string[] bank = File.ReadAllLines(bankPath)
            .Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        int required = takePaths.Count >= 4 ? takePaths.Count - 1 : takePaths.Count;
        // Étape 0 : le défaut livré direct en live sur prises brutes (la
        // porte fait sa propre sélection d'épisodes). Cas typique : gagné
        // en ~30 s sans balayer la banque.
        string shortLine = bank.FirstOrDefault(l => LabelOf(l).Contains("SHORT")) ?? bank[0];
        log?.Invoke("Direct : " + LabelOf(shortLine) + " en live sur prises brutes.");
        foreach (float threshold in ThresholdGrid)
        {
            foreach (double gate in GateGrid)
            {
                (bool[] directHits, bool directSil) = await ValidateLiveAsync(
                    shortLine, threshold, gate, takes, silence, log, ct).ConfigureAwait(false);
                CalibrationVerdict direct = PickWinner(
                    new[] { (shortLine, threshold, gate, directHits, directSil) }, required);
                if (direct.Ok)
                {
                    return direct;
                }
            }
        }
        // Étapes 1-2 : balayage offline sur prises découpées (0,7 s de
        // contexte, comme le pré-roll), puis validation live sur brutes.
        List<float[]> trimmed = takes
            .Select(t => TrimToLastEpisode(t, WakeWordOptions.DefaultGateRms, log)).ToList();
        List<CandidateScore> scores = SweepOffline(bank, trimmed, silence, DiscoverThreshold, required, log);
        List<CandidateScore> full = scores.Where(s => s.TakeHits.Count(h => h) >= required).ToList();
        // Toujours retester les partiels en solo : la compétition masque des
        // gagnants (SHORT masqué par les plus courts sur certaines prises).
        List<CandidateScore> partials = scores
            .Where(s => s.TakeHits.Any(h => h) && s.TakeHits.Count(h => h) < required)
            .OrderByDescending(s => s.TakeHits.Count(h => h)).ToList();
        List<CandidateScore> soloFull = SweepSolo(partials, trimmed, silence, DiscoverThreshold, required, log)
            .Where(s => s.TakeHits.Count(h => h) >= required).ToList();
        var seen = new HashSet<string>(full.Select(s => s.Label));
        foreach (CandidateScore s in soloFull)
        {
            if (seen.Add(s.Label))
            {
                full.Add(s);
            }
        }
        full.Sort((a, b) => scores.FindIndex(s => s.Label == a.Label)
            .CompareTo(scores.FindIndex(s => s.Label == b.Label)));
        var results = new List<(string Line, float Threshold, double Gate, bool[] TakeHits, bool SilenceHit)>();
        foreach (float threshold in ThresholdGrid)
        {
            foreach (double gate in GateGrid)
            {
                foreach (CandidateScore cand in full.Take(3))
                {
                    (bool[] hits, bool sil) = await ValidateLiveAsync(
                        cand.Line, threshold, gate, takes, silence, log, ct).ConfigureAwait(false);
                    results.Add((cand.Line, threshold, gate, hits, sil));
                    CalibrationVerdict early = PickWinner(results, required);
                    if (early.Ok)
                    {
                        return early;
                    }
                }
            }
        }
        return PickWinner(results, required);
    }

    private static string LabelOf(string line)
    {
        int at = line.LastIndexOf('@');
        return at >= 0 ? line[(at + 1)..].Trim() : line.Trim();
    }

    /// <summary>
    /// Rejoue un extrait en plein flux sur un détecteur déjà ouvert
    /// (l'appelant fait Reset() entre prises : le modèle reste chargé).
    /// </summary>
    private static List<string> FeedTake(SherpaWakeDetector detector, float[] samples)
    {
        var labels = new List<string>();
        const int frame = 1600;
        for (int at = 0; at + frame <= samples.Length; at += frame)
        {
            float[] chunk = new float[frame];
            Array.Copy(samples, at, chunk, 0, frame);
            detector.Feed(chunk);
            while (detector.TakeDetected())
            {
                labels.Add(detector.LastKeyword);
            }
        }
        return labels;
    }

    /// <summary>
    /// Rejoue un extrait en live (porte + temps réel) sur un détecteur déjà
    /// ouvert (l'appelant fait Reset() entre prises : le modèle reste chargé).
    /// </summary>
    private static async Task<bool> FeedLiveStreamAsync(
        SherpaWakeDetector detector, float[] samples, double gate, CancellationToken ct)
    {
        using var listener = new WakeWordListener(detector, gateRms: gate, ownsDetector: false);
        int fires = 0;
        listener.Detected += () => fires++;
        const int frame = 1600;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int fed = 0;
        for (int at = 0; at + frame <= samples.Length; at += frame)
        {
            ct.ThrowIfCancellationRequested();
            float[] chunk = new float[frame];
            Array.Copy(samples, at, chunk, 0, frame);
            listener.OnAudio(chunk);
            fed++;
            int waitMs = fed * 100 - (int)watch.ElapsedMilliseconds;
            if (waitMs > 0)
            {
                await Task.Delay(waitMs, ct).ConfigureAwait(false);
            }
        }
        return fires > 0;
    }

    /// <summary>Phases du wizard (le visage suit, la console ignore).</summary>
    internal enum EnrollPhase
    {
        Ambient,
        Takes,
        Thinking,
        DoneOk,
        DoneKo,
    }

    /// <summary>
    /// Prise franche ? Exactement "Hey, Patate !" (ou "Patate"), sans doublon
    /// ni autre mot. "Hein ? Hé, patate" et "Patate. Hé, patate" sont refusés :
    /// aucune variante ne calibre sur des prises incohérentes. Pur (testé).
    /// </summary>
    internal static bool IsCleanTake(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return false;
        }
        string norm = transcript.ToLowerInvariant();
        foreach (char p in new[] { '.', ',', '?', '!', ';', ':', '"', '«', '»', '\'', '\n', '\r' })
        {
            norm = norm.Replace(p, ' ');
        }
        norm = string.Join(" ", norm.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return norm is "hey patate" or "hé patate" or "he patate" or "patate";
    }

    private static string ShortEcho(string? heard)
    {
        string clean = string.Join(" ", (heard ?? "").Split(
            new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        return clean.Length <= 60 ? clean : clean.Substring(0, 60) + "...";
    }

    /// <summary>
    /// Consignes du wizard pré-synthétisées (zéro token, zéro latence, hors
    /// ligne). La ligne dynamique ("J'ai entendu...") reste en TTS.
    /// </summary>
    internal static readonly (string Name, string Text)[] Prompts =
    {
        ("enroll-hello", "Bonjour, je m'appelle Patate ! Pour mieux te connaître, reste silencieux quelques secondes..."),
        ("enroll-repeat", "Répète après moi : Hey, Patate !"),
        ("enroll-again", "Encore une fois, Hey, Patate !"),
        ("enroll-thinking", "Attends-moi, je réfléchis..."),
        ("enroll-ok", "C'est bon, je te reconnaîtrai !"),
        ("enroll-ko", "J'ai du mal à te reconnaître, je garde mes réglages de départ."),
        ("enroll-unheard", "Je n'ai rien entendu, on recommence..."),
        ("enroll-canthear", "J'ai du mal à t'entendre, je garde mes réglages de départ."),
    };

    internal static string PromptAudioDir =>
        Path.Combine(AppContext.BaseDirectory, "Models", "audio");

    /// <summary>Nom d'asset pour un texte de consigne, null si dynamique.</summary>
    internal static string? PromptAssetName(string text)
    {
        foreach ((string name, string prompt) in Prompts)
        {
            if (prompt == text)
            {
                return name + ".wav";
            }
        }
        return null;
    }

    /// <summary>Octets WAV pré-synthétisés, null si absent (repli TTS).</summary>
    internal static byte[]? LoadPrompt(string text)
    {
        string? name = PromptAssetName(text);
        if (name is null)
        {
            return null;
        }
        string path = Path.Combine(PromptAudioDir, name);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    /// <summary>
    /// Wizard parlé : silence ambiant, prises "Hey, Patate !" (contrôlées par
    /// transcription), calibration, écriture des réglages. IO injectées.
    /// </summary>
    internal static async Task<CalibrationVerdict> RunWizardAsync(
        Func<string, CancellationToken, Task> speak,
        Func<int, CancellationToken, Task<VoiceClip>> record,
        Func<byte[], CancellationToken, Task<string?>> transcribe,
        Action<string> log,
        Action<EnrollPhase> onPhase,
        CalibrationPaths paths,
        CancellationToken ct,
        string? enrollDir = null)
    {
        string dir = enrollDir ?? EnrollDir;
        Directory.CreateDirectory(dir);
        // Laisse la consigne parlée s'éteindre (reverb + buffer audio).
        async Task<VoiceClip> RecordGapAsync(int seconds)
        {
            await Task.Delay(500, ct).ConfigureAwait(false);
            return await record(seconds, ct).ConfigureAwait(false);
        }
        onPhase(EnrollPhase.Ambient);
        await speak("Bonjour, je m'appelle Patate ! Pour mieux te connaître, " +
            "reste silencieux quelques secondes...", ct).ConfigureAwait(false);
        VoiceClip ambient = await RecordGapAsync(4).ConfigureAwait(false);
        string silencePath = Path.Combine(dir, "ambient.wav");
        File.WriteAllBytes(silencePath, ambient.Wav);
        if (ambient.HadSpeech)
        {
            log("Ambiance : du bruit pendant le silence (pic " +
                $"{ambient.PeakRms:F0}), le contrôle sera sévère.");
        }
        var takes = new List<string>();
        int attempts = 0;
        int rejects = 0;
        onPhase(EnrollPhase.Takes);
        while (takes.Count < MinTakes && attempts < MinTakes + 2)
        {
            attempts++;
            await speak(takes.Count == 0
                ? "Répète après moi : Hey, Patate !"
                : "Encore une fois, Hey, Patate !", ct).ConfigureAwait(false);
            VoiceClip take = await RecordGapAsync(8).ConfigureAwait(false);
            if (!take.HadSpeech)
            {
                log($"Prise {attempts} : rien entendu (pic {take.PeakRms:F0}).");
                await speak("Je n'ai rien entendu, on recommence...", ct).ConfigureAwait(false);
                continue;
            }
            string? heard;
            try
            {
                heard = await transcribe(take.Wav, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log($"Transcription impossible ({ex.Message}), prise acceptée quand même.");
                heard = "patate";
            }
            if (!IsCleanTake(heard))
            {
                rejects++;
                log($"Prise {attempts} floue (« {(heard ?? "").Trim()} »).");
                string tip = rejects >= 2 ? " Dis juste : Hey, Patate !" : "";
                await speak($"J'ai entendu « {ShortEcho(heard)} », on recommence...{tip}", ct)
                    .ConfigureAwait(false);
                continue;
            }
            rejects = 0;
            string path = Path.Combine(dir, $"take{takes.Count + 1}.wav");
            File.WriteAllBytes(path, take.Wav);
            takes.Add(path);
            log($"Prise {takes.Count} : {take.RecordedFor.TotalSeconds:F1} s, pic {take.PeakRms:F0}.");
        }
        if (takes.Count < MinTakes)
        {
            onPhase(EnrollPhase.DoneKo);
            await speak("J'ai du mal à t'entendre, je garde mes réglages de départ.", ct)
                .ConfigureAwait(false);
            return new CalibrationVerdict(false, null,
                WakeWordOptions.DefaultThreshold, WakeWordOptions.DefaultGateRms,
                "prises inutilisables");
        }
        onPhase(EnrollPhase.Thinking);
        await speak("Attends-moi, je réfléchis...", ct).ConfigureAwait(false);
        CalibrationVerdict verdict = await CalibrateAsync(
            takes, silencePath, paths.BankPath, log, ct).ConfigureAwait(false);
        while (!verdict.Ok && takes.Count < MaxTakes && attempts < MaxTakes + 2)
        {
            attempts++;
            await speak("Encore une fois, Hey, Patate !", ct).ConfigureAwait(false);
            VoiceClip take = await RecordGapAsync(8).ConfigureAwait(false);
            if (!take.HadSpeech)
            {
                continue;
            }
            try
            {
                string? extra = await transcribe(take.Wav, ct).ConfigureAwait(false);
                if (!IsCleanTake(extra))
                {
                    log($"Prise floue (« {(extra ?? "").Trim()} »).");
                    await speak($"J'ai entendu « {ShortEcho(extra)} », on recommence...", ct)
                        .ConfigureAwait(false);
                    continue;
                }
            }
            catch
            {
                // Transcription impossible : prise acceptée quand même.
            }
            string path = Path.Combine(dir, $"take{takes.Count + 1}.wav");
            File.WriteAllBytes(path, take.Wav);
            takes.Add(path);
            verdict = await CalibrateAsync(
                takes, silencePath, paths.BankPath, log, ct).ConfigureAwait(false);
        }
        if (verdict.Ok && verdict.WinnerLine is not null)
        {
            EnsureOverrideModelDir();
            WriteKeywordsNoBom(paths.OverrideKeywordsPath, new[] { verdict.WinnerLine });
            WakeWordOptions.SaveOverride(paths.OverrideOptionsPath,
                new WakeWordOptions(verdict.Threshold, Score, verdict.GateRms),
                takes.Count, verdict.WinnerLine);
            log($"Calibration : {verdict.Reason}, seuil={verdict.Threshold}, porte={verdict.GateRms}.");
            onPhase(EnrollPhase.DoneOk);
            await speak("C'est bon, je te reconnaîtrai !", ct).ConfigureAwait(false);
        }
        else
        {
            log($"Calibration impossible : {verdict.Reason}.");
            onPhase(EnrollPhase.DoneKo);
            await speak("J'ai du mal à te reconnaître, je garde mes réglages de départ.", ct)
                .ConfigureAwait(false);
        }
        return verdict;
    }
}
