using System.CommandLine;
using FluentAssertions;
using KnotVM.CLI.Commands;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Moq;
using Xunit;

namespace KnotVM.Tests.CLI;

public class ListCommandTests
{
    [Fact]
    public void List_WithInstallations_DisplaysTable()
    {
        // Arrange
        var repositoryMock = new Mock<IInstallationsRepository>();
        var installations = new[]
        {
            new Installation("lts", "20.11.0", "/test/versions/lts", Use: true),
            new Installation("latest", "21.0.0", "/test/versions/latest", Use: false)
        };
        repositoryMock.Setup(x => x.GetAll()).Returns(installations);

        var command = new ListCommand(repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Act
        var exitCode = rootCommand.Parse(["list"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        repositoryMock.Verify(x => x.GetAll(), Times.Once);
    }
}
