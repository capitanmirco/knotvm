using FluentAssertions;
using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Repositories;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class LocalInstallationsRepositoryTests : IDisposable
{
    private readonly string _tempRoot;

    public LocalInstallationsRepositoryTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"knotvm-repo-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void GetByAlias_ShouldResolveCaseInsensitiveAlias_WithoutScanningVersions()
    {
        var config = new Configuration(
            AppDataPath: _tempRoot,
            VersionsPath: Path.Combine(_tempRoot, "versions"),
            BinPath: Path.Combine(_tempRoot, "bin"),
            CachePath: Path.Combine(_tempRoot, "cache"),
            SettingsFile: Path.Combine(_tempRoot, "settings.txt"),
            TemplatesPath: Path.Combine(_tempRoot, "templates"),
            LocksPath: Path.Combine(_tempRoot, "locks"));
        config.EnsureDirectoriesExist();

        var aliasDirectory = Path.Combine(config.VersionsPath, "MyAlias");
        Directory.CreateDirectory(aliasDirectory);
        var otherAliasDirectory = Path.Combine(config.VersionsPath, "OtherAlias");
        Directory.CreateDirectory(otherAliasDirectory);

        var nodePath = Path.Combine(aliasDirectory, "node.exe");
        File.WriteAllText(nodePath, "node");
        File.WriteAllText(Path.Combine(otherAliasDirectory, "node.exe"), "node");
        var unixNodePath = Path.Combine(aliasDirectory, "bin", "node");
        Directory.CreateDirectory(Path.GetDirectoryName(unixNodePath)!);
        File.WriteAllText(unixNodePath, "node");
        var otherUnixNodePath = Path.Combine(otherAliasDirectory, "bin", "node");
        Directory.CreateDirectory(Path.GetDirectoryName(otherUnixNodePath)!);
        File.WriteAllText(otherUnixNodePath, "node");
        File.WriteAllText(config.SettingsFile, "MyAlias");

        var platformServiceMock = new Mock<IPlatformService>();
        platformServiceMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Windows);

        var processRunnerMock = new Mock<IProcessRunner>();
        string? resolvedNodePath = null;
        processRunnerMock
            .Setup(x => x.GetNodeVersion(It.IsAny<string>()))
            .Callback<string>(path => resolvedNodePath = path)
            .Returns("20.11.1");

        var fileSystemService = new FileSystemService(platformServiceMock.Object);

        var repository = new LocalInstallationsRepository(config, platformServiceMock.Object, processRunnerMock.Object, fileSystemService);

        var installation = repository.GetByAlias("myalias");

        installation.Should().NotBeNull();
        installation!.Alias.Should().BeEquivalentTo("MyAlias");
        installation.Version.Should().Be("20.11.1");
        installation.Use.Should().BeTrue();
        resolvedNodePath.Should().NotBeNull();
        Path.GetFileName(resolvedNodePath!).Should().BeOneOf("node.exe", "node");
        processRunnerMock.Verify(x => x.GetNodeVersion(It.IsAny<string>()), Times.Once);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
