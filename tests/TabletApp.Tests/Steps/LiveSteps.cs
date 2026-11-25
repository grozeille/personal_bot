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
    private readonly List<string> _sounds = new();
    private readonly List<bool> _speaking = new();
    private readonly List<string> _logs = new();
    private readonly List<VoiceGlow> _glows = new();
    private readonly List<string> _gestures = new();
    private bool _calibrationRequested;
    private bool _heardAfterInterrupt;
    private bool _forcedTurn;
    private int _lastTurnChunks = -1;
    private bool _turnWhileBuffered;
    private bool _turnAfterDrain;
    private bool _sendFailed;
    private readonly List<(double? Yaw, double? Pitch, double? Roll)> _moved = new();
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

    [Given("un serveur live qui envoie un tour vide")]
    public void GivenTourVide()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"serverContent":{"modelTurn":{"parts":[{"inlineData":{"mimeType":"audio/pcm;rate=24000","data":"QUI="}}]}}}""",
            """{"serverContent":{"outputTranscription":{"text":"<no speech>{pause}"},"turnComplete":true}}""");
        _cancelAfterInterrupt = false;
    }

    [Given("un serveur live qui cherche sur Wikipédia")]
    public void GivenChercheWiki()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"cw","name":"search_wikipedia","args":{"query":"pingouin"}}]}}""");
        _cancelAfterInterrupt = false;
    }

    [Given("un serveur live qui charge une skill")]
    public void GivenChargeSkill()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"ck","name":"load_skill","args":{"query":"devinette-animaux"}}]}}""");
        _cancelAfterInterrupt = false;
    }

    [Given("un serveur live qui cherche un Pokémon")]
    public void GivenCherchePokemon()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"cd","name":"search_pokemon","args":{"query":"Poupsacha"}}]}}""");
        _cancelAfterInterrupt = false;
    }

    [Given("un serveur live qui tire un Pokémon")]
    public void GivenTirePokemon()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"ct","name":"draw_pokemon"}]}}""");
        _cancelAfterInterrupt = false;
    }

    [Given("un serveur live qui joue un son")]
    public void GivenJoueUnSon()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"c9","name":"play_sound","args":{"sound":"giggle"}}]}}""");
    }

    [Given("un serveur live qui fait un prout puis parle")]
    public void GivenProut()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"c7","name":"play_fart"}]}}""",
            """{"serverContent":{"modelTurn":{"parts":[{"inlineData":{"mimeType":"audio/pcm;rate=24000","data":"QUI="}}]}}}""",
            """{"serverContent":{"turnComplete":true}}""");
    }

    [Given("un serveur live silencieux")]
    public void GivenSilencieux()
    {
        _socket.Script("""{"setupComplete":{}}""");
    }

    [Given("un serveur live qui parle sans finir son tour")]
    public void GivenParleSansFin()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"serverContent":{"modelTurn":{"parts":[{"inlineData":{"mimeType":"audio/pcm;rate=24000","data":"QUI="}}]}}}""");
    }

    [Given("un serveur live qui termine la discussion")]
    public void GivenTermine()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"c5","name":"end_conversation"}]}}""");
    }

    [Given("un serveur live qui demande une calibration")]
    public void GivenCalibre()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"c6","name":"run_calibration"}]}}""");
    }

    [Given("un serveur live qui tourne la tête")]
    public void GivenTourneTete()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"c8","name":"move_head","args":{"yaw":-30}}]}}""");
    }

    [Given("un serveur live qui répète un appel d'outil")]
    public void GivenRepeteTool()
    {
        _socket.Script(
            """{"setupComplete":{}}""",
            """{"toolCall":{"functionCalls":[{"id":"c9","name":"play_gesture","args":{"gesture":"nod_yes"}}]}}""",
            """{"toolCall":{"functionCalls":[{"id":"c9","name":"play_gesture","args":{"gesture":"nod_yes"}}]}}""");
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
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(
                OnEmotion: emotion => _emotions.Add(emotion),
                OnSound: sound =>
                {
                    _sounds.Add(sound);
                    byte[]? pcm = RobotSounds.Render(sound);
                    if (pcm is not null)
                    {
                        _player.PlayRaw(pcm);
                    }
                },
                OnHeardTranscript: text => _heard.Add(text),
                OnRobotTranscript: text => _said.Add(text),
                OnSpeaking: speaking => _speaking.Add(speaking),
                OnGlow: _ => { },
                OnGesture: gesture => _gestures.Add(gesture),
                OnUserSpeech: () => { },
                OnLog: msg => _logs.Add(msg),
                OnCalibrationRequested: () => _calibrationRequested = true,
                OnMoveHead: (yaw, pitch, roll) => _moved.Add((yaw, pitch, roll)),
                OnSearchWikipedia: (q, ct) => Task.FromResult("RESUME:" + q),
                OnLoadSkill: slug => "REGLES:" + slug,
                OnSearchPokemon: q => "FICHE:" + q,
                OnDrawPokemon: () => "TIRAGE:mystère"),
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
            // 14 envois : 6 passent la queue, seuil parole (4) atteint.
            _mic.Push(pushes < 14 ? loud : silence);
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
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(_ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, () => { }, _ => { }));
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
            && json.Contains("set_emotion") && json.Contains("SYS")
            && json.Contains("fr-FR") && json.Contains("search_wikipedia")
            && json.Contains("load_skill") && json.Contains("search_pokemon")
            && json.Contains("draw_pokemon"));
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

    [Then("le tour vide est ignoré sans transcript robot")]
    public void ThenTourVideIgnore()
    {
        Assert.True(_player.Clears >= 1);
        Assert.Empty(_said);
        Assert.Contains(_logs, msg => msg.Contains("tour vide ignoré"));
    }

    [Then("le résumé Wikipédia est renvoyé au modèle")]
    public void ThenWiki()
    {
        Assert.Contains(_socket.Sent, json => json.Contains("RESUME:pingouin") && json.Contains("\"cw\""));
    }

    [Then("les règles de la skill sont renvoyées au modèle")]
    public void ThenSkill()
    {
        Assert.Contains(_socket.Sent, json => json.Contains("REGLES:devinette-animaux") && json.Contains("\"ck\""));
    }

    [Then("la fiche Pokémon est renvoyée au modèle")]
    public void ThenFichePokemon()
    {
        Assert.Contains(_socket.Sent, json => json.Contains("FICHE:Poupsacha") && json.Contains("\"cd\""));
    }

    [Then("un Pokémon mystère est renvoyé au modèle")]
    public void ThenTiragePokemon()
    {
        Assert.Contains(_socket.Sent, json => json.Contains("TIRAGE") && json.Contains("\"ct\""));
    }

    [Then("le son giggle est joué et le tool est acquitté")]
    public void ThenSon()
    {
        Assert.Equal(new[] { "giggle" }, _sounds.ToArray());
        Assert.Contains(_socket.Sent, json => json.Contains("\"c9\""));
        Assert.True(_player.PlayedRaw.Count > 0);
    }

    [When("le micro capte un bruit bref puis le silence")]
    public async Task WhenBruitBref()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(
                OnEmotion: emotion => _emotions.Add(emotion),
                OnSound: sound => _sounds.Add(sound),
                OnHeardTranscript: text => _heard.Add(text),
                OnRobotTranscript: text => _said.Add(text),
                OnSpeaking: speaking => _speaking.Add(speaking),
                OnGlow: _ => { },
                OnGesture: _ => { },
                OnUserSpeech: () => { },
                OnLog: _ => { }),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromMilliseconds(150));
        Task run = session.RunAsync(cts.Token);
        byte[] loud = LoudChunk();
        _mic.Push(loud);
        _mic.Push(loud);
        for (int i = 0; i < 10; i++)
        {
            _mic.Push(new byte[320]);
            await Task.Delay(50);
        }
        cts.Cancel();
        await run;
    }

    [Then("le prout est joué et la voix du robot est coupée")]
    public void ThenProut()
    {
        Assert.Equal(new[] { "prout" }, _sounds.ToArray());
        Assert.Empty(_player.Played);
        Assert.Contains(_socket.Sent, json => json.Contains("\"c7\""));
    }

    [Then("la discussion se termine après réponse au tool")]
    public void ThenDiscussionTerminee()
    {
        Assert.True(_finished);
        Assert.Contains(_socket.Sent, json => json.Contains("\"c5\""));
        Assert.Contains(_logs, log => log.Contains("fin de discussion demandée"));
        Assert.DoesNotContain(_logs, log => log.Contains("silence prolongé"));
    }

    [Then("la calibration est demandée puis la session se termine")]
    public void ThenCalibrationDemandee()
    {
        Assert.True(_finished);
        Assert.Contains(_socket.Sent, json => json.Contains("\"c6\""));
        Assert.True(_calibrationRequested);
        Assert.Contains(_logs, log => log.Contains("calibration demandée"));
    }

    [Then("la tête tourne et la session continue")]
    public void ThenTeteTourne()
    {
        Assert.True(_finished);
        Assert.Single(_moved);
        Assert.Equal(-30.0, _moved[0].Yaw);
        Assert.Null(_moved[0].Pitch);
        Assert.Null(_moved[0].Roll);
        int ack = _socket.Sent.FindIndex(json => json.Contains("\"c8\""));
        Assert.True(ack >= 0 && _socket.Sent.Skip(ack + 1).Any(json => json.Contains("realtimeInput")));
    }

    [Then("l'appel dupliqué n'agit qu'une fois")]
    public void ThenDedupTool()
    {
        Assert.True(_finished);
        Assert.Equal(new[] { "nod_yes" }, _gestures.ToArray());
        Assert.Contains(_socket.Sent, json => json.Contains("\"c9\""));
    }

    [When("le micro capte du souffle puis une voix faible")]
    public async Task WhenSouffle()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(_ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, () => { }, _ => { }),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromSeconds(30));
        Task run = session.RunAsync(cts.Token);
        Assert.True(
            await WaitForAsync(() => _socket.Sent.Any(json => json.Contains("\"turns\"")), TimeSpan.FromSeconds(2)),
            "La salutation aurait dû partir.");
        await Task.Delay(100);
        _mic.Push(new byte[320]);
        byte[] faible = new byte[320];
        for (int i = 0; i < 160; i++)
        {
            faible[i * 2] = 200;
        }
        _mic.Push(faible);
        cts.Cancel();
        await run;
        _finished = true;
    }

    [Then("le souffle part en zéros et la voix faible intacte")]
    public void ThenPlancher()
    {
        Assert.True(_finished);
        List<string> audios = _socket.Sent.Where(json => json.Contains("realtimeInput")).ToList();
        Assert.Equal(2, audios.Count);
        Assert.True(IsSilenceAudio(audios[0]));
        Assert.False(IsSilenceAudio(audios[1]));
    }

    [When("le micro capte des claquements forts")]
    public async Task WhenClaquements()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(_ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, () => { }, _ => { }),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromMilliseconds(150));
        Task run = session.RunAsync(cts.Token);
        Assert.True(
            await WaitForAsync(() => _socket.Sent.Any(json => json.Contains("\"turns\"")), TimeSpan.FromSeconds(2)),
            "La salutation aurait dû partir.");
        await Task.Delay(100);
        byte[] bang = BangChunk();
        byte[] silence = new byte[320];
        for (int i = 0; i < 6; i++)
        {
            _mic.Push(bang);
        }
        for (int i = 0; i < 10; i++)
        {
            _mic.Push(silence);
            await Task.Delay(50);
        }
        cts.Cancel();
        await run;
        _finished = true;
    }

    [Then("les bangs forts ne déclenchent pas de tour")]
    public void ThenBangsIgnores()
    {
        Assert.True(_finished);
        Assert.DoesNotContain(_socket.Sent, IsBareTurnComplete);
    }

    [When("la réponse est coupée par un tap")]
    public async Task WhenInterrompt()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(
                OnEmotion: emotion => _emotions.Add(emotion),
                OnSound: sound => _sounds.Add(sound),
                OnHeardTranscript: text => _heard.Add(text),
                OnRobotTranscript: text => _said.Add(text),
                OnSpeaking: speaking => _speaking.Add(speaking),
                OnGlow: glow => _glows.Add(glow),
                OnGesture: _ => { },
                OnUserSpeech: () => { },
                OnLog: msg => _logs.Add(msg)),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromMilliseconds(150));
        Task run = session.RunAsync(cts.Token);
        Assert.True(
            await WaitForAsync(() => _player.Played.Count > 0, TimeSpan.FromSeconds(2)),
            "Le robot aurait dû parler.");
        session.InterruptToListening();
        _socket.Script(
            """{"serverContent":{"modelTurn":{"parts":[{"inlineData":{"mimeType":"audio/pcm;rate=24000","data":"QUI="}}]}}}""",
            """{"serverContent":{"outputTranscription":{"text":"bla"},"turnComplete":true}}""");
        await Task.Delay(300);
        await Task.Delay(LiveSession.SpeakMuteTail + TimeSpan.FromMilliseconds(200));
        byte[] loud = LoudChunk();
        byte[] silence = new byte[320];
        int mark = _socket.Sent.Count;
        for (int i = 0; i < 5; i++)
        {
            _mic.Push(loud);
        }
        DateTime end = DateTime.UtcNow.AddSeconds(5);
        while (!run.IsCompleted && DateTime.UtcNow < end
            && !_socket.Sent.Skip(mark).Any(IsBareTurnComplete))
        {
            _mic.Push(silence);
            await Task.Delay(50);
        }
        _heardAfterInterrupt = _socket.Sent.Skip(mark).Any(IsBareTurnComplete);
        cts.Cancel();
        await run;
        _finished = true;
    }

    [Then("la fin de réponse est ignorée et l'écoute reprend")]
    public void ThenReponseCoupee()
    {
        Assert.True(_finished);
        Assert.Equal(new byte[] { 0x41, 0x42 }, _player.Played.ToArray());
        Assert.Empty(_said);
        Assert.Contains(_glows, glow => glow == VoiceGlow.Listening);
        Assert.Equal(VoiceGlow.Listening, _glows[^2]);
        Assert.True(_heardAfterInterrupt);
    }

    [When("l'utilisateur parle sans s'arrêter")]
    public async Task WhenParleSansArret()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(_ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, () => { }, _ => { }),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromSeconds(30),
            maxTurnTimeout: TimeSpan.FromMilliseconds(300));
        Task run = session.RunAsync(cts.Token);
        byte[] loud = LoudChunk();
        int mark = _socket.Sent.Count;
        DateTime end = DateTime.UtcNow.AddSeconds(5);
        while (!run.IsCompleted && DateTime.UtcNow < end
            && !_socket.Sent.Skip(mark).Any(IsBareTurnComplete))
        {
            _mic.Push(loud);
            await Task.Delay(50);
        }
        _forcedTurn = _socket.Sent.Skip(mark).Any(IsBareTurnComplete);
        cts.Cancel();
        await run;
        _finished = true;
    }

    [Then("une fin de phrase est forcée malgré la parole continue")]
    public void ThenTourForce()
    {
        Assert.True(_finished);
        Assert.True(_forcedTurn);
    }

    [When("deux tours utilisateur encadrent une réponse")]
    public async Task WhenDeuxTours()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(
                OnEmotion: _ => { },
                OnSound: _ => { },
                OnHeardTranscript: _ => { },
                OnRobotTranscript: _ => { },
                OnSpeaking: speaking => _speaking.Add(speaking),
                OnGlow: _ => { },
                OnGesture: _ => { },
                OnUserSpeech: () => { },
                OnLog: msg => _logs.Add(msg)),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromMilliseconds(150));
        Task run = session.RunAsync(cts.Token);
        byte[] loud = LoudChunk();
        byte[] silence = new byte[320];
        Assert.True(
            await WaitForAsync(() => _socket.Sent.Any(json => json.Contains("\"turns\"")), TimeSpan.FromSeconds(2)),
            "La salutation aurait dû partir.");
        await Task.Delay(100);
        for (int i = 0; i < 6; i++)
        {
            _mic.Push(loud);
        }
        _socket.Script(
            """{"serverContent":{"modelTurn":{"parts":[{"inlineData":{"mimeType":"audio/pcm;rate=24000","data":"QUI="}}]}}}""");
        Assert.True(
            await WaitForAsync(() => _speaking.Contains(true), TimeSpan.FromSeconds(2)),
            "Le robot aurait dû parler.");
        await Task.Delay(LiveSession.SpeakMuteTail + TimeSpan.FromMilliseconds(300));
        _socket.Script("""{"serverContent":{"turnComplete":true}}""");
        await Task.Delay(LiveSession.SpeakMuteTail + TimeSpan.FromMilliseconds(300));
        int mark = _socket.Sent.Count;
        for (int i = 0; i < 5; i++)
        {
            _mic.Push(loud);
        }
        DateTime end = DateTime.UtcNow.AddSeconds(5);
        while (!run.IsCompleted && DateTime.UtcNow < end
            && !_socket.Sent.Skip(mark).Any(IsBareTurnComplete))
        {
            _mic.Push(silence);
            await Task.Delay(50);
        }
        foreach (string log in _logs)
        {
            const string tag = "tour envoyé : ";
            int at = log.IndexOf(tag, StringComparison.Ordinal);
            if (at >= 0 && int.TryParse(
                new string(log.Skip(at + tag.Length).TakeWhile(char.IsDigit).ToArray()), out int n))
            {
                _lastTurnChunks = n;
            }
        }
        cts.Cancel();
        await run;
        _finished = true;
    }

    [Then("le compteur de tour repart de zéro après la réponse")]
    public void ThenCompteurTour()
    {
        Assert.True(_finished);
        Assert.InRange(_lastTurnChunks, 1, 11);
    }

    [When("le haut-parleur garde du retard puis se vide")]
    public async Task WhenRetardHautParleur()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(_ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, () => { }, _ => { }),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromMilliseconds(150));
        Task run = session.RunAsync(cts.Token);
        Assert.True(
            await WaitForAsync(() => _socket.Sent.Any(json => json.Contains("\"turns\"")), TimeSpan.FromSeconds(2)),
            "La salutation aurait dû partir.");
        await Task.Delay(100);
        byte[] loud = LoudChunk();
        byte[] silence = new byte[320];
        _player.Drained = false;
        for (int i = 0; i < 5; i++)
        {
            _mic.Push(loud);
        }
        for (int i = 0; i < 6; i++)
        {
            _mic.Push(silence);
            await Task.Delay(50);
        }
        _turnWhileBuffered = _socket.Sent.Any(IsBareTurnComplete);
        _player.Drained = true;
        int mark = _socket.Sent.Count;
        for (int i = 0; i < 5; i++)
        {
            _mic.Push(loud);
        }
        DateTime end = DateTime.UtcNow.AddSeconds(5);
        while (!run.IsCompleted && DateTime.UtcNow < end
            && !_socket.Sent.Skip(mark).Any(IsBareTurnComplete))
        {
            _mic.Push(silence);
            await Task.Delay(50);
        }
        _turnAfterDrain = _socket.Sent.Skip(mark).Any(IsBareTurnComplete);
        cts.Cancel();
        await run;
        _finished = true;
    }

    [Then("le micro reste sourd puis l'écoute reprend")]
    public void ThenHautParleurVide()
    {
        Assert.True(_finished);
        Assert.False(_turnWhileBuffered);
        Assert.True(_turnAfterDrain);
    }

    [When("le micro tourne pendant le blocage")]
    public async Task WhenBlocage()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(
                OnEmotion: _ => { },
                OnSound: _ => { },
                OnHeardTranscript: _ => { },
                OnRobotTranscript: _ => { },
                OnSpeaking: speaking => _speaking.Add(speaking),
                OnGlow: glow => _glows.Add(glow),
                OnGesture: _ => { },
                OnUserSpeech: () => { },
                OnLog: msg => _logs.Add(msg)),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromMilliseconds(150),
            watchdogTimeout: TimeSpan.FromMilliseconds(300));
        Task run = session.RunAsync(cts.Token);
        Assert.True(
            await WaitForAsync(() => _speaking.Contains(true), TimeSpan.FromSeconds(2)),
            "Le robot aurait dû parler.");
        byte[] silence = new byte[320];
        for (int i = 0; i < 12; i++)
        {
            _mic.Push(silence);
            await Task.Delay(50);
        }
        cts.Cancel();
        await run;
        _finished = true;
    }

    [Then("le micro est rouvert et ça se voit dans le journal")]
    public void ThenWatchdog()
    {
        Assert.True(_finished);
        Assert.Contains(_logs, log => log.Contains("réponse bloquée"));
        Assert.Contains(_glows, glow => glow == VoiceGlow.Listening);
        Assert.False(_speaking[^1]);
    }

    [When("les envois échouent")]
    public async Task WhenEnvoisEchouent()
    {
        using var cts = new CancellationTokenSource();
        var session = new LiveSession(
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(
                OnEmotion: _ => { },
                OnSound: _ => { },
                OnHeardTranscript: _ => { },
                OnRobotTranscript: _ => { },
                OnSpeaking: _ => { },
                OnGlow: _ => { },
                OnGesture: _ => { },
                OnUserSpeech: () => { },
                OnLog: msg => _logs.Add(msg)),
            idleTimeout: TimeSpan.FromSeconds(30),
            turnTimeout: TimeSpan.FromMilliseconds(150));
        Task run = session.RunAsync(cts.Token);
        Assert.True(
            await WaitForAsync(() => _socket.Sent.Any(json => json.Contains("\"turns\"")), TimeSpan.FromSeconds(2)),
            "La salutation aurait dû partir.");
        await Task.Delay(100);
        _socket.FailSend = true;
        byte[] silence = new byte[320];
        for (int i = 0; i < 8 && !run.IsCompleted; i++)
        {
            _mic.Push(silence);
            await Task.Delay(50);
        }
        await run.WaitAsync(TimeSpan.FromSeconds(5));
        _sendFailed = session.SendFailed;
        _finished = true;
    }

    [Then("la session s'arrête en demandant le repli unaire")]
    public void ThenRepliUnaire()
    {
        Assert.True(_finished);
        Assert.True(_sendFailed);
        Assert.Contains(_logs, log => log.Contains("envois en échec répétés"));
    }

    [Then("aucune fin de phrase n'est envoyée pour un bruit")]
    public void ThenPasDeTour()
    {
        Assert.DoesNotContain(_socket.Sent, IsBareTurnComplete);
    }

    [Then("le silence prolongé est streamé")]
    public void ThenSilenceStreame()
    {
        // Flux continu exigé par le serveur : 2 forts + 10 silences = 12 envois.
        long audioSent = _socket.Sent.Count(json => json.Contains("realtimeInput"));
        Assert.Equal(12, audioSent);
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
            "cle-fausse", "SYS", new[] { "happy" }, new[] { "giggle" }, new[] { "nod_yes" },
            _socket, _mic, _player,
            new LiveCallbacks(
                OnEmotion: emotion => _emotions.Add(emotion),
                OnSound: sound => _sounds.Add(sound),
                OnHeardTranscript: text => _heard.Add(text),
                OnRobotTranscript: text => _said.Add(text),
                OnSpeaking: speaking => _speaking.Add(speaking),
                OnGlow: _ => { },
                OnGesture: _ => { },
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
        for (int i = 0; i < 5; i++)
        {
            _mic.Push(loud);
        }
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

    [Then("seul du silence est envoyé pendant la parole du robot")]
    public void ThenSilencePendantParole()
    {
        Assert.True(_finished);
        List<string> audios = _sentWhileSpeaking.Where(json => json.Contains("realtimeInput")).ToList();
        Assert.Equal(5, audios.Count);
        Assert.All(audios, json => Assert.True(IsSilenceAudio(json)));
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

    /// <summary>Claquement : RMS max mais ZCR 1.0 (non voisé).</summary>
    private static byte[] BangChunk()
    {
        byte[] bang = new byte[320];
        for (int i = 0; i < 160; i++)
        {
            short v = i % 2 == 0 ? short.MaxValue : short.MinValue;
            bang[i * 2] = (byte)(v & 0xFF);
            bang[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return bang;
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

    private static bool IsSilenceAudio(string json)
    {
        if (!json.Contains("realtimeInput"))
        {
            return false;
        }
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            string? data = doc.RootElement
                .GetProperty("realtimeInput").GetProperty("audio").GetProperty("data").GetString();
            return data is not null && Convert.FromBase64String(data).All(b => b == 0);
        }
        catch
        {
            return false;
        }
    }
}
