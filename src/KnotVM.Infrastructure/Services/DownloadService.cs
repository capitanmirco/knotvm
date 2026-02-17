using System.Security.Cryptography;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio download con checksum SHA256 e progress tracking.
/// </summary>
public class DownloadService : IDownloadService
{
    private const int DefaultTimeoutSeconds = 300; // 5 minuti
    private const int MaxRetryAttempts = 3;
    private const int RetryBaseDelayMilliseconds = 500;
    private const int BufferSize = 8192;

    private readonly IFileSystemService _fileSystem;
    private readonly HttpClient _httpClient;

    public DownloadService(IFileSystemService fileSystem, HttpClient httpClient)
    {
        _fileSystem = fileSystem;
        _httpClient = httpClient;
    }

    public async Task<DownloadResult> DownloadFileAsync(
        string url,
        string destinationPath,
        string? expectedSha256 = null,
        IProgress<DownloadProgress>? progressCallback = null,
        int timeoutSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        // Validazione HTTPS only
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new DownloadResult(
                Success: false,
                LocalFilePath: null,
                BytesDownloaded: 0,
                ChecksumVerified: false,
                ErrorMessage: "Solo URL HTTPS sono supportati per sicurezza",
                ErrorCode: KnotErrorCode.DownloadFailed.ToString()
            );
        }

        var timeout = timeoutSeconds > 0 ? timeoutSeconds : DefaultTimeoutSeconds;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeout));

        // Crea directory destinazione
        var destDir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(destDir))
            _fileSystem.EnsureDirectoryExists(destDir);

        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            long bytesDownloaded = 0;
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;

                using (var contentStream = await response.Content.ReadAsStreamAsync(cts.Token))
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
                {
                    var buffer = new byte[BufferSize];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                        bytesDownloaded += bytesRead;

                        if (progressCallback != null && totalBytes > 0)
                        {
                            var percent = (int)((bytesDownloaded * 100) / totalBytes);
                            progressCallback.Report(new DownloadProgress(bytesDownloaded, totalBytes, percent));
                        }
                    }

                    await fileStream.FlushAsync(cts.Token);
                } // FileStream è ora completamente chiuso

                // Verifica checksum se fornito (dopo che il file è chiuso)
                var checksumVerified = false;
                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    checksumVerified = await VerifyChecksumAsync(destinationPath, expectedSha256);

                    if (!checksumVerified)
                    {
                        _fileSystem.DeleteFileIfExists(destinationPath);

                        if (attempt < MaxRetryAttempts)
                        {
                            await DelayBeforeRetryAsync(attempt, cts.Token);
                            continue;
                        }

                        return new DownloadResult(
                            Success: false,
                            LocalFilePath: null,
                            BytesDownloaded: bytesDownloaded,
                            ChecksumVerified: false,
                            ErrorMessage: "Checksum SHA256 non corrisponde",
                            ErrorCode: KnotErrorCode.ChecksumMismatch.ToString()
                        );
                    }
                }

                return new DownloadResult(
                    Success: true,
                    LocalFilePath: destinationPath,
                    BytesDownloaded: bytesDownloaded,
                    ChecksumVerified: checksumVerified
                );
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _fileSystem.DeleteFileIfExists(destinationPath);
                return new DownloadResult(
                    Success: false,
                    LocalFilePath: null,
                    BytesDownloaded: bytesDownloaded,
                    ChecksumVerified: false,
                    ErrorMessage: "Download annullato",
                    ErrorCode: KnotErrorCode.DownloadFailed.ToString()
                );
            }
            catch (TaskCanceledException) when (attempt < MaxRetryAttempts)
            {
                _fileSystem.DeleteFileIfExists(destinationPath);
                await DelayBeforeRetryAsync(attempt, cts.Token);
            }
            catch (HttpRequestException) when (attempt < MaxRetryAttempts)
            {
                _fileSystem.DeleteFileIfExists(destinationPath);
                await DelayBeforeRetryAsync(attempt, cts.Token);
            }
            catch (IOException) when (attempt < MaxRetryAttempts)
            {
                _fileSystem.DeleteFileIfExists(destinationPath);
                await DelayBeforeRetryAsync(attempt, cts.Token);
            }
            catch (TaskCanceledException)
            {
                _fileSystem.DeleteFileIfExists(destinationPath);
                return new DownloadResult(
                    Success: false,
                    LocalFilePath: null,
                    BytesDownloaded: bytesDownloaded,
                    ChecksumVerified: false,
                    ErrorMessage: $"Download timeout dopo {timeout} secondi",
                    ErrorCode: KnotErrorCode.DownloadFailed.ToString()
                );
            }
            catch (HttpRequestException ex)
            {
                _fileSystem.DeleteFileIfExists(destinationPath);
                return new DownloadResult(
                    Success: false,
                    LocalFilePath: null,
                    BytesDownloaded: bytesDownloaded,
                    ChecksumVerified: false,
                    ErrorMessage: $"Errore HTTP: {ex.Message}",
                    ErrorCode: KnotErrorCode.DownloadFailed.ToString()
                );
            }
            catch (Exception ex)
            {
                _fileSystem.DeleteFileIfExists(destinationPath);
                return new DownloadResult(
                    Success: false,
                    LocalFilePath: null,
                    BytesDownloaded: bytesDownloaded,
                    ChecksumVerified: false,
                    ErrorMessage: $"Errore download: {ex.Message}",
                    ErrorCode: KnotErrorCode.DownloadFailed.ToString()
                );
            }
        }

        _fileSystem.DeleteFileIfExists(destinationPath);
        return new DownloadResult(
            Success: false,
            LocalFilePath: null,
            BytesDownloaded: 0,
            ChecksumVerified: false,
            ErrorMessage: $"Download fallito dopo {MaxRetryAttempts} tentativi",
            ErrorCode: KnotErrorCode.DownloadFailed.ToString()
        );
    }

    public async Task<string?> FetchChecksumAsync(
        string checksumUrl,
        string artifactFileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await _httpClient.GetStringAsync(checksumUrl, cancellationToken);
            
            // SHASUMS256.txt format: "checksum  filename"
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var checksum = parts[0].Trim().ToLowerInvariant();
                    var fileName = parts[1].Trim();

                    if (fileName.Equals(artifactFileName, StringComparison.OrdinalIgnoreCase))
                        return checksum;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string> ComputeSha256Async(string filePath)
    {
        if (!_fileSystem.FileExists(filePath))
            throw new FileNotFoundException($"File non trovato: {filePath}");

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        using var sha256 = SHA256.Create();
        
        var hashBytes = await sha256.ComputeHashAsync(stream);
        
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public async Task<bool> VerifyChecksumAsync(string filePath, string expectedSha256)
    {
        var actual = await ComputeSha256Async(filePath);
        var expected = expectedSha256.Trim().ToLowerInvariant();
        
        return actual.Equals(expected, StringComparison.Ordinal);
    }

    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delayMs = RetryBaseDelayMilliseconds * (int)Math.Pow(2, Math.Max(0, attempt - 1));
        return Task.Delay(delayMs, cancellationToken);
    }
}
