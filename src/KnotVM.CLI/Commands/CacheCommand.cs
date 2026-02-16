using System.CommandLine;
using KnotVM.CLI.Extensions;
using KnotVM.CLI.Utils;
using KnotVM.Core.Interfaces;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per gestire la cache download.
/// </summary>
public class CacheCommand : Command
{
    private readonly ICacheService _cacheService;
    private readonly Option<bool> _listOption;
    private readonly Option<bool> _clearOption;
    private readonly Option<bool> _cleanOption;

    public CacheCommand(ICacheService cacheService)
        : base("cache", "Gestisce la cache download artifact")
    {
        _cacheService = cacheService;

        _listOption = new Option<bool>(name: "--list")
        {
            Description = "Mostra i file nella cache"
        };

        _clearOption = new Option<bool>(name: "--clear")
        {
            Description = "Elimina tutti i file dalla cache"
        };

        _cleanOption = new Option<bool>(name: "--clean")
        {
            Description = "Elimina file cache obsoleti (più vecchi di 30 giorni)"
        };

        this.Add(_listOption);
        this.Add(_clearOption);
        this.Add(_cleanOption);

        // Handler
        this.SetAction((context) =>
        {
            var list = context.GetValue(_listOption);
            var clear = context.GetValue(_clearOption);
            var clean = context.GetValue(_cleanOption);
            return Execute(list, clear, clean);
        });
    }

    private int Execute(bool list, bool clear, bool clean)
    {
        return CommandExecutor.ExecuteWithExitCode(() =>
        {
            // Validazione mutua esclusione: una sola azione alla volta.
            CommandValidation.EnsureExactlyOne(
                "Specificare un'azione: --list, --clear o --clean",
                "Specificare solo un'azione tra: --list, --clear o --clean",
                list,
                clear,
                clean
            );

            if (list)
            {
                ExecuteList();
            }
            else if (clear)
            {
                ExecuteClear();
            }
            else if (clean)
            {
                ExecuteClean();
            }

            return 0;
        });
    }

    private void ExecuteList()
    {
        var files = _cacheService.ListCacheFiles();
        var totalSize = _cacheService.GetCacheSizeBytes();

        if (files.Length == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Cache vuota[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn(new TableColumn("[bold]File[/]").LeftAligned());
        table.AddColumn(new TableColumn("[bold]Dimensione[/]").RightAligned());
        table.AddColumn(new TableColumn("[bold]Data Modifica[/]").Centered());

        foreach (var (fileName, sizeBytes, modifiedDate) in files)
        {
            table.AddRow(
                fileName,
                sizeBytes.ToHumanReadableSize(),
                modifiedDate.ToString("yyyy-MM-dd HH:mm")
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]→[/] Totale: {files.Length} file, {totalSize.ToHumanReadableSize()}");
    }

    private void ExecuteClear()
    {
        var totalSize = _cacheService.GetCacheSizeBytes();
        var fileCount = _cacheService.ListCacheFiles().Length;

        if (fileCount == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Cache già vuota[/]");
            return;
        }

        if (!AnsiConsole.Confirm($"Eliminare tutti i {fileCount} file dalla cache ({totalSize.ToHumanReadableSize()})?", false))
        {
            AnsiConsole.MarkupLine("[dim]Operazione annullata[/]");
            return;
        }

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .Start("Pulizia cache in corso...", ctx =>
            {
                _cacheService.ClearCache();
            });

        AnsiConsole.MarkupLine("[green]✓[/] Cache svuotata con successo");
        AnsiConsole.MarkupLine($"[dim]Liberati {totalSize.ToHumanReadableSize()}[/]");
    }

    private void ExecuteClean()
    {
        var filesBefore = _cacheService.ListCacheFiles();
        var sizeBefore = _cacheService.GetCacheSizeBytes();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .Start("Pulizia file obsoleti...", ctx =>
            {
                _cacheService.CleanCache(olderThanDays: 30);
            });

        var filesAfter = _cacheService.ListCacheFiles();
        var sizeAfter = _cacheService.GetCacheSizeBytes();

        var filesRemoved = filesBefore.Length - filesAfter.Length;
        var sizeFreed = sizeBefore - sizeAfter;

        if (filesRemoved == 0)
        {
            AnsiConsole.MarkupLine("[green]✓[/] Nessun file obsoleto trovato");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Rimossi {filesRemoved} file obsoleti");
            AnsiConsole.MarkupLine($"[dim]Liberati {sizeFreed.ToHumanReadableSize()}[/]");
        }
    }
}
