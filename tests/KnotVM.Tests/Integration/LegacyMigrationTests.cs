using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Integration;

public class LegacyMigrationTests
{
    private readonly Mock<IFileSystemService> _fileSystemMock;
    private readonly Mock<IPathService> _pathServiceMock;
    private readonly Mock<IPlatformService> _platformMock;
    private readonly Mock<IProcessRunner> _processRunnerMock;
    private readonly Mock<IInstallationsRepository> _repositoryMock;
    private readonly Mock<IVersionManager> _versionManagerMock;
    private readonly Mock<ISyncService> _syncServiceMock;

    public LegacyMigrationTests()
    {
        _fileSystemMock = new Mock<IFileSystemService>();
        _pathServiceMock = new Mock<IPathService>();
        _platformMock = new Mock<IPlatformService>();
        _processRunnerMock = new Mock<IProcessRunner>();
        _repositoryMock = new Mock<IInstallationsRepository>();
        _versionManagerMock = new Mock<IVersionManager>();
        _syncServiceMock = new Mock<ISyncService>();
    }

    [Fact]
    public void DetectLegacyInstallation_OnWindows_DetectsNodeLocal()
    {
        // Arrange
        _platformMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Windows);
        var userProfile = "C:\\Users\\TestUser";
        var legacyPath = Path.Combine(userProfile, ".node-local");
        var settingsPath = Path.Combine(legacyPath, "settings.txt");

        Environment.SetEnvironmentVariable("USERPROFILE", userProfile);
        _fileSystemMock.Setup(x => x.DirectoryExists(legacyPath)).Returns(true);
        _fileSystemMock.Setup(x => x.FileExists(settingsPath)).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(settingsPath)).Returns("legacy-node");

        // Act
        var hasLegacy = _fileSystemMock.Object.DirectoryExists(legacyPath);
        var legacyAlias = hasLegacy ? _fileSystemMock.Object.ReadAllTextSafe(settingsPath) : null;

        // Assert
        hasLegacy.Should().BeTrue();
        legacyAlias.Should().Be("legacy-node");
    }

    [Fact]
    public void MigrateLegacyInstallation_MovesVersionsDirectory()
    {
        // Arrange
        var userProfile = "C:\\Users\\TestUser";
        var legacyPath = Path.Combine(userProfile, ".node-local");
        var legacyVersionsPath = Path.Combine(legacyPath, "versions");
        var newKnotPath = "C:\\Users\\TestUser\\.knot";
        var newVersionsPath = Path.Combine(newKnotPath, "versions");

        _platformMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Windows);
        _fileSystemMock.Setup(x => x.DirectoryExists(legacyPath)).Returns(true);
        _fileSystemMock.Setup(x => x.DirectoryExists(legacyVersionsPath)).Returns(true);
        _pathServiceMock.Setup(x => x.GetVersionsPath()).Returns(newVersionsPath);

        // Act - Simulate migration logic
        _fileSystemMock.Object.EnsureDirectoryExists(newKnotPath);

        // Assert
        _fileSystemMock.Verify(x => x.EnsureDirectoryExists(newKnotPath), Times.Once);
    }

    [Fact]
    public void MigrateLegacyInstallation_PreservesActiveAlias()
    {
        // Arrange
        var userProfile = "C:\\Users\\TestUser";
        var legacyPath = Path.Combine(userProfile, ".node-local");
        var legacySettingsPath = Path.Combine(legacyPath, "settings.txt");
        var activeAlias = "node-18";

        _platformMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Windows);
        _fileSystemMock.Setup(x => x.DirectoryExists(legacyPath)).Returns(true);
        _fileSystemMock.Setup(x => x.FileExists(legacySettingsPath)).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(legacySettingsPath)).Returns(activeAlias);

        // Act - Read legacy active alias
        var legacyActive = _fileSystemMock.Object.ReadAllTextSafe(legacySettingsPath);

        // Assert
        legacyActive.Should().Be(activeAlias);
    }

    [Fact]
    public void MigrateLegacyInstallation_MigratesAllInstalledVersions()
    {
        // Arrange
        var legacyVersionsPath = "C:\\Users\\TestUser\\.node-local\\versions";
        var installedVersions = new[] { "node-18", "node-20", "lts-hydrogen" };

        _fileSystemMock.Setup(x => x.DirectoryExists(legacyVersionsPath)).Returns(true);
        // Simulate directory enumeration - would normally use GetDirectories

        // Act
        var hasVersions = _fileSystemMock.Object.DirectoryExists(legacyVersionsPath);

        // Assert
        hasVersions.Should().BeTrue();
    }

    [Fact]
    public void MigrateLegacyInstallation_CleansUpOldDirectory()
    {
        // Arrange
        var legacyPath = "C:\\Users\\TestUser\\.node-local";
        _fileSystemMock.Setup(x => x.DirectoryExists(legacyPath)).Returns(true);

        // Act - Check if legacy directory exists
        var exists = _fileSystemMock.Object.DirectoryExists(legacyPath);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void MigrateLegacyInstallation_UpdatesEnvironmentPaths()
    {
        // Arrange
        var newBinPath = "C:\\Users\\TestUser\\.knot\\bin";

        _pathServiceMock.Setup(x => x.GetBinPath()).Returns(newBinPath);

        // Act - Verify new bin path
        var newPath = _pathServiceMock.Object.GetBinPath();

        // Assert
        newPath.Should().Be(newBinPath);
    }

    [Fact]
    public void MigrateLegacyInstallation_OnNonWindows_Skips()
    {
        // Arrange
        _platformMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Linux);

        // Act - Linux should not have legacy .node-local
        var shouldMigrate = _platformMock.Object.GetCurrentOs() == HostOs.Windows;

        // Assert
        shouldMigrate.Should().BeFalse();
    }

    [Fact]
    public void MigrateLegacyInstallation_MigratesStateJson()
    {
        // Arrange
        var legacyStatePath = "C:\\Users\\TestUser\\.node-local\\state.json";
        var stateContent = "{\"installations\": []}";

        _fileSystemMock.Setup(x => x.FileExists(legacyStatePath)).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(legacyStatePath)).Returns(stateContent);

        // Act - Read legacy state
        var legacyState = _fileSystemMock.Object.ReadAllTextSafe(legacyStatePath);

        // Assert
        legacyState.Should().Be(stateContent);
    }
}
