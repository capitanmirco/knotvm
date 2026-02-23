using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services.VersionResolution;

/// <summary>
/// Gestisce alias installati localmente.
/// Priorità #2: se l'input corrisponde a un alias installato, restituisce la sua versione.
/// </summary>
public class AliasStrategy(IInstallationsRepository installationsRepository) : IVersionResolutionStrategy
{
    /// <inheritdoc />
    public bool CanHandle(string versionInput)
    {
        var installations = installationsRepository.GetAll();
        return installations.Any(i => i.Alias.Equals(versionInput, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public Task<string> ResolveAsync(string versionInput, CancellationToken cancellationToken = default)
    {
        var installations = installationsRepository.GetAll();
        var installation = installations.FirstOrDefault(
            i => i.Alias.Equals(versionInput, StringComparison.OrdinalIgnoreCase));

        if (installation == null)
        {
            throw new KnotVMException(
                KnotErrorCode.InstallationNotFound,
                $"Alias '{versionInput}' non trovato tra le installazioni locali");
        }

        return Task.FromResult(installation.Version);
    }
}
