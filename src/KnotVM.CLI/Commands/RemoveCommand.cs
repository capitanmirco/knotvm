using System.CommandLine;
using KnotVM.CLI.Utils;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per rimuovere un'installazione di Node.js.
/// </summary>
public class RemoveCommand : Command
{
    private readonly IInstallationManager _installationManager;
    private readonly IInstallationsRepository _repository;
    private readonly Argument<string> _installationArgument;
    private readonly Option<bool> _forceOption;

    public RemoveCommand(IInstallationManager installationManager, IInstallationsRepository repository)
        : base("remove", "Rimuove un'installazione di Node.js")
    {
        _installationManager = installationManager;
        _repository = repository;

        _installationArgument = new Argument<string>(name: "installation")
        {
            Description = "Alias dell'installazione da rimuovere"
        };

        _forceOption = new Option<bool>(name: "--force")
        {
            Description = "Forza la rimozione anche se l'installazione è attiva"
        };

        this.Add(_installationArgument);
        this.Add(_forceOption);

        // Handler
        this.SetAction((context) =>
        {
            var installation = context.GetValue(_installationArgument);
            var force = context.GetValue(_forceOption);
            return Execute(installation!, force);
        });
    }

    private int Execute(string alias, bool force)
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

            // Conferma se installazione è attiva e force non è specificato
            if (installation.Use && !force)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠[/] L'installazione '{installation.Alias}' è attualmente attiva");
                
                if (!AnsiConsole.Confirm($"Confermi la rimozione? (usa --force per saltare questa conferma)", false))
                {
                    AnsiConsole.MarkupLine("[dim]Operazione annullata[/]");
                    return 0;
                }
            }

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("yellow"))
                .Start("Rimozione in corso...", ctx =>
                {
                    _installationManager.RemoveInstallation(installation.Alias, force);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Installazione [bold]{installation.Alias}[/] (Node.js {installation.Version}) rimossa con successo");

            return 0;
        });
    }
}
