using System.CommandLine;
using FluentAssertions;
using KnotVM.CLI.Commands;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Moq;
using Xunit;

namespace KnotVM.Tests.CLI;

public class ListRemoteCommandTests
{
    [Fact]
    public void ListRemote_Default_CallsRemoteVersionService()
    {
        // Arrange
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        var versions = new[]
        {
            new RemoteVersion("21.0.0", null, "2023-10-01", new[] { "win-x64", "linux-x64" }),
            new RemoteVersion("20.11.0", "Iron", "2023-11-01", new[] { "win-x64", "linux-x64" })
        };
        remoteServiceMock.Setup(x => x.GetAvailableVersionsAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(versions);

        var command = new ListRemoteCommand(remoteServiceMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Act
        var exitCode = rootCommand.Parse(["list-remote"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
    }
}
