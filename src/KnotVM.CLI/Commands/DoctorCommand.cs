using System.CommandLine;
using KnotVM.CLI.Utils;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per diagnosticare lo stato dell'ambiente KnotVM.
/// Exit code 0 se tutti i check passano, 1 se almeno un check critico fallisce.
/// Usabile in CI: knot doctor || exit 1
/// </summary>
public class DoctorCommand : Command
{
    private readonly IDoctorService _doctorService;
    private readonly Option<bool>   _fixOption;

    public DoctorCommand(IDoctorService doctorService)
        : base("doctor", "Diagnostica lo stato dell'ambiente KnotVM")
    {
        _doctorService = doctorService;

        _fixOption = new Option<bool>(name: "--fix")
        {
            Description = "Tenta la riparazione automatica dove possibile"
        };

        this.Add(_fixOption);

        this.SetAction((context) =>
        {
            var fix = context.GetValue(_fixOption);
            return ExecuteAsync(fix, CancellationToken.None);
        });
    }

    private int ExecuteAsync(bool fix, CancellationToken ct)
    {
        return CommandExecutor.ExecuteWithExitCode(() =>
        {
            var checks = RunChecksAsync(fix, ct).GetAwaiter().GetResult();
            RenderChecks(checks);

            var hasCriticalFailure = checks.Any(c => !c.Passed && !c.IsWarning);
            return hasCriticalFailure ? 1 : 0;
        });
    }

    private async Task<IReadOnlyList<DoctorCheck>> RunChecksAsync(bool fix, CancellationToken ct)
    {
        var checks = await _doctorService.RunAllChecksAsync(ct);

        if (!fix)
            return checks;

        var fixable = checks.Where(c => c.CanAutoFix && !c.Passed).ToList();
        if (fixable.Count > 0)
        {
            AnsiConsole.MarkupLine("[dim]Tentativo riparazione automatica...[/]");
            foreach (var check in fixable)
                await _doctorService.TryAutoFixAsync(check, ct);

            // Riesegui i check dopo il fix
            checks = await _doctorService.RunAllChecksAsync(ct);
        }

        return checks;
    }

    private static void RenderChecks(IReadOnlyList<DoctorCheck> checks)
    {
        AnsiConsole.WriteLine();

        foreach (var check in checks)
        {
            var icon = check.Passed
                ? "[green]✓[/]"
                : check.IsWarning ? "[yellow]![/]" : "[red]✗[/]";

            var detail = check.Detail is not null ? $": {check.Detail}" : string.Empty;
            AnsiConsole.MarkupLine($"{icon} {check.Name}{detail}");

            if (!check.Passed && check.Suggestion is not null)
                AnsiConsole.MarkupLine($"    [dim]→ {check.Suggestion}[/]");
        }

        AnsiConsole.WriteLine();

        var passed   = checks.Count(c => c.Passed);
        var warnings = checks.Count(c => !c.Passed && c.IsWarning);
        var failed   = checks.Count(c => !c.Passed && !c.IsWarning);

        if (failed > 0)
            AnsiConsole.MarkupLine($"[red]{failed} problema/i critico/i[/], {warnings} avviso/i, {passed} OK");
        else if (warnings > 0)
            AnsiConsole.MarkupLine($"[yellow]{warnings} avviso/i[/], {passed} OK");
        else
            AnsiConsole.MarkupLine($"[green]Tutti i check superati ({passed}/{checks.Count})[/]");
    }
}
