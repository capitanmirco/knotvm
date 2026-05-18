using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

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
        // Evita di chiamare GetAll() due volte: delega la ricerca a TryFind
        // che viene poi riutilizzata da ResolveAsync.
        return TryFind(versionInput, out _);
    }

    /// <inheritdoc />
    public Task<string> ResolveAsync(string versionInput, CancellationToken cancellationToken = default)
    {
        if (!TryFind(versionInput, out var installation) || installation == null)
        {
            throw new KnotVMException(
                KnotErrorCode.InstallationNotFound,
                $"Alias '{versionInput}' non trovato tra le installazioni locali");
        }

        return Task.FromResult(installation.Version);
    }

    /// <summary>
    /// Cerca l'alias nelle installazioni locali con una singola chiamata a GetAll().
    /// Riutilizzato sia da CanHandle che da ResolveAsync per evitare doppie scansioni.
    /// </summary>
    private bool TryFind(string versionInput, out Installation? installation)
    {
        var installations = installationsRepository.GetAll();
        installation = installations.FirstOrDefault(
            i => i.Alias.Equals(versionInput, StringComparison.OrdinalIgnoreCase));
        return installation != null;
    }
}
