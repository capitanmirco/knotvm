namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per gestione path con supporto KNOT_HOME.
/// </summary>
public interface IPathService
{
    /// <summary>
    /// Ottiene il path base KnotVM (gestisce KNOT_HOME env var).
    /// Windows default: %APPDATA%\KnotVM (directory "node-local" per compatibilità)
    /// Linux default: $HOME/.local/share/KnotVM (directory "node-local" per compatibilità)
    /// macOS default: $HOME/Library/Application Support/KnotVM (directory "node-local" per compatibilità)
    /// </summary>
    string GetBasePath();

    /// <summary>
    /// Ottiene il path directory versions.
    /// </summary>
    string GetVersionsPath();

    /// <summary>
    /// Ottiene il path directory bin (proxy e shim).
    /// </summary>
    string GetBinPath();

    /// <summary>
    /// Ottiene il path directory cache download.
    /// </summary>
    string GetCachePath();

    /// <summary>
    /// Ottiene il path directory templates.
    /// </summary>
    string GetTemplatesPath();

    /// <summary>
    /// Ottiene il path directory locks.
    /// </summary>
    string GetLocksPath();

    /// <summary>
    /// Ottiene il path file settings.txt (alias attivo).
    /// </summary>
    string GetSettingsFilePath();

    /// <summary>
    /// Ottiene il path completo di una installazione per alias.
    /// </summary>
    /// <param name="alias">Alias installazione</param>
    string GetInstallationPath(string alias);

    /// <summary>
    /// Ottiene il path eseguibile node per una installazione.
    /// Windows: <install>/node.exe
    /// Linux/macOS: <install>/bin/node
    /// </summary>
    /// <param name="installationPath">Path installazione</param>
    string GetNodeExecutablePath(string installationPath);

    /// <summary>
    /// Combina path in modo OS-aware (gestisce separator corretto).
    /// </summary>
    string CombinePaths(params string[] paths);

    /// <summary>
    /// Normalizza path per OS corrente (separator, case, etc).
    /// </summary>
    string NormalizePath(string path);

    /// <summary>
    /// Verifica se due path sono equivalenti considerando case-sensitivity OS.
    /// </summary>
    bool ArePathsEquivalent(string path1, string path2);
}
