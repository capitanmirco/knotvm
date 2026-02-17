using System.Text.RegularExpressions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione servizio gestione ciclo vita installazioni.
/// </summary>
public class InstallationManager : IInstallationManager
{
    private readonly IInstallationsRepository _installationsRepo;
    private readonly IVersionManager _versionManager;
    private readonly IInstallationService _installationService;
    private readonly IPathService _paths;
    private readonly IFileSystemService _fileSystem;
    private readonly ISyncService _syncService;
    private readonly ILockManager _lockManager;
    private readonly IProcessRunner _processRunner;

    // Alias riservati che non possono essere usati
    private static readonly HashSet<string> ReservedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "node", "npm", "npx", "knot", "nodejs", "corepack"
    };

    // Regex validazione alias: ^[a-zA-Z0-9_-]+$
    private static readonly Regex AliasRegex = new(@"^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    public InstallationManager(
        IInstallationsRepository installationsRepo,
        IVersionManager versionManager,
        IInstallationService installationService,
        IPathService paths,
        IFileSystemService fileSystem,
        ISyncService syncService,
        ILockManager lockManager,
        IProcessRunner processRunner)
    {
        _installationsRepo = installationsRepo;
        _versionManager = versionManager;
        _installationService = installationService;
        _paths = paths;
        _fileSystem = fileSystem;
        _syncService = syncService;
        _lockManager = lockManager;
        _processRunner = processRunner;
    }

    public void UseInstallation(string alias)
    {
        using var lockHandle = _lockManager.AcquireLock("state");

        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias non può essere vuoto", nameof(alias));

        // Verifica che installazione esista
        if (!AliasExists(alias))
        {
            throw new KnotVMException(
                KnotErrorCode.InstallationNotFound,
                $"Installazione '{alias}' non trovata"
            );
        }

        // Aggiorna versione attiva
        _versionManager.UseVersion(alias);

        // Trigger sync per aggiornare proxy
        _syncService.Sync(force: false);
    }

    public void RenameInstallation(string fromAlias, string toAlias)
    {
        using var lockHandle = _lockManager.AcquireLock("state");

        if (string.IsNullOrWhiteSpace(fromAlias))
            throw new ArgumentException("Alias corrente non può essere vuoto", nameof(fromAlias));

        if (string.IsNullOrWhiteSpace(toAlias))
            throw new ArgumentException("Nuovo alias non può essere vuoto", nameof(toAlias));

        // Valida alias destinazione e verifica unicità.
        ValidateAliasOrThrow(toAlias);

        // Verifica che installazione sorgente esista
        var installation = _installationsRepo.GetByAlias(fromAlias);
        if (installation == null)
        {
            throw new KnotVMException(
                KnotErrorCode.InstallationNotFound,
                $"Installazione '{fromAlias}' non trovata"
            );
        }

        try
        {
            // Ottieni path vecchio e nuovo
            var oldPath = _paths.GetInstallationPath(fromAlias);
            var newPath = _paths.GetInstallationPath(toAlias);

            // Rinomina directory su filesystem
            if (_fileSystem.DirectoryExists(oldPath))
            {
                Directory.Move(oldPath, newPath);
            }

            // Aggiorna repository
            _installationsRepo.Remove(fromAlias);
            _installationsRepo.Add(installation with 
            { 
                Alias = toAlias,
                Path = newPath
            });

            // Se installazione era attiva, aggiorna settings.txt
            if (installation.Use)
            {
                _versionManager.UpdateSettingsFile(toAlias);
            }

            // Sync proxy se necessario
            if (installation.Use)
            {
                _syncService.Sync(force: false);
            }
        }
        catch (Exception ex) when (ex is not KnotVMException)
        {
            throw new KnotVMException(
                KnotErrorCode.InstallationFailed,
                $"Errore rinominando installazione da '{fromAlias}' a '{toAlias}': {ex.Message}",
                ex
            );
        }
    }

    public void RemoveInstallation(string alias, bool force = false)
    {
        using var lockHandle = _lockManager.AcquireLock("state");

        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias non può essere vuoto", nameof(alias));

        // Verifica che installazione esista
        var installation = _installationsRepo.GetByAlias(alias);
        if (installation == null)
        {
            throw new KnotVMException(
                KnotErrorCode.InstallationNotFound,
                $"Installazione '{alias}' non trovata"
            );
        }

        // Policy: blocca rimozione installazione attiva se force=false
        if (installation.Use && !force)
        {
            throw new KnotVMException(
                KnotErrorCode.InvalidAlias,
                $"Impossibile rimuovere installazione '{alias}' perché attualmente attiva. Usa 'knot use <altro>' prima di rimuovere, oppure usa --force."
            );
        }

        try
        {
            // Rileva processi node.exe in esecuzione da questa installazione
            var installPath = _paths.GetInstallationPath(alias);
            var nodeExePath = Path.Combine(installPath, "node.exe");
            var runningProcesses = _processRunner.FindRunningProcesses(nodeExePath);

            if (runningProcesses.Count > 0)
            {
                var processIds = string.Join(", ", runningProcesses);
                throw new KnotVMHintException(
                    KnotErrorCode.InstallationFailed,
                    $"Impossibile rimuovere installazione '{alias}': {runningProcesses.Count} processo/i Node.js in esecuzione (PID: {processIds})",
                    $"Chiudi tutti i processi Node.js attivi prima di rimuovere l'installazione. Puoi usare:\n  PowerShell: Stop-Process -Id {processIds} -Force\n  CMD: taskkill /F /PID {processIds.Replace(", ", " /PID ")}"
                );
            }

            // Se installazione attiva e force=true, disattiva prima
            if (installation.Use)
            {
                _versionManager.UnuseVersion();
            }

            // Rimuovi directory filesystem
            if (_fileSystem.DirectoryExists(installPath))
            {
                _fileSystem.DeleteDirectoryIfExists(installPath, recursive: true);
            }

            // Rimuovi dal repository
            _installationsRepo.Remove(alias);

            // Se rimossa installazione attiva, sync per rimuovere proxy
            if (installation.Use)
            {
                _syncService.Sync(force: false);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new KnotVMHintException(
                KnotErrorCode.InstallationFailed,
                $"Errore rimozione installazione '{alias}': Accesso negato",
                "Assicurati che nessun processo Node.js sia in esecuzione. Chiudi eventuali terminali, IDE o applicazioni che usano Node.js da questa installazione. Su Windows, potresti dover terminare i processi 'node.exe' dal Task Manager.",
                ex
            );
        }
        catch (IOException ex) when (ex.Message.Contains("being used by another process") || 
                                      ex.Message.Contains("because it is being used") ||
                                      ex.Message.Contains("in uso"))
        {
            throw new KnotVMHintException(
                KnotErrorCode.InstallationFailed,
                $"Errore rimozione installazione '{alias}': File in uso da un altro processo",
                "Chiudi tutti i processi Node.js, terminali e IDE che potrebbero usare questa installazione. Prova a eseguire 'taskkill /F /IM node.exe' per terminare tutti i processi Node.js.",
                ex
            );
        }
        catch (Exception ex) when (ex is not KnotVMException)
        {
            throw new KnotVMException(
                KnotErrorCode.InstallationFailed,
                $"Errore rimozione installazione '{alias}': {ex.Message}",
                ex
            );
        }
    }

    public bool IsAliasValid(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return false;

        // Lunghezza 1-50
        if (alias.Length < 1 || alias.Length > 50)
            return false;

        // Regex: ^[a-zA-Z0-9_-]+$
        if (!AliasRegex.IsMatch(alias))
            return false;

        // Non riservato
        if (ReservedAliases.Contains(alias))
            return false;

        return true;
    }

    public bool AliasExists(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return false;

        return _installationsRepo.GetByAlias(alias) != null;
    }

    public void ValidateAliasOrThrow(string alias, bool checkExists = true)
    {
        if (!IsAliasValid(alias))
        {
            throw new KnotVMHintException(
                KnotErrorCode.InvalidAlias,
                $"Alias '{alias}' non valido",
                "Alias validi: 1-50 caratteri, alfanumerici + '_' e '-', no nomi riservati (node, npm, npx, knot, nodejs, corepack)"
            );
        }

        if (checkExists && AliasExists(alias))
        {
            throw new KnotVMHintException(
                KnotErrorCode.InvalidAlias,
                $"Alias '{alias}' già esistente",
                "Usa 'knot list' per vedere gli alias esistenti o 'knot remove <alias>' per rimuovere l'installazione esistente"
            );
        }
    }
}
