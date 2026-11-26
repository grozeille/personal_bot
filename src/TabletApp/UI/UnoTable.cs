using SkiaSharp;
using TabletApp.Uno;

namespace TabletApp.UI;

/// <summary>Table de Uno : pile annoncée + cartes posées + éventail dos.</summary>
internal sealed record UnoTableState(
    UnoCard? Pile,
    UnoColor? PileColor,
    IReadOnlyList<UnoCard> Played,
    bool Drew,
    int HandCount);

/// <summary>
/// Table de Uno sur fond noir : carte du dessus en grand (vraie face
/// Uno Express) + éventail de dos (le compte, jamais les faces) +
/// compteur. Mêmes coordonnées vue 800×550 que FacePainter. La couleur
/// choisie d'un joker est rappelée en bordure.
/// </summary>
internal static class UnoTablePainter
{
    private const float ViewW = 800f;
    private const float ViewH = 550f;

    public static void DrawTable(SKCanvas canvas, int width, int height, UnoTableState table)
    {
        canvas.Clear(SKColors.Black);
        float scale = Math.Min(width / ViewW, height / ViewH);
        float ox = (width - ViewW * scale) / 2;
        float oy = (height - ViewH * scale) / 2;
        canvas.Save();
        canvas.Translate(ox, oy);
        canvas.Scale(scale);
        if (table.Pile is { } pile)
        {
            DrawCardFace(canvas, 250f, 205f, 150f, 218f, pile, table.PileColor);
        }
        else
        {
            DrawCardBackFace(canvas, 250f, 205f, 150f, 218f);
        }
        DrawLabel(canvas, "pile", 250f);
        DrawPlayedFan(canvas, table);
        DrawLabel(canvas, "moi", 550f);
        DrawFan(canvas, Math.Clamp(table.HandCount, 0, 24));
        DrawCounter(canvas, table.HandCount);
        canvas.Restore();
    }

    private static void DrawCardFace(SKCanvas canvas, float cx, float cy, float w, float h,
        UnoCard card, UnoColor? chosen)
    {
        var rect = new SKRect(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2);
        (SKBitmap atlas, SKRectI src) = UnoAtlas.Face(card);
        Blit(canvas, atlas, src, rect);
        // Bordure = couleur choisie pour un joker, blanche sinon.
        SKColor border = card.IsWild && chosen is { } pick && pick != UnoColor.Wild
            ? CardColor(pick)
            : SKColors.White;
        using var frame = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = card.IsWild ? 10 : 5,
            Color = border,
            IsAntialias = true,
        };
        canvas.DrawRoundRect(rect, 18, 18, frame);
    }

    private static void DrawCardBackFace(SKCanvas canvas, float cx, float cy, float w, float h)
    {
        var rect = new SKRect(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2);
        (SKBitmap atlas, SKRectI src) = UnoAtlas.Back();
        Blit(canvas, atlas, src, rect);
        using var frame = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5,
            Color = SKColors.White,
            IsAntialias = true,
        };
        canvas.DrawRoundRect(rect, 18, 18, frame);
    }

    /// <summary>Blit une région d'atlas, clippée arrondie (coins blancs sinon).</summary>
    private static void Blit(SKCanvas canvas, SKBitmap atlas, SKRectI src, SKRect dest)
    {
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(dest, 18, 18), SKClipOperation.Intersect, true);
        canvas.DrawBitmap(atlas,
            new SKRect(src.Left, src.Top, src.Right, src.Bottom), dest,
            SKSamplingOptions.Default);
        canvas.Restore();
    }

    /// <summary>Éventail faces visibles des cartes posées (ou dos si pioche).</summary>
    private static void DrawPlayedFan(SKCanvas canvas, UnoTableState table)
    {
        const float cy = 205f;
        if (table.Played.Count == 0)
        {
            if (table.Drew)
            {
                DrawCardBackFace(canvas, 550f, cy, 110f, 160f);
            }
            return;
        }
        const float w = 110f;
        const float h = 160f;
        int n = Math.Min(table.Played.Count, 4);
        float step = n == 1 ? 0 : Math.Min(55f, 260f / (n - 1));
        float startX = 550f - step * (n - 1) / 2;
        for (int i = 0; i < n; i++)
        {
            DrawCardFace(canvas, startX + i * step, cy, w, h, table.Played[i], null);
        }
    }

    private static void DrawLabel(SKCanvas canvas, string text, float cx)
    {
        using var font = new SKFont { Size = 20 };
        using var textPaint = new SKPaint { Color = new SKColor(130, 130, 130), IsAntialias = true };
        canvas.DrawText(text, cx, 340f, SKTextAlign.Center, font, textPaint);
    }

    private static void DrawFan(SKCanvas canvas, int count)
    {
        if (count <= 0)
        {
            return;
        }
        const float w = 96f;
        const float h = 140f;
        const float cy = 430f;
        float step = count == 1 ? 0 : Math.Min(w * 0.5f, 700f / (count - 1));
        float startX = 400f - step * (count - 1) / 2;
        (SKBitmap atlas, SKRectI src) = UnoAtlas.Back();
        var srcRect = new SKRect(src.Left, src.Top, src.Right, src.Bottom);
        for (int i = 0; i < count; i++)
        {
            float cx = startX + i * step;
            var rect = new SKRect(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2);
            canvas.Save();
            canvas.ClipRoundRect(new SKRoundRect(rect, 12, 12), SKClipOperation.Intersect, true);
            canvas.DrawBitmap(atlas, srcRect, rect, SKSamplingOptions.Default);
            canvas.Restore();
        }
    }

    private static void DrawCounter(SKCanvas canvas, int count)
    {
        string label = count switch
        {
            0 => "GAGNÉ !",
            1 => "1 carte — UNO !",
            _ => $"{count} cartes",
        };
        using var font = new SKFont { Size = 26 };
        using var text = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(label, 400f, 532f, SKTextAlign.Center, font, text);
    }

    private static SKColor CardColor(UnoColor color) => color switch
    {
        UnoColor.Red => new SKColor(229, 57, 53),
        UnoColor.Yellow => new SKColor(251, 192, 45),
        UnoColor.Green => new SKColor(67, 160, 71),
        UnoColor.Blue => new SKColor(30, 136, 229),
        _ => SKColors.White,
    };
}
