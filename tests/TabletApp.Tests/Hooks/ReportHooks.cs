using System.Text;
using Reqnroll;
using SkiaSharp;
using TabletApp.Tests.Support;
using TabletApp.UI;

namespace TabletApp.Tests.Hooks;

/// <summary>
/// Rapport de run : une capture PNG finale par scénario avec visage (rendue
/// offscreen via <c>FacePainter</c>) + <c>docs/rapport-tests/rapport.md</c>
/// consultable sur GitHub.
/// </summary>
[Binding]
public sealed class ReportHooks
{
    private static readonly List<ScenarioResult> Results = new();
    private static readonly Dictionary<string, int> Counters = new();
    private static string _reportDir = "";

    private sealed record ScenarioResult(
        string Feature, string Title, bool Passed, string? Error, string? Png);

    [BeforeTestRun]
    public static void BeforeTestRun()
    {
        _reportDir = Path.Combine(FindRepoRoot(), "docs", "rapport-tests");
        Directory.CreateDirectory(_reportDir);
        foreach (string png in Directory.GetFiles(_reportDir, "*.png"))
        {
            File.Delete(png);
        }
    }

    [AfterScenario]
    public static void AfterScenario(ScenarioContext context, FeatureContext featureContext)
    {
        string feature = featureContext.FeatureInfo.Title;
        string title = context.ScenarioInfo.Title;
        if (context.TryGetValue("emotion", out string? emotion) && !string.IsNullOrWhiteSpace(emotion))
        {
            title = $"{title} ({emotion})";
        }
        string? file = null;

        if (context.TryGetValue("face", out SimulatedFace? face) && face is not null)
        {
            // Suffixe par titre : les lignes d'un plan de scénario partagent le même titre.
            lock (Results)
            {
                string key = Sanitize($"{feature}_{title}");
                Counters.TryGetValue(key, out int n);
                Counters[key] = n + 1;
                file = $"{key}_{n + 1}.png";
            }
            using var bitmap = new SKBitmap(800, 600);
            using var canvas = new SKCanvas(bitmap);
            FacePainter.DrawFace(
                canvas, 800, 600, face.FinalOpen,
                face.Gaze.X, face.Gaze.Y, face.Sleep.Yawn, face.Now.TotalSeconds,
                face.Expression.Current, face.Expression.Previous, face.Expression.Progress,
                awake: face.Sleep.IsAwake);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(Path.Combine(_reportDir, file));
            data.SaveTo(stream);
        }

        bool passed = context.ScenarioExecutionStatus == ScenarioExecutionStatus.OK;
        lock (Results)
        {
            Results.Add(new ScenarioResult(
                feature, title, passed, context.TestError?.Message, file));
        }
    }

    [AfterTestRun]
    public static void AfterTestRun()
    {
        int ok = Results.Count(r => r.Passed);
        var md = new StringBuilder();
        md.AppendLine($"# Rapport de tests BDD — {DateTime.Now:yyyy-MM-dd HH:mm}");
        md.AppendLine();
        md.AppendLine($"*{Results.Count} scénarios : {ok} OK, {Results.Count - ok} KO.*");
        md.AppendLine();
        foreach (var r in Results.OrderBy(r => r.Feature).ThenBy(r => r.Title).ThenBy(r => r.Png))
        {
            md.AppendLine($"## {r.Feature} — {r.Title} — {(r.Passed ? "OK" : "KO")}");
            md.AppendLine();
            if (r.Error is not null)
            {
                md.AppendLine("```");
                md.AppendLine(r.Error);
                md.AppendLine("```");
                md.AppendLine();
            }
            if (r.Png is not null)
            {
                md.AppendLine($"![{r.Title}]({r.Png})");
                md.AppendLine();
            }
        }
        File.WriteAllText(Path.Combine(_reportDir, "rapport.md"), md.ToString());
    }

    private static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }
        return sb.ToString();
    }

    private static string FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        // Repli hors dépôt (ex. CI future) : rapport dans les résultats de test.
        string fallback = Path.Combine(Path.GetTempPath(), "tabletapp-report");
        Console.WriteLine($"[ReportHooks] Dépôt introuvable, rapport dans {fallback}");
        return fallback;
    }
}
