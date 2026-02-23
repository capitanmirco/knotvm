using FluentAssertions;
using KnotVM.Core.Common;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class PathServiceTests
{
    private readonly Mock<IPlatformService> _platformMock;
    private readonly Configuration _config;
    private readonly PathService _sut;

    public PathServiceTests()
    {
        _platformMock = new Mock<IPlatformService>();
        _config = new Configuration(
            AppDataPath: "/test/knot",
            VersionsPath: "/test/knot/versions",
            BinPath: "/test/knot/bin",
            CachePath: "/test/knot/cache",
            SettingsFile: "/test/knot/settings.txt",
            TemplatesPath: "/test/knot/templates",
            LocksPath: "/test/knot/locks"
        );

        _sut = new PathService(_platformMock.Object, _config);
    }

    [Fact]
    public void GetBasePath_ReturnsConfigAppDataPath()
    {
        // Act
        var result = _sut.GetBasePath();

        // Assert
        result.Should().Be("/test/knot");
    }

    [Fact]
    public void GetVersionsPath_ReturnsConfigVersionsPath()
    {
        // Act
        var result = _sut.GetVersionsPath();

        // Assert
        result.Should().Be("/test/knot/versions");
    }

    [Fact]
    public void GetBinPath_ReturnsConfigBinPath()
    {
        // Act
        var result = _sut.GetBinPath();

        // Assert
        result.Should().Be("/test/knot/bin");
    }

    [Fact]
    public void GetCachePath_ReturnsConfigCachePath()
    {
        // Act
        var result = _sut.GetCachePath();

        // Assert
        result.Should().Be("/test/knot/cache");
    }

    [Fact]
    public void GetTemplatesPath_ReturnsConfigTemplatesPath()
    {
        // Act
        var result = _sut.GetTemplatesPath();

        // Assert
        result.Should().Be("/test/knot/templates");
    }

    [Fact]
    public void GetLocksPath_ReturnsConfigLocksPath()
    {
        // Act
        var result = _sut.GetLocksPath();

        // Assert
        result.Should().Be("/test/knot/locks");
    }

    [Fact]
    public void GetSettingsFilePath_ReturnsConfigSettingsFile()
    {
        // Act
        var result = _sut.GetSettingsFilePath();

        // Assert
        result.Should().Be("/test/knot/settings.txt");
    }

    [Fact]
    public void GetInstallationPath_WithAlias_ReturnsCorrectPath()
    {
        // Act
        var result = _sut.GetInstallationPath("test-node");

        // Assert
        var expected = Path.Combine("/test/knot/versions", "test-node");
        result.Should().Be(expected);
    }

    [Fact]
    public void GetNodeExecutablePath_OnWindows_ReturnsNodeExe()
    {
        // Arrange
        _platformMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Windows);
        _platformMock.Setup(x => x.GetExecutableExtension()).Returns(".exe");

        // Act
        var result = _sut.GetNodeExecutablePath("/test/installation");

        // Assert
        var expected = Path.Combine("/test/installation", "node.exe");
        result.Should().Be(expected);
    }

    [Fact]
    public void GetNodeExecutablePath_OnLinux_ReturnsBinNode()
    {
        // Arrange
        _platformMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Linux);
        _platformMock.Setup(x => x.GetExecutableExtension()).Returns("");

        // Act
        var result = _sut.GetNodeExecutablePath("/test/installation");

        // Assert
        var expected = Path.Combine("/test/installation", "bin", "node");
        result.Should().Be(expected);
    }

    [Fact]
    public void CombinePaths_CombinesMultiplePaths()
    {
        // Act
        var result = _sut.CombinePaths("test", "path", "to", "file.txt");

        // Assert
        result.Should().Contain("test");
        result.Should().Contain("file.txt");
    }
}
