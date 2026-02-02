using System.CommandLine;
using KnotVM.CLI.Commands;

namespace KnotVM.CLI;

class Program
{
    static int Main(string[] args)
    {
        // Configurazione root command
        var rootCommand = new RootCommand("knot - Gestore versioni Node.js per Windows");

        // Aggiungi comandi
        rootCommand.Subcommands.Add(new ListCommand());

        // Esegui
        return rootCommand.Parse(args).Invoke();
    }
}
