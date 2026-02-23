using KnotVM.Core.Exceptions;
using Spectre.Console;

namespace KnotVM.CLI.Utils;

/// <summary>
/// Utility per la gestione e la stampa delle eccezioni.
/// </summary>
public static class Exceptions
{
    /// <summary>
    /// Stampa un'eccezione KnotVM con hint.
    /// </summary>
    /// <param name="exception">L'eccezione da stampare.</param>
    public static void PrintKnotHintException(KnotVMHintException exception)
    {
        AnsiConsole.MarkupLine($"[red][[X]] Errore:[/] {exception.Message}");
        AnsiConsole.MarkupLine($"[dim]{exception.Hint}[/]");
    }
}
