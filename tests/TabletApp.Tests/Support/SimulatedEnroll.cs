using TabletApp.AI;

namespace TabletApp.Tests.Support;

/// <summary>
/// Enrôlement simulé : choix du gagnant sur table de scores, calibration
/// sans prise (échec), calibration locale (fichiers temporaires).
/// Hermétique (aucun modèle, aucun micro).
/// </summary>
internal sealed class SimulatedEnroll
{
    public List<(string Line, float Threshold, double Gate, bool[] TakeHits, bool SilenceHit)> Results { get; } = new();

    public CalibrationVerdict? Verdict { get; private set; }

    public string WorkDir { get; } = Path.Combine(
        Path.GetTempPath(), "tabletapp-enroll-test-" + Path.GetRandomFileName());

    public void AddScore(string line, float threshold, double gate, string takes, string silence)
    {
        Results.Add((line, threshold, gate,
            takes.Select(c => c == '1').ToArray(), silence == "1"));
    }

    public void Pick(int required) =>
        Verdict = WakeEnrollment.PickWinner(Results, required);

    public async Task CalibrateEmptyAsync()
    {
        Verdict = await WakeEnrollment.CalibrateAsync(
            Array.Empty<string>(), Path.Combine(WorkDir, "sil.wav"),
            Path.Combine(WorkDir, "bank.txt"), null, CancellationToken.None);
    }

    public string WriteOverride(float threshold)
    {
        Directory.CreateDirectory(WorkDir);
        string path = Path.Combine(WorkDir, "wake-calibration.json");
        WakeWordOptions.SaveOverride(path,
            new WakeWordOptions(threshold, 1.0f, 150.0), 3, "B AH1 T @TEST");
        return path;
    }

    public float[] TrimmedTake { get; private set; } = Array.Empty<float>();

    public void TrimSynthetic()
    {
        // Clic trames 0-1, silence 2-12, parole 13-20, silence 21-25.
        float[] samples = new float[26 * 1600];
        void Loud(int from, int to)
        {
            for (int f = from; f <= to; f++)
            {
                for (int i = 0; i < 1600; i++)
                {
                    samples[f * 1600 + i] = 0.5f * (float)Math.Sin(i * 0.1);
                }
            }
        }
        Loud(0, 1);
        Loud(13, 20);
        TrimmedTake = WakeEnrollment.TrimToLastEpisode(samples, 150.0, null);
    }

    public int TrimmedFrames => TrimmedTake.Length / 1600;

    public int FirstLoudFrame
    {
        get
        {
            for (int f = 0; f < TrimmedFrames; f++)
            {
                double sum = 0;
                for (int i = 0; i < 1600; i++)
                {
                    double s = TrimmedTake[f * 1600 + i];
                    sum += s * s;
                }
                if (Math.Sqrt(sum / 1600) * 32768 >= 150)
                {
                    return f;
                }
            }
            return -1;
        }
    }

    public List<(string Text, bool Expected)> Transcripts { get; } = new();

    public void AddTranscript(string text, string propre) =>
        Transcripts.Add((text, propre == "1"));

    public bool TranscriptsOk() =>
        Transcripts.All(t => WakeEnrollment.IsCleanTake(t.Text) == t.Expected);

    public bool PromptsMapped { get; private set; }

    public void ResolvePrompts() =>
        PromptsMapped = WakeEnrollment.Prompts.Length == 8
            && WakeEnrollment.Prompts.All(p => WakeEnrollment.PromptAssetName(p.Text) == p.Name + ".wav")
            && WakeEnrollment.PromptAssetName("J'ai entendu x, on recommence...") is null;

    public List<string> Phases { get; } = new();

    public async Task RunWizardFakeAsync()
    {
        Directory.CreateDirectory(WorkDir);
        Task Speak(string text, CancellationToken ct) => Task.CompletedTask;
        Task<VoiceClip> Record(int seconds, CancellationToken ct) =>
            Task.FromResult(new VoiceClip(new byte[44], true, 600, TimeSpan.FromSeconds(1)));
        Verdict = await WakeEnrollment.RunWizardAsync(
            Speak, Record, (_, _) => Task.FromResult<string?>("patate"),
            _ => { }, phase => Phases.Add(phase.ToString()),
            new CalibrationPaths(
                Path.Combine(WorkDir, "bank.txt"),
                Path.Combine(WorkDir, "keywords.txt"),
                Path.Combine(WorkDir, "wake-calibration.json")),
            CancellationToken.None, WorkDir);
    }
}
