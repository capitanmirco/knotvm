using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services.VersionResolution;

/// <summary>
/// Gestisce keyword generiche: "latest" e "current" → ultima versione stabile disponibile.
/// </summary>
public class KeywordStrategy(IRemoteVersionService remoteVersionService) : IVersionResolutionStrategy
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "latest",
        "current"
    };

    /// <inheritdoc />
    public bool CanHandle(string versionInput)
    {
        return Keywords.Contains(versionInput);
    }

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string versionInput, CancellationToken cancellationToken = default)
    {
        var allVersions = await remoteVersionService.GetAvailableVersionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // L'index.json è ordinato descending: la prima entry è la più recente
        var latest = allVersions.FirstOrDefault();

        if (latest == null)
        {
            throw new KnotVMException(
                KnotErrorCode.RemoteApiFailed,
                "Nessuna versione Node.js disponibile nel repository remoto");
        }

        return latest.Version;
    }
}
