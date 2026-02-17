using System.CommandLine;
using FluentAssertions;
using KnotVM.CLI.Commands;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Moq;
using Xunit;

namespace KnotVM.Tests.CLI;

public class RenameCommandTests
{
    [Fact]
    public void Rename_ValidAliases_CallsInstallationManager()
    {
        // Arrange
        var installManagerMock = new Mock<IInstallationManager>();
        var repositoryMock = new Mock<IInstallationsRepository>();
        
        var installation = new Installation("old-node", "20.11.0", "/test/versions/old-node", Use: false);
        repositoryMock.Setup(x => x.GetByAlias("old-node")).Returns(installation);

        var command = new RenameCommand(installManagerMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Act
        var exitCode = rootCommand.Parse(["rename", "--from", "old-node", "--to", "new-node"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        installManagerMock.Verify(x => x.RenameInstallation("old-node", "new-node"), Times.Once);
    }
}
