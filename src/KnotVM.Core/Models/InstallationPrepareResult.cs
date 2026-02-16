namespace KnotVM.Core.Models;

/// <summary>
/// Risultato dell'operazione di preparazione installazione (download + verifica + estrazione).
/// </summary>
/// <param name="Success">True se operazione completata con successo</param>
/// <param name="Alias">Alias dell'installazione</param>
/// <param name="Version">Versione Node.js installata</param>
/// <param name="InstallationPath">Path completo directory installazione</param>
/// <param name="ErrorMessage">Messaggio errore se Success=false</param>
/// <param name="ErrorCode">Codice errore KNOT-* se Success=false</param>
public record InstallationPrepareResult(
    bool Success,
    string Alias,
    string Version,
    string InstallationPath,
    string? ErrorMessage = null,
    string? ErrorCode = null
);
