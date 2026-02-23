using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio gestione versione attiva e settings.txt.
/// </summary>
public class VersionManager(IPathService pathService, IFileSystemService fileSystem, IInstallationsRepository installationsRepo) : IVersionManager
{
    public string? GetActiveAlias() => ReadSettingsFile();

    public Installation? GetActiveInstallation()
    {
        var activeAlias = GetActiveAlias();
        return string.IsNullOrEmpty(activeAlias) 
            ? null 
            : installationsRepo.GetAll().FirstOrDefault(i => i.Alias.Equals(activeAlias, StringComparison.OrdinalIgnoreCase));
    }

    public bool UseVersion(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        var installation = installationsRepo.GetAll()
            .FirstOrDefault(i => i.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase))
            ?? throw new KnotVMException(KnotErrorCode.InstallationNotFound, $"Installazione '{alias}' non trovata");

        UpdateSettingsFile(alias);
        installationsRepo.SetActiveInstallation(alias);
        return true;
    }

    public void UnuseVersion()
    {
        UpdateSettingsFile(null);
        
        foreach (var installation in installationsRepo.GetAll().Where(i => i.Use))
        {
            installationsRepo.Update(installation with { Use = false });
        }
    }

    public bool IsAliasActive(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return false;

        var activeAlias = GetActiveAlias();
        return !string.IsNullOrEmpty(activeAlias) && activeAlias.Equals(alias, StringComparison.OrdinalIgnoreCase);
    }

    public void UpdateSettingsFile(string? alias)
    {
        var settingsPath = pathService.GetSettingsFilePath();

        if (string.IsNullOrWhiteSpace(alias))
        {
            fileSystem.DeleteFileIfExists(settingsPath);
            return;
        }

        try
        {
            fileSystem.WriteAllTextSafe(settingsPath, alias.Trim());
        }
        catch (Exception ex)
        {
            throw new KnotVMException(KnotErrorCode.PathCreationFailed, 
                $"Impossibile aggiornare settings.txt: {ex.Message}", ex);
        }
    }

    public string? ReadSettingsFile()
    {
        var settingsPath = pathService.GetSettingsFilePath();

        if (!fileSystem.FileExists(settingsPath))
            return null;

        try
        {
            var content = fileSystem.ReadAllTextSafe(settingsPath);
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (Exception ex)
        {
            throw new KnotVMException(KnotErrorCode.CorruptedSettingsFile, 
                $"Errore lettura settings.txt: {ex.Message}", ex);
        }
    }
}
