using System.CommandLine;
using System.CommandLine.Parsing;
using KnotVM.CLI.Utils;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per eseguire un comando con una versione specifica di Node.js.
/// </summary>
public class RunCommand : Command
{
    private readonly IInstallationsRepository _repository;
    private readonly IProcessRunner _processRunner;
    private readonly IPlatformService _platformService;
    private readonly Argument<string> _commandArgument;
    private readonly Option<string> _withVersionOption;

    public RunCommand(
        IInstallationsRepository repository,
        IProcessRunner processRunner,
        IPlatformService platformService)
        : base("run", "Esegue un comando con una versione specifica di Node.js")
    {
        _repository = repository;
        _processRunner = processRunner;
        _platformService = platformService;

        _commandArgument = new Argument<string>(name: "command")
        {
            Description = "Comando completo da eseguire (racchiuso tra virgolette)"
        };

        _withVersionOption = new Option<string>(name: "--with-version")
        {
            Description = "Alias dell'installazione da usare",
            Required = true
        };

        this.Add(_commandArgument);
        this.Add(_withVersionOption);

        // Handler
        this.SetAction((context) =>
        {
            var command = context.GetValue(_commandArgument);
            var version = context.GetValue(_withVersionOption);
            return Execute(command!, version);
        });
    }

    private int Execute(string commandLine, string? alias)
    {
        return CommandExecutor.ExecuteWithExitCode(() =>
        {
            // Validazione --with-version obbligatorio
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new KnotVMHintException(
                    KnotErrorCode.UnexpectedError,
                    "--with-version è obbligatorio",
                    "Specificare l'alias dell'installazione da usare"
                );
            }

            // Verifica che l'installazione esista
            var installation = _repository.GetByAlias(alias);

            if (installation == null)
            {
                throw new KnotVMHintException(
                    KnotErrorCode.InstallationNotFound,
                    $"Installazione '{alias}' non trovata",
                    "Usare 'knot list' per vedere installazioni disponibili"
                );
            }

            // Parse comando (primo token è il comando, resto sono argomenti)
            var parts = ParseCommandLine(commandLine);
            if (parts.Length == 0)
            {
                throw new KnotVMHintException(
                    KnotErrorCode.InstallationFailed,
                    "Comando non valido",
                    "Specificare un comando da eseguire"
                );
            }

            var commandName = parts[0];
            var arguments = parts.Skip(1).ToArray();
            var lookupCommand = _platformService.GetCurrentOs() == HostOs.Windows ? "where" : "which";

            // Risolvi path comando con OS-aware resolution order
            var commandPath = ResolveCommandPath(installation.Path, commandName);
            if (commandPath == null)
            {
                throw new KnotVMHintException(
                    KnotErrorCode.CommandNotFound,
                    $"Comando '{commandName}' non trovato nella versione {installation.Alias}",
                    $"Verificare presenza comando con 'knot run \"{lookupCommand} {commandName}\" --with-version {installation.Alias}'"
                );
            }

            // Setup environment isolato
            var env = SetupEnvironment(installation.Path);

            // Esegui comando e propaga exit code
            var exitCode = _processRunner.RunAndPropagateExitCode(
                commandPath,
                arguments,
                workingDirectory: Directory.GetCurrentDirectory(),
                environmentVariables: env
            );

            return exitCode;
        });
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
