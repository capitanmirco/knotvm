using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio installazione Node.js da remote.
/// </summary>
public class InstallationService : IInstallationService
{
    private readonly IRemoteVersionService _remoteVersions;
    private readonly INodeArtifactResolver _artifactResolver;
    private readonly IDownloadService _downloadService;
    private readonly IArchiveExtractor _archiveExtractor;
    private readonly IPathService _pathService;
    private readonly IFileSystemService _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly IPlatformService _platform;
    private readonly IInstallationsRepository _installationsRepo;
    private readonly ILockManager _lockManager;

    public InstallationService(
        IRemoteVersionService remoteVersions,
        INodeArtifactResolver artifactResolver,
        IDownloadService downloadService,
        IArchiveExtractor archiveExtractor,
        IPathService pathService,
        IFileSystemService fileSystem,
        IProcessRunner processRunner,
        IPlatformService platform,
        IInstallationsRepository installationsRepo,
        ILockManager lockManager)
    {
        _remoteVersions = remoteVersions;
        _artifactResolver = artifactResolver;
        _downloadService = downloadService;
        _archiveExtractor = archiveExtractor;
        _pathService = pathService;
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _platform = platform;
        _installationsRepo = installationsRepo;
        _lockManager = lockManager;
    }

    public async Task<InstallationPrepareResult> InstallAsync(
        string versionPattern,
        string? alias = null,
        bool forceReinstall = false,
        IProgress<DownloadProgress>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        using var lockHandle = _lockManager.AcquireLock("state");

        // 1. Resolve versione da pattern
        var remoteVersion = await _remoteVersions.ResolveVersionAsync(versionPattern, cancellationToken: cancellationToken);
        
        if (remoteVersion == null)
        {
            return new InstallationPrepareResult(
                Success: false,
                Alias: alias ?? versionPattern,
                Version: string.Empty,
                InstallationPath: string.Empty,
                ErrorMessage: $"Versione '{versionPattern}' non trovata",
                ErrorCode: KnotErrorCode.ArtifactNotAvailable.ToString()
            );
        }

        // 2. Determina alias (usa version se non specificato)
        var finalAlias = alias ?? remoteVersion.Version;

        // 3. Verifica se già installato
        if (!forceReinstall && IsInstalled(finalAlias))
        {
            return new InstallationPrepareResult(
                Success: false,
                Alias: finalAlias,
                Version: remoteVersion.Version,
                InstallationPath: GetInstallationPath(finalAlias),
                ErrorMessage: $"Versione '{finalAlias}' già installata (usa --force per reinstallare)",
                ErrorCode: KnotErrorCode.InvalidAlias.ToString()
            );
        }

        // 4. Verifica artifact disponibile per OS/arch corrente
        if (!_artifactResolver.IsArtifactAvailable(remoteVersion))
        {
            return new InstallationPrepareResult(
                Success: false,
                Alias: finalAlias,
                Version: remoteVersion.Version,
                InstallationPath: string.Empty,
                ErrorMessage: $"Artifact non disponibile per {_platform.GetCurrentOs()}/{_platform.GetCurrentArch()}",
                ErrorCode: KnotErrorCode.ArtifactNotAvailable.ToString()
            );
        }

        // 5. Preflight checks
        await PreflightCheckAsync();

        // 6. Download artifact
        var downloadUrl = _artifactResolver.GetArtifactDownloadUrl(remoteVersion.Version);
        var checksumUrl = _artifactResolver.GetChecksumFileUrl(remoteVersion.Version);
        var artifactFileName = _artifactResolver.GetArtifactFileName(remoteVersion.Version);
        
        var downloadPath = Path.Combine(_pathService.GetCachePath(), artifactFileName);

        // Fetch checksum
        var expectedChecksum = await _downloadService.FetchChecksumAsync(
            checksumUrl,
            artifactFileName,
            cancellationToken
        );

        if (string.IsNullOrEmpty(expectedChecksum))
        {
            return new InstallationPrepareResult(
                Success: false,
                Alias: finalAlias,
                Version: remoteVersion.Version,
                InstallationPath: string.Empty,
                ErrorMessage: "Impossibile recuperare checksum da SHASUMS256.txt",
                ErrorCode: KnotErrorCode.DownloadFailed.ToString()
            );
        }

        // Download con checksum verification
        var downloadResult = await _downloadService.DownloadFileAsync(
            downloadUrl,
            downloadPath,
            expectedChecksum,
            progressCallback,
            timeoutSeconds: 600, // 10 minuti
            cancellationToken
        );

        if (!downloadResult.Success || downloadResult.LocalFilePath == null)
        {
            return new InstallationPrepareResult(
                Success: false,
                Alias: finalAlias,
                Version: remoteVersion.Version,
                InstallationPath: string.Empty,
                ErrorMessage: downloadResult.ErrorMessage ?? "Download fallito",
                ErrorCode: downloadResult.ErrorCode
            );
        }

        // 7. Extract archivio
        var tempExtractPath = Path.Combine(_pathService.GetCachePath(), $"extract_{Guid.NewGuid():N}");
        
        var extractResult = await _archiveExtractor.ExtractAsync(
            downloadResult.LocalFilePath,
            tempExtractPath,
            preservePermissions: true,
            cancellationToken
        );

        if (!extractResult.Success || extractResult.ExtractedPath == null)
        {
            _fileSystem.DeleteDirectoryIfExists(tempExtractPath);
            return new InstallationPrepareResult(
                Success: false,
                Alias: finalAlias,
                Version: remoteVersion.Version,
                InstallationPath: string.Empty,
                ErrorMessage: extractResult.ErrorMessage ?? "Estrazione fallita",
                ErrorCode: extractResult.ErrorCode
            );
        }

        // 8. Trova directory root estratta (es: node-v20.11.0-win-x64/)
        var extractedDirs = _fileSystem.GetDirectories(tempExtractPath);
        if (extractedDirs.Length == 0)
        {
            _fileSystem.DeleteDirectoryIfExists(tempExtractPath);
            return new InstallationPrepareResult(
                Success: false,
                Alias: finalAlias,
                Version: remoteVersion.Version,
                InstallationPath: string.Empty,
                ErrorMessage: "Nessuna directory trovata nell'archivio estratto",
                ErrorCode: KnotErrorCode.CorruptedArchive.ToString()
            );
        }

        var extractedRootDir = extractedDirs[0];

        // 9. Muovi in installazione finale
        var finalInstallPath = GetInstallationPath(finalAlias);

        // Rimuovi installazione esistente se force
        if (forceReinstall && _fileSystem.DirectoryExists(finalInstallPath))
        {
            _fileSystem.DeleteDirectoryIfExists(finalInstallPath);
        }

        try
        {
            // Muovi directory estratta in versions/{alias}
            Directory.Move(extractedRootDir, finalInstallPath);
        }
        catch (Exception ex)
        {
            // Cleanup sicuro senza mascherare l'errore originale
            try
            {
                _fileSystem.DeleteDirectoryIfExists(tempExtractPath);
                // Se Directory.Move ha creato parzialmente la destinazione, ripuliamola
                _fileSystem.DeleteDirectoryIfExists(finalInstallPath);
            }
            catch
            {
                // Ignora errori di cleanup - l'errore importante è quello originale
            }
            
            return new InstallationPrepareResult(
                Success: false,
                Alias: finalAlias,
                Version: remoteVersion.Version,
                InstallationPath: string.Empty,
                ErrorMessage: $"Errore spostamento installazione: {ex.Message}",
                ErrorCode: KnotErrorCode.InstallationFailed.ToString()
            );
        }
        finally
        {
            // Cleanup temp extract (sicuro - ignora errori)
            try
            {
                _fileSystem.DeleteDirectoryIfExists(tempExtractPath);
            }
            catch
            {
                // Ignora errori di cleanup in finally
            }
        }

        // 10. Verifica installazione
        if (!VerifyInstallation(finalAlias))
        {
            RollbackInstallation(finalAlias);
            return new InstallationPrepareResult(
                Success: false,
                Alias: finalAlias,
                Version: remoteVersion.Version,
                InstallationPath: finalInstallPath,
                ErrorMessage: "Verifica integrità installazione fallita",
                ErrorCode: KnotErrorCode.InstallationFailed.ToString()
            );
        }

        // 11. Aggiungi al repository
        _installationsRepo.Add(new Installation(
            Alias: finalAlias,
            Version: remoteVersion.Version,
            Path: finalInstallPath,
            Use: false
        ));

        return new InstallationPrepareResult(
            Success: true,
            Alias: finalAlias,
            Version: remoteVersion.Version,
            InstallationPath: finalInstallPath
        );
    }

    public bool IsInstalled(string alias)
    {
        var installPath = GetInstallationPath(alias);
        return _fileSystem.DirectoryExists(installPath);
    }

    public bool RemoveInstallation(string alias, bool force = false)
    {
        if (!IsInstalled(alias))
            return false;

        var installPath = GetInstallationPath(alias);

        try
        {
            _fileSystem.DeleteDirectoryIfExists(installPath);
            _installationsRepo.Remove(alias);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetInstallationPath(string alias)
    {
        return _pathService.GetInstallationPath(alias);
    }

    public bool VerifyInstallation(string alias)
    {
        var installPath = GetInstallationPath(alias);
        if (!_fileSystem.DirectoryExists(installPath))
            return false;

        var nodeExePath = _pathService.GetNodeExecutablePath(installPath);
        return _processRunner.IsExecutableAccessible(nodeExePath);
    }

    public string? GetInstalledVersion(string alias)
    {
        if (!IsInstalled(alias))
            return null;

        var installPath = GetInstallationPath(alias);
        var nodeExePath = _pathService.GetNodeExecutablePath(installPath);

        return _processRunner.GetNodeVersion(nodeExePath);
    }

    public async Task<bool> PreflightCheckAsync(long estimatedSizeBytes = 50_000_000)
    {
        // 1. Check OS/arch supportati
        if (!_platform.IsOsSupported())
        {
            throw new KnotVMException(
                KnotErrorCode.UnsupportedOs,
                $"Sistema operativo {_platform.GetCurrentOs()} non supportato"
            );
        }

        if (!_platform.IsArchSupported())
        {
            throw new KnotVMException(
                KnotErrorCode.UnsupportedArch,
                $"Architettura {_platform.GetCurrentArch()} non supportata"
            );
        }

        // 2. Check permessi scrittura su directories
        var basePath = _pathService.GetBasePath();
        if (!_fileSystem.CanWrite(basePath))
        {
            throw new KnotVMException(
                KnotErrorCode.InsufficientPermissions,
                $"Permessi scrittura insufficienti su {basePath}"
            );
        }

        // 3. Check spazio disco (stima: 3x dimensione archivio per sicurezza)
        var versionsPath = _pathService.GetVersionsPath();
        _fileSystem.EnsureDirectoryExists(versionsPath);

        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(versionsPath) ?? "C:\\");
            var requiredSpace = estimatedSizeBytes * 3;

            if (driveInfo.AvailableFreeSpace < requiredSpace)
            {
                throw new KnotVMException(
                    KnotErrorCode.PathCreationFailed,
                    $"Spazio disco insufficiente. Richiesto: {requiredSpace / 1024 / 1024}MB, Disponibile: {driveInfo.AvailableFreeSpace / 1024 / 1024}MB"
                );
            }
        }
        catch (ArgumentException)
        {
            // DriveInfo fallback su path non validi, skip check
        }

        // 4. Check connettività remota (tenta fetch versioni)
        try
        {
            await _remoteVersions.GetAvailableVersionsAsync(forceRefresh: false);
        }
        catch (KnotVMException ex) when (ex.ErrorCode == KnotErrorCode.RemoteApiFailed)
        {
            throw new KnotVMException(
                KnotErrorCode.RemoteApiFailed,
                "Impossibile connettersi a nodejs.org. Verifica connessione internet."
            );
        }

        return true;
    }

    public void RollbackInstallation(string alias)
    {
        var installPath = GetInstallationPath(alias);
        _fileSystem.DeleteDirectoryIfExists(installPath);
        _installationsRepo.Remove(alias);
    }
}
