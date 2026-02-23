using System.Text.Json;
using System.Text.RegularExpressions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione di <see cref="IVersionFileDetector"/> che legge file di configurazione
/// versione Node.js dal filesystem (.nvmrc, .node-version, package.json).
/// </summary>
public class VersionFileDetectorService(IFileSystemService fileSystem) : IVersionFileDetector
{
    private readonly IFileSystemService _fileSystem = fileSystem;

    /// <inheritdoc/>
    public async Task<string?> DetectVersionAsync(string directory)
    {
        // Ordine di precedenza: .nvmrc > .node-version > package.json
        var nvmrcPath = Path.Combine(directory, ".nvmrc");
        if (_fileSystem.FileExists(nvmrcPath))
        {
            return await ReadNvmrcAsync(nvmrcPath).ConfigureAwait(false);
        }

        var nodeVersionPath = Path.Combine(directory, ".node-version");
        if (_fileSystem.FileExists(nodeVersionPath))
        {
            return await ReadNodeVersionAsync(nodeVersionPath).ConfigureAwait(false);
        }

        var packageJsonPath = Path.Combine(directory, "package.json");
        if (_fileSystem.FileExists(packageJsonPath))
        {
            return await ReadPackageJsonEnginesAsync(packageJsonPath).ConfigureAwait(false);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<ProjectContext> DetectProjectContextAsync(string directory)
    {
        string? version = null;
        string? projectName = null;

        // Priorità versione: package.json > .nvmrc > .node-version
        var packageJsonPath = Path.Combine(directory, "package.json");
        if (_fileSystem.FileExists(packageJsonPath))
        {
            version = await ReadPackageJsonEnginesAsync(packageJsonPath).ConfigureAwait(false);
            projectName = await ReadPackageJsonNameAsync(packageJsonPath).ConfigureAwait(false);
        }

        if (version == null)
        {
            var nvmrcPath = Path.Combine(directory, ".nvmrc");
            if (_fileSystem.FileExists(nvmrcPath))
            {
                version = await ReadNvmrcAsync(nvmrcPath).ConfigureAwait(false);
            }
        }

        if (version == null)
        {
            var nodeVersionPath = Path.Combine(directory, ".node-version");
            if (_fileSystem.FileExists(nodeVersionPath))
            {
                version = await ReadNodeVersionAsync(nodeVersionPath).ConfigureAwait(false);
            }
        }

        return new ProjectContext(version, projectName);
    }

    /// <inheritdoc/>
    public Task<string?> ReadNvmrcAsync(string filePath)
    {
        return Task.Run<string?>(() =>
        {
            var content = _fileSystem.ReadAllTextSafe(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return null;
            var version = content.Trim();
            // Strip leading 'v' prefix (e.g. "v18.2.0" → "18.2.0") for nvm compatibility
            if (version.Length > 1 && (version[0] == 'v' || version[0] == 'V'))
                version = version[1..];
            return string.IsNullOrWhiteSpace(version) ? null : version;
        });
    }

    /// <inheritdoc/>
    public Task<string?> ReadNodeVersionAsync(string filePath)
    {
        return Task.Run<string?>(() =>
        {
            var content = _fileSystem.ReadAllTextSafe(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return null;
            var version = content.Trim();
            // Strip leading 'v' prefix (e.g. "v18.2.0" → "18.2.0") for fnm compatibility
            if (version.Length > 1 && (version[0] == 'v' || version[0] == 'V'))
                version = version[1..];
            return string.IsNullOrWhiteSpace(version) ? null : version;
        });
    }

    /// <inheritdoc/>
    public Task<string?> ReadPackageJsonEnginesAsync(string filePath)
    {
        return Task.Run<string?>(() =>
        {
            var content = _fileSystem.ReadAllTextSafe(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return null;

            try
            {
                using var json = JsonDocument.Parse(content);
                if (json.RootElement.TryGetProperty("engines", out var engines) &&
                    engines.TryGetProperty("node", out var nodeVersion))
                {
                    return nodeVersion.GetString();
                }

                return null;
            }
            catch (JsonException ex)
            {
                throw new KnotVMException(
                    KnotErrorCode.InvalidVersionFormat,
                    $"Il file package.json non è JSON valido: {ex.Message}");
            }
        });
    }

    /// <inheritdoc/>
    public Task<string?> ReadPackageJsonNameAsync(string filePath)
    {
        return Task.Run<string?>(() =>
        {
            var content = _fileSystem.ReadAllTextSafe(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return null;

            try
            {
                using var json = JsonDocument.Parse(content);
                if (!json.RootElement.TryGetProperty("name", out var nameProp))
                    return null;

                var rawName = nameProp.GetString();
                return string.IsNullOrWhiteSpace(rawName) ? null : SanitizeAlias(rawName);
            }
            catch (JsonException)
            {
                // Name read is non-critical: malformed JSON here means engines read will throw too
                return null;
            }
        });
    }

    /// <summary>
    /// Normalizza un nome di progetto npm in un alias valido per KnotVM.
    /// Rimuove il prefisso di scope (@org/), sostituisce caratteri non ammessi con trattino,
    /// rimuove trattini iniziali/finali e tronca a 50 caratteri.
    /// </summary>
    private static string SanitizeAlias(string rawName)
    {
        // Rimuovi scope @org/
        var name = Regex.Replace(rawName, @"^@[^/]+/", string.Empty);

        // Sostituisci caratteri non validi (non alfanumerici, non - _) con -
        name = Regex.Replace(name, @"[^a-zA-Z0-9_-]", "-");

        // Rimuovi trattini multipli consecutivi
        name = Regex.Replace(name, @"-{2,}", "-");

        // Rimuovi trattini iniziali/finali
        name = name.Trim('-');

        // Lunghezza massima alias = 50
        if (name.Length > 50)
            name = name[..50].TrimEnd('-');

        return string.IsNullOrEmpty(name) ? "project" : name;
    }
}
