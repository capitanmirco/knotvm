using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class InstallationServiceArtifactFallbackTests
{
    [Fact]
    public async Task InstallAsync_MacOsArm64WithoutNativeArtifact_FallsBackToMacOsX64()
    {
        // Arrange
        var remoteVersion = new RemoteVersion(
            Version: "10.16.0",
            Lts: "Dubnium",
            Date: "2019-05-28",
            Files: ["osx-x64-tar", "win-x64-zip"]);

        var remoteVersionsMock = new Mock<IRemoteVersionService>();
        var artifactResolverMock = new Mock<INodeArtifactResolver>();
        var downloadServiceMock = new Mock<IDownloadService>();
        var archiveExtractorMock = new Mock<IArchiveExtractor>();
        var pathServiceMock = new Mock<IPathService>();
        var fileSystemMock = new Mock<IFileSystemService>();
        var processRunnerMock = new Mock<IProcessRunner>();
        var platformMock = new Mock<IPlatformService>();
        var installationsRepoMock = new Mock<IInstallationsRepository>();
        var lockManagerMock = new Mock<ILockManager>();
        var lockHandleMock = new Mock<IDisposable>();

        lockManagerMock.Setup(x => x.AcquireLock(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(lockHandleMock.Object);

        remoteVersionsMock.Setup(x => x.ResolveVersionAsync("10.16.0", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(remoteVersion);
        remoteVersionsMock.Setup(x => x.GetAvailableVersionsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([remoteVersion]);

        platformMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.MacOS);
        platformMock.Setup(x => x.GetCurrentArch()).Returns(HostArch.Arm64);
        platformMock.Setup(x => x.IsOsSupported()).Returns(true);
        platformMock.Setup(x => x.IsArchSupported()).Returns(true);

        var basePath = "/tmp";
        var versionsPath = "/tmp/knotvm-tests/versions";
        var cachePath = "/tmp/knotvm-tests/cache";
        var installPath = "/tmp/knotvm-tests/versions/pippo";

        pathServiceMock.Setup(x => x.GetBasePath()).Returns(basePath);
        pathServiceMock.Setup(x => x.GetVersionsPath()).Returns(versionsPath);
        pathServiceMock.Setup(x => x.GetCachePath()).Returns(cachePath);
        pathServiceMock.Setup(x => x.GetInstallationPath("pippo")).Returns(installPath);

        fileSystemMock.Setup(x => x.DirectoryExists(installPath)).Returns(false);
        fileSystemMock.Setup(x => x.CanWrite(basePath)).Returns(true);
        fileSystemMock.Setup(x => x.EnsureDirectoryExists(It.IsAny<string>()));

        artifactResolverMock.Setup(x => x.IsArtifactAvailable(remoteVersion, HostOs.MacOS, HostArch.Arm64))
            .Returns(false);
        artifactResolverMock.Setup(x => x.IsArtifactAvailable(remoteVersion, HostOs.MacOS, HostArch.X64))
            .Returns(true);
        artifactResolverMock.Setup(x => x.GetArtifactDownloadUrl("10.16.0", HostOs.MacOS, HostArch.X64))
            .Returns("https://nodejs.org/dist/v10.16.0/node-v10.16.0-darwin-x64.tar.gz");
        artifactResolverMock.Setup(x => x.GetChecksumFileUrl("10.16.0"))
            .Returns("https://nodejs.org/dist/v10.16.0/SHASUMS256.txt");
        artifactResolverMock.Setup(x => x.GetArtifactFileName("10.16.0", HostOs.MacOS, HostArch.X64))
            .Returns("node-v10.16.0-darwin-x64.tar.gz");

        // Stop flow after artifact resolution and checksum lookup.
        downloadServiceMock.Setup(x => x.FetchChecksumAsync(
                "https://nodejs.org/dist/v10.16.0/SHASUMS256.txt",
                "node-v10.16.0-darwin-x64.tar.gz",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var sut = new InstallationService(
            remoteVersionsMock.Object,
            artifactResolverMock.Object,
            downloadServiceMock.Object,
            archiveExtractorMock.Object,
            pathServiceMock.Object,
            fileSystemMock.Object,
            processRunnerMock.Object,
            platformMock.Object,
            installationsRepoMock.Object,
            lockManagerMock.Object);

        // Act
        var result = await sut.InstallAsync("10.16.0", alias: "pippo");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KnotErrorCode.DownloadFailed.ToString());

        artifactResolverMock.Verify(x => x.GetArtifactFileName("10.16.0", HostOs.MacOS, HostArch.X64), Times.Once);
        artifactResolverMock.Verify(x => x.GetArtifactDownloadUrl("10.16.0", HostOs.MacOS, HostArch.X64), Times.Once);
        downloadServiceMock.Verify(x => x.FetchChecksumAsync(
            It.IsAny<string>(),
            "node-v10.16.0-darwin-x64.tar.gz",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAsync_LinuxArm64WithoutNativeArtifact_ShouldNotFallbackToX64()
    {
        // Arrange
        var remoteVersion = new RemoteVersion(
            Version: "18.19.0",
            Lts: "Hydrogen",
            Date: "2023-11-01",
            Files: ["linux-x64", "win-x64-zip"]);

        var remoteVersionsMock = new Mock<IRemoteVersionService>();
        var artifactResolverMock = new Mock<INodeArtifactResolver>();
        var downloadServiceMock = new Mock<IDownloadService>();
        var archiveExtractorMock = new Mock<IArchiveExtractor>();
        var pathServiceMock = new Mock<IPathService>();
        var fileSystemMock = new Mock<IFileSystemService>();
        var processRunnerMock = new Mock<IProcessRunner>();
        var platformMock = new Mock<IPlatformService>();
        var installationsRepoMock = new Mock<IInstallationsRepository>();
        var lockManagerMock = new Mock<ILockManager>();
        var lockHandleMock = new Mock<IDisposable>();

        lockManagerMock.Setup(x => x.AcquireLock(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(lockHandleMock.Object);

        remoteVersionsMock.Setup(x => x.ResolveVersionAsync("18.19.0", It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(remoteVersion);

        platformMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Linux);
        platformMock.Setup(x => x.GetCurrentArch()).Returns(HostArch.Arm64);

        pathServiceMock.Setup(x => x.GetInstallationPath("legacy")).Returns("/tmp/knotvm-tests/versions/legacy");
        fileSystemMock.Setup(x => x.DirectoryExists("/tmp/knotvm-tests/versions/legacy")).Returns(false);

        artifactResolverMock.Setup(x => x.IsArtifactAvailable(remoteVersion, HostOs.Linux, HostArch.Arm64))
            .Returns(false);

        var sut = new InstallationService(
            remoteVersionsMock.Object,
            artifactResolverMock.Object,
            downloadServiceMock.Object,
            archiveExtractorMock.Object,
            pathServiceMock.Object,
            fileSystemMock.Object,
            processRunnerMock.Object,
            platformMock.Object,
            installationsRepoMock.Object,
            lockManagerMock.Object);

        // Act
        var result = await sut.InstallAsync("18.19.0", alias: "legacy");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KnotErrorCode.ArtifactNotAvailable.ToString());

        artifactResolverMock.Verify(x => x.IsArtifactAvailable(remoteVersion, HostOs.Linux, HostArch.X64), Times.Never);
        downloadServiceMock.Verify(x => x.FetchChecksumAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
