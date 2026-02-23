using System.CommandLine;
using KnotVM.CLI.Utils;
using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per attivare una versione di Node.js installata.
/// </summary>
public class UseCommand : Command
{
    private readonly IInstallationManager _installationManager;
    private readonly IInstallationsRepository _repository;
    private readonly IVersionFileDetector _detector;
    private readonly Argument<string?> _installationArgument;
    private readonly Option<bool> _autoOption;

    public UseCommand(
        IInstallationManager installationManager,
        IInstallationsRepository repository,
        IVersionFileDetector detector)
        : base("use", "Attiva una versione di Node.js installata")
    {
        _installationManager = installationManager;
        _repository = repository;
        _detector = detector;

        _installationArgument = new Argument<string?>(name: "installation")
        {
            Description = "Alias dell'installazione da attivare",
            Arity = ArgumentArity.ZeroOrOne
        };

        _autoOption = new Option<bool>(name: "--auto")
        {
            Description = "Auto-rileva la versione dal file di configurazione progetto (.nvmrc, .node-version, package.json)"
        };

        this.Add(_installationArgument);
        this.Add(_autoOption);

        // Handler
        this.SetAction(async (context) =>
        {
            var installation = context.GetValue(_installationArgument);
            var auto = context.GetValue(_autoOption);
            return await ExecuteAsync(installation, auto);
        });
    }

    private async Task<int> ExecuteAsync(string? alias, bool auto)
    {
        return await CommandExecutor.ExecuteWithExitCodeAsync(async () =>
        {
            CommandValidation.EnsureExactlyOne(
                "Specificare un alias o --auto",
                "Specificare solo uno tra: <installation> o --auto",
                !string.IsNullOrEmpty(alias), auto);

            if (auto)
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

                var detected = _repository.GetByAlias(detectedVersion)
                    ?? _repository.GetByVersion(detectedVersion);

                if (detected == null)
                {
                    throw new KnotVMHintException(
                        KnotErrorCode.InstallationNotFound,
                        $"Versione '{detectedVersion}' rilevata ma non installata",
                        "Usa 'knot install --from-file' per installarla automaticamente");
                }

                alias = detected.Alias;
            }

            var installation = _repository.GetByAlias(alias!)
                ?? throw new KnotVMHintException(KnotErrorCode.InstallationNotFound,
                    $"Installazione '{alias}' non trovata",
                    "Usare 'knot list' per vedere installazioni disponibili");

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .Start("Attivazione in corso...", ctx =>
                {
                    ctx.Status("Configurazione versione attiva...");
                    _installationManager.UseInstallation(installation.Alias);
                    ctx.Status("Sincronizzazione completata");
                });

            AnsiConsole.MarkupLine($"[green][[OK]][/] Versione [bold]{Markup.Escape(installation.Alias)}[/] attivata (Node.js {Markup.Escape(installation.Version)})");
            AnsiConsole.WriteLine();
            var nodeProxy = ProxyNaming.BuildIsolatedProxyName("node");
            AnsiConsole.MarkupLine($"[green]->[/] Usa 'node --version' per verificare (ricorda il prefisso [bold]{ProxyNaming.IsolatedPrefix}[/] per isolated mode)");
            AnsiConsole.MarkupLine($"[dim]   Esempio: {Markup.Escape(nodeProxy)} --version[/]");
        });
    }
}
