using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using TabletApp.AI;
using TabletApp.Gestures;
using TabletApp.UI;
using TabletApp.Update;

namespace TabletApp;

internal sealed class MainForm : Form
{
    // Touches 1-9,0 : même ordre que les visages du dessin de référence (lecture 1-10).
    private static readonly RobotExpression[] ExpressionKeys =
    {
        RobotExpression.Neutral,
        RobotExpression.Dead,
        RobotExpression.Mean,
        RobotExpression.Dizzy,
        RobotExpression.Happy,
        RobotExpression.Tired,
        RobotExpression.Love,
        RobotExpression.Laugh,
        RobotExpression.Wink,
        RobotExpression.Sleep,
    };

    private readonly RobotFaceRenderer _face;
    private readonly System.Windows.Forms.Timer _titleTimer;
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly ConfirmDialog _confirm = new();
    private readonly RobotUpdater _updater;
    private readonly TapMessageFilter _tapFilter;
    private bool _applying;
    private bool _iconPreview;
    private bool _voiceRunning;
    private bool _chatRunning;
    private CancellationTokenSource? _chatCts;
    private bool _pendingCalibration;
    private LiveSession? _liveSession;
    private Conversation? _conversation;
    private WakeWordListener? _wake;

    // Empêche la mise en veille de l'écran / du système (tête de robot allumée en continu).
    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;

    // Désactive le "clic droit sur appui long" tactile dans notre fenêtre.
    private const int WM_TABLET_QUERYSYSTEMGESTURESTATUS = 0x02CC;
    private const int TABLET_DISABLE_PRESSANDHOLD = 0x00000001;

    public MainForm()
    {
        Text = "Robot Companion";
        BackColor = Color.Black;
        if (Program.FaceSize is { } faceSize)
        {
            // Mode dev : fenetre aux dimensions de la tablette (logs + simu visibles a cote).
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = faceSize;
            TopMost = false;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
        }

        _face = new RobotFaceRenderer { Dock = DockStyle.Fill };
        Controls.Add(_face);
        _face.YawnStarted += () => _ = PlaySoundAsync("baillement");
        _face.WokeUp += () => _ = PlaySoundAsync("reveil");

        UpdateOptions options = UpdateOptions.Load();
        _updater = new RobotUpdater(new VelopackUpdateSource(options.Source));
        _face.Idle = IdleOptions.Load();

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.S)
            {
                // Toute touche est une activité (réarme l'endormissement,
                // réveille). S gère son cas plus bas (pas de réveil).
                _face.NotifyActivity();
            }
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.S)
            {
                _face.NotifyActivity(wake: false);
                _face.FallAsleep();
            }
            else if (e.KeyCode == Keys.W)
            {
                _face.WakeUp();
            }
            else if (e.KeyCode == Keys.F11)
            {
                _confirm.Cancel();
                _face.SetExpression(RobotExpression.Thinking);
            }
            else if (e.KeyCode == Keys.F12)
            {
                _confirm.Cancel();
                _face.SetExpression(RobotExpression.Shocked);
            }
            else if (e.KeyCode == Keys.U)
            {
                // Dev : forcer une vérification de maj (le robot le fait seul toutes les 5 min).
                _ = CheckForUpdatesAsync();
            }
            else if (e.KeyCode == Keys.E)
            {
                // Enrôlement : recalibrer le wake word sur la voix.
                _ = RunEnrollAsync();
            }
            else if (e.KeyCode == Keys.C)
            {
                _confirm.Cancel();
                _face.SetExpression(RobotExpression.Confirm);
            }
            else if (e.KeyCode == Keys.I)
            {
                // Dev : aperçu de l'icône de maj (sans NAS), tap icône puis
                // yeux testables comme une vraie maj. Sans effet sur la détection.
                _iconPreview = !_iconPreview;
                UpdateFaceIcon();
            }
            else if (e.KeyCode == Keys.T)
            {
                _ = RunVoiceTestAsync();
            }
            else if (e.KeyCode == Keys.H)
            {
                // Dev : geste tete + visage couples (simu Godot ou Pi sur HEAD_SIM_HOST:HEAD_SIM_PORT).
                _ = RunHeadTestAsync("tilt_curious");
            }
            else if (e.KeyCode == Keys.D)
            {
                ToggleConversation();
            }
            else if (Digit(e.KeyCode) is { } digit)
            {
                _confirm.Cancel();
                _face.SetExpression(ExpressionKeys[digit]);
            }
        };

        _titleTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _titleTimer.Tick += (_, _) =>
            Text = $"Robot Companion — {_face.Fps} FPS"
                + (_updater.UpdateAvailable ? " — MAJ !" : "")
                + (_iconPreview ? " — APERÇU" : "");
        _titleTimer.Start();

        _updateTimer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(60_000, (int)options.CheckInterval.TotalMilliseconds),
        };
        _updateTimer.Tick += (_, _) => _ = CheckForUpdatesAsync();
        _updateTimer.Start();

        _tapFilter = new TapMessageFilter(this);
        Application.AddMessageFilter(_tapFilter);

        // Premier contrôle peu après le démarrage (silencieux si NAS hors-ligne).
        Shown += (_, _) =>
        {
            _ = CheckForUpdatesAsync();
            PromptApiKeyIfMissing();
            StartWakeWord();
            if (!WakeEnrollment.IsCalibrated() && WakeWordOptions.Load().AutoEnroll)
            {
                _ = RunEnrollAsync();
            }
            if (Program.AutoHeadTest)
            {
                _ = RunHeadTestAsync();
            }
            if (Program.AutoVoiceTest)
            {
                _ = RunVoiceTestAsync();
            }
            if (Program.AutoDiscuss)
            {
                _ = RunConversationAsync();
            }
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (Program.FaceSize is null)
        {
            // Vrai plein écran : couvre aussi la barre des tâches (Maximized seul la respecte).
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
            Cursor.Hide();
        }
        SetThreadExecutionState(ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_TABLET_QUERYSYSTEMGESTURESTATUS)
        {
            m.Result = (IntPtr)TABLET_DISABLE_PRESSANDHOLD;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _wake?.Dispose();
        _wake = null;
        Application.RemoveMessageFilter(_tapFilter);
        SetThreadExecutionState(ES_CONTINUOUS);
        Cursor.Show();
        base.OnFormClosed(e);
    }

    private static int? Digit(Keys key) => key switch
    {
        >= Keys.D1 and <= Keys.D9 => (int)(key - Keys.D1),
        Keys.D0 => 9,
        >= Keys.NumPad1 and <= Keys.NumPad9 => (int)(key - Keys.NumPad1),
        Keys.NumPad0 => 9,
        _ => null,
    };

    private async Task CheckForUpdatesAsync()
    {
        if (_applying)
        {
            return;
        }
        await _updater.CheckAsync().ConfigureAwait(true);
        UpdateFaceIcon();
        if (!_updater.UpdateAvailable && _confirm.Active && !_applying)
        {
            // Maj disparue (NAS débranché) : on referme le dialogue.
            _confirm.Cancel();
            _face.SetExpression(_confirm.Previous);
        }
    }

    private void UpdateFaceIcon() =>
        _face.UpdateAvailable = _updater.UpdateAvailable || _iconPreview;

    /// <summary>
    /// Première installation : demande la clé Gemini si absente et la stocke.
    /// Ne bloque jamais le démarrage (annuler = continuer sans voix).
    /// </summary>
    private void PromptApiKeyIfMissing()
    {
        try
        {
            if (GeminiKey.Resolve() is not null)
            {
                return;
            }
            Cursor.Show();
            try
            {
                using var dialog = new ApiKeyDialog();
                if (dialog.ShowDialog(this) == DialogResult.OK
                    && !string.IsNullOrWhiteSpace(dialog.ApiKey))
                {
                    GeminiKey.StoreApiKey(GeminiKey.SecretsPath(), dialog.ApiKey);
                }
            }
            finally
            {
                Cursor.Hide();
            }
        }
        catch
        {
            // Dialogue impossible : l'app démarre quand même.
        }
    }

    private async Task DownloadAndApplyAsync()
    {
        _applying = true;
        RobotExpression previous = _confirm.Previous;
        _confirm.Cancel();
        _face.BusyDots = true;
        await _updater.DownloadAndApplyAsync().ConfigureAwait(true);
        // Succès = Velopack a relancé l'app (on ne revient pas ici).
        // Échec = on restaure le visage, la maj reste proposée.
        _face.BusyDots = false;
        _face.SetExpression(previous);
        UpdateFaceIcon();
        _applying = false;
    }

    /// <summary>
    /// Test tete (touche H, flag --head-test) : geste + visage couples.
    /// Tilt_curious par defaut : visage Thinking + piste TCP vers simu/Pi.
    /// </summary>
    private async Task RunHeadTestAsync(string? gesture = null)
    {
        gesture ??= Program.AutoHeadGesture;
        try
        {
            string result = await HeadTest.PlayAsync(_face, gesture, PlaySoundAsync).ConfigureAwait(true);
            VoiceLog.Info(result);
        }
        catch (Exception ex)
        {
            VoiceLog.Error(ex.Message);
        }
    }

    /// <summary>
    /// Test voix #5 (touche T, flag --voice-test) : dit bonjour (TTS Gemini),
    /// écoute 10 s (STT Gemini), visage cœur si « bonjour » entendu. Visage
    /// mort 2 s si échec (clé, micro, réseau). Tourne sur le thread UI.
    /// </summary>
    private async Task RunVoiceTestAsync()
    {
        if (_voiceRunning || _chatRunning)
        {
            return;
        }
        _voiceRunning = true;
        _face.Concentrated = true;
        _wake?.Stop();
        RobotExpression previous = _face.CurrentExpression;
        try
        {
            string key = GeminiKey.Resolve()
                ?? throw new InvalidOperationException("Clé Gemini manquante.");
            var speech = new GeminiSpeech(key);
            var test = new VoiceTest(
                (text, ct) => SpeakAsync(speech, text, ct),
                (duration, ct) => ListenAsync(speech, duration, ct),
                expr => Ui(() => _face.SetExpression(expr)));
            string? heard = await test.RunAsync(previous, CancellationToken.None);
            VoiceLog.Info(VoiceTest.HearsBonjour(heard) ? "Bonjour détecté → cœur." : "Pas de bonjour.");
        }
        catch (Exception ex)
        {
            VoiceLog.Error(ex.Message);
            _ = PlaySoundAsync("erreur");
            _face.SetExpression(RobotExpression.Dead);
            await Task.Delay(TimeSpan.FromSeconds(2));
            _face.SetExpression(previous);
        }
        finally
        {
            _face.Concentrated = false;
            _wake?.Start();
            _voiceRunning = false;
        }
    }

    /// <summary>
    /// Enrôlement (1er lancement sans calibration, touche E, flag --enroll) :
    /// le robot se présente et calibre le wake word sur la voix (prises
    /// parlées + sweep offline). Micro wake libéré pendant, comme le test voix.
    /// </summary>
    private async Task RunEnrollAsync()
    {
        if (_voiceRunning || _chatRunning)
        {
            return;
        }
        _voiceRunning = true;
        _face.Concentrated = true;
        _wake?.Stop();
        RobotExpression previous = _face.CurrentExpression;
        try
        {
            string key = GeminiKey.Resolve()
                ?? throw new InvalidOperationException("Clé Gemini manquante.");
            var speech = new GeminiSpeech(key);
            WakeEnrollment.EnrollPhase enrollPhase = WakeEnrollment.EnrollPhase.Ambient;
            await WakeEnrollment.RunWizardAsync(
                (text, ct) => SpeakAsync(speech, text, ct),
                async (seconds, ct) =>
                {
                    // Halo seulement pour les prises (pas le silence ambiant).
                    bool halo = enrollPhase == WakeEnrollment.EnrollPhase.Takes;
                    if (halo)
                    {
                        Ui(() => _face.Glow = VoiceGlow.Listening);
                    }
                    try
                    {
                        return await NAudioVoice.RecordAsync(TimeSpan.FromSeconds(seconds), ct);
                    }
                    finally
                    {
                        if (halo)
                        {
                            Ui(() => _face.Glow = VoiceGlow.None);
                        }
                    }
                },
                (wav, ct) => speech.TranscribeAsync(wav, ct),
                VoiceLog.Info,
                phase => Ui(() =>
                {
                    enrollPhase = phase;
                    switch (phase)
                    {
                        case WakeEnrollment.EnrollPhase.Thinking:
                            _face.SetExpression(RobotExpression.Thinking);
                            _face.Glow = VoiceGlow.Thinking;
                            break;
                        case WakeEnrollment.EnrollPhase.DoneOk:
                            _face.SetExpression(RobotExpression.Happy);
                            _face.Glow = VoiceGlow.None;
                            break;
                        case WakeEnrollment.EnrollPhase.DoneKo:
                            _face.SetExpression(previous);
                            _face.Glow = VoiceGlow.None;
                            break;
                    }
                }),
                CalibrationPaths.Default,
                CancellationToken.None);
            _face.SetExpression(previous);
        }
        catch (Exception ex)
        {
            VoiceLog.Error(ex.Message);
            _ = PlaySoundAsync("erreur");
            _face.SetExpression(RobotExpression.Dead);
            await Task.Delay(TimeSpan.FromSeconds(2));
            _face.SetExpression(previous);
        }
        finally
        {
            _face.Concentrated = false;
            _face.Glow = VoiceGlow.None;
            _wake?.Start();
            _voiceRunning = false;
        }
    }

    private void StartWakeWord()
    {
        try
        {
            WakeWordOptions options = WakeWordOptions.Load();
            if (!SherpaWakeDetector.TryCreate(
                options.Threshold, options.Score,
                out SherpaWakeDetector? detector) || detector is null)
            {
                VoiceLog.Info("Wake word désactivé : modèle absent ou invalide.");
                return;
            }
            var listener = new WakeWordListener(detector, gateRms: options.GateRms);
            listener.Detected += () => BeginInvoke(new Action(OnWakeWord));

            listener.Tick += tick =>
            {
                if (tick.FeedingStarted) { VoiceLog.Info("Wake : parole entendue, analyse..."); }
                if (tick.FeedingStopped) { VoiceLog.Info("Wake : silence, pas de réveil."); }
                if (tick.Detected) { VoiceLog.Info("Wake : motif reconnu."); }
            };            _wake = listener;
            listener.Start();
            VoiceLog.Info("Wake word « Hey Patate » en écoute.");

            VoiceLog.Info($"Wake : porte {options.GateRms:F0} RMS, seuil {options.Threshold:F2}, " +
                $"modèle {SherpaWakeDetector.ResolveModelDir()}.");        }
        catch (Exception ex)
        {
            VoiceLog.Error("Wake word : " + ex.Message);
            _wake?.Dispose();
            _wake = null;
        }
    }

    private void OnWakeWord()
    {
        if (_chatRunning || _voiceRunning || _confirm.Active || _applying)
        {
            return;
        }
        VoiceLog.Info("Hey Patate détecté.");
        _face.NotifyActivity();
        _ = RunConversationAsync();
    }

    private void ToggleConversation()
    {
        if (_chatRunning && _chatCts is not null)
        {
            _chatCts.Cancel();
            return;
        }
        if (_chatRunning || _voiceRunning)
        {
            return;
        }
        _ = RunConversationAsync();
    }

    /// <summary>
    /// Discussion libre (touche D, flag --discuter) : salutations, boucle
    /// écoute → pense → répond avec émotion, adieux après 2 silences ou D.
    /// </summary>
    private async Task RunLiveAsync(string key, CancellationToken ct)
    {
        await using var socket = new LiveClientWebSocket();
        using var mic = new LiveMic();
        using var player = new LivePlayer();
        var session = new LiveSession(
            key, GeminiBrain.LiveSystemPrompt, GeminiBrain.EmotionNames, RobotSounds.Names, GeminiBrain.GestureNames,
            socket, mic, player,
            new LiveCallbacks(
                OnEmotion: emotion => Ui(() =>
                {
                    if (ExpressionController.TryParse(emotion, out RobotExpression parsed))
                    {
                        _face.SetExpression(parsed);
                    }
                    VoiceLog.Info($"Emotion : {emotion}.");
                }),
                OnGesture: gesture => { _ = RunHeadTestAsync(gesture); },
                OnSound: sound =>
                {
                    byte[]? pcm = RobotSounds.Render(sound);
                    if (pcm is not null)
                    {
                        player.PlayChunk(pcm);
                    }
                    else
                    {
                        VoiceLog.Info($"Son inconnu ignoré : {sound}.");
                    }
                },
                OnHeardTranscript: text => VoiceLog.Info($"Entendu : '{text}'."),
                OnRobotTranscript: text => VoiceLog.Info($"Robot : {text}"),
                OnSpeaking: speaking => Ui(() => _face.Speaking = speaking),
                OnGlow: glow => Ui(() => _face.Glow = glow),
                OnUserSpeech: () => Ui(() => _face.NotifyActivity()),
                OnLog: VoiceLog.Info,
                OnCalibrationRequested: () => _pendingCalibration = true));
        _liveSession = session;
        try
        {
            await session.RunAsync(ct);
        }
        finally
        {
            _liveSession = null;
        }
    }

    private async Task RunUnaryAsync(string key, RobotExpression previous, CancellationToken ct)
    {
        var speech = new GeminiSpeech(key);
        var brain = new GeminiBrain(key);
        var chat = new Conversation(
            (duration, _) => ListenAsync(speech, duration, ct),
            async (history, heard, _) =>
            {
                Ui(() => _face.SetExpression(RobotExpression.Thinking));
                Ui(() => _face.Glow = VoiceGlow.Thinking);
                Ui(() => _face.NotifyActivity());
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var thought = await brain.ThinkAsync(history, heard, ct);
                string son = thought.Sound is null ? "" : $" + son {thought.Sound}";
                VoiceLog.Info($"Pense : '{thought.Reply ?? "(rien)"}' + " +
                    $"{thought.Emotion ?? "(neutre)"}{son} ({watch.Elapsed.TotalSeconds:F1} s).");
                return thought;
            },
            (text, _) => SpeakAsync(speech, text, ct),
            expr => Ui(() => _face.SetExpression(expr)),
            playSound: (sound, _) => PlaySoundAsync(sound));
        _conversation = chat;
        try
        {
            await chat.RunAsync(previous, ct);
        }
        finally
        {
            _conversation = null;
        }
    }

    /// <summary>Tap pendant une discussion : retour écoute ou fin, selon l'état visible.</summary>
    internal enum ConversationTapAction
    {
        BackToListening,
        EndDiscussion,
    }

    internal static ConversationTapAction ConversationTap(VoiceGlow glow, bool speaking) =>
        glow == VoiceGlow.Thinking || speaking
            ? ConversationTapAction.BackToListening
            : ConversationTapAction.EndDiscussion;

    private void TapConversation()
    {
        if (!_chatRunning)
        {
            return;
        }
        if (ConversationTap(_face.Glow, _face.Speaking) == ConversationTapAction.BackToListening)
        {
            _liveSession?.InterruptToListening();
            _conversation?.CancelTurn();
            VoiceLog.Info("Tap : retour à l'écoute.");
        }
        else if (_chatCts is not null)
        {
            VoiceLog.Info("Tap : fin de discussion.");
            _chatCts.Cancel();
        }
    }

    private void Ui(Action action)
    {
        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task RunConversationAsync()
    {
        if (_chatRunning)
        {
            return;
        }
        _chatRunning = true;
        _chatCts = new CancellationTokenSource();
        _confirm.Cancel();
        _face.Concentrated = true;
        _wake?.Stop();
        VoiceLog.Info($"Prompt voix v{GeminiBrain.PromptVersion} (" +
            (GeminiBrain.PersonalityCustom ? "personnalisé" : "défaut") + ").");
        RobotExpression previous = _face.CurrentExpression;
        try
        {
            string key = GeminiKey.Resolve()
                ?? throw new InvalidOperationException("Clé Gemini manquante.");
            try
            {
                await RunLiveAsync(key, _chatCts.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                VoiceLog.Error($"Live : {ex.Message} - repli unaire.");
                await RunUnaryAsync(key, previous, _chatCts.Token);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            VoiceLog.Error(ex.Message);
            _ = PlaySoundAsync("erreur");
            _face.SetExpression(RobotExpression.Dead);
            await Task.Delay(TimeSpan.FromSeconds(2));
            _face.SetExpression(previous);
        }
        finally
        {
            _face.Concentrated = false;
            _wake?.Start();
            _chatRunning = false;
            _chatCts?.Dispose();
            _chatCts = null;
            if (_pendingCalibration)
            {
                _pendingCalibration = false;
                _ = RunEnrollAsync();
            }
        }
    }

    /// <summary>Joue un petit son robot (nom inconnu ignoré, jamais d'exception).</summary>
    private async Task PlaySoundAsync(string name)
    {
        try
        {
            byte[]? pcm = RobotSounds.Render(name);
            if (pcm is null)
            {
                VoiceLog.Info($"Son inconnu ignoré : {name}.");
                return;
            }
            await NAudioVoice.PlayAsync(RobotSounds.ToWav(pcm, 24000), CancellationToken.None);
        }
        catch (Exception ex)
        {
            VoiceLog.Info($"Son {name} impossible : {ex.Message}.");
        }
    }

    private async Task SpeakAsync(GeminiSpeech speech, string text, CancellationToken ct)
    {
        VoiceLog.Info($"Robot : {text}");
        Ui(() => _face.Glow = VoiceGlow.None);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        byte[]? asset = WakeEnrollment.LoadPrompt(text);
        byte[] wav;
        if (asset is not null)
        {
            wav = asset;
        }
        else
        {
            wav = await speech.SpeakAsync(text, ct);
            VoiceLog.Info($"Synthèse : {watch.Elapsed.TotalSeconds:F1} s.");
        }
        watch.Restart();
        Ui(() => _face.Speaking = true);
        try
        {
            await NAudioVoice.PlayAsync(wav, ct);
        }
        finally
        {
            Ui(() => _face.Speaking = false);
        }
        VoiceLog.Info($"Lecture : {watch.Elapsed.TotalSeconds:F1} s.");
    }

    private async Task<string?> ListenAsync(GeminiSpeech speech, TimeSpan duration, CancellationToken ct)
    {
        _face.Glow = VoiceGlow.Listening;
        try
        {
            // Laisse mourir l'écho de notre propre voix avant d'enregistrer.
            await Task.Delay(400, ct);
            VoiceClip clip = await NAudioVoice.RecordAsync(duration, ct);
            VoiceLog.Info($"Écoute : {clip.RecordedFor.TotalSeconds:F1} s, " +
                $"parole={(clip.HadSpeech ? "oui" : "non")}, RMS max {clip.PeakRms:F0}.");
            if (!clip.HadSpeech)
            {
                return null;
            }
            _face.Glow = VoiceGlow.Thinking;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string? heard = await speech.TranscribeAsync(clip.Wav, ct);
            VoiceLog.Info($"Entendu : '{heard ?? "(rien)"}' (STT {watch.Elapsed.TotalSeconds:F1} s).");
            return heard;
        }
        finally
        {
            Ui(() => _face.Glow = VoiceGlow.None);
        }
    }

    private void OnTapDown()
    {
        // Tout toucher est une activité (réveille sur tablette sans clavier).
        _face.NotifyActivity();
        // Que des taps simples : pas d'appui long (Windows le promeut en
        // clic droit sur la tablette). Coin haut-gauche = sortir (toujours),
        // icône haut-droite = maj (si affichée).
        Point location = PointToClient(MousePosition);
        if (_confirm.Active)
        {
            (float vx, float vy) = FacePainter.ToView(
                location.X, location.Y, ClientSize.Width, ClientSize.Height);
            switch (_confirm.Tap(vx, vy))
            {
                case ConfirmTap.Cancelled:
                    _face.SetExpression(_confirm.Previous);
                    break;
                case ConfirmTap.Confirmed:
                    if (_confirm.Purpose == ConfirmPurpose.Quit)
                    {
                        Close();
                    }
                    else
                    {
                        _face.SetExpression(_confirm.Previous);
                        if (!_applying)
                        {
                            _ = DownloadAndApplyAsync();
                        }
                    }
                    break;
                case ConfirmTap.Ignored:
                    break;
            }
            return;
        }
        if (ConfirmDialog.QuitHit(location))
        {
            _confirm.Show(_face.CurrentExpression, ConfirmPurpose.Quit);
            _face.SetExpression(RobotExpression.Confirm);
        }
        else if (_face.UpdateAvailable && !_applying
            && ConfirmDialog.IconHit(location, ClientSize.Width))
        {
            _confirm.Show(_face.CurrentExpression, ConfirmPurpose.Update);
            _face.SetExpression(RobotExpression.Confirm);
        }
        else if (_chatRunning)
        {
            // Tap pendant une discussion : réflexion/parole -> retour écoute,
            // écoute -> fin de discussion.
            TapConversation();
        }
        else if (!_voiceRunning && !_applying)
        {
            // Tap sur le visage (pas de clavier sur tablette) : discuter.
            _ = RunConversationAsync();
        }
    }

    /// <summary>
    /// Les touchers/clics arrivent au contrôle sous le doigt (le canvas), pas au
    /// formulaire : ce filtre les intercepte au niveau applicatif pour les
    /// taps (sortie en haut-gauche, maj en haut-droite puis yeux).
    /// </summary>
    private sealed class TapMessageFilter : IMessageFilter
    {
        private const int WM_LBUTTONDOWN = 0x0201;

        private readonly MainForm _form;

        public TapMessageFilter(MainForm form)
        {
            _form = form;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_LBUTTONDOWN)
            {
                _form.OnTapDown();
            }
            return false;
        }
    }
}
