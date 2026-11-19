using SkiaSharp;

namespace TabletApp.UI;

/// <summary>
/// Forçage de bouche : utilisé pendant le sommeil (bâillement = bouche en O
/// qui grandit, réveil = bouche plate gardée jusqu'au bout).
/// </summary>
internal sealed record MouthOverride(RobotExpression Expression, double Openness);

/// <summary>
/// Dessin du visage à partir des SVG embarqués (docs/faces, un par émotion),
/// découpés en 2 calques (yeux/bouche) pour l'animation : clignement (squash
/// vertical des yeux), regard (translation, bouche en parallaxe), bâillement.
/// Changements d'expression instantanés (pas de transition).
/// Le glow est ajouté au rendu (flou, coupable par `enableGlow`), les SVG restent plats.
/// Règle : seuls neutral et happy clignent ; le sommeil ferme tous les yeux.
/// </summary>
internal static class FacePainter
{
    private const float ViewW = 800f;
    private const float ViewH = 550f;
    private const float FaceCenterX = 400f;

    public static void DrawFace(
        SKCanvas canvas, int width, int height,
        double openAmount, double gazeX, double gazeY,
        double yawn, double timeSeconds,
        RobotExpression expression,
        MouthOverride? mouthOverride = null, bool awake = true, bool enableGlow = true)
    {
        canvas.Clear(SKColors.Black);
        var face = SvgFaceLoader.For(expression);

        float scale = Math.Min(width / ViewW, height / ViewH);
        float ox = (width - ViewW * scale) / 2;
        float oy = (height - ViewH * scale) / 2;

        double open = awake && !Blinks(expression) ? 1.0 : openAmount;
        float squash = (float)Math.Max(0.02, open);
        float squeeze = (float)(1 - 0.1 * yawn);
        // Pas de tremblement : une oscillation faisait vibrer le visage (voir test SteadyYawn).
        float ex = (float)(gazeX * ViewW * 0.0375);
        float ey = (float)(-gazeY * ViewH * 0.045);
        const float mouthParallax = 0.55f;

        SKPaint? glowPaint = enableGlow
            ? new SKPaint { MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 26f) }
            : null;
        using (glowPaint)
        {
            SKPicture mouthPic = mouthOverride is null
                ? face.Mouth
                : SvgFaceLoader.For(mouthOverride.Expression).Mouth;
            float mouthSY = (float)(mouthOverride?.Openness ?? 1.0);
            float mouthPivotY = mouthOverride is null
                ? face.MouthPivotY
                : SvgFaceLoader.For(mouthOverride.Expression).MouthPivotY;

            DrawLayer(canvas, mouthPic, ox, oy, scale,
                ex * mouthParallax, ey * mouthParallax, 1f, mouthSY,
                FaceCenterX, mouthPivotY, glowPaint);
            DrawLayer(canvas, face.Eyes, ox, oy, scale,
                ex, ey, squeeze, squash, FaceCenterX, face.EyePivotY, glowPaint);
        }
    }

    internal static bool Blinks(RobotExpression expression) => expression switch
    {
        RobotExpression.Neutral => true,
        RobotExpression.Happy => true,
        _ => false,
    };

    private static void DrawLayer(SKCanvas canvas, SKPicture picture,
        float ox, float oy, float scale,
        float tx, float ty, float sx, float sy,
        float pivotX, float pivotY, SKPaint? glowPaint)
    {
        if (sx < 0.01 || sy < 0.01)
        {
            return;
        }
        canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);
        canvas.Translate(tx, ty);
        canvas.Translate(pivotX, pivotY);
        canvas.Scale(sx, sy);
        canvas.Translate(-pivotX, -pivotY);
        if (glowPaint is not null)
        {
            canvas.DrawPicture(picture, glowPaint);
        }
        canvas.DrawPicture(picture);
        canvas.Restore();
    }
}
