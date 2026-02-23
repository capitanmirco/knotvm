using System.Text.Json;
using System.Text.Json.Serialization;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio versioni remote da nodejs.org.
/// Copia locale dell'index salvata in KNOT_HOME/versions-index.json.
/// </summary>
public class RemoteVersionService : IRemoteVersionService
{
    private const string NodeDistIndexUrl = "https://nodejs.org/dist/index.json";
    private const int CacheExpiryMinutes = 60; // Cache valida per 1 ora

    private readonly IFileSystemService _fileSystem;
    private readonly IPathService _pathService;
    private readonly HttpClient _httpClient;
    private readonly string _indexFilePath;
    private RemoteVersion[]? _memoryCache;
    private DateTime? _memoryCacheTimestamp;

    public RemoteVersionService(IFileSystemService fileSystem, IPathService pathService, HttpClient httpClient)
    {
        _fileSystem = fileSystem;
        _pathService = pathService;
        _httpClient = httpClient;
        _indexFilePath = Path.Combine(_pathService.GetBasePath(), "versions-index.json");
    }

    public async Task<RemoteVersion[]> GetAvailableVersionsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        // Check memory cache
        if (!forceRefresh && _memoryCache != null && _memoryCacheTimestamp.HasValue)
        {
            var age = DateTime.UtcNow - _memoryCacheTimestamp.Value;
            if (age.TotalMinutes < CacheExpiryMinutes)
                return _memoryCache;
        }

        // Check index file locale
        if (!forceRefresh && _fileSystem.FileExists(_indexFilePath))
        {
            var indexAge = DateTime.UtcNow - _fileSystem.GetFileLastWriteTime(_indexFilePath).ToUniversalTime();
            if (indexAge.TotalMinutes < CacheExpiryMinutes)
            {
                try
                {
                    var cachedJson = _fileSystem.ReadAllTextSafe(_indexFilePath);
                    var versions = ParseVersionsJson(cachedJson);
                    _memoryCache = versions;
                    _memoryCacheTimestamp = DateTime.UtcNow;
                    return versions;
                }
                catch
                {
                    // Index corrotto, procedi con fetch remoto
                }
            }
        }

        // Fetch da remoto
        try
        {
            var json = await _httpClient.GetStringAsync(NodeDistIndexUrl, cancellationToken);
            var versions = ParseVersionsJson(json);

            // Salva index locale in KNOT_HOME
            _fileSystem.EnsureDirectoryExists(_pathService.GetBasePath());
            _fileSystem.WriteAllTextSafe(_indexFilePath, json);

            _memoryCache = versions;
            _memoryCacheTimestamp = DateTime.UtcNow;

            return versions;
        }
        catch (HttpRequestException ex)
        {
            throw new KnotVMException(
                KnotErrorCode.RemoteApiFailed,
                $"Impossibile connettersi a nodejs.org: {ex.Message}"
            );
        }
    }

    public async Task<RemoteVersion[]> GetLtsVersionsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var all = await GetAvailableVersionsAsync(forceRefresh, cancellationToken);
        return all.Where(v => !string.IsNullOrEmpty(v.Lts)).ToArray();
    }

    public async Task<RemoteVersion?> GetLatestLtsVersionAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var lts = await GetLtsVersionsAsync(forceRefresh, cancellationToken);
        return lts.FirstOrDefault(); // Index.json è già ordinato per versione descending
    }

    public async Task<RemoteVersion?> ResolveVersionAsync(string versionPattern, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(versionPattern))
            return null;

        var pattern = versionPattern.Trim().ToLowerInvariant().TrimStart('v');

        // Alias special
        if (pattern == "latest")
        {
            var all = await GetAvailableVersionsAsync(forceRefresh, cancellationToken);
            return all.FirstOrDefault();
        }

        if (pattern == "lts")
            return await GetLatestLtsVersionAsync(forceRefresh, cancellationToken);

        var versions = await GetAvailableVersionsAsync(forceRefresh, cancellationToken);

        // Match esatto
        var exact = versions.FirstOrDefault(v => v.Version.Equals(pattern, StringComparison.OrdinalIgnoreCase));
        if (exact != null)
            return exact;

        // Partial match: "20" → "20.x.x", "20.11" → "20.11.x"
        var partial = versions.FirstOrDefault(v => v.Version.StartsWith(pattern + ".", StringComparison.OrdinalIgnoreCase));
        if (partial != null)
            return partial;

        // LTS codename match: "iron" → versione LTS Iron
        var ltsCodename = versions.FirstOrDefault(v => 
            !string.IsNullOrEmpty(v.Lts) && 
            v.Lts.Equals(pattern, StringComparison.OrdinalIgnoreCase)
        );

        return ltsCodename;
    }

    public void ClearCache()
    {
        _memoryCache = null;
        _memoryCacheTimestamp = null;
        _fileSystem.DeleteFileIfExists(_indexFilePath);
    }

    private RemoteVersion[] ParseVersionsJson(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var items = JsonSerializer.Deserialize<NodeDistIndexItem[]>(json, options);
            
            if (items == null || items.Length == 0)
                throw new KnotVMException(KnotErrorCode.RemoteApiFailed, "Index.json vuoto o non valido");

            return items.Select(item => new RemoteVersion(
                Version: item.Version?.TrimStart('v') ?? string.Empty,
                Lts: item.Lts is JsonElement ltsElement && ltsElement.ValueKind == JsonValueKind.String 
                    ? ltsElement.GetString() 
                    : null,
                Date: item.Date ?? string.Empty,
                Files: item.Files ?? Array.Empty<string>()
            )).ToArray();
        }
        catch (JsonException ex)
        {
            throw new KnotVMException(
                KnotErrorCode.RemoteApiFailed,
                $"Errore parsing index.json: {ex.Message}"
            );
        }
    }

    // DTO per deserializzazione JSON da nodejs.org/dist/index.json
    private class NodeDistIndexItem
    {
        public string? Version { get; set; }
        public JsonElement Lts { get; set; } // Può essere false (bool) o "Iron" (string)
        public string? Date { get; set; }
        public string[]? Files { get; set; }
    }
}
