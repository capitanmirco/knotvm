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
public class InstallCommandTests
{
    [Fact]
    public void Install_WithExplicitVersion_CallsInstallService()
    {
        // Arrange
        var installServiceMock = new Mock<IInstallationService>();
        var installManagerMock = new Mock<IInstallationManager>();
        
        var expectedResult = new InstallationPrepareResult(
            Success: true,
            Alias: "20.11.0",
            Version: "20.11.0",
            InstallationPath: "/test/versions/20.11.0"
        );

        installServiceMock
            .Setup(x => x.InstallAsync(
                "20.11.0",
                null,
                false,
                It.IsAny<IProgress<DownloadProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var command = new InstallCommand(installServiceMock.Object, installManagerMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Configure AnsiConsole for testing
        var testConsole = new TestConsole();
        testConsole.Profile.Capabilities.Interactive = false;
        AnsiConsole.Console = testConsole;

        // Act
        var exitCode = rootCommand.Parse(["install", "20.11.0"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        installServiceMock.Verify(
            x => x.InstallAsync("20.11.0", null, false, It.IsAny<IProgress<DownloadProgress>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Install_WithLatestFlag_ResolveToLatest()
    {
        // Arrange
        var installServiceMock = new Mock<IInstallationService>();
        var installManagerMock = new Mock<IInstallationManager>();
        
        var expectedResult = new InstallationPrepareResult(
            Success: true,
            Alias: "21.0.0",
            Version: "21.0.0",
            InstallationPath: "/test/versions/21.0.0"
        );

        installServiceMock
            .Setup(x => x.InstallAsync(
                "latest",
                null,
                false,
                It.IsAny<IProgress<DownloadProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var command = new InstallCommand(installServiceMock.Object, installManagerMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Act
        var exitCode = rootCommand.Parse(["install", "--latest"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        installServiceMock.Verify(
            x => x.InstallAsync("latest", null, false, It.IsAny<IProgress<DownloadProgress>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
