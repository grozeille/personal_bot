using TabletApp.UI;

namespace TabletApp.Gestures;

/// <summary>
/// Test bout-en-bout : geste (tete TCP) + expression (visage), comme le fera le LLM.
/// </summary>
internal static class HeadTest
{
    public static async Task<string> PlayAsync(RobotFaceRenderer face, string gestureName)
    {
        string path = GestureLibrary.ResolvePath();
        GestureLibrary lib = GestureLibrary.Load(path);
        if (!lib.TryGet(gestureName, out GestureLibrary.Gesture gesture))
        {
            return $"geste inconnu: {gestureName} ({lib.Count} gestes charges)";
        }
        if (ExpressionController.TryParse(gesture.Expression, out RobotExpression expression))
        {
            face.SetExpression(expression);
        }
        try
        {
            var sender = new HeadTcpClient(HeadTcpClient.Host, HeadTcpClient.Port);
            string reply = await sender.SendMovesAsync(gesture.MovesJson).ConfigureAwait(false);
            return $"geste {gestureName} -> {reply}";
        }
        catch (Exception ex)
        {
            return $"geste {gestureName} : visage OK, tete injoignable ({ex.Message})";
        }
    }
}
