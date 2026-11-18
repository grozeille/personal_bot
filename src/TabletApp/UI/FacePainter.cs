using SkiaSharp;

namespace TabletApp.UI;

/// <summary>
/// Dessin pur du visage (sans état ni fenêtre) : utilisable à la fois par le
/// rendu temps réel (<c>RobotFaceRenderer</c>) et par le self-test offscreen
/// (<c>SelfTest</c>), qui produit des PNG vérifiables sans écran.
/// </summary>
internal static class FacePainter
{
    private static readonly SKColor EyeColor = new(0x33, 0xCC, 0xFF); // cyan lumineux
    private static readonly SKColor CoreColor = SKColors.White;

    public static void DrawFace(
        SKCanvas canvas, int width, int height,
        double openAmount, double gazeX, double gazeY)
    {
        canvas.Clear(SKColors.Black);
        float r = Math.Min(width, height) * 0.11f;
        float cy = height * 0.44f;
        DrawEye(canvas, width * 0.5f - r * 1.7f, cy, r, openAmount, gazeX, gazeY);
        DrawEye(canvas, width * 0.5f + r * 1.7f, cy, r, openAmount, gazeX, gazeY);
    }

    private static void DrawEye(
        SKCanvas canvas, float cx, float cy, float r,
        double open, double gazeX, double gazeY)
    {
        // Halo.
        using (var glow = new SKPaint
        {
            Color = EyeColor.WithAlpha(60),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, r * 0.5f),
        })
        {
            canvas.DrawCircle(cx, cy, r * 1.15f, glow);
        }

        // Corps de l'œil (s'écrase verticalement pendant le clignement).
        float ry = Math.Max(2f, (float)(r * open));
        using (var body = new SKPaint { Color = EyeColor, IsAntialias = true })
        {
            canvas.DrawOval(cx, cy, r, ry, body);
        }

        // Pupille décalée par le regard, contenue dans l'œil même à moitié fermé.
        float px = cx + (float)(gazeX * r * 0.42);
        float py = cy - (float)(gazeY * r * 0.25);
        float pr = Math.Max(1.5f, (float)(r * 0.42 * (0.35 + 0.65 * open)));
        using (var pupil = new SKPaint { Color = CoreColor, IsAntialias = true })
        {
            canvas.DrawOval(px, py, pr, Math.Max(1.5f, pr * (float)open), pupil);
        }
    }
}
