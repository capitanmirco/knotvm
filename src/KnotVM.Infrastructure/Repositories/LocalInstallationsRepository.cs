using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Repositories;

/// <summary>
/// Repository per gestire le installazioni di Node.js dal filesystem locale.
/// </summary>
public class LocalInstallationsRepository : IInstallationsRepository
{
    private readonly Configuration _config;
    private readonly bool _isWindows;
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystemService _fileSystem;

    public LocalInstallationsRepository(
        Configuration config,
        IPlatformService platformService,
        IProcessRunner processRunner,
        IFileSystemService fileSystem)
    {
        _config = config;
        _isWindows = platformService.GetCurrentOs() == HostOs.Windows;
        _processRunner = processRunner;
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Ottiene tutte le installazioni presenti nel filesystem.
    /// </summary>
    public Installation[] GetAll()
    {
        // Verifica che la directory versions esista
        if (!Directory.Exists(_config.VersionsPath))
        {
            return Array.Empty<Installation>();
        }

        var installations = new List<Installation>();
        
        // Leggi l'alias attivo da settings.txt
        var activeAlias = GetActiveAlias();

        // Enumera tutte le sottocartelle
        var directories = Directory.GetDirectories(_config.VersionsPath);

        foreach (var dir in directories)
        {
            // Verifica se è una installazione valida (contiene node.exe)
            if (!IsValidInstallation(dir))
            {
                continue;
            }

            var alias = Path.GetFileName(dir);
            var version = GetNodeVersion(dir);

            if (version != null)
            {
                var isActive = !string.IsNullOrEmpty(activeAlias) && alias.Equals(activeAlias, StringComparison.OrdinalIgnoreCase);
                installations.Add(new Installation(alias, version, dir, Use: isActive));
            }
        }

        return installations.ToArray();
    }
    
    /// <summary>
    /// Legge il nome dell'alias attivo dal file settings.txt.
    /// </summary>
    /// <returns>Il nome dell'alias attivo o null se non presente</returns>
    private string? GetActiveAlias()
    {
        try
        {
            if (!_fileSystem.FileExists(_config.SettingsFile))
            {
                return null;
            }
            
            var content = _fileSystem.ReadAllTextSafe(_config.SettingsFile).Trim();
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Verifica se una directory contiene una installazione valida di Node.js.
    /// </summary>
    private bool IsValidInstallation(string directoryPath)
    {
        var nodeExePath = GetNodeExecutablePath(directoryPath);
        return _fileSystem.FileExists(nodeExePath);
    }

    /// <summary>
    /// Ottiene la versione di Node.js eseguendo node.exe -v.
    /// </summary>
    /// <returns>Versione (es: "20.11.0") o null se fallisce</returns>
    private string? GetNodeVersion(string directoryPath)
    {
        try
        {
            var nodeExePath = GetNodeExecutablePath(directoryPath);
            return _processRunner.GetNodeVersion(nodeExePath);
        }
        catch
        {
            // Se qualcosa va storto, ritorna null
            return null;
        }
    }

    private string GetNodeExecutablePath(string directoryPath)
    {
        // Windows: directoryPath\node.exe
        // Linux/macOS: directoryPath/bin/node
        return _isWindows
            ? Path.Combine(directoryPath, "node.exe")
            : Path.Combine(directoryPath, "bin", "node");
    }

    public void Add(Installation installation)
    {
        // No-op: il filesystem è già stato modificato da InstallationService
        // Il repository legge direttamente dal disco al prossimo GetAll()
    }

    public void Update(Installation installation)
    {
        // No-op: il repository legge sempre lo stato dal filesystem
    }

    public bool Remove(string alias)
    {
        // No-op: la rimozione fisica è gestita da InstallationService
        return true;
    }

    public void SetActiveInstallation(string alias)
    {
        // Scrivi alias in settings.txt usando FileSystemService per gestione corretta errori
        _fileSystem.WriteAllTextSafe(_config.SettingsFile, alias);
    }

    public Installation? GetByAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return null;
        }

        var installationPath = ResolveInstallationPath(alias);
        if (installationPath == null || !IsValidInstallation(installationPath))
        {
            return null;
        }

        var version = GetNodeVersion(installationPath);
        if (version == null)
        {
            return null;
        }

        var resolvedAlias = Path.GetFileName(installationPath);
        var activeAlias = GetActiveAlias();
        var isActive = !string.IsNullOrEmpty(activeAlias) &&
                       resolvedAlias.Equals(activeAlias, StringComparison.OrdinalIgnoreCase);

        return new Installation(resolvedAlias, version, installationPath, Use: isActive);
    }

    private string? ResolveInstallationPath(string alias)
    {
        if (!Directory.Exists(_config.VersionsPath))
        {
            return null;
        }

        // Fast path: alias with exact casing.
        var directPath = Path.Combine(_config.VersionsPath, alias);
        if (Directory.Exists(directPath))
        {
            return directPath;
        }

        // Fallback case-insensitive lookup.
        foreach (var directory in Directory.GetDirectories(_config.VersionsPath))
        {
            var dirAlias = Path.GetFileName(directory);
            if (dirAlias.Equals(alias, StringComparison.OrdinalIgnoreCase))
            {
                return directory;
            }
        }

        return null;
    }
}
