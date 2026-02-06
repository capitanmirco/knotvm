using System.CommandLine;
using System.Linq;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
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
        _pathOption = new Option<bool>(name: "with-path", aliases: ["--path", "-p"])
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
            PrintNoInstallationsMessage();
            return;
        }

        var table = CreateListTable(showPath);
        AddListRows(installations, table, showPath);

        AnsiConsole.Write(table);

        PrintActualUsedMessage(installations);
    }

    private static Table CreateListTable(bool showPath)
    {
        var table = Utils.Tables.CreateSpectreTable(["Alias", "Versione Node.js", "Attiva"]);

        if (showPath)
        {
            Utils.Tables.AddHeaderColumn(table, "Path");
        }

        return table;
    }

    private static void AddListRows(Installation[] installations, Table table, bool showPath)
    {
        foreach (var installation in installations)
        {
            bool isActive = installation.Use;
            string[] values = [installation.Alias, installation.Version, isActive ? "✓" : ""];
            if (showPath)
            {
                values = [.. values, installation.Path];
            }
            Utils.Tables.AddContentRow(
                    table,
                    values, v => isActive ? $"[green]{v}[/]" : $"[dim]{v}[/]"
                );
        }
    }

    private static void PrintNoInstallationsMessage()
    {
        AnsiConsole.MarkupLine("[yellow]Nessuna installazione trovata.[/]");
        AnsiConsole.MarkupLine("[dim]Usa 'knot install <versione>' per installare Node.js.[/]");
    }

    private static void PrintActualUsedMessage(Installation[] installations)
    {
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
