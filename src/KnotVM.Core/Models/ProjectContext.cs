namespace KnotVM.Core.Models;

/// <summary>
/// Contesto di progetto rilevato da file di configurazione.
/// Contiene la versione Node.js richiesta e il nome del progetto da usare come alias.
/// </summary>
/// <param name="Version">Versione Node.js richiesta, o null se non trovata.</param>
/// <param name="ProjectName">Nome del progetto (da package.json), o null se non disponibile.</param>
public record ProjectContext(string? Version, string? ProjectName);
