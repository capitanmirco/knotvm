using KnotVM.Core.Common;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio path management con supporto KNOT_HOME.
/// </summary>
public class PathService(IPlatformService platform, Configuration config) : IPathService
{
    public string GetBasePath() => config.AppDataPath;
    public string GetVersionsPath() => config.VersionsPath;
    public string GetBinPath() => config.BinPath;
    public string GetCachePath() => config.CachePath;
    public string GetTemplatesPath() => config.TemplatesPath;
    public string GetLocksPath() => config.LocksPath;
    public string GetSettingsFilePath() => config.SettingsFile;

    public string GetInstallationPath(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        return Path.Combine(config.VersionsPath, alias);
    }

    public string GetNodeExecutablePath(string installationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationPath);
        
        var exeName = $"node{platform.GetExecutableExtension()}";
        return platform.GetCurrentOs() == Core.Enums.HostOs.Windows
            ? Path.Combine(installationPath, exeName)
            : Path.Combine(installationPath, "bin", exeName);
    }

    public string CombinePaths(params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Length == 0)
            throw new ArgumentException("Almeno un path richiesto", nameof(paths));

        return Path.Combine(paths);
    }

    public string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var normalized = path.Replace('/', Path.DirectorySeparatorChar)
                             .Replace('\\', Path.DirectorySeparatorChar);

        try
        {
            return Path.GetFullPath(normalized);
        }
        catch
        {
            return normalized;
        }
    }

    public bool ArePathsEquivalent(string path1, string path2) =>
        (string.IsNullOrWhiteSpace(path1), string.IsNullOrWhiteSpace(path2)) switch
        {
            (true, true) => true,
            (true, false) or (false, true) => false,
            _ => string.Equals(NormalizePath(path1), NormalizePath(path2),
                platform.IsPathCaseSensitive() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase)
        };
}
