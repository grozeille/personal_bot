namespace TabletApp.AI;

/// <summary>
/// Petits sons robot synthétisés en local (PCM 16 bits mono, doux et ronds
/// façon EVE : sinus et glissandos, jamais de carrés agressifs). Deux usages :
/// les sons système (bâillement, réveil, erreur) joués par l'app, et la
/// palette <see cref="Names"/> jouable par le LLM (tool play_sound en Live,
/// champ "sound" en unaire). 100 % déterministe (bruit à graine fixe).
/// </summary>
internal static class RobotSounds
{
    internal const int SampleRate = 24000;

    /// <summary>Palette jouable par le LLM (doit matcher le prompt).</summary>
    internal static IReadOnlyList<string> Names { get; } = new[]
    {
        "giggle", "curious", "sad", "wow", "oops", "snore",
    };

    /// <summary>Rend un son nommé, ou null si inconnu (appelant silencieux).</summary>
    internal static byte[]? Render(string name, int sampleRate = SampleRate)
    {
        short[] samples = name switch
        {
            "giggle" => Concat(
                Blip(700, 1050, 0.09, sampleRate),
                Silence(0.05, sampleRate),
                Blip(800, 1150, 0.09, sampleRate),
                Silence(0.05, sampleRate),
                Blip(900, 1300, 0.12, sampleRate)),
            "curious" => Concat(
                Sweep(500, 900, 0.18, sampleRate),
                Sweep(900, 750, 0.12, sampleRate)),
            "sad" => Sweep(480, 240, 0.50, sampleRate),
            "wow" => Shimmer(400, 1200, 0.35, sampleRate),
            "oops" => Sweep(220, 160, 0.18, sampleRate),
            "prout" => Prout(0.7, sampleRate),
            "snore" => Snore(1.0, sampleRate),
            "reveil" => Concat(
                Blip(800, 1400, 0.12, sampleRate),
                Silence(0.08, sampleRate),
                Blip(900, 1500, 0.14, sampleRate)),
            "baillement" => Concat(
                Sweep(550, 180, 0.90, sampleRate),
                Breath(0.35, sampleRate)),
            "erreur" => Concat(
                Blip(300, 280, 0.14, sampleRate),
                Silence(0.10, sampleRate),
                Blip(280, 260, 0.16, sampleRate)),
            _ => Array.Empty<short>(),
        };
        if (samples.Length == 0)
        {
            return null;
        }
        byte[] pcm = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            pcm[i * 2] = (byte)(samples[i] & 0xFF);
            pcm[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
        }
        return pcm;
    }

    /// <summary>Enveloppe un PCM 16 bits mono dans un conteneur WAV.</summary>
    internal static byte[] ToWav(byte[] pcm, int sampleRate = SampleRate)
    {
        byte[] wav = new byte[44 + pcm.Length];
        WriteAscii(wav, 0, "RIFF");
        WriteInt32(wav, 4, 36 + pcm.Length);
        WriteAscii(wav, 8, "WAVE");
        WriteAscii(wav, 12, "fmt ");
        WriteInt32(wav, 16, 16);
        WriteInt16(wav, 20, 1);
        WriteInt16(wav, 22, 1);
        WriteInt32(wav, 24, sampleRate);
        WriteInt32(wav, 28, sampleRate * 2);
        WriteInt16(wav, 32, 2);
        WriteInt16(wav, 34, 16);
        WriteAscii(wav, 36, "data");
        WriteInt32(wav, 40, pcm.Length);
        Buffer.BlockCopy(pcm, 0, wav, 44, pcm.Length);
        return wav;
    }

    // Blip montant doux (attaque 10 ms, décroissance exponentielle).
    private static short[] Blip(double fromHz, double toHz, double seconds, int rate)
    {
        int n = (int)(seconds * rate);
        var out_ = new short[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            double f = fromHz + (toHz - fromHz) * t / seconds;
            phase += 2.0 * Math.PI * f / rate;
            double attack = Math.Min(1.0, t / 0.01);
            double env = attack * Math.Exp(-3.0 * t / seconds);
            out_[i] = (short)(12000 * env * Math.Sin(phase));
        }
        return out_;
    }

    // Glissando enveloppé en cloche (démarre et finit à zéro : sans clic).
    private static short[] Sweep(double fromHz, double toHz, double seconds, int rate)
    {
        int n = (int)(seconds * rate);
        var out_ = new short[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            double f = fromHz + (toHz - fromHz) * t / seconds;
            phase += 2.0 * Math.PI * f / rate;
            double env = Math.Sin(Math.PI * t / seconds);
            out_[i] = (short)(11000 * env * Math.Sin(phase));
        }
        return out_;
    }

    // Montée scintillante (modulation d'amplitude 12 Hz).
    private static short[] Shimmer(double fromHz, double toHz, double seconds, int rate)
    {
        int n = (int)(seconds * rate);
        var out_ = new short[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            double f = fromHz + (toHz - fromHz) * t / seconds;
            phase += 2.0 * Math.PI * f / rate;
            double env = Math.Sin(Math.PI * Math.Min(1.0, t / seconds));
            double am = 0.75 + 0.25 * Math.Sin(2.0 * Math.PI * 12 * t);
            out_[i] = (short)(11000 * env * am * Math.Sin(phase));
        }
        return out_;
    }

    // Ronflement : 90 Hz modulé à 3,5 Hz.
    private static short[] Snore(double seconds, int rate)
    {
        int n = (int)(seconds * rate);
        var out_ = new short[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            double env = Math.Sin(Math.PI * Math.Min(1.0, t / seconds));
            double am = 0.6 + 0.4 * Math.Sin(2.0 * Math.PI * 3.5 * t);
            out_[i] = (short)(9000 * env * am * Math.Sin(2.0 * Math.PI * 90 * t));
        }
        return out_;
    }

    // Souffle du bâillement : bruit doux à graine fixe (déterministe).
    // Prout : descente 160->70 Hz avec trémolo + souffle (graine fixe).
    private static short[] Prout(double seconds, int rate)
    {
        int n = (int)(seconds * rate);
        var out_ = new short[n];
        var random = new Random(4242);
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            double f = 160 - 90 * t / seconds;
            phase += 2.0 * Math.PI * f / rate;
            double env = Math.Sin(Math.PI * Math.Min(1.0, t / seconds));
            double am = 0.5 + 0.5 * Math.Sin(2.0 * Math.PI * 28 * t);
            double noise = (random.NextDouble() * 2 - 1) * 0.25;
            out_[i] = (short)(11000 * env * am * (Math.Sin(phase) * 0.8 + noise));
        }
        return out_;
    }

    private static short[] Breath(double seconds, int rate)
    {
        int n = (int)(seconds * rate);
        var out_ = new short[n];
        var random = new Random(7123);
        double soft = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / rate;
            double env = Math.Sin(Math.PI * Math.Min(1.0, t / seconds));
            soft += 0.08 * (random.NextDouble() * 2 - 1 - soft);
            out_[i] = (short)(5000 * env * soft);
        }
        return out_;
    }

    private static short[] Silence(double seconds, int rate) =>
        new short[(int)(seconds * rate)];

    private static short[] Concat(params short[][] parts)
    {
        int total = 0;
        foreach (short[] part in parts)
        {
            total += part.Length;
        }
        var out_ = new short[total];
        int at = 0;
        foreach (short[] part in parts)
        {
            Array.Copy(part, 0, out_, at, part.Length);
            at += part.Length;
        }
        return out_;
    }

    private static void WriteAscii(byte[] wav, int at, string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            wav[at + i] = (byte)text[i];
        }
    }

    private static void WriteInt32(byte[] wav, int at, int value)
    {
        wav[at] = (byte)(value & 0xFF);
        wav[at + 1] = (byte)((value >> 8) & 0xFF);
        wav[at + 2] = (byte)((value >> 16) & 0xFF);
        wav[at + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteInt16(byte[] wav, int at, int value)
    {
        wav[at] = (byte)(value & 0xFF);
        wav[at + 1] = (byte)((value >> 8) & 0xFF);
    }
}
