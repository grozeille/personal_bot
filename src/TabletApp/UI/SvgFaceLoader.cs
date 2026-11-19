using System.Reflection;
using System.Text;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace TabletApp.UI;

/// <summary>
/// Charge les visages SVG embarqués (docs/faces, un par émotion) et les
/// découpe en 2 calques (groupes id="eyes*"/id="mouth*") mis en cache pour le
/// rendu 60 FPS. Mesure aussi les pivots (centre vertical des yeux/bouche)
/// depuis le rendu réel, pour survivre aux retouches de mise en page.
/// Échec rapide et explicite si un fichier ou un groupe manque.
/// Les SKSvg sont conservés en cache avec leurs SKPicture (durée de vie = app).
/// </summary>
internal static class SvgFaceLoader
{
    // Noms de fichiers (autre LLM) -> nos émotions.
    private static readonly Dictionary<RobotExpression, string> Files = new()
    {
        [RobotExpression.Neutral] = "01-happy.svg",
        [RobotExpression.Dead] = "02-dead.svg",
        [RobotExpression.Mean] = "03-angry.svg",
        [RobotExpression.Dizzy] = "04-excited.svg",
        [RobotExpression.Happy] = "05-surprised.svg",
        [RobotExpression.Tired] = "06-bored.svg",
        [RobotExpression.Love] = "07-love.svg",
        [RobotExpression.Laugh] = "08-joyful.svg",
        [RobotExpression.Wink] = "09-wink.svg",
        [RobotExpression.Sleep] = "10-sleepy.svg",
        [RobotExpression.Thinking] = "11-thinking.svg",
        [RobotExpression.Shocked] = "12-shocked.svg",
    };

    internal sealed class FaceLayers
    {
        public required SKSvg EyesSvg;
        public required SKSvg MouthSvg;
        public required float EyePivotY;
        public required float MouthPivotY;
        public SKPicture Eyes => EyesSvg.Picture!;
        public SKPicture Mouth => MouthSvg.Picture!;
    }

    private static readonly Dictionary<RobotExpression, FaceLayers> Cache = new();

    public static FaceLayers For(RobotExpression expression)
    {
        if (!Cache.TryGetValue(expression, out FaceLayers? layers))
        {
            layers = Load(expression);
            Cache[expression] = layers;
        }
        return layers;
    }

    private static FaceLayers Load(RobotExpression expression)
    {
        string file = Files[expression];
        string resource = "TabletApp.Faces." + file;
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException(
                $"Ressource SVG introuvable : {resource} (docs/faces/{file} non embarquée ?).");
        var doc = XDocument.Load(stream);
        GuardGroups(doc, file);
        var eyes = LoadGroups(doc, "eyes", file);
        var mouth = LoadGroups(doc, "mouth", file);
        float eyeY = CentroidY(eyes.Picture!, file, "eyes");
        float mouthY = CentroidY(mouth.Picture!, file, "mouth");
        if (eyeY is < 150 or > 400 || mouthY is < 250 or > 500 || mouthY <= eyeY)
        {
            throw new InvalidDataException(
                $"Géométrie inattendue dans {file} (yeux y={eyeY:F0}, bouche y={mouthY:F0}).");
        }
        return new FaceLayers
        {
            EyesSvg = eyes,
            MouthSvg = mouth,
            EyePivotY = eyeY,
            MouthPivotY = mouthY,
        };
    }

    private static void GuardGroups(XDocument doc, string file)
    {
        foreach (XElement child in doc.Root?.Elements() ?? Enumerable.Empty<XElement>())
        {
            if (child.Name.LocalName != "g")
            {
                continue;
            }
            string? id = (string?)child.Attribute("id");
            if (!IsLayer(id, "eyes") && !IsLayer(id, "mouth"))
            {
                throw new InvalidDataException(
                    $"Groupe inattendu id=\"{id}\" dans {file} (attendu : eyes[-...], mouth[-...]).");
            }
        }
    }

    private static bool IsLayer(string? id, string prefix) =>
        id is not null && (id == prefix || id.StartsWith(prefix + "-", StringComparison.Ordinal));

    private static SKSvg LoadGroups(XDocument doc, string prefix, string file)
    {
        var groups = doc.Descendants()
            .Where(e => e.Name.LocalName == "g" && IsLayer((string?)e.Attribute("id"), prefix))
            .Select(g => new XElement(g))
            .ToList();
        if (groups.Count == 0)
        {
            throw new InvalidDataException($"Aucun groupe id=\"{prefix}[*]\" dans {file}.");
        }
        if (!groups.SelectMany(g => g.Descendants()).Any())
        {
            throw new InvalidDataException($"Groupes id=\"{prefix}[*]\" vides dans {file}.");
        }
        XNamespace ns = "http://www.w3.org/2000/svg";
        var split = new XDocument(new XElement(ns + "svg",
            new XAttribute("viewBox", "0 0 800 550"),
            groups));
        var svg = new SKSvg();
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(split.ToString()));
        svg.Load(ms);
        if (svg.Picture is null)
        {
            throw new InvalidDataException($"Rendu SVG impossible pour {file}#{prefix}.");
        }
        return svg;
    }

    /// <summary>
    /// Centroïde vertical du contenu lumineux, en unités viewBox (800x550).
    /// </summary>
    private static float CentroidY(SKPicture picture, string file, string layer)
    {
        const float scale = 0.25f;
        using var bitmap = new SKBitmap((int)(800 * scale), (int)(550 * scale));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        double sumY = 0;
        int n = 0;
        for (int x = 0; x < bitmap.Width; x++)
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                SKColor p = bitmap.GetPixel(x, y);
                if ((p.Red + p.Green + p.Blue) / 3 < 40)
                {
                    continue;
                }
                sumY += y;
                n++;
            }
        }
        if (n == 0)
        {
            throw new InvalidDataException($"Calque {layer} vide dans {file}.");
        }
        return (float)(sumY / n / scale);
    }
}
