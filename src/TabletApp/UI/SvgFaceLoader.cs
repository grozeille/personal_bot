using System.Reflection;
using System.Text;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace TabletApp.UI;

/// <summary>
/// Charge la bibliothèque de parties SVG embarquées
/// (<c>docs/faces/parts</c> : œil gauche, œil droit, bouche — voir
/// <c>docs/faces/README.md</c>) et compose chaque émotion à partir de ces
/// éléments réutilisables partagés. Deux visages utilisant la même partie
/// partagent exactement le même <see cref="SKPicture"/> : l'équivalence est
/// structurelle, la transition est sautée sans comparaison.
/// Mesure aussi les pivots (centre vertical de chaque partie) depuis le rendu.
/// Échec rapide et explicite si un fichier ou un groupe manque.
/// Les SKSvg sont conservés en cache avec leurs SKPicture (durée de vie = app).
/// </summary>
internal static class SvgFaceLoader
{
    internal enum EyeVariant
    {
        Oval,
        BigOval,
        X,
        XSmall,
        Check,
        Blade,
        Chevron,
        Dome,
        Heart,
        ArcUp,
        ArcDown,
        Round,
    }

    internal enum MouthVariant
    {
        Smile,
        Flat,
        OpenD,
        BigOpen,
        Slant,
        Ring,
        Dot,
        Ellipsis,
    }

    // Composition de chaque émotion : (œil gauche, œil droit, bouche).
    // Même variante = même fichier = pixels identiques, transition sautée.
    private static readonly Dictionary<RobotExpression, (EyeVariant Left, EyeVariant Right, MouthVariant Mouth)> Composition = new()
    {
        [RobotExpression.Neutral] = (EyeVariant.Oval, EyeVariant.Oval, MouthVariant.Smile),
        [RobotExpression.Dead] = (EyeVariant.X, EyeVariant.X, MouthVariant.Flat),
        [RobotExpression.Mean] = (EyeVariant.Blade, EyeVariant.Blade, MouthVariant.Smile),
        [RobotExpression.Dizzy] = (EyeVariant.Chevron, EyeVariant.Chevron, MouthVariant.Smile),
        [RobotExpression.Happy] = (EyeVariant.Oval, EyeVariant.Oval, MouthVariant.OpenD),
        [RobotExpression.Tired] = (EyeVariant.Dome, EyeVariant.Dome, MouthVariant.Flat),
        [RobotExpression.Love] = (EyeVariant.Heart, EyeVariant.Heart, MouthVariant.Smile),
        [RobotExpression.Laugh] = (EyeVariant.ArcUp, EyeVariant.ArcUp, MouthVariant.BigOpen),
        [RobotExpression.Wink] = (EyeVariant.Oval, EyeVariant.Chevron, MouthVariant.Smile),
        [RobotExpression.Sleep] = (EyeVariant.ArcDown, EyeVariant.ArcDown, MouthVariant.Flat),
        [RobotExpression.Thinking] = (EyeVariant.Oval, EyeVariant.Oval, MouthVariant.Slant),
        [RobotExpression.Shocked] = (EyeVariant.BigOval, EyeVariant.BigOval, MouthVariant.Ring),
        [RobotExpression.Confirm] = (EyeVariant.XSmall, EyeVariant.Check, MouthVariant.Dot),
        [RobotExpression.Busy] = (EyeVariant.Round, EyeVariant.Round, MouthVariant.Ellipsis),
    };

    private static string EyeFile(string side, EyeVariant variant) => variant switch
    {
        EyeVariant.Oval => $"eye-{side}-oval.svg",
        EyeVariant.BigOval => $"eye-{side}-big-oval.svg",
        EyeVariant.X => $"eye-{side}-x.svg",
        EyeVariant.XSmall => $"eye-{side}-x-small.svg",
        EyeVariant.Check => $"eye-{side}-check.svg",
        EyeVariant.Blade => $"eye-{side}-blade.svg",
        EyeVariant.Chevron => $"eye-{side}-chevron.svg",
        EyeVariant.Dome => $"eye-{side}-dome.svg",
        EyeVariant.Heart => $"eye-{side}-heart.svg",
        EyeVariant.ArcUp => $"eye-{side}-arc-up.svg",
        EyeVariant.ArcDown => $"eye-{side}-arc-down.svg",
        EyeVariant.Round => $"eye-{side}-round.svg",
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    private static string MouthFile(MouthVariant variant) => variant switch
    {
        MouthVariant.Smile => "mouth-smile.svg",
        MouthVariant.Flat => "mouth-flat.svg",
        MouthVariant.OpenD => "mouth-open-d.svg",
        MouthVariant.BigOpen => "mouth-big-open.svg",
        MouthVariant.Slant => "mouth-slant.svg",
        MouthVariant.Ring => "mouth-ring.svg",
        MouthVariant.Dot => "mouth-dot.svg",
        MouthVariant.Ellipsis => "mouth-ellipsis-3.svg",
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    internal sealed class PartLayer
    {
        public required SKSvg Svg;
        public required float PivotY;
        public SKPicture Picture => Svg.Picture!;
    }

    internal sealed class FaceLayers
    {
        public required PartLayer Left;
        public required PartLayer Right;
        public required PartLayer MouthPart;
        public SKPicture EyesLeft => Left.Picture;
        public SKPicture EyesRight => Right.Picture;
        public SKPicture Mouth => MouthPart.Picture;
        public float LeftPivotY => Left.PivotY;
        public float RightPivotY => Right.PivotY;
        public float MouthPivotY => MouthPart.PivotY;
    }

    private static readonly Dictionary<RobotExpression, FaceLayers> Cache = new();
    private static readonly Dictionary<string, PartLayer> Parts = new();
    private static readonly Dictionary<(FacePart, RobotExpression), ulong> PartHashes = new();

    // Les captures BDD tournent en parallèle : l'initialisation paresseuse
    // des caches doit être verrouillée (l'app live est mono-thread UI).
    private static readonly object CacheLock = new();

    public static FaceLayers For(RobotExpression expression)
    {
        lock (CacheLock)
        {
            if (!Cache.TryGetValue(expression, out FaceLayers? layers))
            {
                layers = Load(expression);
                Cache[expression] = layers;
            }
            return layers;
        }
    }

    /// <summary>
    /// Phase de la bouche busy (1..5, 400 ms par cran, positions fixes) :
    /// [. ] [.. ] [...] [ ..] [  .], en boucle sur 2 s.
    /// </summary>
    internal static int BusyMouthPhase(double timeSeconds) =>
        1 + ((int)(timeSeconds / 0.4) % 5);

    /// <summary>
    /// Bouche busy à l'instant t. La phase 3 (... complet) est aussi la
    /// bouche canonique de la table (transitions entrantes/sortantes).
    /// </summary>
    internal static PartLayer BusyMouth(double timeSeconds)
    {
        lock (CacheLock)
        {
            return Part($"mouth-ellipsis-{BusyMouthPhase(timeSeconds)}.svg", "mouth");
        }
    }

    /// <summary>
    /// Vrai si la partie peut sauter la transition : même variante partagée
    /// (même fichier, pixels identiques par construction), vérifiée au pixel
    /// en garde-fou.
    /// </summary>
    internal static bool PartsMatch(FacePart part, RobotExpression a, RobotExpression b)
    {
        if (a == b)
        {
            return true;
        }
        if (!Equals(VariantOf(part, a), VariantOf(part, b)))
        {
            return false;
        }
        return PixelsMatch(part, a, b);
    }

    /// <summary>
    /// Paires partageant une variante, pour le self-test (qui vérifie que le
    /// partage tient vraiment au pixel).
    /// </summary>
    internal static IEnumerable<(FacePart Part, RobotExpression A, RobotExpression B)> DeclaredPairs()
    {
        foreach (FacePart part in new[] { FacePart.LeftEye, FacePart.RightEye, FacePart.Mouth })
        {
            var groups = Composition.Keys
                .GroupBy(e => VariantOf(part, e))
                .Where(g => g.Count() > 1);
            foreach (var group in groups)
            {
                RobotExpression[] shared = group.ToArray();
                for (int i = 1; i < shared.Length; i++)
                {
                    yield return (part, shared[0], shared[i]);
                }
            }
        }
    }

    internal static bool PixelsMatch(FacePart part, RobotExpression a, RobotExpression b) =>
        PartHash(part, a) == PartHash(part, b);

    internal static (float cx, float cy) PartCentroid(FacePart part, RobotExpression expression)
    {
        var face = For(expression);
        SKPicture picture = part switch
        {
            FacePart.LeftEye => face.EyesLeft,
            FacePart.RightEye => face.EyesRight,
            _ => face.Mouth,
        };
        using var bitmap = RenderSmall(picture);
        double sx = 0;
        double sy = 0;
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
                sx += x;
                sy += y;
                n++;
            }
        }
        if (n == 0)
        {
            return (0, 0);
        }
        return ((float)(sx / n / 0.25), (float)(sy / n / 0.25));
    }

    private static object VariantOf(FacePart part, RobotExpression expression)
    {
        var composition = Composition[expression];
        return part switch
        {
            FacePart.LeftEye => composition.Left,
            FacePart.RightEye => composition.Right,
            _ => composition.Mouth,
        };
    }

    private static ulong PartHash(FacePart part, RobotExpression expression)
    {
        var key = (part, expression);
        lock (CacheLock)
        {
            if (PartHashes.TryGetValue(key, out ulong cached))
            {
                return cached;
            }
        }
        var face = For(expression);
        SKPicture picture = part switch
        {
            FacePart.LeftEye => face.EyesLeft,
            FacePart.RightEye => face.EyesRight,
            _ => face.Mouth,
        };
        using var bitmap = RenderSmall(picture);
        ulong hash = 1469598103934665603ul;
        for (int x = 0; x < bitmap.Width; x++)
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                SKColor p = bitmap.GetPixel(x, y);
                hash ^= p.Red;
                hash *= 1099511628211ul;
                hash ^= p.Green;
                hash *= 1099511628211ul;
                hash ^= p.Blue;
                hash *= 1099511628211ul;
                hash ^= p.Alpha;
                hash *= 1099511628211ul;
            }
        }
        lock (CacheLock)
        {
            PartHashes[key] = hash;
        }
        return hash;
    }

    private static SKBitmap RenderSmall(SKPicture picture)
    {
        var bitmap = new SKBitmap(200, 138);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);
        canvas.Scale(0.25f);
        canvas.DrawPicture(picture);
        return bitmap;
    }

    private static FaceLayers Load(RobotExpression expression)
    {
        var composition = Composition[expression];
        PartLayer left = Part(EyeFile("left", composition.Left), "eyes");
        PartLayer right = Part(EyeFile("right", composition.Right), "eyes");
        PartLayer mouth = Part(MouthFile(composition.Mouth), "mouth");
        if (left.PivotY is < 150 or > 400 || right.PivotY is < 150 or > 400
            || mouth.PivotY is < 250 or > 500 || mouth.PivotY <= Math.Max(left.PivotY, right.PivotY))
        {
            throw new InvalidDataException(
                $"Géométrie inattendue pour {expression} " +
                $"(yeux y={left.PivotY:F0}/{right.PivotY:F0}, bouche y={mouth.PivotY:F0}).");
        }
        return new FaceLayers { Left = left, Right = right, MouthPart = mouth };
    }

    private static PartLayer Part(string file, string layer)
    {
        if (Parts.TryGetValue(file, out PartLayer? part))
        {
            return part;
        }
        string resource = "TabletApp.Faces.Parts." + file;
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException(
                $"Ressource SVG introuvable : {resource} (docs/faces/parts/{file} non embarquée ?).");
        var doc = XDocument.Load(stream);
        GuardGroups(doc, file);
        var svg = LoadGroups(doc, layer, file);
        float pivotY = CentroidY(svg.Picture!, file, layer);
        part = new PartLayer { Svg = svg, PivotY = pivotY };
        Parts[file] = part;
        return part;
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
