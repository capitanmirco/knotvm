using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Exceptions;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using KnotVM.Infrastructure.Services.VersionResolution;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class VersionResolverServiceTests
{
    private static RemoteVersion[] SampleVersions =>
    [
        new("21.6.0", null, "2024-01-15", ["win-x64", "linux-x64"]),
        new("20.11.0", "Iron", "2024-01-09", ["win-x64", "linux-x64"]),
        new("20.10.0", "Iron", "2023-11-22", ["win-x64", "linux-x64"]),
        new("18.19.0", "Hydrogen", "2024-01-09", ["win-x64", "linux-x64"]),
        new("18.18.0", "Hydrogen", "2023-09-18", ["win-x64", "linux-x64"]),
        new("16.20.2", "Gallium", "2023-08-09", ["win-x64", "linux-x64"]),
    ];

    private static Installation[] SampleInstallations =>
    [
        new("my-project", "20.11.0", "/versions/my-project", Use: false),
        new("18.19.0", "18.19.0", "/versions/18.19.0", Use: false),
    ];

    #region ExactVersionStrategy

    [Theory]
    [InlineData("18.2.0")]
    [InlineData("20.11.0")]
    [InlineData("1.0.0")]
    public void ExactVersionStrategy_CanHandle_ReturnsTrueForSemver(string version)
    {
        var strategy = new ExactVersionStrategy();
        strategy.CanHandle(version).Should().BeTrue();
    }

    [Theory]
    [InlineData("20")]
    [InlineData("lts")]
    [InlineData("latest")]
    [InlineData("iron")]
    public void ExactVersionStrategy_CanHandle_ReturnsFalseForNonSemver(string version)
    {
        var strategy = new ExactVersionStrategy();
        strategy.CanHandle(version).Should().BeFalse();
    }

    [Fact]
    public async Task ExactVersionStrategy_Resolve_ReturnsInputUnchanged()
    {
        var strategy = new ExactVersionStrategy();
        var result = await strategy.ResolveAsync("20.11.0");
        result.Should().Be("20.11.0");
    }

    [Fact]
    public async Task ExactVersionStrategy_Resolve_StripsVPrefix()
    {
        var strategy = new ExactVersionStrategy();
        var result = await strategy.ResolveAsync("v20.11.0");
        result.Should().Be("20.11.0");
    }

    #endregion

    #region MajorVersionStrategy

    [Fact]
    public void MajorVersionStrategy_CanHandle_ReturnsTrueForNumber()
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        var strategy = new MajorVersionStrategy(remoteServiceMock.Object);
        strategy.CanHandle("20").Should().BeTrue();
        strategy.CanHandle("18").Should().BeTrue();
    }

    [Fact]
    public void MajorVersionStrategy_CanHandle_ReturnsFalseForNonNumber()
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        var strategy = new MajorVersionStrategy(remoteServiceMock.Object);
        strategy.CanHandle("20.11.0").Should().BeFalse();
        strategy.CanHandle("lts").Should().BeFalse();
    }

    [Fact]
    public async Task MajorVersionStrategy_Resolve_ReturnsLatestInMajor()
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleVersions);

        var strategy = new MajorVersionStrategy(remoteServiceMock.Object);
        var result = await strategy.ResolveAsync("20");

        result.Should().Be("20.11.0"); // Prima della serie 20.x.x nell'index
    }

    [Fact]
    public async Task MajorVersionStrategy_Resolve_ThrowsWhenMajorNotFound()
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleVersions);

        var strategy = new MajorVersionStrategy(remoteServiceMock.Object);

        await strategy.Invoking(s => s.ResolveAsync("99"))
            .Should().ThrowAsync<KnotVMException>()
            .Where(ex => ex.ErrorCode == KnotErrorCode.ArtifactNotAvailable);
    }

    #endregion

    #region LtsVersionStrategy

    [Theory]
    [InlineData("lts")]
    [InlineData("LTS")]
    [InlineData("lts/iron")]
    [InlineData("lts/hydrogen")]
    public void LtsVersionStrategy_CanHandle_ReturnsTrueForLtsInput(string input)
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        var strategy = new LtsVersionStrategy(remoteServiceMock.Object);
        strategy.CanHandle(input).Should().BeTrue();
    }

    [Fact]
    public async Task LtsVersionStrategy_Resolve_LtsKeyword_ReturnsLatestLts()
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleVersions);

        var strategy = new LtsVersionStrategy(remoteServiceMock.Object);
        var result = await strategy.ResolveAsync("lts");

        // Prima versione LTS nell'array (ordinato desc) è 20.11.0 (Iron)
        result.Should().Be("20.11.0");
    }

    [Fact]
    public async Task LtsVersionStrategy_Resolve_LtsCodename_ReturnsLatestInSeries()
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleVersions);

        var strategy = new LtsVersionStrategy(remoteServiceMock.Object);
        var result = await strategy.ResolveAsync("lts/hydrogen");

        result.Should().Be("18.19.0");
    }

    [Fact]
    public async Task LtsVersionStrategy_Resolve_UnknownCodename_ThrowsException()
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleVersions);

        var strategy = new LtsVersionStrategy(remoteServiceMock.Object);

        await strategy.Invoking(s => s.ResolveAsync("lts/unknown"))
            .Should().ThrowAsync<KnotVMException>()
            .Where(ex => ex.ErrorCode == KnotErrorCode.ArtifactNotAvailable);
    }

    #endregion

    #region CodenameStrategy

    [Theory]
    [InlineData("hydrogen")]
    [InlineData("iron")]
    [InlineData("gallium")]
    [InlineData("Hydrogen")]
    public void CodenameStrategy_CanHandle_ReturnsTrueForAlphaString(string input)
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        var strategy = new CodenameStrategy(remoteServiceMock.Object);
        strategy.CanHandle(input).Should().BeTrue();
    }

    [Theory]
    [InlineData("20")]
    [InlineData("20.11.0")]
    [InlineData("lts/iron")]
    public void CodenameStrategy_CanHandle_ReturnsFalseForNonAlpha(string input)
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        var strategy = new CodenameStrategy(remoteServiceMock.Object);
        strategy.CanHandle(input).Should().BeFalse();
    }

    [Fact]
    public async Task CodenameStrategy_Resolve_ReturnsLatestInLtsSeries()
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleVersions);

        var strategy = new CodenameStrategy(remoteServiceMock.Object);
        var result = await strategy.ResolveAsync("hydrogen");

        result.Should().Be("18.19.0");
    }

    [Fact]
    public async Task CodenameStrategy_Resolve_UnknownCodename_ThrowsException()
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleVersions);

        var strategy = new CodenameStrategy(remoteServiceMock.Object);

        await strategy.Invoking(s => s.ResolveAsync("banana"))
            .Should().ThrowAsync<KnotVMException>()
            .Where(ex => ex.ErrorCode == KnotErrorCode.ArtifactNotAvailable);
    }

    #endregion

    #region KeywordStrategy

    [Theory]
    [InlineData("latest")]
    [InlineData("current")]
    [InlineData("Latest")]
    [InlineData("CURRENT")]
    public void KeywordStrategy_CanHandle_ReturnsTrueForKeywords(string input)
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        var strategy = new KeywordStrategy(remoteServiceMock.Object);
        strategy.CanHandle(input).Should().BeTrue();
    }

    [Fact]
    public async Task KeywordStrategy_Resolve_ReturnsNewestVersion()
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleVersions);

        var strategy = new KeywordStrategy(remoteServiceMock.Object);
        var result = await strategy.ResolveAsync("latest");

        result.Should().Be("21.6.0"); // Prima versione nell'index (più recente)
    }

    #endregion

    #region AliasStrategy

    [Fact]
    public void AliasStrategy_CanHandle_ReturnsTrueForInstalledAlias()
    {
        var repositoryMock = new Mock<IInstallationsRepository>();
        repositoryMock.Setup(x => x.GetAll()).Returns(SampleInstallations);

        var strategy = new AliasStrategy(repositoryMock.Object);
        strategy.CanHandle("my-project").Should().BeTrue();
    }

    [Fact]
    public void AliasStrategy_CanHandle_ReturnsFalseForUnknownAlias()
    {
        var repositoryMock = new Mock<IInstallationsRepository>();
        repositoryMock.Setup(x => x.GetAll()).Returns(SampleInstallations);

        var strategy = new AliasStrategy(repositoryMock.Object);
        strategy.CanHandle("not-installed").Should().BeFalse();
    }

    [Fact]
    public async Task AliasStrategy_Resolve_ReturnsVersionForAlias()
    {
        var repositoryMock = new Mock<IInstallationsRepository>();
        repositoryMock.Setup(x => x.GetAll()).Returns(SampleInstallations);

        var strategy = new AliasStrategy(repositoryMock.Object);
        var result = await strategy.ResolveAsync("my-project");

        result.Should().Be("20.11.0");
    }

    #endregion

    #region VersionResolverService

    private VersionResolverService CreateResolverWithRemote(RemoteVersion[] versions, Installation[]? installations = null)
    {
        var remoteServiceMock = new Mock<IRemoteVersionService>();
        remoteServiceMock
            .Setup(x => x.GetAvailableVersionsAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);

        var repositoryMock = new Mock<IInstallationsRepository>();
        repositoryMock.Setup(x => x.GetAll()).Returns(installations ?? []);

        var strategies = new IVersionResolutionStrategy[]
        {
            new ExactVersionStrategy(),
            new AliasStrategy(repositoryMock.Object),
            new MajorVersionStrategy(remoteServiceMock.Object),
            new LtsVersionStrategy(remoteServiceMock.Object),
            new KeywordStrategy(remoteServiceMock.Object),
            new CodenameStrategy(remoteServiceMock.Object),
        };

        return new VersionResolverService(strategies);
    }

    [Theory]
    [InlineData("18.2.0", "18.2.0")]   // Semver esatto
    [InlineData("v20.11.0", "20.11.0")] // Con prefisso v
    public async Task ResolveVersionAsync_ExactSemver_ReturnsInputNormalized(string input, string expected)
    {
        var resolver = CreateResolverWithRemote(SampleVersions);
        var result = await resolver.ResolveVersionAsync(input);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task ResolveVersionAsync_MajorOnly_ReturnsLatestInMajor()
    {
        var resolver = CreateResolverWithRemote(SampleVersions);
        var result = await resolver.ResolveVersionAsync("20");
        result.Should().Be("20.11.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_LtsKeyword_ReturnsLatestLts()
    {
        var resolver = CreateResolverWithRemote(SampleVersions);
        var result = await resolver.ResolveVersionAsync("lts");
        result.Should().Be("20.11.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_LtsWithCodename_ReturnsLatestInSeries()
    {
        var resolver = CreateResolverWithRemote(SampleVersions);
        var result = await resolver.ResolveVersionAsync("lts/hydrogen");
        result.Should().Be("18.19.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_CodenameOnly_ReturnsLatestInLtsSeries()
    {
        var resolver = CreateResolverWithRemote(SampleVersions);
        var result = await resolver.ResolveVersionAsync("iron");
        result.Should().Be("20.11.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_Latest_ReturnsMostRecent()
    {
        var resolver = CreateResolverWithRemote(SampleVersions);
        var result = await resolver.ResolveVersionAsync("latest");
        result.Should().Be("21.6.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_Current_ReturnsMostRecent()
    {
        var resolver = CreateResolverWithRemote(SampleVersions);
        var result = await resolver.ResolveVersionAsync("current");
        result.Should().Be("21.6.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_InstalledAlias_ReturnsAliasVersion()
    {
        var resolver = CreateResolverWithRemote(SampleVersions, SampleInstallations);
        var result = await resolver.ResolveVersionAsync("my-project");
        result.Should().Be("20.11.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_AliasHasPriorityOverRemote()
    {
        // "18.19.0" è sia un alias installato che una versione remota
        var resolver = CreateResolverWithRemote(SampleVersions, SampleInstallations);

        // ExactVersionStrategy handles "18.19.0" (semver), not AliasStrategy
        var result = await resolver.ResolveVersionAsync("18.19.0");
        result.Should().Be("18.19.0");
    }

    [Fact]
    public async Task ResolveVersionAsync_EmptyInput_ThrowsArgumentException()
    {
        var resolver = CreateResolverWithRemote(SampleVersions);

        await resolver.Invoking(r => r.ResolveVersionAsync(""))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveVersionAsync_UnrecognizedFormat_ThrowsKnotVMException()
    {
        var resolver = CreateResolverWithRemote(SampleVersions);

        // "1.2" è parziale: non corrisponde a semver (3 parti) né a numero intero
        await resolver.Invoking(r => r.ResolveVersionAsync("1.2"))
            .Should().ThrowAsync<KnotVMException>()
            .Where(ex => ex.ErrorCode == KnotErrorCode.InvalidVersionFormat);
    }

    [Theory]
    [InlineData("20.11.0", true)]
    [InlineData("v18.2.0", true)]
    [InlineData("20", false)]
    [InlineData("lts", false)]
    [InlineData("latest", false)]
    public void IsExactVersion_VariousInputs_ReturnsCorrectResult(string input, bool expected)
    {
        var resolver = CreateResolverWithRemote(SampleVersions);
        resolver.IsExactVersion(input).Should().Be(expected);
    }

    #endregion
}
