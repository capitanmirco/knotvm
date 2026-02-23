using KnotVM.CLI.Commands;
using KnotVM.Core.Common;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Repositories;
using KnotVM.Infrastructure.Services;
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
        
        // HttpClient con configurazione
        services.AddSingleton(sp =>
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "KnotVM/1.0");
            return httpClient;
        });
        
        // Core services
        services.AddSingleton<IPlatformService, PlatformService>();
        services.AddSingleton<IPathService, PathService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        
        // Remote/Download services
        services.AddSingleton<IRemoteVersionService, RemoteVersionService>();
        services.AddSingleton<INodeArtifactResolver, NodeArtifactResolver>();
        services.AddSingleton<IDownloadService, DownloadService>();
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
        
        return services;
    }
}
