using KnotVM.Core.Common;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio path management con supporto KNOT_HOME.
/// </summary>
public class PathService : IPathService
{
    private readonly IPlatformService _platform;
    private readonly Configuration _config;

    public PathService(IPlatformService platform, Configuration config)
    {
        _platform = platform;
        _config = config;
    }

    public string GetBasePath() => _config.AppDataPath;

    public string GetVersionsPath() => _config.VersionsPath;

    public string GetBinPath() => _config.BinPath;

    public string GetCachePath() => _config.CachePath;

    public string GetTemplatesPath() => _config.TemplatesPath;

    public string GetLocksPath() => _config.LocksPath;

    public string GetSettingsFilePath() => _config.SettingsFile;

    public string GetInstallationPath(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias non può essere vuoto", nameof(alias));

        return Path.Combine(_config.VersionsPath, alias);
    }

    public string GetNodeExecutablePath(string installationPath)
    {
        if (string.IsNullOrWhiteSpace(installationPath))
            throw new ArgumentException("Installation path non può essere vuoto", nameof(installationPath));

        // Windows: installationPath\node.exe
        // Linux/macOS: installationPath/bin/node
        var exeName = $"node{_platform.GetExecutableExtension()}";
        
        return _platform.GetCurrentOs() == Core.Enums.HostOs.Windows
            ? Path.Combine(installationPath, exeName)
            : Path.Combine(installationPath, "bin", exeName);
    }

    public string CombinePaths(params string[] paths)
    {
        if (paths == null || paths.Length == 0)
            throw new ArgumentException("Almeno un path richiesto", nameof(paths));

        return Path.Combine(paths);
    }

    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        // Normalizza separatori al platform-specific
        var normalized = path.Replace('/', Path.DirectorySeparatorChar)
                             .Replace('\\', Path.DirectorySeparatorChar);

        // GetFullPath risolve anche . e ..
        try
        {
            return Path.GetFullPath(normalized);
        }
        catch
        {
            // Se path non valido, ritorna normalizzato senza GetFullPath
            return normalized;
        }
    }

    public bool ArePathsEquivalent(string path1, string path2)
    {
        if (string.IsNullOrWhiteSpace(path1) && string.IsNullOrWhiteSpace(path2))
            return true;

        if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2))
            return false;

        var normalized1 = NormalizePath(path1);
        var normalized2 = NormalizePath(path2);

        var comparison = _platform.IsPathCaseSensitive()
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        return string.Equals(normalized1, normalized2, comparison);
    }
}
