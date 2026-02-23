namespace KnotVM.Core.Models;

/// <summary>
/// Rappresenta il progresso di un download.
/// </summary>
/// <param name="BytesDownloaded">Bytes scaricati finora</param>
/// <param name="TotalBytes">Dimensione totale file (0 se sconosciuta)</param>
/// <param name="PercentComplete">Percentuale completamento (0-100, -1 se indeterminato)</param>
public record DownloadProgress(
    long BytesDownloaded,
    long TotalBytes,
    int PercentComplete
);

/// <summary>
/// Risultato di un'operazione di download.
/// </summary>
/// <param name="Success">True se download completato con successo</param>
/// <param name="LocalFilePath">Path file locale scaricato</param>
/// <param name="BytesDownloaded">Bytes totali scaricati</param>
/// <param name="ChecksumVerified">True se checksum verificato con successo</param>
/// <param name="ErrorMessage">Messaggio errore se fallito</param>
/// <param name="ErrorCode">Codice errore se fallito</param>
public record DownloadResult(
    bool Success,
    string? LocalFilePath,
    long BytesDownloaded,
    bool ChecksumVerified,
    string? ErrorMessage = null,
    string? ErrorCode = null
);

/// <summary>
/// Risultato estrazione archivio.
/// </summary>
/// <param name="Success">True se estrazione completata</param>
/// <param name="ExtractedPath">Path directory estratta</param>
/// <param name="FilesExtracted">Numero file estratti</param>
/// <param name="ErrorMessage">Messaggio errore se fallito</param>
/// <param name="ErrorCode">Codice errore se fallito</param>
public record ExtractionResult(
    bool Success,
    string? ExtractedPath,
    int FilesExtracted,
    string? ErrorMessage = null,
    string? ErrorCode = null
);
