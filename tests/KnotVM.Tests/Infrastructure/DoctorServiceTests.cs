using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class DoctorServiceTests
{
    private readonly Mock<IPathService>           _pathServiceMock;
    private readonly Mock<IFileSystemService>     _fileSystemMock;
    private readonly Mock<IVersionManager>        _versionManagerMock;
    private readonly Mock<IInstallationsRepository> _repositoryMock;
    private readonly Mock<ISyncService>           _syncServiceMock;
    private readonly Mock<ILockManager>           _lockManagerMock;
    private readonly Mock<IProcessRunner>         _processRunnerMock;
    private readonly Mock<IRemoteVersionService>  _remoteVersionServiceMock;
    private readonly Mock<IPlatformService>       _platformServiceMock;
    private readonly DoctorService                _sut;

    public DoctorServiceTests()
    {
        _pathServiceMock          = new Mock<IPathService>();
        _fileSystemMock           = new Mock<IFileSystemService>();
        _versionManagerMock       = new Mock<IVersionManager>();
        _repositoryMock           = new Mock<IInstallationsRepository>();
        _syncServiceMock          = new Mock<ISyncService>();
        _lockManagerMock          = new Mock<ILockManager>();
        _processRunnerMock        = new Mock<IProcessRunner>();
        _remoteVersionServiceMock = new Mock<IRemoteVersionService>();
        _platformServiceMock      = new Mock<IPlatformService>();

        // Default happy-path setup
        _pathServiceMock.Setup(x => x.GetBasePath()).Returns("/knot-home");
        _pathServiceMock.Setup(x => x.GetBinPath()).Returns("/knot-home/bin");
        _pathServiceMock.Setup(x => x.GetTemplatesPath()).Returns("/knot-home/templates");
        _pathServiceMock.Setup(x => x.GetLocksPath()).Returns("/knot-home/locks");

        _fileSystemMock.Setup(x => x.DirectoryExists("/knot-home")).Returns(true);
        _fileSystemMock.Setup(x => x.CanWrite("/knot-home")).Returns(true);
        _fileSystemMock.Setup(x => x.DirectoryExists("/knot-home/templates")).Returns(true);
        _fileSystemMock.Setup(x => x.GetFiles("/knot-home/templates", "*.template"))
            .Returns(new[] { "/knot-home/templates/proxy.sh.template" });
        _fileSystemMock.Setup(x => x.DirectoryExists("/knot-home/locks")).Returns(false);

        _versionManagerMock.Setup(x => x.GetActiveAlias()).Returns("lts");
        _repositoryMock.Setup(x => x.GetByAlias("lts"))
            .Returns(new Installation("lts", "22.14.0", "/knot-home/versions/lts", Use: true));

        _syncServiceMock.Setup(x => x.IsSyncNeeded()).Returns(false);

        _remoteVersionServiceMock
            .Setup(x => x.GetLatestLtsVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RemoteVersion("22.14.0", "Jod", "2025-01-15", Array.Empty<string>()));

        _platformServiceMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Linux);

        _processRunnerMock
            .Setup(x => x.RunAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<int>()))
            .ReturnsAsync(new ProcessResult(1, string.Empty, string.Empty));

        _sut = new DoctorService(
            _pathServiceMock.Object,
            _fileSystemMock.Object,
            _versionManagerMock.Object,
            _repositoryMock.Object,
            _syncServiceMock.Object,
            _lockManagerMock.Object,
            _processRunnerMock.Object,
            _remoteVersionServiceMock.Object,
            _platformServiceMock.Object);
    }

    // ── KNOT_HOME check ─────────────────────────────────────────────────────

    [Fact]
    public async Task RunAllChecksAsync_KnotHomeExists_ReturnsPassedCheck()
    {
        var checks = await _sut.RunAllChecksAsync();

        var knotHomeCheck = checks.First(c => c.Name == "KNOT_HOME");
        knotHomeCheck.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task RunAllChecksAsync_KnotHomeMissing_ReturnsFailedFixableCheck()
    {
        _fileSystemMock.Setup(x => x.DirectoryExists("/knot-home")).Returns(false);

        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "KNOT_HOME");
        check.Passed.Should().BeFalse();
        check.IsWarning.Should().BeFalse();
        check.CanAutoFix.Should().BeTrue();
    }

    [Fact]
    public async Task RunAllChecksAsync_KnotHomeNotWritable_ReturnsFailedNonFixableCheck()
    {
        _fileSystemMock.Setup(x => x.CanWrite("/knot-home")).Returns(false);

        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "KNOT_HOME");
        check.Passed.Should().BeFalse();
        check.CanAutoFix.Should().BeFalse();
    }

    // ── Active version check ─────────────────────────────────────────────────

    [Fact]
    public async Task RunAllChecksAsync_NoActiveAlias_ReturnsWarningCheck()
    {
        _versionManagerMock.Setup(x => x.GetActiveAlias()).Returns((string?)null);

        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "Versione attiva");
        check.Passed.Should().BeFalse();
        check.IsWarning.Should().BeTrue();
    }

    [Fact]
    public async Task RunAllChecksAsync_AliasWithMissingInstallation_ReturnsFailedCheck()
    {
        _versionManagerMock.Setup(x => x.GetActiveAlias()).Returns("ghost");
        _repositoryMock.Setup(x => x.GetByAlias("ghost")).Returns((Installation?)null);

        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "Versione attiva");
        check.Passed.Should().BeFalse();
        check.IsWarning.Should().BeFalse();
    }

    [Fact]
    public async Task RunAllChecksAsync_ValidActiveInstallation_ReturnsPassedCheck()
    {
        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "Versione attiva");
        check.Passed.Should().BeTrue();
        check.Detail.Should().Contain("lts");
        check.Detail.Should().Contain("22.14.0");
    }

    // ── Proxy sync check ─────────────────────────────────────────────────────

    [Fact]
    public async Task RunAllChecksAsync_SyncNeeded_ReturnsFailedFixableCheck()
    {
        _syncServiceMock.Setup(x => x.IsSyncNeeded()).Returns(true);

        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "Proxy sincronizzati");
        check.Passed.Should().BeFalse();
        check.CanAutoFix.Should().BeTrue();
    }

    [Fact]
    public async Task RunAllChecksAsync_ProxiesInSync_ReturnsPassedCheck()
    {
        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "Proxy sincronizzati");
        check.Passed.Should().BeTrue();
    }

    // ── Templates check ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunAllChecksAsync_TemplatesDirMissing_ReturnsFailedCheck()
    {
        _fileSystemMock.Setup(x => x.DirectoryExists("/knot-home/templates")).Returns(false);

        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "Template proxy");
        check.Passed.Should().BeFalse();
        check.CanAutoFix.Should().BeFalse();
    }

    [Fact]
    public async Task RunAllChecksAsync_NoTemplateFiles_ReturnsFailedCheck()
    {
        _fileSystemMock.Setup(x => x.GetFiles("/knot-home/templates", "*.template"))
            .Returns(Array.Empty<string>());

        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "Template proxy");
        check.Passed.Should().BeFalse();
    }

    // ── Connectivity check ───────────────────────────────────────────────────

    [Fact]
    public async Task RunAllChecksAsync_ConnectivityFails_ReturnsWarningCheck()
    {
        _remoteVersionServiceMock
            .Setup(x => x.GetLatestLtsVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "Connettività nodejs.org");
        check.Passed.Should().BeFalse();
        check.IsWarning.Should().BeTrue();
    }

    [Fact]
    public async Task RunAllChecksAsync_ConnectivityOk_ReturnsPassedCheck()
    {
        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == "Connettività nodejs.org");
        check.Passed.Should().BeTrue();
    }

    // ── .NET runtime check ───────────────────────────────────────────────────

    [Fact]
    public async Task RunAllChecksAsync_NetRuntimeCheck_PassesOnCurrentRuntime()
    {
        // The test itself runs on .NET 8+, so this should always pass
        var checks = await _sut.RunAllChecksAsync();

        var check = checks.First(c => c.Name == ".NET runtime");
        check.Passed.Should().BeTrue();
    }

    // ── TryAutoFixAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task TryAutoFixAsync_ProxySyncCheck_CallsSyncService()
    {
        var failedProxyCheck = new DoctorCheck(
            Name:       "Proxy sincronizzati",
            Passed:     false,
            IsWarning:  false,
            Detail:     "Desincronizzati",
            Suggestion: null,
            CanAutoFix: true);

        var result = await _sut.TryAutoFixAsync(failedProxyCheck);

        result.Should().BeTrue();
        _syncServiceMock.Verify(x => x.Sync(false), Times.Once);
    }

    [Fact]
    public async Task TryAutoFixAsync_KnotHomeCheck_CallsEnsureDirectory()
    {
        var failedKnotHomeCheck = new DoctorCheck(
            Name:       "KNOT_HOME",
            Passed:     false,
            IsWarning:  false,
            Detail:     "Mancante",
            Suggestion: null,
            CanAutoFix: true);

        var result = await _sut.TryAutoFixAsync(failedKnotHomeCheck);

        result.Should().BeTrue();
        _fileSystemMock.Verify(x => x.EnsureDirectoryExists("/knot-home"), Times.Once);
    }

    [Fact]
    public async Task TryAutoFixAsync_StaleLocksCheck_CallsCleanupStaleLocks()
    {
        var staleLocksCheck = new DoctorCheck(
            Name:       "Lock file orfani",
            Passed:     false,
            IsWarning:  true,
            Detail:     "2 orfani",
            Suggestion: null,
            CanAutoFix: true);

        var result = await _sut.TryAutoFixAsync(staleLocksCheck);

        result.Should().BeTrue();
        _lockManagerMock.Verify(x => x.CleanupStaleLocks(It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task TryAutoFixAsync_UnknownCheck_ReturnsFalse()
    {
        var unknownCheck = new DoctorCheck(
            Name:       "CheckSconosciuto",
            Passed:     false,
            IsWarning:  false,
            Detail:     null,
            Suggestion: null,
            CanAutoFix: false);

        var result = await _sut.TryAutoFixAsync(unknownCheck);

        result.Should().BeFalse();
    }

    // ── Total checks count ───────────────────────────────────────────────────

    [Fact]
    public async Task RunAllChecksAsync_ReturnsEightChecks()
    {
        var checks = await _sut.RunAllChecksAsync();

        checks.Should().HaveCount(8);
    }
}
