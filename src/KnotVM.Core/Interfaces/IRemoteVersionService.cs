using KnotVM.Core.Models;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per recupero versioni Node.js disponibili da nodejs.org.
/// </summary>
public interface IRemoteVersionService
{
    /// <summary>
    /// Ottiene tutte le versioni disponibili da nodejs.org/dist/index.json.
    /// Usa cache locale con expiry configurabile.
    /// </summary>
    /// <param name="forceRefresh">Se true, ignora cache e fetcha da remoto</param>
    /// <param name="cancellationToken">Token per cancellazione</param>
    /// <returns>Lista versioni disponibili</returns>
    Task<RemoteVersion[]> GetAvailableVersionsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ottiene tutte le versioni LTS disponibili.
    /// </summary>
    Task<RemoteVersion[]> GetLtsVersionsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ottiene l'ultima versione LTS attiva.
    /// </summary>
    Task<RemoteVersion?> GetLatestLtsVersionAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cerca una versione specifica (supporta partial match es: "20", "20.11").
    /// </summary>
    /// <param name="versionPattern">Pattern versione (es: "20.11.0", "20", "latest", "lts")</param>
    /// <param name="forceRefresh">Se true, ignora cache</param>
    /// <param name="cancellationToken">Token cancellazione</param>
    /// <returns>Versione trovata o null</returns>
    Task<RemoteVersion?> ResolveVersionAsync(string versionPattern, bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulisce la cache locale delle versioni.
    /// </summary>
    void ClearCache();
}
