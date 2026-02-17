using System.CommandLine;
using FluentAssertions;
using KnotVM.CLI.Commands;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Moq;
using Xunit;

namespace KnotVM.Tests.CLI;

public class RemoveCommandTests
{
    [Fact]
    public void Remove_WithExistingAlias_CallsInstallationManager()
    {
        // Arrange
        var installManagerMock = new Mock<IInstallationManager>();
        var repositoryMock = new Mock<IInstallationsRepository>();
        
        var installation = new Installation("test-node", "20.11.0", "/test/versions/test-node", Use: false);
        repositoryMock.Setup(x => x.GetByAlias("test-node")).Returns(installation);

        var command = new RemoveCommand(installManagerMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Act
        var exitCode = rootCommand.Parse(["remove", "test-node"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        installManagerMock.Verify(x => x.RemoveInstallation("test-node", false), Times.Once);
    }
}
