namespace TabletApp.UI;

/// <summary>
/// Expressions du visage (style EVE : yeux lumineux sans pupilles + bouche).
/// Noms anglais = contrat du function calling <c>set_emotion(emotion)</c> (issue #5).
/// </summary>
internal enum RobotExpression
{
    Neutral,
    Dead,
    Mean,
    Dizzy,
    Happy,
    Laugh,
    Love,
    Wink,
    Sleep,
    Tired,
    Thinking,
    Shocked,
    /// <summary>Dialogue de confirmation tactile (X = annuler, ✓ = confirmer).</summary>
    Confirm,
}

/// <summary>
/// Pilote l'expression courante + transition par squash-swap-stretch
/// (chaque partie s'aplatit, bascule, se redéploie — voir FacePainter).
/// Logique pure (temps injecté) pour être testable sans UI.
/// </summary>
internal sealed class ExpressionController
{
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(300);

    private TimeSpan _transitionStartedAt;

    public RobotExpression Current { get; private set; } = RobotExpression.Neutral;
    public RobotExpression Previous { get; private set; } = RobotExpression.Neutral;

    /// <summary>Progression 0..1 (1 = repos, aucune transition).</summary>
    public double Progress { get; private set; } = 1.0;

    public bool Transitioning => Progress < 1.0;

    public void SetExpression(RobotExpression expression, TimeSpan now)
    {
        if (expression == Current)
        {
            return;
        }
        Previous = Current;
        Current = expression;
        _transitionStartedAt = now;
        Progress = 0.0;
    }

    public void SetEmotion(string emotion, TimeSpan now)
    {
        if (TryParse(emotion, out RobotExpression expression))
        {
            SetExpression(expression, now);
        }
        // Émotion inconnue : ignorée (robustesse face aux typos du LLM).
    }

    public void Update(TimeSpan now)
    {
        if (Progress >= 1.0)
        {
            return;
        }
        Progress = Math.Min(1.0,
            (now - _transitionStartedAt).TotalMilliseconds / TransitionDuration.TotalMilliseconds);
    }

    internal static bool TryParse(string emotion, out RobotExpression expression)
    {
        RobotExpression? parsed = emotion.Trim().ToLowerInvariant() switch
        {
            "neutral" => RobotExpression.Neutral,
            "dead" => RobotExpression.Dead,
            "mean" or "angry" or "evil" => RobotExpression.Mean,
            "dizzy" => RobotExpression.Dizzy,
            "happy" or "joy" or "joyful" => RobotExpression.Happy,
            "laugh" or "laughing" => RobotExpression.Laugh,
            "love" => RobotExpression.Love,
            "wink" => RobotExpression.Wink,
            "sleep" or "asleep" => RobotExpression.Sleep,
            "tired" or "sleepy" or "bored" => RobotExpression.Tired,
            "thinking" or "think" => RobotExpression.Thinking,
            "shocked" or "surprised" => RobotExpression.Shocked,
            "confirm" or "question" => RobotExpression.Confirm,
            _ => null,
        };
        if (parsed is null)
        {
            expression = RobotExpression.Neutral;
            return false;
        }
        expression = parsed.Value;
        return true;
    }
}
