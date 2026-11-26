using System.Globalization;
using System.Text;

namespace TabletApp.AI;

/// <summary>
/// Mémoire durable du robot : journal quotidien des tours (entendu + dit)
/// et carnet MEMORY.md consolidé par le LLM en fin de discussion.
/// Local uniquement (%LOCALAPPDATA%/TabletApp/memory).
/// </summary>
internal sealed class MemoryStore
{
    public static string DefaultDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TabletApp", "memory");

    private readonly string _dir;
    private readonly object _sync = new();

    public MemoryStore(string? dir = null, int keepDays = 30)
    {
        _dir = dir ?? DefaultDir;
        KeepDays = keepDays;
        Directory.CreateDirectory(_dir);
        Prune();
    }

    public int KeepDays { get; }

    public void AppendHeard(string text) => Append("Enfant", text);

    public void AppendRobot(string text) => Append("Patate", text);

    private void Append(string who, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }
        string line = $"- [{DateTime.Now:HH:mm:ss}] {who} : {OneLine(text)}{Environment.NewLine}";
        lock (_sync)
        {
            File.AppendAllText(JournalPath(DateTime.Today), line);
        }
    }

    /// <summary>Derniers tours (hier + aujourd'hui), plafonnés.</summary>
    public string ReadRecent(int maxChars = 4000)
    {
        var sb = new StringBuilder();
        foreach (DateTime day in new[] { DateTime.Today.AddDays(-1), DateTime.Today })
        {
            string path = JournalPath(day);
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                sb.AppendLine($"### {day:yyyy-MM-dd}");
                sb.Append(File.ReadAllText(path));
            }
            catch
            {
            }
        }
        string all = sb.ToString().Trim();
        return all.Length <= maxChars ? all : all.Substring(all.Length - maxChars);
    }

    public string ReadMemory()
    {
        try
        {
            string path = Path.Combine(_dir, "MEMORY.md");
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }
        catch
        {
            return "";
        }
    }

    public void SaveMemory(string text)
    {
        try
        {
            File.WriteAllText(Path.Combine(_dir, "MEMORY.md"), text.Trim() + Environment.NewLine);
        }
        catch
        {
        }
    }

    /// <summary>Bloc mémoire pour le prompt (vide si rien à dire).</summary>
    public string BuildBlock()
    {
        string memory = ReadMemory();
        string recent = ReadRecent();
        if (memory.Length == 0 && recent.Length == 0)
        {
            return "";
        }
        var sb = new StringBuilder(" Voici ce que tu sais déjà (mémoire locale, ne la relis jamais à voix haute) : ");
        if (memory.Length > 0)
        {
            sb.Append("Carnet : ").Append(memory).Append(' ');
        }
        if (recent.Length > 0)
        {
            sb.Append("Échanges récents : ").Append(recent);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Consolide : journal → carnet via le LLM (null si rien à faire).
    /// Le résumé est injecté (réel en prod, factice en tests).
    /// </summary>
    public static async Task<string?> ConsolidateAsync(
        MemoryStore store,
        Func<string, string, CancellationToken, Task<string>> summarize,
        CancellationToken ct)
    {
        string journal = store.ReadRecent(8000);
        if (journal.Length == 0)
        {
            return null;
        }
        string updated = (await summarize(store.ReadMemory(), journal, ct).ConfigureAwait(false)).Trim();
        if (updated.Length == 0)
        {
            return null;
        }
        store.SaveMemory(updated.Length <= 4000 ? updated : updated.Substring(0, 4000));
        return updated;
    }

    private string JournalPath(DateTime day) => Path.Combine(_dir, $"journal-{day:yyyy-MM-dd}.md");

    private void Prune()
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(_dir, "journal-*.md");
        }
        catch
        {
            return;
        }
        foreach (string file in files)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.Length > "journal-".Length
                    && DateTime.TryParseExact(name.Substring("journal-".Length), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day)
                    && (DateTime.Today - day).TotalDays > KeepDays)
                {
                    File.Delete(file);
                }
            }
            catch
            {
            }
        }
    }

    private static string OneLine(string text) =>
        text.Replace("\r", " ").Replace("\n", " ").Trim();
}
