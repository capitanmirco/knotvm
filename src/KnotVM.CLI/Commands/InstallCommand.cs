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
/// Comando per installare una versione di Node.js.
/// </summary>
public class InstallCommand : Command
{
    private readonly IInstallationService _installationService;
    private readonly IInstallationManager _installationManager;
    private readonly Argument<string?> _versionArgument;
    private readonly Option<string?> _aliasOption;
    private readonly Option<bool> _latestOption;
    private readonly Option<bool> _latestLtsOption;

    public InstallCommand(IInstallationService installationService, IInstallationManager installationManager)
        : base("install", "Installa una versione di Node.js")
    {
        _installationService = installationService;
        _installationManager = installationManager;

        // Argument per versione
        _versionArgument = new Argument<string?>(name: "version")
        {
            Description = "Versione da installare (es: 20, 20.11.0, lts)",
            Arity = ArgumentArity.ZeroOrOne
        };

        // Opzioni
        _aliasOption = new Option<string?>(name: "--alias")
        {
            Description = "Alias per l'installazione (default: numero versione)"
        };

        _latestOption = new Option<bool>(name: "--latest")
        {
            Description = "Installa l'ultima versione disponibile"
        };

        _latestLtsOption = new Option<bool>(name: "--latest-lts")
        {
            Description = "Installa l'ultima versione LTS disponibile"
        };

        this.Add(_versionArgument);
        this.Add(_aliasOption);
        this.Add(_latestOption);
        this.Add(_latestLtsOption);

        // Handler
        this.SetAction(async (context) =>
        {
            var version = context.GetValue(_versionArgument);
            var alias = context.GetValue(_aliasOption);
            var latest = context.GetValue(_latestOption);
            var latestLts = context.GetValue(_latestLtsOption);

            return await ExecuteAsync(version, alias, latest, latestLts);
        });
    }

    private async Task<int> ExecuteAsync(string? version, string? customAlias, bool latest, bool latestLts)
    {
        return await CommandExecutor.ExecuteWithExitCodeAsync(async () =>
        {
            using var cancellationScope = new ConsoleCancellationScope();

            // Validazione mutua esclusione: una sola sorgente versione.
            CommandValidation.EnsureExactlyOne(
                "Specificare una versione, --latest o --latest-lts",
                "Specificare solo una tra: <version>, --latest o --latest-lts",
                !string.IsNullOrEmpty(version),
                latest,
                latestLts
            );

            // Determina pattern versione
            string versionPattern = (latest, latestLts, version) switch
            {
                (false, true, _) => "lts",
                (true, false, _) => "latest",
                (false, false, not null) => version,
                _ => throw new InvalidOperationException("Stato versione non valido")
            };

            // Validazione alias se fornito
            if (customAlias != null)
            {
                _installationManager.ValidateAliasOrThrow(customAlias);
            }

            InstallationPrepareResult? result = null;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync("Installazione in corso...", async ctx =>
                {
                    // Progress reporting
                    var progress = new Progress<DownloadProgress>(p =>
                    {
                        if (p.TotalBytes > 0)
                        {
                            var percentage = (int)((double)p.BytesDownloaded / p.TotalBytes * 100);
                            ctx.Status($"Download in corso... {percentage}% ({p.BytesDownloaded.ToHumanReadableSize()} / {p.TotalBytes.ToHumanReadableSize()})");
                        }
                        else
                        {
                            ctx.Status($"Download in corso... {p.BytesDownloaded.ToHumanReadableSize()}");
                        }
                    });

                    result = await _installationService.InstallAsync(
                        versionPattern,
                        customAlias,
                        forceReinstall: false,
                        progressCallback: progress,
                        cancellationToken: cancellationScope.Token
                    );
                });

            if (result == null)
            {
                throw new KnotVMException(
                    KnotErrorCode.UnexpectedError,
                    "Installazione fallita"
                );
            }

            if (!result.Success)
            {
                var errorCode = KnotErrorCode.InstallationFailed;
                if (!string.IsNullOrWhiteSpace(result.ErrorCode) &&
                    Enum.TryParse<KnotErrorCode>(result.ErrorCode, out var parsedErrorCode))
                {
                    errorCode = parsedErrorCode;
                }

                throw new KnotVMException(
                    errorCode,
                    result.ErrorMessage ?? "Installazione fallita"
                );
            }

            // Success message
            AnsiConsole.MarkupLine($"[green]✓[/] Node.js [bold]{result.Version}[/] installato con successo");
            AnsiConsole.MarkupLine($"[dim]Alias: {result.Alias}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]→[/] Per attivare questa versione, usa: [bold]knot use {result.Alias}[/]");
        });
    }
}
