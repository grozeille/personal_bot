using SkiaSharp;

namespace TabletApp.UI;

/// <summary>
/// Dessin du visage à partir des SVG embarqués (docs/faces, un par émotion),
/// découpés en 2 calques (yeux/bouche) pour l'animation : clignement (squash
/// vertical des yeux), regard (translation, bouche en parallaxe), bâillement,
/// pop de transition. Le glow est ajouté au rendu (flou), les SVG restent plats.
/// Règle : seuls neutral et happy clignent ; le sommeil ferme tous les yeux.
/// </summary>
internal static class FacePainter
{
    private const float ViewW = 800f;
    private const float ViewH = 550f;
    private const float EyeLineY = 295f;
    private const float FaceCenterX = 400f;
    private const float FaceCenterY = 330f;

    public static void DrawFace(
        SKCanvas canvas, int width, int height,
        double openAmount, double gazeX, double gazeY,
        double yawn, double timeSeconds,
        RobotExpression expression, double pop,
        bool awake = true)
    {
        canvas.Clear(SKColors.Black);
        var face = SvgFaceLoader.For(expression);

        float scale = Math.Min(width / ViewW, height / ViewH);
        float ox = (width - ViewW * scale) / 2;
        float oy = (height - ViewH * scale) / 2;

        double open = awake && !Blinks(expression) ? 1.0 : openAmount;
        float squash = (float)Math.Max(0.02, open);
        float squeeze = (float)(1 - 0.1 * yawn);
        float tremble = (float)(Math.Sin(timeSeconds * 45) * 2.5 * yawn);
        float ex = (float)(gazeX * ViewW * 0.0375);
        float ey = (float)(-gazeY * ViewH * 0.045 + tremble);
        const float mouthParallax = 0.55f;

        using var glow = new SKPaint
        {
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 26f),
        };

        DrawLayer(canvas, face.Mouth, ox, oy, scale, (float)pop,
            ex * mouthParallax, ey * mouthParallax, 1f, 1f, glow, FaceCenterY);
        DrawLayer(canvas, face.Eyes, ox, oy, scale, (float)pop,
            ex, ey, squeeze, squash, glow, pivotY: EyeLineY);
    }

    private static bool Blinks(RobotExpression expression) => expression switch
    {
        RobotExpression.Neutral => true,
        RobotExpression.Happy => true,
        _ => false,
    };

    private static void DrawLayer(
        SKCanvas canvas, SKPicture picture,
        float ox, float oy, float scale, float pop,
        float tx, float ty, float sx, float sy,
        SKPaint glow, float pivotY)
    {
        canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);
        canvas.Translate(FaceCenterX, FaceCenterY);
        canvas.Scale(pop);
        canvas.Translate(-FaceCenterX, -FaceCenterY);
        canvas.Translate(tx, ty);
        canvas.Translate(FaceCenterX, pivotY);
        canvas.Scale(sx, sy);
        canvas.Translate(-FaceCenterX, -pivotY);
        canvas.DrawPicture(picture, glow);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }
}
