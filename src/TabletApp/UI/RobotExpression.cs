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
}

/// <summary>
/// Pilote l'expression courante (changement instantané, sans transition).
/// </summary>
internal sealed class ExpressionController
{
    public RobotExpression Current { get; private set; } = RobotExpression.Neutral;

    public void SetExpression(RobotExpression expression)
    {
        Current = expression;
    }

    public void SetEmotion(string emotion)
    {
        if (TryParse(emotion, out RobotExpression expression))
        {
            SetExpression(expression);
        }
        // Émotion inconnue : ignorée (robustesse face aux typos du LLM).
    }

    private static bool TryParse(string emotion, out RobotExpression expression)
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
