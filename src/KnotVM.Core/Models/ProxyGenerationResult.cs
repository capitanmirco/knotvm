namespace KnotVM.Core.Models;

/// <summary>
/// Risultato della generazione di proxy per una installazione.
/// Tutti i proxy sono generati con prefisso 'nlocal-' (modalità isolated).
/// </summary>
/// <param name="Success">True se generazione completata con successo</param>
/// <param name="ProxiesGenerated">Numero di proxy generati</param>
/// <param name="ProxyPaths">Path completi dei proxy generati</param>
/// <param name="ErrorMessage">Messaggio errore se Success=false</param>
/// <param name="ErrorCode">Codice errore KNOT-* se Success=false</param>
public record ProxyGenerationResult(
    bool Success,
    int ProxiesGenerated,
    string[] ProxyPaths,
    string? ErrorMessage = null,
    string? ErrorCode = null
);
