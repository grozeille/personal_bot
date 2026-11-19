using Reqnroll;
using TabletApp.Tests.Support;
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
    public void WhenIcone() => _update.Down(790, 10);

    [When("on appuie loin de l'icône")]
    public void WhenLoin() => _update.Down(400, 300);

    [When("^(\\d+) secondes? s'écoule(?:nt)? en maintenant l'appui sur l'icône$")]
    public void WhenMaintienIcone(int seconds) =>
        _update.Hold(TimeSpan.FromSeconds(seconds));

    [When("on relâche l'icône")]
    public void WhenRelacheIcone() => _update.Release();

    [When("on lance l'installation de la mise à jour")]
    public Task WhenInstalle() => _update.InstallAsync();

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

    [Then("la mise à jour est demandée")]
    public void ThenDemandee() =>
        Assert.True(_update.Gesture.UpdateRequested);

    [Then("la mise à jour n'est pas demandée")]
    public void ThenPasDemandee() =>
        Assert.False(_update.Gesture.UpdateRequested);

    [Then("la version \"(.*)\" est installée")]
    public void ThenInstallee(string version) =>
        Assert.Equal(version, _update.CurrentVersion);

    [Then("la version courante est conservée")]
    public void ThenConservee() =>
        Assert.Equal("1.0.0", _update.CurrentVersion);
}
