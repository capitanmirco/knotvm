using KnotVM.Core.Models;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per gestione versione attiva e switch versioni.
/// </summary>
public interface IVersionManager
{
    /// <summary>
    /// Ottiene l'alias della versione attualmente attiva.
    /// </summary>
    /// <returns>Alias attivo o null se nessuna versione attiva</returns>
    string? GetActiveAlias();

    /// <summary>
    /// Ottiene l'installazione attualmente attiva.
    /// </summary>
    /// <returns>Installation attiva o null</returns>
    Installation? GetActiveInstallation();

    /// <summary>
    /// Attiva una versione per alias.
    /// Aggiorna settings.txt e segna installazione come Use=true.
    /// </summary>
    /// <param name="alias">Alias da attivare</param>
    /// <returns>True se attivazione riuscita</returns>
    /// <exception cref="KnotVM.Core.Exceptions.KnotVMException">Se alias non esiste</exception>
    bool UseVersion(string alias);

    /// <summary>
    /// Disattiva la versione corrente (nessuna versione attiva).
    /// </summary>
    void UnuseVersion();

    /// <summary>
    /// Verifica se un alias è usato come versione attiva.
    /// </summary>
    /// <param name="alias">Alias da verificare</param>
    /// <returns>True se alias attivo</returns>
    bool IsAliasActive(string alias);

    /// <summary>
    /// Aggiorna il file settings.txt con nuovo alias attivo.
    /// </summary>
    /// <param name="alias">Alias da scrivere (null per rimuovere)</param>
    void UpdateSettingsFile(string? alias);

    /// <summary>
    /// Legge il file settings.txt con gestione BOM e fallback.
    /// </summary>
    /// <returns>Alias letto o null se file non esiste/vuoto</returns>
    string? ReadSettingsFile();
}
