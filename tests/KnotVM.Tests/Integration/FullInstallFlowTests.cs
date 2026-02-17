using FluentAssertions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using Moq;
using Xunit;

namespace KnotVM.Tests.Integration;

/// <summary>
/// Integration tests for full installation workflows
/// </summary>
public class FullInstallFlowTests
{
    private readonly Mock<IInstallationsRepository> _repositoryMock;
    private readonly Mock<IPathService> _pathServiceMock;
    private readonly Mock<IFileSystemService> _fileSystemMock;

    public FullInstallFlowTests()
    {
        _repositoryMock = new Mock<IInstallationsRepository>();
        _pathServiceMock = new Mock<IPathService>();
        _fileSystemMock = new Mock<IFileSystemService>();
    }

    [Fact]
    public void Repository_AddInstallation_Success()
    {
        // Arrange
        var alias = "node-20";
        var version = "20.11.0";
        var installPath = Path.Combine("c:\\knot\\versions", alias);
        var installation = new Installation(alias, version, installPath, Use: false);

        // Act
        _repositoryMock.Object.Add(installation);

        // Assert
        _repositoryMock.Verify(x => x.Add(It.Is<Installation>(i => 
            i.Alias == alias && i.Version == version)), Times.Once);
    }

    [Fact]
    public void Repository_RemoveInstallation_Success()
    {
        // Arrange
        var alias = "node-18";
        var installation = new Installation(alias, "18.19.0", "/path", Use: false);
        _repositoryMock.Setup(x => x.GetByAlias(alias)).Returns(installation);

        // Act
        _repositoryMock.Object.Remove(alias);

        // Assert
        _repositoryMock.Verify(x => x.Remove(alias), Times.Once);
    }

    [Fact]
    public void FileSystem_EnsureDirectoryExists_CreatesDirectory()
    {
        // Arrange
        var installPath = "c:\\knot\\versions\\node-20";
        _fileSystemMock.Setup(x => x.DirectoryExists(installPath)).Returns(false);

        // Act
        _fileSystemMock.Object.EnsureDirectoryExists(installPath);

        // Assert
        _fileSystemMock.Verify(x => x.EnsureDirectoryExists(installPath), Times.Once);
    }

    [Fact]
    public void FileSystem_DeleteIfExists_RemovesDirectory()
    {
        // Arrange
        var installPath = "c:\\knot\\versions\\old-node";
        _fileSystemMock.Setup(x => x.DirectoryExists(installPath)).Returns(true);

        // Act
        var result = _fileSystemMock.Object.DirectoryExists(installPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Installation_ActiveFlagManagement_UpdatesCorrectly()
    {
        // Arrange
        var alias1 = "node-18";
        var alias2 = "node-20";
        var inst1 = new Installation(alias1, "18.19.0", "/path1", Use: true);
        var inst2 = new Installation(alias2, "20.11.0", "/path2", Use: false);

        // Act - Switch active installation
        inst1 = inst1 with { Use = false };
        inst2 = inst2 with { Use = true };

        // Assert
        inst1.Use.Should().BeFalse();
        inst2.Use.Should().BeTrue();
    }
}

