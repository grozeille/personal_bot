using SkiaSharp;

namespace TabletApp.UI;

/// <summary>
/// Forçage de bouche : utilisé pendant le sommeil (bâillement = bouche en O
/// qui grandit, réveil = bouche plate gardée jusqu'au bout).
/// </summary>
internal sealed record MouthOverride(RobotExpression Expression, double Openness);

/// <summary>
/// Dessin du visage à partir des SVG embarqués (docs/faces, un par émotion),
/// découpés en 3 parties (œil gauche, œil droit, bouche) pour l'animation :
/// clignement (squash vertical des yeux), regard (translation, bouche en
/// parallaxe), bâillement, et transition en cascade (chaque partie mute à son
/// tour). Le glow est ajouté au rendu (flou), les SVG restent plats.
/// Règle : seuls neutral et happy clignent ; le sommeil ferme tous les yeux.
/// </summary>
internal static class FacePainter
{
    private const float ViewW = 800f;
    private const float ViewH = 550f;
    private const float EyeLineY = 295f;
    private const float FaceCenterX = 400f;
    private const float FaceCenterY = 330f;

    // Fenêtres de la cascade (progression 0..1) : œil gauche, œil droit, bouche.
    private const double PartSpan = 0.6;
    private static readonly double[] PartStarts = { 0.0, 0.2, 0.4 };

    public static void DrawFace(
        SKCanvas canvas, int width, int height,
        double openAmount, double gazeX, double gazeY,
        double yawn, double timeSeconds,
        RobotExpression expression, RobotExpression previous, double progress,
        MouthOverride? mouthOverride = null, bool awake = true)
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

        bool settled = progress >= 1.0 || previous == expression;
        var old = settled ? null : SvgFaceLoader.For(previous);
        SKPicture mouthPic = mouthOverride is null
            ? face.Mouth
            : SvgFaceLoader.For(mouthOverride.Expression).Mouth;
        float mouthSY = (float)(mouthOverride?.Openness ?? 1.0);

        DrawPart(canvas, mouthPic, old?.Mouth, clip: null,
            ox, oy, scale, ex * mouthParallax, ey * mouthParallax,
            1f, mouthSY, FaceCenterX, FaceCenterY, PartQ(progress, 2), glow);
        DrawPart(canvas, face.EyesLeft, old?.EyesLeft, clip: null,
            ox, oy, scale, ex, ey,
            squeeze, squash, 250f, EyeLineY, PartQ(progress, 0), glow);
        DrawPart(canvas, face.EyesRight, old?.EyesRight, clip: null,
            ox, oy, scale, ex, ey,
            squeeze, squash, 550f, EyeLineY, PartQ(progress, 1), glow);
    }

    internal static bool Blinks(RobotExpression expression) => expression switch
    {
        RobotExpression.Neutral => true,
        RobotExpression.Happy => true,
        _ => false,
    };

    private static double PartQ(double progress, int part) =>
        SmoothStep(Math.Clamp((progress - PartStarts[part]) / PartSpan, 0, 1));

    private static double SmoothStep(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static void DrawPart(SKCanvas canvas,
        SKPicture current, SKPicture? previous, SKRect? clip,
        float ox, float oy, float scale,
        float tx, float ty, float sx, float sy,
        float pivotX, float pivotY, double q, SKPaint glow)
    {
        if (previous is not null && q < 0.99)
        {
            DrawInstance(canvas, previous, clip, ox, oy, scale,
                0, 0, 1 - (float)q, 1 - (float)q, pivotX, pivotY, glow);
        }
        if (q > 0.01)
        {
            DrawInstance(canvas, current, clip, ox, oy, scale,
                tx, ty, sx * (float)q, sy * (float)q, pivotX, pivotY, glow);
        }
    }

    private static void DrawInstance(SKCanvas canvas, SKPicture picture, SKRect? clip,
        float ox, float oy, float scale,
        float tx, float ty, float sx, float sy,
        float pivotX, float pivotY, SKPaint glow)
    {
        if (sx < 0.01 || sy < 0.01)
        {
            return;
        }
        canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);
        if (clip is { } r)
        {
            canvas.ClipRect(r);
        }
        canvas.Translate(tx, ty);
        canvas.Translate(pivotX, pivotY);
        canvas.Scale(sx, sy);
        canvas.Translate(-pivotX, -pivotY);
        canvas.DrawPicture(picture, glow);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }
}
