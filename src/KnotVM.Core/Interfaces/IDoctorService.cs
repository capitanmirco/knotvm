using KnotVM.Core.Models;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio diagnostico per la verifica dello stato dell'ambiente KnotVM.
/// </summary>
public interface IDoctorService
{
    /// <summary>
    /// Esegue tutti i check diagnostici e restituisce i risultati.
    /// </summary>
    Task<IReadOnlyList<DoctorCheck>> RunAllChecksAsync(CancellationToken ct = default);

    /// <summary>
    /// Tenta la riparazione automatica per un check che supporta il fix.
    /// </summary>
    /// <returns>True se il fix ha avuto successo.</returns>
    Task<bool> TryAutoFixAsync(DoctorCheck check, CancellationToken ct = default);
}
