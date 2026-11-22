using System.Drawing;
using SkiaSharp;
using TabletApp.UI;
using TabletApp.Update;

namespace TabletApp.Tests.Support;

/// <summary>
/// Source de maj factice : version proposée, mode hors-ligne (le check lève),
/// mode corrompu (l'installation lève, comme un hash Velopack invalide).
/// </summary>
internal sealed class FakeUpdateSource : IUpdateSource
{
    public string SourcePath => "(test)";

    public string? AvailableVersion { get; set; }

    public bool Offline { get; set; }

    public bool Corrupt { get; set; }

    public bool Applied { get; private set; }

    public Task<string?> CheckForUpdateAsync(CancellationToken ct)
    {
        if (Offline)
        {
            throw new IOException("NAS hors-ligne (test).");
        }
        return Task.FromResult(AvailableVersion);
    }

    public Task DownloadAndApplyAsync(Action<int>? progress, CancellationToken ct)
    {
        if (Corrupt)
        {
            throw new InvalidDataException("Hash invalide (test).");
        }
        progress?.Invoke(100);
        Applied = true;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mise à jour simulée : <c>RobotUpdater</c> sur source factice +
/// <c>ConfirmDialog</c> (écran 800×600) pour assertions.
/// </summary>
internal sealed class SimulatedUpdate
{
    public const float ScreenWidth = 800;
    public const float ScreenHeight = 600;

    public FakeUpdateSource Source { get; } = new();

    public RobotUpdater Updater { get; }

    public ConfirmDialog Confirm { get; } = new();

    public RobotExpression Current { get; private set; } = RobotExpression.Neutral;

    public string CurrentVersion { get; private set; } = "1.0.0";

    public SimulatedUpdate()
    {
        Updater = new RobotUpdater(Source);
    }

    /// <summary>Tap en pixels écran (comme MainForm : icône puis yeux).</summary>
    public void Tap(float x, float y)
    {
        if (!Updater.UpdateAvailable)
        {
            return;
        }
        if (Confirm.Active)
        {
            (float vx, float vy) = FacePainter.ToView(x, y, (int)ScreenWidth, (int)ScreenHeight);
            switch (Confirm.Tap(vx, vy))
            {
                case ConfirmTap.Cancelled:
                    Current = Confirm.Previous;
                    break;
                case ConfirmTap.Confirmed:
                    Current = Confirm.Previous;
                    InstallAsync().GetAwaiter().GetResult();
                    break;
                case ConfirmTap.Ignored:
                    break;
            }
        }
        else if (ConfirmDialog.IconHit(new PointF(x, y), ScreenWidth))
        {
            Confirm.Show(Current, ConfirmPurpose.Update);
            Current = RobotExpression.Confirm;
        }
    }

    public async Task InstallAsync()
    {
        bool ok = await Updater.DownloadAndApplyAsync().ConfigureAwait(false);
        if (ok && Source.Applied && Updater.PendingVersion is not null)
        {
            CurrentVersion = Updater.PendingVersion;
        }
    }

    /// <summary>
    /// Vrai si le coin haut-droit rendu contient des pixels cyan (l'icône),
    /// en excluant le cadre blanc du visage (rouge élevé).
    /// </summary>
    public static bool IconVisibleOffscreen(bool updateAvailable)
    {
        using var bitmap = new SKBitmap(800, 600);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(
            canvas, 800, 600, 1.0, 0, 0, 0, timeSeconds: 0.49,
            RobotExpression.Neutral, RobotExpression.Neutral, 1.0, awake: true,
            updateAvailable: updateAvailable);
        for (int y = 0; y < 100; y++)
        {
            for (int x = 700; x < 800; x++)
            {
                SKColor p = bitmap.GetPixel(x, y);
                if (p.Red is >= 100 and < 180 && p.Green > 180 && p.Blue > 180)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Vrai si la zone centrale (sans yeux ni bouche) contient du cyan
    /// (les points d'attente), à l'instant t avec busyDots.
    /// </summary>
    public static bool BusyDotsVisibleOffscreen(double timeSeconds) =>
        BusyDotPixels(timeSeconds) > 50;

    /// <summary>Compte les pixels cyan dans la zone des points d'attente.</summary>
    public static int BusyDotPixels(double timeSeconds)
    {
        using var bitmap = new SKBitmap(800, 600);
        using var canvas = new SKCanvas(bitmap);
        FacePainter.DrawFace(
            canvas, 800, 600, 1.0, 0, 0, 0, timeSeconds,
            RobotExpression.Neutral, RobotExpression.Neutral, 1.0, awake: true,
            busyDots: true);
        int count = 0;
        for (int y = 320; y < 350; y++)
        {
            for (int x = 350; x < 450; x++)
            {
                SKColor p = bitmap.GetPixel(x, y);
                if (p.Red is >= 100 and < 180 && p.Green > 180 && p.Blue > 180)
                {
                    count++;
                }
            }
        }
        return count;
    }
}
