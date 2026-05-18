using KnotVM.CLI.Commands;
using KnotVM.Core.Common;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Repositories;
using KnotVM.Infrastructure.Services;
using KnotVM.Infrastructure.Services.VersionResolution;
using Microsoft.Extensions.DependencyInjection;

namespace KnotVM.CLI.Extensions;

/// <summary>
/// Extension methods per configurazione servizi DI in modo organizzato.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra tutti i servizi core, infrastructure e management di KnotVM.
    /// </summary>
    public static IServiceCollection AddKnotVMServices(this IServiceCollection services)
    {
        // Configuration
        var configuration = Configuration.Instance;
        configuration.EnsureDirectoriesExist();
        services.AddSingleton(configuration);
        
        // HttpClient tramite IHttpClientFactory: evita DNS blindness e socket exhaustion
        // del pattern singleton manuale. Timeout e User-Agent configurati centralmente.
        services.AddHttpClient<IRemoteVersionService, RemoteVersionService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Add("User-Agent", "KnotVM/1.0");
        });
        
        // Core services
        services.AddSingleton<IPlatformService, PlatformService>();
        services.AddSingleton<IPathService, PathService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        
        // Remote/Download services
        // Nota: IRemoteVersionService è già registrato tramite AddHttpClient<> sopra.
        services.AddSingleton<INodeArtifactResolver, NodeArtifactResolver>();
        
        // ARC-05: registra DownloadService tramite factory per evitare DNS staleness
        services.AddHttpClient<IDownloadService, DownloadService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Add("User-Agent", "KnotVM/1.0");
        });

        services.AddSingleton<IArchiveExtractor, ArchiveExtractor>();
        
        // Installation/Versioning services
        services.AddSingleton<ILockManager, LockManager>();
        services.AddSingleton<IVersionManager, VersionManager>();
        services.AddSingleton<IInstallationService, InstallationService>();
        
        // Proxy/Sync services
        services.AddSingleton<IProxyGeneratorService, ProxyGeneratorService>();
        services.AddSingleton<ISyncService, SyncService>();
        
        // Version file detection
        services.AddSingleton<IVersionFileDetector, VersionFileDetectorService>();
        
        // Management services
        services.AddSingleton<IInstallationManager, InstallationManager>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<ICompletionGenerator, CompletionGeneratorService>();
        
        // Repository
        services.AddSingleton<IInstallationsRepository, LocalInstallationsRepository>();

        // Doctor/diagnostics
        services.AddSingleton<IDoctorService, DoctorService>();

        // Upgrade/downgrade
        services.AddSingleton<IUpgradeDowngradeService, UpgradeDowngradeService>();

        // Version resolution strategies (ordine determina priorità di matching)
        services.AddSingleton<IVersionResolutionStrategy, ExactVersionStrategy>();  // 1. Semver esatto
        services.AddSingleton<IVersionResolutionStrategy, AliasStrategy>();          // 2. Alias installato
        services.AddSingleton<IVersionResolutionStrategy, MajorVersionStrategy>();   // 3. Versione maggiore
        services.AddSingleton<IVersionResolutionStrategy, LtsVersionStrategy>();     // 4. Keyword LTS
        services.AddSingleton<IVersionResolutionStrategy, KeywordStrategy>();        // 5. latest/current
        services.AddSingleton<IVersionResolutionStrategy, CodenameStrategy>();       // 6. Codename LTS
        services.AddSingleton<IVersionResolver, VersionResolverService>();

        return services;
    }
    
    /// <summary>
    /// Registra tutti i comandi CLI di KnotVM.
    /// </summary>
    public static IServiceCollection AddKnotVMCommands(this IServiceCollection services)
    {
        services.AddSingleton<ListCommand>();
        services.AddSingleton<ListRemoteCommand>();
        services.AddSingleton<InstallCommand>();
        services.AddSingleton<UseCommand>();
        services.AddSingleton<SyncCommand>();
        services.AddSingleton<RemoveCommand>();
        services.AddSingleton<RenameCommand>();
        services.AddSingleton<RunCommand>();
        services.AddSingleton<CacheCommand>();
        services.AddSingleton<VersionCommand>();
        services.AddSingleton<AutoDetectCommand>();
        services.AddSingleton<CompletionCommand>();
        services.AddSingleton<DoctorCommand>();
        services.AddSingleton<UpgradeCommand>();
        services.AddSingleton<DowngradeCommand>();
        
        return services;
    }
}
