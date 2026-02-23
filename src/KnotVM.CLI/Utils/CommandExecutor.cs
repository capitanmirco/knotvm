using KnotVM.Core.Exceptions;
using Spectre.Console;

namespace KnotVM.CLI.Utils;

/// <summary>
/// Utility centralizzata per gestione errori nei comandi CLI.
/// Elimina duplicazione del pattern try-catch in tutti i comandi.
/// </summary>
public static class CommandExecutor
{
    /// <summary>
    /// Esegue un'azione sincrona e ritorna l'exit code invece di terminare il processo.
    /// </summary>
    public static int ExecuteWithExitCode(Action action)
    {
        return ExecuteWithExitCode(() =>
        {
            action();
            return 0;
        });
    }

    /// <summary>
    /// Esegue un'azione asincrona e ritorna l'exit code invece di terminare il processo.
    /// </summary>
    public static async Task<int> ExecuteWithExitCodeAsync(Func<Task> asyncAction)
    {
        try
        {
            await asyncAction();
            return 0;
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>
    /// Esegue un'azione asincrona che ritorna un exit code.
    /// </summary>
    public static async Task<int> ExecuteWithExitCodeAsync(Func<Task<int>> asyncAction)
    {
        try
        {
            return await asyncAction();
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    /// <summary>
    /// Esegue un'azione sincrona e ritorna l'exit code invece di terminare il processo.
    /// Utile per comandi che devono propagare un exit code specifico.
    /// </summary>
    public static int ExecuteWithExitCode(Func<int> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            return HandleException(ex);
        }
    }

    private static int HandleException(Exception ex)
    {
        switch (ex)
        {
            case KnotVMHintException hintException:
                Exceptions.PrintKnotHintException(hintException);
                return hintException.ExitCode;
            case KnotVMException knotException:
                AnsiConsole.MarkupLine($"[red]{knotException.CodeString}:[/] {knotException.Message}");
                return knotException.ExitCode;
            case OperationCanceledException:
                AnsiConsole.MarkupLine("[yellow]Operazione annullata.[/]");
                return 130;
            default:
                AnsiConsole.MarkupLine("[red]KNOT-GEN-001:[/] Errore inatteso durante l'esecuzione del comando");
                AnsiConsole.MarkupLine("[yellow]Hint:[/] Riprovare o aprire una issue su GitHub con i dettagli dell'errore");
                AnsiConsole.MarkupLine($"[dim]Dettaglio: {ex.Message}[/]");
                return 99;
        }
    }
}
