using System.CommandLine;
using System.Reflection;
using KnotVM.CLI.Utils;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per mostrare la versione di KnotVM CLI.
/// </summary>
public class VersionCommand : Command
{
    public VersionCommand() : base("version", "Mostra la versione di KnotVM")
    {
        // Handler
        this.SetAction((_) => Execute());
    }

    private static int Execute()
    {
        return CommandExecutor.ExecuteWithExitCode(() =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;

            AnsiConsole.MarkupLine($"[bold green]KnotVM[/] versione [bold]{infoVersion}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Gestore versioni Node.js cross-platform[/]");
            AnsiConsole.MarkupLine("[dim]Copyright © 2026[/]");

            return 0;
        });
    }
}
