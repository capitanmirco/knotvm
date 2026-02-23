namespace KnotVM.Core.Interfaces;

/// <summary>
/// Risolve notazioni abbreviate di versioni Node.js a versioni semver complete.
/// Supporta: versioni esatte (18.2.0), versioni maggiori (20), keyword (lts, latest, current),
/// codename LTS (hydrogen, iron) e alias installati localmente.
/// </summary>
public interface IVersionResolver
{
    /// <summary>
    /// Risolve una stringa versione abbreviata a versione semver completa.
    /// </summary>
    /// <param name="versionInput">Input utente (es. "20", "lts", "hydrogen", "18.2.0")</param>
    /// <param name="cancellationToken">Token per cancellazione</param>
    /// <returns>Versione semver risolta (es. "20.11.0")</returns>
    Task<string> ResolveVersionAsync(string versionInput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se l'input è già una versione semver esatta (non richiede risoluzione remota).
    /// </summary>
    /// <param name="versionInput">Input da verificare</param>
    /// <returns>True se è nella forma X.Y.Z</returns>
    bool IsExactVersion(string versionInput);
}
