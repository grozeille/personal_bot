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
}

/// <summary>
/// Pilote l'expression courante. Transitions désactivées pour l'instant :
/// changement instantané (remettre <c>TransitionsEnabled</c> à true pour le pop).
/// Logique pure (temps injecté) pour être testable sans UI.
/// </summary>
internal sealed class ExpressionController
{
    private static readonly bool TransitionsEnabled = false;
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(300);

    private TimeSpan _transitionStartedAt;
    private bool _transitioning;

    public RobotExpression Current { get; private set; } = RobotExpression.Neutral;

    /// <summary>1 = taille normale, creuse pendant la transition (effet de "pop").</summary>
    public double PopScale { get; private set; } = 1.0;

    public bool Transitioning => _transitioning;

    public void SetExpression(RobotExpression expression, TimeSpan now)
    {
        Current = expression;
        _transitionStartedAt = now;
        _transitioning = TransitionsEnabled;
        PopScale = 1.0;
        if (_transitioning)
        {
            Update(now);
        }
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
        if (!_transitioning || !TransitionsEnabled)
        {
            _transitioning = false;
            PopScale = 1.0;
            return;
        }
        double p = Math.Min(1.0,
            (now - _transitionStartedAt).TotalMilliseconds / TransitionDuration.TotalMilliseconds);
        PopScale = 1.0 - 0.45 * Math.Sin(Math.PI * p);
        if (p >= 1)
        {
            _transitioning = false;
            PopScale = 1.0;
        }
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
