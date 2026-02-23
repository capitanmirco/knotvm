using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services.VersionResolution;

/// <summary>
/// Gestisce codename LTS diretti (es. "hydrogen", "iron", "gallium").
/// Accetta qualsiasi stringa alfabetica e la risolve dinamicamente contro i codename LTS remoti.
/// </summary>
public class CodenameStrategy(IRemoteVersionService remoteVersionService) : IVersionResolutionStrategy
{
    /// <inheritdoc />
    public bool CanHandle(string versionInput)
    {
        // Accetta qualsiasi stringa puramente alfabetica (nessun numero o separatore)
        return !string.IsNullOrEmpty(versionInput) && versionInput.All(char.IsLetter);
    }

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string versionInput, CancellationToken cancellationToken = default)
    {
        var allVersions = await remoteVersionService.GetAvailableVersionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // L'index.json è ordinato desc: il primo match è il più recente per quel codename
        var ltsVersion = allVersions.FirstOrDefault(v =>
            !string.IsNullOrEmpty(v.Lts) &&
            v.Lts.Equals(versionInput, StringComparison.OrdinalIgnoreCase));

        if (ltsVersion == null)
        {
            throw new KnotVMException(
                KnotErrorCode.ArtifactNotAvailable,
                $"Codename LTS '{versionInput}' non trovato. " +
                $"Esegui 'knot list-remote --lts' per vedere i codename disponibili");
        }

        return ltsVersion.Version;
    }
}
