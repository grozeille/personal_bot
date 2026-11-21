using Reqnroll;
using TabletApp.AI;
using TabletApp.UI;
using Xunit;

namespace TabletApp.Tests.Steps;

[Binding]
public sealed class ConversationSteps
{
    private readonly List<(string? Heard, string? Reply, string? Emotion, string? Sound)> _script = new();
    private readonly List<string> _sounds = new();
    private readonly List<string> _spoken = new();
    private readonly List<RobotExpression> _faces = new();
    private readonly List<int> _historySizes = new();

    [Given("une discussion avec :")]
    public void GivenScript(Table table)
    {
        foreach (var row in table.Rows)
        {
            _script.Add((
                EmptyToNull(row["entendu"]),
                EmptyToNull(row["réplique"]),
                EmptyToNull(row["émotion"]),
                row.TryGetValue("son", out string? son) ? EmptyToNull(son) : null));        }
    }

    [When("la discussion se déroule")]
    public async Task WhenDiscute()
    {
        var heard = new Queue<string?>(_script.Select(s => s.Heard));
        var map = _script
            .Where(s => s.Heard is not null)
            .ToDictionary(s => s.Heard!, s => (s.Reply, s.Emotion, s.Sound));
        var chat = new Conversation(
            (_, _) => Task.FromResult(heard.Count > 0 ? heard.Dequeue() : null),
            (history, h, _) =>
            {
                _historySizes.Add(history.Count);
                var (reply, emotion, sound) = map[h];
                return Task.FromResult((reply, emotion, sound));
            },
            (text, _) =>
            {
                _spoken.Add(text);
                return Task.CompletedTask;
            },
            expr => _faces.Add(expr),
            listenFor: TimeSpan.FromMilliseconds(20),
            playSound: (sound, _) =>
            {
                _sounds.Add(sound);
                return Task.CompletedTask;
            });
        await chat.RunAsync(RobotExpression.Neutral, CancellationToken.None);
    }

    [Then("le robot salue, répond et dit au revoir")]
    public void ThenParoles()
    {
        var expected = new List<string> { Conversation.Greeting };
        expected.AddRange(_script
            .Where(s => s.Heard is not null && s.Reply is not null)
            .Select(s => s.Reply!));
        expected.Add(Conversation.Goodbye);
        Assert.Equal(expected, _spoken);
    }

    [Then("les visages suivent le script")]
    public void ThenVisages()
    {
        var expected = new List<RobotExpression>();
        foreach (string? emotion in _script
            .Where(s => s.Heard is not null)
            .Select(s => s.Emotion))
        {
            expected.Add(
                emotion is not null && ExpressionController.TryParse(emotion, out RobotExpression parsed)
                    ? parsed
                    : RobotExpression.Neutral);
        }
        expected.Add(RobotExpression.Neutral);
        Assert.Equal(expected, _faces);
        Assert.Equal(Enumerable.Range(0, expected.Count - 1), _historySizes);
    }

    [Then("les sons suivent le script")]
    public void ThenSons()
    {
        var expected = _script
            .Where(s => s.Heard is not null && s.Sound is not null)
            .Select(s => s.Sound!);
        Assert.Equal(expected, _sounds);
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
