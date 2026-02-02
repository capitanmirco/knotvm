using System.CommandLine;
using KnotVM.Core.Common;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per mostrare le versioni di Node.js installate.
/// </summary>
public class ListCommand : Command
{
    public ListCommand() : base("list", "Mostra le versioni di Node.js installate")
    {
        // Handler
        base.SetAction((p) => Execute());
    }

    private void Execute()
    {
        Console.WriteLine("Mostrerà le versioni installate");
    }
}
