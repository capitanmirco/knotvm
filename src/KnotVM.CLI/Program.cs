using System.CommandLine;
using KnotVM.CLI.Commands;
using KnotVM.CLI.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace KnotVM.CLI;

class Program
{
    static int Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddKnotVMServices();
        services.AddKnotVMCommands();
        
        var serviceProvider = services.BuildServiceProvider();
        var rootCommand = new RootCommand("knot - Gestore versioni Node.js cross-platform");

        foreach (var command in new Command[]
        {
            serviceProvider.GetRequiredService<ListCommand>(),
            serviceProvider.GetRequiredService<ListRemoteCommand>(),
            serviceProvider.GetRequiredService<InstallCommand>(),
            serviceProvider.GetRequiredService<UseCommand>(),
            serviceProvider.GetRequiredService<SyncCommand>(),
            serviceProvider.GetRequiredService<RemoveCommand>(),
            serviceProvider.GetRequiredService<RenameCommand>(),
            serviceProvider.GetRequiredService<RunCommand>(),
            serviceProvider.GetRequiredService<CacheCommand>(),
            serviceProvider.GetRequiredService<VersionCommand>(),
            serviceProvider.GetRequiredService<AutoDetectCommand>(),
            serviceProvider.GetRequiredService<CompletionCommand>()
        })
        {
            rootCommand.Subcommands.Add(command);
        }

        return rootCommand.Parse(args).Invoke();
    }
}
