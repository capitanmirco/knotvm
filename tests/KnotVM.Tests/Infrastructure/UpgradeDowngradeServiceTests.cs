using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class UpgradeDowngradeServiceTests
{
    private readonly Mock<IRemoteVersionService> _remoteServiceMock;
    private readonly Mock<IInstallationService> _installServiceMock;
    private readonly Mock<IInstallationManager> _installManagerMock;
    private readonly UpgradeDowngradeService _sut;

    public UpgradeDowngradeServiceTests()
    {
        _remoteServiceMock = new Mock<IRemoteVersionService>();
        _installServiceMock = new Mock<IInstallationService>();
        _installManagerMock = new Mock<IInstallationManager>();

        _sut = new UpgradeDowngradeService(
            _remoteServiceMock.Object,
            _installServiceMock.Object,
            _installManagerMock.Object);
    }

    #region IsLtsAlias Tests

    [Theory]
    [InlineData("lts", true)]
    [InlineData("LTS", true)]
    [InlineData("Lts", true)]
    [InlineData("production", false)]
    [InlineData("22.14.0", false)]
    [InlineData("my-lts", false)]
    [InlineData("lts-custom", false)]
    [InlineData("", false)]
    public void IsLtsAlias_VariousInputs_ReturnsExpected(string alias, bool expected)
    {
        _sut.IsLtsAlias(alias).Should().Be(expected);
    }

    [Fact]
    public void IsLtsAlias_WithWhitespace_ReturnsFalse()
    {
        _sut.IsLtsAlias("   ").Should().BeFalse();
    }

    #endregion

    #region GetLatestLtsVersionAsync Tests

    [Fact]
    public async Task GetLatestLtsVersionAsync_DelegatesToRemoteService()
    {
        var expected = new RemoteVersion("22.14.0", "Jod", "2025-01-01", []);
        _remoteServiceMock
            .Setup(x => x.GetLatestLtsVersionAsync(false, default))
            .ReturnsAsync(expected);

        var result = await _sut.GetLatestLtsVersionAsync();

        result.Should().Be(expected);
        _remoteServiceMock.Verify(x => x.GetLatestLtsVersionAsync(false, default), Times.Once);
    }

    [Fact]
    public async Task GetLatestLtsVersionAsync_WhenNoLts_ReturnsNull()
    {
        _remoteServiceMock
            .Setup(x => x.GetLatestLtsVersionAsync(false, default))
            .ReturnsAsync((RemoteVersion?)null);

        var result = await _sut.GetLatestLtsVersionAsync();

        result.Should().BeNull();
    }

    #endregion

    #region GetRecentLtsVersionsAsync Tests

    [Fact]
    public async Task GetRecentLtsVersionsAsync_ExcludesCurrentVersion()
    {
        var versions = new RemoteVersion[]
        {
            new("22.14.0", "Jod", "2025-01-01", []),
            new("22.13.0", "Jod", "2024-12-01", []),
            new("20.18.0", "Iron", "2024-11-01", []),
        };
        _remoteServiceMock
            .Setup(x => x.GetLtsVersionsAsync(false, default))
            .ReturnsAsync(versions);

        var result = await _sut.GetRecentLtsVersionsAsync("22.14.0", count: 15);

        result.Should().HaveCount(2);
        result.Should().NotContain(v => v.Version == "22.14.0");
    }

    [Fact]
    public async Task GetRecentLtsVersionsAsync_ExcludesCurrentVersionWithLeadingV()
    {
        var versions = new RemoteVersion[]
        {
            new("22.14.0", "Jod", "2025-01-01", []),
            new("20.18.0", "Iron", "2024-11-01", []),
        };
        _remoteServiceMock
            .Setup(x => x.GetLtsVersionsAsync(false, default))
            .ReturnsAsync(versions);

        var result = await _sut.GetRecentLtsVersionsAsync("v22.14.0");

        result.Should().HaveCount(1);
        result[0].Version.Should().Be("20.18.0");
    }

    [Fact]
    public async Task GetRecentLtsVersionsAsync_RespectsCountLimit()
    {
        var versions = Enumerable.Range(0, 20)
            .Select(i => new RemoteVersion($"20.{i}.0", "Iron", "2024-01-01", []))
            .ToArray();
        _remoteServiceMock
            .Setup(x => x.GetLtsVersionsAsync(false, default))
            .ReturnsAsync(versions);

        var result = await _sut.GetRecentLtsVersionsAsync("99.0.0", count: 5);

        result.Should().HaveCount(5);
    }

    #endregion

    #region GetRemoteVersionsAsync Tests

    [Fact]
    public async Task GetRemoteVersionsAsync_ReturnsLimitedResults()
    {
        var versions = Enumerable.Range(0, 50)
            .Select(i => new RemoteVersion($"20.{i}.0", null, "2024-01-01", []))
            .ToArray();
        _remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(false, default))
            .ReturnsAsync(versions);

        var result = await _sut.GetRemoteVersionsAsync(10);

        result.Should().HaveCount(10);
    }

    #endregion

    #region ReplaceVersionAsync Tests

    [Fact]
    public async Task ReplaceVersionAsync_CallsRemoveBeforeInstall()
    {
        var callOrder = new List<string>();

        _installManagerMock
            .Setup(x => x.RemoveInstallation("lts", true))
            .Callback(() => callOrder.Add("remove"));

        _installServiceMock
            .Setup(x => x.InstallAsync("22.14.0", "lts", false, It.IsAny<IProgress<DownloadProgress>?>(), default))
            .Callback(() => callOrder.Add("install"))
            .ReturnsAsync(new InstallationPrepareResult(true, "lts", "22.14.0", "/path/lts"));

        await _sut.ReplaceVersionAsync("lts", wasActive: false, "22.14.0");

        callOrder.Should().Equal("remove", "install");
    }

    [Fact]
    public async Task ReplaceVersionAsync_WhenWasActive_ReactivatesAfterInstall()
    {
        var callOrder = new List<string>();

        _installManagerMock
            .Setup(x => x.RemoveInstallation("lts", true))
            .Callback(() => callOrder.Add("remove"));

        _installServiceMock
            .Setup(x => x.InstallAsync("22.14.0", "lts", false, It.IsAny<IProgress<DownloadProgress>?>(), default))
            .Callback(() => callOrder.Add("install"))
            .ReturnsAsync(new InstallationPrepareResult(true, "lts", "22.14.0", "/path/lts"));

        _installManagerMock
            .Setup(x => x.UseInstallation("lts"))
            .Callback(() => callOrder.Add("use"));

        await _sut.ReplaceVersionAsync("lts", wasActive: true, "22.14.0");

        callOrder.Should().Equal("remove", "install", "use");
        _installManagerMock.Verify(x => x.UseInstallation("lts"), Times.Once);
    }

    [Fact]
    public async Task ReplaceVersionAsync_WhenNotActive_DoesNotReactivate()
    {
        _installManagerMock.Setup(x => x.RemoveInstallation("production", true));

        _installServiceMock
            .Setup(x => x.InstallAsync("20.11.0", "production", false, It.IsAny<IProgress<DownloadProgress>?>(), default))
            .ReturnsAsync(new InstallationPrepareResult(true, "production", "20.11.0", "/path/production"));

        await _sut.ReplaceVersionAsync("production", wasActive: false, "20.11.0");

        _installManagerMock.Verify(x => x.UseInstallation(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReplaceVersionAsync_WhenInstallFails_ReturnsFailureResult()
    {
        _installManagerMock.Setup(x => x.RemoveInstallation("lts", true));

        var failResult = new InstallationPrepareResult(
            false, "lts", "22.14.0", string.Empty,
            ErrorMessage: "Artifact non disponibile", ErrorCode: "ArtifactNotAvailable");

        _installServiceMock
            .Setup(x => x.InstallAsync("22.14.0", "lts", false, It.IsAny<IProgress<DownloadProgress>?>(), default))
            .ReturnsAsync(failResult);

        var result = await _sut.ReplaceVersionAsync("lts", wasActive: true, "22.14.0");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Artifact non disponibile");
        _installManagerMock.Verify(x => x.UseInstallation(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReplaceVersionAsync_WhenWasActive_AndUseInstallationThrowsKnotVMException_Propagates()
    {
        _installManagerMock.Setup(x => x.RemoveInstallation("lts", true));
        _installServiceMock
            .Setup(x => x.InstallAsync("22.14.0", "lts", false, It.IsAny<IProgress<DownloadProgress>?>(), default))
            .ReturnsAsync(new InstallationPrepareResult(true, "lts", "22.14.0", "/path/lts"));
        _installManagerMock
            .Setup(x => x.UseInstallation("lts"))
            .Throws(new KnotVMException(KnotErrorCode.SyncFailed, "Sync fallita"));

        var act = async () => await _sut.ReplaceVersionAsync("lts", wasActive: true, "22.14.0");

        await act.Should().ThrowAsync<KnotVMException>()
            .Where(e => e.ErrorCode == KnotErrorCode.SyncFailed);
    }

    [Fact]
    public async Task ReplaceVersionAsync_WhenWasActive_AndUseInstallationThrowsGenericException_WrapsAsInstallationFailed()
    {
        _installManagerMock.Setup(x => x.RemoveInstallation("lts", true));
        _installServiceMock
            .Setup(x => x.InstallAsync("22.14.0", "lts", false, It.IsAny<IProgress<DownloadProgress>?>(), default))
            .ReturnsAsync(new InstallationPrepareResult(true, "lts", "22.14.0", "/path/lts"));
        _installManagerMock
            .Setup(x => x.UseInstallation("lts"))
            .Throws(new InvalidOperationException("Operazione non valida"));

        var act = async () => await _sut.ReplaceVersionAsync("lts", wasActive: true, "22.14.0");

        await act.Should().ThrowAsync<KnotVMException>()
            .Where(e => e.ErrorCode == KnotErrorCode.InstallationFailed);
    }

    [Fact]
    public async Task GetRecentLtsVersionsAsync_WhenEmptyLtsList_ReturnsEmpty()
    {
        _remoteServiceMock
            .Setup(x => x.GetLtsVersionsAsync(false, default))
            .ReturnsAsync([]);

        var result = await _sut.GetRecentLtsVersionsAsync("22.14.0");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRemoteVersionsAsync_WhenEmptyList_ReturnsEmpty()
    {
        _remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(false, default))
            .ReturnsAsync([]);

        var result = await _sut.GetRemoteVersionsAsync(10);

        result.Should().BeEmpty();
    }

    #endregion
}
