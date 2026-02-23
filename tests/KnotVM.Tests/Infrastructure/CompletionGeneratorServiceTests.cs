using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class CompletionGeneratorServiceTests
{
    private readonly Mock<IInstallationsRepository> _repositoryMock;
    private readonly Mock<IPathService> _pathServiceMock;
    private readonly Mock<IFileSystemService> _fileSystemMock;
    private readonly CompletionGeneratorService _sut;

    private const string CachePath = "/test/cache";

    public CompletionGeneratorServiceTests()
    {
        _repositoryMock = new Mock<IInstallationsRepository>();
        _pathServiceMock = new Mock<IPathService>();
        _fileSystemMock = new Mock<IFileSystemService>();

        _pathServiceMock.Setup(x => x.GetCachePath()).Returns(CachePath);
        _fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);
        _repositoryMock.Setup(x => x.GetAll()).Returns([]);

        _sut = new CompletionGeneratorService(
            _repositoryMock.Object,
            _pathServiceMock.Object,
            _fileSystemMock.Object);
    }

    [Theory]
    [InlineData(ShellType.Bash)]
    [InlineData(ShellType.Zsh)]
    [InlineData(ShellType.PowerShell)]
    [InlineData(ShellType.Fish)]
    public async Task GenerateCompletionScriptAsync_AllShells_GeneratesNonEmptyScript(ShellType shellType)
    {
        // Act
        var script = await _sut.GenerateCompletionScriptAsync(shellType);

        // Assert
        script.Should().NotBeNullOrWhiteSpace();
        script.Should().Contain("knot");
    }

    [Fact]
    public async Task GenerateCompletionScriptAsync_BashShell_ContainsCompgenAndComplete()
    {
        // Act
        var script = await _sut.GenerateCompletionScriptAsync(ShellType.Bash);

        // Assert
        script.Should().Contain("compgen");
        script.Should().Contain("complete -F _knot_completion knot");
        script.Should().Contain("_knot_completion");
    }

    [Fact]
    public async Task GenerateCompletionScriptAsync_ZshShell_ContainsCompdef()
    {
        // Act
        var script = await _sut.GenerateCompletionScriptAsync(ShellType.Zsh);

        // Assert
        script.Should().Contain("#compdef knot");
        script.Should().Contain("_knot");
    }

    [Fact]
    public async Task GenerateCompletionScriptAsync_PowerShellShell_ContainsRegisterArgumentCompleter()
    {
        // Act
        var script = await _sut.GenerateCompletionScriptAsync(ShellType.PowerShell);

        // Assert
        script.Should().Contain("Register-ArgumentCompleter");
        script.Should().Contain("$knotCommands");
    }

    [Fact]
    public async Task GenerateCompletionScriptAsync_FishShell_ContainsFishCompleteSyntax()
    {
        // Act
        var script = await _sut.GenerateCompletionScriptAsync(ShellType.Fish);

        // Assert
        script.Should().Contain("complete -c knot");
        script.Should().Contain("install");
    }

    [Fact]
    public async Task GenerateCompletionScriptAsync_UnsupportedShellType_ThrowsKnotVMException()
    {
        // Act
        var act = async () => await _sut.GenerateCompletionScriptAsync(ShellType.Cmd);

        // Assert
        await act.Should().ThrowAsync<KnotVMException>()
            .WithMessage("*non supportato*");
    }

    [Fact]
    public async Task GenerateCompletionScriptAsync_WithInstalledAliases_IncludesAliasesInScript()
    {
        // Arrange
        var installations = new[]
        {
            new Installation("lts-18", "18.20.0", "/versions/lts-18", Use: false),
            new Installation("current", "22.1.0", "/versions/current", Use: true)
        };
        _repositoryMock.Setup(x => x.GetAll()).Returns(installations);

        // Act
        var script = await _sut.GenerateCompletionScriptAsync(ShellType.Bash);

        // Assert
        script.Should().Contain("lts-18");
        script.Should().Contain("current");
    }

    [Fact]
    public async Task GetInstalledAliasesAsync_WithInstallations_ReturnsOrderedAliases()
    {
        // Arrange
        var installations = new[]
        {
            new Installation("zeta", "22.0.0", "/versions/zeta", Use: false),
            new Installation("alpha", "18.0.0", "/versions/alpha", Use: true),
            new Installation("beta", "20.0.0", "/versions/beta", Use: false)
        };
        _repositoryMock.Setup(x => x.GetAll()).Returns(installations);

        // Act
        var aliases = (await _sut.GetInstalledAliasesAsync()).ToList();

        // Assert
        aliases.Should().BeInAscendingOrder();
        aliases.Should().ContainInOrder("alpha", "beta", "zeta");
    }

    [Fact]
    public async Task GetInstalledAliasesAsync_WithNoInstallations_ReturnsEmptyCollection()
    {
        // Arrange
        _repositoryMock.Setup(x => x.GetAll()).Returns([]);

        // Act
        var aliases = await _sut.GetInstalledAliasesAsync();

        // Assert
        aliases.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPopularVersionsAsync_WithNoCacheFile_ReturnsDefaultVersionsAndWritesCache()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(false);

        // Act
        var versions = (await _sut.GetPopularVersionsAsync()).ToList();

        // Assert
        versions.Should().NotBeEmpty();
        versions.Should().Contain("lts");
        versions.Should().Contain("latest");
        _fileSystemMock.Verify(
            x => x.WriteAllTextSafe(
                It.Is<string>(p => p.Contains("completion-versions.txt")),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task GetPopularVersionsAsync_WithValidCacheFile_ReturnsCachedVersions()
    {
        // Arrange
        var cachedContent = "lts\nlatest\n20\n22";
        _fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock
            .Setup(x => x.GetFileLastWriteTime(It.IsAny<string>()))
            .Returns(DateTime.UtcNow.AddHours(-1)); // Cache valida (meno di 24h)
        _fileSystemMock
            .Setup(x => x.ReadAllTextSafe(It.IsAny<string>()))
            .Returns(cachedContent);

        // Act
        var versions = (await _sut.GetPopularVersionsAsync()).ToList();

        // Assert
        versions.Should().Contain("lts");
        versions.Should().Contain("latest");
        versions.Should().Contain("20");
        versions.Should().Contain("22");
        _fileSystemMock.Verify(
            x => x.WriteAllTextSafe(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GetPopularVersionsAsync_WithExpiredCacheFile_RefreshesCache()
    {
        // Arrange
        _fileSystemMock.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
        _fileSystemMock
            .Setup(x => x.GetFileLastWriteTime(It.IsAny<string>()))
            .Returns(DateTime.UtcNow.AddHours(-25)); // Cache scaduta (più di 24h)

        // Act
        await _sut.GetPopularVersionsAsync();

        // Assert - deve riscrivere la cache
        _fileSystemMock.Verify(
            x => x.WriteAllTextSafe(
                It.Is<string>(p => p.Contains("completion-versions.txt")),
                It.IsAny<string>()),
            Times.Once);
    }
}
