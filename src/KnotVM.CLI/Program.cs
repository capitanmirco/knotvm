using System.CommandLine;
using KnotVM.CLI.Commands;
using KnotVM.Core.Common;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace KnotVM.CLI;

class Program
{
    static int Main(string[] args)
    {
        // Setup DI container
        var services = new ServiceCollection();
        
        // Registra Configuration (singleton)
        services.AddSingleton(Configuration.Instance);
        
        // Registra repository
        services.AddSingleton<IInstallationsRepository, LocalInstallationsRepository>();
        
        // Registra comandi
        services.AddSingleton<ListCommand>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Configurazione root command
        var rootCommand = new RootCommand("knot - Gestore versioni Node.js per Windows");

        // Aggiungi comandi (risolvi dal DI container)
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<ListCommand>());

        // Esegui
        return rootCommand.Parse(args).Invoke();
    }
}
