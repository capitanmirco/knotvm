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
        
        // Copia template dalla repository alla directory temporanea
        CopyTemplatesFromRepository();
    }
    
    private void CopyTemplatesFromRepository()
    {
        // Trova directory templates dalla root del repository
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var repoTemplatesPath = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", "..", "templates"));
        
        if (Directory.Exists(repoTemplatesPath))
        {
            foreach (var templateFile in Directory.GetFiles(repoTemplatesPath, "*.template"))
            {
                var destFile = Path.Combine(_emptyTemplatesPath, Path.GetFileName(templateFile));
                File.Copy(templateFile, destFile, overwrite: true);
            }
        }
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

        // Usa FileSystemService reale invece di mock
        var fileSystem = new FileSystemService(platformServiceMock.Object);

        var service = new ProxyGeneratorService(platformServiceMock.Object, pathServiceMock.Object, fileSystem);

        service.GenerateGenericProxy("node", "node.exe");

        var proxyFile = Path.Combine(_tempBinPath, "nlocal-node.cmd");
        File.Exists(proxyFile).Should().BeTrue();
    }

    [Fact]
    public void RemoveAllProxies_Unix_ShouldKeepKnotBinaryAndNonManagedFiles()
    {
        var platformServiceMock = new Mock<IPlatformService>();
        platformServiceMock.Setup(x => x.GetCurrentOs()).Returns(HostOs.MacOS);

        var pathServiceMock = new Mock<IPathService>();
        pathServiceMock.Setup(x => x.GetBinPath()).Returns(_tempBinPath);
        pathServiceMock.Setup(x => x.GetTemplatesPath()).Returns(_emptyTemplatesPath);
        pathServiceMock.Setup(x => x.GetSettingsFilePath()).Returns(Path.Combine(_tempRoot, "settings.txt"));
        pathServiceMock.Setup(x => x.GetVersionsPath()).Returns(Path.Combine(_tempRoot, "versions"));

        var fileSystem = new FileSystemService(platformServiceMock.Object);
        var service = new ProxyGeneratorService(platformServiceMock.Object, pathServiceMock.Object, fileSystem);

        var knotBinary = Path.Combine(_tempBinPath, "knot");
        var nodeWrapper = Path.Combine(_tempBinPath, "node");
        var isolatedProxy = Path.Combine(_tempBinPath, "nlocal-node");
        var userTool = Path.Combine(_tempBinPath, "custom-tool");

        File.WriteAllText(knotBinary, "knot-binary");
        File.WriteAllText(nodeWrapper, "node-wrapper");
        File.WriteAllText(isolatedProxy, "isolated-proxy");
        File.WriteAllText(userTool, "user-tool");

        service.RemoveAllProxies();

        File.Exists(knotBinary).Should().BeTrue();
        File.Exists(userTool).Should().BeTrue();
        File.Exists(nodeWrapper).Should().BeFalse();
        File.Exists(isolatedProxy).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
