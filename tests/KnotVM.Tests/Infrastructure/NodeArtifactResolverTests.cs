using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Core.Models;
using KnotVM.Infrastructure.Services;
using Moq;

namespace KnotVM.Tests.Infrastructure;

public class NodeArtifactResolverTests
{
    [Theory]
    [InlineData(HostOs.Windows, ".zip")]
    [InlineData(HostOs.Linux, ".tar.xz")]
    [InlineData(HostOs.MacOS, ".tar.gz")]
    public void GetArtifactExtension_ShouldReturnExpectedExtension(HostOs os, string expectedExtension)
    {
        var platformMock = CreatePlatformMock(os, HostArch.X64);
        var resolver = new NodeArtifactResolver(platformMock.Object);

        var extension = resolver.GetArtifactExtension();

        extension.Should().Be(expectedExtension);
    }

    [Fact]
    public void GetArtifactFileName_Linux_ShouldUseTarXz()
    {
        var platformMock = CreatePlatformMock(HostOs.Linux, HostArch.X64);
        var resolver = new NodeArtifactResolver(platformMock.Object);

        var fileName = resolver.GetArtifactFileName("20.11.0");

        fileName.Should().Be("node-v20.11.0-linux-x64.tar.xz");
    }

    [Fact]
    public void IsArtifactAvailable_ShouldMatchPlatformString()
    {
        var platformMock = CreatePlatformMock(HostOs.Linux, HostArch.Arm64);
        var resolver = new NodeArtifactResolver(platformMock.Object);
        var remote = new RemoteVersion(
            Version: "20.11.0",
            Lts: "Iron",
            Date: "2024-01-01",
            Files: ["win-x64", "linux-arm64", "darwin-x64"]);

        var available = resolver.IsArtifactAvailable(remote);

        available.Should().BeTrue();
    }

    private static Mock<IPlatformService> CreatePlatformMock(HostOs os, HostArch arch)
    {
        var mock = new Mock<IPlatformService>();
        mock.Setup(x => x.GetCurrentOs()).Returns(os);
        mock.Setup(x => x.GetCurrentArch()).Returns(arch);
        return mock;
    }
}

