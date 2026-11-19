using System.Reflection;
using SkiaSharp;
using Svg.Skia;

namespace TabletApp.UI;

/// <summary>
/// Icône de mise à jour (docs/icons/update.svg, flèches circulaires cyan)
/// affichée en haut à droite quand une maj est disponible. Chargée une fois
/// et mise en cache ; pulsation douce pour attirer l'œil.
/// </summary>
internal static class UpdateIcon
{
    private static SKSvg? _svg;
    private static readonly object CacheLock = new();

    public static SKPicture Picture
    {
        get
        {
            lock (CacheLock)
            {
                return (_svg ??= Load()).Picture
                    ?? throw new InvalidDataException("Rendu SVG impossible pour update.svg.");
            }
        }
    }

    /// <summary>
    /// Dessine l'icône (coin haut-droit en (right, top), taille size).
    /// </summary>
    public static void Draw(SKCanvas canvas, float right, float top, float size, double timeSeconds)
    {
        float pulse = (float)(0.72 + 0.28 * (0.5 + 0.5 * Math.Sin(timeSeconds * 3.2)));
        byte alpha = (byte)(255 * pulse);

        using var paint = new SKPaint { Color = new SKColor(255, 255, 255, alpha) };
        canvas.Save();
        canvas.Translate(right - size, top);
        canvas.Scale(size / 100f);
        canvas.DrawPicture(Picture, paint);
        canvas.Restore();
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
