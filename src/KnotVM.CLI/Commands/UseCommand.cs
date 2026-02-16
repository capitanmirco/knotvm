using System.CommandLine;
using KnotVM.CLI.Utils;
using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per attivare una versione di Node.js installata.
/// </summary>
public class UseCommand : Command
{
    private readonly IInstallationManager _installationManager;
    private readonly IInstallationsRepository _repository;
    private readonly Argument<string> _installationArgument;

    public UseCommand(IInstallationManager installationManager, IInstallationsRepository repository)
        : base("use", "Attiva una versione di Node.js installata")
    {
        _installationManager = installationManager;
        _repository = repository;

        _installationArgument = new Argument<string>(name: "installation")
        {
            Description = "Alias dell'installazione da attivare"
        };

        this.Add(_installationArgument);

        // Handler
        this.SetAction((context) =>
        {
            var installation = context.GetValue(_installationArgument);
            return Execute(installation!);
        });
    }

    private int Execute(string alias)
    {
        return CommandExecutor.ExecuteWithExitCode(() =>
        {
            // Verifica che l'installazione esista
            var installation = _repository.GetByAlias(alias);

            if (installation == null)
            {
                throw new KnotVMHintException(
                    KnotErrorCode.InstallationNotFound,
                    $"Installazione '{alias}' non trovata",
                    "Usare 'knot list' per vedere installazioni disponibili"
                );
            }

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .Start("Attivazione in corso...", ctx =>
                {
                    ctx.Status("Configurazione versione attiva...");
                    _installationManager.UseInstallation(installation.Alias);

                    ctx.Status("Sincronizzazione completata");
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Versione [bold]{installation.Alias}[/] attivata (Node.js {installation.Version})");
            AnsiConsole.WriteLine();
            var nodeProxy = ProxyNaming.BuildIsolatedProxyName("node");
            AnsiConsole.MarkupLine($"[green]→[/] Usa 'node --version' per verificare (ricorda il prefisso [bold]{ProxyNaming.IsolatedPrefix}[/] per isolated mode)");
            AnsiConsole.MarkupLine($"[dim]   Esempio: {nodeProxy} --version[/]");
        });
    }
}
