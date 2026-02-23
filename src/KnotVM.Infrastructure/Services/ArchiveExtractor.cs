using System.IO.Compression;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio estrazione archivi .zip, .tar.gz e .tar.xz.
/// </summary>
public class ArchiveExtractor : IArchiveExtractor
{
    private readonly IPlatformService _platform;
    private readonly IFileSystemService _fileSystem;
    private readonly IProcessRunner _processRunner;

    public ArchiveExtractor(IPlatformService platform, IFileSystemService fileSystem, IProcessRunner processRunner)
    {
        _platform = platform;
        _fileSystem = fileSystem;
        _processRunner = processRunner;
    }

    public async Task<ExtractionResult> ExtractAsync(
        string archivePath,
        string destinationDirectory,
        bool preservePermissions = true,
        CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(archivePath))
        {
            return new ExtractionResult(
                Success: false,
                ExtractedPath: null,
                FilesExtracted: 0,
                ErrorMessage: $"Archivio non trovato: {archivePath}",
                ErrorCode: KnotErrorCode.PathNotFound.ToString()
            );
        }

        if (!IsValidArchive(archivePath))
        {
            return new ExtractionResult(
                Success: false,
                ExtractedPath: null,
                FilesExtracted: 0,
                ErrorMessage: "Formato archivio non supportato. Supportati: .zip, .tar.gz, .tar.xz",
                ErrorCode: KnotErrorCode.InstallationFailed.ToString()
            );
        }

        _fileSystem.EnsureDirectoryExists(destinationDirectory);

        try
        {
            var filesExtracted = await ExtractArchiveInternalAsync(archivePath, destinationDirectory, preservePermissions, cancellationToken);

            return new ExtractionResult(
                Success: true,
                ExtractedPath: destinationDirectory,
                FilesExtracted: filesExtracted
            );
        }
        catch (Exception ex)
        {
            return new ExtractionResult(
                Success: false,
                ExtractedPath: null,
                FilesExtracted: 0,
                ErrorMessage: $"Errore estrazione: {ex.Message}",
                ErrorCode: KnotErrorCode.InstallationFailed.ToString()
            );
        }
    }

    public bool IsValidArchive(string archivePath)
    {
        if (!_fileSystem.FileExists(archivePath))
            return false;

        var extension = Path.GetExtension(archivePath).ToLowerInvariant();
        
        if (extension == ".zip")
            return true;

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            return true;

        if (archivePath.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public async Task<string[]> ListArchiveContentsAsync(string archivePath)
    {
        if (!IsValidArchive(archivePath))
            throw new ArgumentException("Archivio non valido", nameof(archivePath));

        var extension = Path.GetExtension(archivePath).ToLowerInvariant();

        if (extension == ".zip")
        {
            using var archive = ZipFile.OpenRead(archivePath);
            return archive.Entries.Select(e => e.FullName).ToArray();
        }

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            // Usa tar -tzf per listare contenuto
            var result = await _processRunner.RunAsync("tar", $"-tzf \"{archivePath}\"");
            
            if (result.ExitCode != 0)
                throw new IOException($"Errore listare tar.gz: {result.StandardError}");

            return result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToArray();
        }

        if (archivePath.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
        {
            // Usa tar -tJf per listare contenuto
            var result = await _processRunner.RunAsync("tar", $"-tJf \"{archivePath}\"");

            if (result.ExitCode != 0)
                throw new IOException($"Errore listare tar.xz: {result.StandardError}");

            return result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private async Task<int> ExtractArchiveInternalAsync(
        string archivePath,
        string destinationDirectory,
        bool preservePermissions,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(archivePath).ToLowerInvariant();

        if (extension == ".zip")
            return await ExtractZipAsync(archivePath, destinationDirectory, cancellationToken);

        if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            return await ExtractTarGzAsync(archivePath, destinationDirectory, preservePermissions, cancellationToken);

        if (archivePath.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
            return await ExtractTarXzAsync(archivePath, destinationDirectory, preservePermissions, cancellationToken);

        throw new NotSupportedException($"Formato archivio non supportato: {archivePath}");
    }

    private Task<int> ExtractZipAsync(string archivePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(archivePath);
            
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var destinationPath = Path.Combine(destinationDirectory, entry.FullName);

                // Validazione path traversal
                var fullDestPath = Path.GetFullPath(destinationPath);
                var fullDestDir = Path.GetFullPath(destinationDirectory);
                
                if (!fullDestPath.StartsWith(fullDestDir, StringComparison.OrdinalIgnoreCase))
                    throw new IOException($"Path traversal rilevato: {entry.FullName}");

                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    // Directory entry
                    Directory.CreateDirectory(destinationPath);
                }
                else
                {
                    // File entry
                    var fileDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(fileDir))
                        Directory.CreateDirectory(fileDir);

                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }

            return archive.Entries.Count;
        }, cancellationToken);
    }

    private async Task<int> ExtractTarGzAsync(
        string archivePath,
        string destinationDirectory,
        bool preservePermissions,
        CancellationToken cancellationToken)
    {
        // Usa tar command-line per preservare permessi Unix
        var tarArgs = preservePermissions
            ? $"-xzf \"{archivePath}\" -C \"{destinationDirectory}\""
            : $"--no-same-permissions -xzf \"{archivePath}\" -C \"{destinationDirectory}\"";

        var result = await _processRunner.RunAsync("tar", tarArgs);

        if (result.ExitCode != 0)
            throw new IOException($"Errore estrazione tar.gz: {result.StandardError}");

        // Conta file estratti
        var files = _fileSystem.GetFiles(destinationDirectory, "*");
        var dirs = _fileSystem.GetDirectories(destinationDirectory);
        
        return files.Length + dirs.Length;
    }

    private async Task<int> ExtractTarXzAsync(
        string archivePath,
        string destinationDirectory,
        bool preservePermissions,
        CancellationToken cancellationToken)
    {
        // Usa tar command-line per preservare permessi Unix
        var tarArgs = preservePermissions
            ? $"-xJf \"{archivePath}\" -C \"{destinationDirectory}\""
            : $"--no-same-permissions -xJf \"{archivePath}\" -C \"{destinationDirectory}\"";

        var result = await _processRunner.RunAsync("tar", tarArgs);

        if (result.ExitCode != 0)
            throw new IOException($"Errore estrazione tar.xz: {result.StandardError}");

        // Conta file estratti
        var files = _fileSystem.GetFiles(destinationDirectory, "*");
        var dirs = _fileSystem.GetDirectories(destinationDirectory);

        return files.Length + dirs.Length;
    }
}
