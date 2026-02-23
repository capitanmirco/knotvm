namespace KnotVM.Core.Interfaces;

/// <summary>
/// Servizio per gestione cache download artifact.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Ottiene informazioni sui file nella cache.
    /// </summary>
    /// <returns>Array di tuple (nome file, dimensione bytes, data modifica)</returns>
    (string FileName, long SizeBytes, DateTime ModifiedDate)[] ListCacheFiles();

    /// <summary>
    /// Calcola la dimensione totale della cache.
    /// </summary>
    /// <returns>Dimensione totale in bytes</returns>
    long GetCacheSizeBytes();

    /// <summary>
    /// Elimina tutti i file nella cache.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Elimina file cache obsoleti o invalidi.
    /// Criteri: file più vecchi di N giorni o checksum non corrispondente.
    /// </summary>
    /// <param name="olderThanDays">Giorni oltre i quali file sono considerati obsoleti (default: 30)</param>
    void CleanCache(int olderThanDays = 30);

    /// <summary>
    /// Verifica se esiste un file in cache.
    /// </summary>
    /// <param name="fileName">Nome file da cercare</param>
    /// <returns>True se file esiste</returns>
    bool ExistsInCache(string fileName);

    /// <summary>
    /// Ottiene il path completo di un file in cache se esiste.
    /// </summary>
    /// <param name="fileName">Nome file</param>
    /// <returns>Path completo o null se non esiste</returns>
    string? GetCachedFilePath(string fileName);
}
