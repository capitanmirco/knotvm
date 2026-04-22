using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;

namespace KnotVM.Infrastructure.Services;

/// <summary>
/// Implementazione del servizio diagnostico per knot doctor.
/// Esegue check sullo stato dell'ambiente KnotVM e supporta fix automatici dove possibile.
/// </summary>
public class DoctorService(
    IPathService            pathService,
    IFileSystemService      fileSystem,
    IVersionManager         versionManager,
    IInstallationsRepository repository,
    ISyncService            syncService,
    ILockManager            lockManager,
    IProcessRunner          processRunner,
    IRemoteVersionService   remoteVersionService,
    IPlatformService        platformService) : IDoctorService
{
    private const string CheckKnotHome        = "KNOT_HOME";
    private const string CheckActiveVersion   = "Versione attiva";
    private const string CheckProxySync       = "Proxy sincronizzati";
    private const string CheckTemplates       = "Template proxy";
    private const string CheckConnectivity    = "Connettività nodejs.org";
    private const string CheckPathConflicts   = "PATH: conflitti node";
    private const string CheckStaleLocks      = "Lock file orfani";
    private const string CheckDotNetRuntime   = ".NET runtime";

    public async Task<IReadOnlyList<DoctorCheck>> RunAllChecksAsync(CancellationToken ct = default)
    {
        var checks = new List<DoctorCheck>
        {
            CheckKnotHomeDirectory(),
            CheckActiveVersionState(),
            CheckProxySyncState(),
            CheckProxyTemplates(),
            await CheckConnectivityAsync(ct),
            await CheckPathConflictsAsync(ct),
            CheckStaleLocksState(),
            CheckDotNetRuntimeVersion()
        };

        return checks;
    }

    public Task<bool> TryAutoFixAsync(DoctorCheck check, CancellationToken ct = default)
    {
        var result = check.Name switch
        {
            CheckKnotHome    => TryFixKnotHome(),
            CheckProxySync   => TryFixProxySync(),
            CheckStaleLocks  => TryFixStaleLocks(),
            _                => false
        };

        return Task.FromResult(result);
    }

    // ── Check implementations ────────────────────────────────────────────────

    private DoctorCheck CheckKnotHomeDirectory()
    {
        var basePath = pathService.GetBasePath();

        if (!fileSystem.DirectoryExists(basePath))
        {
            return new DoctorCheck(
                Name:        CheckKnotHome,
                Passed:      false,
                IsWarning:   false,
                Detail:      $"Directory non trovata: {basePath}",
                Suggestion:  "Esegui: knot doctor --fix",
                CanAutoFix:  true);
        }

        if (!fileSystem.CanWrite(basePath))
        {
            return new DoctorCheck(
                Name:        CheckKnotHome,
                Passed:      false,
                IsWarning:   false,
                Detail:      $"Permessi insufficienti su: {basePath}",
                Suggestion:  "Verifica i permessi della directory con 'ls -la'",
                CanAutoFix:  false);
        }

        return new DoctorCheck(
            Name:        CheckKnotHome,
            Passed:      true,
            IsWarning:   false,
            Detail:      basePath,
            Suggestion:  null,
            CanAutoFix:  false);
    }

    private DoctorCheck CheckActiveVersionState()
    {
        var alias = versionManager.GetActiveAlias();

        if (alias is null)
        {
            return new DoctorCheck(
                Name:        CheckActiveVersion,
                Passed:      false,
                IsWarning:   true,
                Detail:      "Nessuna versione attiva",
                Suggestion:  "Esegui: knot use <alias>",
                CanAutoFix:  false);
        }

        var installation = repository.GetByAlias(alias);
        if (installation is null)
        {
            return new DoctorCheck(
                Name:        CheckActiveVersion,
                Passed:      false,
                IsWarning:   false,
                Detail:      $"Alias '{alias}' registrato in settings.txt ma installazione non trovata",
                Suggestion:  "Esegui: knot install <versione> oppure knot use <altro-alias>",
                CanAutoFix:  false);
        }

        return new DoctorCheck(
            Name:        CheckActiveVersion,
            Passed:      true,
            IsWarning:   false,
            Detail:      $"{alias} → {installation.Version}",
            Suggestion:  null,
            CanAutoFix:  false);
    }

    private DoctorCheck CheckProxySyncState()
    {
        try
        {
            var needed = syncService.IsSyncNeeded();
            if (needed)
            {
                return new DoctorCheck(
                    Name:        CheckProxySync,
                    Passed:      false,
                    IsWarning:   false,
                    Detail:      "I proxy non sono allineati con le installazioni correnti",
                    Suggestion:  "Esegui: knot sync  oppure  knot doctor --fix",
                    CanAutoFix:  true);
            }
        }
        catch
        {
            return new DoctorCheck(
                Name:        CheckProxySync,
                Passed:      false,
                IsWarning:   true,
                Detail:      "Impossibile verificare lo stato dei proxy",
                Suggestion:  "Esegui: knot sync",
                CanAutoFix:  true);
        }

        return new DoctorCheck(
            Name:        CheckProxySync,
            Passed:      true,
            IsWarning:   false,
            Detail:      null,
            Suggestion:  null,
            CanAutoFix:  false);
    }

    private DoctorCheck CheckProxyTemplates()
    {
        var templatesPath = pathService.GetTemplatesPath();

        if (!fileSystem.DirectoryExists(templatesPath))
        {
            return new DoctorCheck(
                Name:        CheckTemplates,
                Passed:      false,
                IsWarning:   false,
                Detail:      $"Directory template non trovata: {templatesPath}",
                Suggestion:  "Reinstalla KnotVM con ./install.sh --force",
                CanAutoFix:  false);
        }

        var templates = fileSystem.GetFiles(templatesPath, "*.template");
        if (templates.Length == 0)
        {
            return new DoctorCheck(
                Name:        CheckTemplates,
                Passed:      false,
                IsWarning:   false,
                Detail:      $"Nessun file .template trovato in: {templatesPath}",
                Suggestion:  "Reinstalla KnotVM con ./install.sh --force",
                CanAutoFix:  false);
        }

        return new DoctorCheck(
            Name:        CheckTemplates,
            Passed:      true,
            IsWarning:   false,
            Detail:      $"{templates.Length} template presenti",
            Suggestion:  null,
            CanAutoFix:  false);
    }

    private async Task<DoctorCheck> CheckConnectivityAsync(CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            var latest = await remoteVersionService.GetLatestLtsVersionAsync(
                forceRefresh: true,
                cancellationToken: timeoutCts.Token);

            if (latest is null)
            {
                return new DoctorCheck(
                    Name:        CheckConnectivity,
                    Passed:      false,
                    IsWarning:   true,
                    Detail:      "Risposta vuota da nodejs.org",
                    Suggestion:  "Verifica la connessione di rete",
                    CanAutoFix:  false);
            }

            return new DoctorCheck(
                Name:        CheckConnectivity,
                Passed:      true,
                IsWarning:   false,
                Detail:      $"OK (LTS corrente: {latest.Version})",
                Suggestion:  null,
                CanAutoFix:  false);
        }
        catch (OperationCanceledException)
        {
            return new DoctorCheck(
                Name:        CheckConnectivity,
                Passed:      false,
                IsWarning:   true,
                Detail:      "Timeout raggiunto (5s)",
                Suggestion:  "Verifica la connessione di rete",
                CanAutoFix:  false);
        }
        catch (Exception ex)
        {
            return new DoctorCheck(
                Name:        CheckConnectivity,
                Passed:      false,
                IsWarning:   true,
                Detail:      ex.Message,
                Suggestion:  "Verifica la connessione di rete",
                CanAutoFix:  false);
        }
    }

    private async Task<DoctorCheck> CheckPathConflictsAsync(CancellationToken ct)
    {
        try
        {
            var isWindows = platformService.GetCurrentOs() == HostOs.Windows;
            var cmd       = isWindows ? "where" : "which";
            var result    = await processRunner.RunAsync(cmd, "node", timeoutMilliseconds: 3000);

            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return new DoctorCheck(
                    Name:        CheckPathConflicts,
                    Passed:      true,
                    IsWarning:   false,
                    Detail:      "Nessun node globale nel PATH",
                    Suggestion:  null,
                    CanAutoFix:  false);
            }

            var nodePath  = result.StandardOutput.Trim().Split('\n').First().Trim();
            var basePath  = pathService.GetBasePath();

            if (pathService.ArePathsEquivalent(Path.GetDirectoryName(nodePath) ?? string.Empty, pathService.GetBinPath()) ||
                nodePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return new DoctorCheck(
                    Name:        CheckPathConflicts,
                    Passed:      true,
                    IsWarning:   false,
                    Detail:      $"node → {nodePath} (KnotVM)",
                    Suggestion:  null,
                    CanAutoFix:  false);
            }

            return new DoctorCheck(
                Name:        CheckPathConflicts,
                Passed:      false,
                IsWarning:   true,
                Detail:      $"node trovato al di fuori di KnotVM: {nodePath}",
                Suggestion:  "Verifica l'ordine del PATH e rimuovi installazioni node concorrenti",
                CanAutoFix:  false);
        }
        catch
        {
            return new DoctorCheck(
                Name:        CheckPathConflicts,
                Passed:      true,
                IsWarning:   false,
                Detail:      "Impossibile verificare il PATH",
                Suggestion:  null,
                CanAutoFix:  false);
        }
    }

    private DoctorCheck CheckStaleLocksState()
    {
        var locksPath = pathService.GetLocksPath();

        if (!fileSystem.DirectoryExists(locksPath))
        {
            return new DoctorCheck(
                Name:        CheckStaleLocks,
                Passed:      true,
                IsWarning:   false,
                Detail:      null,
                Suggestion:  null,
                CanAutoFix:  false);
        }

        var lockFiles = fileSystem.GetFiles(locksPath, "*.lock");
        if (lockFiles.Length == 0)
        {
            return new DoctorCheck(
                Name:        CheckStaleLocks,
                Passed:      true,
                IsWarning:   false,
                Detail:      null,
                Suggestion:  null,
                CanAutoFix:  false);
        }

        // Considera stale i lock più vecchi di 24 ore
        var stale = lockFiles.Where(f =>
            fileSystem.GetFileLastWriteTime(f) < DateTime.UtcNow.AddHours(-24)).ToArray();

        if (stale.Length > 0)
        {
            return new DoctorCheck(
                Name:        CheckStaleLocks,
                Passed:      false,
                IsWarning:   true,
                Detail:      $"{stale.Length} lock file orfani trovati",
                Suggestion:  "Esegui: knot doctor --fix",
                CanAutoFix:  true);
        }

        return new DoctorCheck(
            Name:        CheckStaleLocks,
            Passed:      true,
            IsWarning:   false,
            Detail:      null,
            Suggestion:  null,
            CanAutoFix:  false);
    }

    private static DoctorCheck CheckDotNetRuntimeVersion()
    {
        var version = Environment.Version;
        if (version.Major < 8)
        {
            return new DoctorCheck(
                Name:        CheckDotNetRuntime,
                Passed:      false,
                IsWarning:   false,
                Detail:      $".NET {version.Major}.{version.Minor} rilevato (richiesto ≥ 8)",
                Suggestion:  "Aggiorna il runtime .NET a versione 8 o superiore",
                CanAutoFix:  false);
        }

        return new DoctorCheck(
            Name:        CheckDotNetRuntime,
            Passed:      true,
            IsWarning:   false,
            Detail:      $"{version.Major}.{version.Minor}",
            Suggestion:  null,
            CanAutoFix:  false);
    }

    // ── Auto-fix implementations ─────────────────────────────────────────────

    private bool TryFixKnotHome()
    {
        try
        {
            fileSystem.EnsureDirectoryExists(pathService.GetBasePath());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryFixProxySync()
    {
        try
        {
            syncService.Sync(force: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryFixStaleLocks()
    {
        try
        {
            lockManager.CleanupStaleLocks();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
