using System.Text.RegularExpressions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services.VersionResolution;

/// <summary>
/// Gestisce versioni maggiori (es. "18", "20") → ultima versione x.y.z disponibile.
/// </summary>
public class MajorVersionStrategy(IRemoteVersionService remoteVersionService) : IVersionResolutionStrategy
{
    private static readonly Regex MajorVersionPattern = new(@"^\d+$", RegexOptions.Compiled);

    /// <inheritdoc />
    public bool CanHandle(string versionInput)
    {
        return MajorVersionPattern.IsMatch(versionInput);
    }

    /// <inheritdoc />
    public async Task<string> ResolveAsync(string versionInput, CancellationToken cancellationToken = default)
    {
        var major = int.Parse(versionInput);
        var allVersions = await remoteVersionService.GetAvailableVersionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Filtra per versione maggiore — l'index.json è già ordinato descending: il primo è il più recente
        var best = allVersions.FirstOrDefault(v =>
        {
            var parts = v.Version.Split('.');
            return parts.Length >= 1 && int.TryParse(parts[0], out var majorPart) && majorPart == major;
        });

        if (best == null)
        {
            throw new KnotVMException(
                KnotErrorCode.ArtifactNotAvailable,
                $"Nessuna versione Node.js {major}.x.x trovata nel repository remoto");
        }

        return best.Version;
    }
}
