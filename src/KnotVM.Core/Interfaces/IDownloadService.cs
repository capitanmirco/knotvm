using KnotVM.Core.Models;

namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per download file con checksum verification e progress tracking.
/// </summary>
public interface IDownloadService
{
    /// <summary>
    /// Scarica file da URL con verifica checksum SHA256.
    /// </summary>
    /// <param name="url">URL file da scaricare (HTTPS only)</param>
    /// <param name="destinationPath">Path locale di destinazione</param>
    /// <param name="expectedSha256">Checksum SHA256 atteso (lowercase hex)</param>
    /// <param name="progressCallback">Callback per progress tracking (opzionale)</param>
    /// <param name="timeoutSeconds">Timeout in secondi (0 = default 300s)</param>
    /// <param name="cancellationToken">Token cancellazione</param>
    /// <returns>Risultato download</returns>
    Task<DownloadResult> DownloadFileAsync(
        string url,
        string destinationPath,
        string? expectedSha256 = null,
        IProgress<DownloadProgress>? progressCallback = null,
        int timeoutSeconds = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Scarica e valida checksum file SHASUMS256.txt da nodejs.org.
    /// </summary>
    /// <param name="checksumUrl">URL file SHASUMS256.txt</param>
    /// <param name="artifactFileName">Nome file artifact per cui cercare checksum</param>
    /// <param name="cancellationToken">Token cancellazione</param>
    /// <returns>Checksum SHA256 (lowercase hex) o null se non trovato</returns>
    Task<string?> FetchChecksumAsync(
        string checksumUrl,
        string artifactFileName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Calcola checksum SHA256 di un file locale.
    /// </summary>
    /// <param name="filePath">Path file locale</param>
    /// <returns>Checksum SHA256 (lowercase hex)</returns>
    Task<string> ComputeSha256Async(string filePath);

    /// <summary>
    /// Verifica checksum SHA256 di un file locale.
    /// </summary>
    /// <param name="filePath">Path file locale</param>
    /// <param name="expectedSha256">Checksum atteso (lowercase hex)</param>
    /// <returns>True se checksum corrisponde</returns>
    Task<bool> VerifyChecksumAsync(string filePath, string expectedSha256);
}
