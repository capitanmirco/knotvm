using System.CommandLine;
using KnotVM.CLI.Utils;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using Spectre.Console;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Genera script di completamento shell per KnotVM.
/// Supporta Bash, Zsh, PowerShell e Fish.
/// </summary>
public class CompletionCommand : Command
{
    private readonly ICompletionGenerator _generator;
    private readonly Argument<string> _shellArgument;

    /// <summary>
    /// Inizializza il comando completion con il generatore di script.
    /// </summary>
    /// <param name="generator">Servizio generatore script di completamento</param>
    public CompletionCommand(ICompletionGenerator generator)
        : base("completion", "Genera script di completamento shell (bash, zsh, powershell, fish)")
    {
        _generator = generator;

        _shellArgument = new Argument<string>(name: "shell")
        {
            Description = "Shell target: bash, zsh, powershell, fish"
        };

        this.Add(_shellArgument);

        this.SetAction(async context =>
        {
            var shell = context.GetValue(_shellArgument)!;
            return await ExecuteAsync(shell);
        });
    }

    private async Task<int> ExecuteAsync(string shell)
    {
        return await CommandExecutor.ExecuteWithExitCodeAsync(async () =>
        {
            var shellType = ParseShellType(shell);
            var script = await _generator.GenerateCompletionScriptAsync(shellType).ConfigureAwait(false);

            // Output diretto su stdout senza markup Spectre per permettere pipe/redirect
            Console.WriteLine(script);

            return 0;
        });
    }

    private static ShellType ParseShellType(string shell)
    {
        return shell.Trim().ToLowerInvariant() switch
        {
            "bash" => ShellType.Bash,
            "zsh" => ShellType.Zsh,
            "powershell" or "pwsh" => ShellType.PowerShell,
            "fish" => ShellType.Fish,
            _ => throw new KnotVMException(
                KnotErrorCode.InvalidArgument,
                $"Shell '{Markup.Escape(shell)}' non supportata. Shell supportate: bash, zsh, powershell, fish")
        };
    }
}
