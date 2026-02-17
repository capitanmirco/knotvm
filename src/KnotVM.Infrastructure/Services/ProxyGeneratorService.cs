using System.Text;
using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Servizio per generazione proxy cross-platform da template.
/// </summary>
public class ProxyGeneratorService : IProxyGeneratorService
{
    private static readonly string[] RequiredTemplateFiles =
    [
        "generic-proxy.cmd.template",
        "generic-proxy.bash.template",
        "package-manager.cmd.template",
        "package-manager.bash.template",
        "node-shim.cs.template"
    ];

    private static readonly HashSet<string> UnixWrapperCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "node", "npm", "npx", "corepack"
    };

    private readonly IPlatformService _platform;
    private readonly IPathService _paths;
    private readonly IFileSystemService _fileSystem;
    private readonly string _templateDir;
    private readonly string[] _templateSearchPaths;

    public ProxyGeneratorService(
        IPlatformService platform,
        IPathService paths,
        IFileSystemService fileSystem)
    {
        _platform = platform;
        _paths = paths;
        _fileSystem = fileSystem;

        // Candidate 1: path configurato (runtime/installazione).
        var configuredTemplateDir = _paths.GetTemplatesPath();

        // Candidate 2: fallback repository root (sviluppo/test locale).
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var repositoryTemplateDir = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", "..", "templates"));

        _templateSearchPaths =
        [
            configuredTemplateDir,
            repositoryTemplateDir
        ];

        _templateDir = ResolveTemplateDirectory(_templateSearchPaths) ?? configuredTemplateDir;
    }

    public void GenerateGenericProxy(string commandName, string commandExe)
    {
        try
        {
            EnsureTemplateDirectoryExists();

            var binDir = _paths.GetBinPath();
            Directory.CreateDirectory(binDir);

            // KnotVM usa solo isolated mode.
            var proxyName = ProxyNaming.BuildIsolatedProxyName(commandName);

            if (_platform.GetCurrentOs() == HostOs.Windows)
            {
                GenerateWindowsGenericProxy(proxyName, commandName, commandExe, binDir);
            }
            else
            {
                GenerateUnixGenericProxy(proxyName, commandName, commandExe, binDir);
            }
        }
        catch (Exception ex) when (ex is not KnotVMException)
        {
            throw new KnotVMException(
                KnotErrorCode.ProxyGenerationFailed,
                $"Errore generazione proxy per {commandName}",
                ex);
        }
    }

    public void GeneratePackageManagerProxy(string packageManager, string scriptPath)
    {
        try
        {
            EnsureTemplateDirectoryExists();

            var binDir = _paths.GetBinPath();
            Directory.CreateDirectory(binDir);

            // KnotVM usa solo isolated mode.
            var proxyName = ProxyNaming.BuildIsolatedProxyName(packageManager);

            if (_platform.GetCurrentOs() == HostOs.Windows)
            {
                GenerateWindowsPackageManagerProxy(proxyName, packageManager, scriptPath, binDir);
            }
            else
            {
                GenerateUnixPackageManagerProxy(proxyName, packageManager, scriptPath, binDir);
            }
        }
        catch (Exception ex) when (ex is not KnotVMException)
        {
            throw new KnotVMException(
                KnotErrorCode.ProxyGenerationFailed,
                $"Errore generazione proxy per package manager {packageManager}",
                ex);
        }
    }

    public void GenerateNodeShim()
    {
        if (_platform.GetCurrentOs() != HostOs.Windows)
            return; // Shim solo su Windows

        try
        {
            EnsureTemplateDirectoryExists();

            var templatePath = Path.Combine(_templateDir, "node-shim.cs.template");
            if (!_fileSystem.FileExists(templatePath))
            {
                throw new FileNotFoundException($"Template node-shim.cs.template non trovato in {_templateDir}");
            }

            var template = _fileSystem.ReadAllTextSafe(templatePath);

            var settingsFile = _paths.GetSettingsFilePath();
            var versionsPath = _paths.GetVersionsPath();

            var shimCode = template
                .Replace("{{SETTINGS_FILE}}", EscapeCSharpString(settingsFile))
                .Replace("{{VERSIONS_PATH}}", EscapeCSharpString(versionsPath));

            // Compila lo shim (sarebbe da implementare la compilazione)
            // Per ora salviamo solo il codice sorgente
            var binDir = _paths.GetBinPath();
            var shimSourcePath = Path.Combine(binDir, "node-shim.cs");
            _fileSystem.WriteAllTextSafe(shimSourcePath, shimCode);
        }
        catch (Exception ex)
        {
            throw new KnotVMException(
                KnotErrorCode.ProxyGenerationFailed,
                "Errore generazione node shim",
                ex);
        }
    }

    public void RemoveProxy(string proxyName)
    {
        var binDir = _paths.GetBinPath();
        
        if (_platform.GetCurrentOs() == HostOs.Windows)
        {
            var cmdPath = Path.Combine(binDir, $"{proxyName}.cmd");
            _fileSystem.DeleteFileIfExists(cmdPath);
        }
        else
        {
            var proxyPath = Path.Combine(binDir, proxyName);
            _fileSystem.DeleteFileIfExists(proxyPath);
        }
    }

    public void RemoveAllProxies()
    {
        var binDir = _paths.GetBinPath();
        if (!Directory.Exists(binDir))
            return;

        if (_platform.GetCurrentOs() == HostOs.Windows)
        {
            // Rimuovi tutti i .cmd nella bin directory
            foreach (var cmdFile in Directory.GetFiles(binDir, "*.cmd"))
            {
                try
                {
                    File.Delete(cmdFile);
                }
                catch
                {
                    // Ignora errori su singoli file (potrebbero essere locked)
                }
            }
            
            // Rimuovi anche eventuali shim (ignora errori se locked)
            var shimPath = Path.Combine(binDir, "node.exe");
            try
            {
                _fileSystem.DeleteFileIfExists(shimPath);
            }
            catch
            {
                // File locked o protetto - ignora
            }
        }
        else
        {
            // Rimuovi solo proxy/wrapper gestiti da KnotVM.
            // Non toccare binari estranei presenti nella cartella (es: knot).
            foreach (var file in Directory.GetFiles(binDir))
            {
                if (!IsManagedUnixProxyOrWrapper(file))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignora errori su singoli file
                }
            }
        }
    }

    #region Windows Proxy Generation

    private void GenerateWindowsGenericProxy(string proxyName, string commandName, string commandExe, string binDir)
    {
        var templatePath = Path.Combine(_templateDir, "generic-proxy.cmd.template");
        if (!_fileSystem.FileExists(templatePath))
        {
            throw new FileNotFoundException($"Template generic-proxy.cmd.template non trovato");
        }

        var template = _fileSystem.ReadAllTextSafe(templatePath);
        var settingsFile = _paths.GetSettingsFilePath();
        var versionsPath = _paths.GetVersionsPath();

        var proxyContent = template
            .Replace("{{SETTINGS_FILE}}", settingsFile)
            .Replace("{{VERSIONS_PATH}}", versionsPath)
            .Replace("{{COMMAND_NAME}}", commandName.ToUpperInvariant())
            .Replace("{{COMMAND_EXE}}", commandExe);

        var proxyPath = Path.Combine(binDir, $"{proxyName}.cmd");
        
        // Windows: encoding ASCII, line ending CRLF (default)
        _fileSystem.WriteAllTextSafe(proxyPath, proxyContent);
    }

    private void GenerateWindowsPackageManagerProxy(string proxyName, string packageManager, string scriptPath, string binDir)
    {
        var templatePath = Path.Combine(_templateDir, "package-manager.cmd.template");
        if (!_fileSystem.FileExists(templatePath))
        {
            throw new FileNotFoundException($"Template package-manager.cmd.template non trovato");
        }

        var template = _fileSystem.ReadAllTextSafe(templatePath);
        var settingsFile = _paths.GetSettingsFilePath();
        var versionsPath = _paths.GetVersionsPath();

        var proxyContent = template
            .Replace("{{SETTINGS_FILE}}", settingsFile)
            .Replace("{{VERSIONS_PATH}}", versionsPath)
            .Replace("{{PM_NAME}}", packageManager)
            .Replace("{{SCRIPT_PATH}}", scriptPath);

        var proxyPath = Path.Combine(binDir, $"{proxyName}.cmd");
        _fileSystem.WriteAllTextSafe(proxyPath, proxyContent);
    }

    #endregion

    #region Unix Proxy Generation

    private void GenerateUnixGenericProxy(string proxyName, string commandName, string commandExe, string binDir)
    {
        var templatePath = Path.Combine(_templateDir, "generic-proxy.bash.template");
        if (!_fileSystem.FileExists(templatePath))  {
            throw new FileNotFoundException($"Template generic-proxy.bash.template non trovato");
        }

        var template = _fileSystem.ReadAllTextSafe(templatePath);
        var settingsFile = _paths.GetSettingsFilePath();
        var versionsPath = _paths.GetVersionsPath();

        var proxyContent = template
            .Replace("{{SETTINGS_FILE}}", settingsFile)
            .Replace("{{VERSIONS_PATH}}", versionsPath)
            .Replace("{{COMMAND_NAME}}", commandName)
            .Replace("{{COMMAND_EXE}}", commandExe);

        var proxyPath = Path.Combine(binDir, proxyName);
        
        // Unix: UTF-8 no BOM, line ending LF
        _fileSystem.WriteAllTextSafe(proxyPath, proxyContent);
        
        // Imposta permessi eseguibili
        _fileSystem.SetExecutablePermissions(proxyPath);
    }

    private void GenerateUnixPackageManagerProxy(string proxyName, string packageManager, string scriptPath, string binDir)
    {
        var templatePath = Path.Combine(_templateDir, "package-manager.bash.template");
        if (!_fileSystem.FileExists(templatePath))
        {
            throw new FileNotFoundException($"Template package-manager.bash.template non trovato");
        }

        var template = _fileSystem.ReadAllTextSafe(templatePath);
        var settingsFile = _paths.GetSettingsFilePath();
        var versionsPath = _paths.GetVersionsPath();

        var proxyContent = template
            .Replace("{{SETTINGS_FILE}}", settingsFile)
            .Replace("{{VERSIONS_PATH}}", versionsPath)
            .Replace("{{PM_NAME}}", packageManager)
            .Replace("{{SCRIPT_PATH}}", scriptPath);

        var proxyPath = Path.Combine(binDir, proxyName);
        
        _fileSystem.WriteAllTextSafe(proxyPath, proxyContent);
        _fileSystem.SetExecutablePermissions(proxyPath);
    }

    #endregion

    #region Helper Methods

    private static string EscapeCSharpString(string str)
    {
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static bool IsManagedUnixProxyOrWrapper(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        return fileName.StartsWith(ProxyNaming.IsolatedPrefix, StringComparison.OrdinalIgnoreCase)
               || UnixWrapperCommands.Contains(fileName);
    }

    private void EnsureTemplateDirectoryExists()
    {
        if (!HasRequiredTemplates(_templateDir))
        {
            throw new KnotVMException(
                KnotErrorCode.PathNotFound,
                $"Template directory non trovata o incompleta. Percorsi verificati: {string.Join(", ", _templateSearchPaths)}");
        }
    }

    private static string? ResolveTemplateDirectory(IEnumerable<string> searchPaths)
    {
        foreach (var path in searchPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (HasRequiredTemplates(path))
            {
                return path;
            }
        }

        return null;
    }

    internal static bool HasRequiredTemplatesForDiagnostics(string templateDirectory)
    {
        return HasRequiredTemplates(templateDirectory);
    }

    internal static string? ResolveTemplateDirectoryForDiagnostics(IEnumerable<string> searchPaths)
    {
        return ResolveTemplateDirectory(searchPaths);
    }

    private static bool HasRequiredTemplates(string templateDirectory)
    {
        if (!Directory.Exists(templateDirectory))
        {
            return false;
        }

        return RequiredTemplateFiles.All(file => File.Exists(Path.Combine(templateDirectory, file)));
    }

    #endregion
}
