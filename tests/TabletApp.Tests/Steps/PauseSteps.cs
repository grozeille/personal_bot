using Reqnroll;
using TabletApp;
using TabletApp.AI;
using TabletApp.Tests.Support;
using Xunit;

namespace TabletApp.Tests.Steps;

[Binding]
public sealed class PauseSteps
{
    private readonly PauseTracker _pause = new();
    private readonly FakeLiveSocket _socket = new();
    private bool _resume;
    private string _greeting = "";
    private bool _pausedByTool;

    [Given("une discussion en pause depuis (.*) minutes")]
    public void GivenPauseDepuis(int minutes) => _pause.Pause(DateTime.Now.AddMinutes(-minutes));

    [Given("aucune pause")]
    public void GivenAucune()
    {
    }

    [When("on redémarre une discussion")]
    public void WhenRedemarre() => _resume = _pause.TryResume(DateTime.Now);

    [Then("c'est une reprise")]
    public void ThenReprise() => Assert.True(_resume);

    [Then("c'est une discussion fraîche")]
    public void ThenFraiche() => Assert.False(_resume);

    [When("on lit la consigne de reprise")]
    public void WhenConsigne() => _greeting = LiveSession.ResumePrompt;

    [Then("elle dit Oui sans bonjour")]
    public void ThenOui()
    {
        Assert.Contains("Oui ?", _greeting);
        Assert.DoesNotContain("Bonjour", _greeting);
    }

    [Given("un serveur live qui demande une pause")]
    public void GivenServeurPause()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"cz","name":"pause_conversation"}]}}""");
    }

    [When("la session live tourne jusqu'à la pause")]
    public async Task WhenSessionPause()
    {
        using var mic = new FakeLiveMic();
        using var player = new FakeLivePlayer();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" }, Array.Empty<string>(), Array.Empty<string>(),
            _socket, mic, player,
            new LiveCallbacks(
                OnEmotion: _ => { },
                OnSound: _ => { },
                OnGesture: _ => { },
                OnHeardTranscript: _ => { },
                OnRobotTranscript: _ => { },
                OnSpeaking: _ => { },
                OnGlow: _ => { },
                OnUserSpeech: () => { },
                OnLog: _ => { }),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await session.RunAsync(cts.Token);
        _pausedByTool = session.PausedByTool;
    }

    [Then("la session est marquée en pause")]
    public void ThenMarquee()
    {
        Assert.True(_pausedByTool);
        Assert.Contains(_socket.Sent, json => json.Contains("\"cz\""));
    }
}
