using System.CommandLine;
using FluentAssertions;
using KnotVM.CLI.Commands;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Moq;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace KnotVM.Tests.CLI;

[Collection("Sequential")]
public class UseCommandTests
{
    [Fact]
    public void Use_WithExistingAlias_CallsInstallationManager()
    {
        // Arrange
        var installManagerMock = new Mock<IInstallationManager>();
        var repositoryMock = new Mock<IInstallationsRepository>();
        var detectorMock = new Mock<IVersionFileDetector>();
        var versionResolverMock = new Mock<IVersionResolver>();
        
        var installation = new Installation("test-node", "20.11.0", "/test/versions/test-node", Use: false);
        repositoryMock.Setup(x => x.GetByAlias("test-node")).Returns(installation);

        var command = new UseCommand(installManagerMock.Object, repositoryMock.Object, detectorMock.Object, versionResolverMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Configure AnsiConsole for testing
        var testConsole = new TestConsole();
        testConsole.Profile.Capabilities.Interactive = false;
        AnsiConsole.Console = testConsole;

        // Act  
        var exitCode = rootCommand.Parse(["use", "test-node"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        installManagerMock.Verify(x => x.UseInstallation("test-node"), Times.Once);
    }
}
