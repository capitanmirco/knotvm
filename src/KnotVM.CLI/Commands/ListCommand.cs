using System.CommandLine;
using KnotVM.Core.Interfaces;

namespace KnotVM.CLI.Commands;

/// <summary>
/// Comando per mostrare le versioni di Node.js installate.
/// </summary>
public class ListCommand : Command
{
    private readonly IInstallationsRepository _repository;

    public ListCommand(IInstallationsRepository repository) : base("list", "Mostra le versioni di Node.js installate")
    {
        _repository = repository;
        
        // Handler
        this.SetAction((p) => Execute());
    }

    private void Execute()
    {
        var installations = _repository.GetAll();
        
        if (installations.Length == 0)
        {
            Console.WriteLine("Nessuna installazione trovata.");
            Console.WriteLine("Usa 'knot install <versione>' per installare Node.js.");
            return;
        }
        
        Console.WriteLine($"\nInstallazioni trovate ({installations.Length}):\n");
        
        foreach (var installation in installations)
        {
            var marker = installation.Use ? "*" : " ";
            Console.WriteLine($"{marker} {installation.Alias,-20} (Node.js {installation.Version})");
        }
        
        Console.WriteLine();
        
        var activeInstallation = installations.FirstOrDefault(i => i.Use);
        if (activeInstallation != null)
        {
            Console.WriteLine($"Versione attiva: {activeInstallation.Alias} (Node.js {activeInstallation.Version})");
        }
        else
        {
            Console.WriteLine("Nessuna versione attiva. Usa 'knot use <alias>' per attivare una versione.");
        }
    }
}
