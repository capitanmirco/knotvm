using System.Text.RegularExpressions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services.VersionResolution;

/// <summary>
/// Gestisce versioni esatte in formato semver (es. "18.2.0").
/// Nessuna risoluzione remota necessaria: restituisce l'input inalterato.
/// </summary>
public class ExactVersionStrategy : IVersionResolutionStrategy
{
    private static readonly Regex SemverPattern = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

    /// <inheritdoc />
    public bool CanHandle(string versionInput)
    {
        return SemverPattern.IsMatch(versionInput.TrimStart('v'));
    }

    /// <inheritdoc />
    public Task<string> ResolveAsync(string versionInput, CancellationToken cancellationToken = default)
    {
        // Versione esatta: restituisce l'input normalizzato (senza prefisso 'v')
        return Task.FromResult(versionInput.TrimStart('v'));
    }
}
