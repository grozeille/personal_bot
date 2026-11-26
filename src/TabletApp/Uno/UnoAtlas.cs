// Faces Mattel (Uno Express) : usage personnel/familial uniquement.
using System.Reflection;
using System.Text.Json;
using SkiaSharp;

namespace TabletApp.Uno;

/// <summary>
/// Atlas des faces Uno Express embarqué (docs/uno-atlas) : 51 régions
/// (36 chiffres 1-9, 12 spéciales couleur, 2 jokers, dos). Chargé une
/// fois, échec rapide et explicite si incomplet.
/// </summary>
internal static class UnoAtlas
{
    private static readonly object CacheLock = new();
    private static SKBitmap? _bitmap;
    private static Dictionary<string, SKRectI>? _rects;

    public static string KeyFor(UnoCard card) => card.Rank switch
    {
        UnoRank.Plus1 => $"plus1_{ColorName(card.Color)}",
        UnoRank.Skip => $"skip_{ColorName(card.Color)}",
        UnoRank.Reverse => $"reverse_{ColorName(card.Color)}",
        UnoRank.Wild => "wild",
        UnoRank.WildPlus2 => "wildplus2",
        _ => $"n{(int)card.Rank}_{ColorName(card.Color)}",
    };

    public static (SKBitmap Bitmap, SKRectI Rect) Face(UnoCard card) => Region(KeyFor(card));

    public static (SKBitmap Bitmap, SKRectI Rect) Back() => Region("back");

    internal static IReadOnlyDictionary<string, SKRectI> Regions
    {
        get
        {
            EnsureLoaded();
            return _rects!;
        }
    }

    private static (SKBitmap, SKRectI) Region(string key)
    {
        EnsureLoaded();
        if (!_rects!.TryGetValue(key, out SKRectI rect))
        {
            throw new InvalidDataException($"Face Uno introuvable dans l'atlas : {key}.");
        }
        return (_bitmap!, rect);
    }

    private static void EnsureLoaded()
    {
        lock (CacheLock)
        {
            if (_bitmap is not null)
            {
                return;
            }
            Assembly asm = Assembly.GetExecutingAssembly();
            using Stream png = asm.GetManifestResourceStream("TabletApp.UnoAtlas.atlas.png")
                ?? throw new FileNotFoundException("Atlas Uno introuvable (docs/uno-atlas/atlas.png non embarqué ?).");
            _bitmap = SKBitmap.Decode(png) ?? throw new InvalidDataException("Atlas Uno illisible.");
            using Stream json = asm.GetManifestResourceStream("TabletApp.UnoAtlas.atlas.json")
                ?? throw new FileNotFoundException("Coords atlas Uno introuvables.");
            using var doc = JsonDocument.Parse(json);
            var rects = new Dictionary<string, SKRectI>(StringComparer.Ordinal);
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
            {
                int[] v = prop.Value.EnumerateArray().Select(e => e.GetInt32()).ToArray();
                if (v.Length != 4)
                {
                    throw new InvalidDataException($"Coords invalides : {prop.Name}.");
                }
                var rect = new SKRectI(v[0], v[1], v[0] + v[2], v[1] + v[3]);
                if (rect.Left < 0 || rect.Top < 0 || rect.Right > _bitmap.Width || rect.Bottom > _bitmap.Height)
                {
                    throw new InvalidDataException($"Région hors atlas : {prop.Name}.");
                }
                rects[prop.Name] = rect;
            }
            foreach (UnoCard card in UnoGame.FullDeck().Distinct())
            {
                if (!rects.ContainsKey(KeyFor(card)))
                {
                    throw new InvalidDataException($"Face manquante : {KeyFor(card)}.");
                }
            }
            if (!rects.ContainsKey("back"))
            {
                throw new InvalidDataException("Dos manquant : back.");
            }
            _rects = rects;
        }
    }

    private static string ColorName(UnoColor color) => color switch
    {
        UnoColor.Red => "red",
        UnoColor.Yellow => "yellow",
        UnoColor.Green => "green",
        UnoColor.Blue => "blue",
        _ => throw new ArgumentOutOfRangeException(nameof(color)),
    };
}
