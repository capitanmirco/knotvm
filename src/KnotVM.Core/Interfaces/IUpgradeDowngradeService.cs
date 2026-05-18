using KnotVM.Core.Models;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per upgrade e downgrade della versione Node.js associata a un alias esistente.
/// Mantiene lo stesso alias, sostituendo solo la versione installata.
/// </summary>
public interface IUpgradeDowngradeService
{
    /// <summary>
    /// Verifica se l'alias è di tipo LTS (case-insensitive match su "lts").
    /// </summary>
    bool IsLtsAlias(string alias);

    /// <summary>
    /// Ottiene l'ultima versione LTS disponibile da remoto.
    /// </summary>
    Task<RemoteVersion?> GetLatestLtsVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ottiene le ultime N versioni LTS disponibili, escludendo la versione corrente dell'alias.
    /// Utile per mostrare le opzioni di downgrade per un alias LTS.
    /// </summary>
    /// <param name="currentVersion">Versione attualmente installata sull'alias (da escludere)</param>
    /// <param name="count">Numero massimo di versioni da restituire</param>
    Task<RemoteVersion[]> GetRecentLtsVersionsAsync(string currentVersion, int count = 15, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ottiene le prime N versioni remote (tutte, non solo LTS).
    /// Utile per mostrare le opzioni di upgrade/downgrade per alias non-LTS.
    /// </summary>
    /// <param name="limit">Numero massimo di versioni da restituire</param>
    Task<RemoteVersion[]> GetRemoteVersionsAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sostituisce la versione Node.js di un alias esistente mantenendo lo stesso alias.
    /// Operazione: rimozione installazione corrente → installazione nuova versione → riattivazione se era attiva.
    /// </summary>
    /// <param name="alias">Alias da aggiornare</param>
    /// <param name="wasActive">True se l'alias era la versione attiva (da riattivare dopo il cambio)</param>
    /// <param name="targetVersion">Versione semver da installare (es: "22.14.0")</param>
    /// <param name="progress">Callback per progressi download</param>
    /// <param name="cancellationToken">Token cancellazione</param>
    /// <returns>Risultato dell'installazione</returns>
    Task<InstallationPrepareResult> ReplaceVersionAsync(
        string alias,
        bool wasActive,
        string targetVersion,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
