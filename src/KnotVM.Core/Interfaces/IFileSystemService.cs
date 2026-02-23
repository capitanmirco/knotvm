using System.Text;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per operazioni filesystem OS-aware con gestione encoding e permessi.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Legge tutto il contenuto di un file di testo con gestione BOM UTF-8.
    /// Rimuove BOM se presente e applica trim.
    /// </summary>
    /// <param name="filePath">Path file</param>
    /// <returns>Contenuto file trimmed</returns>
    string ReadAllTextSafe(string filePath);

    /// <summary>
    /// Scrive contenuto in file di testo UTF-8 senza BOM.
    /// </summary>
    /// <param name="filePath">Path file</param>
    /// <param name="content">Contenuto da scrivere</param>
    void WriteAllTextSafe(string filePath, string content);

    /// <summary>
    /// Legge tutte le linee di un file con gestione BOM.
    /// </summary>
    string[] ReadAllLinesSafe(string filePath);

    /// <summary>
    /// Verifica se un file esiste.
    /// </summary>
    bool FileExists(string filePath);

    /// <summary>
    /// Verifica se una directory esiste.
    /// </summary>
    bool DirectoryExists(string directoryPath);

    /// <summary>
    /// Crea directory se non esiste (ricorsivo).
    /// </summary>
    void EnsureDirectoryExists(string directoryPath);

    /// <summary>
    /// Elimina file se esiste (nessun errore se già assente).
    /// </summary>
    void DeleteFileIfExists(string filePath);

    /// <summary>
    /// Elimina directory ricorsivamente se esiste.
    /// </summary>
    /// <param name="directoryPath">Path directory</param>
    /// <param name="recursive">Se true, elimina ricorsivamente</param>
    void DeleteDirectoryIfExists(string directoryPath, bool recursive = true);

    /// <summary>
    /// Copia file con overwrite opzionale.
    /// </summary>
    void CopyFile(string sourceFile, string destinationFile, bool overwrite = false);

    /// <summary>
    /// Copia directory ricorsivamente.
    /// </summary>
    void CopyDirectory(string sourceDir, string destinationDir);

    /// <summary>
    /// Imposta permessi eseguibili su file (Linux/macOS: chmod +x).
    /// Windows: no-op.
    /// </summary>
    void SetExecutablePermissions(string filePath);

    /// <summary>
    /// Ottiene dimensione file in bytes.
    /// </summary>
    long GetFileSize(string filePath);

    /// <summary>
    /// Ottiene data ultima modifica file.
    /// </summary>
    DateTime GetFileLastWriteTime(string filePath);

    /// <summary>
    /// Enumera file in directory con pattern opzionale.
    /// </summary>
    string[] GetFiles(string directoryPath, string searchPattern = "*");

    /// <summary>
    /// Enumera subdirectory in directory.
    /// </summary>
    string[] GetDirectories(string directoryPath);

    /// <summary>
    /// Ottiene path directory temporanea unica.
    /// </summary>
    string GetTempDirectory();

    /// <summary>
    /// Verifica permessi di lettura su path.
    /// </summary>
    bool CanRead(string path);

    /// <summary>
    /// Verifica permessi di scrittura su path.
    /// </summary>
    bool CanWrite(string path);
}
