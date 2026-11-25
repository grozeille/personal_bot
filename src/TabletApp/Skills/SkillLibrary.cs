using System.Text;

namespace TabletApp.Skills;

/// <summary>Skill : règles nommées chargeables à la demande (tool load_skill).</summary>
internal sealed record Skill(string Slug, string Title, string Triggers, string Body);

/// <summary>
/// Bibliothèque de skills (Skills/*.md livrés + %LOCALAPPDATA%/TabletApp/skills
/// en surcharge). Format : ligne "# Titre", ligne "Triggers: a, b", puis règles.
/// Tolérant : titre = slug et déclencheurs vides si absents.
/// </summary>
internal sealed class SkillLibrary
{
    private readonly string _shippedDir;
    private readonly string _overrideDir;

    public SkillLibrary(string? shippedDir = null, string? overrideDir = null)
    {
        _shippedDir = shippedDir ?? Path.Combine(AppContext.BaseDirectory, "Skills");
        _overrideDir = overrideDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TabletApp", "skills");
    }

    /// <summary>Toutes les skills (surcharge locale prioritaire), triées par slug.</summary>
    public IReadOnlyList<Skill> List()
    {
        var merged = new Dictionary<string, Skill>(StringComparer.OrdinalIgnoreCase);
        foreach (string dir in new[] { _shippedDir, _overrideDir })
        {
            string[] files;
            try
            {
                files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.md") : Array.Empty<string>();
            }
            catch
            {
                continue;
            }
            foreach (string file in files)
            {
                try
                {
                    Skill skill = Parse(Path.GetFileNameWithoutExtension(file).ToLowerInvariant(),
                        File.ReadAllText(file));
                    merged[skill.Slug] = skill;
                }
                catch
                {
                    // Fichier illisible : ignoré, les autres restent.
                }
            }
        }
        return merged.Values.OrderBy(s => s.Slug, StringComparer.Ordinal).ToList();
    }

    public bool TryGet(string slug, out Skill skill)
    {
        foreach (Skill candidate in List())
        {
            if (string.Equals(candidate.Slug, slug?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                skill = candidate;
                return true;
            }
        }
        skill = null!;
        return false;
    }

    internal static Skill Parse(string slug, string markdown)
    {
        string title = slug;
        string triggers = "";
        var body = new StringBuilder();
        foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                title = line.Substring(2).Trim();
                if (title.Length == 0)
                {
                    title = slug;
                }
            }
            else if (line.StartsWith("Triggers:", StringComparison.OrdinalIgnoreCase))
            {
                triggers = line.Substring("Triggers:".Length).Trim();
            }
            else
            {
                body.AppendLine(raw.TrimEnd());
            }
        }
        return new Skill(slug, title, triggers, body.ToString().Trim());
    }
}
