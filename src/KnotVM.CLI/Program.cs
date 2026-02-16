using System.CommandLine;
using KnotVM.CLI.Commands;
using KnotVM.CLI.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace KnotVM.CLI;

class Program
{
    static int Main(string[] args)
    {
        // Setup DI container
        var services = new ServiceCollection();
        
        // Registra servizi e comandi
        services.AddKnotVMServices();
        services.AddKnotVMCommands();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Configurazione root command
        var rootCommand = new RootCommand("knot - Gestore versioni Node.js cross-platform");

        // Aggiungi comandi (risolvi dal DI container)
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<ListCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<ListRemoteCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<InstallCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<UseCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<SyncCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<RemoveCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<RenameCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<RunCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<CacheCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<VersionCommand>());

        // Esegui
        return rootCommand.Parse(args).Invoke();
    }
}
