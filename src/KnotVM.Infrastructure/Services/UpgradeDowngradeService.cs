using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio upgrade/downgrade versioni Node.js per alias esistenti.
/// </summary>
public class UpgradeDowngradeService : IUpgradeDowngradeService
{
    private readonly IRemoteVersionService _remoteVersionService;
    private readonly IInstallationService _installationService;
    private readonly IInstallationManager _installationManager;

    public UpgradeDowngradeService(
        IRemoteVersionService remoteVersionService,
        IInstallationService installationService,
        IInstallationManager installationManager)
    {
        _remoteVersionService = remoteVersionService;
        _installationService = installationService;
        _installationManager = installationManager;
    }

    public bool IsLtsAlias(string alias) =>
        !string.IsNullOrWhiteSpace(alias) &&
        alias.Trim().Equals("lts", StringComparison.OrdinalIgnoreCase);

    public async Task<RemoteVersion?> GetLatestLtsVersionAsync(CancellationToken cancellationToken = default) =>
        await _remoteVersionService.GetLatestLtsVersionAsync(cancellationToken: cancellationToken);

    public async Task<RemoteVersion[]> GetRecentLtsVersionsAsync(
        string currentVersion,
        int count = 15,
        CancellationToken cancellationToken = default)
    {
        var all = await _remoteVersionService.GetLtsVersionsAsync(cancellationToken: cancellationToken);
        return all
            .Where(v => !v.Version.Equals(currentVersion.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
            .Take(count)
            .ToArray();
    }

    public async Task<RemoteVersion[]> GetRemoteVersionsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var all = await _remoteVersionService.GetAvailableVersionsAsync(cancellationToken: cancellationToken);
        return all.Take(limit).ToArray();
    }

    public async Task<InstallationPrepareResult> ReplaceVersionAsync(
        string alias,
        bool wasActive,
        string targetVersion,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Rimuove l'installazione corrente (force per gestire alias attivo)
        _installationManager.RemoveInstallation(alias, force: true);

        // Installa la nuova versione con lo stesso alias
        var result = await _installationService.InstallAsync(
            targetVersion,
            alias,
            forceReinstall: false,
            progressCallback: progress,
            cancellationToken: cancellationToken);

        if (!result.Success)
            return result;

        // Riattiva l'alias se era precedentemente attivo
        if (wasActive)
        {
            try
            {
                _installationManager.UseInstallation(alias);
            }
            catch (Exception ex) when (ex is not KnotVMException)
            {
                throw new KnotVMException(
                    KnotErrorCode.InstallationFailed,
                    $"Versione installata ma riattivazione alias '{alias}' fallita: {ex.Message}",
                    ex);
            }
        }

        return result;
    }
}
