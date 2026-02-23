namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per sincronizzazione proxy cross-platform.
/// </summary>
public interface ISyncService
{
    /// <summary>
    /// Sincronizza i proxy con lo stato corrente delle installazioni.
    /// </summary>
    /// <param name="force">Se true, rigenera tutti i proxy. Se false, rigenera solo quelli dinamici.</param>
    void Sync(bool force = false);

    /// <summary>
    /// Verifica se è necessaria una sincronizzazione.
    /// </summary>
    /// <returns>True se lo stato dei proxy è inconsistente con le installazioni</returns>
    bool IsSyncNeeded();
}
