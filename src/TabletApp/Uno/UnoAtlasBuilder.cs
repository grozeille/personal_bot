// Visuels Mattel (Uno Express) : usage personnel/familial uniquement,
// vérifier la licence avant toute redistribution (même régime que les
// samples prout). Sources : docs/uno-source/*.png (non embarquées).
using SkiaSharp;
using System.Text.Json;

namespace TabletApp.Uno;

/// <summary>
/// Fabrique l'atlas des faces Uno Express : découpe les planches sources
/// (grille uniforme détectée depuis la bbox du contenu), décline les
/// spéciales couleur par rotation de teinte (teintes cibles mesurées sur
/// les chiffres), normalise chaque face à 128×200 et écrit atlas.png +
/// atlas.json + contact.png (contrôle visuel). Échec explicite si la
/// grille est irrégulière.
/// </summary>
internal static class UnoAtlasBuilder
{
    internal const int CellW = 128;
    internal const int CellH = 200;
    internal const int AtlasCols = 8;

    internal static readonly string[] ColorNames = new[] { "red", "yellow", "green", "blue" };

    public static bool BuildAtlas(string sourceDir, string outDir, Action<string> log)
    {
        try
        {
            Directory.CreateDirectory(outDir);
            using var numbers = Decode(Path.Combine(sourceDir, "numbers.png"));
            using var specials = Decode(Path.Combine(sourceDir, "specials.png"));
            using var back = Decode(Path.Combine(sourceDir, "back.png"));
            var faces = new Dictionary<string, SKBitmap>();
            // Planche chiffres : 9 colonnes (1-9) × 4 lignes (vert, bleu, jaune, rouge).
            string[] rows = new[] { "green", "blue", "yellow", "red" };
            List<SKRectI> numCells = GridCells(numbers, 9, 4);
            log($"numbers.png : {numbers.Width}x{numbers.Height}, {numCells.Count} cellules.");
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 9; c++)
                {
                    faces[$"n{c + 1}_{rows[r]}"] = Normalize(Crop(numbers, numCells[r * 9 + c]));
                }
            }
            // Planche spéciales : joker, joker+2, +1 rouge / inverser jaune,
            // passe vert (ligne du bas décalée : zones explicites + bbox
            // serrée, les marqueurs sont exclus par le prédicat, case
            // bas-droite vide).
            log($"specials.png : {specials.Width}x{specials.Height}.");
            SKRectI Z(double x0, double y0, double x1, double y1) => new(
                (int)(specials.Width * x0), (int)(specials.Height * y0),
                (int)(specials.Width * x1), (int)(specials.Height * y1));
            using var wild = Normalize(Crop(specials, CardBox(specials, Z(0.11, 0.20, 0.35, 0.57), dark: true)));
            using var wildPlus2 = Normalize(Crop(specials, CardBox(specials, Z(0.35, 0.20, 0.60, 0.57), dark: true)));
            using var plus1Red = Normalize(Crop(specials, CardBox(specials, Z(0.59, 0.20, 0.83, 0.57), dark: false)));
            using var reverseYellow = Normalize(Crop(specials, CardBox(specials, Z(0.11, 0.57, 0.35, 0.95), dark: false)));
            using var skipGreen = Normalize(Crop(specials, CardBox(specials, Z(0.43, 0.57, 0.67, 0.95), dark: false)));
            faces["wild"] = CopyOf(wild);
            faces["wildplus2"] = CopyOf(wildPlus2);
            (double Hue, double Val)[] targets = new (double, double)[4];
            for (int i = 0; i < 4; i++)
            {
                targets[i] = (MedianHue(faces[$"n5_{ColorNames[i]}"]), MedianValue(faces[$"n5_{ColorNames[i]}"]));
            }
            log($"Cibles : rouge={targets[0].Hue:F0}/{targets[0].Val:F2} jaune={targets[1].Hue:F0}/{targets[1].Val:F2} " +
                $"vert={targets[2].Hue:F0}/{targets[2].Val:F2} bleu={targets[3].Hue:F0}/{targets[3].Val:F2}.");
            Decline(faces, "plus1", plus1Red, targets, 0);
            Decline(faces, "reverse", reverseYellow, targets, 1);
            Decline(faces, "skip", skipGreen, targets, 2);
            // Dos : carte unique centrée.
            faces["back"] = Normalize(Crop(back, ContentBox(back, dark: true)));
            // Atlas : 51 faces en grille de 8.
            string[] order = faces.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            int rowCount = (order.Length + AtlasCols - 1) / AtlasCols;
            using var atlas = new SKBitmap(AtlasCols * CellW, rowCount * CellH);
            using (var canvas = new SKCanvas(atlas))
            {
                canvas.Clear(SKColors.Black);
                for (int i = 0; i < order.Length; i++)
                {
                    int x = (i % AtlasCols) * CellW;
                    int y = (i / AtlasCols) * CellH;
                    using var face = faces[order[i]];
                    canvas.DrawBitmap(face, new SKRect(x, y, x + CellW, y + CellH), SKSamplingOptions.Default);
                }
            }
            var coords = new Dictionary<string, int[]>();
            for (int i = 0; i < order.Length; i++)
            {
                coords[order[i]] = new[] { (i % AtlasCols) * CellW, (i / AtlasCols) * CellH, CellW, CellH };
            }
            File.WriteAllText(
                Path.Combine(outDir, "atlas.json"),
                JsonSerializer.Serialize(coords, new JsonSerializerOptions { WriteIndented = true }));
            using (var image = SKImage.FromBitmap(atlas))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.OpenWrite(Path.Combine(outDir, "atlas.png")))
            {
                data.SaveTo(stream);
            }
            // Planche de contrôle : atlas + grille rouge.
            using var contact = atlas.Copy();
            using (var canvas = new SKCanvas(contact))
            using (var grid = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.Red })
            {
                for (int i = 0; i <= AtlasCols; i++)
                {
                    canvas.DrawLine(i * CellW, 0, i * CellW, contact.Height, grid);
                }
                for (int j = 0; j <= rowCount; j++)
                {
                    canvas.DrawLine(0, j * CellH, contact.Width, j * CellH, grid);
                }
            }
            using (var image = SKImage.FromBitmap(contact))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.OpenWrite(Path.Combine(outDir, "contact.png")))
            {
                data.SaveTo(stream);
            }
            log($"{order.Length} faces -> {outDir} (atlas.png + atlas.json + contact.png).");
            return true;
        }
        catch (Exception ex)
        {
            log($"Atlas impossible : {ex.Message}.");
            return false;
        }
    }

    /// <summary>
    /// Grille uniforme : bbox du contenu (fond quasi blanc) découpée en
    /// cols×rows égales, retrait de 4 % (marqueurs au rebut). Échoue si
    /// les dimensions sont incohérentes.
    /// </summary>
    internal static List<SKRectI> GridCells(SKBitmap sheet, int cols, int rows)
    {
        SKRectI box = ContentBox(sheet, dark: false);
        int w = box.Width / cols;
        int h = box.Height / rows;
        if (w < 20 || h < 20)
        {
            throw new InvalidDataException($"Grille {cols}x{rows} trop petite dans {box.Width}x{box.Height}.");
        }
        int insetX = Math.Max(4, w * 4 / 100);
        int insetY = Math.Max(4, h * 4 / 100);
        var cells = new List<SKRectI>(cols * rows);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                cells.Add(new SKRectI(
                    box.Left + c * w + insetX, box.Top + r * h + insetY,
                    box.Left + (c + 1) * w - insetX, box.Top + (r + 1) * h - insetY));
            }
        }
        return cells;
    }

    /// <summary>Bbox du contenu : pixels non blancs (dark=false) ou sombres (dark=true).</summary>
    internal static SKRectI ContentBox(SKBitmap bitmap, bool dark)
    {
        int left = bitmap.Width;
        int top = bitmap.Height;
        int right = 0;
        int bottom = 0;
        for (int y = 0; y < bitmap.Height; y += 2)
        {
            for (int x = 0; x < bitmap.Width; x += 2)
            {
                SKColor p = bitmap.GetPixel(x, y);
                bool content = dark
                    ? (p.Red + p.Green + p.Blue) / 3 < 128
                    : p.Red < 235 || p.Green < 235 || p.Blue < 235;
                if (content)
                {
                    if (x < left)
                    {
                        left = x;
                    }
                    if (y < top)
                    {
                        top = y;
                    }
                    if (x > right)
                    {
                        right = x;
                    }
                    if (y > bottom)
                    {
                        bottom = y;
                    }
                }
            }
        }
        if (right <= left || bottom <= top)
        {
            throw new InvalidDataException("Contenu introuvable (image blanche ?).");
        }
        return new SKRectI(left, top, right + 1, bottom + 1);
    }

    /// <summary>
    /// Carte dans une zone : plus longue série contiguë de tranches à fort
    /// contenu (sombres si dark, saturés sinon — le blanc inter-cartes et
    /// les marqueurs cassent la contiguïté), étendue au ratio 128:200
    /// centrée, clampée à la planche, retrait 2 px.
    /// </summary>
    internal static SKRectI CardBox(SKBitmap sheet, SKRectI zone, bool dark)
    {
        const int step = 8;
        int hSlices = Math.Max(1, (zone.Width + step - 1) / step);
        int vSlices = Math.Max(1, (zone.Height + step - 1) / step);
        double[] colFrac = new double[hSlices];
        double[] rowFrac = new double[vSlices];
        for (int s = 0; s < hSlices; s++)
        {
            int xa = zone.Left + s * step;
            int xb = Math.Min(zone.Right, xa + step);
            (int hit, int tot) = SampleRect(sheet, xa, zone.Top, xb, zone.Bottom, dark);
            colFrac[s] = tot == 0 ? 0 : (double)hit / tot;
        }
        for (int s = 0; s < vSlices; s++)
        {
            int ya = zone.Top + s * step;
            int yb = Math.Min(zone.Bottom, ya + step);
            (int hit, int tot) = SampleRect(sheet, zone.Left, ya, zone.Right, yb, dark);
            rowFrac[s] = tot == 0 ? 0 : (double)hit / tot;
        }
        (int cs, int cl) = LongestRun(colFrac);
        (int rs, int rl) = LongestRun(rowFrac);
        if (cl == 0 || rl == 0)
        {
            throw new InvalidDataException("Carte introuvable dans sa zone.");
        }
        int left = zone.Left + cs * step;
        int right = Math.Min(zone.Right, zone.Left + (cs + cl) * step);
        int top = zone.Top + rs * step;
        int bottom = Math.Min(zone.Bottom, zone.Top + (rs + rl) * step);
        if (right <= left || bottom <= top)
        {
            throw new InvalidDataException("Carte introuvable dans sa zone.");
        }
        double w = right - left + 1;
        double h = bottom - top + 1;
        double want = (double)CellW / CellH;
        if (w / h < want)
        {
            double dx = (h * want - w) / 2;
            left = (int)(left - dx);
            right = (int)(right + dx);
        }
        else
        {
            double dy = (w / want - h) / 2;
            top = (int)(top - dy);
            bottom = (int)(bottom + dy);
        }
        left = Math.Clamp(left + 2, 0, sheet.Width - 1);
        top = Math.Clamp(top + 2, 0, sheet.Height - 1);
        right = Math.Clamp(right - 2, 0, sheet.Width - 1);
        bottom = Math.Clamp(bottom - 2, 0, sheet.Height - 1);
        return new SKRectI(left, top, right + 1, bottom + 1);
    }

    private static (int Hit, int Total) SampleRect(
        SKBitmap sheet, int xa, int ya, int xb, int yb, bool dark)
    {
        int hit = 0;
        int tot = 0;
        for (int y = ya; y < yb; y += 4)
        {
            for (int x = xa; x < xb; x += 4)
            {
                SKColor p = sheet.GetPixel(x, y);
                tot++;
                if (dark
                    ? (p.Red + p.Green + p.Blue) / 3 < 128
                    : ToHsv(p.Red, p.Green, p.Blue).S > 0.25)
                {
                    hit++;
                }
            }
        }
        return (hit, tot);
    }

    /// <summary>Plus longue série d'indices à fraction > 0,25 (départ, longueur).</summary>
    private static (int Start, int Length) LongestRun(double[] frac)
    {
        int bestStart = 0;
        int bestLen = 0;
        int curStart = 0;
        int curLen = 0;
        for (int s = 0; s < frac.Length; s++)
        {
            if (frac[s] > 0.25)
            {
                if (curLen == 0)
                {
                    curStart = s;
                }
                curLen++;
            }
            else
            {
                if (curLen > bestLen)
                {
                    bestLen = curLen;
                    bestStart = curStart;
                }
                curLen = 0;
            }
        }
        if (curLen > bestLen)
        {
            bestLen = curLen;
            bestStart = curStart;
        }
        return (bestStart, bestLen);
    }

    private static void Decline(Dictionary<string, SKBitmap> faces, string kind, SKBitmap source,
        (double Hue, double Val)[] targets, int sourceColor)
    {
        string[] colors = ColorNames;
        faces[$"{kind}_{colors[sourceColor]}"] = CopyOf(source);
        double sourceHue = MedianHue(source);
        double sourceVal = MedianValue(source);
        for (int i = 0; i < 4; i++)
        {
            if (i != sourceColor)
            {
                double scale = Math.Clamp(targets[i].Val / sourceVal, 0.7, 1.4);
                faces[$"{kind}_{colors[i]}"] = ShiftHue(source, targets[i].Hue - sourceHue, scale);
            }
        }
    }

    /// <summary>Valeur médiane des pixels saturés (luminosité de la couleur).</summary>
    internal static double MedianValue(SKBitmap bitmap)
    {
        var values = new List<double>();
        for (int y = 0; y < bitmap.Height; y += 4)
        {
            for (int x = 0; x < bitmap.Width; x += 4)
            {
                SKColor p = bitmap.GetPixel(x, y);
                (double _, double s, double v) = ToHsv(p.Red, p.Green, p.Blue);
                if (s > 0.25)
                {
                    values.Add(v);
                }
            }
        }
        if (values.Count == 0)
        {
            throw new InvalidDataException("Aucun pixel saturé pour mesurer la valeur.");
        }
        values.Sort();
        return values[values.Count / 2];
    }

    /// <summary>Teinte médiane circulaire des pixels saturés (échantillonnés).</summary>
    internal static double MedianHue(SKBitmap bitmap)
    {
        double sx = 0;
        double sy = 0;
        int n = 0;
        for (int y = 0; y < bitmap.Height; y += 4)
        {
            for (int x = 0; x < bitmap.Width; x += 4)
            {
                SKColor p = bitmap.GetPixel(x, y);
                (double h, double s, _) = ToHsv(p.Red, p.Green, p.Blue);
                if (s > 0.25)
                {
                    double a = h * Math.PI / 180.0;
                    sx += Math.Cos(a);
                    sy += Math.Sin(a);
                    n++;
                }
            }
        }
        if (n == 0)
        {
            throw new InvalidDataException("Aucun pixel saturé pour mesurer la teinte.");
        }
        double hue = Math.Atan2(sy / n, sx / n) * 180.0 / Math.PI;
        return hue < 0 ? hue + 360 : hue;
    }

    /// <summary>
    /// Rotation de teinte + mise à l'échelle de valeur (pixels saturés
    /// seuls : blanc/noir intacts).
    /// </summary>
    internal static SKBitmap ShiftHue(SKBitmap source, double degrees, double valueScale = 1.0)
    {
        var dest = new SKBitmap(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                SKColor p = source.GetPixel(x, y);
                (double hue, double sat, double val) = ToHsv(p.Red, p.Green, p.Blue);
                if (sat <= 0.25)
                {
                    dest.SetPixel(x, y, p);
                    continue;
                }
                double shifted = (hue + degrees) % 360;
                if (shifted < 0)
                {
                    shifted += 360;
                }
                dest.SetPixel(x, y, FromHsv(shifted, sat, Math.Min(1.0, val * valueScale), p.Alpha));
            }
        }
        return dest;
    }

    /// <summary>RGB 0-255 vers HSV (teinte 0-360, saturation/valeur 0-1).</summary>
    internal static (double H, double S, double V) ToHsv(byte r, byte g, byte b)
    {
        double rd = r / 255.0;
        double gd = g / 255.0;
        double bd = b / 255.0;
        double max = Math.Max(rd, Math.Max(gd, bd));
        double min = Math.Min(rd, Math.Min(gd, bd));
        double d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == rd)
            {
                h = 60 * (((gd - bd) / d) % 6);
            }
            else if (max == gd)
            {
                h = 60 * ((bd - rd) / d + 2);
            }
            else
            {
                h = 60 * ((rd - gd) / d + 4);
            }
        }
        if (h < 0)
        {
            h += 360;
        }
        return (h, max == 0 ? 0 : d / max, max);
    }

    private static SKColor FromHsv(double h, double s, double v, byte alpha)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
        double m = v - c;
        (double r, double g, double b) = (h % 360) switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return new SKColor(
            (byte)Math.Clamp((r + m) * 255, 0, 255),
            (byte)Math.Clamp((g + m) * 255, 0, 255),
            (byte)Math.Clamp((b + m) * 255, 0, 255),
            alpha);
    }

    private static SKBitmap Crop(SKBitmap source, SKRectI rect)
    {
        var dest = new SKBitmap(rect.Width, rect.Height);
        using var canvas = new SKCanvas(dest);
        canvas.DrawBitmap(source,
            new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom),
            new SKRect(0, 0, rect.Width, rect.Height),
            SKSamplingOptions.Default);
        return dest;
    }

    private static SKBitmap Normalize(SKBitmap source)
    {
        var dest = new SKBitmap(CellW, CellH);
        using var canvas = new SKCanvas(dest);
        canvas.DrawBitmap(source, new SKRect(0, 0, CellW, CellH), SKSamplingOptions.Default);
        source.Dispose();
        return dest;
    }

    private static SKBitmap CopyOf(SKBitmap source) => source.Copy();

    private static SKBitmap Decode(string path)
    {
        using var stream = File.OpenRead(path);
        return SKBitmap.Decode(stream) ?? throw new InvalidDataException($"Illisible : {path}.");
    }
}
