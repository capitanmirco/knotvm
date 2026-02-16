using System.Runtime.InteropServices;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;

namespace KnotVM.Core.Common;

/// <summary>
/// Configurazione base per gestori versioni Node.js.
/// Supporta env var KNOT_HOME per path personalizzato.
/// </summary>
/// <param name="AppDataPath">Path root applicazione</param>
/// <param name="VersionsPath">Path installazioni</param>
/// <param name="BinPath">Path binari e proxy</param>
/// <param name="CachePath">Path cache download</param>
/// <param name="SettingsFile">File installazione attiva (settings.txt)</param>
/// <param name="TemplatesPath">Path template proxy</param>
/// <param name="LocksPath">Path directory lock files</param>
public record Configuration(
    string AppDataPath,
    string VersionsPath,
    string BinPath,
    string CachePath,
    string SettingsFile,
    string TemplatesPath,
    string LocksPath)
{
    /// <summary>
    /// Env var per override path base.
    /// </summary>
    public const string KnotHomeEnvVar = "KNOT_HOME";

    /// <summary>
    /// Nome directory default (compatibilità node-local).
    /// </summary>
    public const string DefaultDirectoryName = "node-local";

    /// <summary>
    /// Inizializza le directory necessarie.
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        try
        {
            Directory.CreateDirectory(AppDataPath);
            Directory.CreateDirectory(VersionsPath);
            Directory.CreateDirectory(BinPath);
            Directory.CreateDirectory(CachePath);
            Directory.CreateDirectory(TemplatesPath);
            Directory.CreateDirectory(LocksPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new KnotVMException(
                KnotErrorCode.InsufficientPermissions,
                $"Permessi insufficienti per creare directory KnotVM in '{AppDataPath}'",
                ex
            );
        }
        catch (Exception ex)
        {
            throw new KnotVMException(
                KnotErrorCode.PathCreationFailed,
                $"Impossibile creare directory KnotVM in '{AppDataPath}'",
                ex
            );
        }
    }

    /// <summary>
    /// Determina il path base con supporto KNOT_HOME.
    /// Priorità: KNOT_HOME env var -> default OS-specific (.NET gestisce cross-platform)
    /// </summary>
    private static string GetBasePath()
    {
        // 1. Check KNOT_HOME env var
        var knotHome = Environment.GetEnvironmentVariable(KnotHomeEnvVar);
        if (!string.IsNullOrWhiteSpace(knotHome))
        {
            return knotHome.Trim();
        }

        // 2. Default OS-specific
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows: %APPDATA%\node-local
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, DefaultDirectoryName);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux: $HOME/.local/share/node-local
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".local", "share", DefaultDirectoryName);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: $HOME/Library/Application Support/node-local
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", DefaultDirectoryName);
        }
        else
        {
            // Fallback generico (dovrebbe essere catturato da platform validation)
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".local", "share", DefaultDirectoryName);
        }
    }

    /// <summary>
    /// Crea configurazione per OS corrente con supporto KNOT_HOME.
    /// </summary>
    public static Configuration Create()
    {
        var basePath = GetBasePath();
        
        return new Configuration(
            AppDataPath: basePath,
            VersionsPath: Path.Combine(basePath, "versions"),
            BinPath: Path.Combine(basePath, "bin"),
            CachePath: Path.Combine(basePath, "cache"),
            SettingsFile: Path.Combine(basePath, "settings.txt"),
            TemplatesPath: Path.Combine(basePath, "templates"),
            LocksPath: Path.Combine(basePath, "locks")
        );
    }

    /// <summary>
    /// Crea configurazione e converte eventuali errori in KnotVMException tipizzata.
    /// </summary>
    public static Configuration CreateOrThrow()
    {
        try
        {
            return Create();
        }
        catch (Exception ex) when (ex is not KnotVMException)
        {
            throw new KnotVMException(
                KnotErrorCode.PathCreationFailed,
                "Impossibile creare configurazione KnotVM",
                ex
            );
        }
    }

    /// <summary>
    /// Crea configurazione per node-local (compatibilità sviluppo Windows).
    /// DEPRECATED: Usare Create() che gestisce automaticamente OS e KNOT_HOME.
    /// </summary>
    [Obsolete("Usare Configuration.Create() che gestisce OS e KNOT_HOME automaticamente")]
    public static Configuration ForNodeLocal()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var rootPath = Path.Combine(appDataPath, DefaultDirectoryName);
        
        return new Configuration(
            AppDataPath: rootPath,
            VersionsPath: Path.Combine(rootPath, "versions"),
            BinPath: Path.Combine(rootPath, "bin"),
            CachePath: Path.Combine(rootPath, "cache"),
            SettingsFile: Path.Combine(rootPath, "settings.txt"),
            TemplatesPath: Path.Combine(rootPath, "templates"),
            LocksPath: Path.Combine(rootPath, "locks")
        );
    }

    /// <summary>
    /// Istanza singleton con detection OS automatica e supporto KNOT_HOME.
    /// </summary>
    private static readonly Lazy<Configuration> _instance = new(() => CreateOrThrow());

    /// <summary>
    /// Ottiene l'istanza singleton di Configuration.
    /// </summary>
    public static Configuration Instance => _instance.Value;
}
