using Velopack;
using Velopack.Sources;

namespace TabletApp.Update;

/// <summary>
/// Source Velopack sur dossier local/réseau (<see cref="SimpleFileSource"/>) :
/// le NAS monté en P:. Les exceptions (NAS hors-ligne, hash invalide, ...)
/// remontent à <see cref="RobotUpdater"/> qui les traite en silence.
/// </summary>
internal sealed class VelopackUpdateSource : IUpdateSource
{
    public string SourcePath { get; }

    public VelopackUpdateSource(string sourcePath)
    {
        SourcePath = sourcePath;
    }

    public async Task<string?> CheckForUpdateAsync(CancellationToken ct)
    {
        UpdateManager manager = CreateManager();
        UpdateInfo? info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return info?.TargetFullRelease.Version.ToString();
    }

    public async Task DownloadAndApplyAsync(Action<int>? progress, CancellationToken ct)
    {
        UpdateManager manager = CreateManager();
        UpdateInfo? info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (info is null)
        {
            return;
        }
        await manager.DownloadUpdatesAsync(info, progress, ct).ConfigureAwait(false);
        manager.ApplyUpdatesAndRestart(info);
    }

    private UpdateManager CreateManager() =>
        new(new SimpleFileSource(new DirectoryInfo(SourcePath)));
}
