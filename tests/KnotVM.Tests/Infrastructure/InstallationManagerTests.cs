using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class InstallationManagerTests
{
    private readonly Mock<IInstallationsRepository> _repositoryMock;
    private readonly Mock<IVersionManager> _versionManagerMock;
    private readonly Mock<IInstallationService> _installationServiceMock;
    private readonly Mock<IPathService> _pathServiceMock;
    private readonly Mock<IFileSystemService> _fileSystemMock;
    private readonly Mock<ISyncService> _syncServiceMock;
    private readonly Mock<ILockManager> _lockManagerMock;
    private readonly Mock<IProcessRunner> _processRunnerMock;
    private readonly Mock<IDisposable> _lockHandleMock;
    private readonly InstallationManager _sut;

    public InstallationManagerTests()
    {
        _repositoryMock = new Mock<IInstallationsRepository>();
        _versionManagerMock = new Mock<IVersionManager>();
        _installationServiceMock = new Mock<IInstallationService>();
        _pathServiceMock = new Mock<IPathService>();
        _fileSystemMock = new Mock<IFileSystemService>();
        _syncServiceMock = new Mock<ISyncService>();
        _lockManagerMock = new Mock<ILockManager>();
        _processRunnerMock = new Mock<IProcessRunner>();
        _lockHandleMock = new Mock<IDisposable>();

        _lockManagerMock.Setup(x => x.AcquireLock(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(_lockHandleMock.Object);

        // Setup default: nessun processo in esecuzione
        _processRunnerMock.Setup(x => x.FindRunningProcesses(It.IsAny<string>()))
            .Returns(new List<int>());

        _sut = new InstallationManager(
            _repositoryMock.Object,
            _versionManagerMock.Object,
            _installationServiceMock.Object,
            _pathServiceMock.Object,
            _fileSystemMock.Object,
            _syncServiceMock.Object,
            _lockManagerMock.Object,
            _processRunnerMock.Object
        );
    }

    #region IsAliasValid Tests

    [Theory]
    [InlineData("valid-alias", true)]
    [InlineData("valid_alias", true)]
    [InlineData("validAlias123", true)]
    [InlineData("a", true)]
    [InlineData("node", false)] // Reserved
    [InlineData("npm", false)] // Reserved
    [InlineData("npx", false)] // Reserved
    [InlineData("knot", false)] // Reserved
    [InlineData("nodejs", false)] // Reserved
    [InlineData("corepack", false)] // Reserved
    [InlineData("", false)] // Empty
    [InlineData("invalid alias", false)] // Contains space
    [InlineData("invalid@alias", false)] // Invalid char
    [InlineData("invalid.alias", false)] // Invalid char
    public void IsAliasValid_VariousAliases_ReturnsExpectedResult(string alias, bool expected)
    {
        // Act
        var result = _sut.IsAliasValid(alias);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsAliasValid_TooLongAlias_ReturnsFalse()
    {
        // Arrange - 51 characters
        var longAlias = new string('a', 51);

        // Act
        var result = _sut.IsAliasValid(longAlias);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region AliasExists Tests

    [Fact]
    public void AliasExists_ExistingAlias_ReturnsTrue()
    {
        // Arrange
        var installation = new Installation("test-alias", "20.11.0", "/path/test", Use: false);
        _repositoryMock.Setup(x => x.GetByAlias("test-alias")).Returns(installation);

        // Act
        var result = _sut.AliasExists("test-alias");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AliasExists_NonExistingAlias_ReturnsFalse()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetByAlias("non-existing")).Returns((Installation?)null);

        // Act
        var result = _sut.AliasExists("non-existing");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ValidateAliasOrThrow Tests

    [Fact]
    public void ValidateAliasOrThrow_ValidNonExistingAlias_DoesNotThrow()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetByAlias("valid-alias")).Returns((Installation?)null);

        // Act
        var act = () => _sut.ValidateAliasOrThrow("valid-alias");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateAliasOrThrow_InvalidAlias_ThrowsKnotVMHintException()
    {
        // Act
        var act = () => _sut.ValidateAliasOrThrow("invalid alias");

        // Assert
        act.Should().Throw<KnotVMHintException>()
            .Which.ErrorCode.Should().Be(KnotErrorCode.InvalidAlias);
    }

    [Fact]
    public void ValidateAliasOrThrow_ReservedAlias_ThrowsKnotVMHintException()
    {
        // Act
        var act = () => _sut.ValidateAliasOrThrow("node");

        // Assert
        act.Should().Throw<KnotVMHintException>()
            .Which.ErrorCode.Should().Be(KnotErrorCode.InvalidAlias);
    }

    [Fact]
    public void ValidateAliasOrThrow_ExistingAlias_ThrowsKnotVMHintException()
    {
        // Arrange
        var installation = new Installation("existing", "20.11.0", "/path/existing", Use: false);
        _repositoryMock.Setup(x => x.GetByAlias("existing")).Returns(installation);

        // Act
        var act = () => _sut.ValidateAliasOrThrow("existing", checkExists: true);

        // Assert
        act.Should().Throw<KnotVMHintException>()
            .Which.ErrorCode.Should().Be(KnotErrorCode.InvalidAlias);
    }

    [Fact]
    public void ValidateAliasOrThrow_ExistingAliasWithCheckExistsFalse_DoesNotThrow()
    {
        // Arrange
        var installation = new Installation("existing", "20.11.0", "/path/existing", Use: false);
        _repositoryMock.Setup(x => x.GetByAlias("existing")).Returns(installation);

        // Act
        var act = () => _sut.ValidateAliasOrThrow("existing", checkExists: false);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region UseInstallation Tests

    [Fact]
    public void UseInstallation_ValidAlias_CallsVersionManagerAndSync()
    {
        // Arrange
        var installation = new Installation("test-alias", "20.11.0", "/path/test", Use: false);
        _repositoryMock.Setup(x => x.GetByAlias("test-alias")).Returns(installation);

        // Act
        _sut.UseInstallation("test-alias");

        // Assert
        _versionManagerMock.Verify(x => x.UseVersion("test-alias"), Times.Once);
        _syncServiceMock.Verify(x => x.Sync(false), Times.Once);
        _lockManagerMock.Verify(x => x.AcquireLock("state", It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void UseInstallation_NonExistingAlias_ThrowsKnotVMException()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetByAlias("non-existing")).Returns((Installation?)null);

        // Act
        var act = () => _sut.UseInstallation("non-existing");

        // Assert
        act.Should().Throw<KnotVMException>()
            .Which.ErrorCode.Should().Be(KnotErrorCode.InstallationNotFound);
    }

    [Fact]
    public void UseInstallation_EmptyAlias_ThrowsArgumentException()
    {
        // Act
        var act = () => _sut.UseInstallation("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region RemoveInstallation Tests

    [Fact]
    public void RemoveInstallation_NonActiveInstallation_RemovesSuccessfully()
    {
        // Arrange
        var installation = new Installation("test-alias", "20.11.0", "/path/test", Use: false);
        _repositoryMock.Setup(x => x.GetByAlias("test-alias")).Returns(installation);
        _pathServiceMock.Setup(x => x.GetInstallationPath("test-alias")).Returns("/path/test");
        _fileSystemMock.Setup(x => x.DirectoryExists("/path/test")).Returns(true);

        // Act
        _sut.RemoveInstallation("test-alias", force: false);

        // Assert
        _fileSystemMock.Verify(x => x.DeleteDirectoryIfExists("/path/test", true), Times.Once);
        _repositoryMock.Verify(x => x.Remove("test-alias"), Times.Once);
        _lockManagerMock.Verify(x => x.AcquireLock("state", It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void RemoveInstallation_ActiveInstallationWithoutForce_ThrowsKnotVMException()
    {
        // Arrange
        var installation = new Installation("active-alias", "20.11.0", "/path/active", Use: true);
        _repositoryMock.Setup(x => x.GetByAlias("active-alias")).Returns(installation);

        // Act
        var act = () => _sut.RemoveInstallation("active-alias", force: false);

        // Assert
        act.Should().Throw<KnotVMException>()
            .Which.ErrorCode.Should().Be(KnotErrorCode.InvalidAlias);
    }

    [Fact]
    public void RemoveInstallation_ActiveInstallationWithForce_RemovesAndDisables()
    {
        // Arrange
        var installation = new Installation("active-alias", "20.11.0", "/path/active", Use: true);
        _repositoryMock.Setup(x => x.GetByAlias("active-alias")).Returns(installation);
        _pathServiceMock.Setup(x => x.GetInstallationPath("active-alias")).Returns("/path/active");
        _fileSystemMock.Setup(x => x.DirectoryExists("/path/active")).Returns(true);

        // Act
        _sut.RemoveInstallation("active-alias", force: true);

        // Assert
        _versionManagerMock.Verify(x => x.UnuseVersion(), Times.Once);
        _fileSystemMock.Verify(x => x.DeleteDirectoryIfExists("/path/active", true), Times.Once);
        _repositoryMock.Verify(x => x.Remove("active-alias"), Times.Once);
        _syncServiceMock.Verify(x => x.Sync(false), Times.Once);
    }

    [Fact]
    public void RemoveInstallation_NonExistingAlias_ThrowsKnotVMException()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetByAlias("non-existing")).Returns((Installation?)null);

        // Act
        var act = () => _sut.RemoveInstallation("non-existing", force: false);

        // Assert
        act.Should().Throw<KnotVMException>()
            .Which.ErrorCode.Should().Be(KnotErrorCode.InstallationNotFound);
    }

    [Fact]
    public void RemoveInstallation_WithRunningProcesses_ThrowsKnotVMHintException()
    {
        // Arrange
        var installation = new Installation("test-alias", "20.11.0", "/path/test", Use: false);
        _repositoryMock.Setup(x => x.GetByAlias("test-alias")).Returns(installation);
        _pathServiceMock.Setup(x => x.GetInstallationPath("test-alias")).Returns("/path/test");
        _processRunnerMock.Setup(x => x.FindRunningProcesses(It.IsAny<string>()))
            .Returns(new List<int> { 1234, 5678 });

        // Act
        var act = () => _sut.RemoveInstallation("test-alias", force: false);

        // Assert
        var exception = act.Should().Throw<KnotVMHintException>()
            .Which;
        exception.ErrorCode.Should().Be(KnotErrorCode.InstallationFailed);
        exception.Message.Should().Contain("2 processo/i Node.js in esecuzione");
        exception.Message.Should().Contain("1234, 5678");
    }

    #endregion

    #region RenameInstallation Tests

    [Fact]
    public void RenameInstallation_ValidAliases_RenamesSuccessfully()
    {
        // Arrange
        var installation = new Installation("old-alias", "20.11.0", "/path/old", Use: false);
        _repositoryMock.Setup(x => x.GetByAlias("old-alias")).Returns(installation);
        _repositoryMock.Setup(x => x.GetByAlias("new-alias")).Returns((Installation?)null);
        _pathServiceMock.Setup(x => x.GetInstallationPath("old-alias")).Returns("/path/old");
        _pathServiceMock.Setup(x => x.GetInstallationPath("new-alias")).Returns("/path/new");
        _fileSystemMock.Setup(x => x.DirectoryExists("/path/old")).Returns(false); // Don't attempt real move

        // Act
        _sut.RenameInstallation("old-alias", "new-alias");

        // Assert
        _repositoryMock.Verify(x => x.Remove("old-alias"), Times.Once);
        _repositoryMock.Verify(x => x.Add(It.Is<Installation>(i => 
            i.Alias == "new-alias" && 
            i.Version == "20.11.0" &&
            i.Path == "/path/new"
        )), Times.Once);
        _lockManagerMock.Verify(x => x.AcquireLock("state", It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public void RenameInstallation_ActiveInstallation_UpdatesSettingsAndSync()
    {
        // Arrange
        var installation = new Installation("old-alias", "20.11.0", "/path/old", Use: true);
        _repositoryMock.Setup(x => x.GetByAlias("old-alias")).Returns(installation);
        _repositoryMock.Setup(x => x.GetByAlias("new-alias")).Returns((Installation?)null);
        _pathServiceMock.Setup(x => x.GetInstallationPath("old-alias")).Returns("/path/old");
        _pathServiceMock.Setup(x => x.GetInstallationPath("new-alias")).Returns("/path/new");
        _fileSystemMock.Setup(x => x.DirectoryExists("/path/old")).Returns(false); // Don't attempt real move

        // Act
        _sut.RenameInstallation("old-alias", "new-alias");

        // Assert
        _versionManagerMock.Verify(x => x.UpdateSettingsFile("new-alias"), Times.Once);
        _syncServiceMock.Verify(x => x.Sync(false), Times.Once);
    }

    [Fact]
    public void RenameInstallation_ToExistingAlias_ThrowsKnotVMHintException()
    {
        // Arrange
        var installation1 = new Installation("alias1", "20.11.0", "/path/1", Use: false);
        var installation2 = new Installation("alias2", "18.0.0", "/path/2", Use: false);
        _repositoryMock.Setup(x => x.GetByAlias("alias1")).Returns(installation1);
        _repositoryMock.Setup(x => x.GetByAlias("alias2")).Returns(installation2);

        // Act
        var act = () => _sut.RenameInstallation("alias1", "alias2");

        // Assert
        act.Should().Throw<KnotVMHintException>()
            .Which.ErrorCode.Should().Be(KnotErrorCode.InvalidAlias);
    }

    [Fact]
    public void RenameInstallation_NonExistingSource_ThrowsKnotVMException()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetByAlias("non-existing")).Returns((Installation?)null);
        _repositoryMock.Setup(x => x.GetByAlias("new-alias")).Returns((Installation?)null);

        // Act
        var act = () => _sut.RenameInstallation("non-existing", "new-alias");

        // Assert
        act.Should().Throw<KnotVMException>()
            .Which.ErrorCode.Should().Be(KnotErrorCode.InstallationNotFound);
    }

    [Fact]
    public void RenameInstallation_ToInvalidAlias_ThrowsKnotVMHintException()
    {
        // Arrange
        var installation = new Installation("valid-alias", "20.11.0", "/path/valid", Use: false);
        _repositoryMock.Setup(x => x.GetByAlias("valid-alias")).Returns(installation);

        // Act
        var act = () => _sut.RenameInstallation("valid-alias", "node"); // Reserved alias

        // Assert
        act.Should().Throw<KnotVMHintException>()
            .Which.ErrorCode.Should().Be(KnotErrorCode.InvalidAlias);
    }

    #endregion
}
