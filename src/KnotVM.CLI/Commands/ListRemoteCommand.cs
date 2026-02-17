using System.CommandLine;
using KnotVM.CLI.Utils;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per mostrare le versioni di Node.js disponibili da nodejs.org.
/// </summary>
public class ListRemoteCommand : Command
{
    private readonly IRemoteVersionService _remoteVersionService;
    private readonly Option<bool> _ltsOption;
    private readonly Option<bool> _allOption;
    private readonly Option<int?> _limitOption;

    public ListRemoteCommand(IRemoteVersionService remoteVersionService) 
        : base("list-remote", "Mostra le versioni di Node.js disponibili da nodejs.org")
    {
        _remoteVersionService = remoteVersionService;

        // Opzioni
        _ltsOption = new Option<bool>(name: "--lts")
        {
            Description = "Mostra solo le versioni LTS"
        };
        
        _allOption = new Option<bool>(name: "--all")
        {
            Description = "Mostra tutte le versioni disponibili (senza limite)"
        };
        
        _limitOption = new Option<int?>(name: "--limit")
        {
            Description = "Numero massimo di versioni da mostrare (default: 20)"
        };

        this.Add(_ltsOption);
        this.Add(_allOption);
        this.Add(_limitOption);

        // Handler
        this.SetAction(async (context) =>
        {
            var lts = context.GetValue(_ltsOption);
            var all = context.GetValue(_allOption);
            var limit = context.GetValue(_limitOption);
            
            return await ExecuteAsync(lts, all, limit);
        });
    }

    private async Task<int> ExecuteAsync(bool ltsOnly, bool showAll, int? customLimit)
    {
        return await CommandExecutor.ExecuteWithExitCodeAsync(async () =>
        {
            using var cancellationScope = new ConsoleCancellationScope();

            if (showAll && customLimit.HasValue)
            {
                throw new KnotVMException(
                    KnotErrorCode.UnexpectedError,
                    "Opzioni incompatibili: usare solo una tra --all e --limit"
                );
            }

            if (customLimit.HasValue && customLimit.Value <= 0)
            {
                throw new KnotVMHintException(
                    KnotErrorCode.UnexpectedError,
                    "Valore --limit non valido",
                    "Specificare un valore intero positivo (es: --limit 20)"
                );
            }

            RemoteVersion[] versions = Array.Empty<RemoteVersion>();
            
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync("Recupero versioni disponibili...", async ctx =>
                {
                    versions = ltsOnly
                        ? await _remoteVersionService.GetLtsVersionsAsync(cancellationToken: cancellationScope.Token)
                        : await _remoteVersionService.GetAvailableVersionsAsync(cancellationToken: cancellationScope.Token);
                });

            if (versions.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Nessuna versione disponibile.[/]");
                return;
            }

            // Determina il limite
            int effectiveLimit = showAll ? versions.Length : (customLimit ?? 20);
            var versionsToShow = versions.Take(effectiveLimit).ToArray();

            DisplayVersions(versionsToShow, ltsOnly);

            // Mostra info su versioni nascoste
            if (!showAll && versions.Length > effectiveLimit)
            {
                int remaining = versions.Length - effectiveLimit;
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]... e altre {remaining} versioni. Usa --all per mostrarle tutte o --limit <n> per specificare un limite.[/]");
            }
        });
    }

    private static void DisplayVersions(RemoteVersion[] versions, bool ltsOnly)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn(new TableColumn("[bold]Versione[/]").Centered());
        table.AddColumn(new TableColumn("[bold]LTS[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Data Rilascio[/]").Centered());

        foreach (var version in versions)
        {
            string versionText = version.Version.StartsWith('v') ? version.Version : $"v{version.Version}";
            string ltsText = version.Lts != null ? $"[green]{version.Lts}[/]" : "[dim]-[/]";
            string dateText = !string.IsNullOrEmpty(version.Date) ? version.Date : "[dim]N/A[/]";

            var styledVersionText = version.Lts != null 
                ? $"[bold green]{versionText}[/]" 
                : $"[white]{versionText}[/]";
            
            table.AddRow(
                styledVersionText,
                ltsText,
                dateText
            );
        }

        AnsiConsole.Write(table);

        // Info aggiuntiva
        AnsiConsole.WriteLine();
        if (ltsOnly)
        {
            AnsiConsole.MarkupLine($"[green]->[/] Mostrate {versions.Length} versioni LTS");
        }
        else
        {
            var ltsCount = versions.Count(v => v.Lts != null);
            AnsiConsole.MarkupLine($"[green]->[/] Mostrate {versions.Length} versioni ([green]{ltsCount} LTS[/], {versions.Length - ltsCount} standard)");
        }
    }
}
