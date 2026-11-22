using Reqnroll;
using TabletApp.Tests.Support;
using Xunit;

namespace TabletApp.Tests.Steps;

[Binding]
public sealed class EnrollSteps
{
    private readonly SimulatedEnroll _enroll;
    private string? _overridePath;

    public EnrollSteps(ScenarioContext context)
    {
        if (!context.TryGetValue("enroll", out SimulatedEnroll? enroll) || enroll is null)
        {
            enroll = new SimulatedEnroll();
            context["enroll"] = enroll;
        }
        _enroll = enroll;
    }

    [Given("les scores suivants :")]
    public void GivenScores(Table table)
    {
        foreach (var row in table.Rows)
        {
            _enroll.AddScore(
                row["Ligne"],
                float.Parse(row["Seuil"], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(row["Porte"], System.Globalization.CultureInfo.InvariantCulture),
                row["Prises"],
                row["Silence"]);
        }
    }

    [When("on choisit le gagnant")]
    public void WhenChoisit() => _enroll.Pick(required: 3);

    [Then("le gagnant est \"(.*)\"")]
    public void ThenGagnant(string line) =>
        Assert.Equal(line, _enroll.Verdict?.WinnerLine);

    [Then("le seuil est \"(.*)\" et la porte \"(.*)\"")]
    public void ThenReglages(string threshold, string gate)
    {
        Assert.Equal(float.Parse(threshold, System.Globalization.CultureInfo.InvariantCulture),
            _enroll.Verdict?.Threshold);
        Assert.Equal(double.Parse(gate, System.Globalization.CultureInfo.InvariantCulture),
            _enroll.Verdict?.GateRms);
    }

    [When("on calibre sans aucune prise")]
    public Task WhenSansPrise() => _enroll.CalibrateEmptyAsync();

    [When("le wizard tourne avec des prises factices")]
    public Task WhenWizardFake() => _enroll.RunWizardFakeAsync();

    [Then("les phases sont \"(.*)\"")]
    public void ThenPhases(string expected) =>
        Assert.Equal(expected, string.Join(", ", _enroll.Phases));

    [When("on vérifie des transcriptions :")]
    public void WhenTranscriptions(Table table)
    {
        foreach (var row in table.Rows)
        {
            _enroll.AddTranscript(row["Texte"], row["Propre"]);
        }
    }

    [Then("les verdicts sont corrects")]
    public void ThenVerdicts() => Assert.True(_enroll.TranscriptsOk());

    [When("on découpe une prise avec un clic initial")]
    public void WhenDecoupe() => _enroll.TrimSynthetic();

    [Then("la prise découpée commence 0,7 s avant la parole")]
    public void ThenDecoupee()
    {
        Assert.Equal(20, _enroll.TrimmedFrames);
        Assert.Equal(7, _enroll.FirstLoudFrame);
    }

    [Then("la calibration échoue")]
    public void ThenEchec() =>
        Assert.False(_enroll.Verdict?.Ok);

    [Then("aucun fichier n'est écrit")]
    public void ThenRienEcrit() =>
        Assert.False(Directory.Exists(_enroll.WorkDir));

    [When("on écrit une calibration avec le seuil \"(.*)\"")]
    public void WhenEcritCalibration(string threshold) =>
        _overridePath = _enroll.WriteOverride(
            float.Parse(threshold, System.Globalization.CultureInfo.InvariantCulture));

    [Then("le seuil chargé est \"(.*)\"")]
    public void ThenSeuilCharge(string threshold) =>
        Assert.Equal(
            float.Parse(threshold, System.Globalization.CultureInfo.InvariantCulture),
            TabletApp.AI.WakeWordOptions.Load(
                Path.Combine(Path.GetTempPath(), "tabletapp-jamais-un-fichier.json"),
                _overridePath).Threshold);

    [Then("sans calibration le seuil est \"(.*)\"")]
    public void ThenSeuilDefaut(string threshold) =>
        Assert.Equal(
            float.Parse(threshold, System.Globalization.CultureInfo.InvariantCulture),
            TabletApp.AI.WakeWordOptions.Load(
                Path.Combine(Path.GetTempPath(), "tabletapp-jamais-un-fichier.json"),
                Path.Combine(Path.GetTempPath(), "tabletapp-jamais-une-calibration.json")).Threshold);
}
