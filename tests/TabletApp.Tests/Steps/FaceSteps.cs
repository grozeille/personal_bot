using System.Globalization;
using Reqnroll;
using TabletApp.Tests.Support;
using TabletApp.UI;
using Xunit;

namespace TabletApp.Tests.Steps;

[Binding]
public sealed class FaceSteps
{
    private readonly SimulatedFace _face;
    private readonly ScenarioContext _context;

    public FaceSteps(ScenarioContext context)
    {
        if (!context.TryGetValue("face", out SimulatedFace? face) || face is null)
        {
            face = new SimulatedFace();
            context["face"] = face;
        }
        _face = face;
        _context = context;
    }

    [Given("un visage éveillé")]
    public void GivenVisageEveille()
    {
        // L'état initial du visage simulé est éveillé.
        Assert.True(_face.Sleep.IsAwake);
    }

    [When("^(\\d+) secondes? s'écoulent$")]
    public void WhenSecondesSEcoulent(int seconds) =>
        _face.Advance(TimeSpan.FromSeconds(seconds));

    [When("^(\\d+) minutes? s'écoulent$")]
    public void WhenMinutesSEcoulent(int minutes) =>
        _face.Advance(TimeSpan.FromMinutes(minutes));

    [When("le robot s'endort")]
    public void WhenRobotSEndort() =>
        _face.Sleep.FallAsleep(_face.Now);

    [When("le robot se réveille")]
    public void WhenRobotSeReveille() =>
        _face.Sleep.WakeUp(_face.Now);

    [Then("le robot a cligné entre (\\d+) et (\\d+) fois")]
    public void ThenCligneEntre(int min, int max) =>
        Assert.InRange(_face.BlinkStarts.Count, min, max);

    [Then("tous les intervalles de clignement sont entre (.*) et (.*) secondes")]
    public void ThenIntervalles(string minText, string maxText)
    {
        double min = ParseFr(minText);
        double max = ParseFr(maxText);
        var gaps = _face.BlinkStarts
            .Zip(_face.BlinkStarts.Skip(1), (a, b) => b - a)
            .Where(g => g > 1) // hors doubles-clignements (~0,3 s)
            .ToList();
        Assert.NotEmpty(gaps);
        Assert.All(gaps, g => Assert.InRange(g, min, max));
    }

    [Then("les yeux se ferment complètement")]
    public void ThenFermetureComplete() =>
        Assert.True(_face.MinBlinkOpen < 0.05, $"Ouverture min {_face.MinBlinkOpen:F2}");

    [Then("le regard a fait au moins (\\d+) saccades")]
    public void ThenSaccades(int min) =>
        Assert.True(_face.Saccades >= min, $"Saccades : {_face.Saccades}");

    [Then("le regard reste entre la gauche et la droite extrêmes")]
    public void ThenBornes() =>
        Assert.True(_face.GazeBounded, $"X dans [{_face.MinGazeX:F2}, {_face.MaxGazeX:F2}]");

    [Then("le regard a visité la gauche et la droite")]
    public void ThenExtremes() =>
        Assert.True(
            _face.MinGazeX < -0.9 && _face.MaxGazeX > 0.9,
            $"X dans [{_face.MinGazeX:F2}, {_face.MaxGazeX:F2}]");

    [Then("le robot dort paisiblement")]
    public void ThenDortPaisiblement()
    {
        Assert.Equal(SleepController.State.Asleep, _face.Sleep.Current);
        Assert.Equal(1.0, _face.Sleep.EyeOpen);
    }

    [Then("les paupières ont battu avant l'ouverture complète")]
    public void ThenBattements() =>
        Assert.True(
            _face.WakeMinOpen < 0.3 && _face.WakeMaxOpen > 0.6,
            $"Réveil [{_face.WakeMinOpen:F2}-{_face.WakeMaxOpen:F2}]");

    [Then("les yeux sont ouverts")]
    public void ThenOuverts()
    {
        Assert.Equal(SleepController.State.Awake, _face.Sleep.Current);
        Assert.Equal(1.0, _face.Sleep.EyeOpen);
    }

    [When("^on affiche l'expression \"(.*)\"$")]
    public void WhenExpression(string emotion)
    {
        _face.Expression.SetEmotion(emotion, _face.Now);
        _context["emotion"] = emotion;
    }

    [Then("^l'expression affichée est \"(.*)\"$")]
    public void ThenExpression(string expected) =>
        Assert.Equal(
            expected.ToLowerInvariant(),
            _face.Expression.Current.ToString().ToLowerInvariant());

    [Then("^la transition est terminée$")]
    public void ThenTransitionTerminee() =>
        Assert.False(_face.Expression.Transitioning);

    private static double ParseFr(string text) =>
        double.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);
}
