namespace TabletApp.AI;

/// <summary>
/// Journal voix : console parente (dotnet run) + %TEMP%\tabletapp-voice.log.
/// N'y faire transiter que des textes et erreurs — jamais la clé API.
/// </summary>
internal static class VoiceLog
{
    private static readonly object Lock = new();
    private static readonly string Path = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "tabletapp-voice.log");

    public static void Info(string message) => Write(message);

    public static void Error(string message) => Write("ERREUR : " + message);

    private static void Write(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (Lock)
        {
            Console.WriteLine(line);
            try
            {
                File.AppendAllText(Path, line + Environment.NewLine);
            }
            catch
            {
                // Journal indisponible : ne jamais faire échouer la voix.
            }
        }
    }
}
