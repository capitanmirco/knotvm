using FluentAssertions;
using KnotVM.Core.Common;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class SyncServiceTests
{
    private readonly Mock<IProxyGeneratorService> _proxyGeneratorMock;
    private readonly Mock<IInstallationsRepository> _repositoryMock;
    private readonly Mock<IPathService> _pathServiceMock;
    private readonly Mock<IFileSystemService> _fileSystemMock;
    private readonly Mock<IPlatformService> _platformMock;
    private readonly SyncService _sut;

    public SyncServiceTests()
    {
        _proxyGeneratorMock = new Mock<IProxyGeneratorService>();
        _repositoryMock = new Mock<IInstallationsRepository>();
        _pathServiceMock = new Mock<IPathService>();
        _fileSystemMock = new Mock<IFileSystemService>();
        _platformMock = new Mock<IPlatformService>();

        _pathServiceMock.Setup(x => x.GetBinPath()).Returns("/test/bin");
        _platformMock.Setup(x => x.GetCurrentOs()).Returns(KnotVM.Core.Enums.HostOs.Windows);

        _sut = new SyncService(
            _proxyGeneratorMock.Object,
            _repositoryMock.Object,
            _pathServiceMock.Object,
            _fileSystemMock.Object,
            _platformMock.Object
        );
    }

    [Fact]
    public void Sync_WithNoActiveInstallation_RemovesAllProxies()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetAll()).Returns(Array.Empty<Installation>());

        // Act
        _sut.Sync(force: false);

        // Assert
        _proxyGeneratorMock.Verify(x => x.RemoveAllProxies(), Times.Once);
        _proxyGeneratorMock.Verify(x => x.GenerateGenericProxy(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Sync_WithActiveInstallation_GeneratesProxies()
    {
        // Arrange
        var installation = new Installation("test-node", "20.11.0", "/test/versions/test-node", Use: true);
        _repositoryMock.Setup(x => x.GetAll()).Returns(new[] { installation });

        // Act
        _sut.Sync(force: false);

        // Assert
        _proxyGeneratorMock.Verify(x => x.GenerateGenericProxy("node", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Sync_WithForceTrue_RemovesAllProxiesFirst()
    {
        // Arrange
        var installation = new Installation("test-node", "20.11.0", "/test/versions/test-node", Use: true);
        _repositoryMock.Setup(x => x.GetAll()).Returns(new[] { installation });

        // Act
        _sut.Sync(force: true);

        // Assert
        _proxyGeneratorMock.Verify(x => x.RemoveAllProxies(), Times.Once);
        _proxyGeneratorMock.Verify(x => x.GenerateGenericProxy("node", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void IsSyncNeeded_WithoutBinDirectory_ReturnsTrue()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.DirectoryExists("/test/bin")).Returns(false);

        // Act
        var result = _sut.IsSyncNeeded();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSyncNeeded_WithoutNodeProxy_ReturnsTrue()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.DirectoryExists("/test/bin")).Returns(true);
        _fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

        // Act
        var result = _sut.IsSyncNeeded();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSyncNeeded_WithAllProxies_ReturnsFalse()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.DirectoryExists("/test/bin")).Returns(true);
        _fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);

        // Act
        var result = _sut.IsSyncNeeded();

        // Assert
        result.Should().BeFalse();
    }
}
