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
        var detectorMock = new Mock<IVersionFileDetector>();
        var versionResolverMock = new Mock<IVersionResolver>();

        versionResolverMock
            .Setup(x => x.ResolveVersionAsync("20.11.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync("20.11.0");
        versionResolverMock
            .Setup(x => x.IsExactVersion("20.11.0"))
            .Returns(true);
        
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

        var command = new InstallCommand(installServiceMock.Object, installManagerMock.Object, detectorMock.Object, versionResolverMock.Object);
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
        var detectorMock = new Mock<IVersionFileDetector>();
        var versionResolverMock = new Mock<IVersionResolver>();

        // Resolver maps "latest" → "21.0.0" (exact resolved version)
        versionResolverMock
            .Setup(x => x.ResolveVersionAsync("latest", It.IsAny<CancellationToken>()))
            .ReturnsAsync("21.0.0");
        versionResolverMock
            .Setup(x => x.IsExactVersion("latest"))
            .Returns(false);
        
        var expectedResult = new InstallationPrepareResult(
            Success: true,
            Alias: "21.0.0",
            Version: "21.0.0",
            InstallationPath: "/test/versions/21.0.0"
        );

        // InstallAsync now receives the resolved version ("21.0.0"), not the original pattern ("latest")
        installServiceMock
            .Setup(x => x.InstallAsync(
                "21.0.0",
                null,
                false,
                It.IsAny<IProgress<DownloadProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var command = new InstallCommand(installServiceMock.Object, installManagerMock.Object, detectorMock.Object, versionResolverMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);

        // Act
        var exitCode = rootCommand.Parse(["install", "--latest"]).Invoke();

        // Assert
        exitCode.Should().Be(0);
        versionResolverMock.Verify(
            x => x.ResolveVersionAsync("latest", It.IsAny<CancellationToken>()),
            Times.Once);
        installServiceMock.Verify(
            x => x.InstallAsync("21.0.0", null, false, It.IsAny<IProgress<DownloadProgress>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
