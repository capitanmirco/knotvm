namespace KnotVM.Core.Common;

/// <summary>
/// Configurazione base per gestori versioni Node.js.
/// </summary>
/// <param name="AppDataPath">Path root applicazione</param>
/// <param name="VersionsPath">Path installazioni</param>
/// <param name="BinPath">Path binari e proxy</param>
/// <param name="CachePath">Path cache ZIP</param>
/// <param name="SettingsFile">File installazione attiva</param>
/// <param name="TemplatesPath">Path template proxy</param>
/// <param name="LockFile">File lock concurrency</param>
public record Configuration(
    string AppDataPath,
    string VersionsPath,
    string BinPath,
    string CachePath,
    string SettingsFile,
    string TemplatesPath,
    string LockFile)
{
    /// <summary>
    /// Inizializza le directory necessarie.
    /// </summary>
    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(AppDataPath);
        Directory.CreateDirectory(VersionsPath);
        Directory.CreateDirectory(BinPath);
        Directory.CreateDirectory(CachePath);
        Directory.CreateDirectory(TemplatesPath);
    }

    /// <summary>
    /// Crea configurazione per KnotVM (futuro - non ancora usata).
    /// </summary>
    public static Configuration ForKnot()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var rootPath = Path.Combine(appDataPath, "knotvm");
        
        return new Configuration(
            AppDataPath: rootPath,
            VersionsPath: Path.Combine(rootPath, "versions"),
            BinPath: Path.Combine(rootPath, "bin"),
            CachePath: Path.Combine(rootPath, "cache"),
            SettingsFile: Path.Combine(rootPath, "settings.txt"),
            TemplatesPath: Path.Combine(rootPath, "bin", "templates"),
            LockFile: Path.Combine(rootPath, ".lock")
        );
    }

    /// <summary>
    /// Crea configurazione per node-local (compatibilità durante sviluppo).
    /// </summary>
    public static Configuration ForNodeLocal()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var rootPath = Path.Combine(appDataPath, "node-local");
        
        return new Configuration(
            AppDataPath: rootPath,
            VersionsPath: Path.Combine(rootPath, "versions"),
            BinPath: Path.Combine(rootPath, "bin"),
            CachePath: Path.Combine(rootPath, "cache"),
            SettingsFile: Path.Combine(rootPath, "settings.txt"),
            TemplatesPath: Path.Combine(rootPath, "bin", "templates"),
            LockFile: Path.Combine(rootPath, ".lock")
        );
    }

    /// <summary>
    /// Istanza singleton (attualmente usa node-local).
    /// </summary>
    private static readonly Lazy<Configuration> _instance = 
        new(() => ForNodeLocal()); // TODO: Cambiare in ForKnot() per il rilascio

    /// <summary>
    /// Ottiene l'istanza singleton di Configuration.
    /// </summary>
    public static Configuration Instance => _instance.Value;
}
