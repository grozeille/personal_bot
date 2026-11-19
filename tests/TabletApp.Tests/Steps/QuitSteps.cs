using Reqnroll;
using TabletApp.Tests.Support;
using Xunit;

namespace TabletApp.Tests.Steps;

[Binding]
public sealed class QuitSteps
{
    private readonly SimulatedQuit _quit;

    public QuitSteps(ScenarioContext context)
    {
        if (!context.TryGetValue("quit", out SimulatedQuit? quit) || quit is null)
        {
            quit = new SimulatedQuit();
            context["quit"] = quit;
        }
        _quit = quit;
    }

    [Given("un geste de sortie")]
    public void GivenGeste() =>
        Assert.False(_quit.Gesture.QuitRequested);

    [When("on appuie dans le coin haut-gauche")]
    public void WhenCoin() => _quit.Down(10, 10);

    [When("on appuie au milieu de l'écran")]
    public void WhenMilieu() => _quit.Down(400, 300);

    [When("^(\\d+) secondes? s'écoule(?:nt)? en maintenant l'appui$")]
    public void WhenMaintien(int seconds) =>
        _quit.Hold(TimeSpan.FromSeconds(seconds));

    [When("on relâche l'appui")]
    public void WhenRelache() => _quit.Release();

    [Then("la fermeture est demandée")]
    public void ThenDemandee() =>
        Assert.True(_quit.Gesture.QuitRequested);

    [Then("la fermeture n'est pas demandée")]
    public void ThenPasDemandee() =>
        Assert.False(_quit.Gesture.QuitRequested);
}
