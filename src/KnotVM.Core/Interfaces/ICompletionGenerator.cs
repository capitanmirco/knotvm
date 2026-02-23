using KnotVM.Core.Enums;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Genera script di completamento shell per KnotVM.
/// </summary>
public interface ICompletionGenerator
{
    /// <summary>
    /// Genera script di completamento per la shell specificata.
    /// </summary>
    /// <param name="shellType">Tipo di shell target</param>
    /// <returns>Contenuto dello script di completamento</returns>
    Task<string> GenerateCompletionScriptAsync(ShellType shellType);

    /// <summary>
    /// Ottiene gli alias installati per il completamento dinamico.
    /// </summary>
    /// <returns>Collezione di alias installati ordinati alfabeticamente</returns>
    Task<IEnumerable<string>> GetInstalledAliasesAsync();

    /// <summary>
    /// Ottiene le versioni remote popolari per il completamento.
    /// Utilizza una cache con TTL di 24 ore per minimizzare le chiamate remote.
    /// </summary>
    /// <returns>Collezione di versioni popolari (es: lts, latest, 18, 20, 22)</returns>
    Task<IEnumerable<string>> GetPopularVersionsAsync();
}
