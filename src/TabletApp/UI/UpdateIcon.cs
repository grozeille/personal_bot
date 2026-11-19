using System.Reflection;
using SkiaSharp;
using Svg.Skia;

namespace TabletApp.UI;

/// <summary>
/// Icône de mise à jour (docs/icons/update.svg, flèches circulaires cyan)
/// affichée en haut à droite quand une maj est disponible. Chargée une fois
/// et mise en cache ; pulsation douce au repos, anneau de progression
/// pendant l'appui de 3 secondes.
/// </summary>
internal static class UpdateIcon
{
    private static SKSvg? _svg;

    public static SKPicture Picture => (_svg ??= Load()).Picture
        ?? throw new InvalidDataException("Rendu SVG impossible pour update.svg.");

    /// <summary>
    /// Dessine l'icône (coin haut-droit en (right, top), taille size).
    /// progress = 0..1 de l'appui en cours (anneau), timeSeconds = pulsation.
    /// </summary>
    public static void Draw(
        SKCanvas canvas, float right, float top, float size,
        double progress, double timeSeconds)
    {
        float left = right - size;
        float pulse = (float)(0.72 + 0.28 * (0.5 + 0.5 * Math.Sin(timeSeconds * 3.2)));
        byte alpha = progress > 0 ? (byte)255 : (byte)(255 * pulse);

        using var paint = new SKPaint { Color = new SKColor(255, 255, 255, alpha) };
        canvas.Save();
        canvas.Translate(left, top);
        canvas.Scale(size / 100f);
        canvas.DrawPicture(Picture, paint);
        canvas.Restore();

        float cx = left + size / 2;
        float cy = top + size / 2;
        float radius = size * 0.62f;
        using var track = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(3, size * 0.045f),
            Color = new SKColor(142, 220, 233, 70),
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
        };
        using var bar = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(3, size * 0.045f),
            Color = new SKColor(142, 220, 233, 255),
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
        };
        var oval = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
        if (progress > 0)
        {
            canvas.DrawCircle(cx, cy, radius, track);
            canvas.DrawArc(oval, -90, (float)(360 * Math.Clamp(progress, 0, 1)), false, bar);
        }
        else if (alpha < 255)
        {
            // Halo subtil au repos pour attirer l'œil sans agresser.
            canvas.DrawCircle(cx, cy, radius, track);
        }
    }

    private static SKSvg Load()
    {
        const string resource = "TabletApp.Icons.update.svg";
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException(
                $"Ressource SVG introuvable : {resource} (docs/icons/update.svg non embarquée ?).");
        var svg = new SKSvg();
        svg.Load(stream);
        if (svg.Picture is null)
        {
            throw new InvalidDataException($"Rendu SVG impossible pour {resource}.");
        }
        return svg;
    }
}
