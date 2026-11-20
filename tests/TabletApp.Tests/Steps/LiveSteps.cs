using Reqnroll;
using TabletApp.AI;
using TabletApp.Tests.Support;
using TabletApp.UI;
using Xunit;

namespace TabletApp.Tests.Steps;

[Binding]
public sealed class LiveSteps
{
    private readonly FakeLiveSocket _socket = new();
    private readonly FakeLiveMic _mic = new();
    private readonly FakeLivePlayer _player = new();
    private readonly List<string> _emotions = new();
    private readonly List<string> _heard = new();
    private readonly List<string> _said = new();
    private readonly List<bool> _speaking = new();
    private bool _cancelAfterInterrupt;
    private bool _finished;
    private List<string> _sentWhileSpeaking = new();
    private List<string> _sentAfterSilence = new();
    private List<string> _sentAfterSpeech = new();

    [Given("un serveur live qui salue, joue de l'audio et demande happy")]
    public void GivenSalut()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"serverContent":{"modelTurn":{"parts":[{"inlineData":{"mimeType":"audio/pcm;rate=24000","data":"QUI="}}]}}}""",
            """{"toolCall":{"functionCalls":[{"id":"c1","name":"set_emotion","args":{"emotion":"happy"}}]}}""",
            """{"serverContent":{"inputTranscription":{"text":"salut"}}}""",
            """{"serverContent":{"outputTranscription":{"text":"coucou"},"turnComplete":true}}""");
        _cancelAfterInterrupt = false;
    }

    [Given("un serveur live qui interrompt")]
    public void GivenInterrompt()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"serverContent":{"modelTurn":{"parts":[{"inlineData":{"mimeType":"audio/pcm;rate=24000","data":"QUI="}}]}}}""",
            """{"serverContent":{"interrupted":true}}""");
        _cancelAfterInterrupt = true;
    }

    [Given("un serveur live injoignable")]
    public void GivenInjoignable() => _socket.FailConnect = true;

    [Given("un serveur live qui parle puis termine son tour")]
    public void GivenParlePuisTermine()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"serverContent":{"modelTurn":{"parts":[{"inlineData":{"mimeType":"audio/pcm;rate=24000","data":"QUI="}}]}}}""");
    }

    [When("la session live se déroule")]
    public async Task WhenSession()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" },
            _socket, _mic, _player,
            new LiveCallbacks(
                OnEmotion: emotion => _emotions.Add(emotion),
                OnHeardTranscript: text => _heard.Add(text),
                OnRobotTranscript: text => _said.Add(text),
                OnSpeaking: speaking => _speaking.Add(speaking),
                OnGlow: _ => { },
                OnUserSpeech: () => { },
                OnLog: _ => { }),
            idleTimeout: TimeSpan.FromMilliseconds(300),
            turnTimeout: TimeSpan.FromMilliseconds(150));
        Task run = session.RunAsync(cts.Token);
        byte[] loud = LoudChunk();
        byte[] silence = new byte[320];
        DateTime end = DateTime.UtcNow.AddSeconds(8);
        int pushes = 0;
        while (!run.IsCompleted && DateTime.UtcNow < end)
        {
            // Parole forte d'abord (le robot a fini : sourdine levée),
            // puis silence pour déclencher la fin de phrase côté client.
            _mic.Push(pushes < 10 ? loud : silence);
            pushes++;
            if (_cancelAfterInterrupt && _player.Clears > 0)
            {
                cts.Cancel();
            }
            await Task.Delay(50);
        }
        Assert.True(run.IsCompleted, "La session aurait dû se terminer.");
        await run;
        _finished = true;
    }

    [When("la session live démarre")]
    public async Task WhenDemarre()
    {
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" },
            _socket, _mic, _player,
            new LiveCallbacks(_ => { }, _ => { }, _ => { }, _ => { }, _ => { }, () => { }, _ => { }));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.RunAsync(CancellationToken.None));
        _finished = true;
    }

    [Then("le setup live déclare le modèle, le prompt et set_emotion")]
    public void ThenSetup()
    {
        Assert.True(_finished);
        Assert.Contains(_socket.Sent, json =>
            json.Contains("models/gemini-3.8-live")
            && json.Contains("set_emotion") && json.Contains("SYS"));
    }

    [Then("le robot salue, joue l'audio et répond au tool")]
    public void ThenEchanges()
    {
        Assert.Contains(_socket.Sent, json => json.Contains("Je t'écoute")
            || json.Contains("arriver"));
        Assert.Equal(new byte[] { 0x41, 0x42 }, _player.Played.ToArray());
        Assert.Contains(_socket.Sent, json => json.Contains("\"c1\""));
        Assert.Equal(new[] { "salut" }, _heard.ToArray());
        Assert.Equal(new[] { "coucou" }, _said.ToArray());
    }

    [Then("l'émotion happy s'affiche puis la parole s'arrête")]
    public void ThenEmotion()
    {
        Assert.Equal(new[] { "happy" }, _emotions.ToArray());
        Assert.Contains(true, _speaking);
        Assert.False(_speaking[^1]);
    }

    [Then("la fin de phrase est signalée une fois")]
    public void ThenFinDePhrase() =>
        Assert.Single(_socket.Sent, json => json.Contains("\"turnComplete\":true")
            && !json.Contains("\"turns\""));

    [Then("l'interruption vide le haut-parleur")]
    public void ThenInterruption()
    {
        Assert.True(_player.Clears >= 1);
        Assert.False(_speaking[^1]);
    }

    [Then("rien n'est envoyé")]
    public void ThenRien()
    {
        Assert.Empty(_socket.Sent);
        Assert.False(_socket.Connected);
    }

    [When("la session live tourne en semi-duplex")]
    public async Task WhenSemiduplex()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" },
            _socket, _mic, _player,
            new LiveCallbacks(
                OnEmotion: emotion => _emotions.Add(emotion),
                OnHeardTranscript: text => _heard.Add(text),
                OnRobotTranscript: text => _said.Add(text),
                OnSpeaking: speaking => _speaking.Add(speaking),
                OnGlow: _ => { },
                OnUserSpeech: () => { },
                OnLog: _ => { }),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromMilliseconds(150));
        Task run = session.RunAsync(cts.Token);
        byte[] loud = LoudChunk();
        byte[] silence = new byte[320];
        Assert.True(
            await WaitForAsync(() => _speaking.Contains(true), TimeSpan.FromSeconds(2)),
            "Le robot aurait dû parler.");
        int mark = _socket.Sent.Count;
        for (int i = 0; i < 5; i++)
        {
            _mic.Push(loud);
            await Task.Delay(50);
        }
        _sentWhileSpeaking = _socket.Sent.Skip(mark).ToList();
        _socket.Script("""{"serverContent":{"turnComplete":true}}""");
        Assert.True(
            await WaitForAsync(() => _speaking.Count > 0 && !_speaking[^1], TimeSpan.FromSeconds(2)),
            "Le tour robot aurait dû finir.");
        await Task.Delay(LiveSession.SpeakMuteTail + TimeSpan.FromMilliseconds(200));
        mark = _socket.Sent.Count;
        for (int i = 0; i < 4; i++)
        {
            _mic.Push(silence);
            await Task.Delay(50);
        }
        _sentAfterSilence = _socket.Sent.Skip(mark).ToList();
        mark = _socket.Sent.Count;
        _mic.Push(loud);
        DateTime end = DateTime.UtcNow.AddSeconds(5);
        while (!run.IsCompleted && DateTime.UtcNow < end
            && !_socket.Sent.Skip(mark).Any(IsBareTurnComplete))
        {
            _mic.Push(silence);
            await Task.Delay(50);
        }
        _sentAfterSpeech = _socket.Sent.Skip(mark).ToList();
        cts.Cancel();
        await run;
        _finished = true;
    }

    [Then("rien du micro n'est envoyé pendant la parole du robot")]
    public void ThenSourdPendantParole()
    {
        Assert.True(_finished);
        Assert.Empty(_sentWhileSpeaking);
    }

    [Then("l'écho n'arme pas la fin de phrase")]
    public void ThenEchoDesarme()
    {
        Assert.DoesNotContain(_sentAfterSilence, IsBareTurnComplete);
    }

    [Then("le micro repart après une vraie parole")]
    public void ThenMicroRepart()
    {
        Assert.Contains(_sentAfterSpeech, json => json.Contains("realtimeInput"));
        Assert.Single(_sentAfterSpeech, IsBareTurnComplete);
    }

    private static byte[] LoudChunk()
    {
        byte[] loud = new byte[320];
        for (int i = 0; i < loud.Length; i += 2)
        {
            loud[i] = 0xFF;
            loud[i + 1] = 0x7F;
        }
        return loud;
    }

    private static async Task<bool> WaitForAsync(Func<bool> ready, TimeSpan timeout)
    {
        DateTime end = DateTime.UtcNow + timeout;
        while (!ready())
        {
            if (DateTime.UtcNow >= end)
            {
                return false;
            }
            await Task.Delay(20);
        }
        return true;
    }

    private static bool IsBareTurnComplete(string json) =>
        json.Contains("\"turnComplete\":true") && !json.Contains("\"turns\"");
}
