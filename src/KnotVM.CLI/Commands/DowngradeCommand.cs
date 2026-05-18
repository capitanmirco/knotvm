using System.CommandLine;
using KnotVM.CLI.Extensions;
using KnotVM.CLI.Utils;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per effettuare il downgrade della versione Node.js di un alias mantenendo lo stesso alias.
/// </summary>
public class DowngradeCommand : Command
{
    private readonly IUpgradeDowngradeService _upgradeService;
    private readonly IInstallationsRepository _repository;
    private readonly Argument<string> _aliasArgument;
    private readonly Option<string?> _targetOption;
    private readonly Option<bool> _yesOption;

    public DowngradeCommand(
        IUpgradeDowngradeService upgradeService,
        IInstallationsRepository repository)
        : base("downgrade", "Effettua il downgrade della versione Node.js di un alias mantenendo lo stesso alias")
    {
        _upgradeService = upgradeService;
        _repository = repository;

        _aliasArgument = new Argument<string>(name: "alias")
        {
            Description = "Alias dell'installazione di cui fare il downgrade"
        };

        _targetOption = new Option<string?>(name: "--target")
        {
            Description = "Versione target specifica (es: 20.18.0). Se non specificata, viene richiesta interattivamente."
        };

        _yesOption = new Option<bool>(name: "--yes")
        {
            Description = "Salta la conferma prima dell'installazione"
        };

        this.Add(_aliasArgument);
        this.Add(_targetOption);
        this.Add(_yesOption);

        this.SetAction(async (context) =>
        {
            var alias = context.GetValue(_aliasArgument)!;
            var target = context.GetValue(_targetOption);
            var yes = context.GetValue(_yesOption);
            return await ExecuteAsync(alias, target, yes);
        });
    }

    private async Task<int> ExecuteAsync(string alias, string? targetVersion, bool skipConfirm)
    {
        return await CommandExecutor.ExecuteWithExitCodeAsync(async () =>
        {
            using var cancellationScope = new ConsoleCancellationScope();

            var installation = _repository.GetByAlias(alias)
                ?? throw new KnotVMHintException(
                    KnotErrorCode.InstallationNotFound,
                    $"Alias '{alias}' non trovato",
                    "Usa 'knot list' per vedere le installazioni disponibili");

            AnsiConsole.MarkupLine($"[dim]Installazione corrente:[/] [bold]{Markup.Escape(alias)}[/] → Node.js {Markup.Escape(installation.Version)}");
            AnsiConsole.WriteLine();

            string resolvedTarget;

            if (targetVersion != null)
            {
                resolvedTarget = targetVersion.TrimStart('v');
            }
            else if (_upgradeService.IsLtsAlias(alias))
            {
                resolvedTarget = await SelectLtsVersionInteractivelyAsync(installation.Version, cancellationScope.Token);
            }
            else
            {
                resolvedTarget = await SelectVersionInteractivelyAsync(installation.Version, cancellationScope.Token);
            }

            if (resolvedTarget.Equals(installation.Version.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[yellow]L'alias '{Markup.Escape(alias)}' è già sulla versione {Markup.Escape(resolvedTarget)}.[/]");
                return;
            }

            if (!skipConfirm)
            {
                AnsiConsole.MarkupLine($"[dim]Downgrade:[/] {Markup.Escape(installation.Version)} → [bold]{Markup.Escape(resolvedTarget)}[/]");
                if (!AnsiConsole.Confirm($"Procedere con il downgrade dell'alias '[bold]{Markup.Escape(alias)}[/]'?"))
                {
                    AnsiConsole.MarkupLine("[yellow]Operazione annullata.[/]");
                    return;
                }
            }

            InstallationPrepareResult? result = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync("Installazione in corso...", async ctx =>
                {
                    var progress = new Progress<DownloadProgress>(p =>
                    {
                        ctx.Status(p.TotalBytes > 0
                            ? $"Download in corso... {(int)((double)p.BytesDownloaded / p.TotalBytes * 100)}% ({p.BytesDownloaded.ToHumanReadableSize()} / {p.TotalBytes.ToHumanReadableSize()})"
                            : $"Download in corso... {p.BytesDownloaded.ToHumanReadableSize()}");
                    });

                    result = await _upgradeService.ReplaceVersionAsync(
                        alias, installation.Use, resolvedTarget,
                        progress: progress, cancellationToken: cancellationScope.Token);
                });

            if (result == null || !result.Success)
            {
                var errorCode = !string.IsNullOrWhiteSpace(result?.ErrorCode) &&
                                Enum.TryParse<KnotErrorCode>(result.ErrorCode, out var parsedCode)
                    ? parsedCode
                    : KnotErrorCode.InstallationFailed;
                throw new KnotVMException(errorCode, result?.ErrorMessage ?? "Downgrade fallito");
            }

            AnsiConsole.MarkupLine($"[green][[OK]][/] Node.js [bold]{Markup.Escape(result.Version)}[/] installato con alias '[bold]{Markup.Escape(result.Alias)}[/]'");
            if (installation.Use)
                AnsiConsole.MarkupLine("[dim]→ Versione riattivata (era precedentemente attiva)[/]");
            else
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[green]->[/] Per attivare questa versione, usa: [bold]knot use {Markup.Escape(alias)}[/]");
            }
        });
    }

    /// <summary>
    /// Mostra le ultime 15 versioni LTS pubblicate e chiede all'utente di scegliere (flusso alias LTS).
    /// </summary>
    private async Task<string> SelectLtsVersionInteractivelyAsync(string currentVersion, CancellationToken ct)
    {
        RemoteVersion[] versions = [];
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Recupero versioni LTS disponibili...", async _ =>
            {
                versions = await _upgradeService.GetRecentLtsVersionsAsync(currentVersion, count: 15, cancellationToken: ct);
            });

        if (versions.Length == 0)
            throw new KnotVMException(KnotErrorCode.RemoteApiFailed, "Nessuna versione LTS disponibile per il downgrade");

        DisplayVersionsTable(versions, currentVersion);
        AnsiConsole.MarkupLine($"[dim]Versioni LTS disponibili (ultime {versions.Length}, esclusa la corrente)[/]");
        AnsiConsole.WriteLine();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<RemoteVersion>()
                .Title("Seleziona la versione LTS di destinazione:")
                .UseConverter(v => FormatVersionChoice(v, currentVersion))
                .AddChoices(versions));

        return selection.Version;
    }

    /// <summary>
    /// Chiede quante versioni mostrare, visualizza la lista e chiede all'utente di scegliere (flusso non-LTS).
    /// </summary>
    private async Task<string> SelectVersionInteractivelyAsync(string currentVersion, CancellationToken ct)
    {
        var limit = AnsiConsole.Ask("[dim]Quante versioni remote vuoi visualizzare?[/]", 20);
        if (limit <= 0) limit = 20;

        RemoteVersion[] versions = [];
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Recupero versioni disponibili...", async _ =>
            {
                versions = await _upgradeService.GetRemoteVersionsAsync(limit, ct);
            });

        if (versions.Length == 0)
            throw new KnotVMException(KnotErrorCode.RemoteApiFailed, "Nessuna versione remota disponibile");

        DisplayVersionsTable(versions, currentVersion);

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<RemoteVersion>()
                .Title("Seleziona la versione di destinazione:")
                .UseConverter(v => FormatVersionChoice(v, currentVersion))
                .AddChoices(versions));

        return selection.Version;
    }

    private static void DisplayVersionsTable(RemoteVersion[] versions, string currentVersion)
    {
        var current = currentVersion.TrimStart('v');
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        table.AddColumn(new TableColumn("[bold]Versione[/]").Centered());
        table.AddColumn(new TableColumn("[bold]LTS[/]").Centered());
        table.AddColumn(new TableColumn("[bold]Data Rilascio[/]").Centered());

        foreach (var v in versions)
        {
            var isCurrent = v.Version.Equals(current, StringComparison.OrdinalIgnoreCase);
            string versionText = v.Version.StartsWith('v') ? v.Version : $"v{v.Version}";
            string ltsText = v.Lts != null ? $"[green]{Markup.Escape(v.Lts)}[/]" : "[dim]-[/]";
            string dateText = !string.IsNullOrEmpty(v.Date) ? v.Date : "[dim]N/A[/]";

            var styledVersion = isCurrent
                ? $"[bold yellow]{versionText} (corrente)[/]"
                : v.Lts != null ? $"[bold green]{versionText}[/]" : $"[white]{versionText}[/]";

            table.AddRow(styledVersion, ltsText, dateText);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static string FormatVersionChoice(RemoteVersion v, string currentVersion)
    {
        var current = currentVersion.TrimStart('v');
        var isCurrent = v.Version.Equals(current, StringComparison.OrdinalIgnoreCase);
        string label = v.Version.StartsWith('v') ? v.Version : $"v{v.Version}";
        if (v.Lts != null) label += $"  [{v.Lts}]";
        if (isCurrent) label += " (corrente)";
        return label;
    }
}
