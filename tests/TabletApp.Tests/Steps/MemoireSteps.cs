using Reqnroll;
using TabletApp.AI;
using Xunit;

namespace TabletApp.Tests.Steps;

[Binding]
public sealed class MemoireSteps
{
    private string _dir = "";
    private string _prompt = "";

    [Given("un dossier mémoire avec un vieux journal")]
    public void GivenVieuxJournal()
    {
        _dir = Directory.CreateTempSubdirectory("robot-memoire-").FullName;
        File.WriteAllText(Path.Combine(_dir, "journal-2000-01-01.md"), "vieux\n");
    }

    [When("on y consigne deux tours")]
    public void WhenDeuxTours()
    {
        var store = new MemoryStore(_dir);
        store.AppendHeard("bonjour");
        store.AppendRobot("coucou");
        store.AppendHeard("salut");
        store.AppendRobot("rebonjour");
    }

    [Then("le journal contient les tours")]
    public void ThenJournal()
    {
        string recent = new MemoryStore(_dir).ReadRecent();
        Assert.Contains("bonjour", recent);
        Assert.Contains("rebonjour", recent);
    }

    [Then("le vieux journal est élagué")]
    public void ThenElague() =>
        Assert.False(File.Exists(Path.Combine(_dir, "journal-2000-01-01.md")));

    [Given("un carnet avec des souvenirs")]
    public void GivenSouvenirs()
    {
        _dir = Directory.CreateTempSubdirectory("robot-memoire-").FullName;
        var store = new MemoryStore(_dir);
        store.SaveMemory("L'enfant s'appelle Lina.");
        store.AppendHeard("on rejoue ?");
    }

    [When("on construit le prompt avec la mémoire")]
    public void WhenPrompt()
    {
        _prompt = GeminiBrain.BuildLivePrompt("X", memory: new MemoryStore(_dir).BuildBlock());
    }

    [Then("le prompt contient les souvenirs")]
    public void ThenPrompt()
    {
        Assert.Contains("Lina", _prompt);
        Assert.Contains("on rejoue ?", _prompt);
    }

    [Given("un carnet avec un journal")]
    public void GivenJournal()
    {
        _dir = Directory.CreateTempSubdirectory("robot-memoire-").FullName;
        var store = new MemoryStore(_dir);
        store.AppendHeard("j'aime les dinosaures");
    }

    [When("on consolide avec un résumé factice")]
    public async Task WhenConsolide()
    {
        var store = new MemoryStore(_dir);
        string? updated = await MemoryStore.ConsolidateAsync(
            store,
            (mem, journal, ct) => Task.FromResult("RESUME:" + journal.Length),
            CancellationToken.None);
        Assert.NotNull(updated);
    }

    [Then("le carnet contient le résumé")]
    public void ThenCarnet() =>
        Assert.Contains("RESUME:", new MemoryStore(_dir).ReadMemory());

    [AfterScenario]
    public void Cleanup()
    {
        try
        {
            if (_dir.Length > 0 && Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }
        catch
        {
        }
        _dir = "";
        _prompt = "";
    }
}
