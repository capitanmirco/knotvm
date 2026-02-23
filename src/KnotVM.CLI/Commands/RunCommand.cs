using System.CommandLine;
using System.CommandLine.Parsing;
using KnotVM.CLI.Utils;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per eseguire un comando con una versione specifica di Node.js.
/// </summary>
public class RunCommand : Command
{
    private readonly IInstallationsRepository _repository;
    private readonly IProcessRunner _processRunner;
    private readonly IPlatformService _platformService;
    private readonly IVersionFileDetector _detector;
    private readonly IInstallationService _installationService;
    private readonly Argument<string> _commandArgument;
    private readonly Option<string?> _withVersionOption;
    private readonly Option<bool> _autoOption;

    public RunCommand(
        IInstallationsRepository repository,
        IProcessRunner processRunner,
        IPlatformService platformService,
        IVersionFileDetector detector,
        IInstallationService installationService)
        : base("run", "Esegue un comando con una versione specifica di Node.js")
    {
        _repository = repository;
        _processRunner = processRunner;
        _platformService = platformService;
        _detector = detector;
        _installationService = installationService;

        _commandArgument = new Argument<string>(name: "command")
        {
            Description = "Comando completo da eseguire (racchiuso tra virgolette)"
        };

        _withVersionOption = new Option<string?>(name: "--with-version")
        {
            Description = "Alias dell'installazione da usare"
        };

        _autoOption = new Option<bool>(name: "--auto")
        {
            Description = "Rileva automaticamente la versione da package.json (engines.node), .nvmrc o .node-version; installa se non presente usando il nome del progetto come alias"
        };

        this.Add(_commandArgument);
        this.Add(_withVersionOption);
        this.Add(_autoOption);

        // Handler
        this.SetAction(async (context) =>
        {
            var command = context.GetValue(_commandArgument);
            var version = context.GetValue(_withVersionOption);
            var auto = context.GetValue(_autoOption);
            return await ExecuteAsync(command!, version, auto);
        });
    }

    private async Task<int> ExecuteAsync(string commandLine, string? alias, bool auto)
    {
        return await CommandExecutor.ExecuteWithExitCodeAsync(async () =>
        {
            CommandValidation.EnsureExactlyOne(
                "Specificare --with-version <alias> oppure --auto",
                "Specificare solo uno tra: --with-version o --auto",
                !string.IsNullOrEmpty(alias), auto);

            if (auto)
            {
                alias = await ResolveAndEnsureInstalledAsync().ConfigureAwait(false);
            }

            var installation = _repository.GetByAlias(alias!)
                ?? throw new KnotVMHintException(KnotErrorCode.InstallationNotFound,
                    $"Installazione '{alias}' non trovata",
                    "Usare 'knot list' per vedere installazioni disponibili");

            var parts = ParseCommandLine(commandLine);
            if (parts.Length == 0)
            {
                throw new KnotVMHintException(KnotErrorCode.InstallationFailed,
                    "Comando non valido",
                    "Specificare un comando da eseguire");
            }

            var commandName = parts[0];
            var arguments = parts.Skip(1).ToArray();
            var lookupCommand = _platformService.GetCurrentOs() == HostOs.Windows ? "where" : "which";

            var commandPath = ResolveCommandPath(installation.Path, commandName)
                ?? throw new KnotVMHintException(KnotErrorCode.CommandNotFound,
                    $"Comando '{commandName}' non trovato nella versione {installation.Alias}",
                    $"Verificare presenza comando con 'knot run \"{lookupCommand} {commandName}\" --with-version {installation.Alias}'");

            var env = SetupEnvironment(installation.Path);

            return _processRunner.RunAndPropagateExitCode(
                commandPath,
                arguments,
                workingDirectory: Directory.GetCurrentDirectory(),
                environmentVariables: env
            );
        });
    }

    /// <summary>
    /// Rileva il contesto di progetto, installa la versione se necessario e restituisce l'alias da usare.
    /// </summary>
    private async Task<string> ResolveAndEnsureInstalledAsync()
    {
        var context = await _detector.DetectProjectContextAsync(Directory.GetCurrentDirectory()).ConfigureAwait(false);

        if (context.Version == null)
        {
            throw new KnotVMHintException(
                KnotErrorCode.VersionFileNotFound,
                "Nessuna versione Node.js specificata nel progetto",
                "Aggiungi engines.node in package.json, oppure un file .nvmrc o .node-version");
        }

        // Deriva alias: nome progetto se disponibile, altrimenti la versione stessa
        var candidateAlias = context.ProjectName ?? context.Version;

        // Cerca installazione per alias (nome progetto) o versione
        var installation = _repository.GetByAlias(candidateAlias)
            ?? _repository.GetByVersion(context.Version);

        if (installation != null)
        {
            AnsiConsole.MarkupLine($"[dim]Versione rilevata:[/] [cyan]{Markup.Escape(context.Version)}[/] [dim](alias: {Markup.Escape(installation.Alias)})[/]");
            return installation.Alias;
        }

        // Non installata → installa automaticamente
        AnsiConsole.MarkupLine($"[yellow]![/] Versione [cyan]{Markup.Escape(context.Version)}[/] non installata. Installazione automatica...");

        InstallationPrepareResult? result = null;
        using var cancellationScope = new ConsoleCancellationScope();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync($"Installazione Node.js {context.Version}...", async ctx =>
            {
                var progress = new Progress<DownloadProgress>(p =>
                {
                    ctx.Status(p.TotalBytes > 0
                        ? $"Download... {(int)((double)p.BytesDownloaded / p.TotalBytes * 100)}% ({p.BytesDownloaded} / {p.TotalBytes} byte)"
                        : $"Download... {p.BytesDownloaded} byte");
                });

                result = await _installationService.InstallAsync(
                    context.Version,
                    alias: candidateAlias,
                    forceReinstall: false,
                    progressCallback: progress,
                    cancellationToken: cancellationScope.Token).ConfigureAwait(false);
            });

        if (result == null || !result.Success)
        {
            var errorCode = result?.ErrorCode is not null &&
                            Enum.TryParse<KnotErrorCode>(result.ErrorCode, out var parsed)
                ? parsed
                : KnotErrorCode.InstallationFailed;

            throw new KnotVMException(errorCode, result?.ErrorMessage ?? $"Installazione di Node.js {context.Version} fallita");
        }

        AnsiConsole.MarkupLine($"[green][[OK]][/] Node.js [bold]{Markup.Escape(result.Version)}[/] installato con alias [bold]{Markup.Escape(result.Alias)}[/]");
        return result.Alias;
    }

    private string? ResolveCommandPath(string installationPath, string commandName)
    {
        var isWindows = _platformService.GetCurrentOs() == HostOs.Windows;

        string[] candidates;

        if (isWindows)
        {
            // Windows resolution order
            candidates =
            [
                Path.Combine(installationPath, $"{commandName}.exe"),
                Path.Combine(installationPath, $"{commandName}.cmd"),
                Path.Combine(installationPath, "node_modules", ".bin", $"{commandName}.cmd"),
                Path.Combine(installationPath, "node_modules", ".bin", commandName)
            ];
        }
        else
        {
            // Linux/macOS resolution order
            candidates =
            [
                Path.Combine(installationPath, "bin", commandName),
                Path.Combine(installationPath, commandName),
                Path.Combine(installationPath, "lib", "node_modules", ".bin", commandName),
                Path.Combine(installationPath, "node_modules", ".bin", commandName)
            ];
        }

        // Trova primo candidato esistente
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private Dictionary<string, string> SetupEnvironment(string installationPath)
    {
        var env = new Dictionary<string, string>();
        var isWindows = _platformService.GetCurrentOs() == HostOs.Windows;

        // Setup PATH per includere bin directory installazione
        string binPath = isWindows
            ? installationPath
            : Path.Combine(installationPath, "bin");

        // Ottieni PATH corrente e prependi bin path
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        var pathSeparator = isWindows ? ";" : ":";
        env["PATH"] = $"{binPath}{pathSeparator}{currentPath}";

        // Setup NODE_PATH se esiste lib/node_modules
        var nodeModulesPath = Path.Combine(installationPath, "lib", "node_modules");
        if (Directory.Exists(nodeModulesPath))
        {
            env["NODE_PATH"] = nodeModulesPath;
        }

        return env;
    }

    private static string[] ParseCommandLine(string commandLine)
    {
        return CommandLineParser.SplitCommandLine(commandLine).ToArray();
    }
}
