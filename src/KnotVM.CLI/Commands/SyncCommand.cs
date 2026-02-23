using System.CommandLine;
using KnotVM.CLI.Utils;
using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per sincronizzare i proxy con le installazioni correnti.
/// </summary>
public class SyncCommand : Command
{
    private readonly ISyncService _syncService;
    private readonly IInstallationsRepository _repository;
    private readonly ILockManager _lockManager;
    private readonly Option<bool> _forceOption;

    public SyncCommand(ISyncService syncService, IInstallationsRepository repository, ILockManager lockManager)
        : base("sync", "Sincronizza i proxy con le installazioni correnti")
    {
        _syncService = syncService;
        _repository = repository;
        _lockManager = lockManager;

        _forceOption = new Option<bool>(name: "--force")
        {
            Description = "Rigenera tutti i proxy (non solo quelli dinamici)"
        };

        this.Add(_forceOption);

        // Handler
        this.SetAction((context) =>
        {
            var force = context.GetValue(_forceOption);
            return Execute(force);
        });
    }

    private int Execute(bool force)
    {
        return CommandExecutor.ExecuteWithExitCode(() =>
        {
            using var lockHandle = _lockManager.AcquireLock("state");

            var installations = _repository.GetAll();
            if (installations.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nessuna installazione trovata. Sync non necessaria.[/]");
                AnsiConsole.MarkupLine("[dim]Usa 'knot install <versione>' per installare Node.js.[/]");
                return 0;
            }

            if (!force && !_syncService.IsSyncNeeded())
            {
                AnsiConsole.MarkupLine("[green][[OK]][/] Proxy già sincronizzati");
                return 0;
            }

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .Start("Sincronizzazione proxy...", ctx => _syncService.Sync(force));

            AnsiConsole.MarkupLine(force 
                ? "[green][[OK]][/] Tutti i proxy rigenerati con successo"
                : "[green][[OK]][/] Proxy dinamici sincronizzati con successo");

            AnsiConsole.WriteLine();
            var proxyNames = string.Join(", ", [
                ProxyNaming.BuildIsolatedProxyName("node"),
                ProxyNaming.BuildIsolatedProxyName("npm"),
                ProxyNaming.BuildIsolatedProxyName("npx"),
                ProxyNaming.BuildIsolatedProxyName("corepack")
            ]);
            AnsiConsole.MarkupLine($"[dim]Proxy disponibili: {proxyNames}[/]");
            AnsiConsole.MarkupLine("[dim]Package managers auto-rilevati: npm, yarn, pnpm, bun, ni, nun, nup[/]");
            return 0;
        });
    }
}
