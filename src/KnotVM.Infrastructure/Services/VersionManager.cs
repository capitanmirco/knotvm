using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio gestione versione attiva e settings.txt.
/// </summary>
public class VersionManager : IVersionManager
{
    private readonly IPathService _pathService;
    private readonly IFileSystemService _fileSystem;
    private readonly IInstallationsRepository _installationsRepo;

    public VersionManager(
        IPathService pathService,
        IFileSystemService fileSystem,
        IInstallationsRepository installationsRepo)
    {
        _pathService = pathService;
        _fileSystem = fileSystem;
        _installationsRepo = installationsRepo;
    }

    public string? GetActiveAlias()
    {
        return ReadSettingsFile();
    }

    public Installation? GetActiveInstallation()
    {
        var activeAlias = GetActiveAlias();
        if (string.IsNullOrEmpty(activeAlias))
            return null;

        var all = _installationsRepo.GetAll();
        return all.FirstOrDefault(i => i.Alias.Equals(activeAlias, StringComparison.OrdinalIgnoreCase));
    }

    public bool UseVersion(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias non può essere vuoto", nameof(alias));

        // Verifica che alias esista
        var all = _installationsRepo.GetAll();
        var installation = all.FirstOrDefault(i => 
            i.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase)
        );

        if (installation == null)
        {
            throw new KnotVMException(
                KnotErrorCode.InstallationNotFound,
                $"Installazione '{alias}' non trovata"
            );
        }

        // Aggiorna settings.txt
        UpdateSettingsFile(alias);

        // Aggiorna flag Use nel repository
        _installationsRepo.SetActiveInstallation(alias);

        return true;
    }

    public void UnuseVersion()
    {
        UpdateSettingsFile(null);
        
        // Rimuovi flag Use da tutte le installazioni
        var all = _installationsRepo.GetAll();
        foreach (var installation in all.Where(i => i.Use))
        {
            _installationsRepo.Update(installation with { Use = false });
        }
    }

    public bool IsAliasActive(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return false;

        var activeAlias = GetActiveAlias();
        return !string.IsNullOrEmpty(activeAlias) && 
               activeAlias.Equals(alias, StringComparison.OrdinalIgnoreCase);
    }

    public void UpdateSettingsFile(string? alias)
    {
        var settingsPath = _pathService.GetSettingsFilePath();

        if (string.IsNullOrWhiteSpace(alias))
        {
            // Rimuovi settings.txt se alias null
            _fileSystem.DeleteFileIfExists(settingsPath);
            return;
        }

        try
        {
            // Scrivi alias in UTF-8 no BOM
            _fileSystem.WriteAllTextSafe(settingsPath, alias.Trim());
        }
        catch (Exception ex)
        {
            throw new KnotVMException(
                KnotErrorCode.PathCreationFailed,
                $"Impossibile aggiornare settings.txt: {ex.Message}",
                ex
            );
        }
    }

    public string? ReadSettingsFile()
    {
        var settingsPath = _pathService.GetSettingsFilePath();

        if (!_fileSystem.FileExists(settingsPath))
            return null;

        try
        {
            var content = _fileSystem.ReadAllTextSafe(settingsPath);
            
            // Ritorna null se vuoto dopo trim
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (Exception ex)
        {
            throw new KnotVMException(
                KnotErrorCode.CorruptedSettingsFile,
                $"Errore lettura settings.txt: {ex.Message}",
                ex
            );
        }
    }
}
