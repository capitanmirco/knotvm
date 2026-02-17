using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class VersionManagerTests
{
    private readonly Mock<IPathService> _pathServiceMock;
    private readonly Mock<IFileSystemService> _fileSystemMock;
    private readonly Mock<IInstallationsRepository> _repositoryMock;
    private readonly VersionManager _sut;
    private const string SettingsFilePath = "/test/settings.txt";

    public VersionManagerTests()
    {
        _pathServiceMock = new Mock<IPathService>();
        _fileSystemMock = new Mock<IFileSystemService>();
        _repositoryMock = new Mock<IInstallationsRepository>();

        _pathServiceMock.Setup(x => x.GetSettingsFilePath()).Returns(SettingsFilePath);

        _sut = new VersionManager(_pathServiceMock.Object, _fileSystemMock.Object, _repositoryMock.Object);
    }

    [Fact]
    public void GetActiveAlias_WithExistingSettingsFile_ReturnsAlias()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(SettingsFilePath)).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(SettingsFilePath)).Returns("test-node");

        // Act
        var result = _sut.GetActiveAlias();

        // Assert
        result.Should().Be("test-node");
    }

    [Fact]
    public void GetActiveAlias_WithoutSettingsFile_ReturnsNull()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(SettingsFilePath)).Returns(false);

        // Act
        var result = _sut.GetActiveAlias();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetActiveInstallation_WithValidAlias_ReturnsInstallation()
    {
        // Arrange
        var installation = new Installation("test-node", "20.11.0", "/test/path", Use: true);
        _fileSystemMock.Setup(x => x.FileExists(SettingsFilePath)).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(SettingsFilePath)).Returns("test-node");
        _repositoryMock.Setup(x => x.GetAll()).Returns(new[] { installation });

        // Act
        var result = _sut.GetActiveInstallation();

        // Assert
        result.Should().NotBeNull();
        result!.Alias.Should().Be("test-node");
    }

    [Fact]
    public void UseVersion_WithValidAlias_UpdatesSettingsFile()
    {
        // Arrange
        var installation = new Installation("test-node", "20.11.0", "/test/path", Use: false);
        _repositoryMock.Setup(x => x.GetAll()).Returns(new[] { installation });

        // Act
        _sut.UseVersion("test-node");

        // Assert
        _fileSystemMock.Verify(x => x.WriteAllTextSafe(SettingsFilePath, "test-node"), Times.Once);
        _repositoryMock.Verify(x => x.SetActiveInstallation("test-node"), Times.Once);
    }

    [Fact]
    public void UseVersion_WithNonExistingAlias_ThrowsKnotVMException()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetAll()).Returns(Array.Empty<Installation>());

        // Act
        var act = () => _sut.UseVersion("non-existing");

        // Assert
        act.Should().Throw<KnotVMException>()
            .Which.ErrorCode.Should().Be(KnotErrorCode.InstallationNotFound);
    }

    [Fact]
    public void UnuseVersion_RemovesSettingsFile()
    {
        // Act
        _sut.UnuseVersion();

        // Assert
        _fileSystemMock.Verify(x => x.DeleteFileIfExists(SettingsFilePath), Times.Once);
    }

    [Fact]
    public void UpdateSettingsFile_WithNullAlias_RemovesFile()
    {
        // Act
        _sut.UpdateSettingsFile(null);

        // Assert
        _fileSystemMock.Verify(x => x.DeleteFileIfExists(SettingsFilePath), Times.Once);
    }

    [Fact]
    public void UpdateSettingsFile_WithValidAlias_WritesFile()
    {
        // Act
        _sut.UpdateSettingsFile("test-node");

        // Assert
        _fileSystemMock.Verify(x => x.WriteAllTextSafe(SettingsFilePath, "test-node"), Times.Once);
    }

    [Fact]
    public void IsAliasActive_WithActiveAlias_ReturnsTrue()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(SettingsFilePath)).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(SettingsFilePath)).Returns("test-node");

        // Act
        var result = _sut.IsAliasActive("test-node");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAliasActive_WithDifferentAlias_ReturnsFalse()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(SettingsFilePath)).Returns(true);
        _fileSystemMock.Setup(x => x.ReadAllTextSafe(SettingsFilePath)).Returns("other-node");

        // Act
        var result = _sut.IsAliasActive("test-node");

        // Assert
        result.Should().BeFalse();
    }
}
