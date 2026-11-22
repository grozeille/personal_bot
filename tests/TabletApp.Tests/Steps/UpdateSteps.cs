using Reqnroll;
using TabletApp.Tests.Support;
using TabletApp.UI;
using Xunit;

namespace TabletApp.Tests.Steps;

[Binding]
public sealed class UpdateSteps
{
    private readonly SimulatedUpdate _update;

    public UpdateSteps(ScenarioContext context)
    {
        if (!context.TryGetValue("update", out SimulatedUpdate? update) || update is null)
        {
            update = new SimulatedUpdate();
            context["update"] = update;
        }
        _update = update;
    }

    [Given("une source de maj proposant la version \"(.*)\"")]
    public void GivenVersionProposee(string version) =>
        _update.Source.AvailableVersion = version;

    [Given("une source de maj hors-ligne")]
    public void GivenHorsLigne() => _update.Source.Offline = true;

    [Given("une source de maj corrompue proposant la version \"(.*)\"")]
    public void GivenCorrompue(string version)
    {
        _update.Source.AvailableVersion = version;
        _update.Source.Corrupt = true;
    }

    [When("le robot vérifie les mises à jour")]
    public Task WhenVerifie() => _update.Updater.CheckAsync();

    [When("on appuie sur l'icône de mise à jour")]
    public void WhenIcone() => _update.Tap(790, 10);

    [When("on appuie sur l'œil gauche")]
    public void WhenOeilGauche() => _update.Tap(250, 300);

    [When("on appuie sur l'œil droit")]
    public void WhenOeilDroit() => _update.Tap(550, 300);

    [When("on appuie loin des yeux")]
    public void WhenLoin() => _update.Tap(400, 550);

    [Then("une mise à jour est proposée")]
    public void ThenProposee() =>
        Assert.True(_update.Updater.UpdateAvailable);

    [Then("aucune mise à jour n'est proposée")]
    public void ThenPasProposee() =>
        Assert.False(_update.Updater.UpdateAvailable);

    [Then("l'icône de mise à jour est visible")]
    public void ThenIconeVisible() =>
        Assert.True(SimulatedUpdate.IconVisibleOffscreen(updateAvailable: true));

    [Then("l'icône de mise à jour est masquée")]
    public void ThenIconeMasquee() =>
        Assert.False(SimulatedUpdate.IconVisibleOffscreen(updateAvailable: false));

    [Then("le visage de confirmation est affiché")]
    public void ThenConfirmation() =>
        Assert.True(_update.Confirm.Active && _update.Current == RobotExpression.Confirm);

    [Then("le regard est figé")]
    public void ThenRegardFige() =>
        Assert.False(FacePainter.LooksAround(RobotExpression.Confirm));

    [Then("la confirmation est refermée")]
    public void ThenRefermee() =>
        Assert.False(_update.Confirm.Active);

    [Then("la confirmation est toujours affichée")]
    public void ThenToujours() =>
        Assert.True(_update.Confirm.Active && _update.Current == RobotExpression.Confirm);

    [Then("aucune confirmation n'est affichée")]
    public void ThenAucune() =>
        Assert.False(_update.Confirm.Active);

    [Then("le visage précédent est restauré")]
    public void ThenRestaure() =>
        Assert.Equal(RobotExpression.Neutral, _update.Current);

    [Then("la version \"(.*)\" est installée")]
    public void ThenInstallee(string version) =>
        Assert.Equal(version, _update.CurrentVersion);

    [Then("la version courante est conservée")]
    public void ThenConservee() =>
        Assert.Equal("1.0.0", _update.CurrentVersion);

    [Then("des points d'attente remplacent le visage")]
    public void ThenPointsAttente() =>
        Assert.True(SimulatedUpdate.BusyDotsVisibleOffscreen(timeSeconds: 0.1));

    [Then("les points d'attente sont animés")]
    public void ThenPointsAnimes()
    {
        Assert.True(SimulatedUpdate.BusyDotPixels(0.1) < SimulatedUpdate.BusyDotPixels(0.9));
        Assert.Equal(1, FacePainter.BusyDotCount(0.1));
        Assert.Equal(2, FacePainter.BusyDotCount(0.5));
        Assert.Equal(3, FacePainter.BusyDotCount(0.9));
        Assert.Equal(1, FacePainter.BusyDotCount(1.3));
    }
}
