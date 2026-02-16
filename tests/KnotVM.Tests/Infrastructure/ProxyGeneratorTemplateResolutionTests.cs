using FluentAssertions;
using KnotVM.Core.Enums;
using KnotVM.Core.Interfaces;
using KnotVM.Infrastructure.Services;
using Moq;
using Xunit;

namespace KnotVM.Tests.Infrastructure;

public class ProxyGeneratorTemplateResolutionTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _tempBinPath;
    private readonly string _emptyTemplatesPath;

    public ProxyGeneratorTemplateResolutionTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"knotvm-proxy-template-tests-{Guid.NewGuid():N}");
        _tempBinPath = Path.Combine(_tempRoot, "bin");
        _emptyTemplatesPath = Path.Combine(_tempRoot, "templates");
        Directory.CreateDirectory(_tempBinPath);
        Directory.CreateDirectory(_emptyTemplatesPath);
    }

    [Fact]
    public void ProxyGenerator_ShouldFallbackToRepositoryTemplates_WhenConfiguredTemplatesMissing()
    {
        var platformServiceMock = new Mock<IPlatformService>();
        platformServiceMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.Windows);

        var pathServiceMock = new Mock<IPathService>();
        pathServiceMock.Setup(x => x.GetBinPath()).Returns(_tempBinPath);
        pathServiceMock.Setup(x => x.GetTemplatesPath()).Returns(_emptyTemplatesPath);
        pathServiceMock.Setup(x => x.GetSettingsFilePath()).Returns(Path.Combine(_tempRoot, "settings.txt"));
        pathServiceMock.Setup(x => x.GetVersionsPath()).Returns(Path.Combine(_tempRoot, "versions"));

        var fileSystemMock = new Mock<IFileSystemService>();

        var service = new ProxyGeneratorService(platformServiceMock.Object, pathServiceMock.Object, fileSystemMock.Object);

        service.GenerateGenericProxy("node", "node.exe");

        var proxyFile = Path.Combine(_tempBinPath, "nlocal-node.cmd");
        File.Exists(proxyFile).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
