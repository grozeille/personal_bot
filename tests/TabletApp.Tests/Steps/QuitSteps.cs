using Reqnroll;
using TabletApp.Tests.Support;
using TabletApp.UI;
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

    [Given("un dialogue de sortie")]
    public void GivenDialogue() =>
        Assert.False(_quit.Confirm.Active);

    [When("on appuie dans le coin haut-gauche")]
    public void WhenCoin() => _quit.Tap(10, 10);

    [When("on appuie au milieu de l'écran")]
    public void WhenMilieu() => _quit.Tap(400, 300);

    [When("on appuie sur l'œil gauche pour rester")]
    public void WhenOeilGauche() => _quit.Tap(250, 300);

    [When("on appuie sur l'œil droit pour quitter")]
    public void WhenOeilDroit() => _quit.Tap(550, 300);

    [When("on appuie sur la bouche")]
    public void WhenBouche() => _quit.Tap(400, 420);

    [Then("le visage de sortie est affiché")]
    public void ThenSortie() =>
        Assert.True(_quit.Confirm.Active && _quit.Current == RobotExpression.Confirm);

    [Then("la sortie reste affichée")]
    public void ThenReste() =>
        Assert.True(_quit.Confirm.Active && _quit.Current == RobotExpression.Confirm);

    [Then("aucune sortie n'est proposée")]
    public void ThenAucune() =>
        Assert.False(_quit.Confirm.Active);

    [Then("l'application reste ouverte")]
    public void ThenOuverte() =>
        Assert.False(_quit.Confirm.Active);

    [Then("la fermeture est demandée")]
    public void ThenDemandee() =>
        Assert.True(_quit.QuitRequested);

    [Then("la fermeture n'est pas demandée")]
    public void ThenPasDemandee() =>
        Assert.False(_quit.QuitRequested);
}
