using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Common;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Servizio per sincronizzazione proxy cross-platform.
/// </summary>
public class SyncService : ISyncService
{
    private readonly IProxyGeneratorService _proxyGenerator;
    private readonly IInstallationsRepository _installationsRepo;
    private readonly IPathService _paths;
    private readonly IFileSystemService _fileSystem;
    private readonly IPlatformService _platform;

    // Package manager minimi supportati
    private static readonly string[] PackageManagers = 
    {
        "npm", "npx", "yarn", "yarnpkg", "pnpm", "ni", "nun", "nup", "bun"
    };

    public SyncService(
        IProxyGeneratorService proxyGenerator,
        IInstallationsRepository installationsRepo,
        IPathService paths,
        IFileSystemService fileSystem,
        IPlatformService platform)
    {
        _proxyGenerator = proxyGenerator;
        _installationsRepo = installationsRepo;
        _paths = paths;
        _fileSystem = fileSystem;
        _platform = platform;
    }

    public void Sync(bool force = false)
    {
        try
        {
            // KnotVM usa solo isolated mode (prefisso nlocal-)
            
            // Se force, rimuovi tutti i proxy esistenti
            if (force)
            {
                _proxyGenerator.RemoveAllProxies();
            }

            // Ottieni installazione attiva
            var currentInstallation = _installationsRepo.GetAll()
                .FirstOrDefault(i => i.Use);
            
            if (currentInstallation == null)
            {
                // Nessuna installazione attiva: rimuovi tutti i proxy
                _proxyGenerator.RemoveAllProxies();
                return;
            }

            // Genera proxy per node
            GenerateNodeProxy();

            // Genera proxy per package manager
            GeneratePackageManagerProxies(currentInstallation.Path);
            
            // Genera wrapper senza prefisso per comodità
            GenerateUnprefixedWrappers();
        }
        catch (Exception ex) when (ex is not KnotVMException)
        {
            throw new KnotVMException(
                KnotErrorCode.SyncFailed,
                $"Errore durante sincronizzazione proxy: {ex.Message} | StackTrace: {ex.StackTrace}",
                ex);
        }
    }

    public bool IsSyncNeeded()
    {
        try
        {
            var binPath = _paths.GetBinPath();
            if (!_fileSystem.DirectoryExists(binPath))
                return true;

            // KnotVM usa solo isolated mode: verifica presenza proxy core.
            var nodeProxyName = ProxyNaming.BuildIsolatedProxyName("node");
            var nodeProxyPath = _platform.GetCurrentOs() == HostOs.Windows
                ? Path.Combine(binPath, $"{nodeProxyName}.cmd")
                : Path.Combine(binPath, nodeProxyName);

            if (!_fileSystem.FileExists(nodeProxyPath))
                return true;

            // Verifica presenza proxy npm
            var npmProxyName = ProxyNaming.BuildIsolatedProxyName("npm");
            var npmProxyPath = _platform.GetCurrentOs() == HostOs.Windows
                ? Path.Combine(binPath, $"{npmProxyName}.cmd")
                : Path.Combine(binPath, npmProxyName);

            return !_fileSystem.FileExists(npmProxyPath);
        }
        catch
        {
            // In caso di errore, assumiamo che sia necessaria la sync
            return true;
        }
    }

    #region Private Methods

    private void GenerateNodeProxy()
    {
        var commandName = "node";
        var commandExe = _platform.GetCurrentOs() == HostOs.Windows
            ? "node.exe"
            : "bin/node";

        // KnotVM usa solo isolated mode: genera nlocal-node
        _proxyGenerator.GenerateGenericProxy(commandName, commandExe);
        
        // Note: Node shim C# non più necessario (solo isolated mode)
    }

    private void GeneratePackageManagerProxies(string installationPath)
    {
        foreach (var pm in PackageManagers)
        {
            // Risolvi path reale script package manager nell'installazione
            if (!TryResolvePackageManagerScriptPath(installationPath, pm, out var scriptPath))
                continue;
            
            // KnotVM usa solo isolated mode: genera nlocal-<pm>
            _proxyGenerator.GeneratePackageManagerProxy(pm, scriptPath);
        }
    }

    private bool TryResolvePackageManagerScriptPath(string installationPath, string packageManager, out string scriptPath)
    {
        string[] candidates = _platform.GetCurrentOs() == HostOs.Windows
            ? [
                $"{packageManager}.cmd",
                Path.Combine("node_modules", ".bin", $"{packageManager}.cmd")
            ]
            : [
                Path.Combine("bin", packageManager),
                Path.Combine("node_modules", ".bin", packageManager)
            ];

        foreach (var candidate in candidates)
        {
            if (_fileSystem.FileExists(Path.Combine(installationPath, candidate)))
            {
                scriptPath = candidate;
                return true;
            }
        }

        scriptPath = string.Empty;
        return false;
    }
    
    private void GenerateUnprefixedWrappers()
    {
        // Crea wrapper senza prefisso per comodità utente (node, npm, npx)
        // Questi wrapper semplicemente chiamano i proxy isolati nlocal-*
        
        var binPath = _paths.GetBinPath();
        var commands = new[] { "node", "npm", "npx", "corepack" };
        
        if (_platform.GetCurrentOs() == HostOs.Windows)
        {
            foreach (var cmd in commands)
            {
                var wrapperPath = Path.Combine(binPath, $"{cmd}.cmd");
                var isolatedProxy = ProxyNaming.BuildIsolatedProxyName(cmd);
                var wrapperContent = $"@echo off\r\n{isolatedProxy}.cmd %*\r\n";
                
                try
                {
                    _fileSystem.WriteAllTextSafe(wrapperPath, wrapperContent);
                }
                catch
                {
                    // Ignora errori su wrapper opzionali
                }
            }
        }
        else
        {
            foreach (var cmd in commands)
            {
                var wrapperPath = Path.Combine(binPath, cmd);
                var isolatedProxy = ProxyNaming.BuildIsolatedProxyName(cmd);
                var wrapperContent = $"#!/bin/bash\nexec {isolatedProxy} \"$@\"\n";
                
                try
                {
                    _fileSystem.WriteAllTextSafe(wrapperPath, wrapperContent);
                    // Rendi eseguibile su Unix
                    _fileSystem.SetExecutablePermissions(wrapperPath);
                }
                catch
                {
                    // Ignora errori su wrapper opzionali
                }
            }
        }
    }

    #endregion
}
