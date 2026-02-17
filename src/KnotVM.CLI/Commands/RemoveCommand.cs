using System.CommandLine;
using KnotVM.CLI.Utils;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
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
            Description = "Alias o versione dell'installazione da rimuovere (es: 'lts' o '20.11.0')"
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

    private int Execute(string aliasOrVersion, bool force)
    {
        return CommandExecutor.ExecuteWithExitCode(() =>
        {
            // Cerca prima per alias
            var installation = _repository.GetByAlias(aliasOrVersion);

            // Se trovato per alias, rimuovi direttamente
            if (installation != null)
            {
                RemoveSingleInstallation(installation, force);
                return 0;
            }

            // Altrimenti cerca per versione
            var installationsByVersion = _repository.GetAllByVersion(aliasOrVersion);

            if (installationsByVersion.Length == 0)
            {
                throw new KnotVMHintException(
                    KnotErrorCode.InstallationNotFound,
                    $"Installazione '{aliasOrVersion}' non trovata",
                    "Usa 'knot list' per vedere le installazioni disponibili. Puoi specificare sia l'alias che la versione precisa (es: 'lts' o '20.11.0')"
                );
            }

            // Se c'è una sola installazione con quella versione, rimuovila
            if (installationsByVersion.Length == 1)
            {
                RemoveSingleInstallation(installationsByVersion[0], force);
                return 0;
            }

            // Più installazioni con la stessa versione: chiedi all'utente
            return HandleMultipleInstallations(installationsByVersion, force);
        });
    }

    private void RemoveSingleInstallation(Installation installation, bool force)
    {
        // Conferma se installazione è attiva e force non è specificato
        if (installation.Use && !force)
        {
            AnsiConsole.MarkupLine($"[yellow][[!]][/] L'installazione '{installation.Alias}' è attualmente attiva");
            
            if (!AnsiConsole.Confirm($"Confermi la rimozione? (usa --force per saltare questa conferma)", false))
            {
                AnsiConsole.MarkupLine("[dim]Operazione annullata[/]");
                return;
            }
        }

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .Start("Rimozione in corso...", ctx =>
            {
                _installationManager.RemoveInstallation(installation.Alias, force);
            });

        AnsiConsole.MarkupLine($"[green][[OK]][/] Installazione [bold]{installation.Alias}[/] (Node.js {installation.Version}) rimossa con successo");
    }

    private int HandleMultipleInstallations(Installation[] installations, bool force)
    {
        var version = installations[0].Version;
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[yellow][[!]][/] Trovate [bold]{installations.Length}[/] installazioni con Node.js [bold]{version}[/]:");
        AnsiConsole.WriteLine();

        // Mostra lista installazioni
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Alias[/]");
        table.AddColumn("[bold]Stato[/]");

        foreach (var inst in installations)
        {
            var status = inst.Use ? "[green]attiva[/]" : "[dim]inattiva[/]";
            table.AddRow(inst.Alias, status);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Menu di scelta
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Cosa vuoi fare?")
                .AddChoices(new[]
                {
                    "Rimuovi tutte le installazioni",
                    "Seleziona quali rimuovere",
                    "Annulla operazione"
                }));

        if (choice == "Annulla operazione")
        {
            AnsiConsole.MarkupLine("[dim]Operazione annullata[/]");
            return 0;
        }

        List<Installation> toRemove;

        if (choice == "Rimuovi tutte le installazioni")
        {
            // Conferma se ci sono installazioni attive
            var hasActive = installations.Any(i => i.Use);
            if (hasActive && !force)
            {
                AnsiConsole.MarkupLine("[yellow][[!]][/] Attenzione: alcune installazioni sono attive");
                
                if (!AnsiConsole.Confirm("Confermi la rimozione di tutte le installazioni?", false))
                {
                    AnsiConsole.MarkupLine("[dim]Operazione annullata[/]");
                    return 0;
                }
            }

            toRemove = installations.ToList();
        }
        else // Seleziona quali rimuovere
        {
            var selected = AnsiConsole.Prompt(
                new MultiSelectionPrompt<string>()
                    .Title("Seleziona le installazioni da rimuovere:")
                    .Required()
                    .InstructionsText("[grey](Premi [blue]<spazio>[/] per selezionare, [green]<invio>[/] per confermare)[/]")
                    .AddChoices(installations.Select(i => 
                        i.Use ? $"{i.Alias} [green](attiva)[/]" : i.Alias)));

            // Estrai gli alias dalle scelte (rimuovendo il suffisso "(attiva)")
            var selectedAliases = selected.Select(s => 
                s.Replace(" [green](attiva)[/]", "").Trim()).ToList();

            toRemove = installations
                .Where(i => selectedAliases.Contains(i.Alias))
                .ToList();
        }

        // Rimuovi le installazioni selezionate
        AnsiConsole.WriteLine();
        var removed = 0;
        var failed = 0;

        foreach (var inst in toRemove)
        {
            try
            {
                AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("yellow"))
                    .Start($"Rimozione {inst.Alias}...", ctx =>
                    {
                        _installationManager.RemoveInstallation(inst.Alias, force);
                    });

                AnsiConsole.MarkupLine($"[green][[OK]][/] [bold]{inst.Alias}[/] rimossa");
                removed++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red][[X]][/] Errore rimozione [bold]{inst.Alias}[/]: {ex.Message}");
                failed++;
            }
        }

        AnsiConsole.WriteLine();
        if (failed == 0)
        {
            AnsiConsole.MarkupLine($"[green][[OK]][/] Rimosse con successo [bold]{removed}[/] installazioni con Node.js {version}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow][[!]][/] Rimosse [bold]{removed}[/] installazioni, [bold]{failed}[/] fallite");
        }

        return 0;
    }
}
