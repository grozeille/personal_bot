namespace TabletApp.Update;

/// <summary>
/// Source de mises à jour (Velopack en production, faux en tests).
/// <c>CheckForUpdateAsync</c> rend la version disponible, ou null si à jour.
/// <c>DownloadAndApplyAsync</c> télécharge, applique et redémarre l'app
/// (ne revient pas en cas de succès ; le hash est vérifié par Velopack).
/// </summary>
internal interface IUpdateSource
{
    string SourcePath { get; }

    Task<string?> CheckForUpdateAsync(CancellationToken ct);

    Task DownloadAndApplyAsync(Action<int>? progress, CancellationToken ct);
}
