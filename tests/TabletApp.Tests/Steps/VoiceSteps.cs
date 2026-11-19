using System.Diagnostics;
using Reqnroll;
using TabletApp.AI;
using TabletApp.UI;
using Xunit;

namespace TabletApp.Tests.Steps;

[Binding]
public sealed class VoiceSteps
{
    private readonly List<(RobotExpression Expr, double At)> _faces = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private string _said = "";
    private string? _heard;

    [Given("un test voix dont l'écoute entend \"(.*)\"")]
    public void GivenEcoute(string heard) => _heard = heard;

    [When("le robot dit bonjour et écoute")]
    public async Task WhenParleEtEcoute()
    {
        var test = new VoiceTest(
            (text, _) => { _said = text; return Task.CompletedTask; },
            (_, _) => Task.FromResult(_heard),
            expr => _faces.Add((expr, _clock.Elapsed.TotalSeconds)),
            listenFor: TimeSpan.FromMilliseconds(50),
            loveFor: TimeSpan.FromMilliseconds(100));
        await test.RunAsync(RobotExpression.Neutral, CancellationToken.None);
    }

    [Then("le robot a dit \"(.*)\"")]
    public void ThenADit(string expected) => Assert.Equal(expected, _said);

    [Then("le visage cœur a été affiché puis le visage neutre restauré")]
    public void ThenCoeur()
    {
        Assert.Equal(
            new[] { RobotExpression.Love, RobotExpression.Neutral },
            _faces.Select(f => f.Expr).ToArray());
        double loveFor = _faces[1].At - _faces[0].At;
        Assert.True(loveFor >= 0.08, $"Cœur affiché {loveFor:F2} s");
    }

    [Then("le visage cœur n'a pas été affiché")]
    public void ThenPasDeCoeur() =>
        Assert.DoesNotContain(_faces, f => f.Expr == RobotExpression.Love);
}
