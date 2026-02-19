using System.CommandLine;
using System.Reflection;
using System.Runtime.InteropServices;
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
        this.SetAction((_) => Execute());
    }

    private static int Execute() => CommandExecutor.ExecuteWithExitCode(() =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;

        // Estrae versione e commit hash (formato: "1.0.0+hash")
        var parts = infoVersion.Split('+');
        var cleanVersion = parts[0];
        var commitHash = parts.Length > 1 ? parts[1] : null;
        var shortHash = commitHash?.Length > 7 ? commitHash.Substring(0, 7) : commitHash;

        AnsiConsole.WriteLine();

        // ASCII art del nodo a sinistra
        var asciiArt = @"[green]
      ██╗  ██╗███╗   ██╗ ██████╗ ████████╗
      ██║ ██╔╝████╗  ██║██╔═══██╗╚══██╔══╝
      █████╔╝ ██╔██╗ ██║██║   ██║   ██║   
      ██╔═██╗ ██║╚██╗██║██║   ██║   ██║   
      ██║  ██╗██║ ╚████║╚██████╔╝   ██║   
      ╚═╝  ╚═╝╚═╝  ╚═══╝ ╚═════╝    ╚═╝   
                                          
              ██╗   ██╗███╗   ███╗           
              ██║   ██║████╗ ████║           
              ██║   ██║██╔████╔██║           
              ╚██╗ ██╔╝██║╚██╔╝██║           
               ╚████╔╝ ██║ ╚═╝ ██║           
                ╚═══╝  ╚═╝     ╚═╝           [/]";

        // Info a destra
        var info = new Panel(new Markup(
            $"[bold cyan]KnotVM[/] - Node.js Version Manager\n" +
            $"[dim]────────────────────────────────────[/]\n" +
            $"[yellow]Version:[/]  [white]{cleanVersion}[/]\n" +
            (shortHash != null ? $"[yellow]Build:[/]    [dim]{shortHash}[/]\n" : "") +
            $"[yellow]Platform:[/] [white]{RuntimeInformation.OSDescription}[/]\n" +
            $"[yellow]Arch:[/]     [white]{RuntimeInformation.OSArchitecture}[/]\n" +
            $"\n" +
            $"[dim]Gestore versioni Node.js cross-platform[/]\n" +
            $"[dim]Copyright © 2026[/]"
        ))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(1, 0, 1, 0)
        };

        // Layout a due colonne
        var leftColumn = new Markup(asciiArt);
        var rightColumn = info;
        
        var columns = new Columns(leftColumn, rightColumn)
        {
            Padding = new Padding(2, 0, 2, 0)
        };

        AnsiConsole.Write(columns);
        AnsiConsole.WriteLine();
    });
}
