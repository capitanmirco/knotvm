using System.CommandLine;
using KnotVM.CLI.Utils;
using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per rilevare automaticamente la versione Node.js dal file di configurazione progetto
/// (.nvmrc, .node-version, package.json) e attivare l'installazione corrispondente.
/// </summary>
public class AutoDetectCommand : Command
{
    private readonly IVersionFileDetector _detector;
    private readonly IInstallationManager _installationManager;
    private readonly IInstallationsRepository _repository;
    private readonly Option<string?> _directoryOption;

    public AutoDetectCommand(
        IVersionFileDetector detector,
        IInstallationManager installationManager,
        IInstallationsRepository repository)
        : base("auto-detect", "Rileva e usa versione Node.js da file di configurazione progetto")
    {
        _detector = detector;
        _installationManager = installationManager;
        _repository = repository;

        _directoryOption = new Option<string?>(name: "--directory")
        {
            Description = "Directory da scansionare (default: directory corrente)"
        };

        this.Add(_directoryOption);

        this.SetAction(async (context) =>
        {
            var directory = context.GetValue(_directoryOption) ?? Directory.GetCurrentDirectory();
            return await ExecuteAsync(directory);
        });
    }

    private async Task<int> ExecuteAsync(string directory)
    {
        return await CommandExecutor.ExecuteWithExitCodeAsync(async () =>
        {
            var detectedVersion = await _detector.DetectVersionAsync(directory).ConfigureAwait(false);

            if (detectedVersion == null)
            {
                throw new KnotVMHintException(
                    KnotErrorCode.VersionFileNotFound,
                    "Nessun file di configurazione versione trovato",
                    "File supportati: .nvmrc, .node-version, package.json (campo engines.node)");
            }

            AnsiConsole.MarkupLine($"[dim]Versione rilevata: [/][cyan]{Markup.Escape(detectedVersion)}[/]");

            // Cerca prima per alias, poi per versione
            var installation = _repository.GetByAlias(detectedVersion)
                ?? _repository.GetByVersion(detectedVersion);

            if (installation == null)
            {
                throw new KnotVMHintException(
                    KnotErrorCode.InstallationNotFound,
                    $"Versione '{detectedVersion}' rilevata ma non installata",
                    $"Usa 'knot install --from-file' per installarla automaticamente");
            }

            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .Start("Attivazione in corso...", ctx =>
                {
                    ctx.Status("Configurazione versione attiva...");
                    _installationManager.UseInstallation(installation.Alias);
                });

            AnsiConsole.MarkupLine($"[green][[OK]][/] Versione [bold]{Markup.Escape(installation.Alias)}[/] attivata (Node.js {Markup.Escape(installation.Version)})");
            AnsiConsole.WriteLine();
            var nodeProxy = ProxyNaming.BuildIsolatedProxyName("node");
            AnsiConsole.MarkupLine($"[green]->[/] Usa '{Markup.Escape(nodeProxy)} --version' per verificare");
        });
    }
}
