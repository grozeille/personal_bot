// Règles maison (version sans +4) : +1 en couleur, +2 joker (choisit sa
// couleur), cumul même valeur uniquement, combo = 2+ cartes de même
// symbole (pas les +1/+2), passe/inverser à effet unique, donne de 8,
// pioche illimitée. Paquet Express : 96 + 4 jokers + 4 jokers +2 = 104.
namespace TabletApp.Uno;

/// <summary>Couleurs du Uno (Wild = joker, sans couleur propre).</summary>
internal enum UnoColor
{
    Red,
    Yellow,
    Green,
    Blue,
    Wild,
}

/// <summary>Rangs du Uno maison (pas de +4, pas de +2 couleur).</summary>
internal enum UnoRank
{
    N1 = 1,
    N2 = 2,
    N3 = 3,
    N4 = 4,
    N5 = 5,
    N6 = 6,
    N7 = 7,
    N8 = 8,
    N9 = 9,
    Plus1,
    Skip,
    Reverse,
    Wild,
    WildPlus2,
}

/// <summary>Carte de Uno + noms français canoniques + parseur tolérant.</summary>
internal sealed record UnoCard(UnoColor Color, UnoRank Rank)
{
    public bool IsWild => Color == UnoColor.Wild;

    public bool IsNumber => Rank is >= UnoRank.N1 and <= UnoRank.N9;

    public bool IsDraw => Rank is UnoRank.Plus1 or UnoRank.WildPlus2;

    public int DrawValue => Rank switch
    {
        UnoRank.Plus1 => 1,
        UnoRank.WildPlus2 => 2,
        _ => 0,
    };

    /// <summary>Combo multi-cartes : chiffres uniquement, même nombre.</summary>
    public bool Comboable => IsNumber;

    public string ToFrench()
    {
        if (Rank == UnoRank.Wild)
        {
            return "joker";
        }
        if (Rank == UnoRank.WildPlus2)
        {
            return "+2";
        }
        string color = ColorToFrench(Color);
        return Rank switch
        {
            UnoRank.Plus1 => $"+1 {color}",
            UnoRank.Skip => $"passe ton tour {color}",
            UnoRank.Reverse => $"inverser {color}",
            _ => $"{(int)Rank} {color}",
        };
    }

    public static string ColorToFrench(UnoColor color) => color switch
    {
        UnoColor.Red => "rouge",
        UnoColor.Yellow => "jaune",
        UnoColor.Green => "vert",
        UnoColor.Blue => "bleu",
        _ => "joker",
    };

    public static bool TryParseColor(string? text, out UnoColor color)
    {
        string norm = Norm(text);
        if (norm.Contains("rouge"))
        {
            color = UnoColor.Red;
            return true;
        }
        if (norm.Contains("jaune"))
        {
            color = UnoColor.Yellow;
            return true;
        }
        if (norm.Contains("vert"))
        {
            color = UnoColor.Green;
            return true;
        }
        if (norm.Contains("bleu"))
        {
            color = UnoColor.Blue;
            return true;
        }
        // Homophones de "vert" fréquents en transcription (mots entiers :
        // "inverser" ne doit pas matcher).
        string[] words = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Contains("vers") || words.Contains("verre") || words.Contains("vair"))
        {
            color = UnoColor.Green;
            return true;
        }
        color = UnoColor.Wild;
        return false;
    }

    /// <summary>
    /// Parseur tolérant (casse, accents, espaces) : "1 vert", "+1 ROUGE",
    /// "passe ton tour bleu", "joker", "+2". Rejette les cartes
    /// inexistantes ("+2 rouge", "joker vert", couleur sans rang...).
    /// </summary>
    public static bool TryParse(string? text, out UnoCard card)
    {
        card = null!;
        string norm = Norm(text);
        if (norm.Length == 0)
        {
            return false;
        }
        bool hasColor = TryParseColor(norm, out UnoColor color);
        bool plus2 = norm.Contains("+2") || norm.Contains("plus 2") || norm.Contains("plus2");
        bool plus1 = !plus2 && (norm.Contains("+1") || norm.Contains("plus 1") || norm.Contains("plus1"));
        if (plus2)
        {
            // Le +2 est un joker : "+2 rouge" n'existe pas.
            if (hasColor)
            {
                return false;
            }
            card = new UnoCard(UnoColor.Wild, UnoRank.WildPlus2);
            return true;
        }
        if (plus1)
        {
            if (!hasColor)
            {
                return false;
            }
            card = new UnoCard(color, UnoRank.Plus1);
            return true;
        }
        if (norm.Contains("joker"))
        {
            if (hasColor)
            {
                return false;
            }
            card = new UnoCard(UnoColor.Wild, UnoRank.Wild);
            return true;
        }
        if (norm.Contains("passe") || norm.Contains("passer") || norm.Contains("skip")
            || norm.Contains("interdit"))
        {
            if (!hasColor)
            {
                return false;
            }
            card = new UnoCard(color, UnoRank.Skip);
            return true;
        }
        if (norm.Contains("invers") || norm.Contains("reverse"))
        {
            if (!hasColor)
            {
                return false;
            }
            card = new UnoCard(color, UnoRank.Reverse);
            return true;
        }
        if (TryParseDigit(norm, out int digit))
        {
            if (!hasColor)
            {
                return false;
            }
            card = new UnoCard(color, (UnoRank)digit);
            return true;
        }
        return false;
    }

    /// <summary>Parse "1 vert, 1 bleu" / "1 vert puis 1 bleu" (null si échec).</summary>
    public static IReadOnlyList<UnoCard>? TryParseMany(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        string[] parts = Norm(text)
            .Replace(" puis ", ",", StringComparison.Ordinal)
            .Replace(" et ", ",", StringComparison.Ordinal)
            .Split(new[] { ',', ';', '/', '|' }, StringSplitOptions.RemoveEmptyEntries);
        var cards = new List<UnoCard>();
        foreach (string part in parts)
        {
            if (!TryParse(part, out UnoCard card))
            {
                return null;
            }
            cards.Add(card);
        }
        return cards.Count == 0 ? null : cards;
    }

    private static bool TryParseDigit(string norm, out int digit)
    {
        foreach (char c in norm)
        {
            if (c is >= '1' and <= '9')
            {
                digit = c - '0';
                return true;
            }
        }
        string[] words = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string[] names = new[] { "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf" };
        for (int i = 0; i < names.Length; i++)
        {
            if (words.Contains(names[i]))
            {
                digit = i + 1;
                return true;
            }
        }
        digit = 0;
        return false;
    }

    private static string Norm(string? text) =>
        (text ?? "")
        .ToLowerInvariant()
        .Replace('é', 'e').Replace('è', 'e').Replace('ê', 'e')
        .Trim();
}

/// <summary>Résultat d'un coup (message = réponse d'outil pour le modèle).</summary>
internal sealed record UnoPlayResult(
    bool Ok,
    string Message,
    UnoCard? Top,
    UnoColor? TopColor,
    int Stack,
    bool Replay,
    bool Uno,
    bool Won,
    IReadOnlyList<UnoCard> Hand);

/// <summary>
/// Partie de Uno côté robot : donne de 8 depuis un paquet fini mélangé,
/// pioche illimitée (tirage uniforme), validation des coups (simple,
/// combo même symbole, cumul même valeur). Le dessus vient de la photo
/// (l'enfant est physique, seul le jeu du robot est suivi).
/// </summary>
internal sealed class UnoGame
{
    private readonly List<UnoCard> _hand = new();

    public IReadOnlyList<UnoCard> Hand => _hand;

    public bool Started { get; private set; }

    /// <summary>Composition Express : 96 + 4 jokers + 4 jokers +2 = 104 (1-9, pas de 0).</summary>
    public static IReadOnlyList<UnoCard> FullDeck()
    {
        var deck = new List<UnoCard>(104);
        foreach (UnoColor color in new[] { UnoColor.Red, UnoColor.Yellow, UnoColor.Green, UnoColor.Blue })
        {
            for (UnoRank rank = UnoRank.N1; rank <= UnoRank.N9; rank++)
            {
                deck.Add(new UnoCard(color, rank));
                deck.Add(new UnoCard(color, rank));
            }
            foreach (UnoRank rank in new[] { UnoRank.Plus1, UnoRank.Skip, UnoRank.Reverse })
            {
                deck.Add(new UnoCard(color, rank));
                deck.Add(new UnoCard(color, rank));
            }
        }
        for (int i = 0; i < 4; i++)
        {
            deck.Add(new UnoCard(UnoColor.Wild, UnoRank.Wild));
            deck.Add(new UnoCard(UnoColor.Wild, UnoRank.WildPlus2));
        }
        return deck;
    }

    /// <summary>Main forcée pour les tests (self-test déterministe).</summary>
    internal void SetHandForTest(IEnumerable<UnoCard> cards)
    {
        _hand.Clear();
        _hand.AddRange(cards);
        Started = true;
    }

    public IReadOnlyList<UnoCard> NewGame(Random rng)
    {
        List<UnoCard> deck = FullDeck().ToList();
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        _hand.Clear();
        _hand.AddRange(deck.Take(8));
        Started = true;
        return Hand;
    }

    public IReadOnlyList<UnoCard> Draw(int count, Random rng)
    {
        if (!Started)
        {
            throw new InvalidOperationException("Aucune partie en cours.");
        }
        IReadOnlyList<UnoCard> full = FullDeck();
        count = Math.Clamp(count, 1, 20);
        var drawn = new List<UnoCard>(count);
        for (int i = 0; i < count; i++)
        {
            UnoCard card = full[rng.Next(full.Count)];
            _hand.Add(card);
            drawn.Add(card);
        }
        return drawn;
    }

    public UnoPlayResult Play(IReadOnlyList<UnoCard> cards, UnoCard? top, int stack, UnoColor? color)
    {
        if (!Started)
        {
            return Fail("Aucune partie en cours : appelle uno_new_game.", top, color, stack);
        }
        if (cards.Count == 0)
        {
            return Fail("Aucune carte proposée.", top, color, stack);
        }
        foreach (UnoCard card in cards)
        {
            int inPlay = cards.Count(c => c == card);
            int inHand = _hand.Count(c => c == card);
            if (inPlay > inHand)
            {
                return Fail($"« {card.ToFrench()} » n'est pas en main. " +
                    $"Main : {DescribeHand()}.", top, color, stack);
            }
        }
        bool topIsDraw = top?.IsDraw == true;
        int effective = stack > 0 ? stack : (topIsDraw ? top!.DrawValue : 0);
        if (effective > 0)
        {
            return PlayStack(cards, top!, effective, color);
        }
        if (cards.Count == 1)
        {
            return PlaySingle(cards[0], top, color);
        }
        return PlayCombo(cards, top);
    }

    private UnoPlayResult PlayStack(IReadOnlyList<UnoCard> cards, UnoCard top, int effective, UnoColor? color)
    {
        if (!top.IsDraw)
        {
            return Fail($"Cumul de {effective} annoncé mais le dessus « {top.ToFrench()} » " +
                "n'est pas un +1/+2 : pioche avec uno_draw.", top, color, effective);
        }
        if (cards.Count != 1 || cards[0].Rank != top.Rank)
        {
            return Fail($"Dessus : « {top.ToFrench()} » ({effective} à piocher). Tu dois cumuler " +
                $"un {(top.Rank == UnoRank.Plus1 ? "+1 (n'importe quelle couleur)" : "+2")} " +
                $"ou piocher {effective} avec uno_draw.", top, color, effective);
        }
        UnoCard played = cards[0];
        _hand.Remove(played);
        int total = effective + played.DrawValue;
        UnoColor? shown = played.Color == UnoColor.Wild ? color ?? BestColor() : null;
        string note = played.Color == UnoColor.Wild && color is null
            ? $" Je choisis {UnoCard.ColorToFrench(shown!.Value)}."
            : "";
        return Done($"Tu cumules « {played.ToFrench()} » ! Total à piocher pour l'enfant : {total}." + note,
            played, shown, total, replay: false);
    }

    private UnoPlayResult PlaySingle(UnoCard card, UnoCard? top, UnoColor? color)
    {
        string? refusal = RefuseSingle(card, top, color);
        if (refusal is not null)
        {
            return Fail(refusal, top, color, 0);
        }
        _hand.Remove(card);
        UnoColor? shown = null;
        string note = "";
        if (card.IsWild)
        {
            shown = color ?? BestColor();
            note = $" Tu choisis {UnoCard.ColorToFrench(shown.Value)}.";
        }
        bool replay = card.Rank is UnoRank.Skip or UnoRank.Reverse;
        if (replay)
        {
            note += " L'enfant passe son tour : tu rejoues aussitôt (rappelle uno_play_cards avec dessus = ta carte).";
        }
        return Done($"Tu poses « {card.ToFrench()} »." + note, card, shown, 0, replay);
    }

    private UnoPlayResult PlayCombo(IReadOnlyList<UnoCard> cards, UnoCard? top)
    {
        UnoRank rank = cards[0].Rank;
        if (cards.Any(c => c.Rank != rank) || !cards[0].Comboable)
        {
            return Fail("Combo refusé : 2+ cartes de même chiffre uniquement " +
                "(les spéciales se jouent une par une).", top, null, 0);
        }
        string? refusal = RefuseSingle(cards[0], top, null);
        if (refusal is not null)
        {
            return Fail("Combo refusé : " + refusal, top, null, 0);
        }
        foreach (UnoCard card in cards)
        {
            _hand.Remove(card);
        }
        UnoCard last = cards[cards.Count - 1];
        string names = string.Join(" puis ", cards.Select(c => $"« {c.ToFrench()} »"));
        return Done($"Tu poses {names} ! Le dessus est « {last.ToFrench()} ».",
            last, null, 0, replay: false);
    }

    private static string? RefuseSingle(UnoCard card, UnoCard? top, UnoColor? color)
    {
        if (top is null)
        {
            return null; // Premier coup (le robot commence) : carte libre.
        }
        if (card.IsWild)
        {
            return null; // Joker : toujours jouable.
        }
        if (top.IsWild)
        {
            if (color is null)
            {
                return "Le dessus est un joker : demande à l'enfant quelle couleur il choisit, " +
                    "puis rejoue avec color.";
            }
            return card.Color == color
                ? null
                : $"« {card.ToFrench()} » ne va pas sur joker {UnoCard.ColorToFrench(color.Value)}.";
        }
        if (card.Color == top.Color || card.Rank == top.Rank)
        {
            return null;
        }
        return $"« {card.ToFrench()} » ne va pas sur « {top.ToFrench()} » " +
            "(il faut la même couleur ou le même symbole).";
    }

    private UnoPlayResult Done(string head, UnoCard top, UnoColor? shown, int stack, bool replay)
    {
        bool uno = _hand.Count == 1;
        bool won = _hand.Count == 0;
        string tail = won
            ? " GAGNÉ, tu n'as plus de cartes !"
            : uno
                ? $" UNO ! Il te reste : {DescribeHand()}."
                : $" Il te reste {_hand.Count} cartes : {DescribeHand()}.";
        return new UnoPlayResult(true, head + tail, top, shown, stack, replay, uno, won, Hand.ToList());
    }

    private UnoPlayResult Fail(string reason, UnoCard? top, UnoColor? color, int stack) =>
        new(false, "Coup refusé : " + reason + " Rejoue autre chose, ou pioche avec uno_draw.",
            top, color, stack, false, false, false, Hand.ToList());

    private string DescribeHand() =>
        _hand.Count == 0
            ? "(vide)"
            : string.Join(", ", _hand.Select(c => $"« {c.ToFrench()} »"));

    /// <summary>Couleur la plus tenue en main (choix de joker par défaut).</summary>
    private UnoColor BestColor()
    {
        UnoColor best = UnoColor.Red;
        int bestCount = -1;
        foreach (UnoColor color in new[] { UnoColor.Red, UnoColor.Yellow, UnoColor.Green, UnoColor.Blue })
        {
            int count = _hand.Count(c => c.Color == color);
            if (count > bestCount)
            {
                best = color;
                bestCount = count;
            }
        }
        return best;
    }
}

/// <summary>
/// Anti-rafale des outils Uno : le modèle Live émet parfois le même appel
/// 2-3 fois en une seconde (IDs différents, dédup par ID inefficace). Fenêtre
/// courte : les vrais tours sont espacés par la voix (plusieurs secondes).
/// Les coups refusés n'arment pas (le retry avec correction doit passer) ;
/// un coup avec rejouée (passe/inverser) autorise le coup chaîné.
/// </summary>
internal sealed class UnoRepeatGuard
{
    internal static readonly TimeSpan Window = TimeSpan.FromSeconds(5);

    private DateTime _lastDrawAt = DateTime.MinValue;
    private string _lastDrawMessage = "";
    private DateTime _lastPlayAt = DateTime.MinValue;
    private string _lastPlayTop = "";
    private bool _lastPlayReplay;
    private DateTime _lastNewGameAt = DateTime.MinValue;
    private string _lastNewGameMessage = "";

    /// <summary>Pioche rejouée à l'identique si doublon, sinon null.</summary>
    public string? CheckDraw(DateTime now) =>
        now - _lastDrawAt < Window && _lastDrawMessage.Length > 0 ? _lastDrawMessage : null;

    public void NoteDraw(DateTime now, string message)
    {
        _lastDrawAt = now;
        _lastDrawMessage = message;
    }

    /// <summary>Blocage "un seul coup par tour" si doublon, sinon null.</summary>
    public string? CheckPlay(DateTime now) =>
        now - _lastPlayAt < Window && !_lastPlayReplay && _lastPlayTop.Length > 0
            ? $"Un seul coup par tour : tu as déjà joué « {_lastPlayTop} ». Annonce-le et attends le prochain tour."
            : null;

    public void NotePlay(DateTime now, string topFrench, bool replay)
    {
        _lastPlayAt = now;
        _lastPlayTop = topFrench;
        _lastPlayReplay = replay;
    }

    /// <summary>Main déjà distribuée si re-donne en rafale, sinon null.</summary>
    public string? CheckNewGame(DateTime now) =>
        now - _lastNewGameAt < Window && _lastNewGameMessage.Length > 0 ? _lastNewGameMessage : null;

    public void NoteNewGame(DateTime now, string message)
    {
        _lastNewGameAt = now;
        _lastNewGameMessage = message;
    }
}
