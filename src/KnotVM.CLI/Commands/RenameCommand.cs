using System.CommandLine;
using KnotVM.CLI.Utils;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per rinominare un'installazione di Node.js.
/// </summary>
public class RenameCommand : Command
{
    private readonly IInstallationManager _installationManager;
    private readonly IInstallationsRepository _repository;
    private readonly Option<string?> _fromOption;
    private readonly Option<string?> _toOption;

    public RenameCommand(IInstallationManager installationManager, IInstallationsRepository repository)
        : base("rename", "Rinomina un'installazione di Node.js")
    {
        _installationManager = installationManager;
        _repository = repository;

        _fromOption = new Option<string?>(name: "--from")
        {
            Description = "Alias corrente dell'installazione"
        };

        _toOption = new Option<string?>(name: "--to")
        {
            Description = "Nuovo alias per l'installazione"
        };

        this.Add(_fromOption);
        this.Add(_toOption);

        // Handler
        this.SetAction((context) =>
        {
            var from = context.GetValue(_fromOption);
            var to = context.GetValue(_toOption);
            return Execute(from, to);
        });
    }

    private int Execute(string? fromAlias, string? toAlias)
    {
        return CommandExecutor.ExecuteWithExitCode(() =>
        {
            // Validazione argomenti obbligatori
            if (string.IsNullOrWhiteSpace(fromAlias))
            {
                throw new KnotVMException(
                    KnotErrorCode.UnexpectedError,
                    "--from è obbligatorio"
                );
            }

            if (string.IsNullOrWhiteSpace(toAlias))
            {
                throw new KnotVMException(
                    KnotErrorCode.UnexpectedError,
                    "--to è obbligatorio"
                );
            }

            // Verifica che l'installazione sorgente esista
            var installation = _repository.GetByAlias(fromAlias);

            if (installation == null)
            {
                throw new KnotVMHintException(
                    KnotErrorCode.InstallationNotFound,
                    $"Installazione '{fromAlias}' non trovata",
                    "Usare 'knot list' per vedere installazioni disponibili"
                );
            }

            // Validazione alias destinazione
            _installationManager.ValidateAliasOrThrow(toAlias);

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .Start("Rinominazione in corso...", ctx =>
                {
                    _installationManager.RenameInstallation(fromAlias, toAlias);
                });

            AnsiConsole.MarkupLine($"[green][[OK]][/] Installazione rinominata da [bold]{fromAlias}[/] a [bold]{toAlias}[/]");
            
            if (installation.Use)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Nota: l'installazione è attiva, i proxy sono stati aggiornati automaticamente[/]");
            }

            return 0;
        });
    }
}
