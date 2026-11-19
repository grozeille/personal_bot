namespace TabletApp.Update;

/// <summary>
/// État de l'auto-update : vérifie périodiquement la source et expose
/// <see cref="UpdateAvailable"/> pour l'icône. Toute erreur (NAS hors-ligne,
/// dépôt vide, hash invalide) est silencieuse : on garde la version
/// courante et on réessaiera au prochain cycle. Logique testable via
/// <see cref="IUpdateSource"/> factice (tests BDD).
/// </summary>
internal sealed class RobotUpdater
{
    private readonly IUpdateSource _source;

    public bool UpdateAvailable { get; private set; }

    public string? PendingVersion { get; private set; }

    /// <summary>0-100 pendant le téléchargement, null hors téléchargement.</summary>
    public int? DownloadPercent { get; private set; }

    public RobotUpdater(IUpdateSource source)
    {
        _source = source;
    }

    public async Task CheckAsync(CancellationToken ct = default)
    {
        try
        {
            string? version = await _source.CheckForUpdateAsync(ct).ConfigureAwait(false);
            PendingVersion = version;
            UpdateAvailable = version is not null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // NAS hors-ligne ou dépôt invalide : silencieux, on réessaie plus tard.
            PendingVersion = null;
            UpdateAvailable = false;
        }
    }

    /// <summary>
    /// Télécharge et applique la maj (redémarre l'app si succès).
    /// Rend false (sans lever) si le téléchargement échoue ou est refusé.
    /// </summary>
    public async Task<bool> DownloadAndApplyAsync(CancellationToken ct = default)
    {
        try
        {
            DownloadPercent = 0;
            await _source.DownloadAndApplyAsync(p => DownloadPercent = p, ct)
                .ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Hash invalide, NAS parti en cours de route, ... : on reste
            // sur la version courante, l'icône reste proposée au prochain cycle.
            return false;
        }
        finally
        {
            DownloadPercent = null;
        }
    }
}
