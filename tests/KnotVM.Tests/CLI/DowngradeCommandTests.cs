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
public class DowngradeCommandTests
{
    private static TestConsole SetupTestConsole()
    {
        var testConsole = new TestConsole();
        testConsole.Profile.Capabilities.Interactive = false;
        AnsiConsole.Console = testConsole;
        return testConsole;
    }

    [Fact]
    public async Task Downgrade_LtsAlias_WithTarget_CallsReplaceVersionAsync()
    {
        // Arrange
        var upgradeServiceMock = new Mock<IUpgradeDowngradeService>();
        var repositoryMock = new Mock<IInstallationsRepository>();

        var installation = new Installation("lts", "22.14.0", "/test/versions/lts", Use: false);
        repositoryMock.Setup(x => x.GetByAlias("lts")).Returns(installation);

        upgradeServiceMock.Setup(x => x.IsLtsAlias("lts")).Returns(true);

        var expectedResult = new InstallationPrepareResult(true, "lts", "20.18.0", "/test/versions/lts");
        upgradeServiceMock
            .Setup(x => x.ReplaceVersionAsync("lts", false, "20.18.0",
                It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var command = new DowngradeCommand(upgradeServiceMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        SetupTestConsole();

        // Act
        var exitCode = await rootCommand.Parse(["downgrade", "lts", "--target", "20.18.0", "--yes"]).InvokeAsync();

        // Assert
        exitCode.Should().Be(0);
        upgradeServiceMock.Verify(x => x.ReplaceVersionAsync(
            "lts", false, "20.18.0",
            It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Downgrade_NonLtsAlias_WithTarget_CallsReplaceVersionAsync()
    {
        // Arrange
        var upgradeServiceMock = new Mock<IUpgradeDowngradeService>();
        var repositoryMock = new Mock<IInstallationsRepository>();

        var installation = new Installation("production", "22.14.0", "/test/versions/production", Use: true);
        repositoryMock.Setup(x => x.GetByAlias("production")).Returns(installation);

        upgradeServiceMock.Setup(x => x.IsLtsAlias("production")).Returns(false);

        var expectedResult = new InstallationPrepareResult(true, "production", "20.11.0", "/test/versions/production");
        upgradeServiceMock
            .Setup(x => x.ReplaceVersionAsync("production", true, "20.11.0",
                It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var command = new DowngradeCommand(upgradeServiceMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        SetupTestConsole();

        // Act
        var exitCode = await rootCommand.Parse(["downgrade", "production", "--target", "20.11.0", "--yes"]).InvokeAsync();

        // Assert
        exitCode.Should().Be(0);
        upgradeServiceMock.Verify(x => x.ReplaceVersionAsync(
            "production", true, "20.11.0",
            It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Downgrade_WithActiveAlias_PassesWasActiveTrue()
    {
        // Arrange
        var upgradeServiceMock = new Mock<IUpgradeDowngradeService>();
        var repositoryMock = new Mock<IInstallationsRepository>();

        var installation = new Installation("lts", "22.14.0", "/test/versions/lts", Use: true);
        repositoryMock.Setup(x => x.GetByAlias("lts")).Returns(installation);

        upgradeServiceMock.Setup(x => x.IsLtsAlias("lts")).Returns(true);

        var expectedResult = new InstallationPrepareResult(true, "lts", "20.18.0", "/test/versions/lts");
        upgradeServiceMock
            .Setup(x => x.ReplaceVersionAsync("lts", true, "20.18.0",
                It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var command = new DowngradeCommand(upgradeServiceMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        SetupTestConsole();

        // Act
        var exitCode = await rootCommand.Parse(["downgrade", "lts", "--target", "20.18.0", "--yes"]).InvokeAsync();

        // Assert
        exitCode.Should().Be(0);
        upgradeServiceMock.Verify(x => x.ReplaceVersionAsync(
            "lts", true, "20.18.0",
            It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Downgrade_WithNonExistentAlias_ReturnsErrorCode()
    {
        // Arrange
        var upgradeServiceMock = new Mock<IUpgradeDowngradeService>();
        var repositoryMock = new Mock<IInstallationsRepository>();

        repositoryMock.Setup(x => x.GetByAlias("nonexistent")).Returns((Installation?)null);

        var command = new DowngradeCommand(upgradeServiceMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        SetupTestConsole();

        // Act
        var exitCode = await rootCommand.Parse(["downgrade", "nonexistent", "--target", "20.18.0"]).InvokeAsync();

        // Assert
        exitCode.Should().NotBe(0);
        upgradeServiceMock.Verify(x => x.ReplaceVersionAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
            It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Downgrade_WithSameTargetVersion_PrintsMessageAndExitsZero()
    {
        // Arrange
        var upgradeServiceMock = new Mock<IUpgradeDowngradeService>();
        var repositoryMock = new Mock<IInstallationsRepository>();

        var installation = new Installation("production", "20.11.0", "/test/versions/production", Use: false);
        repositoryMock.Setup(x => x.GetByAlias("production")).Returns(installation);

        upgradeServiceMock.Setup(x => x.IsLtsAlias("production")).Returns(false);

        var command = new DowngradeCommand(upgradeServiceMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        SetupTestConsole();

        // Act
        var exitCode = await rootCommand.Parse(["downgrade", "production", "--target", "20.11.0", "--yes"]).InvokeAsync();

        // Assert
        exitCode.Should().Be(0);
        upgradeServiceMock.Verify(x => x.ReplaceVersionAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
            It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Downgrade_WhenInstallFails_ReturnsNonZeroExitCode()
    {
        // Arrange
        var upgradeServiceMock = new Mock<IUpgradeDowngradeService>();
        var repositoryMock = new Mock<IInstallationsRepository>();

        var installation = new Installation("lts", "22.14.0", "/test/versions/lts", Use: false);
        repositoryMock.Setup(x => x.GetByAlias("lts")).Returns(installation);

        upgradeServiceMock.Setup(x => x.IsLtsAlias("lts")).Returns(true);

        var failResult = new InstallationPrepareResult(
            false, "lts", "20.18.0", string.Empty,
            ErrorMessage: "Artifact non disponibile", ErrorCode: "ArtifactNotAvailable");

        upgradeServiceMock
            .Setup(x => x.ReplaceVersionAsync("lts", false, "20.18.0",
                It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failResult);

        var command = new DowngradeCommand(upgradeServiceMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        SetupTestConsole();

        // Act
        var exitCode = await rootCommand.Parse(["downgrade", "lts", "--target", "20.18.0", "--yes"]).InvokeAsync();

        // Assert
        exitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task Downgrade_LtsAlias_WhenNoLtsVersionsAvailable_ReturnsRemoteApiError()
    {
        // Arrange
        var upgradeServiceMock = new Mock<IUpgradeDowngradeService>();
        var repositoryMock = new Mock<IInstallationsRepository>();

        var installation = new Installation("lts", "22.14.0", "/test/versions/lts", Use: false);
        repositoryMock.Setup(x => x.GetByAlias("lts")).Returns(installation);

        upgradeServiceMock.Setup(x => x.IsLtsAlias("lts")).Returns(true);
        upgradeServiceMock
            .Setup(x => x.GetRecentLtsVersionsAsync("22.14.0", 15, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var command = new DowngradeCommand(upgradeServiceMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        SetupTestConsole();

        // Act — nessun --target: entra nel flusso interattivo LTS
        var exitCode = await rootCommand.Parse(["downgrade", "lts"]).InvokeAsync();

        // Assert: nessuna versione → errore RemoteApiFailed
        exitCode.Should().NotBe(0);
        upgradeServiceMock.Verify(x => x.ReplaceVersionAsync(
            It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
            It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Downgrade_WithTargetHavingLeadingV_StripsVBeforePassingToService()
    {
        // Arrange
        var upgradeServiceMock = new Mock<IUpgradeDowngradeService>();
        var repositoryMock = new Mock<IInstallationsRepository>();

        var installation = new Installation("lts", "22.14.0", "/test/versions/lts", Use: false);
        repositoryMock.Setup(x => x.GetByAlias("lts")).Returns(installation);

        upgradeServiceMock.Setup(x => x.IsLtsAlias("lts")).Returns(true);

        var expectedResult = new InstallationPrepareResult(true, "lts", "20.18.0", "/test/versions/lts");
        upgradeServiceMock
            .Setup(x => x.ReplaceVersionAsync("lts", false, "20.18.0",
                It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var command = new DowngradeCommand(upgradeServiceMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        SetupTestConsole();

        // Act — passiamo "v20.18.0" con la 'v' iniziale
        var exitCode = await rootCommand.Parse(["downgrade", "lts", "--target", "v20.18.0", "--yes"]).InvokeAsync();

        // Assert: il servizio deve ricevere "20.18.0" (senza la 'v')
        exitCode.Should().Be(0);
        upgradeServiceMock.Verify(x => x.ReplaceVersionAsync(
            "lts", false, "20.18.0",
            It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Downgrade_WithTarget_BypassesInteractiveSelection_AndNeverCallsGetRecentLts()
    {
        // Verifica che --target bypassa completamente GetRecentLtsVersionsAsync
        var upgradeServiceMock = new Mock<IUpgradeDowngradeService>();
        var repositoryMock = new Mock<IInstallationsRepository>();

        var installation = new Installation("lts", "22.14.0", "/test/versions/lts", Use: false);
        repositoryMock.Setup(x => x.GetByAlias("lts")).Returns(installation);

        upgradeServiceMock.Setup(x => x.IsLtsAlias("lts")).Returns(true);

        var expectedResult = new InstallationPrepareResult(true, "lts", "20.18.0", "/test/versions/lts");
        upgradeServiceMock
            .Setup(x => x.ReplaceVersionAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var command = new DowngradeCommand(upgradeServiceMock.Object, repositoryMock.Object);
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(command);
        SetupTestConsole();

        await rootCommand.Parse(["downgrade", "lts", "--target", "20.18.0", "--yes"]).InvokeAsync();

        // Con --target, GetRecentLtsVersionsAsync NON deve essere chiamato
        upgradeServiceMock.Verify(x => x.GetRecentLtsVersionsAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        // ReplaceVersionAsync deve essere chiamato con la versione target corretta
        upgradeServiceMock.Verify(x => x.ReplaceVersionAsync(
            "lts", false, "20.18.0",
            It.IsAny<IProgress<DownloadProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
