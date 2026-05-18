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
            // Usa ArgumentList per evitare command injection su archivePath
            var result = await _processRunner.RunAsync("tar", new[] { "-t", "-z", "-f", archivePath }, timeoutMilliseconds: 300_000);

            if (result.ExitCode != 0)
                throw new IOException($"Errore listare tar.gz: {result.StandardError}");

            return result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToArray();
        }

        if (archivePath.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
        {
            // Usa ArgumentList per evitare command injection su archivePath
            var result = await _processRunner.RunAsync("tar", new[] { "-t", "-J", "-f", archivePath }, timeoutMilliseconds: 300_000);

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

                // Zip Slip prevention: il separatore finale garantisce che
                // "/tmp/safe" non accetti path come "/tmp/safehouse/evil".
                // Usa Ordinal (case-sensitive) per correttezza su filesystem case-sensitive (Linux/macOS).
                var fullDestPath = Path.GetFullPath(destinationPath);
                var fullDestDir = Path.GetFullPath(destinationDirectory)
                                      .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                  + Path.DirectorySeparatorChar;
                if (!fullDestPath.StartsWith(fullDestDir, StringComparison.Ordinal))
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
        // BuildTarArgs gestisce le differenze GNU tar (Linux) vs BSD tar (macOS):
        // - GNU tar: usa --no-absolute-filenames per prevenire path traversal
        // - BSD tar (macOS): stripping path assoluti è il default, flag non supportato
        var args = BuildTarArgs("-z", archivePath, destinationDirectory, preservePermissions);
        var result = await _processRunner.RunAsync("tar", args, timeoutMilliseconds: 300_000);

        if (result.ExitCode != 0)
            throw new IOException($"Errore estrazione tar.gz: {result.StandardError}");

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
        // BuildTarArgs gestisce le differenze GNU tar (Linux) vs BSD tar (macOS):
        // - GNU tar: usa --no-absolute-filenames per prevenire path traversal
        // - BSD tar (macOS): stripping path assoluti è il default, flag non supportato
        var args = BuildTarArgs("-J", archivePath, destinationDirectory, preservePermissions);
        var result = await _processRunner.RunAsync("tar", args, timeoutMilliseconds: 300_000);

        if (result.ExitCode != 0)
            throw new IOException($"Errore estrazione tar.xz: {result.StandardError}");

        var files = _fileSystem.GetFiles(destinationDirectory, "*");
        var dirs = _fileSystem.GetDirectories(destinationDirectory);
        return files.Length + dirs.Length;
    }

    /// <summary>
    /// Costruisce gli argomenti tar in base all'OS.
    /// GNU tar (Linux): usa --no-absolute-filenames e --no-same-permissions.
    /// BSD tar (macOS): non supporta quei flag; strip path assoluti è il default,
    ///                  e -p controlla esplicitamente la preservazione dei permessi.
    /// </summary>
    private string[] BuildTarArgs(string formatFlag, string archivePath, string destinationDirectory, bool preservePermissions)
    {
        var isMacOs = _platform.GetCurrentOs() == HostOs.MacOS;

        if (isMacOs)
        {
            // BSD tar: absolute paths stripped by default (no --no-absolute-filenames needed).
            // -p preserves permissions; omitting it applies the current umask.
            return preservePermissions
                ? new[] { "-p", "-x", formatFlag, "-f", archivePath, "-C", destinationDirectory }
                : new[] { "-x", formatFlag, "-f", archivePath, "-C", destinationDirectory };
        }

        // GNU tar (Linux)
        return preservePermissions
            ? new[] { "--no-absolute-filenames", "-x", formatFlag, "-f", archivePath, "-C", destinationDirectory }
            : new[] { "--no-absolute-filenames", "--no-same-permissions", "-x", formatFlag, "-f", archivePath, "-C", destinationDirectory };
    }
}
