using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services.VersionResolution;

/// <summary>
/// Gestisce keyword LTS: "lts" (ultima LTS) e "lts/&lt;codename&gt;" (es. "lts/iron").
/// </summary>
public class LtsVersionStrategy(IRemoteVersionService remoteVersionService) : IVersionResolutionStrategy
{
    /// <inheritdoc />
    public bool CanHandle(string versionInput)
    {
        var lower = versionInput.ToLowerInvariant();
        return lower == "lts" || lower.StartsWith("lts/", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string versionInput, CancellationToken cancellationToken = default)
    {
        var lower = versionInput.ToLowerInvariant();
        var allVersions = await remoteVersionService.GetAvailableVersionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (lower == "lts")
        {
            // Ultima LTS disponibile (index.json è ordinato descending)
            var latestLts = allVersions.FirstOrDefault(v => !string.IsNullOrEmpty(v.Lts));

            if (latestLts == null)
            {
                throw new KnotVMException(
                    KnotErrorCode.ArtifactNotAvailable,
                    "Nessuna versione LTS trovata nel repository remoto");
            }

            return latestLts.Version;
        }

        // Formato "lts/<codename>" (es. "lts/iron")
        var codename = lower.Split('/')[1];
        var ltsVersion = allVersions.FirstOrDefault(v =>
            !string.IsNullOrEmpty(v.Lts) &&
            v.Lts.Equals(codename, StringComparison.OrdinalIgnoreCase));

        if (ltsVersion == null)
        {
            throw new KnotVMException(
                KnotErrorCode.ArtifactNotAvailable,
                $"Nessuna versione LTS con codename '{codename}' trovata. " +
                $"Esegui 'knot list-remote --lts' per vedere le versioni LTS disponibili");
        }

        return ltsVersion.Version;
    }
}
