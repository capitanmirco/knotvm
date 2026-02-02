using System.CommandLine;
using KnotVM.Core.Interfaces;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per mostrare le versioni di Node.js installate.
/// </summary>
public class ListCommand : Command
{
    private readonly IInstallationsRepository _repository;

    public ListCommand(IInstallationsRepository repository) : base("list", "Mostra le versioni di Node.js installate")
    {
        _repository = repository;
        
        // Handler
        this.SetAction((p) => Execute());
    }

    private void Execute()
    {
        var installations = _repository.GetAll();
        
        if (installations.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nessuna installazione trovata.[/]");
            AnsiConsole.MarkupLine("[dim]Usa 'knot install <versione>' per installare Node.js.[/]");
            return;
        }
        
        var table = new Table();
        table.Border(TableBorder.SimpleHeavy);
        table.AddColumn(new TableColumn("[bold]Alias[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold]Versione Node.js[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold]Attiva[/]").Centered());
        
        foreach (var installation in installations)
        {
            var marker = installation.Use ? "[green]✓[/]" : "";
            var alias = installation.Use ? $"[green]{installation.Alias}[/]" : installation.Alias;
            var version = installation.Use ? $"[green]{installation.Version}[/]" : installation.Version;
            
            table.AddRow(alias, version, marker);
        }
        
        AnsiConsole.Write(table);
        
        var activeInstallation = installations.FirstOrDefault(i => i.Use);
        if (activeInstallation != null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]→[/] Versione attiva: [bold]{activeInstallation.Alias}[/] (Node.js {activeInstallation.Version})");
        }
        else
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Nessuna versione attiva. Usa 'knot use <alias>' per attivare una versione.[/]");
        }
    }
}
