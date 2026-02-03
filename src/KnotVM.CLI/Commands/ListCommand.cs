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
    private readonly Option<bool> _pathOption;

    public ListCommand(IInstallationsRepository repository) : base("list", "Mostra le versioni di Node.js installate")
    {
        _repository = repository;

        // Opzione --path
        _pathOption = new Option<bool>(name: "Path", aliases: ["--path", "-p"])
        {
            Description = "Mostra il percorso completo delle installazioni."
        };
        this.Add(_pathOption);
        
        // Handler
        this.SetAction((p) => Execute(p.GetValue(_pathOption)));
    }

    private void Execute(bool showPath)
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
        if (showPath)
        {
            table.AddColumn(new TableColumn("[bold]Path[/]").LeftAligned());
        }
        
        foreach (var installation in installations)
        {
            var marker = installation.Use ? "[green]✓[/]" : "";
            var alias = installation.Use ? $"[green]{installation.Alias}[/]" : installation.Alias;
            var version = installation.Use ? $"[green]{installation.Version}[/]" : installation.Version;
            
            if (showPath)
            {
                var path = installation.Use ? $"[green]{installation.Path}[/]" : $"[dim]{installation.Path}[/]";
                table.AddRow(alias, version, marker, path);
            }
            else
            {
                table.AddRow(alias, version, marker);
            }
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
