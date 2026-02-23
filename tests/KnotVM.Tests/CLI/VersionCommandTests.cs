using System.CommandLine;
using FluentAssertions;
using KnotVM.CLI.Commands;
using Xunit;

namespace KnotVM.Tests.CLI;

public class VersionCommandTests
{
    [Fact]
    public void Version_DisplaysVersionInfo()
    {
        // Arrange
        var command = new VersionCommand();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Act
        var exitCode = rootCommand.Parse(["version"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
    }
}
