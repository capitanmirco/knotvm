namespace KnotVM.Core.Models;

/// <summary>
/// Rappresenta un file nella cache download.
/// </summary>
/// <param name="FileName">Nome file (es: "node-v20.11.0-win-x64.zip")</param>
/// <param name="FilePath">Path completo file in cache</param>
/// <param name="SizeBytes">Dimensione file in bytes</param>
/// <param name="DownloadDate">Data download (se disponibile)</param>
/// <param name="Verified">True se checksum verificato</param>
public record CacheEntry(
    string FileName,
    string FilePath,
    long SizeBytes,
    DateTime? DownloadDate,
    bool Verified
);
