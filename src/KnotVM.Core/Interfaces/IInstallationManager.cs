namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per gestione ciclo vita installazioni (use, rename, remove).
/// </summary>
public interface IInstallationManager
{
    /// <summary>
    /// Attiva una installazione per alias.
    /// Wrapper che combina VersionManager.UseVersion + sync automatico.
    /// </summary>
    /// <param name="alias">Alias da attivare</param>
    void UseInstallation(string alias);

    /// <summary>
    /// Rinomina un'installazione.
    /// </summary>
    /// <param name="fromAlias">Alias corrente</param>
    /// <param name="toAlias">Nuovo alias</param>
    void RenameInstallation(string fromAlias, string toAlias);

    /// <summary>
    /// Rimuove un'installazione.
    /// Policy: blocca rimozione installazione attiva se force=false.
    /// </summary>
    /// <param name="alias">Alias da rimuovere</param>
    /// <param name="force">Se true, rimuove anche se attiva (disattiva prima)</param>
    void RemoveInstallation(string alias, bool force = false);

    /// <summary>
    /// Valida un alias secondo le regole:
    /// - Regex: ^[a-zA-Z0-9_-]+$
    /// - Lunghezza: 1..50
    /// - Non riservato: node, npm, npx, knot
    /// </summary>
    /// <param name="alias">Alias da validare</param>
    /// <returns>True se valido</returns>
    bool IsAliasValid(string alias);

    /// <summary>
    /// Verifica se un alias esiste già (case-insensitive).
    /// </summary>
    /// <param name="alias">Alias da verificare</param>
    /// <returns>True se esiste</returns>
    bool AliasExists(string alias);

    /// <summary>
    /// Valida un alias e lancia KnotVMHintException se non valido o esistente.
    /// </summary>
    /// <param name="alias">Alias da validare</param>
    /// <param name="checkExists">Se true, verifica anche che l'alias non esista già</param>
    /// <exception cref="KnotVMHintException">Lanciata se alias non valido o già esistente</exception>
    void ValidateAliasOrThrow(string alias, bool checkExists = true);
}
