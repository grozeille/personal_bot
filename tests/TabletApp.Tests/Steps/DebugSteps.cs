using Reqnroll;
using TabletApp.AI;
using Xunit;

namespace TabletApp.Tests.Steps;

[Binding]
public sealed class DebugSteps
{
    private string _dir = "";
    private string _purgeDir = "";

    [Given("un dossier debug temporaire")]
    public void GivenTempDir()
    {
        _dir = Directory.CreateTempSubdirectory("robot-debug-").FullName;
    }

    [When("on enregistre deux tours debug")]
    public void WhenDeuxTours()
    {
        using var recorder = new TurnRecorder(_dir);
        for (int t = 0; t < 2; t++)
        {
            recorder.AppendMic(SquarePcm(160, 2000), 2000, speech: true, muted: false);
            recorder.AppendRobot(SquarePcm(320, 1000));
            recorder.NoteHeard("bonjour");
            recorder.NoteSaid("coucou");
            recorder.EndTurn(4, 2000);
        }
    }

    [Then("six fichiers de tours existent")]
    public void ThenSixFichiers() =>
        Assert.Equal(6, Directory.GetFiles(_dir, "turn-*").Length);

    [Then("le WAV micro se recharge avec les bons échantillons")]
    public void ThenWavRecharge()
    {
        string mic = Directory.GetFiles(_dir, "turn-*-mic.wav").Order().First();
        Assert.True(NAudioVoice.LoadMono16k(mic, out float[]? samples));
        Assert.NotNull(samples);
        Assert.Equal(160, samples!.Length);
        float peak = samples.Max(Math.Abs);
        Assert.InRange(peak, 2000 / 32768f - 0.001f, 2000 / 32768f + 0.001f);
    }

    [Then("le sidecar contient les transcriptions")]
    public void ThenSidecar()
    {
        string json = Directory.GetFiles(_dir, "turn-*.json").Order().First();
        string text = File.ReadAllText(json);
        Assert.Contains("bonjour", text);
        Assert.Contains("coucou", text);
    }

    [Given("un dossier debug avec des tours anciens et récents")]
    public void GivenAnciensRecents()
    {
        _purgeDir = Directory.CreateTempSubdirectory("robot-debug-purge-").FullName;
        WriteGroup("turn-old-001", DateTime.Now.AddDays(-3));
        WriteGroup("turn-fresh-A002", DateTime.Now.AddMinutes(-30));
        WriteGroup("turn-fresh-B003", DateTime.Now);
    }

    [When("un enregistreur à purge stricte démarre")]
    public void WhenPurgeStricte()
    {
        // Âge : 1 h (les vieux partent). Taille : de quoi garder un seul
        // tour frais (le plus ancien des frais part aussi).
        using var recorder = new TurnRecorder(_purgeDir, TimeSpan.FromHours(1), 2200);
    }

    [Then("les vieux tours sont purgés")]
    public void ThenVieuxPurges() =>
        Assert.Empty(Directory.GetFiles(_purgeDir, "turn-old-*"));

    [Then("les tours récents sont conservés")]
    public void ThenRecentsConserves() =>
        Assert.Equal(
            new[] { "turn-fresh-B003-mic.wav", "turn-fresh-B003-robot.wav", "turn-fresh-B003.json" },
            Directory.GetFiles(_purgeDir, "turn-*").Select(Path.GetFileName).Order().ToArray());

    [AfterScenario]
    public void Cleanup()
    {
        foreach (string dir in new[] { _dir, _purgeDir })
        {
            try
            {
                if (dir.Length > 0 && Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch
            {
            }
        }
        _dir = "";
        _purgeDir = "";
    }

    private void WriteGroup(string stem, DateTime time)
    {
        string mic = Path.Combine(_purgeDir, stem + "-mic.wav");
        string robot = Path.Combine(_purgeDir, stem + "-robot.wav");
        string json = Path.Combine(_purgeDir, stem + ".json");
        File.WriteAllBytes(mic, new byte[1000]);
        File.WriteAllBytes(robot, new byte[1000]);
        File.WriteAllText(json, "{}");
        File.SetLastWriteTime(mic, time);
        File.SetLastWriteTime(robot, time);
        File.SetLastWriteTime(json, time);
    }

    private static byte[] SquarePcm(int samples, short amplitude)
    {
        byte[] pcm = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            short s = i % 2 == 0 ? amplitude : (short)-amplitude;
            pcm[i * 2] = (byte)(s & 0xFF);
            pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return pcm;
    }
}
