using SkiaSharp;

namespace TabletApp.UI;

/// <summary>
/// Dessin pur du visage (sans état ni fenêtre) : utilisable à la fois par le
/// rendu temps réel (<c>RobotFaceRenderer</c>) et par le self-test offscreen
/// (<c>SelfTest</c>), qui produit des PNG vérifiables sans écran.
/// Style EVE : yeux lumineux sans pupilles ; le regard déplace l'œil entier.
/// </summary>
internal static class FacePainter
{
    private static readonly SKColor EyeColor = new(0x33, 0xCC, 0xFF); // cyan lumineux

    public static void DrawFace(
        SKCanvas canvas, int width, int height,
        double openAmount, double gazeX, double gazeY,
        double yawn, double timeSeconds)
    {
        canvas.Clear(SKColors.Black);
        float r = Math.Min(width, height) * 0.11f;
        float cy = height * 0.44f;
        DrawEye(canvas, width * 0.5f - r * 1.7f, cy, r, openAmount, gazeX, gazeY, yawn, timeSeconds);
        DrawEye(canvas, width * 0.5f + r * 1.7f, cy, r, openAmount, gazeX, gazeY, yawn, timeSeconds);
    }

    private static void DrawEye(
        SKCanvas canvas, float cx, float cy, float r,
        double open, double gazeX, double gazeY,
        double yawn, double timeSeconds)
    {
        // Sans pupilles : le regard déplace l'œil entier.
        float cxGaze = cx + (float)(gazeX * r * 0.45);
        float cyGaze = cy - (float)(gazeY * r * 0.25);

        // Bâillement : œil écrasé horizontalement + léger tremblement vertical.
        float rx = (float)(r * (1 - 0.25 * yawn));
        float cyTremble = cyGaze + (float)(Math.Sin(timeSeconds * 45) * 2.0 * yawn);

        // Halo.
        using (var glow = new SKPaint
        {
            Color = EyeColor.WithAlpha(60),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, r * 0.5f),
        })
        {
            canvas.DrawCircle(cxGaze, cyGaze, r * 1.15f, glow);
        }

        // Corps de l'œil (s'écrase verticalement pendant le clignement).
        float ry = Math.Max(2f, (float)(r * open));
        using (var body = new SKPaint { Color = EyeColor, IsAntialias = true })
        {
            canvas.DrawOval(cxGaze, cyTremble, rx, ry, body);
        }
    }
}
