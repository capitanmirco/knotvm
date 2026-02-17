using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio filesystem con gestione UTF-8 senza BOM e chmod +x.
/// </summary>
public class FileSystemService : IFileSystemService
{
    private readonly IPlatformService _platform;
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public FileSystemService(IPlatformService platform)
    {
        _platform = platform;
    }

    public string ReadAllTextSafe(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File non trovato: {filePath}");

        var content = File.ReadAllText(filePath, Encoding.UTF8);
        
        // Rimuovi BOM UTF-8 se presente (EF BB BF)
        if (content.Length > 0 && content[0] == '\uFEFF')
            content = content.Substring(1);

        return content.Trim();
    }

    public void WriteAllTextSafe(string filePath, string content)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(filePath, content, Utf8NoBom);
    }

    public string[] ReadAllLinesSafe(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File non trovato: {filePath}");

        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        
        // Se prima riga ha BOM, rimuovilo
        if (lines.Length > 0 && lines[0].Length > 0 && lines[0][0] == '\uFEFF')
            lines[0] = lines[0].Substring(1);

        return lines;
    }

    public bool FileExists(string filePath) => File.Exists(filePath);

    public bool DirectoryExists(string directoryPath) => Directory.Exists(directoryPath);

    public void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);
    }

    public void DeleteFileIfExists(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        const int maxRetries = 3;
        const int delayMs = 100;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Rimuovi attributo read-only se presente
                var fileInfo = new FileInfo(filePath);
                if ((fileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    fileInfo.Attributes &= ~FileAttributes.ReadOnly;
                }

                File.Delete(filePath);
                return; // Successo
            }
            catch (UnauthorizedAccessException) when (attempt < maxRetries)
            {
                // File potrebbe essere locked, attendi e riprova
                Thread.Sleep(delayMs * (attempt + 1));
            }
            catch (IOException) when (attempt < maxRetries)
            {
                // File in uso, attendi e riprova
                Thread.Sleep(delayMs * (attempt + 1));
            }
        }

        // Ultimo tentativo senza catch - lancia l'eccezione originale
        File.Delete(filePath);
    }

    public void DeleteDirectoryIfExists(string directoryPath, bool recursive = true)
    {
        if (!Directory.Exists(directoryPath))
            return;

        const int maxRetries = 3;
        const int delayMs = 100;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Prima di eliminare, prova a rimuovere attributi read-only/hidden
                if (recursive)
                {
                    RemoveReadOnlyAttributes(directoryPath);
                }

                Directory.Delete(directoryPath, recursive);
                return; // Successo
            }
            catch (UnauthorizedAccessException) when (attempt < maxRetries)
            {
                // File potrebbe essere locked, attendi e riprova
                Thread.Sleep(delayMs * (attempt + 1));
            }
            catch (IOException) when (attempt < maxRetries)
            {
                // File in uso, attendi e riprova
                Thread.Sleep(delayMs * (attempt + 1));
            }
        }

        // Ultimo tentativo senza catch - lancia l'eccezione originale
        Directory.Delete(directoryPath, recursive);
    }

    private void RemoveReadOnlyAttributes(string directoryPath)
    {
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);
            
            // Rimuovi attributi read-only dalla directory stessa
            if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
            }

            // Rimuovi attributi read-only da tutti i file
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                if ((file.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }
            }

            // Rimuovi attributi read-only da tutte le subdirectory
            foreach (var dir in dirInfo.GetDirectories("*", SearchOption.AllDirectories))
            {
                if ((dir.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    dir.Attributes &= ~FileAttributes.ReadOnly;
                }
            }
        }
        catch
        {
            // Ignora errori nella rimozione attributi - può fallire ma non è critico
        }
    }

    public void CopyFile(string sourceFile, string destinationFile, bool overwrite = false)
    {
        var destDir = Path.GetDirectoryName(destinationFile);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        File.Copy(sourceFile, destinationFile, overwrite);
    }

    public void CopyDirectory(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Directory sorgente non trovata: {sourceDir}");

        Directory.CreateDirectory(destinationDir);

        // Copia file
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destinationDir, fileName);
            File.Copy(file, destFile, overwrite: true);
        }

        // Copia subdirectory ricorsivamente
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            var destSubDir = Path.Combine(destinationDir, dirName);
            CopyDirectory(dir, destSubDir);
        }
    }

    public void SetExecutablePermissions(string filePath)
    {
        // Windows: no-op
        if (_platform.GetCurrentOs() == HostOs.Windows)
            return;

        // Linux/macOS: usa File.SetUnixFileMode (.NET 6+)
        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File non trovato: {filePath}");

#pragma warning disable CA1416 // Validate platform compatibility
            // Ottieni permessi correnti e aggiungi execute
            var currentMode = File.GetUnixFileMode(filePath);
            var newMode = currentMode 
                | UnixFileMode.UserExecute 
                | UnixFileMode.GroupExecute 
                | UnixFileMode.OtherExecute;
            
            File.SetUnixFileMode(filePath, newMode);
#pragma warning restore CA1416
        }
        catch (PlatformNotSupportedException)
        {
            // Fallback a chmod su piattaforme che non supportano UnixFileMode
            FallbackToChmod(filePath);
        }
        catch (Exception ex)
        {
            throw new IOException($"Impossibile impostare permessi eseguibili su {filePath}", ex);
        }
    }

    private void FallbackToChmod(string filePath)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                throw new IOException($"Impossibile avviare chmod per {filePath}");
            
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                throw new IOException($"Errore chmod +x: {error}");
            }
        }
        catch (Exception ex)
        {
            throw new IOException($"Fallback chmod fallito per {filePath}", ex);
        }
    }

    public long GetFileSize(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File non trovato: {filePath}");

        return new FileInfo(filePath).Length;
    }

    public DateTime GetFileLastWriteTime(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File non trovato: {filePath}");

        return File.GetLastWriteTime(filePath);
    }

    public string[] GetFiles(string directoryPath, string searchPattern = "*")
    {
        if (!Directory.Exists(directoryPath))
            return Array.Empty<string>();

        return Directory.GetFiles(directoryPath, searchPattern);
    }

    public string[] GetDirectories(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return Array.Empty<string>();

        return Directory.GetDirectories(directoryPath);
    }

    public string GetTempDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"knot_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }

    public bool CanRead(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                return true;
            }
            
            if (Directory.Exists(path))
            {
                Directory.GetFiles(path);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool CanWrite(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var stream = File.OpenWrite(path);
                return true;
            }
            
            if (Directory.Exists(path))
            {
                var testFile = Path.Combine(path, $".knot_write_test_{Guid.NewGuid():N}");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
