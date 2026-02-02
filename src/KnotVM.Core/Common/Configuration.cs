namespace KnotVM.Core.Common;

/// <summary>
/// Configurazione singleton per path e impostazioni di KnotVM.
/// Gestisce tutti i percorsi e le configurazioni globali dell'applicazione.
/// </summary>
public sealed class Configuration
{
    private static readonly Lazy<Configuration> _instance = new(() => new Configuration());

    /// <summary>
    /// Ottiene l'istanza singleton di Configuration.
    /// </summary>
    public static Configuration Instance => _instance.Value;

    private Configuration()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        // TODO: Cambiare "node-local" in "knotvm" per il rilascio finale
        AppDataPath = Path.Combine(appDataPath, "node-local");
        VersionsPath = Path.Combine(AppDataPath, "versions");
        BinPath = Path.Combine(AppDataPath, "bin");
        CachePath = Path.Combine(AppDataPath, "cache");
        SettingsFile = Path.Combine(AppDataPath, "settings.txt");
        TemplatesPath = Path.Combine(BinPath, "templates");
        LockFile = Path.Combine(AppDataPath, ".lock");
    }

    /// <summary>
    /// Path root: %APPDATA%\knotvm
    /// </summary>
    public string AppDataPath { get; }

    /// <summary>
    /// Path installazioni: %APPDATA%\knotvm\versions
    /// </summary>
    public string VersionsPath { get; }

    /// <summary>
    /// Path binari e proxy: %APPDATA%\knotvm\bin
    /// </summary>
    public string BinPath { get; }

    /// <summary>
    /// Path cache ZIP scaricati: %APPDATA%\knotvm\cache
    /// </summary>
    public string CachePath { get; }

    /// <summary>
    /// File con nome installazione attiva: %APPDATA%\knotvm\settings.txt
    /// </summary>
    public string SettingsFile { get; }

    /// <summary>
    /// Path template proxy: %APPDATA%\knotvm\bin\templates
    /// </summary>
    public string TemplatesPath { get; }

    /// <summary>
    /// File lock per concurrency control: %APPDATA%\knotvm\.lock
    /// </summary>
    public string LockFile { get; }

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
}
