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
/// Comando per aggiornare la versione Node.js di un alias mantenendo lo stesso alias.
/// </summary>
public class UpgradeCommand : Command
{
    private readonly IUpgradeDowngradeService _upgradeService;
    private readonly IInstallationsRepository _repository;
    private readonly Argument<string> _aliasArgument;
    private readonly Option<string?> _targetOption;
    private readonly Option<bool> _yesOption;

    public UpgradeCommand(
        IUpgradeDowngradeService upgradeService,
        IInstallationsRepository repository)
        : base("upgrade", "Aggiorna la versione Node.js di un alias mantenendo lo stesso alias")
    {
        _upgradeService = upgradeService;
        _repository = repository;

        _aliasArgument = new Argument<string>(name: "alias")
        {
            Description = "Alias dell'installazione da aggiornare"
        };

        _targetOption = new Option<string?>(name: "--target")
        {
            Description = "Versione target specifica (es: 22.14.0). Se non specificata, viene richiesta interattivamente."
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
                var latestLts = await _upgradeService.GetLatestLtsVersionAsync(cancellationScope.Token)
                    ?? throw new KnotVMException(KnotErrorCode.RemoteApiFailed, "Impossibile recuperare l'ultima versione LTS da nodejs.org");

                if (latestLts.Version.Equals(installation.Version.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
                {
                    AnsiConsole.MarkupLine($"[green][[OK]][/] Già alla versione LTS più recente ([bold]{Markup.Escape(installation.Version)}[/])");
                    return;
                }

                AnsiConsole.MarkupLine($"[green]Upgrade disponibile:[/] {Markup.Escape(installation.Version)} → [bold green]{Markup.Escape(latestLts.Version)}[/]");
                if (latestLts.Lts != null)
                    AnsiConsole.MarkupLine($"[dim]LTS codename: {Markup.Escape(latestLts.Lts)}[/]");
                AnsiConsole.WriteLine();

                resolvedTarget = latestLts.Version;
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
                AnsiConsole.MarkupLine($"[dim]Upgrade:[/] {Markup.Escape(installation.Version)} → [bold]{Markup.Escape(resolvedTarget)}[/]");
                if (!AnsiConsole.Confirm($"Procedere con l'upgrade dell'alias '[bold]{Markup.Escape(alias)}[/]'?"))
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
                throw new KnotVMException(errorCode, result?.ErrorMessage ?? "Upgrade fallito");
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
                .Title("Seleziona la versione target:")
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
