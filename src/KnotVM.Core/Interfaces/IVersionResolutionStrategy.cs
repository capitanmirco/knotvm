namespace KnotVM.Core.Interfaces;

/// <summary>
/// Strategia per risolvere un tipo specifico di input versione Node.js.
/// Ogni implementazione gestisce un formato specifico (semver esatto, versione maggiore, LTS, ecc.).
/// </summary>
public interface IVersionResolutionStrategy
{
    /// <summary>
    /// Determina se questa strategia può gestire l'input versione fornito.
    /// </summary>
    /// <param name="versionInput">Input utente (es. "20", "lts", "hydrogen")</param>
    /// <returns>True se questa strategia può risolvere l'input</returns>
    bool CanHandle(string versionInput);

    /// <summary>
    /// Risolve l'input versione a versione semver completa.
    /// </summary>
    /// <param name="versionInput">Input utente da risolvere</param>
    /// <param name="cancellationToken">Token per cancellazione</param>
    /// <returns>Versione risolta (es. "20.11.0")</returns>
    Task<string> ResolveAsync(string versionInput, CancellationToken cancellationToken = default);
}
