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
public class SyncCommandTests
{
    [Fact]
    public void Sync_WithInstallations_CallsSyncService()
    {
        // Arrange
        var syncServiceMock = new Mock<ISyncService>();
        var repositoryMock = new Mock<IInstallationsRepository>();
        var lockManagerMock = new Mock<ILockManager>();
        var lockHandleMock = new Mock<IDisposable>();
        
        lockManagerMock.Setup(x => x.AcquireLock(It.IsAny<string>(), It.IsAny<int>())).Returns(lockHandleMock.Object);
        
        var installations = new[] { new Installation("node1", "20.11.0", "/test/versions/node1", Use: true) };
        repositoryMock.Setup(x => x.GetAll()).Returns(installations);
        syncServiceMock.Setup(x => x.IsSyncNeeded()).Returns(true);

        var command = new SyncCommand(syncServiceMock.Object, repositoryMock.Object, lockManagerMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Configure AnsiConsole for testing
        var testConsole = new TestConsole();
        testConsole.Profile.Capabilities.Interactive = false;
        AnsiConsole.Console = testConsole;

        // Act
        var exitCode = rootCommand.Parse(["sync"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        syncServiceMock.Verify(x => x.Sync(false), Times.Once);
    }
}
