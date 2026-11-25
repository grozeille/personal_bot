using SkiaSharp;

namespace TabletApp.UI;

/// <summary>
/// Forçage de bouche : sommeil (bâillement = bouche en O qui grandit,
/// réveil = bouche plate gardée) et parole (bouche grande ouverte).
/// </summary>
internal sealed record MouthOverride(RobotExpression Expression, double Openness);

internal enum FacePart
{
    LeftEye,
    RightEye,
    Mouth,
}

/// <summary>Halo voix autour de l'écran : fixe en écoute, pulsé en réflexion.</summary>
internal enum VoiceGlow
{
    None,
    Listening,
    Thinking,
}

/// <summary>
/// Dessin du visage à partir des SVG embarqués (docs/faces, un par émotion),
/// découpés en 3 parties (œil gauche, œil droit, bouche) pour l'animation :
/// clignement (squash vertical des yeux), regard (translation, bouche en
/// parallaxe), bâillement, et transition par squash-swap-stretch (~300 ms) :
/// chaque partie s'aplatit, bascule sur la nouvelle forme, se redéploie.
/// Les parties déclarées identiques ne bougent pas.
/// Le glow est ajouté au rendu (flou), les SVG restent plats.
/// Règle : seuls neutral et happy clignent ; le sommeil ferme tous les yeux.
/// </summary>
internal static class FacePainter
{
    private const float ViewW = 800f;
    private const float ViewH = 550f;
    private const float FaceCenterX = 400f;

    /// <summary>
    /// Convertit des pixels écran en coordonnées vue 800×550 (inverse de la
    /// mise à l'échelle du dessin), pour les zones de tap.
    /// </summary>
    public static (float X, float Y) ToView(float x, float y, int width, int height)
    {
        float scale = Math.Min(width / ViewW, height / ViewH);
        float ox = (width - ViewW * scale) / 2;
        float oy = (height - ViewH * scale) / 2;
        return ((x - ox) / scale, (y - oy) / scale);
    }

    /// <summary>Nombre de points d'attente (1-2-3 en boucle, 400 ms par cran).</summary>
    internal static int BusyDotCount(double timeSeconds) =>
        1 + ((int)(timeSeconds / 0.4) % 3);

    /// <summary>Points d'attente centrés à la place du visage (mise à jour).</summary>
    private static void DrawBusyDots(SKCanvas canvas, int width, int height, double timeSeconds)
    {
        string dots = new('.', BusyDotCount(timeSeconds));
        using var font = new SKFont { Size = height * 0.22f };
        using var paint = new SKPaint
        {
            Color = new SKColor(142, 220, 233),
            IsAntialias = true,
        };
        canvas.DrawText(dots, width / 2f, height / 2f + height * 0.08f, SKTextAlign.Center, font, paint);
    }

    public static void DrawFace(
        SKCanvas canvas, int width, int height,
        double openAmount, double gazeX, double gazeY,
        double yawn, double timeSeconds,
        RobotExpression expression, RobotExpression previous, double progress,
        MouthOverride? mouthOverride = null, bool awake = true, bool enableGlow = true,
        bool updateAvailable = false, VoiceGlow glow = VoiceGlow.None, bool busyDots = false)
    {
        canvas.Clear(SKColors.Black);
        if (busyDots)
        {
            DrawBusyDots(canvas, width, height, timeSeconds);
            return;
        }
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
            bool settled = progress >= 1.0 || previous == expression;
            var old = settled ? null : SvgFaceLoader.For(previous);
            SvgFaceLoader.PartLayer? busyMouth = mouthOverride is null && expression == RobotExpression.Busy
                ? SvgFaceLoader.BusyMouth(timeSeconds)
                : null;
            SKPicture mouthPic = mouthOverride is null
                ? busyMouth?.Picture ?? face.Mouth
                : SvgFaceLoader.For(mouthOverride.Expression).Mouth;
            float mouthSY = (float)(mouthOverride?.Openness ?? 1.0);
            RobotExpression mouthExpr = mouthOverride?.Expression ?? expression;
            float mouthPivotY = mouthOverride is null
                ? busyMouth?.PivotY ?? face.MouthPivotY
                : SvgFaceLoader.For(mouthOverride.Expression).MouthPivotY;

            float prevGaze = LooksAround(previous) ? 1f : 0f;
            float nextGaze = LooksAround(expression) ? 1f : 0f;
            float mouthNextGaze = LooksAround(mouthExpr) ? 1f : 0f;

            DrawMorphPart(canvas, mouthPic, old?.Mouth,
                settled || SvgFaceLoader.PartsMatch(FacePart.Mouth, previous, mouthExpr),
                ox, oy, scale,
                ex * mouthParallax * mouthNextGaze, ey * mouthParallax * mouthNextGaze,
                ex * mouthParallax * prevGaze, ey * mouthParallax * prevGaze,
                1f, mouthSY, FaceCenterX, mouthPivotY,
                old?.MouthPivotY ?? mouthPivotY, progress, glowPaint);
            DrawMorphPart(canvas, face.EyesLeft, old?.EyesLeft,
                settled || SvgFaceLoader.PartsMatch(FacePart.LeftEye, previous, expression),
                ox, oy, scale,
                ex * nextGaze, ey * nextGaze, ex * prevGaze, ey * prevGaze,
                squeeze, squash, 250f, face.LeftPivotY,
                old?.LeftPivotY ?? face.LeftPivotY, progress, glowPaint);
            DrawMorphPart(canvas, face.EyesRight, old?.EyesRight,
                settled || SvgFaceLoader.PartsMatch(FacePart.RightEye, previous, expression),
                ox, oy, scale,
                ex * nextGaze, ey * nextGaze, ex * prevGaze, ey * prevGaze,
                squeeze, squash, 550f, face.RightPivotY,
                old?.RightPivotY ?? face.RightPivotY, progress, glowPaint);

            if (updateAvailable)
            {
                float size = Math.Min(width, height) * 0.09f;
                UpdateIcon.Draw(canvas, width - 14, 14, size, timeSeconds);
            }
            if (glow != VoiceGlow.None)
            {
                DrawGlow(canvas, width, height, glow, timeSeconds);
            }
        }
    }

    private static void DrawGlow(SKCanvas canvas, int width, int height, VoiceGlow glow, double timeSeconds)
    {
        // Halo seul, sans trait : fixe en écoute, respirant (taille) à 1 Hz en réflexion.
        float margin = Math.Min(width, height) * 0.02f;
        double breathe = glow == VoiceGlow.Thinking
            ? 0.5 + 0.5 * Math.Sin(timeSeconds * 2 * Math.PI)
            : 0.5;
        float stroke = Math.Max(6, Math.Min(width, height) * 0.02f) * (float)(0.7 + 0.6 * breathe);
        using var halo = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = stroke,
            Color = new SKColor(142, 220, 233, 150),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 18f),
        };
        canvas.DrawRoundRect(
            new SKRect(margin, margin, width - margin, height - margin), 24, 24, halo);
    }

    internal static bool Blinks(RobotExpression expression) => expression switch
    {
        RobotExpression.Neutral => true,
        RobotExpression.Happy => true,
        _ => false,
    };

    internal static bool LooksAround(RobotExpression expression) => expression switch
    {
        RobotExpression.Dead => false,
        RobotExpression.Dizzy => false,
        RobotExpression.Tired => false,
        RobotExpression.Wink => false,
        RobotExpression.Sleep => false,
        RobotExpression.Confirm => false,
        RobotExpression.Busy => false,
        _ => true,
    };


    private static void DrawMorphPart(SKCanvas canvas,
        SKPicture picNew, SKPicture? picOld, bool same,
        float ox, float oy, float scale,
        float tx, float ty, float txOld, float tyOld, float sx, float syBase,
        float pivotX, float pivotY, float pivotOldY, double progress, SKPaint? glowPaint)
    {
        if (same || picOld is null)
        {
            DrawInstance(canvas, picNew, ox, oy, scale, tx, ty, sx, syBase,
                pivotX, pivotY, glowPaint);
            return;
        }
        if (progress < 0.5)
        {
            double s = 1.0 - Smooth(progress * 2);
            if (s > 0.01)
            {
                DrawInstance(canvas, picOld, ox, oy, scale, txOld, tyOld, sx, (float)(syBase * s),
                    pivotX, pivotOldY, glowPaint);
            }
        }
        else
        {
            double s = Smooth((progress - 0.5) * 2);
            if (s > 0.01)
            {
                DrawInstance(canvas, picNew, ox, oy, scale, tx, ty, sx, (float)(syBase * s),
                    pivotX, pivotY, glowPaint);
            }
        }
    }

    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static void DrawInstance(SKCanvas canvas, SKPicture picture,
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
