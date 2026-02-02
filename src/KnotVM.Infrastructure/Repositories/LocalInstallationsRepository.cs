using System.Diagnostics;
using KnotVM.Core.Common;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Repositories;

/// <summary>
/// Repository per gestire le installazioni di Node.js dal filesystem locale.
/// </summary>
public class LocalInstallationsRepository : IInstallationsRepository
{
    private readonly Configuration _config;

    public LocalInstallationsRepository(Configuration config)
    {
        _config = config;
    }

    /// <summary>
    /// Ottiene tutte le installazioni presenti nel filesystem.
    /// </summary>
    public Installation[] GetAll()
    {
        // Verifica che la directory versions esista
        if (!Directory.Exists(_config.VersionsPath))
        {
            return Array.Empty<Installation>();
        }

        var installations = new List<Installation>();
        
        // Leggi l'alias attivo da settings.txt
        var activeAlias = GetActiveAlias();

        // Enumera tutte le sottocartelle
        var directories = Directory.GetDirectories(_config.VersionsPath);

        foreach (var dir in directories)
        {
            // Verifica se è una installazione valida (contiene node.exe)
            if (!IsValidInstallation(dir))
            {
                continue;
            }

            var alias = Path.GetFileName(dir);
            var version = GetNodeVersion(dir);

            if (version != null)
            {
                var isActive = !string.IsNullOrEmpty(activeAlias) && alias.Equals(activeAlias, StringComparison.OrdinalIgnoreCase);
                installations.Add(new Installation(alias, version, Use: isActive));
            }
        }

        return installations.ToArray();
    }
    
    /// <summary>
    /// Legge il nome dell'alias attivo dal file settings.txt.
    /// </summary>
    /// <returns>Il nome dell'alias attivo o null se non presente</returns>
    private string? GetActiveAlias()
    {
        try
        {
            if (!File.Exists(_config.SettingsFile))
            {
                return null;
            }
            
            var content = File.ReadAllText(_config.SettingsFile).Trim();
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Verifica se una directory contiene una installazione valida di Node.js.
    /// </summary>
    private bool IsValidInstallation(string directoryPath)
    {
        var nodeExePath = Path.Combine(directoryPath, "node.exe");
        return File.Exists(nodeExePath);
    }

    /// <summary>
    /// Ottiene la versione di Node.js eseguendo node.exe -v.
    /// </summary>
    /// <returns>Versione (es: "20.11.0") o null se fallisce</returns>
    private string? GetNodeVersion(string directoryPath)
    {
        try
        {
            var nodeExePath = Path.Combine(directoryPath, "node.exe");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = nodeExePath,
                Arguments = "-v",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            
            // Rimuovi il prefisso 'v' se presente (es: v20.11.0 -> 20.11.0)
            return output.StartsWith('v') ? output.Substring(1) : output;
        }
        catch
        {
            // Se qualcosa va storto, ritorna null
            return null;
        }
    }
}
