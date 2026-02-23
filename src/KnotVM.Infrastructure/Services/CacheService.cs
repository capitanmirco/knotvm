using KnotVM.Core.Common;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio gestione cache download artifact.
/// </summary>
public class CacheService : ICacheService
{
    private readonly Configuration _configuration;
    private readonly string _cachePath;

    public CacheService(Configuration configuration)
    {
        _configuration = configuration;
        _cachePath = configuration.CachePath;
        
        // Assicura che la directory cache esista
        if (!Directory.Exists(_cachePath))
        {
            Directory.CreateDirectory(_cachePath);
        }
    }

    public (string FileName, long SizeBytes, DateTime ModifiedDate)[] ListCacheFiles()
    {
        if (!Directory.Exists(_cachePath))
        {
            return Array.Empty<(string, long, DateTime)>();
        }

        var files = Directory.GetFiles(_cachePath);
        return files
            .Select(f =>
            {
                var info = new FileInfo(f);
                return (Path.GetFileName(f), info.Length, info.LastWriteTime);
            })
            .OrderByDescending(f => f.Item3) // Ordina per data più recente
            .ToArray();
    }

    public long GetCacheSizeBytes()
    {
        if (!Directory.Exists(_cachePath))
        {
            return 0;
        }

        var files = Directory.GetFiles(_cachePath);
        return files.Sum(f => new FileInfo(f).Length);
    }

    public void ClearCache()
    {
        if (!Directory.Exists(_cachePath))
        {
            return;
        }

        var files = Directory.GetFiles(_cachePath);
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignora errori su file singoli (potrebbero essere in uso)
            }
        }
    }

    public void CleanCache(int olderThanDays = 30)
    {
        if (!Directory.Exists(_cachePath))
        {
            return;
        }

        var cutoffDate = DateTime.Now.AddDays(-olderThanDays);
        var files = Directory.GetFiles(_cachePath);
        
        foreach (var file in files)
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime < cutoffDate)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignora errori su file singoli
            }
        }
    }

    public bool ExistsInCache(string fileName)
    {
        var filePath = Path.Combine(_cachePath, fileName);
        return File.Exists(filePath);
    }

    public string? GetCachedFilePath(string fileName)
    {
        var filePath = Path.Combine(_cachePath, fileName);
        return File.Exists(filePath) ? filePath : null;
    }
}
