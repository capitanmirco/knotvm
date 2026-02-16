using KnotVM.Core.Models;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per installazione versioni Node.js da remote e discovery installazioni locali.
/// </summary>
public interface IInstallationService
{
    /// <summary>
    /// Installa una versione Node.js da remote.
    /// </summary>
    /// <param name="versionPattern">Pattern versione (es: "20", "lts", "20.11.0")</param>
    /// <param name="alias">Alias per installazione (null = usa version number)</param>
    /// <param name="forceReinstall">Se true, reinstalla anche se già presente</param>
    /// <param name="progressCallback">Callback per progress download</param>
    /// <param name="cancellationToken">Token cancellazione</param>
    /// <returns>Risultato installazione</returns>
    Task<InstallationPrepareResult> InstallAsync(
        string versionPattern,
        string? alias = null,
        bool forceReinstall = false,
        IProgress<DownloadProgress>? progressCallback = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Verifica se una versione è già installata.
    /// </summary>
    /// <param name="alias">Alias da verificare</param>
    /// <returns>True se installata</returns>
    bool IsInstalled(string alias);

    /// <summary>
    /// Rimuove un'installazione per alias.
    /// </summary>
    /// <param name="alias">Alias da rimuovere</param>
    /// <param name="force">Se true, rimuove anche se versione attiva</param>
    /// <returns>True se rimossa</returns>
    bool RemoveInstallation(string alias, bool force = false);

    /// <summary>
    /// Ottiene path installazione per alias.
    /// </summary>
    /// <param name="alias">Alias</param>
    /// <returns>Path directory installazione</returns>
    string GetInstallationPath(string alias);

    /// <summary>
    /// Verifica integrità installazione (node executable accessibile).
    /// </summary>
    /// <param name="alias">Alias da verificare</param>
    /// <returns>True se installazione valida</returns>
    bool VerifyInstallation(string alias);

    /// <summary>
    /// Ottiene versione Node.js di un'installazione.
    /// </summary>
    /// <param name="alias">Alias</param>
    /// <returns>Versione (es: "20.11.0") o null se non rilevabile</returns>
    string? GetInstalledVersion(string alias);

    /// <summary>
    /// Preflight check prima di installazione.
    /// Verifica: spazio disco, connettività, permessi scrittura.
    /// </summary>
    /// <param name="estimatedSizeBytes">Dimensione stimata download</param>
    /// <returns>True se preflight OK</returns>
    /// <exception cref="KnotVM.Core.Exceptions.KnotVMException">Se check fallisce</exception>
    Task<bool> PreflightCheckAsync(long estimatedSizeBytes = 50_000_000);

    /// <summary>
    /// Cleanup installazione parziale su errore.
    /// </summary>
    /// <param name="alias">Alias installazione fallita</param>
    void RollbackInstallation(string alias);
}
