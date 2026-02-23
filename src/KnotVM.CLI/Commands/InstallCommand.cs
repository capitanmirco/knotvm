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
    private readonly IVersionFileDetector _detector;
    private readonly IVersionResolver _versionResolver;
    private readonly Argument<string?> _versionArgument;
    private readonly Option<string?> _aliasOption;
    private readonly Option<bool> _latestOption;
    private readonly Option<bool> _latestLtsOption;
    private readonly Option<bool> _fromFileOption;

    public InstallCommand(
        IInstallationService installationService,
        IInstallationManager installationManager,
        IVersionFileDetector detector,
        IVersionResolver versionResolver)
        : base("install", "Installa una versione di Node.js")
    {
        _installationService = installationService;
        _installationManager = installationManager;
        _detector = detector;
        _versionResolver = versionResolver;

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

        _fromFileOption = new Option<bool>(name: "--from-file")
        {
            Description = "Installa la versione specificata nel file di configurazione progetto (.nvmrc, .node-version, package.json)"
        };

        this.Add(_versionArgument);
        this.Add(_aliasOption);
        this.Add(_latestOption);
        this.Add(_latestLtsOption);
        this.Add(_fromFileOption);

        // Handler
        this.SetAction(async (context) =>
        {
            var version = context.GetValue(_versionArgument);
            var alias = context.GetValue(_aliasOption);
            var latest = context.GetValue(_latestOption);
            var latestLts = context.GetValue(_latestLtsOption);
            var fromFile = context.GetValue(_fromFileOption);

            return await ExecuteAsync(version, alias, latest, latestLts, fromFile);
        });
    }

    private async Task<int> ExecuteAsync(string? version, string? customAlias, bool latest, bool latestLts, bool fromFile)
    {
        return await CommandExecutor.ExecuteWithExitCodeAsync(async () =>
        {
            using var cancellationScope = new ConsoleCancellationScope();

            CommandValidation.EnsureExactlyOne(
                "Specificare una versione, --latest, --latest-lts o --from-file",
                "Specificare solo una tra: <version>, --latest, --latest-lts o --from-file",
                !string.IsNullOrEmpty(version), latest, latestLts, fromFile);

            if (fromFile)
            {
                var detectedVersion = await _detector.DetectVersionAsync(Directory.GetCurrentDirectory()).ConfigureAwait(false);

                if (detectedVersion == null)
                {
                    throw new KnotVMHintException(
                        KnotErrorCode.VersionFileNotFound,
                        "Nessun file di configurazione versione trovato",
                        "File supportati: .nvmrc, .node-version, package.json (campo engines.node)");
                }

                AnsiConsole.MarkupLine($"[dim]Versione rilevata: [/][cyan]{Markup.Escape(detectedVersion)}[/]");
                version = detectedVersion;
            }

            string versionPattern = (latest, latestLts, version) switch
            {
                (false, true, _) => "lts",
                (true, false, _) => "latest",
                (false, false, not null) => version,
                _ => throw new InvalidOperationException("Stato versione non valido")
            };

            if (customAlias != null)
                _installationManager.ValidateAliasOrThrow(customAlias);

            // Risolvi la versione abbreviata a semver completo prima di installare
            var resolvedVersion = await _versionResolver.ResolveVersionAsync(versionPattern, cancellationScope.Token)
                .ConfigureAwait(false);

            if (!_versionResolver.IsExactVersion(versionPattern))
                AnsiConsole.MarkupLine($"[dim]Versione risolta: [/][cyan]{Markup.Escape(resolvedVersion)}[/]");

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

                    result = await _installationService.InstallAsync(
                        resolvedVersion, customAlias, forceReinstall: false,
                        progressCallback: progress, cancellationToken: cancellationScope.Token);
                });

            if (result == null)
                throw new KnotVMException(KnotErrorCode.UnexpectedError, "Installazione fallita");

            if (!result.Success)
            {
                var errorCode = !string.IsNullOrWhiteSpace(result.ErrorCode) && 
                                Enum.TryParse<KnotErrorCode>(result.ErrorCode, out var parsedErrorCode)
                    ? parsedErrorCode
                    : KnotErrorCode.InstallationFailed;

                throw new KnotVMException(errorCode, result.ErrorMessage ?? "Installazione fallita");
            }

            // Success message
            AnsiConsole.MarkupLine($"[green][[OK]][/] Node.js [bold]{Markup.Escape(result.Version)}[/] installato con successo");
            AnsiConsole.MarkupLine($"[dim]Alias: {Markup.Escape(result.Alias)}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]->[/] Per attivare questa versione, usa: [bold]knot use {Markup.Escape(result.Alias)}[/]");
        });
    }
}
