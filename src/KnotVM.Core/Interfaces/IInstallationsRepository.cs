namespace KnotVM.Core.Interfaces;

using KnotVM.Core.Models;

/// <summary>
/// Repository per gestire le installazioni di Node.js.
/// </summary>
public interface IInstallationsRepository
{
    /// <summary>
    /// Ottiene tutte le installazioni presenti.
    /// </summary>
    /// <returns>Array di installazioni</returns>
    Installation[] GetAll();

    /// <summary>
    /// Aggiunge una nuova installazione.
    /// </summary>
    /// <param name="installation">Installazione da aggiungere</param>
    void Add(Installation installation);

    /// <summary>
    /// Aggiorna un'installazione esistente.
    /// </summary>
    /// <param name="installation">Installazione aggiornata</param>
    void Update(Installation installation);

    /// <summary>
    /// Rimuove un'installazione per alias.
    /// </summary>
    /// <param name="alias">Alias da rimuovere</param>
    /// <returns>True se rimossa</returns>
    bool Remove(string alias);

    /// <summary>
    /// Imposta un'installazione come attiva (Use=true), disattivando le altre.
    /// </summary>
    /// <param name="alias">Alias da attivare</param>
    void SetActiveInstallation(string alias);

    /// <summary>
    /// Ottiene un'installazione per alias.
    /// </summary>
    /// <param name="alias">Alias da cercare</param>
    /// <returns>Installation o null se non trovata</returns>
    Installation? GetByAlias(string alias);
}

